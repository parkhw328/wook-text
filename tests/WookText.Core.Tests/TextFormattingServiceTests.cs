namespace WookText.Core.Tests;

public sealed class TextFormattingServiceTests
{
    [Theory]
    [InlineData("json", "test.json", "{}", "json")]
    [InlineData("javascript", "test.jsonc", "{}", "jsonc")]
    [InlineData("typescript", "test.tsx", "", "typescript")]
    [InlineData("html", "test.vue", "", "vue")]
    [InlineData("css", "test.scss", "", "scss")]
    [InlineData("css", "test.less", "", "less")]
    [InlineData("text", null, "{\"한글\": [1, 2]}", "json")]
    [InlineData("text", null, "[true, null]", "json")]
    [InlineData("text", null, "{broken}", null)]
    [InlineData("python", "test.py", "print('hi')", null)]
    [InlineData("html", "test.svelte", "", null)]
    public void ResolveParser_LanguageAndExtension_SelectsSupportedParser(string language, string? path, string text, string? expected) =>
        Assert.Equal(expected, TextFormattingService.ResolveParser(language, path, text));

    [Fact]
    public async Task FormatAsync_TooLarge_RejectsBeforeStartingProcess() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TextFormattingService().FormatAsync(new string(' ', TextFormattingService.MaximumCharacters + 1), "json"));

    [Fact]
    public async Task FormatAsync_UnsupportedLanguage_ExplainsSupportedFormats() =>
        Assert.Contains("JSON", (await Assert.ThrowsAsync<InvalidOperationException>(() => new TextFormattingService().FormatAsync("print(1)", "python"))).Message);
}
