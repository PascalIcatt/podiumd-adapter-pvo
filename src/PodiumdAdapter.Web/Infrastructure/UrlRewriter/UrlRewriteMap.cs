using System.Collections;
using System.Text;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewriteMapCollection : IReadOnlyCollection<UrlRewriteMap>
    {
        private readonly IReadOnlyCollection<UrlRewriteMap> _rewriters;

        public UrlRewriteMapCollection(string localBaseUrlString, string remoteBaseUrlString, IReadOnlyCollection<UrlRewriteMap> rewriters)
        {
            LocalBaseUrlString = localBaseUrlString;
            RemoteBaseUrlString = remoteBaseUrlString;
            LocalBaseUrlBytes = Encoding.UTF8.GetBytes(LocalBaseUrlString);
            RemoteBaseUrlBytes = Encoding.UTF8.GetBytes(RemoteBaseUrlString);
            _rewriters = rewriters;
        }

        private UrlRewriteMapCollection(IReadOnlyCollection<UrlRewriteMap> rewriters, string localBaseUrlString, string remoteBaseUrlString, ReadOnlyMemory<byte> remoteBaseUrlBytes, ReadOnlyMemory<byte> localBaseUrlBytes)
        {
            _rewriters = rewriters;
            LocalBaseUrlString = localBaseUrlString;
            RemoteBaseUrlString = remoteBaseUrlString;
            RemoteBaseUrlBytes = remoteBaseUrlBytes;
            LocalBaseUrlBytes = localBaseUrlBytes;
        }

        public string LocalBaseUrlString { get; }
        public string RemoteBaseUrlString { get; }
        public ReadOnlyMemory<byte> RemoteBaseUrlBytes { get; }
        public ReadOnlyMemory<byte> LocalBaseUrlBytes { get; }

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
            return new UrlRewriteMapCollection(list, RemoteBaseUrlString, LocalBaseUrlString, LocalBaseUrlBytes, RemoteBaseUrlBytes);
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

        private UrlRewriteMap(string remoteFullString, string localFullString, ReadOnlyMemory<byte> remoteFullBytes, ReadOnlyMemory<byte> localFullBytes)
        {
            RemoteFullString = remoteFullString;
            LocalFullString = localFullString;
            RemoteFullBytes = remoteFullBytes;
            LocalFullBytes = localFullBytes;
        }

        public string RemoteFullString { get; }
        public string LocalFullString { get; }

        public ReadOnlyMemory<byte> RemoteFullBytes { get; }
        public ReadOnlyMemory<byte> LocalFullBytes { get; }

        public UrlRewriteMap Reverse() => new UrlRewriteMap(LocalFullString, RemoteFullString, LocalFullBytes, RemoteFullBytes);
    }
}
