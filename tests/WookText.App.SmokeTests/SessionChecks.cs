using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class SessionChecks
{
    internal static async Task RunAsync(string output)
    {
        string directory = Path.Combine(output, "fixtures", "session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await VerifyNoAndCancelAsync(directory);
        await VerifyRestorationAsync(directory, output);
        await VerifyCheckpointAndFailureAsync(directory);
        Console.WriteLine("PASS: No/Cancel/Yes shutdown, unsaved-session restoration, fresh disk content, missing-file recovery, explicit discard, periodic checkpoints, and failed-save shutdown cancellation.");
    }

    private static EditorPreferencesService Preferences(string directory, string name, bool remember = true)
    {
        EditorPreferencesService preferences = new(new PreferencesStore(Path.Combine(directory, name + ".json")));
        preferences.Update(preferences.Current with { RememberSession = remember });
        return preferences;
    }

    private static async Task ShowAsync(MainWindow window)
    {
        Program.ShowOffscreen(window);
        await window.InitializeSessionAsync();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static async Task VerifyNoAndCancelAsync(string directory)
    {
        int questions = 0;
        MessageBoxResult answer = MessageBoxResult.Cancel;
        MainWindow window = new(Preferences(directory, "discard", false), _ => { questions++; return answer; });
        await ShowAsync(window);
        window.ActiveDocument!.Editor.Text = string.Concat(Enumerable.Repeat("한글 unsaved line\r\n", 64000));
        window.Close();
        await window.ClosingCompletion;
        Check(window.IsVisible && window.ActiveDocument.Editor.IsModified, "Cancel must keep the window and buffer open.");
        answer = MessageBoxResult.No;
        Stopwatch elapsed = Stopwatch.StartNew();
        await Program.CloseWindowAsync(window);
        Check(questions == 2 && elapsed.Elapsed < TimeSpan.FromSeconds(5), "No must close promptly without a repeated prompt or recursive Close exception.");
        Console.WriteLine($"PASS: No closes {System.Text.Encoding.UTF8.GetByteCount(window.ActiveDocument.Editor.Text) / (1024d * 1024):0.0} MiB of unsaved text in {elapsed.Elapsed.TotalMilliseconds:0} ms.");
        Check(!File.Exists(window.SessionFilePath), "Opting out and choosing No must not retain the discarded session.");

        string path = Path.Combine(directory, "save-on-exit.txt");
        await File.WriteAllTextAsync(path, "before\n");
        MainWindow save = new(Preferences(directory, "save", false), _ => MessageBoxResult.Yes);
        await ShowAsync(save);
        await save.OpenFileAsync(path);
        save.ActiveDocument!.Editor.AppendText("after\n");
        await Program.CloseWindowAsync(save);
        Check(await File.ReadAllTextAsync(path) == "before\nafter\n", "Yes must save the file before closing.");
    }

    private static async Task VerifyRestorationAsync(string directory, string output)
    {
        string dirtyPath = Path.Combine(directory, "notes.txt"), cleanPath = Path.Combine(directory, "clean.txt"), missingPath = Path.Combine(directory, "missing.txt");
        await File.WriteAllTextAsync(dirtyPath, "원본\r\n", TextFileEncoding.Utf16LittleEndian.GetEncoding());
        await File.WriteAllTextAsync(cleanPath, "old clean\n");
        await File.WriteAllTextAsync(missingPath, "deleted file backup\n");
        EditorPreferencesService preferences = Preferences(directory, "restore");
        MainWindow window = new(preferences, _ => throw new InvalidOperationException("X must retain the session without asking to save."));
        await ShowAsync(window);
        await window.OpenFileAsync(dirtyPath);
        EditorDocument dirty = window.ActiveDocument!;
        string fingerprint = dirty.Fingerprint!;
        dirty.Editor.AppendText("저장하지 않은 변경 😀\r\n");
        await window.OpenFileAsync(cleanPath);
        await window.OpenFileAsync(missingPath);
        EditorDocument untitled = window.NewDocument();
        untitled.SetLanguage("markdown");
        untitled.Editor.Text = "# 다시 이어서 쓰는 메모\n\n이 문서는 아직 파일로 저장하지 않았습니다.\n창을 닫아도 다음 실행에서 이어집니다.\n\n- 열린 탭과 선택 위치 복원\n- 한글과 English, 이모지 😀 보존\n- 원본 파일은 Ctrl+S로 저장\n";
        untitled.Editor.Select(3, 6);
        Guid active = untitled.SessionId;
        int caret = untitled.Editor.CaretOffset;
        Stopwatch elapsed = Stopwatch.StartNew();
        await Program.CloseWindowAsync(window);
        Console.WriteLine($"PASS: retain and close four documents in {elapsed.Elapsed.TotalMilliseconds:0} ms.");
        Check(await File.ReadAllTextAsync(dirtyPath) == "원본\r\n", "Session snapshots must never autosave into the original file.");
        await File.WriteAllTextAsync(cleanPath, "new disk content\n");
        await File.WriteAllTextAsync(dirtyPath, "external change\n");
        File.Delete(missingPath);

        MainWindow restored = new(new EditorPreferencesService(new PreferencesStore(preferences.StoragePath)), _ => MessageBoxResult.No);
        await ShowAsync(restored);
        Check(restored.Documents.Count == 4 && restored.ActiveDocument?.SessionId == active, "Restart must restore tab order and the active document.");
        EditorDocument restoredDirty = restored.Documents[0];
        Check(restoredDirty.Editor.IsModified && restoredDirty.Editor.Text == dirty.Editor.Text && restoredDirty.Fingerprint == fingerprint && restoredDirty.Encoding == TextFileEncoding.Utf16LittleEndian,
            "Modified files must retain their buffer, original encoding, and original conflict fingerprint.");
        Check(restored.Documents[1].Editor.Text == "new disk content\n" && !restored.Documents[1].Editor.IsModified, "Clean files must load the latest disk content.");
        Check(restored.Documents[2].Editor.Text == "deleted file backup\n" && restored.Documents[2].Editor.IsModified, "Missing files must recover their last content as modified documents.");
        Check(restored.ActiveDocument!.Editor.Text == untitled.Editor.Text && restored.ActiveDocument.Editor.SelectionStart == 3 && restored.ActiveDocument.Editor.SelectionLength == 6 && restored.ActiveDocument.Editor.CaretOffset == caret && restored.ActiveDocument.LanguageOverride == "markdown",
            "Untitled text, syntax mode, selection, and caret must survive restart.");
        bool conflict = false;
        try { await new TextFileService().SaveAsync(dirtyPath, restoredDirty.Editor.Text, restoredDirty.Encoding, restoredDirty.Fingerprint); }
        catch (FileConflictException) { conflict = true; }
        Check(conflict && await File.ReadAllTextAsync(dirtyPath) == "external change\n", "Restored buffers must not overwrite files changed while the app was closed.");
        await Program.RenderAsync(restored, Path.Combine(output, "session-restored.png"), 1240, 800);
        EditorCommands.CloseTab.Execute(null, restored);
        await Program.WaitForIdleAsync(restored);
        await Program.CloseWindowAsync(restored);
        MainWindow afterDiscard = new(new EditorPreferencesService(new PreferencesStore(preferences.StoragePath)));
        await ShowAsync(afterDiscard);
        Check(afterDiscard.Documents.Count == 3 && afterDiscard.Documents.All(d => d.SessionId != active), "A tab explicitly discarded with No must not return on restart.");
        await Program.CloseWindowAsync(afterDiscard);
    }

    private static async Task VerifyCheckpointAndFailureAsync(string directory)
    {
        EditorPreferencesService preferences = Preferences(directory, "checkpoint");
        MainWindow window = new(preferences);
        await ShowAsync(window);
        window.ActiveDocument!.Editor.Text = string.Concat(Enumerable.Repeat("checkpoint 한글\n", 60000));
        int ticks = 0;
        DispatcherTimer heartbeat = new() { Interval = TimeSpan.FromMilliseconds(40) };
        heartbeat.Tick += (_, _) => ticks++;
        heartbeat.Start();
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!File.Exists(window.SessionFilePath))
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("A periodic checkpoint was not written.");
            await Task.Delay(50);
        }
        heartbeat.Stop();
        SessionState captured = (await new SessionStore(window.SessionFilePath!).LoadAsync()).State;
        Check(captured.Documents.Single().Text == window.ActiveDocument.Editor.Text && ticks > 10, "Periodic checkpoints must preserve text while the dispatcher remains responsive.");
        using (FileStream locked = new(window.SessionFilePath!, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            window.ActiveDocument.Editor.AppendText("last edit\n");
            window.Close();
            await window.ClosingCompletion;
            Check(window.IsVisible && window.ActiveDocument.Editor.IsModified && ((TextBlock)window.FindName("Status")).Text.Contains("종료 취소", StringComparison.Ordinal),
                "A failed final checkpoint must cancel shutdown and keep unsaved buffers accessible.");
        }
        await Program.CloseWindowAsync(window);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
