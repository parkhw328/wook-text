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
    private readonly LargeFileScrolling _scrolling;
    private PreviewKind _previewKind;
    private TextFileEncoding _diskEncoding = TextFileEncoding.Utf8;
    private SessionDocumentState? _restoredView;
    public Guid SessionId { get; private set; } = Guid.NewGuid();
    public string LineEndingDescription { get; private set; } = "줄바꿈 없음";
    public Pdf.PdfDocumentView? PdfView { get; private set; }
    public bool IsPdf => PdfView is not null;
    public bool IsLargeFile { get; private set; }
    public bool CanPreview => !IsPdf && !IsLargeFile && Editor.Document.TextLength <= DocumentPreview.MaximumCharacters;
    private readonly System.Windows.Controls.TextBlock _largeNotice = new()
    {
        Text = "대용량 모드 · 미리보기/미니맵/전체 검색 강조/자동 복구 중지 · 외부 갱신은 수동 다시 불러오기 · 수정 후 Ctrl+S로 저장하세요.",
        TextWrapping = System.Windows.TextWrapping.Wrap,
        Foreground = EditorFactory.Brush("#DA702C"),
        Margin = new System.Windows.Thickness(10, 6, 10, 6),
        Visibility = System.Windows.Visibility.Collapsed
    };
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
        _scrolling = new LargeFileScrolling(Editor);
        _largeNotice.ToolTip = "스크롤바를 끌 때 본문은 놓은 위치에서 갱신됩니다. URL·이메일 자동 링크는 표시하지 않습니다.";
        Minimap = new EditorMinimap(Editor);
        View = new System.Windows.Controls.Grid();
        View.ColumnDefinitions.Add(new() { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        View.ColumnDefinitions.Add(new() { Width = System.Windows.GridLength.Auto });
        System.Windows.Controls.Grid.SetColumn(Minimap, 1);
        View.RowDefinitions.Add(new() { Height = System.Windows.GridLength.Auto });
        View.RowDefinitions.Add(new() { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        System.Windows.Controls.Grid.SetRow(Editor, 1);
        System.Windows.Controls.Grid.SetRow(Minimap, 1);
        System.Windows.Controls.Grid.SetColumnSpan(_largeNotice, 4);
        View.Children.Add(Editor);
        View.Children.Add(Minimap);
        View.Children.Add(_largeNotice);
        Minimap.Visibility = preferences.Current.ShowMinimap ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        preferences.Changed += OnPreferencesChanged;
        UntitledName = name;
        Editor.Loaded += (_, _) => RestoreView();
        Editor.Document.UndoStack.PropertyChanged += (_, _) => Notify();
        Editor.TextChanged += (_, _) =>
        {
            UpdateResourceMode();
            _analysisTimer.Stop();
            if (!IsLargeFile) _analysisTimer.Start();
            else LineEndingDescription = "줄바꿈 자동 분석 중지";
            if (!CanPreview && PreviewVisible) TogglePreview();
            PreviewView?.Update(Editor.Text, FilePath, LanguageOverride);
        };
        _analysisTimer.Tick += (_, _) =>
        {
            _analysisTimer.Stop();
            LineEndingDescription = TextAnalysis.LineEndings(Editor.Text);
            ApplyIndentation();
            Notify();
        };
        ApplyIndentation();
    }

    public void Load(LoadedEditorFile file)
    {
        FilePath = file.Path;
        Fingerprint = file.Fingerprint;
        Encoding = file.Encoding;
        _diskEncoding = file.Encoding;
        SetBuffer(file);
        Editor.IsModified = false;
        _analysisTimer.Stop();
        LineEndingDescription = file.File.LineEndings;
        LanguageOverride = null;
        ApplyResourcePreferences();
        Notify();
    }

    private void SetBuffer(LoadedEditorFile file)
    {
        if (PreviewVisible) TogglePreview();
        IsLargeFile = LargeFilePolicy.IsLarge(file.File.FileBytes, file.File.CharacterCount, file.File.LineCount);
        // Apply limits before assigning the buffer; TextChanged and layout can run during assignment.
        ApplyResourcePreferences();
        file.Buffer.SetOwnerThread(Thread.CurrentThread);
        Editor.Document = file.Buffer;
        Editor.Document.UndoStack.SizeLimit = IsLargeFile ? 100 : int.MaxValue;
        Editor.Document.UndoStack.PropertyChanged += (_, _) => Notify();
    }

    public void Reload(LoadedEditorFile file)
    {
        // Only view metadata is needed; never flatten the old buffer before replacing it.
        RestoreSession(CaptureSession(includeText: false)(), file);
    }

    public void MarkSaved(string path, string fingerprint)
    {
        FilePath = path;
        Fingerprint = fingerprint;
        if (PdfView is { } pdf) { pdf.MarkSaved(); Notify(); return; }
        Editor.IsModified = false;
        _diskEncoding = Encoding;
        PreviewView?.Update(Editor.Text, FilePath, LanguageOverride);
        ApplySyntax();
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
        ApplySyntax();
        PreviewView?.Update(Editor.Text, FilePath, LanguageOverride);
        ApplyIndentation();
        Notify();
    }

    public void RetargetPath(string path)
    {
        FilePath = System.IO.Path.GetFullPath(path);
        PreviewView?.Update(Editor.Text, FilePath, LanguageOverride);
        ApplySyntax();
        ApplyIndentation();
        Notify();
    }

    public Func<SessionDocumentState> CaptureSession(bool includeText = true)
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
        var snapshot = includeText && !IsLargeFile ? Editor.Document.CreateSnapshot() : null;
        SessionDocumentState metadata = new()
        {
            PreviewVisible = PreviewVisible,
            PreviewKind = PreviewView?.Kind ?? _previewKind,
            Id = SessionId,
            Name = UntitledName,
            FilePath = FilePath,
            Encoding = IsLargeFile ? _diskEncoding : Encoding,
            DiskEncoding = _diskEncoding,
            Fingerprint = Fingerprint,
            IsModified = !IsLargeFile && Editor.IsModified,
            ReopenFromDisk = IsLargeFile,
            Language = LanguageOverride,
            CaretOffset = Editor.CaretOffset,
            SelectionStart = Editor.SelectionStart,
            SelectionLength = Editor.SelectionLength,
            VerticalOffset = Editor.VerticalOffset,
            HorizontalOffset = Editor.HorizontalOffset
        };
        return () => metadata with { Text = snapshot?.Text ?? "" };
    }

    public void RestoreSession(SessionDocumentState state, LoadedEditorFile? currentFile = null)
    {
        SessionId = state.Id;
        FilePath = currentFile?.Path ?? state.FilePath;
        Fingerprint = currentFile?.Fingerprint ?? state.Fingerprint;
        Encoding = currentFile?.Encoding ?? state.Encoding;
        _diskEncoding = currentFile?.Encoding ?? state.DiskEncoding ?? state.Encoding;
        if (currentFile is not null) SetBuffer(currentFile);
        else
        {
            if (state.ReopenFromDisk) throw new System.IO.IOException("대용량 파일 원본을 다시 열 수 없습니다. 자동 복구 본문은 보관하지 않습니다.");
            IsLargeFile = LargeFilePolicy.IsLarge(0, state.Text.Length, 1);
            ApplyResourcePreferences();
            Editor.Text = state.Text;
        }
        Editor.Document.UndoStack.ClearAll();
        Editor.IsModified = currentFile is null && (state.IsModified || state.FilePath is not null);
        LanguageOverride = state.Language;
        ApplySyntax();
        _analysisTimer.Stop();
        LineEndingDescription = currentFile?.File.LineEndings ?? (IsLargeFile ? "줄바꿈 자동 분석 중지" : TextAnalysis.LineEndings(Editor.Text));
        int start = Math.Clamp(state.SelectionStart, 0, Editor.Document.TextLength);
        int length = Math.Clamp(state.SelectionLength, 0, Editor.Document.TextLength - start);
        Editor.Select(start, length);
        Editor.CaretOffset = Math.Clamp(state.CaretOffset, 0, Editor.Document.TextLength);
        _restoredView = state;
        if (Editor.IsLoaded) RestoreView();
        ApplyIndentation();
        _previewKind = Enum.IsDefined(state.PreviewKind) ? state.PreviewKind : PreviewKind.Auto;
        bool showPreview = CanPreview && (state.PreviewVisible || state.MarkdownPreview);
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
        View.Children.Clear(); View.ColumnDefinitions.Clear(); View.RowDefinitions.Clear(); View.Children.Add(PdfView);
        Notify();
    }

    public void TogglePreview()
    {
        if (IsPdf || (!PreviewVisible && !CanPreview)) return;
        if (PreviewView is { } existing)
        {
            _previewKind = existing.Kind; existing.Dispose(); PreviewView = null;
            while (View.Children.Count > 3) View.Children.RemoveAt(3);
            while (View.ColumnDefinitions.Count > 2) View.ColumnDefinitions.RemoveAt(2);
        }
        else
        {
            PreviewView = new(_preferences) { Kind = _previewKind };
            PreviewView.Changed += (_, _) => Notify();
            View.ColumnDefinitions.Add(new() { Width = new System.Windows.GridLength(5) });
            View.ColumnDefinitions.Add(new() { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star), MinWidth = 160 });
            System.Windows.Controls.GridSplitter splitter = new() { Width = 5, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
            System.Windows.Controls.Grid.SetRow(splitter, 1);
            System.Windows.Controls.Grid.SetColumn(splitter, 2); View.Children.Add(splitter);
            System.Windows.Controls.Grid.SetRow(PreviewView, 1);
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
            SyntaxCatalog.Resolve(FilePath, LanguageOverride).Id, FilePath, IsLargeFile ? "" : Editor.Document.GetText(0, Math.Min(65536, Editor.Document.TextLength)));
        Editor.Options.IndentationSize = options.Size;
        Editor.Options.ConvertTabsToSpaces = !options.UseTabs;
    }

    private void UpdateResourceMode()
    {
        if (!IsLargeFile && LargeFilePolicy.IsLarge(0, Editor.Document.TextLength, Editor.Document.LineCount))
        {
            IsLargeFile = true;
            ApplyResourcePreferences();
            Notify();
        }
    }

    private void ApplySyntax() => Editor.SyntaxHighlighting = IsLargeFile ? null : SyntaxCatalog.Resolve(FilePath, LanguageOverride).Definition;

    private void ApplyResourcePreferences()
    {
        EditorFactory.ApplyPreferences(Editor, _preferences.Current);
        Editor.Options.EnableHyperlinks = !IsLargeFile;
        Editor.Options.EnableEmailHyperlinks = !IsLargeFile;
        _scrolling.SetEnabled(IsLargeFile);
        if (IsLargeFile)
        {
            Editor.WordWrap = false;
            Editor.Options.ShowSpaces = false;
            Editor.Options.ShowTabs = false;
            Editor.Document.UndoStack.SizeLimit = 100;
        }
        _largeNotice.Visibility = IsLargeFile ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        Minimap.Visibility = !IsLargeFile && _preferences.Current.ShowMinimap ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        ApplySyntax();
        ApplyIndentation();
    }

    private void OnPreferencesChanged(object? sender, EventArgs e) => ApplyResourcePreferences();

    internal void CancelPendingScroll() => _scrolling.Cancel();

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public void Dispose()
    {
        _analysisTimer.Stop();
        _scrolling.Dispose();
        PdfView?.Dispose(); PreviewView?.Dispose();
        _preferences.Changed -= OnPreferencesChanged;
    }
}
