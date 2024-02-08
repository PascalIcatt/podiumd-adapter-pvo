using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    /// <summary>
    /// UrlRewritePipeWriter class that extends DelegatingPipeWriter to modify URLs in a PipeWriter stream
    /// </summary>
    /// <param name="inner">The inner <see cref="PipeWriter"/>, for example the one that writes to the <see cref="HttpResponse"/> Body</param>
    /// <param name="rewriterCollection"></param>
    public sealed class UrlRewritePipeWriter : DelegatingPipeWriter
    {
        private readonly UrlRewriterCollection _rewriterCollection;
        private ReadOnlyMemory<byte> _internalBuffer;

        public UrlRewritePipeWriter(PipeWriter inner, UrlRewriterCollection rewriterCollection) : base(inner)
        {
            _rewriterCollection = rewriterCollection;
        }

        /// <summary>
        /// Overrides WriteAsync to implement URL rewriting logic.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            if (source.IsEmpty) return base.WriteAsync(source, cancellationToken);
            var sourceSpan = source.Span;
            var prepend = _internalBuffer.Span;

            if (!_internalBuffer.IsEmpty)
            {
                prepend = _internalBuffer.Span;
                _internalBuffer = new();
                var bufferLength = prepend.Length;

                foreach (var item in _rewriterCollection)
                {
                    var sliced = item.RemoteFullBytes.Span.Slice(bufferLength);
                    if (sourceSpan.StartsWith(sliced))
                    {
                        sourceSpan = sourceSpan.Slice(sliced.Length);
                        prepend = item.LocalFullBytes.Span;
                    }
                }
            }

            if (_rewriterCollection.Count == 0 || !TryGetTargetSize(ref sourceSpan, out var targetSize))
            {
                var length = sourceSpan.Length + prepend.Length;
                var target = GetSpan(length);
                prepend.CopyAndMoveForward(ref target);
                sourceSpan.CopyTo(target);
                Advance(length);
                return FlushAsync(cancellationToken);
            }

            targetSize += prepend.Length;

            var targetSpan = GetSpan(targetSize);

            prepend.CopyAndMoveForward(ref targetSpan);

            while (TryGetNext(sourceSpan, out var index, out var rewriter))
            {
                if (index > 0)
                {
                    sourceSpan[..index].CopyAndMoveForward(ref targetSpan);
                }
                rewriter.LocalFullBytes.Span.CopyAndMoveForward(ref targetSpan);
                var next = index + rewriter.RemoteFullBytes.Length;
                sourceSpan = sourceSpan[next..];
            }


            sourceSpan.CopyTo(targetSpan);

            Advance(targetSize);

            return FlushAsync(cancellationToken);

        }

        /// <summary>
        /// Tries to find the length that the target <see cref="Span{byte}"/> needs to have, 
        /// taking into account all rewritten URLs.
        /// If no URL needs to be rewritten, this method returns <see cref="false"/>.
        /// </summary>
        /// <param name="originalBuffer"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        bool TryGetTargetSize(ref ReadOnlySpan<byte> originalBuffer, out int size)
        {
            var source = originalBuffer;
            size = source.Length;
            var result = false;
            var found = true;
            var baseUrlSpan = _rewriterCollection.RemoteBaseUrlBytes.Span;

            // optimization: first check if we can find the base url
            // if we can't find that, there is no chance of finding a specific url
            // this avoids unnecessarily looping the rewriter collection
            while (found && source.IndexOf(baseUrlSpan) is int index && index > -1)
            {
                source = source.Slice(index);
                found = false;

                foreach (var r in _rewriterCollection)
                {
                    var from = r.RemoteFullBytes.Span;
                    if (source.StartsWith(from))
                    {
                        var to = r.LocalFullBytes.Span;
                        var diff = to.Length - from.Length;
                        size += diff;
                        result = true;
                        found = true;
                        source = source.Slice(from.Length);
                        break;
                    }
                }
            }

            HandlePartialMatch(ref originalBuffer, ref size, source);

            return result;
        }

        /// <summary>
        /// // A method to find the next URL rewriter that matches the source buffer.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="index"></param>
        /// <param name="rewriter"></param>
        /// <returns></returns>
        bool TryGetNext(ReadOnlySpan<byte> source, out int index, [NotNullWhen(true)] out UrlRewriter? rewriter)
        {
            // optimization: first check if we can find the base url
            // if we can't find that, there is no chance of finding a specific url
            // this avoids unnecessarily looping the rewriter collection
            index = source.IndexOf(_rewriterCollection.RemoteBaseUrlBytes.Span);
            rewriter = null;

            if (index < 0) return false;

            source = source.Slice(index);

            foreach (var r in _rewriterCollection)
            {
                if (source.StartsWith(r.RemoteFullBytes.Span))
                {
                    rewriter = r;
                    return true;
                }
            }

            return false;
        }

        private void HandlePartialMatch(ref ReadOnlySpan<byte> originalBuffer, ref int size, ReadOnlySpan<byte> source)
        {
            var lengthOfPartialMatch = 0;
            UrlRewriter? rewriterToPutInInternalBuffer = null;

            foreach (var r in _rewriterCollection)
            {
                var span = r.RemoteFullBytes.Span;
                if (source.MightMatchInNextBuffer(span, ref lengthOfPartialMatch))
                {
                    rewriterToPutInInternalBuffer = r;
                }
            }

            if (rewriterToPutInInternalBuffer != null)
            {
                _internalBuffer = rewriterToPutInInternalBuffer.RemoteFullBytes.Slice(0, lengthOfPartialMatch);
                originalBuffer = originalBuffer.Slice(0, originalBuffer.Length - lengthOfPartialMatch);
                size -= lengthOfPartialMatch;
            }
        }
    }
}
