namespace WookText.Core;

public sealed record EditorPreferences
{
    public const double DefaultFontSize = 15;
    public const double MinimumFontSize = 9;
    public const double MaximumFontSize = 48;
    public string EnglishFont { get; init; } = "JetBrains Mono";
    public string KoreanFont { get; init; } = "Noto Sans KR";
    public double FontSize { get; init; } = DefaultFontSize;
    public bool SidebarVisible { get; init; } = true;
    public double SidebarWidth { get; init; } = 240;
    public bool OpenDocumentsExpanded { get; init; } = true;
    public bool ExplorerExpanded { get; init; } = true;
    public bool ShowLineNumbers { get; init; } = true;
    public bool WordWrap { get; init; }
    public bool ShowWhitespace { get; init; }

    public EditorPreferences Normalize() => this with
    {
        EnglishFont = ValidFontName(EnglishFont) ? EnglishFont.Trim() : "JetBrains Mono",
        KoreanFont = ValidFontName(KoreanFont) ? KoreanFont.Trim() : "Noto Sans KR",
        FontSize = double.IsFinite(FontSize) ? Math.Clamp(FontSize, MinimumFontSize, MaximumFontSize) : DefaultFontSize,
        SidebarWidth = double.IsFinite(SidebarWidth) ? Math.Clamp(SidebarWidth, 170, 500) : 240
    };

    private static bool ValidFontName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 160 &&
        !name.Any(c => char.IsControl(c) || c is '/' or '\\' or ':' or '#' or ',');
}
