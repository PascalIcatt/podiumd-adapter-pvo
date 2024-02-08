namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public sealed class UrlRewriteReadStream : DelegatingStream
    {
        private readonly UrlRewriterCollection _rewriters;

        // we hebben twee stukjes interne buffer nodig.
        // deze gebruiken we in twee scenario's:
        //
        // Scenario 1:
        //   Na het vervangen van de urls is er niet genoeg ruimte in de buffer die als parameter is meegegeven aan ReadAsync.
        //   dan houden we de url die we willen invoegen vast in _internalBufferPart1
        //   dan houden we de nog niet verwerkte bytes vast in _internalBufferPart1
        //
        // Scenario 2:
        //   Het laatste stuk van de buffer bevat het begin van een van de urls die we willen vervangen
        //   in dat scenario kan het zijn dat de volgende call naar ReadAsync het restant van die url bevat
        //   daarom houden we in dat geval dat laatste stuk van de buffer vast in _internalBufferPart1
        private ReadOnlyMemory<byte> _internalBufferPart1;
        private ReadOnlyMemory<byte> _internalBufferPart2;

        public UrlRewriteReadStream(Stream inner, UrlRewriterCollection rewriters) : base(inner)
        {
            _rewriters = rewriters;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // soms is de buffer leeg. dat gebeurt bijvoorbeeld als performance optimalizatie in YARP
            // in dat geval geven we het verzoek rechtstreeks door aan de onderliggende stream
            if (buffer.IsEmpty) return base.ReadAsync(buffer, cancellationToken);


            var bytesWritten = 0;

            var unwrittenBuffer = buffer;

            // eerst schrijven we de intern bijgehouden buffers weg, als die er zijn
            if (!_internalBufferPart1.IsEmpty)
            {
                _internalBufferPart1.CopyTo(unwrittenBuffer);
                unwrittenBuffer = buffer.Slice(_internalBufferPart1.Length);
                bytesWritten += _internalBufferPart1.Length;
                _internalBufferPart1 = new();
            }

            if (!_internalBufferPart2.IsEmpty)
            {
                _internalBufferPart2.CopyTo(unwrittenBuffer);
                unwrittenBuffer = buffer.Slice(_internalBufferPart2.Length);
                bytesWritten += _internalBufferPart2.Length;
                _internalBufferPart2 = new();
            }

            // nu mag de onderliggende stream gaan schrijven naar de buffer,
            // maar alleen naar het deel waar we zelf nog niet naartoe hebben geschreven
            return base.ReadAsync(unwrittenBuffer, cancellationToken)
                .ContinueWith(bytesWrittenByUnderlyingStream =>
                    // de replace functie heeft de gehele buffer nodig:
                    // - het stuk waar we onze interne buffers naartoe hebben geschreven
                    // - het stuk waar de onderliggende stream naartoe heeft geschreven
                    // - het stuk dat nog niet beschreven is, voor het geval de body langer wordt door het vervangen van urls
                    Replace(buffer, bytesWritten + bytesWrittenByUnderlyingStream));
        }

        private int Replace(Memory<byte> fullBuffer, int bytesWritten)
        {
            // als er helemaal geen bytes geschreven zijn naar de buffer, hoeven we er niks mee
            if (bytesWritten <= 0) return bytesWritten;

            // de buffer komt uit een pool van eerder gebruikte arrays. dit is een performance optimalizatie
            // de buffer kan daarom verder door lopen dan alleen de inhoud die geschreven is door de onderliggende stream
            // de rest van de buffer bevat dan bytes waar we niks mee kunnen / mogen doen
            // daarom pakken we hier het stuk van de buffer waar daadwerkelijk in geschreven is door de onderliggende stream
            var bufferToSearchIn = fullBuffer.Span.Slice(0, bytesWritten);

            // zoek eerst op de te vervangen base url in de buffer.
            // dit doen we om onnodige loops door de rewriters te voorkomen:
            // als de base url al niet matcht, zal ook geen enkele volledige url matchen
            while (bufferToSearchIn.IndexOf(_rewriters.LocalBaseUrlBytes.Span) is int index && index > -1)
            {
                var foundFullMatch = false;
                bufferToSearchIn = bufferToSearchIn.Slice(index);
                fullBuffer = fullBuffer.Slice(index);
                // loop nu door de rewriters om de juiste url te vinden
                foreach (var rewriter in _rewriters)
                {
                    foundFullMatch = bufferToSearchIn.StartsWith(rewriter.LocalFullBytes.Span);
                    if (!foundFullMatch)
                    {
                        continue;
                    }
                    // vervang de url en pas het aantal geschreven bytes daarop aan
                    if (Replace(rewriter, ref fullBuffer, ref bufferToSearchIn, ref bytesWritten))
                    {
                        // het vervangen is gelukt, break uit de foreach loop en ga verder binnen de while loop
                        break;
                    }

                    // het replacen is niet gelukt, waarschijnlijk omdat te buffer te klein is.
                    // stop helemaal met replacen en return het aantal geschreven bytes
                    return bytesWritten;
                }
                if (!foundFullMatch)
                {
                    // we vinden een match op de base url, maar niet op een volledige url.
                    // mogelijk hebben we de volgende buffer nodig.
                    // break uit de while loop om dat scenario af te vangen.
                    break;
                }
            }

            HandlePartialMatches(ref bytesWritten, bufferToSearchIn);

            return bytesWritten;
        }

        private bool Replace(UrlRewriter replacer, ref Memory<byte> fullBuffer, ref Span<byte> bufferToSearchIn, ref int bytesWritten)
        {
            // in het geval van een ReadStream replacen we inline:
            // we hebben een stuk schrijfbaar geheugen waar al in geschreven is
            // als we urls moeten vervangen, moeten we dus mogelijk bytes verschuiven binnen de buffer
            var from = replacer.LocalFullBytes;
            var to = replacer.RemoteFullBytes;

            var diff = to.Length - from.Length;
            var unwritten = fullBuffer.Length - bufferToSearchIn.Length;

            if (diff > unwritten)
            {
                // de bytearray die we gaan vervangen is korter dan de bytearray waarmee deze vervangen wordt
                // en we hebben niet genoeg ruimte over in de buffer om de bytes op te schuiven
                // dan houden we een interne buffer bij, zodat we bij de volgende read poging eerst het restant kunnen toevoegen
                var writtenMemory = fullBuffer.Slice(0, bufferToSearchIn.Length);
                _internalBufferPart1 = to;
                _internalBufferPart2 = writtenMemory.Slice(from.Length);
                bytesWritten -= writtenMemory.Length;

                // stop verdere verwerking
                return false;
            }

            if (diff > 0)
            {
                // de bytearray die we gaan vervangen is korter dan de bytearray waarmee deze vervangen wordt
                // en we hebben genoeg ruimte in de totale buffer om de bytes op te schuiven
                // pak de bytes die we door moeten schuiven, namelijk de bytes NA de gevonden match
                var copyFrom = bufferToSearchIn.Slice(from.Length);
                // pak het stuk waar de bytes naartoe moeten
                var copyTo = fullBuffer.Span.Slice(to.Length);
                // schuif de bytes door naar rechts
                copyFrom.CopyTo(copyTo);
            }

            if (diff < 0)
            {
                // de bytearray die we gaan vervangen is langer dan de bytearray waarmee deze vervangen wordt
                // we korten de buffer daarom eerst in
                // pak de bytes die we moeten verschuiven 
                var copyFrom = bufferToSearchIn.Slice(-diff);
                // verschuif de bytes naar links
                copyFrom.CopyTo(bufferToSearchIn);
            }

            to.Span.CopyTo(fullBuffer.Span);
            bytesWritten += diff;
            unwritten -= diff;
            fullBuffer = fullBuffer.Slice(to.Length);
            bufferToSearchIn = fullBuffer.Span[..^unwritten];
            return true;
        }

        // Deze functie ondervangt een edge case:
        // Als de buffer eindigt met met het begin van een url die we willen vervangen,
        // Kan het zijn dat de rest van die url doorkomt in de volgende buffer
        // In dat geval houden we dat begin van die url vast in _internalBufferPart1,
        // En halen we dat aantal bytes af van bytesWritten,
        // zodat degene die de stream aan het lezen is weet dat die bytes niet gebruikt moeten worden
        private void HandlePartialMatches(ref int bytesWritten, Span<byte> usedPartOftheBuffer)
        {
            var lengthOfPartialMatch = 0;
            UrlRewriter? rewriterToBuffer = null;

            foreach (var rewriter in _rewriters)
            {
                if (usedPartOftheBuffer.MightMatchInNextBuffer(rewriter.LocalFullBytes.Span, ref lengthOfPartialMatch))
                {
                    rewriterToBuffer = rewriter;
                }
            }

            if (rewriterToBuffer != null)
            {
                _internalBufferPart1 = rewriterToBuffer.LocalFullBytes.Slice(0, lengthOfPartialMatch);
                bytesWritten -= lengthOfPartialMatch;
            }
        }
    }
}
