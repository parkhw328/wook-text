using System.ComponentModel;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using WookText.Core;

namespace WookText.App;

public sealed class EditorDocument : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _analysisTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly EditorPreferencesService _preferences;
    private SessionDocumentState? _restoredView;
    public Guid SessionId { get; private set; } = Guid.NewGuid();
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
        Editor.Loaded += (_, _) => RestoreView();
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

    public Func<SessionDocumentState> CaptureSession()
    {
        var snapshot = Editor.Document.CreateSnapshot();
        SessionDocumentState metadata = new()
        {
            Id = SessionId,
            Name = UntitledName,
            FilePath = FilePath,
            Encoding = Encoding,
            Fingerprint = Fingerprint,
            IsModified = Editor.IsModified,
            Language = LanguageOverride,
            CaretOffset = Editor.CaretOffset,
            SelectionStart = Editor.SelectionStart,
            SelectionLength = Editor.SelectionLength,
            VerticalOffset = Editor.VerticalOffset,
            HorizontalOffset = Editor.HorizontalOffset
        };
        return () => metadata with { Text = snapshot.Text };
    }

    public void RestoreSession(SessionDocumentState state, LoadedTextFile? currentFile = null)
    {
        SessionId = state.Id;
        FilePath = currentFile?.Path ?? state.FilePath;
        Fingerprint = currentFile?.Fingerprint ?? state.Fingerprint;
        Encoding = currentFile?.Encoding ?? state.Encoding;
        Editor.Text = currentFile?.Text ?? state.Text;
        Editor.Document.UndoStack.ClearAll();
        Editor.IsModified = currentFile is null && (state.IsModified || state.FilePath is not null);
        LanguageOverride = state.Language;
        EditorFactory.ApplySyntax(Editor, FilePath, LanguageOverride);
        _analysisTimer.Stop();
        LineEndingDescription = TextAnalysis.LineEndings(Editor.Text);
        int start = Math.Clamp(state.SelectionStart, 0, Editor.Document.TextLength);
        int length = Math.Clamp(state.SelectionLength, 0, Editor.Document.TextLength - start);
        Editor.Select(start, length);
        Editor.CaretOffset = Math.Clamp(state.CaretOffset, 0, Editor.Document.TextLength);
        _restoredView = state;
        if (Editor.IsLoaded) RestoreView();
        Notify();
    }

    private void RestoreView()
    {
        if (_restoredView is not { } state) return;
        _restoredView = null;
        Editor.Dispatcher.InvokeAsync(() =>
        {
            Editor.ScrollToVerticalOffset(double.IsFinite(state.VerticalOffset) ? Math.Max(0, state.VerticalOffset) : 0);
            Editor.ScrollToHorizontalOffset(double.IsFinite(state.HorizontalOffset) ? Math.Max(0, state.HorizontalOffset) : 0);
        }, DispatcherPriority.Loaded);
    }

    private void OnPreferencesChanged(object? sender, EventArgs e) => EditorFactory.ApplyPreferences(Editor, _preferences.Current);

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public void Dispose()
    {
        _analysisTimer.Stop();
        _preferences.Changed -= OnPreferencesChanged;
    }
}
