using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    /// <summary>
    /// UrlRewritePipeWriter class that extends DelegatingPipeWriter to modify URLs in a PipeWriter stream
    /// </summary>
    /// <param name="inner">The inner <see cref="PipeWriter"/>, for example the one that writes to the <see cref="HttpResponse"/> Body</param>
    /// <param name="rewriterCollection"></param>
    public sealed class UrlRewritePipeWriter(PipeWriter inner, UrlRewriterCollection rewriterCollection) : DelegatingPipeWriter(inner)
    {
        // Overridden WriteAsync method to process and rewrite URLs in the stream
        public override ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            var sourceSpan = source.Span;

            // Checks if there are no rewriters or if the target size cannot be determined, then calls base WriteAsync
            if (rewriterCollection.Count == 0 || !TryGetTargetSize(rewriterCollection, sourceSpan, out var targetSize))
            {
                return base.WriteAsync(source, cancellationToken);
            }

            var targetSpan = GetSpan(targetSize);

            // Processes each rewriter in the collection and rewrites URLs as needed
            while (TryGetNext(rewriterCollection, sourceSpan, out var index, out var replacer))
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

            // Advances the writer and flushes the result
            Advance(targetSize);

            return FlushAsync(cancellationToken);
        }

        // Static method to calculate the target size for the rewritten buffer
        static bool TryGetTargetSize(UrlRewriterCollection rewriterCollection, ReadOnlySpan<byte> source, out int size)
        {
            size = source.Length;
            var result = false;
            var found = true;
            while (found && source.IndexOf(rewriterCollection.RemoteBaseUrlBytes.Span) is int index && index > -1)
            {
                source = source.Slice(index);
                found = false;

                // Searches for rewriters and adjusts the target size accordingly
                foreach (var replacer in rewriterCollection)
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

        // Static method to find the next URL rewriter to apply
        static bool TryGetNext(UrlRewriterCollection rewriterCollection, ReadOnlySpan<byte> source, out int index, [NotNullWhen(true)] out UrlRewriter? replacer)
        {
            index = source.IndexOf(rewriterCollection.RemoteBaseUrlBytes.Span);
            replacer = null;

            if (index < 0) return false;

            source = source.Slice(index);

            // Iterates through rewriters to find a match
            foreach (var r in rewriterCollection)
            {
                if (source.StartsWith(r.RemoteFullBytes.Span))
                {
                    replacer = r;
                    return true;
                }
            }

            return false;
        }

        // Static method to copy a span of bytes and advance the target span
        static Span<byte> CopyAndAdvance(ReadOnlySpan<byte> from, Span<byte> to)
        {
            from.CopyTo(to);
            return to.Slice(from.Length);
        }
    }
}
