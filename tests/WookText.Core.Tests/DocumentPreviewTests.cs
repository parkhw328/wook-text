using System.Text.Json;
using System.Xml;

namespace WookText.Core.Tests;

public sealed class DocumentPreviewTests
{
    [Theory]
    [InlineData("note.MD", PreviewKind.Markdown)]
    [InlineData("index.html", PreviewKind.Html)]
    [InlineData("icon.svg", PreviewKind.Svg)]
    [InlineData("report.csv", PreviewKind.Csv)]
    [InlineData("report.tsv", PreviewKind.Tsv)]
    [InlineData("settings.json", PreviewKind.Json)]
    [InlineData("project.csproj", PreviewKind.Xml)]
    public void Detect_SupportedPath_SelectsRenderer(string path, PreviewKind kind) => Assert.Equal(kind, DocumentPreview.Detect(path));

    [Fact]
    public void Build_UnsavedOrUnknownBuffer_UsesChosenFormatWithoutChangingSource()
    {
        const string text = "<main style='display:grid'><h1>레이아웃</h1></main>";
        Assert.Equal(text, DocumentPreview.Build(text, null, PreviewKind.Html).Html);
        Assert.Equal(PreviewKind.Html, DocumentPreview.Build(text, null, language: "html").Kind);
        Assert.Equal(PreviewKind.Markdown, DocumentPreview.Build("# 제목", null).Kind);
        Assert.Null(DocumentPreview.Detect("program.cs"));
        Assert.Throws<NotSupportedException>(() => DocumentPreview.Build(text, "program.cs"));
    }

    [Fact]
    public void Build_Markdown_RendersTablesAndEscapesHtml()
    {
        string html = DocumentPreview.Build("# 제목\n\n<script>alert(1)</script>\n\n| A | B |\n|---|---|\n| 1 | 2 |", "note.md").Html;
        Assert.Contains("<h1>", html); Assert.Contains("<table>", html);
        Assert.DoesNotContain("<script>", html); Assert.Contains("&lt;script&gt;", html);
    }

