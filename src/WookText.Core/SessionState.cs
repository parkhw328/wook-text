namespace WookText.Core;

public sealed record SessionDocumentState
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "새 문서";
    public string? FilePath { get; init; }
    public string Text { get; init; } = "";
    public bool IsModified { get; init; }
    public TextFileEncoding Encoding { get; init; } = TextFileEncoding.Utf8;
    public string? Fingerprint { get; init; }
    public string? Language { get; init; }
    public int CaretOffset { get; init; }
    public int SelectionStart { get; init; }
    public int SelectionLength { get; init; }
    public double VerticalOffset { get; init; }
    public double HorizontalOffset { get; init; }
}

public sealed record SessionState
{
    public int FormatVersion { get; init; } = 1;
    public Guid? ActiveDocumentId { get; init; }
    public SessionDocumentState[] Documents { get; init; } = [];
}

public sealed record SessionLoadResult(SessionState State, string? Warning = null, bool CanOverwrite = true);
