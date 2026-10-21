using System.IO;
using System.Windows.Input;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class ExternalChangeChecks
{
    internal static async Task RunAsync(string output)
    {
        string directory = Path.Combine(output, "fixtures", "external-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "notes.txt");
        await File.WriteAllTextAsync(path, "original text\r\n");
        EditorPreferencesService preferences = new(new PreferencesStore(Path.Combine(directory, "settings.json")));
        MainWindow window = new(preferences);
        List<AppDialogRequest> notices = [];
        AppDialogResult answer = AppDialogResult.Primary;
        window.ExternalChangeDialog = request => { notices.Add(request); return answer; };
        Program.ShowOffscreen(window);
        await window.InitializeSessionAsync();
        await window.OpenFileAsync(path);
        EditorDocument document = window.ActiveDocument!;
        document.SetLanguage("markdown");
        document.Editor.CaretOffset = 4;
        await File.WriteAllTextAsync(path, "external text\r\n", TextFileEncoding.Utf16LittleEndian.GetEncoding());
        await window.CheckExternalChangesAsync();
        Check(document.Editor.Text == "external text\r\n" && !document.Editor.IsModified, "Clean buffer must reload.");
        Check(document.Encoding == TextFileEncoding.Utf16LittleEndian && document.LanguageOverride == "markdown" && document.Editor.CaretOffset == 4,
            "Reload must preserve language/view and detect the new encoding.");
        Check(notices.Count == 1, "Reload must notify once.");
        await window.CheckExternalChangesAsync();
        Check(notices.Count == 1, "Unchanged content must not repeat notices.");

        document.Editor.AppendText("local edit");
        string local = document.Editor.Text, baseline = document.Fingerprint!;
        await File.WriteAllTextAsync(path, "disk version two");
        await window.CheckExternalChangesAsync();
        Check(document.Editor.Text == local && document.Fingerprint == baseline && document.Editor.IsModified,
            "Keep editing must retain buffer, dirty state, and original save conflict baseline.");
        await window.CheckExternalChangesAsync();
        Check(notices.Count == 2, "Declined version must not nag on each poll.");
        try
        {
            await new TextFileService().SaveAsync(path, local, document.Encoding, document.Fingerprint);
            throw new InvalidOperationException("A retained conflict must block overwriting the disk.");
        }
        catch (FileConflictException) { }
        answer = AppDialogResult.Cancel;
        await File.WriteAllTextAsync(path, "disk version three");
        await window.CheckExternalChangesAsync();
        Check(document.Editor.Text == local, "Esc/X cancellation must preserve edits.");
        answer = AppDialogResult.Secondary;
        await File.WriteAllTextAsync(path, "four");
        await window.CheckExternalChangesAsync();
        Check(document.Editor.Text == "four" && !document.Editor.IsModified, "Explicit discard must reload the changed disk version.");
        Check(document.Editor.CaretOffset <= 4, "Caret must be clamped for shorter content.");

        document.Editor.AppendText(" saved");
        ApplicationCommands.Save.Execute(null, window);
        await Program.WaitForIdleAsync(window);
        int before = notices.Count;
        await window.CheckExternalChangesAsync();
        Check(notices.Count == before, "Own saves must not produce external-change notices.");
        File.Delete(path);
        await window.CheckExternalChangesAsync();
        Check(document.Editor.Text == "four saved" && document.Editor.IsModified, "Deletion must retain a recoverable dirty buffer.");
        await window.CheckExternalChangesAsync();
        Check(notices.Count == before + 1, "Missing file must notify only once.");
        await File.WriteAllTextAsync(path, "recreated");
        await window.CheckExternalChangesAsync();
        Check(document.Editor.Text == "recreated", "Recreation must resume monitoring.");
        document.Editor.AppendText(" local");
        answer = AppDialogResult.Cancel;
        EditorCommands.Reload.Execute(null, window);
        await Program.WaitForIdleAsync(window);
        Check(document.Editor.Text == "recreated local", "Manual reload cancellation must retain edits.");
        answer = AppDialogResult.Secondary;
        EditorCommands.Reload.Execute(null, window);
        await Program.WaitForIdleAsync(window);
        Check(document.Editor.Text == "recreated" && !document.Editor.IsModified, "Manual reload must recover a declined disk version.");
        EditorDocument other = window.NewDocument();
        other.Editor.Text = "untitled work";
        await File.WriteAllTextAsync(path, "background tab");
        await window.CheckExternalChangesAsync();
        Check(document.Editor.Text == "background tab" && window.ActiveDocument == other && other.Editor.Text == "untitled work",
            "Monitoring must update inactive file tabs without switching tabs or touching untitled work.");
        await Program.CloseWindowAsync(window);
        await window.CheckExternalChangesAsync();
        Console.WriteLine("PASS: external reload, encoding/view, keep/cancel/discard, deduplication, save conflicts, own saves, deletion and recreation.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
