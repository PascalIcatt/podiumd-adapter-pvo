using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewriteRequestFeature(IHttpRequestFeature inner, ReplacerList replacers) : IHttpRequestFeature
    {
        public string Protocol { get => inner.Protocol; set => inner.Protocol = value; }
        public string Scheme { get => inner.Scheme; set => inner.Scheme = value; }
        public string Method { get => inner.Method; set => inner.Method = value; }
        public string PathBase { get => inner.PathBase; set => inner.PathBase = value; }
        public string Path { get => inner.Path; set => inner.Path = value; }
        public string RawTarget { get => inner.RawTarget; set => inner.RawTarget = value; }

        public string QueryString { get; set; } = ReplaceString(inner.QueryString, replacers);

        public IHeaderDictionary Headers { get; set; } = Remove(inner.Headers, "content-length");

        public Stream Body { get; set; } = new UrlRewritePipeReader(PipeReader.Create(inner.Body), replacers).AsStream();

        private static IHeaderDictionary Remove(IHeaderDictionary headers, string key)
        {
            headers.Remove(key);
            return headers;
        }

        private static string ReplaceString(string input, ReplacerList replacers)
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
}
