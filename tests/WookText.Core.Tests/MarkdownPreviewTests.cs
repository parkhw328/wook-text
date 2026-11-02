using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace WookText.Core.Tests;

public sealed class MarkdownPreviewTests
{
    [Fact]
    public void Parse_CommonMarkdown_ProducesStructuredBlocks()
    {
        MarkdownDocument parsed = MarkdownPreview.Parse("# 제목\n\n**강조**와 `코드`\n\n- [x] 완료\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n```cs\nvar x = 1;\n```\n");
        Assert.Contains(parsed, b => b is HeadingBlock);
        Assert.Contains(parsed, b => b is ListBlock);
        Assert.Contains(parsed, b => b is Table);
        Assert.Contains(parsed, b => b is FencedCodeBlock);
    }

    [Fact]
    public void Parse_RawHtml_DoesNotCreateHtmlBlocks()
    {
        MarkdownDocument parsed = MarkdownPreview.Parse("<script>alert('x')</script>\n\n<iframe src='https://example.org'></iframe>");
        Assert.DoesNotContain(parsed, b => b is HtmlBlock);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/test.exe")]
    [InlineData("data:text/html,test")]
    public void WebLink_ActiveOrLocalScheme_IsRejected(string value) => Assert.Null(MarkdownPreview.WebLink(value));

    [Fact]
    public void Parse_OversizedBuffer_ExplainsLimit() => Assert.Throws<IOException>(() => MarkdownPreview.Parse(new string('a', MarkdownPreview.MaximumCharacters + 1)));
}
