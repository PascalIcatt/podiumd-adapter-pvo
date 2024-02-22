using System.IO.Pipelines;
using System.Net;
using System.Text;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter.Internal.HttpClient
{
    public class UrlRewriteHttpMessageHandler : DelegatingHandler
    {
        private readonly GetUrlRewriteMapCollection _getMaps;

        public UrlRewriteHttpMessageHandler(GetUrlRewriteMapCollection getMaps)
        {
            _getMaps = getMaps;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var maps = _getMaps()?.Reverse();
            
            if (maps == null || maps.Count <= 0)
            {
                return base.SendAsync(request, cancellationToken);
            }

            if (request.RequestUri != null)
            {
                var newQuery = ReplaceString(request.RequestUri.Query, maps);
                var newUriBuilder = new UriBuilder(request.RequestUri) { Query = newQuery };
                request.RequestUri = newUriBuilder.Uri;
            }
            
            if (request.Content?.Headers.ContentType?.MediaType?.Contains("json") ?? false)
            {
                var newContent = new RewriterContent(request.Content, maps);
                foreach (var (key,value) in request.Content.Headers)
                {
                    if (key.Equals("content-length", StringComparison.OrdinalIgnoreCase)) continue;
                    newContent.Headers.Add(key, value);
                }
                request.Content = newContent;
            }
            
            return base.SendAsync(request, cancellationToken);
        }

        private static string ReplaceString(string input, UrlRewriteMapCollection replacers)
        {
            if (replacers.Count == 0) return input;
            var builder = new StringBuilder(input);
            foreach (var replacer in replacers)
            {
                builder.Replace(replacer.LocalFullString, replacer.RemoteFullString);
            }
            return builder.ToString();
        }
    }

    public sealed class RewriterContent : HttpContent
    {
        private readonly HttpContent _inner;
        private readonly UrlRewriteMapCollection _maps;

        public RewriterContent(HttpContent inner, UrlRewriteMapCollection maps)
        {
            _inner = inner;
            _maps = maps;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _inner.Dispose();
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            if (_maps.Count > 0)
            {
                var innerWriter = PipeWriter.Create(stream);
                var wrappedWriter = new UrlRewritePipeWriter(innerWriter, _maps);
                stream = wrappedWriter.AsStream();
            }
            return _inner.CopyToAsync(stream, context, cancellationToken);
        }

        protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            base.SerializeToStream(stream, context, cancellationToken);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => SerializeToStreamAsync(stream, context, CancellationToken.None);
    }
}
