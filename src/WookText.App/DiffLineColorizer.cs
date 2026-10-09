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
            ChangeType.Modified => "#393627",
            ChangeType.Imaginary => "#20232A",
            _ => null
        };
        if (background is not null)
            ChangeLinePart(line.Offset, line.EndOffset, element => element.TextRunProperties.SetBackgroundBrush(EditorFactory.Brush(background)));
        ChangeLinePart(line.Offset, Math.Min(line.EndOffset, line.Offset + PrefixLength),
            element => element.TextRunProperties.SetForegroundBrush(EditorFactory.Brush("#9BA6B8")));
        int offset = line.Offset + PrefixLength;
        foreach (DiffPiece subpiece in piece.SubPieces)
        {
            int end = Math.Min(offset + subpiece.Text.Length, line.EndOffset);
            if (end > offset && subpiece.Type is ChangeType.Inserted or ChangeType.Deleted or ChangeType.Modified)
                ChangeLinePart(offset, end, element => element.TextRunProperties.SetBackgroundBrush(EditorFactory.Brush("#615034")));
            offset = end;
        }
    }

    public static string Format(DiffPiece piece)
    {
        string marker = piece.Type switch { ChangeType.Inserted => "+", ChangeType.Deleted => "−", ChangeType.Modified => "~", _ => " " };
        return $"{piece.Position?.ToString().PadLeft(5) ?? "     "}  {marker} {piece.Text}";
    }
}
