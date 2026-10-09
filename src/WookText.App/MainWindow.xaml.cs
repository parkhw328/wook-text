using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using Microsoft.Win32;
using WookText.Core;

namespace WookText.App;

public partial class MainWindow : Window
{
    private readonly TextFileService _files = new();
    private int _untitledCount;
    private bool _busy;
    private bool _allowClose;
    private readonly EditorPreferencesService _preferences;
    private readonly Func<EditorDocument, MessageBoxResult>? _confirmClose;
    private readonly SessionCoordinator _session;
    private bool _restoringSession = true;
    private bool _closing;
    private Task? _initialization;
    public Task ClosingCompletion { get; private set; } = Task.CompletedTask;
    public string? SessionFilePath => _session.StoragePath;
    private bool _layoutReady;
    private bool _applyingPreferences;
    private bool _focusMode;
    public ObservableCollection<EditorDocument> Documents { get; } = [];
    public EditorDocument? ActiveDocument => DocumentTabs.SelectedItem as EditorDocument;
    public Explorer.ExplorerView Explorer => WorkspaceExplorer;

    public MainWindow(EditorPreferencesService? preferences = null, Func<EditorDocument, MessageBoxResult>? confirmClose = null)
    {
        _preferences = preferences ?? new EditorPreferencesService();
        _confirmClose = confirmClose;
        string settingsName = Path.GetFileNameWithoutExtension(_preferences.StoragePath);
        string sessionDirectory = Path.Combine(Path.GetDirectoryName(_preferences.StoragePath)!, settingsName == "settings" ? "sessions" : settingsName + ".sessions");
        _session = new(sessionDirectory, CaptureSession, () => _preferences.Current.RememberSession);
        InitializeComponent();
        NativeWindowTheme.Attach(this);
        DataContext = this;
        WorkspaceExplorer.Initialize(new WorkspaceStateStore(Path.ChangeExtension(_preferences.StoragePath, ".workspace.json")),
            path => RunAsync(() => OpenFileAsync(path)), OpenFolderAsync, () => ActiveDocument?.FilePath, OnExplorerPathRenamed);
        Bind(ApplicationCommands.New, () => { NewDocument(); return Task.CompletedTask; });
        Bind(ApplicationCommands.Open, OpenDialogAsync);
        Bind(ApplicationCommands.Save, async () => { if (ActiveDocument is { } doc) await SaveDocumentAsync(doc); });
        Bind(EditorCommands.SaveAs, async () => { if (ActiveDocument is { } doc) await SaveDocumentAsync(doc, true); });
        Bind(EditorCommands.CloseTab, async () => { if (ActiveDocument is { } doc) await CloseDocumentAsync(doc); });
        BindExplorer(EditorCommands.OpenFolder, OpenFolderAsync);
        BindExplorer(EditorCommands.FindFile, async () =>
        {
            EnsureExplorerVisible();
            if (!WorkspaceExplorer.Workspace.HasRoot) await OpenFolderAsync();
            WorkspaceExplorer.FocusSearch();
        });
        BindExplorer(EditorCommands.RevealFile, async () =>
        {
            EnsureExplorerVisible();
            if (!WorkspaceExplorer.Workspace.HasRoot && ActiveDocument?.FilePath is { } path) await OpenWorkspaceAsync(Path.GetDirectoryName(path)!);
            await WorkspaceExplorer.RevealCurrentAsync();
            WorkspaceExplorer.FocusTree();
        });
        Bind(EditorCommands.Compare, () => { new CompareWindow(_preferences) { Owner = this }.Show(); return Task.CompletedTask; });
        Bind(EditorCommands.ToggleSidebar, () => { ToggleSidebar(); return Task.CompletedTask; });
        Bind(EditorCommands.ToggleFocus, () => { ToggleFocus(); return Task.CompletedTask; });
        Bind(EditorCommands.Preferences, () => { ShowPreferences(); return Task.CompletedTask; });
        Bind(EditorCommands.ZoomIn, () => { ChangeFontSize(1); return Task.CompletedTask; });
        Bind(EditorCommands.ZoomOut, () => { ChangeFontSize(-1); return Task.CompletedTask; });
        Bind(EditorCommands.ResetZoom, () => { _preferences.Update(_preferences.Current with { FontSize = EditorPreferences.DefaultFontSize }); return Task.CompletedTask; });
        Bind(ApplicationCommands.Find, () => { ShowSearch(false); return Task.CompletedTask; });
        Bind(ApplicationCommands.Replace, () => { ShowSearch(true); return Task.CompletedTask; });
        PreviewKeyDown += OnWindowKeyDown;
        _preferences.Changed += OnPreferencesChanged;
        Closed += (_, _) => { WorkspaceExplorer.Dispose(); _session.Dispose(); _preferences.Changed -= OnPreferencesChanged; foreach (EditorDocument document in Documents) document.Dispose(); };
        Loaded += async (_, _) => await InitializeSessionAsync();
        _session.Warning += warning => Status.Text = warning;
        _layoutReady = true;
        ApplyLayoutPreferences();
        NewDocument();
        FillLanguageMenu(LanguageMenu.Items);
        if (_preferences.LastWarning is { } warning) Status.Text = warning;
    }

