using System.ComponentModel;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using WookText.Core;
using WookText.Core.Pdf;

namespace WookText.App;

public sealed class EditorDocument : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _analysisTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly EditorPreferencesService _preferences;
    private PreviewKind _previewKind;
    private SessionDocumentState? _restoredView;
    public Guid SessionId { get; private set; } = Guid.NewGuid();
    public string LineEndingDescription { get; private set; } = "줄바꿈 없음";
    public Pdf.PdfDocumentView? PdfView { get; private set; }
    public bool IsPdf => PdfView is not null;
    public bool IsModified => PdfView?.IsModified ?? Editor.IsModified;
    public Preview.DocumentPreviewView? PreviewView { get; private set; }
    public bool PreviewVisible => PreviewView is not null;
    public TextEditor Editor { get; }
    public System.Windows.Controls.Grid View { get; }
    public EditorMinimap Minimap { get; }
    public string? LanguageOverride { get; private set; }
    public string LanguageName => SyntaxCatalog.Resolve(FilePath, LanguageOverride).Label;
    public string? FilePath { get; private set; }
    public string? Fingerprint { get; private set; }
    public TextFileEncoding Encoding { get; private set; } = TextFileEncoding.Utf8;
    public string UntitledName { get; }
    public string Name => FilePath is null ? UntitledName : System.IO.Path.GetFileName(FilePath);
    public string Header => Name + (IsModified ? "  ●" : "");
    public System.Windows.Media.ImageSource FileIcon => Explorer.ExplorerIcons.Get(new WorkspaceEntry(FilePath ?? "", Name, false), false);
    public string Location => FilePath ?? "아직 저장하지 않은 문서";
    public event PropertyChangedEventHandler? PropertyChanged;

    public EditorDocument(string name, EditorPreferencesService preferences)
    {
        _preferences = preferences;
        Editor = EditorFactory.Create(preferences: preferences.Current);
        EditorFactory.BindZoom(Editor, preferences);
        Minimap = new EditorMinimap(Editor);
        View = new System.Windows.Controls.Grid();
        View.ColumnDefinitions.Add(new() { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        View.ColumnDefinitions.Add(new() { Width = System.Windows.GridLength.Auto });
        System.Windows.Controls.Grid.SetColumn(Minimap, 1);
        View.Children.Add(Editor);
        View.Children.Add(Minimap);
        Minimap.Visibility = preferences.Current.ShowMinimap ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        preferences.Changed += OnPreferencesChanged;
        UntitledName = name;
        Editor.Loaded += (_, _) => RestoreView();
        Editor.Document.UndoStack.PropertyChanged += (_, _) => Notify();
        Editor.TextChanged += (_, _) => { _analysisTimer.Stop(); _analysisTimer.Start(); PreviewView?.Update(Editor.Text, FilePath, LanguageOverride); };
        _analysisTimer.Tick += (_, _) =>
        {
            _analysisTimer.Stop();
            LineEndingDescription = TextAnalysis.LineEndings(Editor.Text);
            ApplyIndentation();
            Notify();
        };
        ApplyIndentation();
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
        ApplyIndentation();
        Notify();
    }

    public void Reload(LoadedTextFile file)
    {
        SessionDocumentState view = CaptureSession()();
        RestoreSession(view, file);
    }

    public void MarkSaved(string path, string fingerprint)
    {
        FilePath = path;
        Fingerprint = fingerprint;
        if (PdfView is { } pdf) { pdf.MarkSaved(); Notify(); return; }
        Editor.IsModified = false;
        PreviewView?.Update(Editor.Text, FilePath, LanguageOverride);
        EditorFactory.ApplySyntax(Editor, FilePath, LanguageOverride);
        ApplyIndentation();
        Notify();
    }

    public void ConvertToUtf8()
    {
        Encoding = TextFileEncoding.Utf8;
        Editor.IsModified = true;
        ApplyIndentation();
        Notify();
    }

    public void SetLanguage(string? id)
    {
        LanguageOverride = id;
        EditorFactory.ApplySyntax(Editor, FilePath, id);
        PreviewView?.Update(Editor.Text, FilePath, LanguageOverride);
        ApplyIndentation();
        Notify();
    }

    public void RetargetPath(string path)
    {
        FilePath = System.IO.Path.GetFullPath(path);
        PreviewView?.Update(Editor.Text, FilePath, LanguageOverride);
        EditorFactory.ApplySyntax(Editor, FilePath, LanguageOverride);
        ApplyIndentation();
        Notify();
    }

    public Func<SessionDocumentState> CaptureSession()
    {
        if (PdfView is { } pdf)
        {
            SessionDocumentState state = new()
            {
                Id = SessionId,
                Name = UntitledName,
                FilePath = FilePath,
                Fingerprint = Fingerprint,
                Kind = "pdf",
                PdfBytes = pdf.Bytes,
                PdfPage = pdf.PageIndex,
                PdfZoom = pdf.Zoom,
                IsModified = pdf.IsModified
            };
            return () => state;
        }
        var snapshot = Editor.Document.CreateSnapshot();
        SessionDocumentState metadata = new()
        {
            PreviewVisible = PreviewVisible,
            PreviewKind = PreviewView?.Kind ?? _previewKind,
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
        ApplyIndentation();
        _previewKind = Enum.IsDefined(state.PreviewKind) ? state.PreviewKind : PreviewKind.Auto;
        bool showPreview = state.PreviewVisible || state.MarkdownPreview;
        if (showPreview != PreviewVisible) TogglePreview();
        if (PreviewView is { } preview) { preview.Kind = _previewKind; preview.Update(Editor.Text, FilePath, LanguageOverride); }
        Notify();
    }

    public void LoadPdf(LoadedPdfFile file, PdfInfo? info, bool modified = false, int page = 0, double zoom = 1, Guid? sessionId = null)
    {
        PdfView?.Dispose(); PreviewView?.Dispose(); PreviewView = null;
        FilePath = file.Path; Fingerprint = file.Fingerprint;
        if (sessionId is { } id) SessionId = id;
        PdfView = new(file.Bytes, info, _preferences, modified, page, zoom);
        PdfView.Changed += (_, _) => Notify();
        Editor.IsReadOnly = true;
        View.Children.Clear(); View.ColumnDefinitions.Clear(); View.Children.Add(PdfView);
        Notify();
    }

    public void TogglePreview()
    {
        if (IsPdf) return;
        if (PreviewView is { } existing)
        {
            _previewKind = existing.Kind; existing.Dispose(); PreviewView = null;
            while (View.Children.Count > 2) View.Children.RemoveAt(2);
            while (View.ColumnDefinitions.Count > 2) View.ColumnDefinitions.RemoveAt(2);
        }
        else
        {
            PreviewView = new(_preferences) { Kind = _previewKind };
            PreviewView.Changed += (_, _) => Notify();
            View.ColumnDefinitions.Add(new() { Width = new System.Windows.GridLength(5) });
            View.ColumnDefinitions.Add(new() { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star), MinWidth = 160 });
            System.Windows.Controls.GridSplitter splitter = new() { Width = 5, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
            System.Windows.Controls.Grid.SetColumn(splitter, 2); View.Children.Add(splitter);
            System.Windows.Controls.Grid.SetColumn(PreviewView, 3); View.Children.Add(PreviewView);
            PreviewView.Update(Editor.Text, FilePath, LanguageOverride);
        }
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

    private void ApplyIndentation()
    {
        IndentationOptions options = IndentationService.Resolve(_preferences.Current,
            SyntaxCatalog.Resolve(FilePath, LanguageOverride).Id, FilePath, Editor.Text);
        Editor.Options.IndentationSize = options.Size;
        Editor.Options.ConvertTabsToSpaces = !options.UseTabs;
    }

    private void OnPreferencesChanged(object? sender, EventArgs e)
    {
        EditorFactory.ApplyPreferences(Editor, _preferences.Current);
        Minimap.Visibility = _preferences.Current.ShowMinimap ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        ApplyIndentation();
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public void Dispose()
    {
        _analysisTimer.Stop();
        PdfView?.Dispose(); PreviewView?.Dispose();
        _preferences.Changed -= OnPreferencesChanged;
    }
}
