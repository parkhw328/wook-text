using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class NavigationEditingChecks
{
    public static async Task RunAsync(MainWindow window, string output)
    {
        EditorDocument document = window.NewDocument();
        TextEditor editor = document.Editor;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        foreach (string newline in new[] { "\n", "\r\n", "\r" })
        {
            Reset("alpha" + newline + "한글" + newline + "last");
            editor.CaretOffset = editor.Document.GetLineByNumber(2).Offset + 1;
            string original = editor.Text;
            EditorEditing.Duplicate(editor);
            Check(editor.Text == "alpha" + newline + "한글" + newline + "한글" + newline + "last", "Duplicate must preserve Unicode and newline style.");
            Undo(original);
            editor.Select(editor.Document.GetLineByNumber(2).Offset, 0);
            EditorEditing.MoveLines(editor, 1);
            Check(editor.Text == "alpha" + newline + "last" + newline + "한글", "Moving to EOF must retain separators and the missing final newline.");
            Undo(original);
            editor.Select(editor.Document.GetLineByNumber(2).Offset, 0);
            EditorEditing.MoveLines(editor, -1);
            Check(editor.Text == "한글" + newline + "alpha" + newline + "last", "Move up must swap complete lines.");
            Undo(original);
            editor.Select(editor.Document.GetLineByNumber(3).Offset, 0);
            EditorEditing.DeleteLines(editor);
            Check(editor.Text == "alpha" + newline + "한글", "Deleting the last line must remove the preceding separator.");
            Undo(original);
        }
        Reset("one\ntwo\nthree\nfour");
        editor.Select(4, 10); // Ends exactly at the fourth line: it must remain outside the block.
        EditorEditing.MoveLines(editor, -1);
        Check(editor.Text == "two\nthree\none\nfour", "Selection ending at a line start must exclude that line.");
        Undo("one\ntwo\nthree\nfour");
        Reset("abc");
        editor.Select(1, 1);
        EditorEditing.Duplicate(editor);
        Check(editor.Text == "abbc" && editor.SelectedText == "b", "Selected text must be duplicated and selected.");
        Undo("abc");
        editor.Select(3, 0);
        EditorEditing.Duplicate(editor);
        Check(editor.Text == "abc" + Environment.NewLine + "abc", "A final unterminated line must be duplicated as another line.");
        Undo("abc");
        Reset("abcd\r\nx\r\n");
        editor.CaretOffset = 3;
        EditorEditing.PasteBlock(editor, "가\r\n나\r다\n라");
        Check(editor.Text == "abc가d\r\nx  나\r\n   다\r\n   라", "Block paste must pad short rows, normalize clipboard separators, and extend EOF.");
        Undo("abcd\r\nx\r\n");
        editor.Redo();
        Check(editor.Text.Contains("   라"), "Block paste must redo as a single edit.");
        Reset("aa11\nbb22\ncc33");
        editor.Select(2, 12);
        KeyBinding blockShortcut = editor.TextArea.InputBindings.OfType<KeyBinding>().Single(binding =>
            binding.Key == Key.B && binding.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt));
        Check(blockShortcut.Command == EditorCommands.SelectBlock, "Ctrl+Alt+B must convert the selection into a block.");
        ((RoutedCommand)blockShortcut.Command).Execute(null, editor.TextArea);
        Check(editor.TextArea.Selection is RectangleSelection && editor.Text == "aa11\nbb22\ncc33" && !editor.CanUndo,
            "Block conversion must preserve text and undo history.");
        EditorEditing.CopyBlock(editor);
        Check(Clipboard.GetText().Replace("\r\n", "\n") == "11\n22\n33" && Clipboard.ContainsData(RectangleSelection.RectangularSelectionDataType), "Copy must preserve rectangle metadata.");
        Reset("----\n----\n----");
        editor.CaretOffset = 2;
        ApplicationCommands.Paste.Execute(null, editor.TextArea);
        Check(editor.Text == "--11--\n--22--\n--33--", "Ctrl+V routing must keep copied blocks rectangular.");
        Undo("----\n----\n----");
        Reset("aa11\nbb22\ncc33");
        editor.TextArea.Selection = new RectangleSelection(editor.TextArea, new TextViewPosition(1, 3), new TextViewPosition(3, 5));
        EditorEditing.PasteBlock(editor, "X");
        Check(editor.Text == "aaX\nbbX\nccX", "Single-row block paste must replace every selected row.");
        Undo("aa11\nbb22\ncc33");
        editor.TextArea.Selection = new RectangleSelection(editor.TextArea, new TextViewPosition(1, 3), new TextViewPosition(3, 5));
        bool rejected = false;
        try { EditorEditing.PasteBlock(editor, "A\nB"); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && editor.Text == "aa11\nbb22\ncc33", "Mismatched block heights must not partially edit the buffer.");
        Reset("\nxx");
        editor.TextArea.Selection = new RectangleSelection(editor.TextArea, new TextViewPosition(1, 1), new TextViewPosition(2, 3));
        EditorEditing.CopyBlock(editor);
        Check(Clipboard.GetText().Replace("\r\n", "\n") == "\nxx", "Empty leading rows must survive block copy.");

        Reset(string.Join("\n", Enumerable.Range(1, 1200).Select(i => $"// {i:0000}   문서 탐색 · minimap sample")));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(document.Minimap.IsVisible && document.Minimap.ActualWidth > 0, "Minimap must be visible by default.");
        int caret = editor.CaretOffset;
        document.Minimap.NavigateTo(0.75);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(editor.VerticalOffset > 0 && editor.CaretOffset == caret, "Minimap navigation must scroll without moving the caret.");
        await Program.RenderAsync(window, Path.Combine(output, "minimap.png"), 1200, 800);
        var menu = (MenuItem)window.FindName("MinimapMenu");
        menu.IsChecked = false;
        menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(document.Minimap.Visibility == Visibility.Collapsed && window.Documents.All(d => d.Minimap.Visibility == Visibility.Collapsed), "Visibility must update every tab.");
        menu.IsChecked = true;
        menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(document.Minimap.Visibility == Visibility.Visible, "Hidden minimaps must be easy to restore.");
        PreferencesWindow settings = new(new EditorPreferences { ShowMinimap = false });
        Check(((CheckBox)settings.FindName("MinimapBox")).IsChecked == false && settings.TryGetPreferences(out var saved) && !saved.ShowMinimap, "Settings must read and write minimap visibility.");
        settings.Close();

        void Reset(string text)
        {
            editor.TextArea.ClearSelection();
            editor.Text = text;
            editor.Select(0, 0);
            editor.Document.UndoStack.ClearAll();
            editor.UpdateLayout();
        }
        void Undo(string expected)
        {
            editor.Undo();
            Check(editor.Text == expected && !editor.CanUndo, "One undo must restore the complete original edit.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
