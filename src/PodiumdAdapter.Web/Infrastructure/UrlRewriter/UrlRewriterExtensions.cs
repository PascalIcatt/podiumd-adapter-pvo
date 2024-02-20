namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter
{
    public static class UrlRewriterExtensions
    {
        /// <summary>
        /// Check if the start of one Span matches the end of another Span.
        /// If so, we need to keep it in memory.
        /// </summary>
        /// <param name="searchIn">The bytes in which to search</param>
        /// <param name="searchFor">The bytes to search for</param>
        /// <param name="length">The minimum length of a match. This value is updated if a longer match is found</param>
        /// <returns></returns>
        public static bool MightMatchInNextBuffer(this Span<byte> searchIn, ReadOnlySpan<byte> searchFor, ref int length)
            => ((ReadOnlySpan<byte>)searchIn).MightMatchInNextBuffer(searchFor, ref length);

        /// <summary>
        /// Check if the start of one Span matches the end of another Span.
        /// If so, we need to keep it in memory.
        /// </summary>
        /// <param name="searchIn">The bytes in which to search</param>
        /// <param name="searchFor">The bytes to search for</param>
        /// <param name="length">The minimum length of a match. This value is updated if a longer match is found</param>
        /// <returns></returns>
        public static bool MightMatchInNextBuffer(this ReadOnlySpan<byte> searchIn, ReadOnlySpan<byte> searchFor, ref int length)
        {
            for (var l = searchFor.Length - 1; l > length; l--)
            {
                var partial = searchFor.Slice(0, l);
                if (searchIn.EndsWith(partial))
                {
                    length = l;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A utility method to copy data from one span to another and move the target span forward.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static void CopyAndMoveForward(this ReadOnlySpan<byte> from, ref Span<byte> to)
        {
            from.CopyTo(to);
            to = to.Slice(from.Length);
        }

        /// <summary>
        /// Performance optimalization. 
        /// Allows you to do synchronous work on the result of a <see cref="ValueTask{TResult}"/> if it is already complete
        /// Otherwise, it awaits the underlying <see cref="Task{TResult}"/> an does the work on the result of that.
        /// </summary>
        /// <typeparam name="I"></typeparam>
        /// <typeparam name="O"></typeparam>
        /// <param name="input"></param>
        /// <param name="continuationFunction"></param>
        /// <returns></returns>
        public static ValueTask<O> ContinueWith<I, O>(this ValueTask<I> input, Func<I, O> continuationFunction)
            => input.IsCompleted
                ? new ValueTask<O>(continuationFunction(input.Result))
                : new ValueTask<O>(input.AsTask().ContinueWith(task => continuationFunction(task.Result)));
    }
}
