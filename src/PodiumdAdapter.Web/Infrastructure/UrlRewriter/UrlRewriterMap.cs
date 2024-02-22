using System.Collections;
using System.Text;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewriterMapCollection : IReadOnlyCollection<UrlRewriterMap>
    {
        private readonly IReadOnlyCollection<UrlRewriterMap> _rewriter;

        public UrlRewriterMapCollection(string localBaseUrlString, string remoteBaseUrlString, IReadOnlyCollection<UrlRewriterMap> rewriters)
        {
            LocalBaseUrlString = localBaseUrlString;
            RemoteBaseUrlString = remoteBaseUrlString;
            LocalBaseUrlBytes = Encoding.UTF8.GetBytes(LocalBaseUrlString); ;
            RemoteBaseUrlBytes = Encoding.UTF8.GetBytes(RemoteBaseUrlString); ;
            _rewriter = rewriters;
        }

        public string LocalBaseUrlString { get; }
        public string RemoteBaseUrlString { get; }
        public ReadOnlyMemory<byte> RemoteBaseUrlBytes { get; }
        public ReadOnlyMemory<byte> LocalBaseUrlBytes { get; }

        public int Count => _rewriter.Count;

        public IEnumerator<UrlRewriterMap> GetEnumerator()
        {
            return _rewriter.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_rewriter).GetEnumerator();
        }
    }

    public record UrlRewriterMap
    {
        public UrlRewriterMap(string localUrl, string remoteUrl)
        {
            RemoteFullString = remoteUrl;
            LocalFullString = localUrl;
            RemoteFullBytes = Encoding.UTF8.GetBytes(RemoteFullString);
            LocalFullBytes = Encoding.UTF8.GetBytes(LocalFullString);
        }

        public string RemoteFullString { get; }
        public string LocalFullString { get; }

        public ReadOnlyMemory<byte> RemoteFullBytes { get; }
        public ReadOnlyMemory<byte> LocalFullBytes { get; }
    }
}
