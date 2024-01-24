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
        private ReadOnlySequence<byte> _previousResult = default;
        private ReadOnlySequence<byte> _buffer1 = new();
        private ReadOnlySequence<byte> _buffer2 = default;

        // Overridden ReadAsync method to process and rewrite URLs in the stream
        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            // Checks if there is a buffer available and returns it if so
            if (!_buffer1.IsEmpty)
            {
                var result = new ReadResult(_buffer1, false, _buffer2.IsEmpty);
                _buffer1 = _buffer2;
                _buffer2 = new();
                return new(result);
            }

            var valueTask = base.ReadAsync(cancellationToken);

            // Directly returns the processed result if already completed
            if (valueTask.IsCompleted)
            {
                return new(Handle(valueTask.Result));
            }

            // Continues processing the result asynchronously if not completed
            return new(valueTask.AsTask().ContinueWith(x => Handle(x.Result)));

            // Local function to handle the read result
            ReadResult Handle(ReadResult readResult)
            {
                // Returns the original result if canceled or unable to read
                if (readResult.IsCanceled || !TryRead(readResult, out var buffer, out var isComplete))
                {
                    return readResult;
                }

                // Returns a new ReadResult with the processed buffer
                return new ReadResult(buffer, false, isComplete);
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
        private bool TryRead(ReadResult readResult, out ReadOnlySequence<byte> sub, out bool isComplete)
        {
            // Creates a SequenceReader for the buffer
            var reader = new SequenceReader<byte>(readResult.Buffer);
            isComplete = readResult.IsCompleted;

            // Searches for the local base url bytes in the buffer
            var rootSpan = rewriterCollection.LocalBaseUrlBytes.Span;

            if (!reader.TryReadTo(out sub, rootSpan, true))
            {
                return false;
            }

            // Iterates through the rewriters to find and replace matching sequences
            foreach (var rewriter in rewriterCollection)
            {
                var slice = rewriter.LocalFullBytes.Span.Slice(rootSpan.Length);
                if (reader.UnreadSpan.StartsWith(slice))
                {
                    reader.Advance(slice.Length);
                    isComplete = false;
                    _buffer1 = new(rewriter.RemoteFullBytes);
                    _previousResult = readResult.Buffer;
                    _buffer2 = reader.UnreadSequence;
                    return true;
                }
            }

            return false;
        }

        // Tries to advance to the buffered sequence(s) if available
        private bool TryAdvanceBuffer()
        {
            if (!_buffer1.IsEmpty)
            {
                // don't advance anything yet, the buffer needs to be read first
                return true;
            }

            // there is nothing to buffer so we can handle Advancing normally
            if (_previousResult.IsEmpty)
            {
                return false;
            }

            // Advances the inner reader to the start and end positions of the previous result
            base.AdvanceTo(_previousResult.Start, _previousResult.End);

            // reset the previous result
            _previousResult = new();
            return true;
        }
    }
}
