using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter.Internal
{
    public class UrlRewriteFeature : IHttpResponseBodyFeature
    {
        private readonly IHttpResponseBodyFeature _responseBodyFeature;

        public UrlRewriteFeature(
            IHttpResponseBodyFeature responseBodyFeature,
            UrlRewriteMapCollection replacers)
        {
            _responseBodyFeature = responseBodyFeature;

            Writer = new UrlRewritePipeWriter(responseBodyFeature.Writer, replacers);
            Stream = Writer.AsStream();
        }

        public Stream Stream { get; set; }

        public PipeWriter Writer { get; set; }

        public Task CompleteAsync() => _responseBodyFeature.CompleteAsync();

        public void DisableBuffering() => _responseBodyFeature.DisableBuffering();

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) => _responseBodyFeature.SendFileAsync(path, offset, count, cancellationToken);

        public Task StartAsync(CancellationToken cancellationToken = default) => _responseBodyFeature.StartAsync(cancellationToken);
    }
}
