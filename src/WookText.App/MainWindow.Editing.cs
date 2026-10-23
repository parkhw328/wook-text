using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;

namespace WookText.App;

public partial class MainWindow
{
    private void BindEditingCommands()
    {
        BindEdit(EditorCommands.SelectBlock, EditorEditing.SelectBlock);
        BindEdit(EditorCommands.CopyBlock, EditorEditing.CopyBlock);
        BindEdit(EditorCommands.PasteBlock, editor => { if (Clipboard.ContainsText()) EditorEditing.PasteBlock(editor, Clipboard.GetText()); });
        BindEdit(EditorCommands.Duplicate, EditorEditing.Duplicate, true);
        BindEdit(EditorCommands.MoveLinesUp, editor => EditorEditing.MoveLines(editor, -1), true);
        BindEdit(EditorCommands.MoveLinesDown, editor => EditorEditing.MoveLines(editor, 1), true);
        BindEdit(EditorCommands.DeleteLines, EditorEditing.DeleteLines, true);
        BindEdit(EditorCommands.SelectLines, EditorEditing.SelectLines, true);
    }

    private void BindEdit(ICommand command, Action<TextEditor> action, bool linearOnly = false) => CommandBindings.Add(new CommandBinding(command,
        (_, e) =>
        {
            e.Handled = true;
            if (ActiveDocument?.Editor is not { } editor) return;
            EndLinkedEditing();
            try { action(editor); }
            catch (Exception ex) when (ex is ExternalException or InvalidOperationException) { Status.Text = ex.Message; }
            editor.TextArea.Focus();
        },
        (_, e) =>
        {
            e.CanExecute = !_busy && ActiveDocument?.Editor is { IsReadOnly: false } editor &&
                (!linearOnly || editor.TextArea.Selection is not RectangleSelection);
            e.Handled = true;
        }));

    private void InstallEditingShortcuts(TextEditor editor)
    {
        (Key Key, ModifierKeys Modifiers, ICommand Command)[] shortcuts =
        [
            (Key.C, ModifierKeys.Control | ModifierKeys.Shift, EditorCommands.CopyBlock),
            (Key.V, ModifierKeys.Control | ModifierKeys.Shift, EditorCommands.PasteBlock),
            (Key.D, ModifierKeys.Control | ModifierKeys.Alt, EditorCommands.Duplicate),
            (Key.Up, ModifierKeys.Control | ModifierKeys.Shift, EditorCommands.MoveLinesUp),
            (Key.Down, ModifierKeys.Control | ModifierKeys.Shift, EditorCommands.MoveLinesDown),
            (Key.K, ModifierKeys.Control | ModifierKeys.Shift, EditorCommands.DeleteLines),
            (Key.L, ModifierKeys.Control, EditorCommands.SelectLines)
        ];
        foreach (var shortcut in shortcuts) editor.TextArea.InputBindings.Add(new KeyBinding(shortcut.Command, shortcut.Key, shortcut.Modifiers));
        CommandManager.AddPreviewExecutedHandler(editor, (_, e) =>
        {
            if (_linkedEditor is not null) return;
            try
            {
                if ((e.Command == ApplicationCommands.Copy || e.Command == ApplicationCommands.Cut) && editor.TextArea.Selection is RectangleSelection)
                {
                    e.Handled = true;
                    EditorEditing.CopyBlock(editor);
                    if (e.Command == ApplicationCommands.Cut && !editor.IsReadOnly) editor.TextArea.Selection.ReplaceSelectionWithText("");
                }
                else if (e.Command == ApplicationCommands.Paste && (editor.TextArea.Selection is RectangleSelection || Clipboard.ContainsData(RectangleSelection.RectangularSelectionDataType)))
                {
                    e.Handled = true;
                    EditorEditing.PasteBlock(editor, Clipboard.GetText());
                }
            }
            catch (Exception ex) when (ex is ExternalException or InvalidOperationException) { e.Handled = true; Status.Text = ex.Message; }
        });
    }

    private void OnCut(object sender, RoutedEventArgs e) => ApplicationCommands.Cut.Execute(null, ActiveDocument?.Editor.TextArea);
    private void OnCopy(object sender, RoutedEventArgs e) => ApplicationCommands.Copy.Execute(null, ActiveDocument?.Editor.TextArea);
    private void OnPaste(object sender, RoutedEventArgs e) => ApplicationCommands.Paste.Execute(null, ActiveDocument?.Editor.TextArea);
}
