namespace WookText.Core.Tests;

public sealed class SearchEditSessionTests
{
    private static string Apply(string text, SearchEditChange change)
    {
        foreach (TextReplacement edit in change.Replacements)
            text = text.Remove(edit.Offset, edit.Length).Insert(edit.Offset, edit.Text);
        return text;
    }

    [Theory]
    [InlineData("pear", "pear Apple pear", 4)]
    [InlineData("", " Apple ", 0)]
    [InlineData("apple apple", "apple apple Apple apple apple", 11)]
    [InlineData("한글\r\n🙂", "한글\r\n🙂 Apple 한글\r\n🙂", 6)]
    public void Replace_AllTargets_PreservesOutsideTextAndTracksLength(string replacement, string expected, int length)
    {
        SearchEditSession session = SearchEditSession.Create("apple Apple apple", "apple", true);
        SearchEditChange change = session.Replace(0, 5, replacement);
        Assert.Equal(expected, Apply("apple Apple apple", change));
        Assert.Equal(length, change.Session.MatchLength);
        Assert.Equal(2, change.Session.Offsets.Count);
        Assert.Equal(new[] { 0, 7 + length }, change.Session.Offsets);
    }

    [Fact]
    public void Replace_SequentialTyping_KeepsOriginalTargetsWhenQueryDisappears()
    {
        string text = "cat cat";
        SearchEditSession session = SearchEditSession.Create(text, "cat", true);
        var first = session.Replace(0, 3, "d");
        text = Apply(text, first);
        var second = first.Session.Replace(1, 0, "og");
        Assert.Equal("dog dog", Apply(text, second));
        Assert.Equal(new[] { 0, 4 }, second.Session.Offsets);
    }

    [Fact]
    public void Replace_AdjacentDeletedTargets_CanInsertAtCoincidentCarets()
    {
        var deleted = SearchEditSession.Create("aa", "a", true).Replace(0, 1, "");
        Assert.Equal("", Apply("aa", deleted));
        Assert.Equal(new[] { 0, 0 }, deleted.Session.Offsets);
        var inserted = deleted.Session.Replace(0, 0, "b");
        Assert.Equal("bb", Apply("", inserted));
        Assert.Equal(new[] { 0, 1 }, inserted.Session.Offsets);
    }

    [Fact]
    public void Replace_MiddleOfCaseInsensitiveMatches_OnlyChangesSelectedPart()
    {
        var change = SearchEditSession.Create("Cat CAT", "cat", false).Replace(1, 1, "oo");
        Assert.Equal("Coot CooT", Apply("Cat CAT", change));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 2)]
    [InlineData(0, -1)]
    public void Replace_OutsideTarget_RejectsEdit(int offset, int length) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SearchEditSession.Create("cat", "cat", true).Replace(offset, length, "x"));

    [Fact]
    public void FindTarget_OutsideOrCrossingSelection_RejectsEdit()
    {
        var session = SearchEditSession.Create("cat xx cat", "cat", true);
        Assert.Equal(-1, session.FindTarget(4, 0, 0));
        Assert.Equal(-1, session.FindTarget(2, 3, 0));
        Assert.Equal(1, session.FindTarget(8, 1, 0));
    }

    [Theory]
    [InlineData("🙂", 2, true, 0, 2)]
    [InlineData("e\u0301", 2, true, 0, 2)]
    [InlineData("한글", 2, true, 1, 1)]
    [InlineData("\r\n", 0, false, 0, 2)]
    [InlineData("cat", 0, true, 0, 0)]
    [InlineData("cat", 3, false, 3, 0)]
    [InlineData("", 0, false, 0, 0)]
    public void DeletionRange_Caret_UsesWholeTextElementsAndStaysWithinTarget(string text, int caret, bool backwards, int offset, int length) =>
        Assert.Equal((offset, length), SearchEditSession.DeletionRange(text, caret, backwards));
}