    private void Bind(ICommand command, Func<Task> action) => CommandBindings.Add(new CommandBinding(command,
        async (_, e) => { e.Handled = true; await RunAsync(action); },
        (_, e) => { e.CanExecute = !_busy; e.Handled = true; }));

    private void BindExplorer(ICommand command, Func<Task> action) => CommandBindings.Add(new CommandBinding(command,
        async (_, e) => { e.Handled = true; await action(); },
        (_, e) => { e.CanExecute = !_busy; e.Handled = true; }));

    public EditorDocument NewDocument()
    {
        string name;
        do { name = $"새 문서 {++_untitledCount}"; } while (Documents.Any(d => d.UntitledName == name));
        return AddDocument(name);
    }

    private EditorDocument AddDocument(string name)
    {
        EditorDocument document = new(name, _preferences);
        document.Editor.TextArea.Caret.PositionChanged += (_, _) => { UpdateStatus(); SessionChanged(); };
        document.Editor.TextChanged += (_, _) => { UpdateStatus(); SessionChanged(); };
        document.PropertyChanged += (_, _) => { UpdateStatus(); WorkspaceExplorer.Workspace.MarkActive(ActiveDocument?.FilePath); SessionChanged(); };
        Documents.Add(document);
        DocumentTabs.SelectedItem = document;
        document.Editor.Focus();
        SessionChanged();
        return document;
    }

    private void SessionChanged() { if (!_restoringSession && !_closing) _session.Changed(); }

    private Func<SessionState> CaptureSession()
    {
        Guid? active = ActiveDocument?.SessionId;
        Func<SessionDocumentState>[] documents = Documents.Select(d => d.CaptureSession()).ToArray();
        return () => new SessionState { ActiveDocumentId = active, Documents = documents.Select(capture => capture()).ToArray() };
    }

    public Task InitializeSessionAsync() => _initialization ??= RestoreSessionAsync();

