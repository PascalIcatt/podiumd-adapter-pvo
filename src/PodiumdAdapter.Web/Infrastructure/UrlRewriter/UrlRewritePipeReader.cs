using System.Buffers;
using System.IO.Pipelines;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewritePipeReader(PipeReader reader, ReplacerList replacers) : DelegatingPipeReader(reader)
    {
        private ReadOnlySequence<byte> _previousResult = default;
        private ReadOnlySequence<byte> _nextResult = new();
        private ReadOnlySequence<byte> _secondResult = default;

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (!_nextResult.IsEmpty)
            {
                var result = new ReadResult(_nextResult, false, _secondResult.IsEmpty);
                _nextResult = _secondResult;
                _secondResult = new();
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
            if (!TryAdvancePrevious())
            {
                base.AdvanceTo(consumed, examined);
            }
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            if (!TryAdvancePrevious())
            {
                base.AdvanceTo(consumed);
            }
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
                    _nextResult = new(item.RemoteFullBytes);
                    _previousResult = readResult.Buffer;
                    _secondResult = reader.UnreadSequence;
                    return true;
                }
            }

            return false;
        }

        private bool TryAdvancePrevious()
        {
            if (!_nextResult.IsEmpty)
            {
                return true;
            }

            if (_previousResult.IsEmpty)
            {
                return false;
            }

            base.AdvanceTo(_previousResult.Start, _previousResult.End);
            _previousResult = new();
            return true;
        }
    }
}
