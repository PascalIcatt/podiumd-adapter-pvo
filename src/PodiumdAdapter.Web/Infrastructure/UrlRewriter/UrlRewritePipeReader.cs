using System.Buffers;
using System.IO.Pipelines;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewritePipeReader(PipeReader reader, ReplacerList replacers) : DelegatingPipeReader(reader)
    {
        private ReadOnlySequence<byte> _buffer = new();
        private ReadOnlySequence<byte> _previousResult = default;
        private ReadOnlySequence<byte> _next = default;

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (!_buffer.IsEmpty)
            {
                var result = new ReadResult(_buffer, false, _next.IsEmpty);
                _buffer = _next;
                _next = new();
                return new(result);
            }

            return Internal();

            async ValueTask<ReadResult> Internal()
            {
                var readResult = await base.ReadAsync(cancellationToken);

                if (readResult.IsCanceled || !TryRead(readResult, out var buffer, out var isComplete))
                {
                    return readResult;
                }

                return new ReadResult(buffer, false, isComplete);
            }
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            if (!_buffer.IsEmpty)
            {
                return;
            }

            if (_previousResult.IsEmpty)
            {
                base.AdvanceTo(consumed, examined);
                return;
            }

            base.AdvanceTo(_previousResult.Start, _previousResult.End);
            _previousResult = new();
            return;
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            if (!_buffer.IsEmpty)
            {
                return;
            }

            if (_previousResult.IsEmpty)
            {
                base.AdvanceTo(consumed);
                return;
            }

            base.AdvanceTo(_previousResult.Start, _previousResult.End);
            _previousResult = new();
            return;
        }

        private bool TryRead(ReadResult readResult, out ReadOnlySequence<byte> sub, out bool isComplete)
        {
            var reader = new SequenceReader<byte>(readResult.Buffer);
            isComplete = readResult.IsCompleted;

            var rootSpan = replacers.LocalRootBytes.Span;

            if (!reader.TryReadTo(out sub, rootSpan, true))
            {
                return false;
            }

            foreach (var item in replacers)
            {
                var slice = item.LocalFullBytes.Span.Slice(rootSpan.Length);
                if (reader.UnreadSpan.StartsWith(slice))
                {
                    reader.Advance(slice.Length);
                    isComplete = false;
                    _buffer = new(item.RemoteFullBytes);
                    _previousResult = readResult.Buffer;
                    _next = reader.UnreadSequence;
                    return true;
                }
            }

            return false;
        }
    }
}
