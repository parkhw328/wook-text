using DiffPlex.DiffBuilder.Model;

namespace WookText.Core.Tests;

public sealed class TextDiffServiceTests
{
    private readonly TextDiffService _service = new();

    [Theory]
    [InlineData("")]
    [InlineData("한글🙂\nsecond line\n")]
    public void Compare_IdenticalText_HasNoChanges(string text)
    {
        ComparisonResult result = _service.Compare(text, text);
        Assert.True(result.ExactMatch);
        Assert.Equal(0, result.Added + result.Deleted + result.Modified);
        Assert.False(result.LineEndingsDiffer);
    }

    [Fact]
    public void Compare_InsertedLine_AlignsBothPanes()
    {
        ComparisonResult result = _service.Compare("a\nb", "a\nnew\nb");
        Assert.Equal(1, result.Added);
        Assert.Equal(result.Model.OldText.Lines.Count, result.Model.NewText.Lines.Count);
        Assert.Equal(ChangeType.Imaginary, result.Model.OldText.Lines[1].Type);
        Assert.Equal(2, result.Model.NewText.Lines[1].Position);
    }

    [Fact]
    public void Compare_DeletedLine_ReportsDeletion()
    {
        ComparisonResult result = _service.Compare("a\nremoved\nb", "a\nb");
        Assert.Equal(1, result.Deleted);
        Assert.Equal(0, result.Added);
    }

    [Fact]
    public void Compare_ModifiedText_ProvidesWordDifferences()
    {
        ComparisonResult result = _service.Compare("hello old world", "hello new world");
        Assert.Equal(1, result.Modified);
        Assert.Contains(result.Model.NewText.Lines[0].SubPieces, p => p.Type != ChangeType.Unchanged);
    }

    [Fact]
    public void Compare_LineEndingDifference_IsNotReportedAsExactMatch()
    {
        ComparisonResult result = _service.Compare("first\r\nsecond", "first\nsecond");
        Assert.False(result.ExactMatch);
        Assert.True(result.LineEndingsDiffer);
    }

    [Fact]
    public void Compare_FinalNewlineDifference_IsNotExactMatch()
    {
        ComparisonResult result = _service.Compare("hello", "hello\n");
        Assert.False(result.ExactMatch);
        Assert.True(result.LineEndingsDiffer || result.Added + result.Modified + result.Deleted > 0);
    }

    [Fact]
    public void Compare_WhitespaceOption_IsExplicitAndCaseRemainsSignificant()
    {
        Assert.Equal(0, _service.Compare("  hello  ", "hello", true).Modified);
        Assert.Equal(1, _service.Compare("  hello  ", "hello", false).Modified);
        Assert.Equal(1, _service.Compare("Hello", "hello", true).Modified);
    }

    [Fact]
    public void Compare_TooLarge_RejectsInput()
    {
        Assert.Throws<InvalidOperationException>(() => _service.Compare(new string('x', TextDiffService.MaximumCharacters + 1), ""));
        Assert.Throws<InvalidOperationException>(() => _service.Compare(new string('\n', TextDiffService.MaximumLines), ""));
    }
}
