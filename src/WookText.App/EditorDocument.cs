using System.ComponentModel;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using WookText.Core;

namespace WookText.App;

public sealed class EditorDocument : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _analysisTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly EditorPreferencesService _preferences;
    public string LineEndingDescription { get; private set; } = "줄바꿈 없음";
    public TextEditor Editor { get; }
    public string? LanguageOverride { get; private set; }
    public string LanguageName => SyntaxCatalog.Resolve(FilePath, LanguageOverride).Label;
    public string? FilePath { get; private set; }
    public string? Fingerprint { get; private set; }
    public TextFileEncoding Encoding { get; private set; } = TextFileEncoding.Utf8;
    public string UntitledName { get; }
    public string Name => FilePath is null ? UntitledName : System.IO.Path.GetFileName(FilePath);
    public string Header => Name + (Editor.IsModified ? "  ●" : "");
    public System.Windows.Media.ImageSource FileIcon => Explorer.ExplorerIcons.Get(new WorkspaceEntry(FilePath ?? "", Name, false), false);
    public string Location => FilePath ?? "아직 저장하지 않은 문서";
    public event PropertyChangedEventHandler? PropertyChanged;

    public EditorDocument(string name, EditorPreferencesService preferences)
    {
        _preferences = preferences;
        Editor = EditorFactory.Create(preferences: preferences.Current);
        EditorFactory.BindZoom(Editor, preferences);
        preferences.Changed += OnPreferencesChanged;
        UntitledName = name;
        Editor.Document.UndoStack.PropertyChanged += (_, _) => Notify();
        Editor.TextChanged += (_, _) => { _analysisTimer.Stop(); _analysisTimer.Start(); };
        _analysisTimer.Tick += (_, _) =>
        {
            _analysisTimer.Stop();
            LineEndingDescription = TextAnalysis.LineEndings(Editor.Text);
            Notify();
        };
    }

    public void Load(LoadedTextFile file)
    {
        FilePath = file.Path;
        Fingerprint = file.Fingerprint;
        Encoding = file.Encoding;
        Editor.Text = file.Text;
        Editor.Document.UndoStack.ClearAll();
        Editor.IsModified = false;
        _analysisTimer.Stop();
        LineEndingDescription = TextAnalysis.LineEndings(file.Text);
        LanguageOverride = null;
        EditorFactory.ApplySyntax(Editor, FilePath);
        Notify();
    }

    public void MarkSaved(string path, string fingerprint)
    {
        FilePath = path;
        Fingerprint = fingerprint;
        Editor.IsModified = false;
        EditorFactory.ApplySyntax(Editor, FilePath, LanguageOverride);
        Notify();
    }

    public void ConvertToUtf8()
    {
        Encoding = TextFileEncoding.Utf8;
        Editor.IsModified = true;
        Notify();
    }

    public void SetLanguage(string? id)
    {
        LanguageOverride = id;
        EditorFactory.ApplySyntax(Editor, FilePath, id);
        Notify();
    }

    public void RetargetPath(string path)
    {
        FilePath = System.IO.Path.GetFullPath(path);
        EditorFactory.ApplySyntax(Editor, FilePath, LanguageOverride);
        Notify();
    }

    private void OnPreferencesChanged(object? sender, EventArgs e) => EditorFactory.ApplyPreferences(Editor, _preferences.Current);

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public void Dispose()
    {
        _analysisTimer.Stop();
        _preferences.Changed -= OnPreferencesChanged;
    }
}
