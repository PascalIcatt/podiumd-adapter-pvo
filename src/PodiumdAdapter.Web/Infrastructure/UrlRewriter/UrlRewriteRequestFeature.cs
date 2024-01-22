using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewriteRequestFeature(IHttpRequestFeature inner, IReadOnlyCollection<Replacer> replacers) : IHttpRequestFeature
    {
        public string Protocol { get => inner.Protocol; set => inner.Protocol = value; }
        public string Scheme { get => inner.Scheme; set => inner.Scheme = value; }
        public string Method { get => inner.Method; set => inner.Method = value; }
        public string PathBase { get => inner.PathBase; set => inner.PathBase = value; }
        public string Path { get => inner.Path; set => inner.Path = value; }
        public string RawTarget { get => inner.RawTarget; set => inner.RawTarget = value; }

        public string QueryString { get; set; } = ReplaceString(inner.QueryString, replacers);

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary(new Dictionary<string, StringValues>(inner.Headers.Where(x => !x.Key.Equals("content-length", StringComparison.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase));

        public Stream Body { get; set; } = new UrlRewriteReadStream(inner.Body, replacers);

        private static string ReplaceString(string input, IReadOnlyCollection<Replacer> replacers)
        {
            if (replacers.Count == 0) return input;
            var builder = new StringBuilder(input);
            foreach (var replacer in replacers)
            {
                builder.Replace(replacer.LocalString, replacer.RemoteString);
            }
            return builder.ToString();
        }
    }
}
