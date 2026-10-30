namespace WookText.Core.Tests;

public sealed class MinimapLayoutTests
{
    [Theory]
    [InlineData(1, 800, 2)]
    [InlineData(3, 800, 6)]
    [InlineData(100, 800, 200)]
    [InlineData(400, 800, 800)]
    [InlineData(1000000, 800, 800)]
    [InlineData(3, 0, 0)]
    public void Layout_DocumentLength_CapsHeightWithoutStretching(int lines, double height, double expected)
    {
        MinimapLayout layout = new(lines, height);
        Assert.Equal(expected, layout.ContentHeight, 8);
        Assert.InRange(layout.LineHeight, 0, 2);
        var viewport = layout.GetViewport(1, lines);
        Assert.Equal(0, viewport.Top);
        Assert.Equal(expected, viewport.Height, 8);
    }

    [Theory]
    [InlineData(-10, 1)]
    [InlineData(0, 1)]
    [InlineData(1.99, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 3)]
    [InlineData(799, 3)]
    public void GetLineAtY_ShortDocument_UsesDrawnRowsAndClampsBlankArea(double y, int line)
    {
        Assert.Equal(line, new MinimapLayout(3, 800).GetLineAtY(y));
    }

    [Fact]
    public void Layout_ResizeAndDocumentGrowth_KeepsDrawingAndNavigationAligned()
    {
        foreach (int count in new[] { 3, 400, 1200, 1000000 })
            foreach (double height in new[] { 1.0, 240, 800 })
            {
                MinimapLayout layout = new(count, height);
                foreach (int line in new[] { 1, Math.Max(1, count / 2), count })
                {
                    Assert.Equal(line, layout.GetLineAtY(layout.GetLineTop(line) + layout.LineHeight / 2));
                    var viewport = layout.GetViewport(line, line + 25);
                    Assert.True(viewport.Top >= 0 && viewport.Height >= 0);
                    Assert.True(viewport.Top + viewport.Height <= layout.ContentHeight + 0.00001);
                }
            }
    }
}
