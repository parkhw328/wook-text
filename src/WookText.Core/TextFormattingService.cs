using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace WookText.Core;

public sealed class TextFormattingService
{
    public const int MaximumCharacters = 2_000_000;

    public static string? ResolveParser(string language, string? path, string text)
    {
        string extension = Path.GetExtension(path ?? "").ToLowerInvariant();
        return language switch
        {
            "json" => "json",
            "javascript" => extension == ".jsonc" ? "jsonc" : "babel",
            "typescript" => "typescript",
            "html" => extension switch { ".vue" => "vue", ".svelte" => null, _ => "html" },
            "css" => extension switch { ".scss" => "scss", ".less" => "less", _ => "css" },
            "yaml" => "yaml",
            "markdown" => "markdown",
            // Untitled/pasted JSON can be detected without guessing arbitrary code.
            "text" when IsJson(text) => "json",
            _ => null
        };
    }

    private static bool IsJson(string text)
    {
        if (!text.TrimStart().StartsWith('{') && !text.TrimStart().StartsWith('[')) return false;
        try { using JsonDocument document = JsonDocument.Parse(text); return true; }
        catch (JsonException) { return false; }
    }

    public async Task<string> FormatAsync(string text, string language, string? path = null,
        string? formatterDirectory = null, CancellationToken cancellationToken = default, EditorPreferences? preferences = null)
    {
        if (text.Length > MaximumCharacters)
            throw new InvalidOperationException("자동 정렬은 200만 자 이하의 문서에서 사용할 수 있습니다.");
        string parser = ResolveParser(language, path, text) ?? throw new InvalidOperationException(
            "이 언어는 자동 정렬을 지원하지 않습니다. JSON, JSONC, JavaScript, TypeScript, HTML, Vue, CSS, SCSS, Less, YAML, Markdown을 지원합니다. 붙여넣은 문서는 언어 메뉴에서 형식을 선택해 주세요.");
        if (string.IsNullOrWhiteSpace(text)) return text;
        // Pasted JSON uses the JSON profile even before the user selects a language.
        IndentationOptions indentation = IndentationService.Resolve(preferences ?? new(),
            language == "text" && parser == "json" ? "json" : language, path, text);
        string directory = formatterDirectory ?? Path.Combine(AppContext.BaseDirectory, "Formatting");
        ProcessStartInfo start = new(Path.Combine(directory, "node.exe"))
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false, true),
            StandardOutputEncoding = new UTF8Encoding(false, true),
            StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("--max-old-space-size=256");
        start.ArgumentList.Add(Path.Combine(directory, "format.cjs"));
        start.Environment.Remove("NODE_OPTIONS");
        start.Environment.Remove("NODE_PATH");
        using Process process = new() { StartInfo = start };
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            process.Start();
            Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { text, parser, tabWidth = indentation.Size, useTabs = indentation.UseTabs }).AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            string result = await output;
            string detail = await error;
            if (process.ExitCode != 0)
                throw new InvalidOperationException("문법을 확인해 주세요. 원문은 변경하지 않았습니다.\n\n" + detail);
            using JsonDocument response = JsonDocument.Parse(result);
            string formatted = response.RootElement.GetProperty("text").GetString()!;
            // Keep the first existing line-ending style and the presence of a final newline.
            string newline = "\n";
            int first = text.IndexOfAny(['\r', '\n']);
            if (first >= 0 && text[first] == '\r') newline = first + 1 < text.Length && text[first + 1] == '\n' ? "\r\n" : "\r";
            if (!text.EndsWith('\r') && !text.EndsWith('\n')) formatted = formatted.TrimEnd('\n');
            return newline == "\n" ? formatted : formatted.Replace("\n", newline);
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException("자동 정렬을 취소했거나 30초 제한을 초과했습니다. 원문은 변경하지 않았습니다.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("자동 정렬 구성요소를 실행할 수 없습니다. 배포본을 다시 설치하거나 전체 압축 해제해 주세요.", ex);
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
    }
}