    private async Task RestoreSessionAsync()
    {
        _busy = true; Root.IsEnabled = false;
        try
        {
            SessionState state = await _session.InitializeAsync();
            if (state.Documents.Length > 0)
            {
                foreach (EditorDocument previous in Documents) previous.Dispose();
                Documents.Clear();
                int recovered = 0;
                foreach (SessionDocumentState saved in state.Documents)
                {
                    LoadedTextFile? current = null;
                    if (!saved.IsModified && saved.FilePath is { } path)
                    {
                        try
                        {
                            try { current = await _files.ReadAsync(path); }
                            catch (DecoderFallbackException) { current = await _files.ReadAsync(path, saved.Encoding); }
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { recovered++; }
                    }
                    AddDocument(saved.Name).RestoreSession(saved, current);
                }
                DocumentTabs.SelectedItem = Documents.FirstOrDefault(d => d.SessionId == state.ActiveDocumentId) ?? Documents[0];
                Status.Text = $"이전 작업 복원 · {Documents.Count}개 문서" + (recovered > 0 ? $" · 읽을 수 없는 원본 {recovered}개는 보관 내용으로 복구" : "");
            }
            if (_session.LastWarning is { } warning) Status.Text = warning;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { Status.Text = "작업 보관 폴더를 열지 못했습니다. " + ex.Message; }
        finally
        {
            _restoringSession = false; _busy = false; Root.IsEnabled = true;
            _session.Changed(); UpdateStatus(); CommandManager.InvalidateRequerySuggested();
        }
    }

    public async Task OpenFileAsync(string path)
    {
        await InitializeSessionAsync();
        try
        {
            string fullPath = Path.GetFullPath(path);
            EditorDocument? existing = Documents.FirstOrDefault(d => string.Equals(d.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                DocumentTabs.SelectedItem = existing;
                existing.Editor.Focus();
                Status.Text = $"열림 · {existing.Name}";
                UpdateStatus();
                return;
            }
            LoadedTextFile file;
            try { file = await _files.ReadAsync(fullPath); }
            catch (DecoderFallbackException)
            {
                if (!AppDialogs.ConfirmEncoding(this, fullPath)) return;
                file = await _files.ReadAsync(fullPath, TextFileEncoding.Korean949);
            }

            EditorDocument? empty = Documents.FirstOrDefault(d => d.FilePath is null && !d.Editor.IsModified && d.Editor.Text.Length == 0);
            EditorDocument document = empty ?? NewDocument();
            document.Load(file);
            DocumentTabs.SelectedItem = document;
            document.Editor.Focus();
            Status.Text = $"열림 · {document.Name}";
            UpdateStatus();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowError("파일을 열 수 없습니다", ex);
        }
    }

    private async Task OpenDialogAsync()
    {
        OpenFileDialog dialog = new() { Title = "wText · 파일 열기", Multiselect = true, Filter = "모든 파일 (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true)
            foreach (string path in dialog.FileNames) await OpenFileAsync(path);
    }

    private async Task<bool> SaveDocumentAsync(EditorDocument document, bool saveAs = false)
    {
        string? path = document.FilePath;
        string? fingerprint = document.Fingerprint;
        if (saveAs || path is null)
        {
            SaveFileDialog dialog = new()
            {
                Title = "wText · 다른 이름으로 저장",
                FileName = document.Name,
                Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
                DefaultExt = ".txt",
                OverwritePrompt = true
            };
            if (path is not null) dialog.InitialDirectory = Path.GetDirectoryName(path);
            if (dialog.ShowDialog(this) != true) return false;
            path = Path.GetFullPath(dialog.FileName);
            EditorDocument? other = Documents.FirstOrDefault(d => d != document && string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (other is not null)
            {
                AppDialogs.Alert(this, "다른 탭에서 사용 중인 파일입니다", "해당 탭에서 저장하거나 다른 파일 이름을 선택해 주세요.");
                return false;
            }
            // Even Save As must retain conflict detection when selecting the original path.
            if (!string.Equals(path, document.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                try { fingerprint = File.Exists(path) ? await TextFileService.GetFingerprintAsync(path) : null; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { ShowError("저장할 수 없습니다", ex); return false; }
            }
        }

        try
        {
            string newFingerprint = await _files.SaveAsync(path, document.Editor.Text, document.Encoding, fingerprint);
            document.MarkSaved(path, newFingerprint);
            Status.Text = $"저장됨 · {document.Name}";
            UpdateStatus();
            return true;
        }
        catch (EncoderFallbackException)
        {
            AppDialogs.Alert(this, "인코딩을 변경해 주세요", "현재 인코딩으로 저장할 수 없는 문자가 있습니다.\n파일 메뉴에서 ‘UTF-8로 변환’한 뒤 다시 저장해 주세요. 원본은 그대로 유지됩니다.");
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { ShowError("저장할 수 없습니다", ex); return false; }
    }

    private async Task<bool> ConfirmCloseAsync(EditorDocument document)
    {
        if (!document.Editor.IsModified) return true;
        MessageBoxResult choice = _confirmClose?.Invoke(document) ?? AppDialogs.ConfirmSave(this, document.Name, document.FilePath);
        return choice == MessageBoxResult.No || choice == MessageBoxResult.Yes && await SaveDocumentAsync(document);
    }

    private async Task CloseDocumentAsync(EditorDocument document)
    {
        if (!await ConfirmCloseAsync(document)) return;
        int index = Documents.IndexOf(document);
        EditorDocument? previous = ActiveDocument;
        Documents.Remove(document);
        document.Dispose();
        if (Documents.Count == 0) NewDocument();
        else
        {
            // The sidebar selection binding can clear TabControl selection on removal.
            DocumentTabs.SelectedItem = previous != document && previous is not null && Documents.Contains(previous)
                ? previous : Documents[Math.Clamp(index, 0, Documents.Count - 1)];
            ActiveDocument?.Editor.Focus();
        }
        SessionChanged();
        // Persist explicit tab removal now, so a discarded buffer cannot return after a restart.
        await _session.FlushAsync(_preferences.Current.RememberSession);
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_busy || _closing) return;
        _closing = true;
        ClosingCompletion = FinishClosingAsync();
    }

    private async Task FinishClosingAsync()
    {
        // WPF forbids Close() inside an in-progress Closing event. Even No/clean paths must yield.
        await Dispatcher.Yield(DispatcherPriority.Background);
        _busy = true; Root.IsEnabled = false;
        try
        {
            if (!_preferences.Current.RememberSession)
                foreach (EditorDocument document in Documents.ToArray())
                    if (!await ConfirmCloseAsync(document)) return;
            Status.Text = "작업을 보관하고 종료하는 중…";
            await _session.FlushAsync(_preferences.Current.RememberSession);
            await WorkspaceExplorer.Restoration;
            await WorkspaceExplorer.Workspace.FlushStateAsync();
            _allowClose = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Status.Text = "종료 취소 · 작업을 보관하지 못했습니다. 파일로 저장하거나 다시 시도하세요.";
            Status.ToolTip = ex.Message;
        }
        finally { _busy = false; _closing = false; Root.IsEnabled = true; CommandManager.InvalidateRequerySuggested(); }
        if (_allowClose) Close();
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        Root.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { ShowError("작업을 완료할 수 없습니다", ex); }
        finally { _busy = false; Root.IsEnabled = true; CommandManager.InvalidateRequerySuggested(); }
    }

    private void ShowError(string title, Exception exception)
    {
        Status.Text = title;
        if (exception is FileConflictException)
            AppDialogs.Alert(this, "원본 파일을 확인해 주세요", exception.Message);
        else AppDialogs.Alert(this, title, "파일 경로와 접근 권한을 확인한 뒤 다시 시도해 주세요.", exception.Message);
    }

    private async Task OpenFolderAsync()
    {
        OpenFolderDialog dialog = new() { Title = "wText · 작업 폴더 추가 (최대 10개)", Multiselect = true };
        if (WorkspaceExplorer.Workspace.RootPath is { } root) dialog.InitialDirectory = root;
        if (dialog.ShowDialog(this) == true)
            foreach (string path in dialog.FolderNames) await OpenWorkspaceAsync(path);
    }

    public async Task OpenWorkspaceAsync(string path)
    {
        EnsureExplorerVisible();
        await WorkspaceExplorer.Restoration;
        await WorkspaceExplorer.Workspace.OpenFolderAsync(path);
    }

    private void EnsureExplorerVisible()
    {
        _focusMode = false;
        _preferences.Update(_preferences.Current with { SidebarVisible = true, ExplorerExpanded = true });
    }

    private void OnExplorerPathRenamed(string before, string after, bool directory)
    {
        foreach (EditorDocument document in Documents)
        {
            if (document.FilePath is not { } path) continue;
            if (string.Equals(path, before, StringComparison.OrdinalIgnoreCase)) document.RetargetPath(after);
            else if (directory && WorkspaceFiles.IsWithin(before, path)) document.RetargetPath(Path.Combine(after, Path.GetRelativePath(before, path)));
        }
        WorkspaceExplorer.Workspace.MarkActive(ActiveDocument?.FilePath);
    }

    private void ShowSearch(bool replace)
    {
        SearchPanel.Visibility = Visibility.Visible;
        ReplacePanel.Visibility = replace ? Visibility.Visible : Visibility.Collapsed;
        if (ActiveDocument?.Editor.SelectedText is { Length: > 0 } selected && !selected.Contains('\n')) FindBox.Text = selected;
        FindBox.Focus();
        FindBox.SelectAll();
    }

    private void FindNext()
    {
        if (ActiveDocument?.Editor is not { } editor || FindBox.Text.Length == 0) return;
        int index = TextAnalysis.FindNext(editor.Text, FindBox.Text, editor.SelectionStart + editor.SelectionLength, MatchCase.IsChecked == true);
        if (index < 0) { Status.Text = "일치하는 내용이 없습니다."; return; }
        editor.Select(index, FindBox.Text.Length);
        editor.TextArea.Caret.Offset = index + FindBox.Text.Length;
        editor.ScrollToLine(editor.Document.GetLineByOffset(index).LineNumber);
        Status.Text = "일치하는 내용을 찾았습니다.";
    }

    private void OnFindNext(object sender, RoutedEventArgs e) => FindNext();
    private void OnReplaceOne(object sender, RoutedEventArgs e)
    {
        if (ActiveDocument?.Editor is not { } editor || FindBox.Text.Length == 0) return;
        StringComparison comparison = MatchCase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (string.Equals(editor.SelectedText, FindBox.Text, comparison)) editor.SelectedText = ReplaceBox.Text;
        FindNext();
    }

    private void OnReplaceAll(object sender, RoutedEventArgs e)
    {
        if (ActiveDocument?.Editor is not { } editor || FindBox.Text.Length == 0) return;
        StringComparison comparison = MatchCase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        List<int> offsets = [];
        string text = editor.Text;
        int start = 0;
        while (start <= text.Length)
        {
            int found = text.IndexOf(FindBox.Text, start, comparison);
            if (found < 0) break;
            offsets.Add(found);
            start = found + FindBox.Text.Length;
        }
        using (editor.Document.RunUpdate())
            foreach (int offset in offsets.AsEnumerable().Reverse()) editor.Document.Replace(offset, FindBox.Text.Length, ReplaceBox.Text);
        Status.Text = $"{offsets.Count:N0}개 항목을 바꿨습니다. Ctrl+Z로 취소할 수 있습니다.";
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { FindNext(); e.Handled = true; }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (WorkspaceExplorer.IsKeyboardFocusWithin && WorkspaceExplorer.Workspace.HasQuery) return;
            if (SearchPanel.Visibility == Visibility.Visible) { OnHideSearch(sender, e); e.Handled = true; }
            else if (_focusMode) { ToggleFocus(); e.Handled = true; }
        }
        if (e.Key == Key.F3) { FindNext(); e.Handled = true; }
        if (e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Documents.Count > 0)
        {
            int delta = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1;
            DocumentTabs.SelectedIndex = (DocumentTabs.SelectedIndex + delta + Documents.Count) % Documents.Count;
            e.Handled = true;
        }
    }

    private void OnHideSearch(object sender, RoutedEventArgs e) { SearchPanel.Visibility = Visibility.Collapsed; ActiveDocument?.Editor.Focus(); }
    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != DocumentTabs) return;
        if (ActiveDocument is { } document && WrapMenu is not null) WrapMenu.IsChecked = document.Editor.WordWrap;
        WorkspaceExplorer?.Workspace?.MarkActive(ActiveDocument?.FilePath);
        UpdateStatus();
        SessionChanged();
    }

    private void UpdateStatus()
    {
        if (ActiveDocument is not { } document || FormatStatus is null || CaretStatus is null) return;
        Title = $"{document.Header} — wText";
        CaretStatus.Text = $"Ln {document.Editor.TextArea.Caret.Line}, Col {document.Editor.TextArea.Caret.Column}";
        FormatStatus.Text = $"{document.Encoding.DisplayName()}  ·  {document.LineEndingDescription}";
        LanguageButton.Content = document.LanguageName;
        FontSizeButton.Content = $"{_preferences.Current.FontSize:0.#} px";
    }

    private async void OnCloseTabButton(object sender, RoutedEventArgs e)
    { if (sender is Button { DataContext: EditorDocument document }) await RunAsync(() => CloseDocumentAsync(document)); }
    private void OnUndo(object sender, RoutedEventArgs e) => ActiveDocument?.Editor.Undo();
    private void OnRedo(object sender, RoutedEventArgs e) => ActiveDocument?.Editor.Redo();
    private void OnWordWrap(object sender, RoutedEventArgs e) => _preferences.Update(_preferences.Current with { WordWrap = WrapMenu.IsChecked });
    private void OnLineNumbers(object sender, RoutedEventArgs e) => _preferences.Update(_preferences.Current with { ShowLineNumbers = LineNumbersMenu.IsChecked });
    private void OnWhitespace(object sender, RoutedEventArgs e) => _preferences.Update(_preferences.Current with { ShowWhitespace = WhitespaceMenu.IsChecked });
    private void OnConvertEncoding(object sender, RoutedEventArgs e) { ActiveDocument?.ConvertToUtf8(); UpdateStatus(); }
    private void OnExit(object sender, RoutedEventArgs e) => Close();
    private void OnAbout(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
    private void OnLicenses(object sender, RoutedEventArgs e) => new LicenseWindow { Owner = this }.ShowDialog();
    private void OnOpenRepository(object sender, RoutedEventArgs e) => AppInfo.OpenRepository(this);

    private void ShowPreferences()
    {
        PreferencesWindow dialog = new(_preferences.Current) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is { } preferences) _preferences.Update(preferences);
    }

    private void ChangeFontSize(int delta) => _preferences.Update(_preferences.Current with { FontSize = _preferences.Current.FontSize + delta });

    private void ToggleSidebar()
    {
        if (_focusMode) { _focusMode = false; _preferences.Update(_preferences.Current with { SidebarVisible = true }); }
        else _preferences.Update(_preferences.Current with { SidebarVisible = !_preferences.Current.SidebarVisible });
        ActiveDocument?.Editor.Focus();
    }

    private void ToggleFocus()
    {
        _focusMode = !_focusMode;
        ApplyLayoutPreferences();
        ActiveDocument?.Editor.Focus();
    }

    private void OnPreferencesChanged(object? sender, EventArgs e)
    {
        ApplyLayoutPreferences();
        UpdateStatus();
        if (_preferences.LastWarning is { } warning) Status.Text = warning;
        SessionChanged();
    }

    private void ApplyLayoutPreferences()
    {
        if (!_layoutReady) return;
        _applyingPreferences = true;
        try
        {
            EditorPreferences preferences = _preferences.Current;
            bool showSidebar = preferences.SidebarVisible && !_focusMode;
            Sidebar.Visibility = SidebarSplitter.Visibility = showSidebar ? Visibility.Visible : Visibility.Collapsed;
            SidebarColumn.MinWidth = showSidebar ? 170 : 0;
            SidebarColumn.Width = new GridLength(showSidebar ? preferences.SidebarWidth : 0);
            SplitterColumn.Width = new GridLength(showSidebar ? 4 : 0);
            SidebarMenu.IsChecked = preferences.SidebarVisible;
            FocusMenu.IsChecked = _focusMode;
            WrapMenu.IsChecked = preferences.WordWrap;
            LineNumbersMenu.IsChecked = preferences.ShowLineNumbers;
            WhitespaceMenu.IsChecked = preferences.ShowWhitespace;
            OpenDocumentsSection.IsExpanded = preferences.OpenDocumentsExpanded;
            ExplorerSection.IsExpanded = preferences.ExplorerExpanded;
            MainHeader.Visibility = MainMenu.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;
            FocusToolbar.Visibility = _focusMode ? Visibility.Visible : Visibility.Collapsed;
            HeaderRow.Height = new GridLength(_focusMode ? 34 : 62);
            StatusRow.Height = new GridLength(_focusMode ? 0 : 32);
            StatusBar.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;
            SidebarToggleButton.ToolTip = showSidebar ? "사이드바 접기 · Ctrl+B" : "사이드바 펼치기 · Ctrl+B";
        }
        finally { _applyingPreferences = false; }
    }

    private void OnSidebarResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_focusMode && _preferences.Current.SidebarVisible)
            _preferences.Update(_preferences.Current with { SidebarWidth = SidebarColumn.ActualWidth });
    }

    private void OnSidebarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (OpenDocumentsList is not null) OpenDocumentsList.MaxHeight = Math.Clamp((e.NewSize.Height - 350) * 0.5, 68, 160);
        WorkspaceExplorer?.KeepSelectionVisible();
    }

    private void OnSidebarSectionChanged(object sender, RoutedEventArgs e)
    {
        if (!_layoutReady || _applyingPreferences || (e.Source != OpenDocumentsSection && e.Source != ExplorerSection)) return;
        _preferences.Update(_preferences.Current with
        {
            OpenDocumentsExpanded = OpenDocumentsSection.IsExpanded,
            ExplorerExpanded = ExplorerSection.IsExpanded
        });
    }

    private void FillLanguageMenu(ItemCollection items)
    {
        items.Clear();
        MenuItem automatic = new() { Header = "자동 감지", IsCheckable = true, IsChecked = ActiveDocument?.LanguageOverride is null };
        automatic.Click += (_, _) => ActiveDocument?.SetLanguage(null);
        items.Add(automatic);
        items.Add(new Separator());
        foreach (SyntaxLanguage language in SyntaxCatalog.Languages)
        {
            MenuItem item = new() { Header = language.Label, IsCheckable = true, IsChecked = ActiveDocument?.LanguageOverride == language.Id };
            item.Click += (_, _) => ActiveDocument?.SetLanguage(language.Id);
            items.Add(item);
        }
    }

    private void OnLanguageMenuOpened(object sender, RoutedEventArgs e)
    { if (e.Source == LanguageMenu) FillLanguageMenu(LanguageMenu.Items); }

    private void OnChooseLanguage(object sender, RoutedEventArgs e)
    {
        ContextMenu menu = new() { PlacementTarget = LanguageButton, Placement = PlacementMode.Top };
        FillLanguageMenu(menu.Items);
        LanguageButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void OnFilesDragOver(object sender, DragEventArgs e)
    { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void OnFilesDropped(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        e.Handled = true;
        foreach (string directory in paths.Where(Directory.Exists)) await OpenWorkspaceAsync(directory);
        await RunAsync(async () => { foreach (string path in paths.Where(File.Exists)) await OpenFileAsync(path); });
    }
}
