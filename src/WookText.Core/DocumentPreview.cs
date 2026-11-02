using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace WookText.Core;

public enum PreviewKind { Auto, Markdown, Html, Svg, Csv, Tsv, Json, Xml }
public sealed record PreviewContent(PreviewKind Kind, string Html);

public static class DocumentPreview
{
    public const int MaximumCharacters = 1_000_000;
    public const int MaximumItems = 10_000;
    public const int MaximumResourceBytes = 8 * 1024 * 1024;

    public static PreviewKind? Detect(string? path, string? language = null)
    {
        PreviewKind? fromExtension = Path.GetExtension(path ?? "").ToLowerInvariant() switch
        {
            ".md" or ".markdown" or ".mdown" => PreviewKind.Markdown,
            ".html" or ".htm" or ".xhtml" => PreviewKind.Html,
            ".svg" => PreviewKind.Svg,
            ".csv" => PreviewKind.Csv,
            ".tsv" => PreviewKind.Tsv,
            ".json" => PreviewKind.Json,
            ".xml" or ".xaml" or ".config" or ".csproj" or ".props" or ".resx" or ".slnx" => PreviewKind.Xml,
            _ => null
        };
        if (fromExtension is not null) return fromExtension;
        return language switch
        {
            "markdown" => PreviewKind.Markdown,
            "html" => PreviewKind.Html,
            "json" => PreviewKind.Json,
            "xml" => PreviewKind.Xml,
            null or "text" when path is null => PreviewKind.Markdown,
            _ => null
        };
    }

    public static PreviewContent Build(string text, string? path, PreviewKind kind = PreviewKind.Auto, string? language = null)
    {
        if (text.Length > MaximumCharacters) throw new IOException("미리보기는 1,000,000자까지 지원합니다. 원문은 계속 편집할 수 있습니다.");
        if (kind == PreviewKind.Auto) kind = Detect(path, language) ?? throw new NotSupportedException("자동 미리보기를 지원하지 않는 형식입니다. 위 목록에서 표시할 형식을 선택하세요.");
        string html = kind switch
        {
            PreviewKind.Markdown => Wrap(MarkdownPreview.ToHtml(text)),
            PreviewKind.Html => text,
            PreviewKind.Svg => Svg(text),
            PreviewKind.Csv => Wrap(Table(text, ',')),
            PreviewKind.Tsv => Wrap(Table(text, '\t')),
            PreviewKind.Json => Wrap(Json(text)),
            PreviewKind.Xml => Wrap(Xml(text)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return new(kind, html);
    }

    public static string Label(PreviewKind kind) => kind switch
    {
        PreviewKind.Auto => "자동",
        PreviewKind.Markdown => "Markdown",
        PreviewKind.Html => "HTML",
        PreviewKind.Svg => "SVG",
        PreviewKind.Csv => "CSV",
        PreviewKind.Tsv => "TSV",
        PreviewKind.Json => "JSON",
        PreviewKind.Xml => "XML",
        _ => "자동"
    };

    public static string ContentSecurityPolicy => "default-src 'none'; script-src 'none'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; connect-src 'none'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; sandbox allow-same-origin allow-popups";

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
    private static string Wrap(string body) => "<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><style>" +
        "html{color-scheme:dark}body{margin:24px;background:#181818;color:#dedcd5;font:15px/1.65 'Noto Sans KR','Segoe UI',sans-serif;overflow-wrap:anywhere}" +
        "h1,h2,h3{line-height:1.3}h1,h2{border-bottom:1px solid #444;padding-bottom:.35em}a{color:#ef9b65}img,svg{max-width:100%}" +
        "table{border-collapse:collapse}td,th{border:1px solid #555;padding:6px 12px;white-space:pre-wrap;vertical-align:top}th{background:#303030}" +
        "pre,code{font-family:'JetBrains Mono',Consolas,monospace;background:#252525}pre{padding:14px;overflow:auto}blockquote{margin-left:0;border-left:3px solid #da702c;padding-left:16px;color:#bbb}" +
        "details{margin-left:16px}summary{cursor:pointer}details>div{margin-left:18px}.key{color:#efb985}.value{color:#a6cba5}" +
        "</style></head><body>" + body + "</body></html>";

    private static XDocument ReadXml(string text)
    {
        using StringReader input = new(text);
        using XmlReader reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumCharacters });
        XDocument doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        int count = 0;
        foreach (XElement element in doc.Descendants())
        {
            if (++count > MaximumItems || element.Ancestors().Take(65).Count() > 64)
                throw new IOException("XML/SVG 미리보기는 요소 10,000개와 깊이 64까지 지원합니다.");
        }
        return doc;
    }

