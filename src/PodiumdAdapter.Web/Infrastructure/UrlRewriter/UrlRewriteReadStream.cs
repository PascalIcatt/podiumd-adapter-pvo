namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public sealed class UrlRewriteReadStream : DelegatingStream
    {
        private readonly UrlRewriterCollection _rewriters;
        private ReadOnlyMemory<byte> _internalBuffer;

        public UrlRewriteReadStream(Stream inner, UrlRewriterCollection rewriters): base(inner)
        {
            _rewriters = rewriters;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var valueTask = base.ReadAsync(buffer, cancellationToken);
            
            if (valueTask.IsCompletedSuccessfully)
            {
                var bytesWritten = Read(buffer, valueTask.Result);
                return new(bytesWritten);
            }
            
            var task = valueTask.AsTask()
                .ContinueWith(t => Read(buffer, t.Result));
            
            return new(task);
        }

        private int Read(Memory<byte> buffer, int bytesWritten)
        {
            if (!_internalBuffer.IsEmpty)
            {
                _internalBuffer.CopyTo(buffer);
                buffer = buffer.Slice(_internalBuffer.Length);
                bytesWritten += _internalBuffer.Length;
                _internalBuffer = new();
            }

            if (bytesWritten <= 0) return bytesWritten;

            while (buffer.Span.IndexOf(_rewriters.LocalBaseUrlBytes.Span) is int index && index > -1)
            {
                var found = false;
                foreach (var rewriter in _rewriters)
                {
                    var slice = buffer.Span.Slice(index);
                    if (slice.StartsWith(rewriter.LocalFullBytes.Span))
                    {
                        bytesWritten += ReplaceBytes(buffer, rewriter, index, bytesWritten);
                        found = true;
                        break;
                    }
                }
                if (!found) return bytesWritten;
            }

            return bytesWritten;
        }

        private int ReplaceBytes(Memory<byte> buffer, UrlRewriter replacer, int index, int initialBytesWritten)
        {
            var from = replacer.LocalFullBytes;
            var to = replacer.RemoteFullBytes;

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
                if (max.Length + diff > buffer.Length)
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
