using System.Buffers;
using System.IO.Pipelines;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    /// <summary>
    /// UrlRewritePipeReader class that extends DelegatingPipeReader to modify URLs in a PipeReader stream
    /// </summary>
    /// <param name="reader">The inner <see cref="PipeReader"/>, for example the one that reads the <see cref="HttpRequest"/> Body</param>
    /// <param name="rewriterCollection"></param>
    public class UrlRewritePipeReader(PipeReader reader, UrlRewriterCollection rewriterCollection) : DelegatingPipeReader(reader)
    {
        // Stores the previous, current, and next buffer sequences for processing
        private ReadOnlyMemory<byte> _replacedByMemory = new();
        private ReadResult _originalResult = new();
        private ReadOnlySequence<byte> _unreadSequence = new();
        private bool _replacing;

        // Overridden ReadAsync method to process and rewrite URLs in the stream
        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            // Checks if there is a buffer available and returns it if so
            if (!_replacedByMemory.IsEmpty)
            {
                var sequence = new ReadOnlySequence<byte>(_replacedByMemory);
                _replacing = !_unreadSequence.IsEmpty;
                var isComplete = !_replacing && _originalResult.IsCompleted;
                var result = new ReadResult(sequence, false, isComplete);
                _replacedByMemory = new();
                return new(result);
            }

            if (!_unreadSequence.IsEmpty)
            {
                if (TryRead(_unreadSequence, out var sub))
                {
                    return new(new ReadResult(sub, false, false));
                }
                var result = new ReadResult(_unreadSequence, _originalResult.IsCanceled, _originalResult.IsCompleted);

                _unreadSequence = new();
                _replacing = false;
                return new(result);
            }

            var valueTask = base.ReadAsync(cancellationToken);

            // Directly returns the processed result if already completed
            if (valueTask.IsCompleted)
            {
                var result = Handle(valueTask.Result);
                return new(result);
            }

            // Continues processing the result asynchronously if not completed
            return HandleAsync(valueTask);

            async ValueTask<ReadResult> HandleAsync(ValueTask<ReadResult> task)
            {
                var result = await task;
                return Handle(result);
            }

            // Local function to handle the read result
            ReadResult Handle(ReadResult readResult)
            {
                // Returns the original result if canceled or unable to read
                if (readResult.IsCanceled || !TryRead(readResult.Buffer, out var buffer))
                {
                    return readResult;
                }

                _replacing = true;
                _originalResult = readResult;

                if (buffer.IsEmpty)
                {
                    _replacing = !_unreadSequence.IsEmpty;
                    var isComplete = !_replacing && _originalResult.IsCompleted;
                    var result = new ReadResult(new(_replacedByMemory), false, isComplete);
                    _replacedByMemory = new();
                    return result;
                }

                // Returns a new ReadResult with the processed buffer
                return new ReadResult(buffer, false, false);
            }
        }

        // Overridden AdvanceTo methods to handle advancing the buffer(s) first
        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            if (!TryAdvanceBuffer())
            {
                base.AdvanceTo(consumed, examined);
            }
        }

        // Overridden AdvanceTo methods to handle advancing the buffer(s) first
        public override void AdvanceTo(SequencePosition consumed)
        {
            if (!TryAdvanceBuffer())
            {
                base.AdvanceTo(consumed);
            }
        }

        // Tries to read and process the read result, replacing URLs as needed
        private bool TryRead(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> readSequence)
        {
            readSequence = default;

            if (buffer.IsEmpty)
            {
                return false;
            }

            // Creates a SequenceReader for the buffer
            var reader = new SequenceReader<byte>(buffer);

            // Iterates through the rewriters to find and replace matching sequences
            foreach (var rewriter in rewriterCollection)
            {
                var spanToReplace = rewriter.LocalFullBytes.Span;
                if (reader.TryReadTo(out readSequence, spanToReplace, true))
                {
                    _replacedByMemory = rewriter.RemoteFullBytes;
                    _unreadSequence = reader.UnreadSequence;
                    return true;
                }
            }

            return false;
        }

        // Tries to advance to the buffered sequence(s) if available
        private bool TryAdvanceBuffer()
        {
            if (_replacing)
            {
                return true;
            }
            if (!_originalResult.Buffer.IsEmpty)
            {
                base.AdvanceTo(_originalResult.Buffer.End, _originalResult.Buffer.End);
                _originalResult = new();
                return true;
            }
            return false;
        }
    }
}
