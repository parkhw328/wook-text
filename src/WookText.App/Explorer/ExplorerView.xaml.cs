using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WookText.Core;

namespace WookText.App.Explorer;

public partial class ExplorerView : UserControl, IDisposable
{
    public ExplorerWorkspace Workspace { get; private set; } = null!;
    private Func<string, Task> _openFile = _ => Task.CompletedTask;
    private Func<Task> _openFolder = () => Task.CompletedTask;
    private Func<string?> _currentFile = () => null;
    private DispatcherOperation? _pendingReveal;
    public Task Restoration { get; private set; } = Task.CompletedTask;

    public ExplorerView() => InitializeComponent();

    public void Initialize(WorkspaceStateStore store, Func<string, Task> openFile, Func<Task> openFolder, Func<string?> currentFile, Action<string, string, bool> renamed)
    {
        Workspace = new(store);
        DataContext = Workspace;
        _openFile = openFile; _openFolder = openFolder; _currentFile = currentFile;
        Workspace.PathRenamed += renamed;
        Workspace.RevealRequested += async node =>
        {
            await Dispatcher.Yield(DispatcherPriority.Loaded);
            BringNodeIntoView(node);
        };
        Loaded += OnFirstLoaded;
    }

    private async void OnFirstLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnFirstLoaded;
        Restoration = Workspace.RestoreAsync();
        await Restoration;
    }

    public void FocusSearch() { FilterBox.Focus(); FilterBox.SelectAll(); }
    public async Task RevealCurrentAsync() => await Workspace.RevealAsync(_currentFile());
    public void FocusTree()
    {
        if (Workspace.SelectedNode is { } selected) BringNodeIntoView(selected, focus: true);
        else FileTree.Focus();
    }

    public void KeepSelectionVisible()
    {
        _pendingReveal?.Abort();
        _pendingReveal = Dispatcher.InvokeAsync(() =>
        {
            if (IsVisible && Workspace is { HasQuery: false, SelectedNode: { } selected }) BringNodeIntoView(selected);
        }, DispatcherPriority.Loaded);
    }

    private async Task OpenAsync(ExplorerNode? node)
    {
        if (node is not { IsActionable: true }) return;
        if (node.IsDirectory) { node.IsExpanded = !node.IsExpanded; if (node.IsExpanded) await Workspace.EnsureLoadedAsync(node); }
        else await _openFile(node.FullPath);
    }

    private void OnSelectedNodeChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    { Workspace.SelectedNode = e.NewValue as ExplorerNode; }
    private void OnSearchSelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (SearchResults.SelectedItem is ExplorerNode node) Workspace.SelectedNode = node; }
    private async void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        TreeViewItem? item = Ancestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (item?.DataContext is ExplorerNode { IsDirectory: false, IsActionable: true } node)
        { e.Handled = true; await OpenAsync(node); }
    }
    private async void OnSearchDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is ExplorerNode node)
        { e.Handled = true; await OpenAsync(node); }
    }

    private async void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await OpenAsync(FileTree.SelectedItem as ExplorerNode); }
        else if (e.Key == Key.F2) { e.Handled = true; RenameSelected(); }
        else if (e.Key == Key.F5) { e.Handled = true; await Workspace.RefreshAsync(); }
        else if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { e.Handled = true; CopyPath(Workspace.SelectedNode, false); }
    }
    private async void OnResultsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await OpenAsync(SearchResults.SelectedItem as ExplorerNode); }
        else if (e.Key == Key.Escape) { e.Handled = true; Workspace.Query = ""; FocusSearch(); }
        else if (e.Key == Key.F2) { e.Handled = true; RenameSelected(); }
        else if (e.Key == Key.F5) { e.Handled = true; await Workspace.RefreshAsync(); }
        else if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { e.Handled = true; CopyPath(Workspace.SelectedNode, false); }
    }
    private async void OnFilterKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; Workspace.Query = ""; FileTree.Focus(); }
        else if (e.Key == Key.Down && Workspace.Matches.Count > 0)
        { e.Handled = true; SearchResults.SelectedIndex = 0; ((ListBoxItem?)SearchResults.ItemContainerGenerator.ContainerFromIndex(0))?.Focus(); }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await Workspace.SearchTask;
            await OpenAsync(SearchResults.SelectedItem as ExplorerNode ?? Workspace.Matches.FirstOrDefault());
        }
    }

    private void OnTreeRightClick(object sender, MouseButtonEventArgs e)
    {
        if (Ancestor<TreeViewItem>(e.OriginalSource as DependencyObject) is { DataContext: ExplorerNode node } item)
        { item.IsSelected = true; item.Focus(); Workspace.SelectedNode = node; }
        else
        {
            if (Workspace.SelectedNode is { } previous) previous.IsSelected = false;
            Workspace.SelectedNode = null;
        }
        PopulateContextMenu(FileTree.ContextMenu, Workspace.SelectedNode);
    }
    private void OnResultsRightClick(object sender, MouseButtonEventArgs e)
    {
        if (Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject) is { DataContext: ExplorerNode node } item)
        { item.IsSelected = true; item.Focus(); Workspace.SelectedNode = node; }
        else { SearchResults.SelectedIndex = -1; Workspace.SelectedNode = null; }
        PopulateContextMenu(SearchResults.ContextMenu, Workspace.SelectedNode);
    }
    private void OnTreeContextMenu(object sender, ContextMenuEventArgs e) => PopulateContextMenu(FileTree.ContextMenu, Workspace.SelectedNode);
    private void OnResultsContextMenu(object sender, ContextMenuEventArgs e) => PopulateContextMenu(SearchResults.ContextMenu, Workspace.SelectedNode);

    private void PopulateContextMenu(ContextMenu menu, ExplorerNode? node)
    {
        menu.Items.Clear();
        if (!Workspace.HasRoot) { Add(menu, "폴더 열기…", _openFolder); return; }
        if (node is { IsActionable: true })
        {
            Add(menu, node.IsDirectory ? "펼치기 / 접기" : "파일 열기", () => OpenAsync(node), "Enter");
            Add(menu, "이름 바꾸기…", () => { RenameSelected(); return Task.CompletedTask; }, "F2");
            menu.Items.Add(new Separator());
        }
        Add(menu, "새 파일…", () => { CreateItem(false); return Task.CompletedTask; });
        Add(menu, "새 폴더…", () => { CreateItem(true); return Task.CompletedTask; });
        menu.Items.Add(new Separator());
        Add(menu, "상대 경로 복사", () => { CopyPath(node, true); return Task.CompletedTask; });
        Add(menu, "전체 경로 복사", () => { CopyPath(node, false); return Task.CompletedTask; }, "Ctrl+Shift+C");
        Add(menu, "Windows 탐색기에서 보기", () => { OpenInWindows(node); return Task.CompletedTask; });
        menu.Items.Add(new Separator());
        Add(menu, "새로고침", Workspace.RefreshAsync, "F5");
    }

    private void OnMore(object sender, RoutedEventArgs e)
    {
        ContextMenu menu = new() { PlacementTarget = MoreButton };
        Add(menu, "폴더 열기…", _openFolder, "Ctrl+Shift+O");
        if (Workspace.RecentFolders.Count > 0)
        {
            MenuItem recent = new() { Header = "최근 작업 폴더" };
            foreach (string path in Workspace.RecentFolders)
            {
                StackPanel label = new() { Width = 280 };
                label.Children.Add(new TextBlock { Text = Path.GetFileName(path) is { Length: > 0 } name ? name : path, TextTrimming = TextTrimming.CharacterEllipsis });
                label.Children.Add(new TextBlock { Text = path, FontSize = 11, Foreground = EditorFactory.Brush("#9C9990"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 0) });
                MenuItem item = new() { Header = label, ToolTip = path };
                System.Windows.Automation.AutomationProperties.SetName(item, path);
                item.Click += async (_, _) => await Workspace.OpenFolderAsync(path);
                recent.Items.Add(item);
            }
            menu.Items.Add(recent);
        }
        if (Workspace.HasRoot)
        {
            Add(menu, "Windows 탐색기에서 보기", () => { OpenInWindows(null); return Task.CompletedTask; });
            Add(menu, "작업 폴더 닫기", () => { Workspace.CloseFolder(); return Task.CompletedTask; });
        }
        menu.Items.Add(new Separator());
        MenuItem hidden = new() { Header = "숨김 항목 표시", IsCheckable = true, IsChecked = Workspace.ShowHidden };
        hidden.Click += async (_, _) => await Workspace.SetOptionsAsync(hidden.IsChecked, Workspace.HideGenerated);
        menu.Items.Add(hidden);
        MenuItem generated = new()
        {
            Header = "빌드·의존성 폴더 숨기기",
            IsCheckable = true,
            IsChecked = Workspace.HideGenerated,
            ToolTip = ".git, node_modules, bin, obj, .vs, .idea, .next, .cache, .venv, __pycache__"
        };
        generated.Click += async (_, _) => await Workspace.SetOptionsAsync(Workspace.ShowHidden, generated.IsChecked);
        menu.Items.Add(generated);
        MoreButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void Add(ContextMenu menu, string header, Func<Task> action, string shortcut = "")
    {
        MenuItem item = new() { Header = header, InputGestureText = shortcut };
        item.Click += async (_, _) =>
        {
            try { await action(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { Workspace.Report(ex.Message); }
        };
        menu.Items.Add(item);
    }

    private void CreateItem(bool directory)
    {
        if (Workspace.RootPath is not { } root) return;
        ExplorerNode? selected = Workspace.SelectedNode is { IsPlaceholder: false } valid ? valid : null;
        string parent = selected is null ? root : selected.IsDirectory ? selected.FullPath : Path.GetDirectoryName(selected.FullPath)!;
        if (selected is { Entry.IsLink: true }) { Workspace.Report("연결 폴더는 Windows 탐색기에서 관리하세요."); return; }
        NameDialog dialog = new(directory ? "새 폴더" : "새 파일", parent, "", async name =>
        {
            string? path = await Workspace.CreateAsync(parent, name, directory);
            if (!directory && path is not null) await _openFile(path);
        })
        { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
    }

    private void RenameSelected()
    {
        if (Workspace.SelectedNode is not { IsActionable: true } node) return;
        NameDialog dialog = new("이름 바꾸기", Path.GetDirectoryName(node.FullPath)!, node.Name, name => Workspace.RenameAsync(node, name), selectStem: !node.IsDirectory) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
    }

    private void CopyPath(ExplorerNode? node, bool relative)
    {
        string? path = node is { IsActionable: true } ? node.FullPath : Workspace.RootPath;
        if (path is null) return;
        try
        {
            Clipboard.SetText(relative && Workspace.RootPath is { } root ? Path.GetRelativePath(root, path) : path);
            Workspace.Report(relative ? "상대 경로를 복사했습니다." : "전체 경로를 복사했습니다.");
        }
        catch (ExternalException) { Workspace.Report("클립보드에 복사하지 못했습니다. 다시 시도하세요."); }
    }

    private void OpenInWindows(ExplorerNode? node)
    {
        string? path = node?.FullPath ?? Workspace.RootPath;
        if (path is null) return;
        try
        {
            ProcessStartInfo start = new("explorer.exe") { UseShellExecute = true };
            if (node is { IsDirectory: false }) start.Arguments = "/select,\"" + path + "\"";
            else start.ArgumentList.Add(path);
            Process.Start(start);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { Workspace.Report("Windows 탐색기를 열지 못했습니다. " + ex.Message); }
    }

    private async void OnOpenFolder(object sender, RoutedEventArgs e) => await _openFolder();
    private void OnNewFile(object sender, RoutedEventArgs e) => CreateItem(false);
    private void OnNewFolder(object sender, RoutedEventArgs e) => CreateItem(true);
    private async void OnRefresh(object sender, RoutedEventArgs e) => await Workspace.RefreshAsync();
    private void OnCollapseAll(object sender, RoutedEventArgs e) => Workspace.CollapseAll();
    private async void OnReveal(object sender, RoutedEventArgs e) => await RevealCurrentAsync();
    private void OnClearFilter(object sender, RoutedEventArgs e) { Workspace.Query = ""; FocusSearch(); }

    private void BringNodeIntoView(ExplorerNode target, bool focus = false)
    {
        if (Workspace.RootPath is null || !WorkspaceFiles.IsWithin(Workspace.RootPath, target.FullPath)) return;
        ItemsControl parent = FileTree;
        string path = Workspace.RootPath;
        foreach (string segment in target.RelativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            path = Path.Combine(path, segment);
            ExplorerNode? node = parent.Items.OfType<ExplorerNode>().FirstOrDefault(n => string.Equals(n.FullPath, path, StringComparison.OrdinalIgnoreCase));
            if (node is null) return;
            parent.UpdateLayout();
            TreeViewItem? container = parent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
            if (container is null)
            {
                ExplorerPanel? panel = VisualChildren<ExplorerPanel>(parent).FirstOrDefault();
                panel?.RevealIndex(parent.Items.IndexOf(node));
                parent.UpdateLayout();
                container = parent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
            }
            if (container is null) return;
            parent = container;
        }
        if (parent is TreeViewItem item) { item.BringIntoView(); item.IsSelected = true; if (focus) item.Focus(); }
    }

    private static T? Ancestor<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T found) return found;
            child = child is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child);
        }
        return null;
    }
    private static IEnumerable<T> VisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) yield return found;
            foreach (T descendant in VisualChildren<T>(child)) yield return descendant;
        }
    }
    public void Dispose() { _pendingReveal?.Abort(); Workspace?.Dispose(); }
}

public sealed class ExplorerPanel : VirtualizingStackPanel
{
    public void RevealIndex(int index) { if (index >= 0) BringIndexIntoView(index); }
}
