using System.Text.Json;

namespace WookText.Core;

public sealed record WorkspaceState
{
    public string? RootPath { get; init; }
    public string[] ExpandedFolders { get; init; } = [];
    public string[] RecentFolders { get; init; } = [];
    public bool ShowHidden { get; init; }
    public bool HideGenerated { get; init; } = true;
}

public sealed class WorkspaceStateStore(string path)
{
    public string FilePath { get; } = Path.GetFullPath(path);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public WorkspaceState Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(FilePath)) return new();
            using FileStream stream = File.OpenRead(FilePath);
            if (stream.Length > 256 * 1024) throw new IOException("작업 폴더 설정이 너무 큽니다.");
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

    private static WorkspaceState Normalize(WorkspaceState state) => state with
    {
        RootPath = ValidAbsolute(state.RootPath) ? Path.GetFullPath(state.RootPath!) : null,
        RecentFolders = (state.RecentFolders ?? []).Where(ValidAbsolute).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToArray(),
        ExpandedFolders = (state.ExpandedFolders ?? []).Where(p => !string.IsNullOrEmpty(p) && p.Length < 2048 && !Path.IsPathRooted(p) &&
            !p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is ".." or ".") && !p.Any(c => c < 32))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(200).ToArray()
    };

    private static bool ValidAbsolute(string? path)
    {
        try { return !string.IsNullOrWhiteSpace(path) && path.Length < 2048 && Path.IsPathFullyQualified(path) && !path.Any(c => c < 32 || c is '"' or '<' or '>' or '|' or '?' or '*') && Path.GetFullPath(path).Length > 0; }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { return false; }
    }
}
