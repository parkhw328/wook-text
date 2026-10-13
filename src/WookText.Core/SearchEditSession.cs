using System.Globalization;

namespace WookText.Core;

/// <summary>A fixed set of search matches; newly typed query text never adds targets.</summary>
public sealed class SearchEditSession
{
    public IReadOnlyList<int> Offsets { get; }
    public int MatchLength { get; }

    private SearchEditSession(int[] offsets, int matchLength)
    {
        Offsets = Array.AsReadOnly(offsets);
        MatchLength = matchLength;
    }

    public static SearchEditSession Create(string text, string query, bool matchCase) =>
        new(TextAnalysis.FindAll(text, query, matchCase).ToArray(), query.Length);

    public int FindTarget(int start, int length, int preferredIndex)
    {
        bool Contains(int index) => index >= 0 && index < Offsets.Count && start >= Offsets[index]
            && length >= 0 && start - Offsets[index] <= MatchLength - length;
        if (Contains(preferredIndex)) return preferredIndex;
        for (int i = 0; i < Offsets.Count; i++)
            if (Contains(i)) return i;
        return -1;
    }

    public SearchEditChange Replace(int relativeOffset, int removalLength, string insertedText)
    {
        if (relativeOffset < 0 || removalLength < 0 || relativeOffset > MatchLength - removalLength)
            throw new ArgumentOutOfRangeException(nameof(relativeOffset));
        int delta = insertedText.Length - removalLength;
        TextReplacement[] edits = Offsets.Reverse().Select(offset => new TextReplacement(offset + relativeOffset, removalLength, insertedText)).ToArray();
        int[] offsets = Offsets.Select((offset, index) => checked(offset + index * delta)).ToArray();
        return new(edits, new(offsets, checked(MatchLength + delta)), relativeOffset + insertedText.Length);
    }

    public static (int Offset, int Length) DeletionRange(string target, int caret, bool backwards)
    {
        if (caret < 0 || caret > target.Length) throw new ArgumentOutOfRangeException(nameof(caret));
        int[] boundaries = StringInfo.ParseCombiningCharacters(target).Append(target.Length).ToArray();
        int index = Array.BinarySearch(boundaries, caret);
        if (index < 0)
        {
            int next = ~index;
            return (boundaries[next - 1], boundaries[next] - boundaries[next - 1]);
        }
        if (backwards && index > 0) return (boundaries[index - 1], caret - boundaries[index - 1]);
        if (!backwards && index + 1 < boundaries.Length) return (caret, boundaries[index + 1] - caret);
        return (caret, 0);
    }
}

public sealed record TextReplacement(int Offset, int Length, string Text);
public sealed record SearchEditChange(IReadOnlyList<TextReplacement> Replacements, SearchEditSession Session, int RelativeCaret);
