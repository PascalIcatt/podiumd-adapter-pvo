using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter.Internal
{
    public class UrlRewriteFeature : IHttpResponseBodyFeature
    {
        private readonly HttpContext _context;
        private readonly IHttpResponseBodyFeature _responseBodyFeature;
        private readonly UrlRewritePipeWriter _writer;
        private readonly Stream _stream;

        public UrlRewriteFeature(
            HttpContext context,
            IHttpResponseBodyFeature responseBodyFeature,
            UrlRewriteMapCollection replacers)
        {
            _context = context;
            _responseBodyFeature = responseBodyFeature;

            _writer = new UrlRewritePipeWriter(responseBodyFeature.Writer, replacers);
            _stream = _writer.AsStream();
        }

        public Stream Stream { get => IsJson() ? _stream : _responseBodyFeature.Stream; set => throw new NotImplementedException(); }

        public PipeWriter Writer { get => IsJson() ? _writer : _responseBodyFeature.Writer; set => new NotImplementedException(); }

        public Task CompleteAsync() => _responseBodyFeature.CompleteAsync();

        public void DisableBuffering() => _responseBodyFeature.DisableBuffering();

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) => _responseBodyFeature.SendFileAsync(path, offset, count, cancellationToken);

        public Task StartAsync(CancellationToken cancellationToken = default) => _responseBodyFeature.StartAsync(cancellationToken);

        private bool IsJson() => _context.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) is true;
    }
}
