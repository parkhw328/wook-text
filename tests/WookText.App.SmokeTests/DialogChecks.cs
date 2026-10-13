using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class DialogChecks
{
    internal static async Task RunAsync(string output)
    {
        AppDialogWindow preview = new(AppDialogs.SaveRequest("작업 노트.md", @"C:\Projects\wText\notes\작업 노트.md"));
        Program.ShowOffscreen(preview);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Program.RenderElement((FrameworkElement)preview.Content, Path.Combine(output, "save-confirmation.png"));
        Check(((Button)preview.FindName("PrimaryButton")).IsDefault && ((Button)preview.FindName("CancelButton")).IsCancel, "Confirm dialogs must retain Enter and Escape semantics.");
        preview.Close();
        Check(preview.Result == AppDialogResult.Cancel, "Closing a prompt through the window must mean cancel.");
        foreach (var (key, result) in new[] { (Key.Y, AppDialogResult.Primary), (Key.N, AppDialogResult.Secondary), (Key.C, AppDialogResult.Cancel) })
        {
            AppDialogWindow shortcut = new(AppDialogs.SaveRequest("단축키.txt", null));
            Program.ShowOffscreen(shortcut);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Press(shortcut, key);
            Check(!shortcut.IsVisible && shortcut.Result == result, $"Save shortcut {key} must close the prompt with {result}.");
        }
        AppDialogWindow alert = new(new("파일을 저장할 수 없습니다", "다른 프로그램에서 파일을 사용하고 있습니다.\n파일을 확인한 뒤 다시 시도해 주세요.",
            Details: string.Join("\n", Enumerable.Repeat("Access denied: " + new string('x', 180), 30)), Kind: AppDialogKind.Error));
        Program.ShowOffscreen(alert);
        ((Expander)alert.FindName("DetailsSection")).IsExpanded = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        foreach (Key key in new[] { Key.Y, Key.N, Key.C }) Press(alert, key);
        Check(alert.IsVisible && alert.Result == AppDialogResult.Cancel, "Save shortcuts must not dismiss an error dialog.");
        Button accept = (Button)alert.FindName("PrimaryButton");
        Check(accept.TranslatePoint(new Point(), alert).Y + accept.ActualHeight <= alert.ActualHeight, "Long error details must scroll without hiding the action button.");
        Program.RenderElement((FrameworkElement)alert.Content, Path.Combine(output, "alert-details.png"));
        accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(alert.Result == AppDialogResult.Primary, "Acknowledging alerts must complete the prompt.");

        string sandbox = Path.Combine(output, "fixtures", "dialogs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        string path = Path.Combine(sandbox, "한글 문서.txt");
        await File.WriteAllTextAsync(path, "original\n");
        EditorPreferencesService preferences = new(new PreferencesStore(Path.Combine(sandbox, "settings.json")));
        MainWindow window = new(preferences);
        Program.ShowOffscreen(window);
        await Program.WaitForIdleAsync(window);
        await window.OpenFileAsync(path);
        EditorDocument document = window.ActiveDocument!;
        document.Editor.AppendText("변경 내용\n");
        foreach (string action in new[] { "CancelButton", "Escape", "DismissButton", "C" })
        {
            await WithDialogAsync(() => EditorCommands.CloseTab.Execute(null, window), dialog =>
            {
                Check(dialog.Owner == window && ((TextBlock)dialog.FindName("DocumentName")).Text == "한글 문서.txt", "Save prompts must be owned by the editor and identify the document.");
                if (action == "Escape") Press(dialog, Key.Escape);
                else if (action == "C") Press(dialog, Key.C);
                else Click(dialog, action);
            });
            await Program.WaitForIdleAsync(window);
            Check(window.Documents.Contains(document) && document.Editor.IsModified, "Cancel, Escape, and X must preserve the dirty document.");
        }
        await WithDialogAsync(() => EditorCommands.CloseTab.Execute(null, window), d => Press(d, Key.Y));
        await Program.WaitForIdleAsync(window);
        Check(!window.Documents.Contains(document) && (await File.ReadAllTextAsync(path)).Contains("변경 내용", StringComparison.Ordinal), "Save from the real modal prompt must write the document before closing it.");
        string saved = await File.ReadAllTextAsync(path);
        await window.OpenFileAsync(path);
        document = window.ActiveDocument!; document.Editor.AppendText("discard this");
        await WithDialogAsync(() => EditorCommands.CloseTab.Execute(null, window), d => Press(d, Key.N));
        await Program.WaitForIdleAsync(window);
        Check(!window.Documents.Contains(document) && await File.ReadAllTextAsync(path) == saved, "Discard must close the tab without changing the original file.");
        Task? opening = null;
        await WithDialogAsync(() => opening = window.OpenFileAsync(Path.Combine(sandbox, "missing.txt")), d =>
        {
            Check(((Expander)d.FindName("DetailsSection")).Visibility == Visibility.Visible, "I/O failures must use the themed alert with optional details.");
            Click(d, "PrimaryButton");
        });
        await opening!;
        string cp949 = Path.Combine(sandbox, "cp949.txt");
        await new TextFileService().SaveAsync(cp949, "한글 인코딩", TextFileEncoding.Korean949, null);
        await WithDialogAsync(() => opening = window.OpenFileAsync(cp949), d => Click(d, "CancelButton"));
        await opening!;
        Check(window.Documents.All(d => d.FilePath != cp949), "Cancelling an encoding prompt must not open a misdecoded document.");
        await WithDialogAsync(() => opening = window.OpenFileAsync(cp949), d => Click(d, "PrimaryButton"));
        await opening!;
        Check(window.ActiveDocument?.Editor.Text == "한글 인코딩", "The encoding action must open the original CP949 content.");
        preferences.Update(preferences.Current with { RememberSession = false });
        await window.OpenFileAsync(path);
        window.ActiveDocument!.Editor.AppendText("saved on exit");
        await WithDialogAsync(window.Close, d => Press(d, Key.C));
        await window.ClosingCompletion;
        Check(window.IsVisible && window.ActiveDocument.Editor.IsModified, "C during window exit must keep the window and unsaved buffer.");
        await WithDialogAsync(window.Close, d => Press(d, Key.Y));
        await window.ClosingCompletion;
        Check(!window.IsVisible && (await File.ReadAllTextAsync(path)).Contains("saved on exit", StringComparison.Ordinal), "Y during window exit must save before closing.");
        saved = await File.ReadAllTextAsync(path);
        window = new(preferences);
        Program.ShowOffscreen(window);
        await Program.WaitForIdleAsync(window);
        await window.OpenFileAsync(path);
        window.ActiveDocument!.Editor.AppendText("discarded on exit");
        await WithDialogAsync(window.Close, d => Press(d, Key.N));
        await window.ClosingCompletion;
        Check(!window.IsVisible && await File.ReadAllTextAsync(path) == saved, "N during window exit must close without overwriting the file.");
        Console.WriteLine("PASS: themed save, discard, cancel, Escape, window-close, encoding prompts, error details, and bounded dialog layouts.");
    }

    internal static async Task WithDialogAsync(Action trigger, Action<AppDialogWindow> interact)
    {
        TaskCompletionSource completion = new();
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(20) };
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        timer.Tick += (_, _) =>
        {
            AppDialogWindow? dialog = Application.Current.Windows.OfType<AppDialogWindow>().FirstOrDefault(w => w.IsVisible);
            if (dialog is null)
            {
                if (DateTime.UtcNow > deadline) { timer.Stop(); completion.TrySetException(new TimeoutException("The expected dialog did not appear.")); }
                return;
            }
            timer.Stop();
            try { interact(dialog); completion.TrySetResult(); }
            catch (Exception ex) { dialog.Close(); completion.TrySetException(ex); }
        };
        timer.Start();
        try { trigger(); await completion.Task; }
        finally { timer.Stop(); }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }
    private static void Press(AppDialogWindow dialog, Key key) => dialog.RaiseEvent(
        new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dialog), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    private static void Click(AppDialogWindow dialog, string name) => ((Button)dialog.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
