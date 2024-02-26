using System.Collections;
using System.Text;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewriteMapCollection : IReadOnlyCollection<UrlRewriteMap>
    {
        private readonly IReadOnlyCollection<UrlRewriteMap> _rewriters;

        public UrlRewriteMapCollection(string toBaseUrlString, string fromBaseUrlString, IReadOnlyCollection<UrlRewriteMap> rewriters)
        {
            ToBaseUrlString = toBaseUrlString;
            FromBaseUrlString = fromBaseUrlString;
            ToBaseUrlBytes = Encoding.UTF8.GetBytes(ToBaseUrlString);
            FromBaseUrlBytes = Encoding.UTF8.GetBytes(FromBaseUrlString);
            _rewriters = rewriters;
        }

        private UrlRewriteMapCollection(IReadOnlyCollection<UrlRewriteMap> rewriters, string toBaseUrlString, string fromBaseUrlString, ReadOnlyMemory<byte> fromBaseUrlBytes, ReadOnlyMemory<byte> toBaseUrlBytes)
        {
            _rewriters = rewriters;
            ToBaseUrlString = toBaseUrlString;
            FromBaseUrlString = fromBaseUrlString;
            FromBaseUrlBytes = fromBaseUrlBytes;
            ToBaseUrlBytes = toBaseUrlBytes;
        }

        public string ToBaseUrlString { get; }
        public string FromBaseUrlString { get; }
        public ReadOnlyMemory<byte> FromBaseUrlBytes { get; }
        public ReadOnlyMemory<byte> ToBaseUrlBytes { get; }

        public int Count => _rewriters.Count;

        public IEnumerator<UrlRewriteMap> GetEnumerator()
        {
            return _rewriters.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_rewriters).GetEnumerator();
        }

        public UrlRewriteMapCollection Reverse()
        {
            var list = _rewriters.Select(x => x.Reverse()).ToList();
            return new UrlRewriteMapCollection(list, FromBaseUrlString, ToBaseUrlString, ToBaseUrlBytes, FromBaseUrlBytes);
        }
    }

    public record UrlRewriteMap
    {
        public UrlRewriteMap(string toFullUrl, string fromFullUrl)
        {
            FromFullString = fromFullUrl;
            ToFullString = toFullUrl;
            FromFullBytes = Encoding.UTF8.GetBytes(FromFullString);
            ToFullBytes = Encoding.UTF8.GetBytes(ToFullString);
        }

        private UrlRewriteMap(string fromFullString, string toFullString, ReadOnlyMemory<byte> fromFullBytes, ReadOnlyMemory<byte> toFullBytes)
        {
            FromFullString = fromFullString;
            ToFullString = toFullString;
            FromFullBytes = fromFullBytes;
            ToFullBytes = toFullBytes;
        }

        public string FromFullString { get; }
        public string ToFullString { get; }

        public ReadOnlyMemory<byte> FromFullBytes { get; }
        public ReadOnlyMemory<byte> ToFullBytes { get; }

        public UrlRewriteMap Reverse() => new UrlRewriteMap(ToFullString, FromFullString, ToFullBytes, FromFullBytes);
    }
}
