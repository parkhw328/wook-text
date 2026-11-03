using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class LargeFileScrollChecks
{
    internal static async Task RunAsync(MainWindow window, EditorDocument document)
    {
        TextEditor editor = document.Editor;
        var view = editor.TextArea.TextView;
        IScrollInfo scroll = view;
        await SettleAsync();
        Check(!editor.Options.EnableHyperlinks && !editor.Options.EnableEmailHyperlinks,
            "Large buffers must not scan visible lines for URLs or email addresses.");
        var viewer = (ScrollViewer)editor.Template.FindName("PART_ScrollViewer", editor);
        Check(viewer.IsDeferredScrollingEnabled, "The actual template ScrollViewer must defer thumb dragging.");

        const string edit = "SCROLL-UNDO\n";
        int originalLength = editor.Document.TextLength;
        string originalStart = editor.Document.GetText(0, edit.Length);
        editor.Document.Insert(0, edit);
        editor.Select(10, 3);
        var version = editor.Document.Version;
        int caret = editor.CaretOffset;
        scroll.SetVerticalOffset(5000);
        await SettleAsync();
        double start = view.VerticalOffset;
        double unit = SystemParameters.WheelScrollLines < 0 ? scroll.ViewportHeight : SystemParameters.WheelScrollLines * view.DefaultLineHeight;
        int offsets = 0;
        int layouts = 0;
        void OnOffset(object? sender, EventArgs e) => offsets++;
        void OnLayout(object? sender, EventArgs e) => layouts++;
        view.ScrollOffsetChanged += OnOffset;
        view.VisualLinesChanged += OnLayout;
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            for (int i = 0; i < 80; i++) Wheel(editor, -30);
            Check(view.VerticalOffset == start && offsets == 0, "A burst must queue one target without intermediate layout.");
            await SettleAsync();
            double expected = Math.Min(start + 20 * unit, scroll.ExtentHeight - scroll.ViewportHeight);
            Check(Math.Abs(view.VerticalOffset - expected) < 1 && offsets <= 1,
                $"Fractional wheel input must retain its distance in one move: expected {expected}, actual {view.VerticalOffset}, moves {offsets}.");
        }
        finally
        {
            view.ScrollOffsetChanged -= OnOffset;
            view.VisualLinesChanged -= OnLayout;
        }
        watch.Stop();
        Console.WriteLine($"PASS: 100 MiB wheel burst, 80 inputs -> {offsets} offset change(s), {layouts} visual-line layouts; settled in {watch.Elapsed.TotalMilliseconds:0} ms (includes test wait).");

        // A keyboard/programmatic jump must win over a wheel timer that has not fired yet.
        Wheel(editor, -120);
        scroll.SetVerticalOffset(2000);
        await SettleAsync();
        Check(Math.Abs(view.VerticalOffset - 2000) < 1, "Pending wheel input must not undo a navigation jump.");
        Wheel(editor, -120);
        editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.LeftCtrl)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        await SettleAsync();
        Check(Math.Abs(view.VerticalOffset - 2000) < 1, "Starting Ctrl+wheel zoom must cancel pending scrolling.");

        Wheel(editor, -120);
        editor.Visibility = Visibility.Hidden;
        await SettleAsync();
        editor.Visibility = Visibility.Visible;
        await SettleAsync();
        Check(Math.Abs(view.VerticalOffset - 2000) < 1, "A hidden tab must not apply stale wheel movement.");

        double beforeDrag = view.VerticalOffset;
        Check(ScrollBar.DeferScrollToVerticalOffsetCommand.CanExecute(50000d, viewer), "Deferred thumb commands must be accepted.");
        foreach (double target in new[] { 10000d, 30000d, 50000d })
            ScrollBar.DeferScrollToVerticalOffsetCommand.Execute(target, viewer);
        await SettleAsync();
        Check(Math.Abs(view.VerticalOffset - beforeDrag) < 1, "Dragging the thumb must not repeatedly format intermediate rows.");
        ScrollBar.ScrollToVerticalOffsetCommand.Execute(50000d, viewer);
        await SettleAsync();
        Check(Math.Abs(view.VerticalOffset - 50000) < 1, "Releasing the thumb must reach its final position.");

        scroll.SetVerticalOffset(Math.Max(0, scroll.ExtentHeight - scroll.ViewportHeight));
        await SettleAsync();
        double bottom = view.VerticalOffset;
        for (int i = 0; i < 20; i++) Wheel(editor, -120);
        Wheel(editor, 120);
        await SettleAsync();
        Check(Math.Abs(view.VerticalOffset - Math.Max(0, bottom - unit)) < 1, "Reversing at the end must immediately move up.");
        Check(editor.Document.Version.CompareAge(version) == 0 && editor.SelectionStart == 10 && editor.SelectionLength == 3 &&
            editor.CaretOffset == caret && editor.Document.UndoStack.CanUndo,
            "Scrolling must preserve text, selection, caret and undo history.");
        editor.Undo();
        Check(editor.Document.TextLength == originalLength && editor.Document.GetText(0, edit.Length) == originalStart,
            "Undo after scrolling must still undo the edit made before scrolling.");
        editor.Redo();
        Check(editor.Document.GetText(0, edit.Length) == edit, "Redo after scrolling must restore the original edit.");
        editor.Undo();
        scroll.SetVerticalOffset(0);
        await SettleAsync();
    }

    internal static async Task NormalDocument_UsesOriginalScrollingAsync(EditorPreferencesService preferences)
    {
        using EditorDocument document = new("ordinary.txt", preferences);
        Window host = new() { Content = document.View, Width = 800, Height = 600 };
        Program.ShowOffscreen(host);
        try
        {
            document.Editor.Text = string.Concat(Enumerable.Repeat("https://example.com user@example.com 한글\n", 1000));
            await SettleAsync();
            Check(!document.IsLargeFile && document.Editor.Options.EnableHyperlinks && document.Editor.Options.EnableEmailHyperlinks,
                "Normal documents must retain automatic links.");
            var viewer = (ScrollViewer)document.Editor.Template.FindName("PART_ScrollViewer", document.Editor);
            Check(!viewer.IsDeferredScrollingEnabled, "Normal documents must retain live thumb scrolling.");
            MouseWheelEventArgs wheel = Wheel(document.Editor, -120, expectHandled: false);
            Check(!wheel.Handled, "Normal wheel input must reach AvalonEdit's original handler.");
        }
        finally { host.Close(); }
    }

    private static MouseWheelEventArgs Wheel(TextEditor editor, int delta, bool expectHandled = true)
    {
        MouseWheelEventArgs e = new(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
        editor.RaiseEvent(e);
        if (expectHandled) Check(e.Handled, "Large-file wheel input must be batched.");
        return e;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(80);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
