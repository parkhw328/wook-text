using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class ComparisonChecks
{
    internal static async Task RunAsync(EditorPreferencesService preferences, string output)
    {
        string leftPath = Path.Combine(output, "fixtures", "scroll-left.txt");
        string rightPath = Path.Combine(output, "fixtures", "scroll-right.txt");
        string[] lines = Enumerable.Range(1, 600).Select(i => $"{i:D4}: 한글 scroll test").ToArray();
        await File.WriteAllLinesAsync(leftPath, lines.Select(line => line + new string('x', 180)));
        await File.WriteAllLinesAsync(rightPath, lines);
        CompareWindow window = new(preferences);
        Program.ShowOffscreen(window);
        try
        {
            await Inputs_PasteEditAndSwap_CompareWithoutFilesAsync(window, output);
            await window.CompareFilesAsync(leftPath, rightPath);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Scroll_BothPanes_KeepLatestPositionAsync(window, output);
            await Files_DropsAndFailures_PreserveInputsAsync(window, leftPath, rightPath, output);
        }
        finally { window.Close(); }
        await Drop_EditorSurface_OpensFileWithoutChangingBufferAsync(preferences, leftPath);
        Console.WriteLine("PASS: comparison paste/edit/undo, empty and mixed inputs, swaps, limits, file-drop routing, atomic failures, and stable bidirectional scrolling.");
    }

    private static async Task Scroll_BothPanes_KeepLatestPositionAsync(CompareWindow window, string output)
    {
        TextEditor left = Editor(window, "LeftHost");
        TextEditor right = Editor(window, "RightHost");
        foreach (TextEditor source in new[] { left, right })
        {
            TextEditor target = source == left ? right : left;
            IScrollInfo scroll = source.TextArea.TextView;
            double before = source.TextArea.TextView.VerticalOffset;
            for (int i = 0; i < 5; i++) scroll.MouseWheelDown();
            double requested = source.TextArea.TextView.VerticalOffset;
            Check(requested > before, "Mouse wheel must move down before layout.");
            await SettleAsync();
            window.UpdateLayout();
            Check(Math.Abs(source.VerticalOffset - requested) < 1,
                $"Scrolling must not bounce back after layout: requested {requested}, actual {source.VerticalOffset}.");
            Check(Math.Abs(target.VerticalOffset - source.VerticalOffset) < 1,
                $"Both panes must remain vertically aligned: source {source.VerticalOffset}, target {target.VerticalOffset}, live {source.TextArea.TextView.VerticalOffset}/{target.TextArea.TextView.VerticalOffset}, heights {source.ExtentHeight}/{target.ExtentHeight}, viewports {source.ViewportHeight}/{target.ViewportHeight}.");

            // ScrollViewer commands reproduce scrollbar/keyboard navigation, which
            // is deferred, unlike the direct wheel path above.
            foreach (double offset in new[] { 2400d, 200d, 700d })
            {
                source.ScrollToVerticalOffset(offset);
                await SettleAsync();
                Check(Math.Abs(source.VerticalOffset - offset) < 1 && Math.Abs(target.VerticalOffset - offset) < 1,
                    $"A scrollbar jump must settle at {offset}, not oscillate ({source.VerticalOffset}/{target.VerticalOffset}).");
            }
            source.ScrollToEnd();
            await SettleAsync();
            Check(Math.Abs(source.VerticalOffset - target.VerticalOffset) < 1, "Both panes must align at the last row even when only one has long lines.");
            scroll.MouseWheelUp();
            double aboveBottom = source.TextArea.TextView.VerticalOffset;
            await SettleAsync();
            Check(Math.Abs(source.VerticalOffset - aboveBottom) < 1, "Scrolling up from the bottom must not snap back.");
            source.ScrollToHome();
            await SettleAsync();
        }
        left.ScrollToHorizontalOffset(500);
        await SettleAsync();
        Check(Math.Abs(left.HorizontalOffset - 500) < 1 && right.HorizontalOffset == 0,
            "A short target must not pull the long source back horizontally.");
        left.ScrollToVerticalOffset(1000);
        await SettleAsync();
        Check(Math.Abs(left.HorizontalOffset - 500) < 1 && Math.Abs(right.VerticalOffset - 1000) < 1,
            "Vertical scrolling must preserve the long pane's horizontal position.");
        await Program.RenderAsync(window, Path.Combine(output, "comparison-scroll.png"), 1240, 780);
        window.Width = 940;
        window.Height = 560;
        await SettleAsync();
        right.ScrollToVerticalOffset(400);
        await SettleAsync();
        Check(Math.Abs(left.VerticalOffset - right.VerticalOffset) < 1 && Math.Abs(left.HorizontalOffset - 500) < 1,
            "Resizing and scrolling the short pane vertically must retain alignment without resetting the long pane's horizontal offset.");
    }

    private static async Task Inputs_PasteEditAndSwap_CompareWithoutFilesAsync(CompareWindow window, string output)
    {
        TextEditor left = Editor(window, "LeftInputHost");
        TextEditor right = Editor(window, "RightInputHost");
        Check(!left.IsReadOnly && !right.IsReadOnly && ((TabItem)window.FindName("InputTab")).IsSelected,
            "A new comparison must immediately offer editable text inputs.");
        await Program.RenderAsync(window, Path.Combine(output, "comparison-empty.png"), 940, 560);
        await window.CompareTextAsync();
        Check(window.Result is { ExactMatch: true }, "Two empty inputs must be a valid comparison.");
        Check(!((Button)window.FindName("NextButton")).IsEnabled, "Identical inputs must disable change navigation.");

        const string original = "# 배포 메모\r\n버전: 1.1.0\r\n스크롤 동기화\r\n파일 비교\r\n";
        const string modified = "# 배포 메모\r\n버전: 1.1.0\r\n스크롤 동기화 수정\r\n파일 · 텍스트 비교\r\n파일 드래그 앤 드롭\r\n";
        IDataObject? clipboard = Clipboard.GetDataObject();
        string? lastClipboardText = null;
        try
        {
            Clipboard.SetText(original);
            lastClipboardText = original;
            Click(window, "LeftPaste");
            Check(left.Text == original, "The paste button must preserve Korean text and CRLF without diff prefixes.");
            Clipboard.SetText(modified);
            lastClipboardText = modified;
            ((TabItem)window.FindName("InputTab")).IsSelected = true;
            right.TextArea.Focus();
            ApplicationCommands.Paste.Execute(null, right.TextArea);
            Check(right.Text.Replace("\r\n", "\n") == modified.Replace("\r\n", "\n"), "The editor's standard paste command must accept clipboard text.");
            Click(window, "RightPaste");
            Check(right.Text == modified, "The right paste button must replace the entire input.");
        }
        finally
        {
            // Do not overwrite a clipboard that the user changed during the check.
            if (lastClipboardText is not null && Clipboard.ContainsText() && Clipboard.GetText() == lastClipboardText)
            {
                if (clipboard is null) Clipboard.Clear();
                else Clipboard.SetDataObject(clipboard, true);
            }
        }
        await Program.RenderAsync(window, Path.Combine(output, "comparison-input.png"), 1240, 780);
        Click(window, "CompareButton");
        await WaitUntilAsync(() => ((Button)window.FindName("CompareButton")).IsEnabled);
        Check(window.Result is { Modified: 2, Added: 1 }, "Pasted buffers must produce the expected diff without files.");
        Check(((TabItem)window.FindName("ResultTab")).IsSelected, "Compare must open the aligned result.");
        await Program.RenderAsync(window, Path.Combine(output, "comparison-text.png"), 1240, 780);
        Click(window, "NextButton");
        Check(Editor(window, "LeftHost").TextArea.Caret.Line == 3, "Next must navigate to the first changed block.");
        Click(window, "PreviousButton");
        await SettleAsync();

        ((TabItem)window.FindName("InputTab")).IsSelected = true;
        right.AppendText("한 줄 더\n");
        Check(window.Result is null && !((TabItem)window.FindName("ResultTab")).IsEnabled, "Editing must invalidate the old result.");
        right.Undo();
        Check(right.Text == modified, "Returning from results must retain input undo history.");
        Click(window, "RightClear");
        Check(right.Text == "", "Clear must empty the selected side.");
        right.Undo();
        Check(right.Text == modified, "Clearing must be undoable.");
        Click(window, "SwapButton");
        await WaitUntilAsync(() => ((Button)window.FindName("SwapButton")).IsEnabled);
        Check(left.Text == modified && right.Text == original && window.Result is { Deleted: 1, Modified: 2 }, "Swap must exchange text inputs and diff direction.");

        left.Text = "  한글  \n";
        right.Text = "한글\n";
        CheckBox whitespace = (CheckBox)window.FindName("IgnoreWhitespace");
        whitespace.IsChecked = true;
        whitespace.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
        await WaitUntilAsync(() => whitespace.IsEnabled);
        Check(window.Result is { Modified: 0, ExactMatch: false }, "Whitespace option must work for typed buffers.");
        whitespace.IsChecked = false;
        left.Text = "same\nold\nsame\nbefore\nsame";
        right.Text = "same\nnew\nsame\nafter\nsame";
        await window.CompareTextAsync();
        foreach (int expected in new[] { 2, 4, 2 })
        {
            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.F7)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(Editor(window, "LeftHost").TextArea.Caret.Line == expected, "F7 must navigate separate change blocks and wrap.");
        }
        Click(window, "PreviousButton");
        Check(Editor(window, "LeftHost").TextArea.Caret.Line == 4, "Previous must wrap to the final change block.");
        left.Text = "";
        right.Text = "한글🙂";
        await window.CompareTextAsync();
        Check(window.Result is { Added: 1 }, "Empty versus nonempty text must be comparable.");
        left.Text = new string('x', TextDiffService.MaximumCharacters + 1);
        await window.CompareTextAsync();
        Check(window.Result is null && left.Document.TextLength == TextDiffService.MaximumCharacters + 1
            && ((TextBlock)window.FindName("Summary")).Text.Contains("500,000"), "Oversized pasted text must be retained with an actionable limit message.");
        left.Text = new string('\n', TextDiffService.MaximumLines);
        await window.CompareTextAsync();
        Check(window.Result is null && left.Text.Length == TextDiffService.MaximumLines, "The line limit must also preserve the input.");
        left.Text = "recovered";
        await window.CompareTextAsync();
        Check(window.Result is not null, "A valid edit must recover after a limit failure.");
    }

    private static async Task Files_DropsAndFailures_PreserveInputsAsync(CompareWindow window, string leftPath, string rightPath, string output)
    {
        TextEditor left = Editor(window, "LeftInputHost");
        TextEditor right = Editor(window, "RightInputHost");
        ((TabItem)window.FindName("InputTab")).IsSelected = true;
        left.Text = "왼쪽 메모";
        right.Text = "오른쪽 메모";
        DragEventArgs drop = RaiseDrag(right.TextArea, [rightPath], DragDrop.PreviewDropEvent);
        Check(drop.Handled && drop.Effects == DragDropEffects.Copy, "File drops must be handled before AvalonEdit and accepted as copies.");
        await WaitUntilAsync(() => ((Button)window.FindName("CompareButton")).IsEnabled);
        Check(left.Text == "왼쪽 메모" && right.Text == await File.ReadAllTextAsync(rightPath) && window.Result is not null,
            "One file on the right must compare against and preserve left-hand text.");
        drop = RaiseDrag((UIElement)window.FindName("LeftSource"), [leftPath], DragDrop.PreviewDropEvent);
        await WaitUntilAsync(() => ((Button)window.FindName("CompareButton")).IsEnabled);
        Check(left.Text == await File.ReadAllTextAsync(leftPath), "Dropping onto a header must load that side.");
        RaiseDrag(Editor(window, "RightHost").TextArea, [rightPath, leftPath], DragDrop.PreviewDropEvent);
        await WaitUntilAsync(() => ((Button)window.FindName("CompareButton")).IsEnabled);
        Check(left.Text == await File.ReadAllTextAsync(rightPath) && right.Text == await File.ReadAllTextAsync(leftPath),
            "Two files dropped on a result pane must fill left and right in the supplied order.");

        string beforeLeft = left.Text;
        string beforeRight = right.Text;
        ComparisonResult? beforeResult = window.Result;
        string invalid = Path.Combine(output, "fixtures", "binary.dat");
        await File.WriteAllBytesAsync(invalid, [0, 1, 2]);
        string tooLarge = Path.Combine(output, "fixtures", "oversize.txt");
        await File.WriteAllTextAsync(tooLarge, new string('x', TextDiffService.MaximumCharacters + 1));
        foreach (string[] paths in new[] { new[] { leftPath, invalid }, [rightPath, tooLarge], ["missing-comparison-file.txt"], [leftPath, rightPath, leftPath], [Path.GetDirectoryName(leftPath)!] })
        {
            bool failed = false;
            try { await window.LoadFilesAsync(paths); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException) { failed = true; }
            Check(failed && left.Text == beforeLeft && right.Text == beforeRight && ReferenceEquals(window.Result, beforeResult),
                "Invalid, oversized, missing, excess, and directory drops must leave both inputs and results intact.");
        }
        foreach (string[] paths in new[] { new[] { leftPath, rightPath, leftPath }, [Path.GetDirectoryName(leftPath)!] })
        {
            DragEventArgs over = RaiseDrag((UIElement)window.FindName("RightSource"), paths, DragDrop.PreviewDragOverEvent);
            Check(over.Handled && over.Effects == DragDropEffects.None, "Unsupported drops must display a rejecting cursor.");
        }
        DragEventArgs moveOnly = RaiseDrag((UIElement)window.FindName("LeftSource"), [leftPath], DragDrop.PreviewDropEvent, DragDropEffects.Move);
        Check(moveOnly.Handled && moveOnly.Effects == DragDropEffects.None && left.Text == beforeLeft, "Comparison drops must never move source files.");
        await DialogChecks.WithDialogAsync(() => RaiseDrag((UIElement)window.FindName("LeftSource"), [invalid], DragDrop.PreviewDropEvent),
            dialog => Click(dialog, "PrimaryButton"));
        Check(left.Text == beforeLeft && right.Text == beforeRight && ReferenceEquals(window.Result, beforeResult),
            "A failed drop through the real handler must explain the error and preserve the previous comparison.");
        string cp949 = Path.Combine(output, "fixtures", "comparison-cp949.txt");
        await File.WriteAllBytesAsync(cp949, TextFileEncoding.Korean949.GetEncoding().GetBytes("한글 비교"));
        Task? loading = null;
        await DialogChecks.WithDialogAsync(() => loading = window.LoadFilesAsync([rightPath, cp949]), dialog => Click(dialog, "CancelButton"));
        await loading!;
        Check(left.Text == beforeLeft && right.Text == beforeRight && ReferenceEquals(window.Result, beforeResult),
            "Cancelling the second file's encoding prompt must preserve both sides of the previous comparison.");
        right.AppendText("edited copy");
        Check(((TextBlock)window.FindName("RightPath")).Text.Contains("편집됨"), "Editing a loaded file must identify its modified comparison copy.");
        Check(await File.ReadAllTextAsync(leftPath) == beforeRight, "Comparison editing must never save over a source file.");
        await Program.RenderAsync(window, Path.Combine(output, "comparison-small.png"), 940, 560);
        foreach (string name in new[] { "CompareButton", "LeftBrowse", "RightPaste", "RightClear" })
        {
            FrameworkElement button = (FrameworkElement)window.FindName(name);
            Point position = button.TranslatePoint(new Point(), window);
            Check(position.X >= 0 && position.X + button.ActualWidth < window.ActualWidth,
                "Comparison actions must remain reachable at the minimum width.");
        }
        string utf8 = Path.Combine(output, "fixtures", "comparison-utf8.txt");
        await File.WriteAllTextAsync(utf8, "한글 비교");
        await DialogChecks.WithDialogAsync(() => loading = window.LoadFilesAsync([utf8, cp949]), dialog => Click(dialog, "PrimaryButton"));
        await loading!;
        Check(window.Result is { ExactMatch: true } && ((TextBlock)window.FindName("Summary")).Text.Contains("인코딩 차이"),
            "CP949 confirmation must preserve characters and explain file encoding differences.");
        right.AppendText(" 편집");
        await window.CompareTextAsync();
        Check(!((TextBlock)window.FindName("Summary")).Text.Contains("인코딩 차이"), "Edited text must not claim an on-disk encoding comparison.");
    }

    private static async Task Drop_EditorSurface_OpensFileWithoutChangingBufferAsync(EditorPreferencesService preferences, string path)
    {
        MainWindow window = new(preferences);
        Program.ShowOffscreen(window);
        await Program.WaitForIdleAsync(window);
        EditorDocument document = window.ActiveDocument!;
        document.Editor.Text = "기존 미저장 문서";
        DragEventArgs drop = RaiseDrag(document.Editor.TextArea, [path], DragDrop.PreviewDropEvent);
        await Program.WaitForIdleAsync(window);
        Check(drop.Handled && window.Documents.Any(d => d.FilePath == Path.GetFullPath(path)) && document.Editor.Text == "기존 미저장 문서",
            "Dropping a file onto the main editor must open a tab instead of inserting its path into the current document.");
        await Program.CloseWindowAsync(window);
    }

    private static DragEventArgs RaiseDrag(UIElement target, string[] paths, RoutedEvent routedEvent, DragDropEffects effects = DragDropEffects.Copy)
    {
        // WPF exposes no public constructor; exercise the real routed event with
        // a FileDrop data object without native pointer or Explorer automation.
        DragEventArgs args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [new DataObject(DataFormats.FileDrop, paths), DragDropKeyStates.None, effects, target, new Point()], null)!;
        args.RoutedEvent = routedEvent;
        target.RaiseEvent(args);
        return args;
    }

    private static void Click(Window window, string name) => ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Comparison operation did not complete.");
            await Task.Delay(20);
        }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static TextEditor Editor(CompareWindow window, string host) => (TextEditor)((ContentControl)window.FindName(host)).Content;

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
