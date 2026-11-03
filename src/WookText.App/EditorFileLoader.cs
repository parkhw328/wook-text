using ICSharpCode.AvalonEdit.Document;
using WookText.Core;

namespace WookText.App;

public sealed record LoadedEditorFile(LoadedTextFile File, TextDocument Buffer)
{
    public string Path => File.Path;
    public string Fingerprint => File.Fingerprint;
    public TextFileEncoding Encoding => File.Encoding;
}

public static class EditorFileLoader
{
    public static Task<LoadedEditorFile> ReadAsync(string path, TextFileEncoding? encoding = null,
        TextReadLimits? limits = null, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        TextDocument document = new();
        document.UndoStack.SizeLimit = 0;
        LoadedTextFile metadata = new TextFileService().ReadChunks(path,
            chunk => document.Insert(document.TextLength, chunk), encoding, cancellationToken, limits);
        document.UndoStack.ClearAll();
        document.SetOwnerThread(null);
        return new LoadedEditorFile(metadata, document);
    }, cancellationToken);
}
