using System.Linq;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewriteReadStream(Stream inner, IReadOnlyCollection<Replacer> replacers) : DelegatingStream(inner)
    {
        private ReadOnlyMemory<byte> _internalBuffer = new();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var initialResult = 0;

            if (!_internalBuffer.IsEmpty)
            {
                _internalBuffer.CopyTo(buffer);
                buffer = buffer.Slice(_internalBuffer.Length);
                initialResult += _internalBuffer.Length;
                _internalBuffer = new();
            }

            initialResult += await base.ReadAsync(buffer, cancellationToken);

            if (initialResult <= 0) return initialResult;

            IEnumerable<(int Index, Replacer Replacer)> GetMatches()
            {
                foreach (var replacer in replacers ?? [])
                {
                    var from = replacer.LocalBytes.Span;
                    var index = buffer.Span.IndexOf(from);
                    if (index != -1) yield return (index, replacer);
                }
            }

            var result = initialResult;

            while (GetMatches().OrderBy(x => x.Index).FirstOrDefault() is { Replacer: var replacer, Index: var index }
                    && replacer != null && index != -1)
            {
                result += ReplaceBytes(buffer, replacer, index, initialResult);
            }

            return result;
        }

        private int ReplaceBytes(Memory<byte> buffer, Replacer replacer, int index, int initialBytesWritten)
        {
            var from = replacer.LocalBytes;
            var to = replacer.RemoteBytes;

            var result = 0;

            if (from.Length > to.Length)
            {
                // de bytearray die we gaan vervangen is langer dan de bytearray waarmee deze vervangen wordt
                // we korten de buffer daarom eerst in
                var diff = from.Length - to.Length;
                // pak de bytes waar we overheen moeten schrijven, de bytes VOOR de gevonden match
                var copyTo = buffer.Slice(index);
                // pak de bytes die we moeten verschuiven 
                var copyFrom = copyTo.Slice(diff);
                // verschuif de bytes naar links
                copyFrom.CopyTo(copyTo);
                // van het totaal aantal bytes moeten we het verschil afhalen, want de uiteindelijke buffer wordt kleiner
                // de lezer van deze stream weet dan dat de overige bytes niet gelezen hoeven te worden
                result = -diff;
            }
            else if (from.Length < to.Length)
            {
                // de bytearray die we gaan vervangen is korter dan de bytearray waarmee deze vervangen wordt
                // de buffer moet dus groter worden
                var diff = to.Length - from.Length;
                // pak de bytes die daadwerkelijk in de buffer gezet zijn. de rest van de buffer is namelijk leeg.
                var max = buffer.Slice(0, initialBytesWritten);
                // als de totale buffer niet groot genoeg is om de bytes op te schuiven, moeten we replacen wat we kunnen
                // dan houden we een interne buffer bij, zodat we bij de volgende read poging eerst het restant kunnen toevoegen
                if (max.Length < buffer.Length + diff)
                {
                    var bufferDif = buffer.Length + diff - max.Length;
                    _internalBuffer = to.Slice(bufferDif);
                    to = to.Slice(0, bufferDif);
                }
                // pak de bytes die we door moeten schuiven, namelijk de bytes NA de gevonden match
                var copyFrom = max.Slice(index + from.Length);
                // pak het stuk waar de bytes naartoe moeten
                var copyTo = buffer.Slice(index + to.Length);
                // schuif de bytes door naar rechts
                copyFrom.CopyTo(copyTo);
                // bij het totaal aantal bytes moeten we het verschil optellen, want de uiteindelijke buffer wordt groter
                // de lezer van deze stream weet dan dat de toegevoegde bytes ook gelezen moeten worden
                result = diff;
            }

            to.CopyTo(buffer.Slice(index));

            return result;
        }
    }
}
