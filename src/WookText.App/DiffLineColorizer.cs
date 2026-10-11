using DiffPlex.DiffBuilder.Model;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace WookText.App;

public sealed class DiffLineColorizer(IReadOnlyList<DiffPiece> pieces) : DocumentColorizingTransformer
{
    public const int PrefixLength = 9;
    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.LineNumber > pieces.Count) return;
        DiffPiece piece = pieces[line.LineNumber - 1];
        string? background = piece.Type switch
        {
            ChangeType.Inserted => "#223E33",
            ChangeType.Deleted => "#492B33",
            ChangeType.Modified => "#64531E",
            ChangeType.Imaginary => "#20232A",
            _ => null
        };
        if (background is not null)
            ChangeLinePart(line.Offset, line.EndOffset, element => element.TextRunProperties.SetBackgroundBrush(EditorFactory.Brush(background)));
        if (piece.Type == ChangeType.Modified)
            ChangeLinePart(line.Offset, line.EndOffset, element => element.TextRunProperties.SetForegroundBrush(EditorFactory.Brush("#FFF4CF")));
        ChangeLinePart(line.Offset, Math.Min(line.EndOffset, line.Offset + PrefixLength),
            element => element.TextRunProperties.SetForegroundBrush(EditorFactory.Brush(piece.Type == ChangeType.Modified ? "#F4D66A" : "#9BA6B8")));
        int offset = line.Offset + PrefixLength;
        foreach (DiffPiece subpiece in piece.SubPieces)
        {
            // DiffPlex uses null text for imaginary word-alignment pieces.
            int end = Math.Min(offset + (subpiece.Text?.Length ?? 0), line.EndOffset);
            if (end > offset && subpiece.Type is ChangeType.Inserted or ChangeType.Deleted or ChangeType.Modified)
                ChangeLinePart(offset, end, element =>
                {
                    element.TextRunProperties.SetBackgroundBrush(EditorFactory.Brush("#8A6818"));
                    element.TextRunProperties.SetForegroundBrush(EditorFactory.Brush("#FFF9E8"));
                });
            offset = end;
        }
    }

    public static string Format(DiffPiece piece)
    {
        string marker = piece.Type switch { ChangeType.Inserted => "+", ChangeType.Deleted => "−", ChangeType.Modified => "~", _ => " " };
        return $"{piece.Position?.ToString().PadLeft(5) ?? "     "}  {marker} {piece.Text}";
    }
}
