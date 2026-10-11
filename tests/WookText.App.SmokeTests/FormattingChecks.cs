using System.IO;
using System.Windows;
using System.Windows.Controls;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class FormattingChecks
{
    public static async Task RunAsync(MainWindow window, string output)
    {
        TextFormattingService service = new();
        const string twoSpaces = "{\n  \"name\": \"wText\",\n  \"value\": 1\n}\n";
        Check(await service.FormatAsync(twoSpaces, "json") == twoSpaces, "Existing two-space JSON must not be rewritten to four spaces.");
        string tabbed = twoSpaces.Replace("  ", "\t");
        Check(await service.FormatAsync(tabbed, "json") == tabbed, "Existing tab indentation must be preserved.");
        EditorPreferences explicitIndent = new() { IndentationByLanguage = new() { ["json"] = new() { Size = 4, DetectFromDocument = false } } };
        Check(await service.FormatAsync(twoSpaces, "json", preferences: explicitIndent) == twoSpaces.Replace("  ", "    "), "Disabling detection must enforce the configured width.");
        explicitIndent = explicitIndent with { IndentationByLanguage = new() { ["json"] = new() { Size = 2, UseTabs = true, DetectFromDocument = false } } };
        Check(await service.FormatAsync(twoSpaces, "text", preferences: explicitIndent) == tabbed, "Pasted JSON must use the JSON profile.");
        (string Language, string Path, string Text)[] samples =
        [
            ("json", "a.json", "{\"이름\":\"한글\",\"items\":[1,2,3],\"nested\":{\"a\":true}}"),
            ("javascript", "a.js", "// keep comment\nfunction greet(name){return {name:name,value:1};}"),
            ("javascript", "a.jsonc", "{\n// keep comment\n\"a\":1,\"b\":[2,3]\n}"),
            ("typescript", "a.ts", "interface Item{name:string;count:number}const item:Item={name:'한글',count:2};"),
            ("typescript", "a.tsx", "const element=<div title='한글'><span>hello</span></div>;"),
            ("html", "a.html", "<div><ul><li>one</li><li>two</li></ul></div>"),
            ("html", "a.vue", "<template><div><span>한글</span></div></template>"),
            ("css", "a.css", "body{color:red;background:black}h1{margin:0}"),
            ("css", "a.scss", "$color:red;body{color:$color;.item{margin:0;}}"),
            ("css", "a.less", "@color:red;body{color:@color;.item{margin:0;}}"),
            ("yaml", "a.yml", "name: 한글\nitems: [one,two,three]\n"),
            ("markdown", "a.md", "# Title\n\n-  one\n-   two\n\n|a|b|\n|-|-|\n|1|2|\n")
        ];
        foreach (var sample in samples)
        {
            string formatted = await service.FormatAsync(sample.Text, sample.Language, sample.Path);
            Check(formatted.Length > 0 && formatted != sample.Text, $"Formatter did not change {sample.Path}.");
            Check(await service.FormatAsync(formatted, sample.Language, sample.Path) == formatted, $"Formatter is not idempotent: {sample.Path}.");
            if (sample.Text.Contains("한글")) Check(formatted.Contains("한글"), "Korean text must survive formatting.");
            if (sample.Text.Contains("keep comment")) Check(formatted.Contains("keep comment"), "Comments must survive formatting.");
        }
        foreach (string newline in new[] { "\n", "\r\n", "\r" })
        {
            string formatted = await service.FormatAsync("{" + newline + "\"a\":1,\"b\":2}" + newline, "json");
            Check(formatted.EndsWith(newline) && !formatted.Replace(newline, "").Contains('\n') && !formatted.Replace(newline, "").Contains('\r'), "Existing newline style must survive formatting.");
        }
        foreach (string invalid in new[] { "{\"a\":", "{broken}" })
        {
            try { await service.FormatAsync(invalid, "json"); throw new Exception("Invalid JSON was formatted."); }
            catch (InvalidOperationException ex) { Check(ex.Message.Contains("원문"), "Invalid syntax must be explained."); }
        }
        using (CancellationTokenSource cancelled = new())
        {
            cancelled.Cancel();
            try { await service.FormatAsync("{\"a\":1}", "json", cancellationToken: cancelled.Token); throw new Exception("Cancelled formatting succeeded."); }
            catch (InvalidOperationException ex) { Check(ex.Message.Contains("취소"), "Cancellation must retain the document."); }
        }

        EditorDocument document = window.NewDocument();
        const string original = "{\"name\":\"한글 wText\",\"editor\":{\"fontSize\":15,\"wordWrap\":true},\"files\":[\"app.ts\",\"style.css\"]}";
        document.Editor.Text = original;
        document.Editor.Document.UndoStack.ClearAll();
        document.Editor.IsModified = false;
        var buffer = document.Editor.Document;
        EditorCommands.FormatDocument.Execute(null, window);
        await Program.WaitForIdleAsync(window);
        Check(document.Editor.Text != original && document.Editor.IsModified && ReferenceEquals(buffer, document.Editor.Document), "Format command must replace text in the original buffer and mark it dirty.");
        string formattedText = document.Editor.Text;
        document.Editor.Undo();
        Check(document.Editor.Text == original && !document.Editor.IsModified, "One Undo must restore the original saved state.");
        document.Editor.Redo();
        Check(document.Editor.Text == formattedText, "Redo must restore formatting.");
        await window.FormatDocumentAsync();
        await Program.RenderAsync(window, Path.Combine(output, "formatting.png"), 1240, 800);
        document.Editor.Undo();
        Check(document.Editor.Text == original, "Formatting unchanged text must not create an extra undo step.");
        document.SetLanguage("json");
        document.Editor.Text = "{broken}";
        await DialogChecks.WithDialogAsync(() => EditorCommands.FormatDocument.Execute(null, window), dialog => dialog.Close());
        await Program.WaitForIdleAsync(window);
        Check(document.Editor.Text == "{broken}" && ReferenceEquals(buffer, document.Editor.Document), "Syntax errors must leave the document and undo history untouched.");
        document.Editor.Text = original;
        document.Editor.IsModified = false;
        EditorCommands.CloseTab.Execute(null, window);
        await Program.WaitForIdleAsync(window);

        MenuItem view = (MenuItem)window.FindName("ViewMenu");
        Check(view.Items.OfType<MenuItem>().Any(item => item.Command == EditorCommands.Compare)
            && view.Items.OfType<MenuItem>().Any(item => item.Command == EditorCommands.FormatDocument), "View must contain comparison and formatting.");
        Console.WriteLine("PASS: bundled formatters, comments, Korean, line endings, invalid input, cancellation, format command, undo/redo, and View menu.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
