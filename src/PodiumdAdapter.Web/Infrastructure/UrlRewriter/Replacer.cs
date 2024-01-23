using System.Collections;
using System.Text;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class ReplacerList: IReadOnlyCollection<Replacer>
    {
        private readonly IReadOnlyCollection<Replacer> _replacers;

        public ReplacerList(string localRootString, string remoteRootString, IReadOnlyCollection<Replacer> replacers)
        {
            LocalRootString = localRootString;
            RemoteRootString = remoteRootString;
            LocalRootBytes = Encoding.UTF8.GetBytes(LocalRootString); ;
            RemoteRootBytes = Encoding.UTF8.GetBytes(RemoteRootString); ;
            _replacers = replacers;
        }

        public string LocalRootString { get; }
        public string RemoteRootString { get; }
        public ReadOnlyMemory<byte> RemoteRootBytes { get; }
        public ReadOnlyMemory<byte> LocalRootBytes { get; }

        public int Count => _replacers.Count;

        public IEnumerator<Replacer> GetEnumerator()
        {
            return _replacers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_replacers).GetEnumerator();
        }
    }

    public record Replacer
    { 
        public Replacer(string localUrl, string remoteUrl)
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
