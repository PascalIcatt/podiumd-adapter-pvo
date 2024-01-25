using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewriteFeature : IHttpRequestFeature, IHttpResponseBodyFeature, IRequestBodyPipeFeature
    {
        private readonly IHttpRequestFeature _requestFeature;
        private readonly IHttpResponseBodyFeature _responseBodyFeature;

        public UrlRewriteFeature(
            IHttpRequestFeature requestFeature,
            IHttpResponseBodyFeature responseBodyFeature,
            UrlRewriterCollection replacers)
        {
            _requestFeature = requestFeature;
            _responseBodyFeature = responseBodyFeature;

            QueryString = ReplaceString(requestFeature.QueryString, replacers);
            Headers = Remove(requestFeature.Headers, "content-length");

            Body = new UrlRewriteReadStream(requestFeature.Body, replacers);
            Reader = PipeReader.Create(Body);

            Writer = new UrlRewritePipeWriter(responseBodyFeature.Writer, replacers);
            Stream = Writer.AsStream();
        }

        public string QueryString { get; set; }
        public IHeaderDictionary Headers { get; set; }
        public Stream Body { get; set; }

        public Stream Stream { get; set; }

        public PipeWriter Writer { get; set; }

        public PipeReader Reader { get; set; }

        private static IHeaderDictionary Remove(IHeaderDictionary headers, string key)
        {
            headers.Remove(key);
            return headers;
        }

        private static string ReplaceString(string input, UrlRewriterCollection replacers)
        {
            if (replacers.Count == 0) return input;
            var builder = new StringBuilder(input);
            foreach (var replacer in replacers)
            {
                builder.Replace(replacer.LocalFullString, replacer.RemoteFullString);
            }
            return builder.ToString();
        }





        public string Protocol { get => _requestFeature.Protocol; set => _requestFeature.Protocol = value; }
        public string Scheme { get => _requestFeature.Scheme; set => _requestFeature.Scheme = value; }
        public string Method { get => _requestFeature.Method; set => _requestFeature.Method = value; }
        public string PathBase { get => _requestFeature.PathBase; set => _requestFeature.PathBase = value; }
        public string Path { get => _requestFeature.Path; set => _requestFeature.Path = value; }
        public string RawTarget { get => _requestFeature.RawTarget; set => _requestFeature.RawTarget = value; }

        public Task CompleteAsync() => _responseBodyFeature.CompleteAsync();

        public void DisableBuffering() => _responseBodyFeature.DisableBuffering();

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) => _responseBodyFeature.SendFileAsync(path, offset, count, cancellationToken);

        public Task StartAsync(CancellationToken cancellationToken = default) => _responseBodyFeature.StartAsync(cancellationToken);


    }
}
