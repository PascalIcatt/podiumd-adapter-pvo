using System.IO.Pipelines;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public class UrlRewritePipeWriter(PipeWriter inner, IReadOnlyCollection<Replacer> replacers) : DelegatingPipeWriter(inner)
    {
        public override async ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            if (replacers.Count == 0)
            {
                return await base.WriteAsync(source, cancellationToken);
            }

            IEnumerable<(int Index, Replacer Replacer)> GetIndices()
            {
                foreach (var replacer in replacers ?? [])
                {
                    var from = replacer.RemoteBytes.Span;
                    var index = source.Span.IndexOf(from);
                    if (index != -1) yield return (index, replacer);
                }
            }

            while (GetIndices().OrderBy(x => x.Index).FirstOrDefault() is { Replacer: var replacer, Index: var index }
                && replacer != null && index != -1)
            {
                if (index > 0)
                {
                    var result = await base.WriteAsync(source[..index], cancellationToken);
                    if (result.IsCanceled) return result;
                }
                var from = replacer.RemoteBytes;
                var to = replacer.LocalBytes;
                await base.WriteAsync(to, cancellationToken);
                var next = index + from.Length;
                source = source[next..];
            }

            return await base.WriteAsync(source, cancellationToken);
        }
    }
}
