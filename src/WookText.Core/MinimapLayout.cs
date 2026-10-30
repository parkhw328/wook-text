namespace WookText.Core;

/// <summary>Shared coordinates for the document overview, viewport and pointer navigation.</summary>
public sealed class MinimapLayout
{
    public const double MaximumLineHeight = 2;
    public int LineCount { get; }
    public double LineHeight { get; }
    public double ContentHeight => LineCount * LineHeight;

    public MinimapLayout(int lineCount, double availableHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lineCount, 1);
        if (!double.IsFinite(availableHeight) || availableHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(availableHeight));
        LineCount = lineCount;
        LineHeight = Math.Min(MaximumLineHeight, availableHeight / lineCount);
    }

    public double GetLineTop(int line) => (Math.Clamp(line, 1, LineCount) - 1) * LineHeight;

    public int GetLineAtY(double y)
    {
        if (LineHeight == 0 || double.IsNaN(y)) return 1;
        double row = Math.Floor(Math.Clamp(y, 0, ContentHeight) / LineHeight);
        return (int)Math.Min(LineCount - 1, row) + 1;
    }

    public (double Top, double Height) GetViewport(int firstLine, int lastLine)
    {
        double top = GetLineTop(firstLine);
        double bottom = GetLineTop(Math.Max(firstLine, lastLine)) + LineHeight;
        double height = Math.Min(ContentHeight, Math.Max(2, bottom - top));
        return (Math.Min(top, ContentHeight - height), height);
    }
}
