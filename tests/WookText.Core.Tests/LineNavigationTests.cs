namespace WookText.Core.Tests;

public sealed class LineNavigationTests
{
    [Theory]
    [InlineData("1", 1, 1)]
    [InlineData("3", 3, 1)]
    [InlineData(" 2 : 4 ", 2, 4)]
    [InlineData("2:5", 2, 5)]
    [InlineData("001:02", 1, 2)]
    public void TryResolve_ValidPosition_UsesOneBasedLineAndColumn(string input, int line, int column)
    {
        Assert.True(LineNavigation.TryResolve(input, 3, _ => 4, out var target, out string error));
        Assert.Equal(new LineNavigationTarget(line, column), target);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("1,000")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1:")]
    [InlineData(":1")]
    [InlineData("1:2:3")]
    [InlineData("2147483648")]
    [InlineData("1:2147483648")]
    [InlineData("0")]
    [InlineData("4")]
    public void TryResolve_InvalidLineOrSyntax_DoesNotReadDocument(string? input)
    {
        Assert.False(LineNavigation.TryResolve(input, 3, _ => throw new InvalidOperationException("No line access expected"), out var target, out string error));
        Assert.Equal(default, target);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("2:0")]
    [InlineData("2:6")]
    public void TryResolve_InvalidColumn_RejectsWithoutClamping(string input)
    {
        Assert.False(LineNavigation.TryResolve(input, 3, _ => 4, out _, out string error));
        Assert.Contains("열 번호", error);
    }

    [Fact]
    public void TryResolve_EmptyDocument_OnlyAcceptsFirstPosition()
    {
        Assert.True(LineNavigation.TryResolve("1:1", 1, _ => 0, out var target, out _));
        Assert.Equal(new LineNavigationTarget(1, 1), target);
        Assert.False(LineNavigation.TryResolve("1:2", 1, _ => 0, out _, out _));
    }

    [Fact]
    public void TryResolve_MillionLineDocument_OnlyLooksUpTargetLine()
    {
        int calls = 0;
        Assert.True(LineNavigation.TryResolve("2000000:100001", 2_000_000, line =>
        {
            calls++;
            Assert.Equal(2_000_000, line);
            return 100_000;
        }, out var target, out _));
        Assert.Equal(new LineNavigationTarget(2_000_000, 100_001), target);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void TryResolve_OversizedPaste_RejectsBeforeDocumentAccess()
    {
        Assert.False(LineNavigation.TryResolve(new string('1', 100_000), 3,
            _ => throw new InvalidOperationException("No line access expected"), out _, out _));
    }
}
