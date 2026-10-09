using System.IO;
using WookText.Core;

namespace WookText.App;

public sealed class EditorPreferencesService
{
    private readonly PreferencesStore _store;
    public string StoragePath => _store.FilePath;
    public EditorPreferences Current { get; private set; }
    public string? LastWarning { get; private set; }
    public event EventHandler? Changed;

    public EditorPreferencesService(PreferencesStore? store = null)
    {
        _store = store ?? new PreferencesStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wText", "settings.json"));
        Current = _store.Load(out string? warning);
        LastWarning = warning;
    }

    public bool Update(EditorPreferences preferences)
    {
        Current = preferences.Normalize();
        bool saved = true;
        try { _store.Save(Current); LastWarning = null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastWarning = "변경 사항은 적용했지만 설정을 저장하지 못했습니다. " + ex.Message;
            saved = false;
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return saved;
    }
}
