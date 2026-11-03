using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class LargeFileChecks
{
    internal static async Task RunAsync(string output)
    {
        string directory = Path.Combine(output, "fixtures", "large-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "100MiB.log");
        byte[] line = Encoding.UTF8.GetBytes("2026-11-03 INFO 로그 https://example.com/log user@example.com " + new string('x', 65) + "\r\n");
        byte[] block = Enumerable.Repeat(line, 1024).SelectMany(b => b).ToArray();
        await using (FileStream stream = File.Create(path))
        {
            while (stream.Length < 100 * 1024 * 1024) await stream.WriteAsync(block);
        }
        EditorPreferencesService preferences = new(new PreferencesStore(Path.Combine(directory, "settings.json")));
        preferences.Update(preferences.Current with { WordWrap = true, ShowWhitespace = true, ShowMinimap = true, RememberSession = true });
        MessageBoxResult answer = MessageBoxResult.Cancel;
        int prompts = 0;
        MainWindow window = new(preferences, _ => { prompts++; return answer; });
        Program.ShowOffscreen(window);
        await window.InitializeSessionAsync();
        int ticks = 0;
        DispatcherTimer heartbeat = new() { Interval = TimeSpan.FromMilliseconds(20) };
        heartbeat.Tick += (_, _) => ticks++;
        heartbeat.Start();
        Stopwatch watch = Stopwatch.StartNew();
        await window.OpenFileAsync(path);
        heartbeat.Stop();
        watch.Stop();
        EditorDocument document = window.ActiveDocument!;
        Check(document.IsLargeFile && ticks > 0, "100 MiB load must build the buffer off the UI thread and enter large-file mode.");
        Check(document.Editor.SyntaxHighlighting is null && !document.Editor.WordWrap && !document.Editor.Options.ShowSpaces &&
            document.Minimap.Visibility == Visibility.Collapsed, "Expensive display features must be disabled before rendering.");
        Check(!EditorCommands.DocumentPreview.CanExecute(null, window) && !EditorCommands.FormatDocument.CanExecute(null, window),
            "Preview and formatting commands must be disabled.");
        document.TogglePreview();
        Check(!document.PreviewVisible, "Direct preview entry must also reject a large buffer.");
        preferences.Update(preferences.Current with { FontSize = 15, WordWrap = true, ShowMinimap = true });
        Check(!document.Editor.WordWrap && document.Minimap.Visibility == Visibility.Collapsed, "Preference changes must preserve per-document limits.");
        await LargeFileScrollChecks.RunAsync(window, document);
        var beforeNavigation = document.Editor.Document.Version;
        int lastLine = document.Editor.Document.LineCount;
        await GoToLineChecks.NavigateAsync(window, lastLine.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Check(document.Editor.TextArea.Caret.Line == lastLine && document.Editor.VerticalOffset > 0 &&
            document.Editor.Document.Version.CompareAge(beforeNavigation) == 0, "Ctrl+G must reach the end of a 100 MiB log without modifying it.");
        await GoToLineChecks.NavigateAsync(window, "1");
        SessionDocumentState metadata = document.CaptureSession()();
        Check(metadata.ReopenFromDisk && metadata.Text.Length == 0, "Checkpoint capture must omit the large body.");

        document.Editor.AppendText("BULK-SEARCH-END\r\n");
        ApplicationCommands.Find.Execute(null, window);
        await Program.WaitForIdleAsync(window);
        ((TextBox)window.FindName("FindBox")).Text = "BULK-SEARCH-END";
        document.Editor.Select(0, 0);
        ((Button)window.FindName("ReplaceAllButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!((Button)window.FindName("ReplaceAllButton")).IsEnabled && !((CheckBox)window.FindName("EditAllMatches")).IsEnabled,
            "Bulk replacements and linked editing must be disabled.");
        // Use the same F3 route as the physical key; wait for background search selection.
        window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.F3)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (document.Editor.SelectionLength != "BULK-SEARCH-END".Length)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Large-file search did not select the end marker.");
            await Task.Delay(20);
        }
        Check(document.Editor.SelectedText == "BULK-SEARCH-END", "Chunked search must reach the end of the file.");
        await Program.RenderAsync(window, Path.Combine(output, "large-file.png"), 1240, 800);
        window.Close();
        await window.ClosingCompletion;
        Check(window.IsVisible && document.IsModified && prompts == 1, "Cancel must keep large unsaved edits even with session restore enabled.");
        answer = MessageBoxResult.Yes;
        await Program.CloseWindowAsync(window);
        Check(prompts == 2 && !document.IsModified, "Yes must stream-save before closing.");
        Check(new FileInfo(window.SessionFilePath!).Length < 4096, "Large session metadata must remain small.");
        MainWindow restored = new(new EditorPreferencesService(new PreferencesStore(preferences.StoragePath)), _ => MessageBoxResult.No);
        Program.ShowOffscreen(restored);
        await restored.InitializeSessionAsync();
        EditorDocument reopened = restored.ActiveDocument!;
        Check(reopened.IsLargeFile && reopened.Editor.Document.GetText(reopened.Editor.Document.TextLength - 17, 17) == "BULK-SEARCH-END\r\n",
            "Restart must read the saved file instead of restoring an empty buffer.");
        string savedHash = await TextFileService.GetFingerprintAsync(path);
        reopened.Editor.AppendText("discard this\r\n");
        await Program.CloseWindowAsync(restored);
        Check(await TextFileService.GetFingerprintAsync(path) == savedHash, "No must discard changes without modifying the source.");

        File.Delete(path);
        MainWindow missing = new(new EditorPreferencesService(new PreferencesStore(preferences.StoragePath)), _ => MessageBoxResult.No);
        Task? restoration = null;
        await DialogChecks.WithDialogAsync(() =>
        {
            Program.ShowOffscreen(missing);
            restoration = missing.InitializeSessionAsync();
        }, dialog => ((Button)dialog.FindName("PrimaryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
        await restoration!;
        Check(missing.Documents.All(d => d.FilePath != path) && !File.Exists(path),
            "An unavailable large source must be reported, never restored as an empty saved file.");
        await Program.CloseWindowAsync(missing);
        Console.WriteLine($"PASS: 100 MiB streaming load {watch.Elapsed.TotalSeconds:0.00}s, heartbeat {ticks}, large-file limits, search, save/cancel/discard, and restart. Peak process working set: {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d:0.0} MiB.");

        using EditorDocument growing = new("pasted", preferences);
        growing.Editor.Text = string.Concat(Enumerable.Repeat("line\n", LargeFilePolicy.ThresholdLines));
        Check(growing.IsLargeFile && growing.Minimap.Visibility == Visibility.Collapsed, "A growing buffer must enter large mode without reopening.");
        Check(!growing.Editor.Options.EnableHyperlinks && !growing.Editor.Options.EnableEmailHyperlinks &&
            ScrollViewer.GetIsDeferredScrollingEnabled(growing.Editor), "Growing buffers must also use the large-file scroll policy.");

        string koreanPath = Path.Combine(directory, "legacy.log");
        await File.WriteAllTextAsync(koreanPath, string.Concat(Enumerable.Repeat("한글\n", LargeFilePolicy.ThresholdLines)), TextFileEncoding.Korean949.GetEncoding());
        growing.Load(await EditorFileLoader.ReadAsync(koreanPath, TextFileEncoding.Korean949));
        growing.ConvertToUtf8();
        Check(growing.IsModified && growing.CaptureSession()().Encoding == TextFileEncoding.Korean949,
            "Discarding an unsaved encoding conversion must reopen the large source in its original encoding.");
        string smallPath = Path.Combine(directory, "small.txt");
        await File.WriteAllTextAsync(smallPath, "https://example.com user@example.com");
        growing.Reload(await EditorFileLoader.ReadAsync(smallPath));
        Check(!growing.IsLargeFile && growing.Editor.Options.EnableHyperlinks && growing.Editor.Options.EnableEmailHyperlinks &&
            !ScrollViewer.GetIsDeferredScrollingEnabled(growing.Editor), "Reloading a small buffer must restore ordinary display and scrolling.");
        await LargeFileScrollChecks.NormalDocument_UsesOriginalScrollingAsync(preferences);
        Directory.Delete(directory, true);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
