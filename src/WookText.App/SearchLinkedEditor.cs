using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using WookText.Core;

namespace WookText.App;

/// <summary>Routes typing and clipboard edits to a fixed set of search matches.</summary>
public sealed class SearchLinkedEditor : IDisposable
{
    private readonly TextEditor _editor;
    private bool _applying;
    private bool _disposed;
    private int _activeIndex;
    public SearchEditSession Session { get; private set; }
    public event Action? Updated;
    public event Action? Ended;
    public event Action<string>? Warning;

    public SearchLinkedEditor(TextEditor editor, SearchEditSession session)
    {
        _editor = editor;
        Session = session;
        _activeIndex = session.FindTarget(editor.SelectionStart, editor.SelectionLength, -1);
        if (_activeIndex < 0) _activeIndex = 0;
        editor.Select(session.Offsets[_activeIndex], session.MatchLength);
        editor.TextArea.TextEntering += OnTextEntering;
        editor.PreviewKeyDown += OnKeyDown;
        CommandManager.AddPreviewExecutedHandler(editor.TextArea, OnCommand);
        editor.Document.Changing += OnDocumentChanging;
        editor.Focus();
    }

    public bool ReplaceSelection(string text)
    {
        if (_disposed) return false;
        int index = Session.FindTarget(_editor.SelectionStart, _editor.SelectionLength, _activeIndex);
        if (index < 0) { End(); return false; }
        Apply(index, _editor.SelectionStart - Session.Offsets[index], _editor.SelectionLength, text);
        return true;
    }

    private void Apply(int index, int relativeOffset, int removalLength, string text)
    {
        if (removalLength == 0 && text.Length == 0) return;
        SearchEditChange change = Session.Replace(relativeOffset, removalLength, text);
        _applying = true;
        try
        {
            using (_editor.Document.RunUpdate())
            {
                foreach (TextReplacement edit in change.Replacements)
                    _editor.Document.Replace(edit.Offset, edit.Length, edit.Text);
                Session = change.Session;
                _activeIndex = index;
                _editor.Select(Session.Offsets[index] + change.RelativeCaret, 0);
            }
        }
        finally { _applying = false; }
        Updated?.Invoke();
        _editor.ScrollToLine(_editor.TextArea.Caret.Line);
    }

    private void OnTextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text)) e.Handled = ReplaceSelection(e.Text);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Back or Key.Delete or Key.Return or Key.Tab)) return;
        if (Keyboard.Modifiers != ModifierKeys.None) { End(); return; }
        if (e.Key == Key.Return)
        {
            string text = _editor.Text;
            int newline = text.IndexOfAny(['\r', '\n']);
            string ending = newline < 0 ? "\r\n" : text[newline] == '\r' && newline + 1 < text.Length && text[newline + 1] == '\n'
                ? "\r\n" : text[newline].ToString();
            e.Handled = ReplaceSelection(ending);
        }
        else if (e.Key == Key.Tab)
            e.Handled = ReplaceSelection(_editor.Options.ConvertTabsToSpaces ? new string(' ', _editor.Options.IndentationSize) : "\t");
        else e.Handled = Delete(e.Key == Key.Back);
    }

    public bool Delete(bool backwards)
    {
        if (_disposed) return false;
        int index = Session.FindTarget(_editor.SelectionStart, _editor.SelectionLength, _activeIndex);
        if (index < 0) { End(); return false; }
        if (_editor.SelectionLength > 0) return ReplaceSelection("");
        string target = _editor.Document.GetText(Session.Offsets[index], Session.MatchLength);
        var range = SearchEditSession.DeletionRange(target, _editor.SelectionStart - Session.Offsets[index], backwards);
        Apply(index, range.Offset, range.Length, "");
        return true;
    }

    private void OnCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command == ApplicationCommands.Paste)
        {
            try
            {
                if (Clipboard.ContainsText()) e.Handled = ReplaceSelection(Clipboard.GetText());
            }
            catch (ExternalException) { e.Handled = true; Warning?.Invoke("클립보드를 읽을 수 없습니다. 다시 붙여넣어 주세요."); }
        }
        else if (e.Command == ApplicationCommands.Cut)
        {
            if (_editor.SelectionLength == 0) { e.Handled = true; return; }
            try
            {
                Clipboard.SetText(_editor.SelectedText);
                e.Handled = ReplaceSelection("");
            }
            catch (ExternalException) { e.Handled = true; Warning?.Invoke("클립보드에 복사하지 못해 내용을 지우지 않았습니다."); }
        }
        else if (e.Command == System.Windows.Documents.EditingCommands.Backspace) e.Handled = Delete(true);
        else if (e.Command == System.Windows.Documents.EditingCommands.Delete) e.Handled = Delete(false);
    }

    private void OnDocumentChanging(object? sender, DocumentChangeEventArgs e)
    {
        // Undo, redo, formatting and other native edits must never be mirrored a second time.
        if (!_applying) End();
    }

    private void End() { Dispose(); Ended?.Invoke(); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _editor.TextArea.TextEntering -= OnTextEntering;
        _editor.PreviewKeyDown -= OnKeyDown;
        CommandManager.RemovePreviewExecutedHandler(_editor.TextArea, OnCommand);
        _editor.Document.Changing -= OnDocumentChanging;
    }
}
