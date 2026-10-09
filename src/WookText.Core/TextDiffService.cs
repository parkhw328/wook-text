using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace WookText.Core;

public sealed record ComparisonResult(SideBySideDiffModel Model, bool ExactMatch,
    bool LineEndingsDiffer, int Added, int Deleted, int Modified);

public sealed class TextDiffService
{
    public const int MaximumCharacters = 500_000;
    public const int MaximumLines = 20_000;

    public ComparisonResult Compare(string left, string right, bool ignoreWhitespace = false)
    {
        Validate(left);
        Validate(right);
        SideBySideDiffBuilder builder = new(new Differ());
        SideBySideDiffModel model = builder.BuildDiffModel(left, right, ignoreWhitespace, ignoreCase: false);
        return new ComparisonResult(model, left == right,
            TextAnalysis.LineEndings(left) != TextAnalysis.LineEndings(right) ||
            (!ignoreWhitespace && left != right &&
                model.OldText.Lines.All(line => line.Type == ChangeType.Unchanged) &&
                model.NewText.Lines.All(line => line.Type == ChangeType.Unchanged)),
            model.NewText.Lines.Count(line => line.Type == ChangeType.Inserted),
            model.OldText.Lines.Count(line => line.Type == ChangeType.Deleted),
            model.NewText.Lines.Count(line => line.Type == ChangeType.Modified));
    }

    private static void Validate(string text)
    {
        if (text.Length > MaximumCharacters || text.Count(c => c == '\n' || c == '\r') > MaximumLines * 2 ||
            text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length > MaximumLines)
        {
            throw new InvalidOperationException("첫 버전의 비교 한도는 파일당 500,000자, 20,000줄입니다.");
        }
    }
}
