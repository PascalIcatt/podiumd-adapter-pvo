using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public sealed class UrlRewritePipeWriter(PipeWriter inner, ReplacerList replacers) : DelegatingPipeWriter(inner)
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
                targetSpan = CopyAndAdvance(replacer.LocalFullBytes.Span, targetSpan);
                var next = index + replacer.RemoteFullBytes.Length;
                sourceSpan = sourceSpan[next..];
            }

            sourceSpan.CopyTo(targetSpan);

            Advance(targetSize);

            return FlushAsync(cancellationToken);
        }

        static bool TryGetTargetSize(ReplacerList replacers, ReadOnlySpan<byte> source, out int size)
        {
            size = source.Length;
            var result = false;
            var found = true;
            while (found && source.IndexOf(replacers.RemoteRootBytes.Span) is int index && index > -1)
            {
                source = source.Slice(index);
                found = false;

                foreach (var replacer in replacers)
                {
                    var from = replacer.RemoteFullBytes.Span;
                    if (source.StartsWith(from))
                    {
                        var to = replacer.LocalFullBytes.Span;
                        var diff = to.Length - from.Length;
                        size += diff;
                        result = true;
                        found = true;
                        source = source.Slice(from.Length);
                        break;
                    }
                }
            }

            return result;
        }

        static bool TryGetNext(ReplacerList replacers, ReadOnlySpan<byte> source, out int index, [NotNullWhen(true)] out Replacer? replacer)
        {
            index = source.IndexOf(replacers.RemoteRootBytes.Span);
            replacer = null;

            if (index < 0) return false;

            source = source.Slice(index);

            foreach (var r in replacers)
            {
                if (source.StartsWith(r.RemoteFullBytes.Span))
                {
                    replacer = r;
                    return true;
                }
            }

            return false;
        }

        static Span<byte> CopyAndAdvance(ReadOnlySpan<byte> from, Span<byte> to)
        {
            from.CopyTo(to);
            return to.Slice(from.Length);
        }
    }
}
