using System.IO.Pipelines;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class DelegatingPipeReader(PipeReader inner) : PipeReader
    {
        public override void AdvanceTo(SequencePosition consumed)
        {
            inner.AdvanceTo(consumed);
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            inner.AdvanceTo(consumed, examined);
        }

        public override void CancelPendingRead()
        {
            inner.CancelPendingRead();
        }

        public override void Complete(Exception? exception = null)
        {
            inner.Complete(exception);
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            return inner.ReadAsync(cancellationToken);
        }

        public override bool TryRead(out ReadResult result)
        {
            return inner.TryRead(out result);
        }
    }
}
