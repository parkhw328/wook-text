using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WookText.App;

namespace WookText.App.SmokeTests;

internal static class SearchLinkedEditingChecks
{
    internal static async Task RunAsync(MainWindow window, string output)
    {
        var editor = window.ActiveDocument!.Editor;
        string original = editor.Text;
        editor.Text = "cat Cat cat\r\nuntouched";
        editor.Select(0, 0);
        ApplicationCommands.Find.Execute(null, editor.TextArea);
        await Program.WaitForIdleAsync(window);
        TextBox find = (TextBox)window.FindName("FindBox");
        CheckBox linked = (CheckBox)window.FindName("EditAllMatches");
        CheckBox matchCase = (CheckBox)window.FindName("MatchCase");
        find.Text = "cat";
        matchCase.IsChecked = false;
        linked.IsChecked = true;
        Check(editor.SelectedText == "cat" && editor.IsKeyboardFocusWithin, "Enabling linked editing must select a match and focus the editor.");
        editor.TextArea.PerformTextInput("d");
        editor.TextArea.PerformTextInput("og");
        Check(editor.Text == "dog dog dog\r\nuntouched", "Sequential typing must keep all original targets after the query disappears.");
        SearchMatchColorizer highlight = editor.TextArea.TextView.LineTransformers.OfType<SearchMatchColorizer>().Single();
        Check(highlight.Offsets.SequenceEqual(new[] { 0, 4, 8 }) && highlight.MatchLength == 3, "Highlights must follow the edited targets.");
        editor.Undo();
        Check(editor.Text == "d d d\r\nuntouched" && linked.IsChecked == false, "One undo must restore the last edit across all targets and end linked editing.");
        editor.Redo();
        Check(editor.Text == "dog dog dog\r\nuntouched", "Redo must not mirror the edit twice.");
        find.Text = "dog";
        linked.IsChecked = true;
        Clipboard.SetText("한글\r\n🙂");
        ApplicationCommands.Paste.Execute(null, editor.TextArea);
        Check(editor.Text == "한글\r\n🙂 한글\r\n🙂 한글\r\n🙂\r\nuntouched", "Pasting Unicode and multiple lines must replace every target.");
        System.Windows.Documents.EditingCommands.Backspace.Execute(null, editor.TextArea);
        Check(!editor.Text.Contains("🙂") && linked.IsChecked == true, "Backspace must remove a whole emoji from all targets.");
        editor.Select(0, 2);
        ApplicationCommands.Cut.Execute(null, editor.TextArea);
        Check(editor.Text == "\r\n \r\n \r\n\r\nuntouched" && Clipboard.GetText() == "한글", "Cut must copy the primary selection and remove it from all targets.");
        linked.IsChecked = false;
        editor.Text = "aa";
        find.Text = "a";
        editor.Select(0, 0);
        linked.IsChecked = true;
        System.Windows.Documents.EditingCommands.Delete.Execute(null, editor.TextArea);
        Check(editor.Text == "" && highlight.Offsets.Count == 2 && highlight.MatchLength == 0, "Deleting adjacent matches must retain both insertion targets.");
        editor.TextArea.PerformTextInput("b");
        Check(editor.Text == "bb", "Typing after deleting all targets must populate each target exactly once.");
        find.Text = "b";
        Check(linked.IsChecked == false, "Changing the query must end linked editing.");
        linked.IsChecked = true;
        matchCase.IsChecked = true;
        Check(linked.IsChecked == false, "Changing case matching must end linked editing.");
        linked.IsChecked = true;
        EditorDocument first = window.ActiveDocument!;
        EditorDocument second = window.NewDocument();
        Check(linked.IsChecked == false, "Switching documents must end linked editing.");
        ((TabControl)window.FindName("DocumentTabs")).SelectedItem = first;
        window.Documents.Remove(second);
        second.Dispose();
        find.Text = "absent";
        linked.IsChecked = true;
        Check(linked.IsChecked == false, "No matches must not activate linked editing.");
        editor.Text = "name = name;\r\nprint(name);";
        editor.Select(0, 0);
        find.Text = "name";
        linked.IsChecked = true;
        await Program.RenderAsync(window, System.IO.Path.Combine(output, "search-linked-editing.png"), 1240, 800);
        linked.IsChecked = false;
        editor.Select(0, 4);
        editor.TextArea.PerformTextInput("value");
        Check(editor.Text == "value = name;\r\nprint(name);", "Disabling linked editing must restore ordinary single-location edits.");
        editor.Text = original;
        matchCase.IsChecked = false;
        ((Border)window.FindName("SearchPanel")).Visibility = Visibility.Collapsed;
        find.Clear();
        Console.WriteLine("PASS: linked search editing, sequential typing, clipboard, Unicode deletion, adjacent targets, undo/redo and mode lifecycle.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
