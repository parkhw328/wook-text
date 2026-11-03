namespace WookText.Core.Tests;

public sealed class ChunkedTextSearchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Find_ChunkBoundaryAndWrap_FindsWholeMatch(bool previous)
    {
        string text = new string('x', 65534) + "한글😀needle" + new string('y', 70000);
        const string query = "한글😀needle";
        int start = previous ? text.Length : 0;
        Assert.Equal(65534, ChunkedTextSearch.Find(text.Length, text.Substring, query, start, previous, true));
        start = previous ? 0 : text.Length;
        Assert.Equal(65534, ChunkedTextSearch.Find(text.Length, text.Substring, query, start, previous, true));
    }

    [Fact]
    public void Find_DenseMatches_ReturnsOneMatchWithoutReadingWholeDocument()
    {
        const int length = 100 * 1024 * 1024;
        int calls = 0;
        string Read(int offset, int count)
        {
            calls++;
            Assert.InRange(count, 1, 65536);
            return new string('a', count);
        }
        Assert.Equal(0, ChunkedTextSearch.Find(length, Read, "a", 0, false, true));
        Assert.Equal(length - 1, ChunkedTextSearch.Find(length, Read, "a", length, true, true));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("aAaA", "aa", 1, false, false, 1)]
    [InlineData("one two one", "one", 8, true, true, 0)]
    [InlineData("one two one", "one", 0, true, true, 8)]
    [InlineData("one two one", "one", 11, false, true, 0)]
    [InlineData("abc", "none", 0, false, false, -1)]
    [InlineData("abc", "", 0, false, false, -1)]
    public void Find_DirectionAndCase_RespectsSearchOptions(string text, string query, int start, bool previous, bool matchCase, int expected) =>
        Assert.Equal(expected, ChunkedTextSearch.Find(text.Length, text.Substring, query, start, previous, matchCase));

    [Fact]
    public void Find_Cancelled_DoesNotReadSnapshot() =>
        Assert.Throws<OperationCanceledException>(() => ChunkedTextSearch.Find(100, (_, _) => throw new Exception("Must not read"),
            "a", 0, false, false, new CancellationToken(true)));
}
