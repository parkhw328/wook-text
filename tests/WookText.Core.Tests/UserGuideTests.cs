namespace WookText.Core.Tests;

public sealed class UserGuideTests
{
    [Fact]
    public void Topics_EmbeddedManual_LoadsWithoutFilesystemOrNetwork()
    {
        Assert.True(UserGuide.Topics.Count >= 16);
        Assert.Equal(UserGuide.Topics.Count, UserGuide.Topics.Select(t => t.Title).Distinct().Count());
        Assert.All(UserGuide.Topics, topic =>
        {
            Assert.False(string.IsNullOrWhiteSpace(topic.Title));
            Assert.True(topic.Body.Length > 100, topic.Title);
            Assert.DoesNotContain(topic.Body.Split('\n'), line => line.StartsWith("## ", StringComparison.Ordinal));
        });
    }

    [Theory]
    [InlineData("F1", "도움말")]
    [InlineData("Ctrl+Shift+V", "블록")]
    [InlineData("Ctrl+Alt+B", "블록")]
    [InlineData("미니맵", "미니맵")]
    [InlineData("CP949", "인코딩")]
    [InlineData("Ctrl+Alt+D", "줄 편집")]
    [InlineData("500,000", "비교")]
    [InlineData("settings.json", "설정")]
    [InlineData("sessions", "복원")]
    [InlineData("다시 불러오기", "외부 변경")]
    [InlineData("100,000", "탐색기")]
    [InlineData("한도", "지원 범위")]
    public void Search_FeatureOrShortcut_FindsRelevantTopic(string query, string title)
    {
        Assert.Contains(UserGuide.Search(query), topic => topic.Title.Contains(title, StringComparison.Ordinal));
    }

    [Fact]
    public void Search_WhitespaceAndCase_MatchesAllTermsAcrossTitleAndBody()
    {
        Assert.Equal(UserGuide.Topics, UserGuide.Search(" \t\n"));
        Assert.Contains(UserGuide.Search("블록  ctrl+shift+v"), t => t.Title.Contains("블록"));
        Assert.Empty(UserGuide.Search("미니맵 nonexistent_57fd"));
    }
}
