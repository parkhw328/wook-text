using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using WookText.Core;

namespace WookText.App.Explorer;

public sealed class ExplorerWorkspace : INotifyPropertyChanged, IDisposable
{
    private readonly WorkspaceFiles _files = new();
    private readonly WorkspaceStateStore _store;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private WorkspaceState _state;
    private ExplorerNode? _root;
    private CancellationTokenSource _scope = new();
    private CancellationTokenSource? _search;
    private FileSystemWatcher? _watcher;
    private Task? _refreshTask;
    private int _refreshQueued;
    private bool _restoreAfterRefresh;
    private bool _refreshAgain;
    private bool _disposed, _restoring;
    private Task _stateWriteTask = Task.CompletedTask;
    private WorkspaceState? _pendingState;
    private readonly List<(string Before, string After)> _pendingRenames = [];
    private DateTime _lastRename;
    private string _query = "", _notice = "폴더를 열어 프로젝트를 탐색하세요.", _activeFile = "";
    public ObservableCollection<ExplorerNode> Nodes => _root?.Children ?? _empty;
    private readonly ObservableCollection<ExplorerNode> _empty = [];
    public ObservableCollection<ExplorerNode> Matches { get; } = [];
    public ExplorerNode? SelectedNode { get; set; }
    public string? RootPath => _root?.FullPath;
    public string RootName => _root?.Name ?? "작업 폴더";
    public bool HasRoot => _root is not null;
    public bool HasQuery => _query.Trim().Length > 0;
    public bool ShowHidden => _state.ShowHidden;
    public bool HideGenerated => _state.HideGenerated;
    public IReadOnlyList<string> RecentFolders => _state.RecentFolders;
    public string Notice { get => _notice; private set { _notice = value; Notify(nameof(Notice)); } }
    public string Query { get => _query; set { if (_query == value) return; _query = value; Notify(nameof(Query)); Notify(nameof(HasQuery)); SearchTask = SearchAsync(); } }
    public Task SearchTask { get; private set; } = Task.CompletedTask;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<ExplorerNode>? RevealRequested;
    public event Action<string, string, bool>? PathRenamed;

