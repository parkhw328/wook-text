namespace WookText.Core.Tests;

public sealed class ScrollWheelBatchTests
{
    [Fact]
    public void Add_RapidInput_PreservesDistanceInSingleTarget()
    {
        ScrollWheelBatch batch = new();
        for (int i = 0; i < 1000; i++) batch.Add(-120, 0, 1_000_000, 600, 20, 3);
        Assert.Equal(60_000d, batch.Take());
        Assert.Null(batch.Take());
    }

    [Fact]
    public void Add_FractionalAndReverseInput_PreservesNetDistance()
    {
        ScrollWheelBatch batch = new();
        batch.Add(-30, 100, 10_000, 600, 20, 3);
        batch.Add(-30, 100, 10_000, 600, 20, 3);
        batch.Add(15, 100, 10_000, 600, 20, 3);
        Assert.Equal(122.5, batch.Take());
    }

    [Theory]
    [InlineData(0, 120, -120, 60)]
    [InlineData(9400, -120, 120, 9340)]
    public void Add_ReverseAtDocumentEdge_DoesNotAccumulateOverscroll(double offset, int first, int second, double expected)
    {
        ScrollWheelBatch batch = new();
        for (int i = 0; i < 100; i++) batch.Add(first, offset, 10_000, 600, 20, 3);
        batch.Add(second, offset, 10_000, 600, 20, 3);
        Assert.Equal(expected, batch.Take());
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 120)]
    [InlineData(3, 160)]
    [InlineData(-1, 700)]
    public void Add_SystemWheelSetting_UsesLinesOrOnePage(int lines, double expected)
    {
        ScrollWheelBatch batch = new();
        batch.Add(-120, 100, 10_000, 600, 20, lines);
        Assert.Equal(expected, batch.Take());
    }

    [Fact]
    public void Reset_NavigationBetweenInputs_DiscardsOldPosition()
    {
        ScrollWheelBatch batch = new();
        batch.Add(-120, 0, 10_000, 600, 20, 3);
        batch.Reset();
        Assert.Null(batch.Take());
        batch.Add(-120, 5000, 10_000, 600, 20, 3);
        Assert.Equal(5060d, batch.Take());
    }

    [Fact]
    public void Add_ShortOrShrinkingDocument_ClampsToCurrentExtent()
    {
        ScrollWheelBatch batch = new();
        batch.Add(-120, 5000, 10_000, 600, 20, 3);
        batch.Add(-120, 5000, 800, 600, 20, 3);
        Assert.Equal(200d, batch.Take());
        batch.Add(-120, 0, 200, 600, 20, 3);
        Assert.Equal(0d, batch.Take());
    }
}
