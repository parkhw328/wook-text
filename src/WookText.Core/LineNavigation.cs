using System.Globalization;

namespace WookText.Core;

public readonly record struct LineNavigationTarget(int Line, int Column);

public static class LineNavigation
{
    public const int MaximumInputLength = 64;

    /// <summary>Resolves a 1-based line or line:column using only the requested line's length.</summary>
    public static bool TryResolve(string? input, int lineCount, Func<int, int> getLineLength,
        out LineNavigationTarget target, out string error)
    {
        target = default;
        error = "줄 번호 또는 줄:열을 입력하세요. 예: 120 또는 120:8";
        if (input is null || input.Length > MaximumInputLength) return false;
        ReadOnlySpan<char> value = input.AsSpan().Trim();
        int separator = value.IndexOf(':');
        ReadOnlySpan<char> lineText = separator < 0 ? value : value[..separator].Trim();
        int column = 1;
        if (!int.TryParse(lineText, NumberStyles.None, CultureInfo.InvariantCulture, out int line) ||
            separator >= 0 && !int.TryParse(value[(separator + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out column))
            return false;
        if (line < 1 || line > lineCount)
        {
            error = $"줄 번호는 1부터 {lineCount:N0}까지 입력하세요.";
            return false;
        }
        long maximumColumn = (long)getLineLength(line) + 1;
        if (column < 1 || column > maximumColumn)
        {
            error = $"{line:N0}줄의 열 번호는 1부터 {maximumColumn:N0}까지 입력하세요. 마지막 위치는 줄 끝입니다.";
            return false;
        }
        target = new(line, column);
        error = "";
        return true;
    }
}
