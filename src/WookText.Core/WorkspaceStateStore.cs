using System.Text.Json;
using System.Text.Json.Serialization;

namespace WookText.Core;

public sealed record WorkspaceFolderState
{
    public string Path { get; init; } = "";
    public bool IsExpanded { get; init; } = true;
    public string[] ExpandedFolders { get; init; } = [];
}

public sealed record WorkspaceState
{
    public WorkspaceFolderState[] Folders { get; init; } = [];
    // Read the 0.x single-folder format; new saves use Folders.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RootPath { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? ExpandedFolders { get; init; }
    public string[] RecentFolders { get; init; } = [];
    public bool ShowHidden { get; init; }
    public bool HideGenerated { get; init; } = true;
}

public sealed class WorkspaceStateStore(string path)
{
    public const int FolderLimit = 10;
    public string FilePath { get; } = Path.GetFullPath(path);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public WorkspaceState Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(FilePath)) return new();
            using FileStream stream = File.OpenRead(FilePath);
            if (stream.Length > 32 * 1024 * 1024) throw new IOException("작업 폴더 설정이 너무 큽니다.");
            return Normalize(JsonSerializer.Deserialize<WorkspaceState>(stream, Options) ?? new());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { warning = "작업 폴더 설정을 읽지 못했습니다. " + ex.Message; return new(); }
    }

    public void Save(WorkspaceState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, Normalize(state), Options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static WorkspaceState Normalize(WorkspaceState state)
    {
        WorkspaceFolderState[] folders = state.Folders ?? [];
        if (folders.Length == 0 && ValidAbsolute(state.RootPath))
            folders = [new() { Path = state.RootPath!, ExpandedFolders = state.ExpandedFolders ?? [] }];
        return state with
        {
            RootPath = null,
            ExpandedFolders = null,
            Folders = folders.Where(f => f is not null && ValidAbsolute(f.Path))
                .Select(f => f with { Path = NormalizePath(f.Path), ExpandedFolders = NormalizeExpanded(f.ExpandedFolders) })
                .DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Take(FolderLimit).ToArray(),
            RecentFolders = (state.RecentFolders ?? []).Where(ValidAbsolute).Select(NormalizePath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(FolderLimit).ToArray()
        };
    }

    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string[] NormalizeExpanded(string[]? paths) =>
        (paths ?? []).Where(p => !string.IsNullOrEmpty(p) && p.Length < 2048 && !Path.IsPathRooted(p) &&
            !p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is ".." or ".") && !p.Any(c => c < 32))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(200).ToArray();

    private static bool ValidAbsolute(string? path)
    {
        try { return !string.IsNullOrWhiteSpace(path) && path.Length < 2048 && Path.IsPathFullyQualified(path) && !path.Any(c => c < 32 || c is '"' or '<' or '>' or '|' or '?' or '*') && Path.GetFullPath(path).Length > 0; }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { return false; }
    }
}
