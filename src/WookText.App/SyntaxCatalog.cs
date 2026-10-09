using System.IO;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace WookText.App;

public sealed record SyntaxLanguage(string Id, string Label, IHighlightingDefinition? Definition, string[] Extensions);

public static class SyntaxCatalog
{
    public static IReadOnlyList<SyntaxLanguage> Languages { get; }

    static SyntaxCatalog()
    {
        HighlightingManager manager = HighlightingManager.Instance;
        foreach (IHighlightingDefinition definition in manager.HighlightingDefinitions.ToArray()) ApplyPalette(definition);
        foreach (string name in new[] { "Yaml", "JavaScript", "TypeScript", "Java", "Jsp", "Shell", "Config" })
        {
            using Stream stream = typeof(SyntaxCatalog).Assembly.GetManifestResourceStream($"WookText.App.SyntaxDefinitions.{name}.xshd")!;
            using XmlReader reader = XmlReader.Create(stream);
            IHighlightingDefinition definition = HighlightingLoader.Load(reader, manager);
            manager.RegisterHighlighting(definition.Name, [], definition);
        }
        Languages = [
            Language("text", "일반 텍스트", null, ".txt", ".log"),
            Language("yaml", "YAML", "wText YAML", ".yaml", ".yml"),
            Language("html", "HTML", "HTML", ".html", ".htm", ".xhtml", ".vue", ".svelte"),
            Language("javascript", "JavaScript", "wText JavaScript", ".js", ".mjs", ".cjs", ".jsx", ".jsonc"),
            Language("typescript", "TypeScript", "wText TypeScript", ".ts", ".tsx"),
            Language("jsp", "JSP", "wText JSP", ".jsp", ".jspf", ".tag", ".tagx"),
            Language("java", "Java", "wText Java", ".java"),
            Language("json", "JSON", "Json", ".json"),
            Language("xml", "XML", "XML", ".xml", ".xaml", ".config", ".csproj", ".props", ".resx", ".svg", ".slnx"),
            Language("css", "CSS", "CSS", ".css", ".scss", ".less"),
            Language("csharp", "C#", "C#", ".cs", ".csx"),
            Language("cpp", "C / C++", "C++", ".c", ".h", ".cpp", ".cc", ".cxx", ".hpp"),
            Language("python", "Python", "Python", ".py", ".pyw"),
            Language("sql", "SQL", "TSQL", ".sql"),
            Language("powershell", "PowerShell", "PowerShell", ".ps1", ".psm1", ".psd1"),
            Language("shell", "Shell", "wText Shell", ".sh", ".bash", ".zsh"),
            Language("config", "INI / TOML / Properties", "wText Config", ".ini", ".toml", ".properties", ".env"),
            Language("markdown", "Markdown", "MarkDown", ".md", ".markdown"),
            Language("php", "PHP", "PHP", ".php", ".phtml")
        ];
    }

    private static SyntaxLanguage Language(string id, string label, string? definitionName, params string[] extensions)
    {
        IHighlightingDefinition? definition = definitionName is null ? null : HighlightingManager.Instance.GetDefinition(definitionName)
            ?? throw new InvalidOperationException($"Missing syntax definition: {definitionName}");
        return new SyntaxLanguage(id, label, definition, extensions);
    }

    public static SyntaxLanguage Resolve(string? path, string? overrideId = null)
    {
        if (overrideId is not null) return Languages.FirstOrDefault(l => l.Id == overrideId) ?? Languages[0];
        string extension = Path.GetExtension(path ?? "").ToLowerInvariant();
        if (Path.GetFileName(path) is ".env" or ".gitconfig") extension = ".env";
        if (Path.GetFileName(path) is ".bashrc" or ".zshrc") extension = ".sh";
        return Languages.FirstOrDefault(l => l.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) ?? Languages[0];
    }

    private static void ApplyPalette(IHighlightingDefinition definition)
    {
        foreach (HighlightingColor color in definition.NamedHighlightingColors)
        {
            string name = color.Name.ToLowerInvariant();
            string value = name.Contains("comment") ? "#8A8780"
                : name.Contains("string") || name.Contains("value") || name.Contains("char") ? "#D0A76C"
                : name.Contains("number") || name.Contains("digit") || name.Contains("boolean") ? "#B9A1D7"
                : name.Contains("attribute") || name.Contains("type") ? "#DDBD8B"
                : name.Contains("keyword") || name.Contains("tag") || name.Contains("control") ? "#DA702C"
                : "#B8B8A6";
            color.Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString(value));
            color.Background = null;
        }
    }
}
