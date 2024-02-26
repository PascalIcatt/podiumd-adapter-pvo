using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter.Internal
{
    /// <summary>
    /// UrlRewritePipeWriter class that extends DelegatingPipeWriter to modify URLs in a PipeWriter stream
    /// </summary>
    /// <param name="inner">The inner <see cref="PipeWriter"/>, for example the one that writes to the <see cref="HttpResponse"/> Body</param>
    /// <param name="rewriterCollection"></param>
    public sealed class UrlRewritePipeWriter : DelegatingPipeWriter
    {
        private readonly UrlRewriteMapCollection _rewriterCollection;
        private ReadOnlyMemory<byte> _internalBuffer;

        public UrlRewritePipeWriter(PipeWriter inner, UrlRewriteMapCollection rewriterCollection) : base(inner)
        {
            _rewriterCollection = rewriterCollection;
        }

        public override ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            if (source.IsEmpty) return base.WriteAsync(source, cancellationToken);
            var sourceSpan = source.Span;

            // eerst kijken we of we nog wat weg te schrijven hebben uit de interne buffer
            var spanToPrepend = HandleInternalBuffer(ref sourceSpan);

            // het aantal bytes dat we moeten gaan schrijven is minimaal gelijk aan de hiervoor opgehaalde buffer
            var byteCountToWrite = spanToPrepend.Length;

            // vervolgens hogen we dat aantal bytes op aan de hand van de ShouldReplace functie
            var shouldReplace = ShouldReplace(ref sourceSpan, ref byteCountToWrite);

            // vraag een stuk schrijfbaar geheugen van de onderliggende pipewriter
            // deze is lang genoeg om onze bytes naar weg te schrijven en mogelijk langer
            var targetSpan = GetSpan(byteCountToWrite);

            // schrijf nu eerst de interne buffer weg...
            spanToPrepend.CopyTo(targetSpan);
            // en schuif het schrijfbare geheugen door
            targetSpan = targetSpan.Slice(spanToPrepend.Length);

            // zolang we bytes moeten vervangen...
            while (shouldReplace && TryGetNextReplacement(sourceSpan, out var index, out var rewriter))
            {
                if (index > 0)
                {
                    // kopieer eerst het stuk voor de index, als dat er is
                    sourceSpan[..index].CopyAndMoveForward(ref targetSpan);
                }
                // kopieer nu de vervangende bytes
                rewriter.ToFullBytes.Span.CopyAndMoveForward(ref targetSpan);
                var nextIndex = index + rewriter.FromFullBytes.Length;
                // kort de span in
                sourceSpan = sourceSpan.Slice(nextIndex);
            }

            // we zijn klaar. kopieer de overige bytes
            sourceSpan.CopyTo(targetSpan);

            // licht de onderliggende pipewriter in hoeveel bytes we weggeschreven hebben
            Advance(byteCountToWrite);

            // de bytes mogen gelijk geflusht worden naar de response body
            return FlushAsync(cancellationToken);
        }

        // deze functie checkt of er urls vervangen moeten worden,
        // en rekent uit hoeveel bytes er uiteindelijk weggeschreven moeten worden.
        bool ShouldReplace(ref ReadOnlySpan<byte> originalBuffer, ref int size)
        {
            var source = originalBuffer;
            // standaard gaan we er vanuit dat de hele buffer weggeschreven moet worden
            size += source.Length;
            var result = false;
            var baseUrlSpan = _rewriterCollection.FromBaseUrlBytes.Span;

            // optimalisatie:
            // als we al geen match hebben op de base url,
            // gaan we zeker geen match vinden op de hele url.
            // dan hoeven we niet door de rewriters te loopen.
            while (source.IndexOf(baseUrlSpan) is int index && index > -1)
            {
                source = source.Slice(index);
                var foundFullMatch = false;

                foreach (var r in _rewriterCollection)
                {
                    var from = r.FromFullBytes.Span;
                    if (source.StartsWith(from))
                    {
                        // we hebben een match op de volledige url
                        var to = r.ToFullBytes.Span;
                        var diff = to.Length - from.Length;
                        // als de te vervangen bytes langer zijn dan de vervangende bytes,
                        // hebben we meer geheugen nodig om naar toe te schrijven.
                        // als de te vervangen bytes korter zijn dan de vervangende bytes,
                        // hebben we minder geheugen nodig om naar toe te schrijven.
                        size += diff;
                        result = true;
                        foundFullMatch = true;
                        source = source.Slice(from.Length);
                        break;
                    }
                }
                if (!foundFullMatch)
                {
                    // we vinden een match op de base url, maar niet op een volledige url.
                    // mogelijk hebben we de volgende buffer nodig.
                    // break uit de while loop om dat scenario af te vangen.
                    break;
                }
            }

            HandlePartialMatch(ref originalBuffer, ref size, source);

            return result;
        }

        bool TryGetNextReplacement(ReadOnlySpan<byte> source, out int index, [NotNullWhen(true)] out UrlRewriteMap? rewriter)
        {
            // optimalisatie:
            // als we al geen match hebben op de base url,
            // gaan we zeker geen match vinden op de hele url.
            // dan hoeven we niet door de rewriters te loopen.
            index = source.IndexOf(_rewriterCollection.FromBaseUrlBytes.Span);
            rewriter = null;

            if (index < 0) return false;

            source = source.Slice(index);

            foreach (var r in _rewriterCollection)
            {
                if (source.StartsWith(r.FromFullBytes.Span))
                {
                    // we hebben een match op de volledige url
                    rewriter = r;
                    return true;
                }
            }

            return false;
        }

        // Deze functie ondervangt een edge case:
        // Als de buffer eindigt met met het begin van een url die we willen vervangen,
        // Kan het zijn dat de rest van die url doorkomt in de volgende buffer
        // In dat geval houden we dat begin van die url vast in _internalBuffer
        // En halen we dat aantal bytes af van size,
        // zodat degene die de response aan het schrijven is, weet dat die bytes niet gebruikt moeten worden
        private void HandlePartialMatch(ref ReadOnlySpan<byte> originalBytes, ref int size, ReadOnlySpan<byte> source)
        {
            var lengthOfPartialMatch = 0;
            UrlRewriteMap? rewriterToPutInInternalBuffer = null;

            foreach (var r in _rewriterCollection)
            {
                var span = r.FromFullBytes.Span;
                if (source.MightMatchInNextBuffer(span, ref lengthOfPartialMatch))
                {
                    rewriterToPutInInternalBuffer = r;
                }
            }

            if (rewriterToPutInInternalBuffer != null)
            {
                // de interne buffer wordt het deel van de te vervangen bytes dat aan het einde van de oorspronkelijke bytes staat
                _internalBuffer = rewriterToPutInInternalBuffer.FromFullBytes.Slice(0, lengthOfPartialMatch);
                // we korten de oorspronkelijke bytes in: het laatste deel moet n og niet weggeschreven worden
                originalBytes = originalBytes.Slice(0, originalBytes.Length - lengthOfPartialMatch);
                // heet aantal bytes korten we op dezelfde manier in,
                // zodat degene die de response aan het schrijven is, weet dat die bytes niet gebruikt moeten worden
                size -= lengthOfPartialMatch;
            }
        }

        // Deze functie handelt de buffer af die we de vorige keer hebben vastgehouden met de HandlePartialMatch functie.
        private ReadOnlySpan<byte> HandleInternalBuffer(ref ReadOnlySpan<byte> sourceSpan)
        {
            if (_internalBuffer.IsEmpty)
            {
                return [];
            }

            var result = _internalBuffer.Span;
            _internalBuffer = new();

            var bufferLength = result.Length;

            foreach (var rewriter in _rewriterCollection)
            {
                var remoteSpan = rewriter.FromFullBytes.Span;
                if (remoteSpan.Length < bufferLength) continue;

                // het stuk dat mogelijk in de interne buffer zit
                var firstPart = remoteSpan.Slice(0, bufferLength);
                // het stuk dat mogelijk aan het begin van de nieuwe bytes zit
                var secondPart = remoteSpan.Slice(bufferLength);

                // als de interne buffer matcht met eerste deel van de bytes waar we op zoeken..
                if (result.SequenceEqual(firstPart)
                    // en de nieuwe bytes beginnen met het tweede deel van de bytes waar we op zoeken...
                    && sourceSpan.StartsWith(secondPart))
                {
                    // dan hebben we een match.
                    // we moeten dan de nieuwe bytes inkorten...
                    sourceSpan = sourceSpan.Slice(secondPart.Length);
                    // en de vervangende bytes als resultaat teruggeven
                    // zodat we die eerst kunnen wegschrijven voordat we de nieuwe bytes wegschrijven
                    return rewriter.ToFullBytes.Span;
                }
            }

            // als we geen match hebben gevonden,
            // moeten we de bytes die we hebben vastgehouden van de vorige keer alsnog as-is wegschrijven
            return result;
        }
    }
}
