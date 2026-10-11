namespace WookText.Core.Tests;

public sealed class IndentationTests
{
    [Theory]
    [InlineData("{\n  a\n    b\n  c\n}", 2, false)]
    [InlineData("{\n    a\n        b\n    c\n}", 4, false)]
    [InlineData("{\n\ta\n\t\tb\n\tc\n}", 4, true)]
    [InlineData("a\n\tb\n  c\n", 4, false)]
    [InlineData("// header\n * comment\n{\n  a\n  b\n}", 2, false)]
    [InlineData("no indentation", 4, false)]
    public void Detect_ExistingIndentation_PreservesStyleOrFallsBack(string text, int size, bool tabs)
    {
        IndentationOptions result = IndentationService.Detect(text, new());
        Assert.Equal(size, result.Size);
        Assert.Equal(tabs, result.UseTabs);
    }

    [Fact]
    public void Resolve_ExplicitOverride_TakesPriorityWhenDetectionDisabled()
    {
        EditorPreferences preferences = new() { IndentationByLanguage = new() { ["json"] = new() { Size = 2, DetectFromDocument = false } } };
        Assert.Equal(2, IndentationService.Resolve(preferences, "json", "test.json", "{\n    a\n}").Size);
        Assert.Equal(4, IndentationService.Resolve(preferences, "javascript", "test.js", "{}").Size);
    }

    [Fact]
    public void Resolve_FormatVariants_UseSeparateProfilesAndYamlNeverUsesTabs()
    {
        EditorPreferences preferences = new() { Indentation = new() { UseTabs = true, DetectFromDocument = false }, IndentationByLanguage = new() { ["jsonc"] = new() { Size = 2 } } };
        Assert.Equal(2, IndentationService.Resolve(preferences, "javascript", "a.jsonc", "{}").Size);
        Assert.False(IndentationService.Resolve(preferences, "yaml", "a.yml", "a: b").UseTabs);
        Assert.Equal("vue", IndentationService.ProfileKey("html", "a.vue"));
        Assert.Equal("scss", IndentationService.ProfileKey("css", "a.scss"));
    }

    [Fact]
    public void Normalize_CorruptOrMissingIndentation_UsesSafeValues()
    {
        EditorPreferences preferences = new EditorPreferences { Indentation = null!, IndentationByLanguage = null! }.Normalize();
        Assert.Equal(new IndentationOptions(), preferences.Indentation);
        Assert.Empty(preferences.IndentationByLanguage);
        Assert.Equal(8, new IndentationOptions { Size = 99 }.Normalize().Size);
        Assert.Equal(1, new IndentationOptions { Size = -1 }.Normalize().Size);
    }
}
