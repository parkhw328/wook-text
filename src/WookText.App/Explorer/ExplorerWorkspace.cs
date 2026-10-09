using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using WookText.Core;

namespace WookText.App.Explorer;

public sealed class ExplorerWorkspace : INotifyPropertyChanged, IDisposable
{
    private sealed class FolderScope(ExplorerNode node, CancellationToken parent) : IDisposable
    {
        public ExplorerNode Node { get; } = node;
        public CancellationTokenSource Cancellation { get; } = CancellationTokenSource.CreateLinkedTokenSource(parent);
        public FileSystemWatcher? Watcher { get; set; }
        public WorkspaceFolderState? PendingRestoration { get; set; }
        public void Dispose() { Cancellation.Cancel(); Watcher?.Dispose(); Cancellation.Dispose(); }
    }
    private readonly WorkspaceFiles _files = new();
    private readonly WorkspaceStateStore _store;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly Dictionary<string, FolderScope> _folders = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Before, string After)> _pendingRenames = [];
    private WorkspaceState _state;
    private CancellationTokenSource _scope = new();
    private CancellationTokenSource? _search;
    private Task? _refreshTask;
    private bool _refreshAgain, _disposed;
    private int _restoring;
    private Task _stateWriteTask = Task.CompletedTask;
    private WorkspaceState? _pendingState;
    private Exception? _stateWriteError;
    private WorkspaceFolderState[]? _expandedToRestore;
    private DateTime _lastRename;
    private ExplorerNode? _selected;
    private string? _lastRoot;
    private string _query = "", _notice = "폴더를 추가해 프로젝트를 탐색하세요.", _activeFile = "";
    public ObservableCollection<ExplorerNode> Nodes { get; } = [];
    public ObservableCollection<ExplorerNode> Matches { get; } = [];
    public ExplorerNode? SelectedNode
    {
        get => _selected;
        set { _selected = value; if (value is not null && _folders.ContainsKey(value.WorkspacePath)) _lastRoot = value.WorkspacePath; Notify(nameof(RootPath)); }
    }
    public string? RootPath => _lastRoot is not null && _folders.ContainsKey(_lastRoot) ? _lastRoot : Nodes.FirstOrDefault()?.FullPath;
    public string RootName => HasRoot ? $"작업 폴더  {Nodes.Count} / {WorkspaceStateStore.FolderLimit}" : "작업 폴더";
    public string RootTooltip => string.Join("\n", Nodes.Select(n => n.FullPath));
    public bool HasRoot => Nodes.Count > 0;
    public bool CanAddFolder => Nodes.Count < WorkspaceStateStore.FolderLimit;
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

    public async Task RestoreAsync()
    {
        WorkspaceFolderState[] folders = _state.Folders;
        CancellationToken token = _scope.Token;
        _restoring++;
        try { foreach (WorkspaceFolderState folder in folders) { if (_disposed || token.IsCancellationRequested) return; await AddFolderAsync(folder.Path, folder); } }
        finally { _restoring--; SaveState(); }
        if (!_disposed) UpdateSummary();
    }

    public Task OpenFolderAsync(string path) => AddFolderAsync(path, null);

    private async Task AddFolderAsync(string path, WorkspaceFolderState? saved)
    {
        CancellationToken opening = _scope.Token;
        try { path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { Report(ex.Message); return; }
        if (saved is null && !await Task.Run(() => Directory.Exists(path)))
        { Report("폴더를 찾거나 접근할 수 없습니다. 경로와 연결 상태를 확인하세요."); return; }
        if (_disposed || opening.IsCancellationRequested) return;
        if (_folders.TryGetValue(path, out FolderScope? existing))
        {
            existing.Node.IsExpanded = true; Select(existing.Node);
            RevealRequested?.Invoke(existing.Node);
            Report("이미 열린 작업 폴더입니다."); return;
        }
        if (!CanAddFolder) { Report("작업 폴더는 최대 10개입니다. 폴더 행의 ×로 닫은 뒤 추가하세요."); return; }
        string name = Path.GetFileName(path);
        ExplorerNode root = CreateNode(new(path, name.Length == 0 ? path : name, true), path);
        FolderScope folder = new(root, _scope.Token) { PendingRestoration = saved };
        _folders.Add(path, folder); Nodes.Add(root); _lastRoot = path;
        if (saved is null) Remember(path);
        NotifyRoots();
        CancellationToken token = folder.Cancellation.Token;
        _restoring++;
        try
        {
            await EnsureLoadedAsync(root);
            token.ThrowIfCancellationRequested();
            if (saved is not null && root.IsLoaded)
            {
                await RestoreExpandedAsync(saved, token);
                folder.PendingRestoration = null;
            }
            root.IsExpanded = saved?.IsExpanded ?? true;
            StartWatcher(folder);
        }
        catch (OperationCanceledException) { }
        finally { _restoring--; SaveState(); }
        if (token.IsCancellationRequested || _disposed) return;
        if (HasQuery) { SearchTask = SearchAsync(false); await SearchTask; } else UpdateSummary();
    }

    private ExplorerNode CreateNode(WorkspaceEntry entry, string root) => new(entry, root)
    {
        IsActive = string.Equals(entry.FullPath, _activeFile, StringComparison.OrdinalIgnoreCase),
        ExpansionChanged = node =>
        {
            if (node.IsExpanded) _ = EnsureLoadedAsync(node);
            if (_restoring == 0) { _saveTimer.Stop(); _saveTimer.Start(); }
        }
    };

    public Task EnsureLoadedAsync(ExplorerNode node)
    {
        if (node.IsLoaded || !node.IsDirectory || node.IsPlaceholder || !_folders.TryGetValue(node.WorkspacePath, out FolderScope? folder)) return Task.CompletedTask;
        return node.LoadingTask ??= LoadNodeAsync(node, folder.Cancellation.Token);
    }

    private async Task LoadNodeAsync(ExplorerNode node, CancellationToken token)
    {
        try
        {
            if (node.Entry.IsLink)
            {
                Reconcile(node.Children, [ExplorerNode.Message("연결 폴더 · Windows 탐색기에서 열기")]);
                node.IsLoaded = true; return;
            }
            DirectoryListing listing = await _files.ReadDirectoryAsync(node.FullPath, new(ShowHidden, HideGenerated), token);
            token.ThrowIfCancellationRequested();
            Dictionary<string, ExplorerNode> existing = node.Children.Where(n => !n.IsPlaceholder).ToDictionary(n => n.FullPath, StringComparer.Ordinal);
            List<ExplorerNode> children = listing.Entries.Select(entry => existing.TryGetValue(entry.FullPath, out ExplorerNode? previous) && previous.Entry == entry
                ? previous : CreateNode(entry, node.WorkspacePath)).ToList();
            if (listing.Truncated) children.Add(ExplorerNode.Message($"{WorkspaceFiles.DirectoryLimit:N0}개까지 표시 · 파일 찾기 이용"));
            Reconcile(node.Children, children);
            node.IsLoaded = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!token.IsCancellationRequested)
            {
                node.IsLoaded = false;
                Reconcile(node.Children, [ExplorerNode.Message("접근할 수 없습니다 · F5로 다시 시도")]);
                Report($"{node.Name} · 폴더에 접근할 수 없습니다.");
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
        if (_disposed || !HasRoot) return Task.CompletedTask;
        if (_refreshTask is { IsCompleted: false }) { _refreshAgain = true; return _refreshTask; }
        return _refreshTask = RefreshLoopAsync(_scope.Token);
    }

    private async Task RefreshLoopAsync(CancellationToken token)
    {
        do
        {
            _refreshAgain = false;
            ApplySettledRenames();
            ExplorerNode? selected = SelectedNode;
            foreach (ExplorerNode root in Nodes.ToArray())
            {
                if (token.IsCancellationRequested || _disposed) return;
                if (!_folders.TryGetValue(root.FullPath, out FolderScope? folder)) continue;
                CancellationToken folderToken = folder.Cancellation.Token;
                List<ExplorerNode> expanded = Walk(root.Children).Where(n => n.IsDirectory && n.IsExpanded).ToList();
                await LoadNodeAsync(root, folderToken);
                if (folderToken.IsCancellationRequested) continue;
                foreach (ExplorerNode collapsed in Walk(root.Children).Where(n => n.IsDirectory && !n.IsExpanded)) collapsed.IsLoaded = false;
                foreach (ExplorerNode node in expanded)
                {
                    if (folderToken.IsCancellationRequested) break;
                    if (FindIn(root.FullPath, node.FullPath) == node) await LoadNodeAsync(node, folderToken);
                }
                if (!folderToken.IsCancellationRequested && root.IsLoaded && folder.PendingRestoration is { } pending)
                {
                    await RestoreExpandedAsync(pending, folderToken);
                    folder.PendingRestoration = null;
                }
                if (!folderToken.IsCancellationRequested && folder.Watcher is null) StartWatcher(folder);
            }
            if (token.IsCancellationRequested || _disposed) return;
            if (_expandedToRestore is { } saved)
            {
                _expandedToRestore = null; _restoring++;
                try { foreach (WorkspaceFolderState folder in saved) await RestoreExpandedAsync(folder, token); }
                finally { _restoring--; }
            }
            if (selected is not null && FindIn(selected.WorkspacePath, selected.FullPath) is { } retained) Select(retained);
            MarkActive(_activeFile);
            if (HasQuery) { SearchTask = SearchAsync(false); await SearchTask; } else UpdateSummary();
        } while (_refreshAgain && !token.IsCancellationRequested && !_disposed);
    }

    public ExplorerNode? FindRoot(string path) => Nodes.Where(n => WorkspaceFiles.IsWithin(n.FullPath, path)).OrderByDescending(n => n.FullPath.Length).FirstOrDefault();
    public ExplorerNode? Find(string path) => FindRoot(path) is { } root ? FindIn(root.FullPath, path) : null;
    private ExplorerNode? FindIn(string root, string path) => _folders.TryGetValue(root, out FolderScope? folder)
        ? Walk([folder.Node]).FirstOrDefault(n => !n.IsPlaceholder && string.Equals(n.FullPath, path, StringComparison.OrdinalIgnoreCase)) : null;
    private static IEnumerable<ExplorerNode> Walk(IEnumerable<ExplorerNode> nodes)
    {
        foreach (ExplorerNode node in nodes)
        {
            yield return node;
            foreach (ExplorerNode child in Walk(node.Children)) yield return child;
        }
    }

    private async Task RestoreExpandedAsync(WorkspaceFolderState state, CancellationToken token)
    {
        foreach (string relative in state.ExpandedFolders.OrderBy(p => p.Count(c => c is '/' or '\\')))
        {
            if (token.IsCancellationRequested) return;
            ExplorerNode? node = FindIn(state.Path, Path.Combine(state.Path, relative));
            if (node is not { IsDirectory: true }) continue;
            node.IsExpanded = true; await EnsureLoadedAsync(node);
        }
    }

    public async Task<ExplorerNode?> RevealAsync(string? path)
    {
        if (path is null || FindRoot(path) is not { } root) { Report("현재 파일이 열린 작업 폴더 밖에 있습니다."); return null; }
        CancellationToken token = _folders[root.FullPath].Cancellation.Token;
        Query = ""; root.IsExpanded = true; await EnsureLoadedAsync(root);
        string relative = Path.GetRelativePath(root.FullPath, path), cursor = root.FullPath;
        ExplorerNode? found = root;
        foreach (string segment in relative == "." ? [] : relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (token.IsCancellationRequested) return null;
            cursor = Path.Combine(cursor, segment); found = FindIn(root.FullPath, cursor);
            if (found is null) { Report("숨김·생성 폴더 필터를 확인하거나 F5로 새로고침하세요."); return null; }
            if (found.IsDirectory && !string.Equals(cursor, path, StringComparison.OrdinalIgnoreCase))
            { found.IsExpanded = true; await EnsureLoadedAsync(found); }
        }
        if (token.IsCancellationRequested) return null;
        Select(found); RevealRequested?.Invoke(found); return found;
    }

    private void Select(ExplorerNode node)
    {
        if (SelectedNode is { } previous) previous.IsSelected = false;
        node.IsSelected = true; SelectedNode = node;
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
        if (!HasQuery || !HasRoot) { UpdateSummary(); return; }
        string[] roots = Nodes.Select(n => n.FullPath).ToArray();
        string query = Query;
        Notice = $"{roots.Length}개 폴더에서 찾는 중…";
        try
        {
            if (debounce) await Task.Delay(180, token);
            WorkspaceSearch result = await _files.SearchAsync(roots, query, new(ShowHidden, HideGenerated), token);
            token.ThrowIfCancellationRequested();
            foreach (WorkspaceEntry entry in result.Entries)
                if (FindRoot(entry.FullPath) is { } root) Matches.Add(CreateNode(entry, root.FullPath));
            Notice = result.Truncated ? $"{Matches.Count}개 표시 · 검색어를 더 입력하세요" : Matches.Count == 0 ? "일치하는 파일이 없습니다." : $"{Matches.Count}개 파일 · {roots.Length}개 폴더";
            if (result.SkippedFolders > 0) Notice += $" · 접근 불가 {result.SkippedFolders}개";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { if (!token.IsCancellationRequested) Report(ex.Message); }
    }

    public void CollapseAll() { foreach (ExplorerNode node in Walk(Nodes)) node.IsExpanded = false; SaveState(); }
    public void MoveFolder(string path, int offset)
    {
        if (!_folders.TryGetValue(path, out FolderScope? folder)) return;
        int index = Nodes.IndexOf(folder.Node), target = index + offset;
        if (target < 0 || target >= Nodes.Count) return;
        Nodes.Move(index, target); SaveState(); NotifyRoots();
    }

    public async Task SetOptionsAsync(bool showHidden, bool hideGenerated)
    {
        _state = _state with { ShowHidden = showHidden, HideGenerated = hideGenerated };
        Notify(nameof(ShowHidden)); Notify(nameof(HideGenerated));
        await RefreshAsync(); SaveState();
    }

    public async Task<string?> CreateAsync(string parent, string name, bool directory)
    {
        if (FindRoot(parent) is null) throw new IOException("작업 폴더 안에서 생성하세요.");
        string path = await Task.Run(() => WorkspaceFiles.Create(parent, name, directory));
        await RefreshAsync();
        ExplorerNode? parentNode = Find(parent);
        if (parentNode is { IsDirectory: true } && _folders.TryGetValue(parentNode.WorkspacePath, out FolderScope? folder))
        { parentNode.IsExpanded = true; await EnsureLoadedAsync(parentNode); await LoadNodeAsync(parentNode, folder.Cancellation.Token); }
        await RevealAsync(path); return path;
    }

    public async Task RenameAsync(ExplorerNode node, string name)
    {
        if (node.IsPlaceholder || FindRoot(node.FullPath) is null) throw new IOException("이름을 변경할 항목을 선택하세요.");
        if (node.IsDirectory && Nodes.Any(root => WorkspaceFiles.IsWithin(node.FullPath, root.FullPath)))
            throw new IOException("작업 폴더로 열린 디렉터리는 목록에서 닫은 뒤 이름을 변경하세요.");
        string before = node.FullPath;
        string after = await Task.Run(() => WorkspaceFiles.Rename(node.Entry, name));
        PathRenamed?.Invoke(before, after, node.IsDirectory);
        RetargetExpanded(before, after);
        await RefreshAsync(); await RevealAsync(after); SaveState();
        Notice = $"이름 변경 · {name}";
    }

    private void RetargetExpanded(string before, string after)
    {
        _expandedToRestore = (_expandedToRestore ?? CaptureFolders()).Select(folder => folder with
        {
            ExpandedFolders = folder.ExpandedFolders.Select(relative =>
            {
                string path = Path.Combine(folder.Path, relative);
                return Path.GetRelativePath(folder.Path, WorkspaceFiles.IsWithin(before, path) ? Path.Combine(after, Path.GetRelativePath(before, path)) : path);
            }).Where(relative => relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray()
        }).ToArray();
    }

    private void StartWatcher(FolderScope folder)
    {
        if (_disposed || !_folders.TryGetValue(folder.Node.FullPath, out FolderScope? current) || !ReferenceEquals(folder, current)) return;
        if (folder.Watcher is not null || !folder.Node.IsLoaded) return;
        try
        {
            FileSystemWatcher watcher = new(folder.Node.FullPath) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName };
            folder.Watcher = watcher;
            watcher.Created += (_, e) => QueueChange(folder, e);
            watcher.Deleted += (_, e) => QueueChange(folder, e);
            watcher.Renamed += (_, e) => QueueChange(folder, e);
            watcher.Error += (_, _) => QueueChange(folder, null);
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { folder.Watcher?.Dispose(); folder.Watcher = null; Report("자동 갱신을 시작하지 못했습니다. F5로 새로고침하세요."); }
    }

    private void QueueChange(FolderScope folder, FileSystemEventArgs? change)
    {
        if (_disposed || _dispatcher.HasShutdownStarted) return;
        _dispatcher.BeginInvoke(() =>
        {
            if (_disposed || !_folders.TryGetValue(folder.Node.FullPath, out FolderScope? current) || !ReferenceEquals(folder, current)) return;
            bool Ignore(string path) => HideGenerated && Path.GetRelativePath(folder.Node.FullPath, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).SkipLast(1).Any(WorkspaceFiles.IsGeneratedFolder);
            if (change is RenamedEventArgs rename)
            {
                if (Ignore(rename.FullPath) && Ignore(rename.OldFullPath)) return;
                var move = (rename.OldFullPath, rename.FullPath);
                if (!_pendingRenames.Contains(move)) _pendingRenames.Add(move);
                _lastRename = DateTime.UtcNow;
            }
            else if (change is not null && Ignore(change.FullPath)) return;
            if (change is null) { folder.Watcher?.Dispose(); folder.Watcher = null; }
            _refreshTimer.Stop(); _refreshTimer.Start();
        });
    }

    private void ApplySettledRenames()
    {
        if (DateTime.UtcNow - _lastRename < TimeSpan.FromMilliseconds(200)) return;
        foreach ((string before, string initialAfter) in _pendingRenames)
        {
            string after = initialAfter;
            foreach ((string nextBefore, string nextAfter) in _pendingRenames)
                if (string.Equals(after, nextBefore, StringComparison.Ordinal) && !string.Equals(after, nextAfter, StringComparison.Ordinal)) after = nextAfter;
            // Ignore temporary ReplaceFile renames; only retarget settled moves.
            bool samePath = string.Equals(before, after, StringComparison.OrdinalIgnoreCase);
            if ((!File.Exists(after) && !Directory.Exists(after)) || (!samePath && (File.Exists(before) || Directory.Exists(before)))) continue;
            if (FindRoot(before) is not null && FindRoot(after) is not null)
            { PathRenamed?.Invoke(before, after, true); RetargetExpanded(before, after); }
        }
        _pendingRenames.Clear();
    }

    private void UpdateSummary()
    {
        if (!HasRoot) { Notice = "폴더 추가 · Ctrl+Shift+O"; return; }
        int unavailable = Nodes.Count(n => !n.IsLoaded);
        Notice = $"{Nodes.Count} / 10 작업 폴더" + (unavailable > 0 ? $" · 접근 불가 {unavailable}개 · F5로 재시도" : HideGenerated ? " · 빌드·의존성 폴더 제외" : " · 생성 폴더 포함");
    }

    private WorkspaceFolderState[] CaptureFolders() => Nodes.Select(root => new WorkspaceFolderState
    {
        Path = root.FullPath,
        IsExpanded = root.IsExpanded,
        ExpandedFolders = !root.IsLoaded && _folders[root.FullPath].PendingRestoration is { } pending ? pending.ExpandedFolders
            : Walk(root.Children).Where(n => n.IsDirectory && n.IsExpanded).Select(n => n.RelativePath).Take(200).ToArray()
    }).ToArray();

    private void SaveState(bool force = false)
    {
        if (_disposed || !force && _restoring > 0) return;
        _state = _state with { Folders = CaptureFolders() };
        _pendingState = _state;
        if (_stateWriteTask.IsCompleted) _stateWriteTask = WriteStateAsync();
    }

    private async Task WriteStateAsync()
    {
        while (_pendingState is { } state)
        {
            _pendingState = null;
            try { await Task.Run(() => _store.Save(state)); _stateWriteError = null; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { _stateWriteError = ex; Notice = "작업 폴더 상태를 저장하지 못했습니다. " + ex.Message; }
        }
    }

    public async Task FlushStateAsync()
    {
        _saveTimer.Stop(); SaveState(force: true); await _stateWriteTask;
        if (_stateWriteError is { } error) throw new IOException("작업 폴더 목록을 보관하지 못했습니다.", error);
    }
    private void Remember(string path)
    {
        _state = _state with { RecentFolders = new[] { path }.Concat(_state.RecentFolders).Distinct(StringComparer.OrdinalIgnoreCase).Take(WorkspaceStateStore.FolderLimit).ToArray() };
        Notify(nameof(RecentFolders));
    }
    public void ClearRecentFolders() { _state = _state with { RecentFolders = [] }; SaveState(); Notify(nameof(RecentFolders)); }

    public void CloseFolder(string? path = null)
    {
        path ??= RootPath;
        if (path is null || !_folders.Remove(path, out FolderScope? folder)) return;
        folder.Dispose(); Nodes.Remove(folder.Node); Remember(path);
        if (string.Equals(SelectedNode?.WorkspacePath, path, StringComparison.OrdinalIgnoreCase)) SelectedNode = null;
        NotifyRoots(); SaveState(); SearchTask = SearchAsync(false);
        if (!HasQuery) Report($"작업 폴더 닫음 · {folder.Node.Name} · 열린 문서는 유지됩니다.");
    }

    public void CloseAllFolders()
    {
        foreach (ExplorerNode node in Nodes.Reverse()) Remember(node.FullPath);
        _scope.Cancel(); _scope.Dispose(); _scope = new();
        foreach (FolderScope folder in _folders.Values) folder.Dispose();
        _folders.Clear(); Nodes.Clear(); SelectedNode = null; _lastRoot = null;
        _refreshTimer.Stop(); _refreshTask = null; _refreshAgain = false;
        _pendingRenames.Clear(); _expandedToRestore = null;
        Query = ""; Matches.Clear(); NotifyRoots(); SaveState();
        Report("모든 작업 폴더를 닫았습니다. 열린 문서는 유지됩니다.");
    }

    private void NotifyRoots()
    {
        foreach (string name in new[] { nameof(RootPath), nameof(RootName), nameof(RootTooltip), nameof(HasRoot), nameof(CanAddFolder) }) Notify(name);
    }
    public void Report(string text) => Notice = text;
    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _refreshTimer.Stop(); _saveTimer.Stop(); _scope.Cancel();
        foreach (FolderScope folder in _folders.Values) folder.Dispose();
        _folders.Clear(); _scope.Dispose(); _search?.Cancel(); _search?.Dispose();
    }
}
