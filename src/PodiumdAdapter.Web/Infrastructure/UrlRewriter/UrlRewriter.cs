using System.Collections;
using System.Text;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewriterCollection : IReadOnlyCollection<UrlRewriter>
    {
        private readonly IReadOnlyCollection<UrlRewriter> _rewriter;

        public UrlRewriterCollection(string localBaseUrlString, string remoteBaseUrlString, IReadOnlyCollection<UrlRewriter> rewriters)
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

        public IEnumerator<UrlRewriter> GetEnumerator()
        {
            return _rewriter.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_rewriter).GetEnumerator();
        }
    }

    public record UrlRewriter
    {
        public UrlRewriter(string localUrl, string remoteUrl)
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
