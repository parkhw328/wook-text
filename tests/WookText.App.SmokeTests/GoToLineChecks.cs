using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WookText.App;

namespace WookText.App.SmokeTests;

internal static class GoToLineChecks
{
    internal static async Task RunAsync(MainWindow window, string output)
    {
        EditorDocument document = window.NewDocument();
        var editor = document.Editor;
        await Program.WaitForIdleAsync(window);
        KeyBinding shortcut = window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == Key.G && b.Modifiers == ModifierKeys.Control);
        Check(shortcut.Command == EditorCommands.GoToLine, "Ctrl+G must invoke line navigation.");
        var menu = (MenuItem)window.FindName("GoToLineMenu");
        Check(menu.Command == shortcut.Command && menu.InputGestureText == "Ctrl+G", "The menu must expose the same command and shortcut.");

        foreach (string newline in new[] { "\r\n", "\n", "\r" })
        {
            editor.Text = "first" + newline + "한글\tvalue" + newline + "last" + newline;
            editor.Document.UndoStack.ClearAll();
            editor.Document.Insert(0, "EDIT ");
            var version = editor.Document.Version;
            editor.Select(1, 3);
            ApplicationCommands.Find.Execute(null, window);
            await Program.WaitForIdleAsync(window);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await WithDialogAsync(() => ((RoutedCommand)shortcut.Command).Execute(null, (IInputElement)window.FindName("FindBox")), dialog =>
            {
                TextBox box = (TextBox)dialog.FindName("PositionBox");
                Check(dialog.Owner == window && box.IsKeyboardFocusWithin && box.SelectionLength == box.Text.Length,
                    "Opening from search must focus and select the current line input.");
                Check(((Button)dialog.FindName("GoButton")).IsDefault && ((Button)dialog.FindName("CancelButton")).IsCancel,
                    "Enter and Escape must retain accept/cancel semantics.");
                foreach (string invalid in new[] { "0", "5", "2:99", "1:2:3", "9999999999999999999999" })
                {
                    box.Text = invalid;
                    Accept(dialog);
                    Check(dialog.IsVisible && ((TextBlock)dialog.FindName("ErrorMessage")).Text.Length > 0 &&
                        editor.SelectionStart == 1 && editor.SelectionLength == 3,
                        "Invalid input must keep the dialog, selection, and original caret intact.");
                }
                box.Text = "2:4";
                Accept(dialog);
            });
            await Program.WaitForIdleAsync(window);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(editor.TextArea.Caret.Line == 2 && editor.TextArea.Caret.Column == 4 && editor.SelectionLength == 0 &&
                Keyboard.FocusedElement == editor.TextArea, "Navigation must move to the requested column after a tab and return editor focus.");
            Check(editor.Document.Version.CompareAge(version) == 0 && editor.CanUndo && document.IsModified,
                "Moving must preserve the buffer version, undo history, and unsaved state.");
            editor.Undo();
            Check(!editor.CanUndo && editor.Document.GetText(0, 5) == "first", "Undo after moving must still undo the earlier edit.");

            await WithDialogAsync(() =>
            {
                ((MenuItem)ItemsControl.ItemsControlFromItemContainer(menu)).IsSubmenuOpen = true;
                ((IInvokeProvider)new MenuItemAutomationPeer(menu).GetPattern(PatternInterface.Invoke)).Invoke();
            }, dialog =>
            {
                ((TextBox)dialog.FindName("PositionBox")).Text = "4";
                if (newline == "\r\n") Program.RenderElement((FrameworkElement)dialog.Content, Path.Combine(output, "go-to-line.png"));
                Accept(dialog);
            });
            Check(editor.CaretOffset == editor.Document.TextLength, "A trailing empty line must be reachable through the menu.");
        }

        editor.Text = "";
        await NavigateAsync(window, "1:1");
        Check(editor.CaretOffset == 0 && editor.Document.TextLength == 0, "Empty documents must have a valid first position.");
        editor.Text = "한글\n" + new string('x', 4000) + "\nlast";
        editor.WordWrap = true;
        await NavigateAsync(window, "2:3000");
        Check(editor.TextArea.Caret.Line == 2 && editor.TextArea.Caret.Column == 3000,
            "Wrapped display rows must not replace logical line/column numbering.");
        editor.Select(1, 1);
        int caret = editor.CaretOffset;
        foreach (bool escape in new[] { false, true })
        {
            await WithDialogAsync(() => EditorCommands.GoToLine.Execute(null, window), dialog =>
            {
                ((TextBox)dialog.FindName("PositionBox")).Text = "3";
                if (escape)
                    dialog.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dialog), 0, Key.Escape)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                else dialog.Close();
            });
            Check(editor.CaretOffset == caret && editor.SelectionStart == 1 && editor.SelectionLength == 1,
                "Escape and the window close button must leave the original location and selection intact.");
        }
        Console.WriteLine("PASS: Ctrl+G/menu navigation, search focus, line/column validation, empty/CRLF/LF/CR/wrapped documents, cancel and undo preservation.");
    }

    internal static async Task NavigateAsync(MainWindow window, string input)
    {
        await WithDialogAsync(() => EditorCommands.GoToLine.Execute(null, window), dialog =>
        {
            ((TextBox)dialog.FindName("PositionBox")).Text = input;
            Accept(dialog);
        });
        await Program.WaitForIdleAsync(window);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static async Task WithDialogAsync(Action trigger, Action<GoToLineWindow> interact)
    {
        TaskCompletionSource completion = new();
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(20) };
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        timer.Tick += (_, _) =>
        {
            GoToLineWindow? dialog = Application.Current.Windows.OfType<GoToLineWindow>().FirstOrDefault(w => w.IsVisible);
            if (dialog is null)
            {
                if (DateTime.UtcNow > deadline) { timer.Stop(); completion.TrySetException(new TimeoutException("Line navigation dialog did not appear.")); }
                return;
            }
            timer.Stop();
            try
            {
                interact(dialog);
                Check(!dialog.IsVisible, "The test interaction must dismiss the dialog.");
                completion.TrySetResult();
            }
            catch (Exception ex) { dialog.Close(); completion.TrySetException(ex); }
        };
        timer.Start();
        try { trigger(); await completion.Task; }
        finally { timer.Stop(); }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static void Accept(GoToLineWindow dialog) => ((Button)dialog.FindName("GoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
