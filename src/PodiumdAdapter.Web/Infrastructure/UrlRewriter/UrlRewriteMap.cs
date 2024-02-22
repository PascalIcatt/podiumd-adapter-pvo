using System.Collections;
using System.Text;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewriteMapCollection : IReadOnlyCollection<UrlRewriteMap>
    {
        private readonly IReadOnlyCollection<UrlRewriteMap> _rewriter;

        public UrlRewriteMapCollection(string localBaseUrlString, string remoteBaseUrlString, IReadOnlyCollection<UrlRewriteMap> rewriters)
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

        public IEnumerator<UrlRewriteMap> GetEnumerator()
        {
            return _rewriter.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_rewriter).GetEnumerator();
        }
    }

    public record UrlRewriteMap
    {
        public UrlRewriteMap(string localUrl, string remoteUrl)
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
