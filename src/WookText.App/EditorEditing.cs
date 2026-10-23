using System.IO;
using System.Windows;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;

namespace WookText.App;

public static class EditorEditing
{
    public static void SelectBlock(TextEditor editor)
    {
        if (editor.TextArea.Selection is RectangleSelection) return;
        var start = new TextViewPosition(editor.Document.GetLocation(editor.SelectionStart));
        var end = new TextViewPosition(editor.Document.GetLocation(editor.SelectionStart + editor.SelectionLength));
        editor.TextArea.Selection = new RectangleSelection(editor.TextArea, start, end);
    }

    public static void CopyBlock(TextEditor editor)
    {
        if (editor.TextArea.Selection.IsEmpty) throw new InvalidOperationException("복사할 블록을 먼저 선택하세요. Alt+드래그 또는 Alt+Shift+방향키로 선택할 수 있습니다.");
        SelectBlock(editor);
        // Keep empty rows: AvalonEdit's GetText omits leading empty segments.
        string text = string.Join(Environment.NewLine, editor.TextArea.Selection.Segments.Select(s => editor.Document.GetText(s)));
        DataObject data = new();
        data.SetText(text);
        data.SetData(RectangleSelection.RectangularSelectionDataType, new MemoryStream([1]));
        Clipboard.SetDataObject(data, true);
    }

    public static void PasteBlock(TextEditor editor, string text)
    {
        if (editor.IsReadOnly || text.Length == 0) return;
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        int rows = text.Count(c => c == '\n') + 1;
        var area = editor.TextArea;
        if (!area.Selection.IsEmpty && area.Selection is not RectangleSelection)
            throw new InvalidOperationException("블록 붙여넣기는 선택을 해제하거나 직사각형 블록을 선택한 뒤 사용하세요.");
        if (area.Selection is RectangleSelection rectangle && (!rectangle.IsEmpty || rectangle.Segments.Count() > 1))
        {
            if (rows != 1 && rows != rectangle.Segments.Count())
                throw new InvalidOperationException("선택한 블록과 붙여넣을 텍스트의 줄 수를 맞춰 주세요. 한 줄 텍스트는 모든 선택 줄에 반복됩니다.");
            rectangle.ReplaceSelectionWithText(text);
            return;
        }
        var start = area.Caret.Position;
        using (editor.Document.RunUpdate())
        {
            int missing = start.Line + rows - 1 - editor.Document.LineCount;
            if (missing > 0) editor.Document.Insert(editor.Document.TextLength, string.Concat(Enumerable.Repeat(Newline(editor), missing)));
            // A zero-width rectangle enables padding on shorter target lines without a global virtual-space mode.
            area.Selection = new RectangleSelection(area, start, start);
            if (!RectangleSelection.PerformRectangularPaste(area, start, text, false))
                throw new InvalidOperationException("현재 위치에 블록을 붙여넣을 수 없습니다.");
        }
        area.Caret.BringCaretToView();
    }

    public static void Duplicate(TextEditor editor)
    {
        if (editor.IsReadOnly || editor.TextArea.Selection is RectangleSelection) return;
        if (editor.SelectionLength > 0)
        {
            string text = editor.SelectedText;
            int offset = editor.SelectionStart + editor.SelectionLength;
            editor.Document.Insert(offset, text);
            editor.Select(offset, text.Length);
        }
        else
        {
            var line = editor.Document.GetLineByOffset(editor.CaretOffset);
            int offset = line.Offset;
            int column = editor.CaretOffset - offset;
            string text = editor.Document.GetText(line.Offset, line.Length);
            string newline = line.DelimiterLength > 0 ? editor.Document.GetText(line.EndOffset, line.DelimiterLength) : Newline(editor);
            editor.Document.Insert(offset, text + newline);
            editor.CaretOffset = offset + text.Length + newline.Length + column;
        }
    }

    public static void SelectLines(TextEditor editor)
    {
        var (first, last) = SelectedLines(editor);
        var start = editor.Document.GetLineByNumber(first);
        var end = editor.Document.GetLineByNumber(last);
        editor.Select(start.Offset, end.EndOffset + end.DelimiterLength - start.Offset);
    }

    public static void DeleteLines(TextEditor editor)
    {
        if (editor.IsReadOnly || editor.TextArea.Selection is RectangleSelection) return;
        var (first, last) = SelectedLines(editor);
        var start = editor.Document.GetLineByNumber(first);
        var end = editor.Document.GetLineByNumber(last);
        int offset = start.Offset;
        int length = end.EndOffset + end.DelimiterLength - offset;
        if (end.DelimiterLength == 0 && first > 1)
        {
            int delimiter = editor.Document.GetLineByNumber(first - 1).DelimiterLength;
            offset -= delimiter;
            length += delimiter;
        }
        editor.Document.Remove(offset, length);
        editor.Select(Math.Min(offset, editor.Document.TextLength), 0);
    }

    public static void MoveLines(TextEditor editor, int direction)
    {
        if (editor.IsReadOnly || editor.TextArea.Selection is RectangleSelection) return;
        var (first, last) = SelectedLines(editor);
        if (direction < 0 && first == 1 || direction > 0 && last == editor.Document.LineCount) return;
        int rangeFirst = direction < 0 ? first - 1 : first;
        int rangeLast = direction < 0 ? last : last + 1;
        var lines = Enumerable.Range(rangeFirst, rangeLast - rangeFirst + 1).Select(editor.Document.GetLineByNumber).ToArray();
        string[] content = lines.Select(l => editor.Document.GetText(l.Offset, l.Length)).ToArray();
        string[] endings = lines.Select(l => editor.Document.GetText(l.EndOffset, l.DelimiterLength)).ToArray();
        string[] moved = direction < 0 ? content.Skip(1).Append(content[0]).ToArray() : content.TakeLast(1).Concat(content.SkipLast(1)).ToArray();
        int offset = lines[0].Offset;
        int length = lines[^1].EndOffset + lines[^1].DelimiterLength - offset;
        bool selected = editor.SelectionLength > 0;
        int column = editor.TextArea.Caret.Column;
        editor.Document.Replace(offset, length, string.Concat(moved.Select((s, i) => s + endings[i])));
        var newStart = editor.Document.GetLineByNumber(first + direction);
        var newEnd = editor.Document.GetLineByNumber(last + direction);
        if (selected) editor.Select(newStart.Offset, newEnd.EndOffset + newEnd.DelimiterLength - newStart.Offset);
        else editor.Select(newStart.Offset + Math.Min(column - 1, newStart.Length), 0);
        editor.TextArea.Caret.BringCaretToView();
    }

    private static (int First, int Last) SelectedLines(TextEditor editor)
    {
        int start = editor.SelectionStart;
        int end = start + editor.SelectionLength;
        if (end > start && editor.Document.GetLineByOffset(end).Offset == end) end--;
        return (editor.Document.GetLineByOffset(start).LineNumber, editor.Document.GetLineByOffset(end).LineNumber);
    }

    private static string Newline(TextEditor editor)
    {
        foreach (var line in editor.Document.Lines)
            if (line.DelimiterLength > 0) return editor.Document.GetText(line.EndOffset, line.DelimiterLength);
        return Environment.NewLine;
    }
}
