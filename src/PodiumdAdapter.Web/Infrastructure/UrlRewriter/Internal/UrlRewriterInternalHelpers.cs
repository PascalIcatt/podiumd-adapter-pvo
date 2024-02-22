namespace PodiumdAdapter.Web.Infrastructure.UrlRewriter.Internal
{
    public static class UrlRewriterInternalHelpers
    {
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
    }
}