    private static string Svg(string text)
    {
        XDocument doc = ReadXml(text);
        if (doc.Root?.Name.LocalName != "svg" || doc.Root.Name.NamespaceName is not ("" or "http://www.w3.org/2000/svg"))
            throw new FormatException("SVG의 루트 요소는 <svg>여야 합니다.");
        // An image document cannot execute scripts or load subdocuments. The browser also enforces the response CSP.
        string data = Convert.ToBase64String(Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting)));
        return Wrap("<div style=\"display:inline-block;background:#f5f5f5;padding:16px\"><img alt=\"SVG 미리보기\" src=\"data:image/svg+xml;base64," + data + "\"></div>");
    }

    private static string Xml(string text)
    {
        XDocument doc = ReadXml(text);
        // Show markup as text; an XML processing instruction must never load an XSLT or execute content.
        return "<pre>" + Encode(doc.ToString()) + "</pre>";
    }

    private static string Json(string text)
    {
        using JsonDocument doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 64 });
        StringBuilder output = new(); int count = 0;
        RenderJson(doc.RootElement, "$", output, ref count);
        return output.ToString();
    }

    private static void RenderJson(JsonElement element, string label, StringBuilder output, ref int count)
    {
        if (++count > MaximumItems) throw new IOException("JSON 미리보기는 값 10,000개까지 지원합니다.");
        bool composite = element.ValueKind is JsonValueKind.Array or JsonValueKind.Object;
        output.Append(composite ? "<details open><summary>" : "<div>").Append("<span class=\"key\">").Append(Encode(label)).Append("</span>: ");
        if (composite)
        {
            output.Append(element.ValueKind == JsonValueKind.Array ? "[ ]" : "{ }").Append("</summary>");
            if (element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement child in element.EnumerateArray()) RenderJson(child, $"[{index++}]", output, ref count);
            }
            else foreach (JsonProperty child in element.EnumerateObject()) RenderJson(child.Value, child.Name, output, ref count);
            output.Append("</details>");
        }
        else output.Append("<span class=\"value\">").Append(Encode(element.GetRawText())).Append("</span></div>");
    }

    private static string Table(string text, char delimiter)
    {
        List<List<string>> rows = ParseTable(text, delimiter);
        StringBuilder output = new("<table>");
        for (int row = 0; row < rows.Count; row++)
        {
            string tag = row == 0 ? "th" : "td";
            output.Append("<tr>");
            foreach (string cell in rows[row]) output.Append('<').Append(tag).Append('>').Append(Encode(cell)).Append("</").Append(tag).Append('>');
            output.Append("</tr>");
        }
        return output.Append("</table>").ToString();
    }

    public static List<List<string>> ParseTable(string text, char delimiter)
    {
        if (text.Length > MaximumCharacters) throw new IOException("CSV/TSV 미리보기는 1,000,000자까지 지원합니다.");
        if (delimiter is not (',' or '\t')) throw new ArgumentOutOfRangeException(nameof(delimiter));
        List<List<string>> rows = []; List<string> row = []; StringBuilder cell = new();
        bool quoted = false, closedQuote = false; int cells = 0;
        void FinishCell()
        {
            if (++cells > MaximumItems) throw new IOException("CSV/TSV 미리보기는 셀 10,000개까지 지원합니다.");
            row.Add(cell.ToString()); cell.Clear(); closedQuote = false;
        }
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch != '"') cell.Append(ch);
                else if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else { quoted = false; closedQuote = true; }
            }
            else if (ch == delimiter) FinishCell();
            else if (ch is '\r' or '\n')
            {
                FinishCell(); rows.Add(row); row = [];
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
            else if (closedQuote) throw new FormatException("닫는 따옴표 다음에는 구분자 또는 줄바꿈이 필요합니다.");
            else if (ch == '"' && cell.Length == 0) quoted = true;
            else if (ch == '"') throw new FormatException("셀 안의 따옴표는 셀 전체를 따옴표로 감싸고 두 번 입력하세요.");
            else cell.Append(ch);
        }
        if (quoted) throw new FormatException("CSV/TSV의 닫는 따옴표가 없습니다.");
        if (row.Count > 0 || cell.Length > 0 || closedQuote) { FinishCell(); rows.Add(row); }
        return rows;
    }

    public static string? ResourcePath(string? documentPath, string relativeUrl)
    {
        if (documentPath is null || string.IsNullOrEmpty(relativeUrl)) return null;
        try
        {
            string decoded = Uri.UnescapeDataString(relativeUrl);
            if (decoded.Contains('\\') || decoded.Contains(':') || decoded.Contains('\0') || decoded.StartsWith('/') || Path.IsPathRooted(decoded)) return null;
            string[] parts = decoded.Split('/');
            if (parts.Any(p => p is ".." or "." || p.EndsWith('.') || p.EndsWith(' '))) return null;
            string root = Path.GetDirectoryName(Path.GetFullPath(documentPath))!;
            string path = Path.GetFullPath(Path.Combine(root, decoded));
            if (!path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return null;
            if (ResourceMime(path) is null || !File.Exists(path) || new FileInfo(path).Length > MaximumResourceBytes) return null;
            string current = path;
            // Never follow a link in the document directory or its descendants.
            while (current.Length >= root.Length)
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return null;
                if (current == root) break;
                current = Path.GetDirectoryName(current)!;
            }
            return path;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException) { return null; }
    }

    public static string? ResourceMime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".css" => "text/css; charset=utf-8",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".svg" => "image/svg+xml",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".ttf" => "font/ttf",
        ".otf" => "font/otf",
        _ => null
    };
}