    public ExplorerWorkspace(WorkspaceStateStore store)
    {
        _store = store;
        _state = store.Load(out string? warning);
        if (warning is not null) Notice = warning;
        _refreshTimer.Tick += async (_, _) => { _refreshTimer.Stop(); await RefreshAsync(); };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveState(); };
    }

    public Task RestoreAsync() => _state.RootPath is { } path ? OpenFolderAsync(path, restore: true) : Task.CompletedTask;

    public async Task OpenFolderAsync(string path, bool restore = false)
    {
        try { path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { Report(ex.Message); return; }
        _scope.Cancel(); _scope.Dispose(); _scope = new();
        _search?.Cancel(); _watcher?.Dispose(); _watcher = null;
        _refreshTimer.Stop(); _saveTimer.Stop();
        _refreshTask = null;
        _restoreAfterRefresh = false;
        _refreshAgain = false;
        _pendingRenames.Clear();
        CancellationToken token = _scope.Token;
        string name = Path.GetFileName(path);
        _root = CreateNode(new WorkspaceEntry(path, name.Length == 0 ? path : name, true), path);
        _state = _state with
        {
            RootPath = path,
            ExpandedFolders = restore || string.Equals(path, _state.RootPath, StringComparison.OrdinalIgnoreCase) ? _state.ExpandedFolders : [],
            RecentFolders = new[] { path }.Concat(_state.RecentFolders).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToArray()
        };
        Query = ""; Matches.Clear(); SelectedNode = null;
        Notify(nameof(Nodes)); Notify(nameof(RootPath)); Notify(nameof(RootName)); Notify(nameof(HasRoot));
        Notice = "폴더를 불러오는 중…";
        _restoring = true;
        try
        {
            await LoadNodeAsync(_root, token);
            token.ThrowIfCancellationRequested();
            await RestoreExpandedAsync(token);
            StartWatcher(path);
            UpdateSummary();
            SaveState();
        }
        catch (OperationCanceledException) { }
        finally { if (!token.IsCancellationRequested) _restoring = false; }
    }

    private ExplorerNode CreateNode(WorkspaceEntry entry, string root) => new(entry, root)
    {
        IsActive = string.Equals(entry.FullPath, _activeFile, StringComparison.OrdinalIgnoreCase),
        ExpansionChanged = node =>
        {
            if (node.IsExpanded) _ = EnsureLoadedAsync(node);
            if (!_restoring) { _saveTimer.Stop(); _saveTimer.Start(); }
        }
    };

    public Task EnsureLoadedAsync(ExplorerNode node)
    {
        if (node.IsLoaded || !node.IsDirectory || node.IsPlaceholder) return Task.CompletedTask;
        return node.LoadingTask ??= LoadNodeAsync(node, _scope.Token);
    }

    private async Task LoadNodeAsync(ExplorerNode node, CancellationToken token)
    {
        try
        {
            if (node.Entry.IsLink)
            {
                Reconcile(node.Children, [ExplorerNode.Message("연결 폴더 · Windows 탐색기에서 열기")]);
                node.IsLoaded = true;
                return;
            }
            DirectoryListing listing = await _files.ReadDirectoryAsync(node.FullPath, new(ShowHidden, HideGenerated), token);
            token.ThrowIfCancellationRequested();
            Dictionary<string, ExplorerNode> existing = node.Children.Where(n => !n.IsPlaceholder).ToDictionary(n => n.FullPath, StringComparer.Ordinal);
            List<ExplorerNode> children = listing.Entries.Select(entry => existing.TryGetValue(entry.FullPath, out ExplorerNode? previous) && previous.Entry == entry
                ? previous : CreateNode(entry, RootPath!)).ToList();
            if (listing.Truncated) children.Add(ExplorerNode.Message($"{WorkspaceFiles.DirectoryLimit:N0}개까지 표시 · 파일 찾기 이용"));
            Reconcile(node.Children, children);
            node.IsLoaded = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!token.IsCancellationRequested)
            {
                Reconcile(node.Children, [ExplorerNode.Message("읽을 수 없습니다 · F5로 다시 시도")]);
                Notice = "폴더를 읽을 수 없습니다. " + ex.Message;
            }
        }
        finally { node.LoadingTask = null; }
    }

    private static void Reconcile(ObservableCollection<ExplorerNode> current, IReadOnlyList<ExplorerNode> next)
    {
        HashSet<ExplorerNode> wanted = next.ToHashSet();
        for (int i = current.Count - 1; i >= 0; i--) if (!wanted.Contains(current[i])) current.RemoveAt(i);
        for (int i = 0; i < next.Count; i++)
        {
            if (i < current.Count && ReferenceEquals(current[i], next[i])) continue;
            int old = current.IndexOf(next[i]);
            if (old >= 0) current.Move(old, i); else current.Insert(i, next[i]);
        }
    }

    public Task RefreshAsync()
    {
        if (_disposed || _root is null) return Task.CompletedTask;
        if (_refreshTask is { IsCompleted: false }) { _refreshAgain = true; return _refreshTask; }
        return _refreshTask = RefreshLoopAsync(_scope.Token);
    }

    private async Task RefreshLoopAsync(CancellationToken token)
    {
        do
        {
            _refreshAgain = false;
            await RefreshCoreAsync(token);
        } while (_refreshAgain && !token.IsCancellationRequested && !_disposed);
    }

    private async Task RefreshCoreAsync(CancellationToken token)
    {
        if (_root is null) return;
        ExplorerNode root = _root;
        ApplySettledRenames();
        string? selected = SelectedNode?.FullPath;
        List<ExplorerNode> expanded = Walk(root.Children).Where(n => n.IsDirectory && n.IsExpanded).ToList();
        await LoadNodeAsync(root, token);
        foreach (ExplorerNode collapsed in Walk(root.Children).Where(n => n.IsDirectory && !n.IsExpanded)) collapsed.IsLoaded = false;
        foreach (ExplorerNode node in expanded)
        {
            if (token.IsCancellationRequested) return;
            if (Find(node.FullPath) == node) await LoadNodeAsync(node, token);
        }
        if (token.IsCancellationRequested) return;
        if (_restoreAfterRefresh)
        {
            _restoreAfterRefresh = false;
            _restoring = true;
            try { await RestoreExpandedAsync(token); }
            finally { _restoring = false; }
        }
        if (selected is not null && Find(selected) is { } retained) { retained.IsSelected = true; SelectedNode = retained; }
        MarkActive(_activeFile);
        if (HasQuery) { SearchTask = SearchAsync(debounce: false); await SearchTask; }
        else UpdateSummary();
    }

    public ExplorerNode? Find(string path) => Walk(Nodes).FirstOrDefault(n => !n.IsPlaceholder && string.Equals(n.FullPath, path, StringComparison.OrdinalIgnoreCase));
    private static IEnumerable<ExplorerNode> Walk(IEnumerable<ExplorerNode> nodes)
    {
        foreach (ExplorerNode node in nodes)
        {
            yield return node;
            foreach (ExplorerNode child in Walk(node.Children)) yield return child;
        }
    }

    private async Task RestoreExpandedAsync(CancellationToken token)
    {
        foreach (string relative in _state.ExpandedFolders.OrderBy(p => p.Count(c => c is '/' or '\\')))
        {
            if (token.IsCancellationRequested) return;
            ExplorerNode? node = Find(Path.Combine(RootPath!, relative));
            if (node is not { IsDirectory: true }) continue;
            node.IsExpanded = true;
            await EnsureLoadedAsync(node);
        }
    }

    public async Task<ExplorerNode?> RevealAsync(string? path)
    {
        if (RootPath is null || path is null || !WorkspaceFiles.IsWithin(RootPath, path))
        { Notice = "현재 파일이 작업 폴더 밖에 있습니다."; return null; }
        CancellationToken token = _scope.Token;
        Query = "";
        string relative = Path.GetRelativePath(RootPath, path);
        string cursor = RootPath;
        ExplorerNode? found = null;
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (token.IsCancellationRequested) return null;
            cursor = Path.Combine(cursor, segment);
            found = Find(cursor);
            if (found is null) { Notice = "숨김·생성 폴더 필터를 확인하거나 F5로 새로고침하세요."; return null; }
            if (found.IsDirectory && !string.Equals(cursor, path, StringComparison.OrdinalIgnoreCase))
            { found.IsExpanded = true; await EnsureLoadedAsync(found); }
        }
        if (!token.IsCancellationRequested && found is not null)
        {
            if (SelectedNode is { } previous) previous.IsSelected = false;
            found.IsSelected = true; SelectedNode = found;
            RevealRequested?.Invoke(found);
        }
        return token.IsCancellationRequested ? null : found;
    }

    public void MarkActive(string? path)
    {
        if (string.Equals(_activeFile, path ?? "", StringComparison.OrdinalIgnoreCase)) return;
        _activeFile = path ?? "";
        foreach (ExplorerNode node in Walk(Nodes).Concat(Matches)) node.IsActive = string.Equals(node.FullPath, _activeFile, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SearchAsync(bool debounce = true)
    {
        _search?.Cancel(); _search?.Dispose();
        _search = CancellationTokenSource.CreateLinkedTokenSource(_scope.Token);
        CancellationToken token = _search.Token;
        Matches.Clear();
        if (!HasQuery || RootPath is null) { UpdateSummary(); return; }
        Notice = "파일을 찾는 중…";
        try
        {
            if (debounce) await Task.Delay(180, token);
            WorkspaceSearch result = await _files.SearchAsync(RootPath, Query, new(ShowHidden, HideGenerated), token);
            token.ThrowIfCancellationRequested();
            foreach (WorkspaceEntry entry in result.Entries) Matches.Add(CreateNode(entry, RootPath));
            Notice = result.Truncated ? $"{Matches.Count}개 표시 · 검색어를 더 입력하세요" : Matches.Count == 0 ? "일치하는 파일이 없습니다." : $"{Matches.Count}개 파일 · Enter로 열기";
            if (result.SkippedFolders > 0) Notice += $" · 접근 불가 {result.SkippedFolders}개";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { if (!token.IsCancellationRequested) Report(ex.Message); }
    }

    public void CollapseAll()
    {
        foreach (ExplorerNode node in Walk(Nodes)) node.IsExpanded = false;
        SaveState();
    }

    public async Task SetOptionsAsync(bool showHidden, bool hideGenerated)
    {
        _state = _state with { ShowHidden = showHidden, HideGenerated = hideGenerated };
        Notify(nameof(ShowHidden)); Notify(nameof(HideGenerated));
        await RefreshAsync();
        SaveState();
    }

    public async Task<string?> CreateAsync(string parent, string name, bool directory)
    {
        if (RootPath is null || !WorkspaceFiles.IsWithin(RootPath, parent)) throw new IOException("작업 폴더 안에서 생성하세요.");
        string path = await Task.Run(() => WorkspaceFiles.Create(parent, name, directory));
        await RefreshAsync();
        ExplorerNode? parentNode = Find(parent);
        if (parentNode is { IsDirectory: true }) { parentNode.IsExpanded = true; await EnsureLoadedAsync(parentNode); await LoadNodeAsync(parentNode, _scope.Token); }
        await RevealAsync(path);
        return path;
    }

    public async Task RenameAsync(ExplorerNode node, string name)
    {
        if (node.IsPlaceholder || RootPath is null || !WorkspaceFiles.IsWithin(RootPath, node.FullPath)) throw new IOException("이름을 변경할 항목을 선택하세요.");
        string before = node.FullPath;
        string[] expanded = Walk(Nodes).Where(n => n.IsDirectory && n.IsExpanded).Select(n => n.FullPath).ToArray();
        string after = await Task.Run(() => WorkspaceFiles.Rename(node.Entry, name));
        PathRenamed?.Invoke(before, after, node.IsDirectory);
        _state = _state with
        {
            ExpandedFolders = expanded.Select(p => Path.GetRelativePath(RootPath,
            node.IsDirectory && WorkspaceFiles.IsWithin(before, p) ? Path.Combine(after, Path.GetRelativePath(before, p)) : p)).ToArray()
        };
        await RefreshAsync();
        _restoring = true;
        try { await RestoreExpandedAsync(_scope.Token); }
        finally { _restoring = false; }
        await RevealAsync(after);
        SaveState();
        Notice = $"이름 변경 · {name}";
    }

    private void StartWatcher(string path)
    {
        try
        {
            _watcher = new FileSystemWatcher(path) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName };
            _watcher.Created += OnFileSystemChanged; _watcher.Deleted += OnFileSystemChanged; _watcher.Renamed += OnFileSystemRenamed;
            _watcher.Error += (_, _) => ScheduleRefresh();
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { Notice = "자동 갱신을 시작하지 못했습니다. F5로 새로고침하세요."; }
    }

    private bool IgnoreChange(string path) => RootPath is { } root && HideGenerated &&
        Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).SkipLast(1).Any(WorkspaceFiles.IsGeneratedFolder);

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    { if (!IgnoreChange(e.FullPath)) ScheduleRefresh(); }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        if (_disposed || _dispatcher.HasShutdownStarted || IgnoreChange(e.FullPath) && IgnoreChange(e.OldFullPath)) return;
        _dispatcher.BeginInvoke(() =>
        {
            if (_disposed || !ReferenceEquals(sender, _watcher) || RootPath is not { } root) return;
            _pendingRenames.Add((e.OldFullPath, e.FullPath));
            _lastRename = DateTime.UtcNow;
            ScheduleRefresh();
        });
    }

    private void ApplySettledRenames()
    {
        if (RootPath is not { } root || DateTime.UtcNow - _lastRename < TimeSpan.FromMilliseconds(200)) return;
        foreach ((string before, string initialAfter) in _pendingRenames)
        {
            string after = initialAfter;
            for (int i = 0; i < _pendingRenames.Count; i++)
            {
                (string nextBefore, string nextAfter) = _pendingRenames[i];
                if (string.Equals(after, nextBefore, StringComparison.Ordinal) && !string.Equals(after, nextAfter, StringComparison.Ordinal)) after = nextAfter;
            }
            // ReplaceFile emits transient old -> ~RF...TMP renames. Only follow a real,
            // settled move, never redirect an editor buffer into an atomic-save temporary file.
            bool samePath = string.Equals(before, after, StringComparison.OrdinalIgnoreCase);
            if ((!File.Exists(after) && !Directory.Exists(after)) || (!samePath && (File.Exists(before) || Directory.Exists(before)))) continue;
            if (WorkspaceFiles.IsWithin(root, before) && WorkspaceFiles.IsWithin(root, after))
            {
                PathRenamed?.Invoke(before, after, true);
                if (Find(before) is { IsDirectory: true })
                {
                    _state = _state with
                    {
                        ExpandedFolders = Walk(Nodes).Where(n => n.IsDirectory && n.IsExpanded).Select(n =>
                        Path.GetRelativePath(root, WorkspaceFiles.IsWithin(before, n.FullPath)
                            ? Path.Combine(after, Path.GetRelativePath(before, n.FullPath)) : n.FullPath)).ToArray()
                    };
                    _restoreAfterRefresh = true;
                }
            }
        }
        _pendingRenames.Clear();
    }
    private void ScheduleRefresh()
    {
        if (_disposed || _dispatcher.HasShutdownStarted || Interlocked.Exchange(ref _refreshQueued, 1) != 0) return;
        _dispatcher.BeginInvoke(() => { Interlocked.Exchange(ref _refreshQueued, 0); if (_disposed) return; _refreshTimer.Stop(); _refreshTimer.Start(); });
    }

    private void UpdateSummary()
    {
        if (!HasRoot) { Notice = "폴더를 열어 프로젝트를 탐색하세요."; return; }
        if (_root is { IsLoaded: false }) return;
        int count = Nodes.Count(n => !n.IsPlaceholder);
        Notice = count == 0 ? "빈 폴더 · 새 파일을 만들어 시작하세요." : $"{count}개 항목" + (HideGenerated ? " · 빌드·의존성 폴더 제외" : " · 생성 폴더 포함");
    }

    private void SaveState()
    {
        _state = _state with { RootPath = RootPath, ExpandedFolders = Walk(Nodes).Where(n => n.IsDirectory && n.IsExpanded).Select(n => n.RelativePath).Take(200).ToArray() };
        _pendingState = _state;
        if (_stateWriteTask.IsCompleted) _stateWriteTask = WriteStateAsync();
    }

    private async Task WriteStateAsync()
    {
        while (_pendingState is { } state)
        {
            _pendingState = null;
            try { await Task.Run(() => _store.Save(state)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Notice = "작업 폴더 상태를 저장하지 못했습니다. " + ex.Message; }
        }
    }

    public async Task FlushStateAsync()
    {
        _saveTimer.Stop();
        SaveState();
        await _stateWriteTask;
    }

    public void CloseFolder()
    {
        _scope.Cancel(); _search?.Cancel(); _watcher?.Dispose(); _watcher = null;
        _refreshTimer.Stop(); _root = null; SelectedNode = null; Query = ""; Matches.Clear();
        Notify(nameof(Nodes)); Notify(nameof(RootPath)); Notify(nameof(RootName)); Notify(nameof(HasRoot));
        SaveState(); UpdateSummary();
    }

    public void Report(string text) => Notice = text;
    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _refreshTimer.Stop(); _saveTimer.Stop();
        _scope.Cancel(); _scope.Dispose(); _search?.Cancel(); _search?.Dispose(); _watcher?.Dispose();
    }
}
