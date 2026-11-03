namespace WookText.Core;

public static class LargeFilePolicy
{
    public const int ThresholdBytes = 16 * 1024 * 1024;
    public const int ThresholdCharacters = 8 * 1024 * 1024;
    public const int ThresholdLines = 100_000;
    public const int MaximumFileBytes = 256 * 1024 * 1024;
    public const int MaximumCharacters = 128 * 1024 * 1024;
    public const int MaximumLines = 2_000_000;
    public const int MaximumLineLength = 100_000;
    public const int MaximumWindowCharacters = 256 * 1024 * 1024;
    public const int MaximumWindowLines = 4_000_000;

    public static bool IsLarge(long bytes, int characters, int lines) =>
        bytes >= ThresholdBytes || characters >= ThresholdCharacters || lines >= ThresholdLines;
}

public sealed record TextReadLimits(int Bytes = LargeFilePolicy.MaximumFileBytes,
    int Characters = LargeFilePolicy.MaximumCharacters, int Lines = LargeFilePolicy.MaximumLines,
    int LineLength = LargeFilePolicy.MaximumLineLength);
