namespace WookText.Core.Tests;

public sealed class PreferencesStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wText-preferences-" + Guid.NewGuid().ToString("N"));
    private PreferencesStore Store => new(Path.Combine(_directory, "settings.json"));

    [Fact]
    public void Load_MissingFile_UsesDefaultsWithoutCreatingFiles()
    {
        Assert.Equal(new EditorPreferences(), Store.Load(out string? warning));
        Assert.Null(warning);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void Save_CustomPreferences_RoundTripsAcrossInstances()
    {
        EditorPreferences expected = new()
        {
            EnglishFont = "Consolas",
            KoreanFont = "맑은 고딕",
            FontSize = 18.5,
            SidebarVisible = false,
            SidebarWidth = 315,
            OpenDocumentsExpanded = false,
            ExplorerExpanded = false,
            ShowLineNumbers = false,
            WordWrap = true,
            ShowWhitespace = true
        };
        Store.Save(expected);
        Assert.Equal(expected, Store.Load(out string? warning));
        Assert.Null(warning);
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData("{broken json")]
    [InlineData("{\"FontSize\":\"large\"}")]
    public void Load_InvalidFile_ReportsWarningAndPreservesOriginal(string contents)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Store.FilePath, contents);
        Assert.Equal(new EditorPreferences(), Store.Load(out string? warning));
        Assert.NotNull(warning);
        Assert.Equal(contents, File.ReadAllText(Store.FilePath));
    }

    [Fact]
    public void Load_OversizedFile_ReportsWarning()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Store.FilePath, new string(' ', 65 * 1024));
        Assert.Equal(new EditorPreferences(), Store.Load(out string? warning));
        Assert.NotNull(warning);
    }

    [Fact]
    public void Load_PartialOrFutureSettings_KeepsDefaultsForMissingProperties()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Store.FilePath, "{\"FontSize\":20,\"FutureOption\":true}");
        Assert.Equal(new EditorPreferences { FontSize = 20 }, Store.Load(out string? warning));
        Assert.Null(warning);
    }

    [Fact]
    public void Save_LockedDestination_PreservesSavedSettingsAndCleansTemporaryFile()
    {
        Store.Save(new EditorPreferences { FontSize = 18 });
        using (FileStream locked = new(Store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Exception? failure = Record.Exception(() => Store.Save(new EditorPreferences { FontSize = 24 }));
            Assert.True(failure is IOException or UnauthorizedAccessException, "A locked destination must reject replacement.");
        }
        Assert.Equal(18, Store.Load(out _).FontSize);
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData(-10, 9, 10, 170)]
    [InlineData(100, 48, 900, 500)]
    [InlineData(double.NaN, 15, double.PositiveInfinity, 240)]
    public void Normalize_InvalidDimensions_UsesSafeValues(double size, double expectedSize, double width, double expectedWidth)
    {
        EditorPreferences actual = new EditorPreferences { FontSize = size, SidebarWidth = width }.Normalize();
        Assert.Equal(expectedSize, actual.FontSize);
        Assert.Equal(expectedWidth, actual.SidebarWidth);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("https://example.invalid/font#Family")]
    [InlineData("A, B")]
    public void Normalize_InvalidFontNames_UsesBundledFonts(string? name)
    {
        EditorPreferences actual = new EditorPreferences { EnglishFont = name!, KoreanFont = name! }.Normalize();
        Assert.Equal("JetBrains Mono", actual.EnglishFont);
        Assert.Equal("Noto Sans KR", actual.KoreanFont);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
