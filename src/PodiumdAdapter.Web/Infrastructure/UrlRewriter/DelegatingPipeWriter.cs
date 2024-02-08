using System.IO.Pipelines;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public abstract class DelegatingPipeWriter(PipeWriter inner) : PipeWriter
    {
        public override void Advance(int bytes) => inner.Advance(bytes);

        public override void CancelPendingFlush() => inner.CancelPendingFlush();

        public override void Complete(Exception? exception = null) => inner.Complete(exception);

        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) => inner.FlushAsync(cancellationToken);

        public override Memory<byte> GetMemory(int sizeHint = 0) => inner.GetMemory(sizeHint);

        public override Span<byte> GetSpan(int sizeHint = 0) => inner.GetSpan(sizeHint);

        public override ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) => base.WriteAsync(source, cancellationToken);

        public override ValueTask CompleteAsync(Exception? exception = null) => inner.CompleteAsync(exception);

        public override bool CanGetUnflushedBytes => inner.CanGetUnflushedBytes;

        public override long UnflushedBytes => base.UnflushedBytes;
    }
}
