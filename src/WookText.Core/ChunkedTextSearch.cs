namespace WookText.Core;

public static class ChunkedTextSearch
{
    public const int MaximumQueryLength = 65536;
    private const int ChunkSize = 65536;

    // Candidate ranges overlap only by the query suffix, so cross-chunk matches are not lost.
    public static int Find(int textLength, Func<int, int, string> getText, string query, int start,
        bool previous, bool matchCase, CancellationToken cancellationToken = default)
    {
        if (query.Length == 0 || query.Length > textLength) return -1;
        if (query.Length > MaximumQueryLength) throw new ArgumentException("대용량 검색어는 65,536자 이하로 입력해 주세요.");
        int end = textLength - query.Length + 1;
        start = Math.Clamp(start, 0, end);
        StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int SearchRange(int from, int to)
        {
            while (from < to)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(ChunkSize, to - from);
                int offset = previous ? to - count : from;
                string chunk = getText(offset, count + query.Length - 1);
                int found = previous ? chunk.LastIndexOf(query, comparison) : chunk.IndexOf(query, comparison);
                if (found >= 0) return offset + found;
                if (previous) to -= count;
                else from += count;
            }
            return -1;
        }
        cancellationToken.ThrowIfCancellationRequested();
        int result = previous ? SearchRange(0, start) : SearchRange(start, end);
        return result >= 0 ? result : previous ? SearchRange(start, end) : SearchRange(0, start);
    }
}
