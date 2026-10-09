using System.IO;
using System.Windows.Markup;
using System.Windows.Media;
using WookText.Core;

namespace WookText.App;

public static class EditorFonts
{
    private static readonly string FontFolder = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts") + Path.DirectorySeparatorChar).AbsoluteUri.Replace(",", "%2C");
    public static FontFamily BundledEnglish { get; } = new(new Uri(FontFolder), "./#JetBrains Mono");
    public static FontFamily BundledKorean { get; } = new(new Uri(FontFolder), "./#Noto Sans KR");
    public static IReadOnlyList<string> AvailableFamilies { get; } = new[] { "JetBrains Mono", "Noto Sans KR" }
        .Concat(Fonts.SystemFontFamilies.Select(f => f.Source)).Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public static FontFamily Create(EditorPreferences preferences)
    {
        string english = Resolve(preferences.EnglishFont, "JetBrains Mono");
        string korean = Resolve(preferences.KoreanFont, "Noto Sans KR");
        FontFamily composite = new();
        composite.FamilyNames.Add(XmlLanguage.GetLanguage("en-us"), "wText editor");
        // Explicit Hangul mapping also works when the English font has Korean glyphs.
        composite.FamilyMaps.Add(new FontFamilyMap
        {
            Unicode = "1100-11FF,3130-318F,A960-A97F,AC00-D7AF,D7B0-D7FF,FFA0-FFDC",
            Target = korean
        });
        composite.FamilyMaps.Add(new FontFamilyMap { Target = english + ", " + korean + ", Segoe UI Symbol" });
        return composite;
    }

    private static string Resolve(string name, string fallback)
    {
        name = AvailableFamilies.FirstOrDefault(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase)) ?? fallback;
        return name is "JetBrains Mono" or "Noto Sans KR" ? FontFolder + "#" + name : name;
    }
}
