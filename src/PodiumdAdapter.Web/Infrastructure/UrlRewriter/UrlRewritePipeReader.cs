namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewriteReadStream(Stream inner, UrlRewriterCollection rewriters) : Stream
    {
        private ReadOnlyMemory<byte> _internalBuffer;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var valueTask = inner.ReadAsync(buffer, cancellationToken);
            
            if (valueTask.IsCompletedSuccessfully)
            {
                var bytesWritten = Read(buffer, valueTask.Result);
                return new(bytesWritten);
            }
            
            var task = valueTask.AsTask()
                .ContinueWith(t => Read(buffer, t.Result));
            
            return new(task);
        }

        public int Read(Memory<byte> buffer, int bytesWritten)
        {
            if (!_internalBuffer.IsEmpty)
            {
                _internalBuffer.CopyTo(buffer);
                buffer = buffer.Slice(_internalBuffer.Length);
                bytesWritten += _internalBuffer.Length;
                _internalBuffer = new();
            }

            if (bytesWritten <= 0) return bytesWritten;

            while (buffer.Span.IndexOf(rewriters.LocalBaseUrlBytes.Span) is int index && index > -1)
            {
                var found = false;
                foreach (var rewriter in rewriters)
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

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => inner.Length;

        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override void Flush()
        {
            inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
        }
    }

    ///// <summary>
    ///// UrlRewritePipeReader class that extends DelegatingPipeReader to modify URLs in a PipeReader stream
    ///// </summary>
    ///// <param name="reader">The inner <see cref="PipeReader"/>, for example the one that reads the <see cref="HttpRequest"/> Body</param>
    ///// <param name="rewriterCollection"></param>
    //public class UrlRewritePipeReader(PipeReader reader, UrlRewriterCollection rewriterCollection) : DelegatingPipeReader(reader)
    //{
    //    // Stores the previous, current, and next buffer sequences for processing
    //    private ReadOnlyMemory<byte> _replacedByMemory = new();
    //    private ReadOnlySequence<byte> _sequenceThatWasReplaced;
    //    private ReadResult _originalResult = new();
    //    private ReadOnlySequence<byte> _unreadSequence = new();
    //    private bool _shouldAdvance = true;

    //    // Overridden ReadAsync method to process and rewrite URLs in the stream
    //    public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
    //    {
    //        // Checks if there is a buffer available and returns it if so
    //        if (!_replacedByMemory.IsEmpty && false)
    //        {
    //            var sequence = new ReadOnlySequence<byte>(_replacedByMemory);
    //            var isComplete = _unreadSequence.IsEmpty && _originalResult.IsCompleted;
    //            var result = new ReadResult(sequence, false, isComplete);
    //            _replacedByMemory = new();
    //            _shouldAdvance = false;
    //            return new(result);
    //        }

    //        var valueTask = base.ReadAsync(cancellationToken);

    //        // Directly returns the processed result if already completed
    //        if (valueTask.IsCompleted)
    //        {
    //            var result = Handle(valueTask.Result);
    //            return new(result);
    //        }

    //        // Continues processing the result asynchronously if not completed
    //        return HandleAsync(valueTask);

    //        async ValueTask<ReadResult> HandleAsync(ValueTask<ReadResult> task)
    //        {
    //            var result = await task;
    //            return Handle(result);
    //        }

    //        // Local function to handle the read result
    //        ReadResult Handle(ReadResult readResult)
    //        {
    //            // Returns the original result if canceled or unable to read
    //            if (readResult.IsCanceled || !TryRead(readResult.Buffer, out var buffer))
    //            {
    //                return readResult;
    //            }

    //            _originalResult = readResult;

    //            // Returns a new ReadResult with the processed buffer
    //            return new ReadResult(buffer, false, false);
    //        }
    //    }

    //    // Overridden AdvanceTo methods to handle advancing the buffer(s) first
    //    public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
    //    {
    //        if (ShouldAdvance())
    //        {
    //            base.AdvanceTo(consumed, examined);
    //        }
    //    }

    //    // Overridden AdvanceTo methods to handle advancing the buffer(s) first
    //    public override void AdvanceTo(SequencePosition consumed)
    //    {
    //        if (ShouldAdvance())
    //        {
    //            base.AdvanceTo(consumed);
    //        }
    //    }

    //    // Tries to read and process the read result, replacing URLs as needed
    //    private bool TryRead(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> readSequence)
    //    {
    //        readSequence = default;

    //        if (buffer.IsEmpty)
    //        {
    //            return false;
    //        }

    //        // Creates a SequenceReader for the buffer
    //        var reader = new SequenceReader<byte>(buffer);

    //        // Iterates through the rewriters to find and replace matching sequences
    //        foreach (var rewriter in rewriterCollection)
    //        {
    //            var spanToReplace = rewriter.LocalFullBytes.Span;
    //            if (reader.TryReadTo(out readSequence, spanToReplace, false))
    //            {

    //                _replacedByMemory = rewriter.RemoteFullBytes;
    //                _sequenceThatWasReplaced = reader.UnreadSequence.Slice(0, spanToReplace.Length);
    //                _unreadSequence = reader.UnreadSequence.Slice(spanToReplace.Length);
    //                return true;
    //            }
    //        }

    //        return false;
    //    }

    //    // Tries to advance to the buffered sequence(s) if available
    //    private bool ShouldAdvance()
    //    {
    //        if (!_shouldAdvance)
    //        {
    //            _shouldAdvance = true;
    //            return false;
    //        }
    //        return true;
    //    }
    //}
}
