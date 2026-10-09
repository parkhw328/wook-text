using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WookText.Core;

/// <summary>Atomic, per-window recovery snapshots. The lease prevents two processes from overwriting a session.</summary>
public sealed class SessionStore : IDisposable
{
    public const int MaximumBytes = 128 * 1024 * 1024;
    public const int MaximumDocuments = 256;
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly FileStream? _lease;
    private readonly SemaphoreSlim _writes = new(1, 1);
    public string FilePath { get; }
    public string BackupPath => FilePath + ".bak";

    public SessionStore(string path) => FilePath = Path.GetFullPath(path);
    private SessionStore(string path, FileStream lease) : this(path) => _lease = lease;

    public static Task<SessionStore> AcquireAsync(string directory, bool fresh = false) => Task.Run(() =>
    {
        Directory.CreateDirectory(directory);
        if (!fresh)
        {
            foreach (string path in Directory.EnumerateFiles(directory, "session-*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                try { return new SessionStore(path, Lease(path)); }
                catch (IOException) { /* Another running window owns this session. */ }
            }
        }
        string created = Path.Combine(directory, $"session-{Guid.NewGuid():N}.json");
        return new SessionStore(created, Lease(created));
    });

    private static FileStream Lease(string path) => new(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite,
        FileShare.None, 1, FileOptions.DeleteOnClose);

    public async Task<SessionLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath) && !File.Exists(BackupPath)) return new(new());
        string? failure = null;
        foreach (string path in new[] { FilePath, BackupPath })
        {
            try
            {
                await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (stream.Length > MaximumBytes) throw new IOException("작업 보관 파일이 128 MiB를 초과합니다.");
                SessionState state = await JsonSerializer.DeserializeAsync<SessionState>(stream, JsonOptions, cancellationToken)
                    ?? throw new JsonException("작업 보관 파일이 비어 있습니다.");
                Validate(state);
                return new(state, path == BackupPath ? "마지막 보관 파일을 읽지 못해 이전 복구본을 불러왔습니다." : null,
                    CanOverwrite: path != BackupPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { failure ??= ex.Message; }
        }
        return new(new(), "보관한 작업을 읽지 못했습니다. 원본 복구 파일은 그대로 남겨두었습니다. " + failure, CanOverwrite: false);
    }

    public async Task SaveAsync(SessionState state, CancellationToken cancellationToken = default)
    {
        Validate(state);
        await _writes.WaitAsync(cancellationToken);
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
                if (stream.Length > MaximumBytes) throw new IOException("작업 보관 한도는 128 MiB입니다. 큰 문서를 파일로 저장한 뒤 해당 탭을 닫아 주세요.");
                await stream.FlushAsync(cancellationToken);
                // This store is called on a worker thread; durable writes never block the editor dispatcher.
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, BackupPath);
            else File.Move(temporary, FilePath);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            _writes.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _writes.WaitAsync();
        try
        {
            // Clear the older recovery copy first so a failed clear never resurrects an explicitly discarded tab.
            File.Delete(BackupPath);
            File.Delete(FilePath);
        }
        finally { _writes.Release(); }
    }

    private static void Validate(SessionState state)
    {
        if (state.FormatVersion != 1) throw new IOException("이 버전에서 읽을 수 없는 작업 보관 형식입니다.");
        if (state.Documents is null || state.Documents.Length > MaximumDocuments) throw new IOException("작업 보관은 최대 256개 문서를 지원합니다.");
        HashSet<Guid> ids = [];
        long characters = 0;
        foreach (SessionDocumentState document in state.Documents)
        {
            if (document is null || document.Id == Guid.Empty || !ids.Add(document.Id) || document.Text is null || string.IsNullOrWhiteSpace(document.Name) ||
                !Enum.IsDefined(document.Encoding) || document.FilePath is { } path && (!Path.IsPathFullyQualified(path) || path.Contains('\0')))
                throw new IOException("작업 보관 파일의 문서 정보가 올바르지 않습니다.");
            characters += document.Text.Length;
            if (characters > MaximumBytes / 2) throw new IOException("보관할 문서가 너무 큽니다. 큰 문서를 저장하고 탭을 닫아 주세요.");
            // JSON writers replace invalid UTF-16; reject it instead of silently changing a buffer.
            _ = new UnicodeEncoding(false, false, true).GetByteCount(document.Text);
        }
    }

    public void Dispose() => _lease?.Dispose();
}
