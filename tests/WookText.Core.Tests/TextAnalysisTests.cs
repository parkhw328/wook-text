namespace WookText.Core.Tests;

public sealed class TextAnalysisTests
{
    [Theory]
    [InlineData("a\r\nb", "CRLF")]
    [InlineData("a\nb", "LF")]
    [InlineData("a\rb", "CR")]
    [InlineData("a\r\nb\nc", "혼합 줄바꿈")]
    [InlineData("abc", "줄바꿈 없음")]
    public void LineEndings_Input_IdentifiesWithoutNormalizing(string text, string expected) => Assert.Equal(expected, TextAnalysis.LineEndings(text));

    [Theory]
    [InlineData("One two one", "one", 4, false, 8)]
    [InlineData("One two one", "One", 4, true, 0)]
    [InlineData("한글 찾기 한글", "한글", 2, true, 6)]
    [InlineData("hello", "absent", 0, false, -1)]
    [InlineData("hello", "", 0, false, -1)]
    public void FindNext_Query_FindsAndWraps(string text, string query, int start, bool matchCase, int expected) =>
        Assert.Equal(expected, TextAnalysis.FindNext(text, query, start, matchCase));
    [Theory]
    [InlineData("One one ONE", "one", false, new[] { 0, 4, 8 })]
    [InlineData("One one ONE", "one", true, new[] { 4 })]
    [InlineData("aaaaa", "aa", true, new[] { 0, 2 })]
    [InlineData("한글\r\n한글", "한글", true, new[] { 0, 4 })]
    [InlineData("a\r\nb\r\na\r\nb", "a\r\nb", true, new[] { 0, 6 })]
    [InlineData("abc", "", false, new int[] { })]
    [InlineData("abc", "missing", false, new int[] { })]
    [InlineData("", "a", false, new int[] { })]
    public void FindAll_Query_ReturnsNonOverlappingLiteralOffsets(string text, string query, bool matchCase, int[] expected) =>
        Assert.Equal(expected, TextAnalysis.FindAll(text, query, matchCase));
}