    [Theory]
    [InlineData(',')]
    [InlineData('\t')]
    public void ParseTable_QuotedMultilineAndEmptyCells_PreservesValues(char separator)
    {
        string text = $"name{separator}value{separator}\r\n\"a{separator}b\"{separator}\"one\r\ntwo \"\"quoted\"\"\"{separator}\r\n";
        var rows = DocumentPreview.ParseTable(text, separator);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "name", "value", "" }, rows[0]);
        Assert.Equal(new[] { $"a{separator}b", "one\r\ntwo \"quoted\"", "" }, rows[1]);
        Assert.Equal(new[] { "", "" }, Assert.Single(DocumentPreview.ParseTable(separator.ToString(), separator)));
        Assert.Empty(DocumentPreview.ParseTable("", separator));
    }

    [Theory]
    [InlineData("\"unclosed")]
    [InlineData("\"closed\"tail")]
    [InlineData("unquoted\"quote")]
    public void ParseTable_InvalidQuotes_ExplainsError(string text) => Assert.Throws<FormatException>(() => DocumentPreview.ParseTable(text, ','));

    [Fact]
    public void Build_JsonAndTables_EncodeActiveMarkup()
    {
        string json = DocumentPreview.Build("{\"<img>\":\"<script>\",\"nested\":[true,null]}", "a.json").Html;
        Assert.Contains("<details open>", json); Assert.Contains("&lt;img&gt;", json); Assert.DoesNotContain("<script>", json);
        string csv = DocumentPreview.Build("a,b\n<script>,<img>", "a.csv").Html;
        Assert.Contains("<td>&lt;script&gt;</td>", csv);
        Assert.ThrowsAny<JsonException>(() => DocumentPreview.Build("{unfinished", "a.json"));
    }

    [Fact]
    public void Build_XmlAndSvg_RejectEntitiesAndKeepMarkupInert()
    {
        const string entity = "<!DOCTYPE a [<!ENTITY x SYSTEM 'file:///private.txt'>]><a>&x;</a>";
        Assert.Throws<XmlException>(() => DocumentPreview.Build(entity, "a.xml"));
        Assert.Throws<XmlException>(() => DocumentPreview.Build(entity, "a.svg"));
        Assert.Contains("&lt;script&gt;", DocumentPreview.Build("<script>hello</script>", "a.xml").Html);
        Assert.Contains("data:image/svg+xml;base64,", DocumentPreview.Build("<svg xmlns='http://www.w3.org/2000/svg'><rect width='10' height='20'/></svg>", "a.svg").Html);
        Assert.Throws<FormatException>(() => DocumentPreview.Build("<html/>", "a.svg"));
    }

    [Fact]
    public void Build_Limits_RejectsExcessiveSizeAndStructure()
    {
        Assert.Throws<IOException>(() => DocumentPreview.Build(new string('x', DocumentPreview.MaximumCharacters + 1), "a.html"));
        Assert.Throws<IOException>(() => DocumentPreview.Build(new string(',', DocumentPreview.MaximumItems), "a.csv"));
        Assert.Throws<IOException>(() => DocumentPreview.Build("[" + string.Join(',', Enumerable.Repeat("0", DocumentPreview.MaximumItems)) + "]", "a.json"));
        Assert.Throws<IOException>(() => DocumentPreview.Build(string.Concat(Enumerable.Repeat("<a>", 66)) + string.Concat(Enumerable.Repeat("</a>", 66)), "a.xml"));
    }

    [Fact]
    public void ResourcePath_LocalCssAndImages_RejectsTraversalLinksAndOversizedFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        try
        {
            string document = Path.Combine(root, "index.html"), css = Path.Combine(root, "assets", "theme.css");
            File.WriteAllText(css, "main { display: grid }");
            Assert.Equal(css, DocumentPreview.ResourcePath(document, "assets/theme.css"));
            File.WriteAllText(Path.Combine(root, "한 글.png"), "test");
            Assert.NotNull(DocumentPreview.ResourcePath(document, Uri.EscapeDataString("한 글.png")));
            foreach (string denied in new[] { "../theme.css", "%2e%2e/theme.css", "assets/../../theme.css", "/assets/theme.css", "https://example.org/a.css", "file:///a.css", "assets\\theme.css", "a.js", "a.html" })
                Assert.Null(DocumentPreview.ResourcePath(document, denied));
            Assert.Null(DocumentPreview.ResourcePath(null, "assets/theme.css"));
            string large = Path.Combine(root, "large.png");
            using (FileStream stream = File.Create(large)) stream.SetLength(DocumentPreview.MaximumResourceBytes + 1L);
            Assert.Null(DocumentPreview.ResourcePath(document, "large.png"));
            if (!OperatingSystem.IsWindows())
            {
                File.CreateSymbolicLink(Path.Combine(root, "link.css"), css);
                Assert.Null(DocumentPreview.ResourcePath(document, "link.css"));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Session_PreviewModeAndVisibility_RoundTripsAndReadsLegacyField()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            using SessionStore store = new(path);
            await store.SaveAsync(new() { Documents = [new() { Text = "<h1>draft</h1>", PreviewVisible = true, PreviewKind = PreviewKind.Html }, new() { MarkdownPreview = true, Text = "# legacy" }] });
            var loaded = await store.LoadAsync();
            Assert.True(loaded.State.Documents[0].PreviewVisible);
            Assert.Equal(PreviewKind.Html, loaded.State.Documents[0].PreviewKind);
            Assert.True(loaded.State.Documents[1].MarkdownPreview);
        }
        finally { File.Delete(path); File.Delete(path + ".bak"); }
    }

    [Fact]
    public void Preferences_FontChoiceAndRuntimeConsent_PersistWithoutChangingOtherSettings()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            PreferencesStore store = new(path);
            EditorPreferences defaults = store.Load(out _);
            Assert.True(defaults.PdfIgnoreOriginalFont); Assert.False(defaults.PreviewRuntimeLicenseAccepted);
            store.Save(defaults with { PdfIgnoreOriginalFont = false, PreviewRuntimeLicenseAccepted = true, FontSize = 18 });
            EditorPreferences loaded = store.Load(out _);
            Assert.False(loaded.PdfIgnoreOriginalFont); Assert.True(loaded.PreviewRuntimeLicenseAccepted); Assert.Equal(18, loaded.FontSize);
        }
        finally { File.Delete(path); }
    }
}
