using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace WookText.App;

public sealed class SearchMatchColorizer : DocumentColorizingTransformer
{
    private static readonly SolidColorBrush _background = EditorFactory.Brush("#74501D");
    private static readonly SolidColorBrush _foreground = EditorFactory.Brush("#FFF4CF");
    public List<int> Offsets { get; set; } = [];
    public int MatchLength { get; set; }

    protected override void ColorizeLine(DocumentLine line)
    {
        int first = Offsets.BinarySearch(Math.Max(0, line.Offset - MatchLength + 1));
        if (first < 0) first = ~first;
        for (int i = first; i < Offsets.Count && Offsets[i] < line.EndOffset; i++)
        {
            int start = Math.Max(line.Offset, Offsets[i]);
            int end = Math.Min(line.EndOffset, Offsets[i] + MatchLength);
            if (end <= start) continue;
            ChangeLinePart(start, end, element =>
            {
                element.TextRunProperties.SetBackgroundBrush(_background);
                element.TextRunProperties.SetForegroundBrush(_foreground);
            });
        }
    }
}
