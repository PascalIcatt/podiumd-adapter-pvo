using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public sealed class UrlRewritePipeWriter(PipeWriter inner, IReadOnlyCollection<Replacer> replacers) : DelegatingPipeWriter(inner)
    {
        public override ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            var sourceSpan = source.Span;

            if (replacers.Count == 0 || !TryGetTargetSize(replacers, sourceSpan, out var targetSize))
            {
                return base.WriteAsync(source, cancellationToken);
            }

            var targetSpan = GetSpan(targetSize);

            while (TryGetNext(replacers, sourceSpan, out var index, out var replacer))
            {
                if (index > 0)
                {
                    targetSpan = CopyAndAdvance(sourceSpan[..index], targetSpan);
                }
                var from = replacer.RemoteBytes;
                var to = replacer.LocalBytes;
                targetSpan = CopyAndAdvance(to.Span, targetSpan);
                var next = index + from.Length;
                sourceSpan = sourceSpan[next..];
            }

            sourceSpan.CopyTo(targetSpan);

            Advance(targetSize);

            return FlushAsync(cancellationToken);
        }

        static bool TryGetTargetSize(IReadOnlyCollection<Replacer> replacers, ReadOnlySpan<byte> source, out int size)
        {
            size = source.Length;
            var result = false;
            foreach (var replacer in replacers ?? [])
            {
                var from = replacer.RemoteBytes.Span;
                var to = replacer.LocalBytes.Span;
                var diff = to.Length - from.Length;
                var count = source.Count(from);
                result = result || count > 0;
                size += (diff * count);
            }
            return result;
        }

        static bool TryGetNext(IReadOnlyCollection<Replacer> replacers, ReadOnlySpan<byte> source, out int index, [NotNullWhen(true)] out Replacer? replacer)
        {
            index = int.MaxValue;
            replacer = null;

            foreach (var r in replacers ?? [])
            {
                var from = r.RemoteBytes.Span;
                var i = source.IndexOf(from);
                if (i != -1 && i <= index)
                {
                    index = i;
                    replacer = r;
                }
            }

            return replacer != null;
        }

        static Span<byte> CopyAndAdvance(ReadOnlySpan<byte> from, Span<byte> to)
        {
            from.CopyTo(to);
            return to.Slice(from.Length);
        }
    }
}
