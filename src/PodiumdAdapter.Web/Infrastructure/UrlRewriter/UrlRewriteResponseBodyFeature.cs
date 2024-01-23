using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewriteResponseBodyFeature : IHttpResponseBodyFeature
    {
        private readonly IHttpResponseBodyFeature _inner;

        public UrlRewriteResponseBodyFeature(IHttpResponseBodyFeature inner, ReplacerList replacers)
        {
            _inner = inner;
            Writer = new UrlRewritePipeWriter(inner.Writer, replacers);
            Stream = Writer.AsStream();
        }

        public Stream Stream { get; }

        public PipeWriter Writer { get; }

        public Task CompleteAsync() => _inner.CompleteAsync();

        public void DisableBuffering() => _inner.DisableBuffering();

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
            => _inner.SendFileAsync(path, offset, count, cancellationToken);

        public Task StartAsync(CancellationToken cancellationToken = default) => _inner.StartAsync(cancellationToken);
    }
}
