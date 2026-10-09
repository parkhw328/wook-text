using System.Text.Json;

namespace WookText.Core;

public sealed class PreferencesStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string FilePath { get; } = Path.GetFullPath(path);

    public EditorPreferences Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(FilePath)) return new EditorPreferences();
        try
        {
            using FileStream stream = File.OpenRead(FilePath);
            if (stream.Length > 64 * 1024) throw new IOException("설정 파일이 너무 큽니다.");
            return (JsonSerializer.Deserialize<EditorPreferences>(stream, JsonOptions) ?? new EditorPreferences()).Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            warning = "설정을 읽지 못해 기본값을 적용했습니다. " + ex.Message;
            return new EditorPreferences();
        }
    }

    public void Save(EditorPreferences preferences)
    {
        string directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, preferences.Normalize(), JsonOptions);
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
}
