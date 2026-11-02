using Markdig;
using Markdig.Syntax;

namespace WookText.Core;

public static class MarkdownPreview
{
    public const int MaximumCharacters = 1_000_000;
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml().UsePipeTables().UseTaskLists().UseAutoLinks().UseEmphasisExtras().Build();

    public static MarkdownDocument Parse(string text)
    {
        if (text.Length > MaximumCharacters) throw new IOException("Markdown 미리보기는 1,000,000자까지 지원합니다. 원문은 계속 편집할 수 있습니다.");
        return Markdown.Parse(text, Pipeline);
    }

    public static string ToHtml(string text)
    {
        if (text.Length > MaximumCharacters) throw new IOException("Markdown 미리보기는 1,000,000자까지 지원합니다.");
        return Markdown.ToHtml(text, Pipeline);
    }

    public static Uri? WebLink(string? value) => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https" ? uri : null;

}
