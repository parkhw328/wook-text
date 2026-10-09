using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WookText.App;
using WookText.App.Explorer;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class MultiFolderChecks
{
    internal static async Task RunAsync(string output)
    {
        string sandbox = Path.Combine(output, "fixtures", "multi-" + Guid.NewGuid().ToString("N"));
        string[] names = ["web-client", "api-server", "design-system", "documentation", "tools", "shared", "examples", "integration", "deployment", "한국어 프로젝트", "eleventh"];
        string[] roots = names.Select(n => Path.Combine(sandbox, n)).ToArray();
        foreach (string root in roots)
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            await File.WriteAllTextAsync(Path.Combine(root, "src", "index.js"), "// " + Path.GetFileName(root) + "\nexport const editor = 'wText';\n");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# " + Path.GetFileName(root) + "\n");
        }
        string settings = Path.Combine(sandbox, "settings.json");
        await File.WriteAllTextAsync(Path.ChangeExtension(settings, ".workspace.json"),
            JsonSerializer.Serialize(new { RootPath = roots[0], ExpandedFolders = new[] { "src" }, RecentFolders = new[] { roots[0] } }));
        MainWindow window = new(new EditorPreferencesService(new PreferencesStore(settings)));
        Program.ShowOffscreen(window);
        await Program.WaitForIdleAsync(window);
        await window.Explorer.Restoration;
        ExplorerWorkspace workspace = window.Explorer.Workspace;
        Check(workspace.Nodes.Count == 1 && workspace.Find(Path.Combine(roots[0], "src")) is { IsExpanded: true }, "A 0.4 workspace must migrate with its expanded paths intact.");
        await window.OpenWorkspaceAsync(roots[1]);
        await window.OpenWorkspaceAsync(roots[2]);
        string firstFile = Path.Combine(roots[0], "src", "index.js");
        string secondFile = Path.Combine(roots[1], "src", "index.js");
        await window.OpenFileAsync(firstFile);
        await window.OpenFileAsync(secondFile);
        EditorDocument editing = window.ActiveDocument!;
        editing.Editor.AppendText("\n// API와 화면 프로젝트를 함께 편집합니다.\nconst workspace = {\n    name: 'wText',\n    version: '1.0.0',\n    maxFolders: 10,\n    rememberSession: true\n};\n");
        string buffer = editing.Editor.Text;
        await workspace.RevealAsync(secondFile);
        workspace.Nodes[2].IsExpanded = false;
        await Program.RenderAsync(window, Path.Combine(output, "workspaces.png"), 1240, 800);
        workspace.Query = "index.js"; await workspace.SearchTask;
        Check(workspace.Matches.Count == 3 && workspace.Matches.Select(n => n.WorkspacePath).Distinct().Count() == 3, "File search must include every open root with distinct owning paths.");
        await Program.RenderAsync(window, Path.Combine(output, "workspaces-search.png"), 1240, 800);
        workspace.Query = "";
        string created = (await workspace.CreateAsync(roots[1], "server-only.txt", false))!;
        Check(File.Exists(created) && !File.Exists(Path.Combine(roots[0], "server-only.txt")), "File creation must use the selected workspace.");
        await workspace.RenameAsync(workspace.Find(secondFile)!, "server.js");
        secondFile = Path.Combine(roots[1], "src", "server.js");
        Check(editing.FilePath == secondFile && editing.Editor.Text == buffer && editing.Editor.IsModified, "Renaming in the second root must preserve its unsaved editor.");
        string changeA = Path.Combine(roots[0], "external-a.txt"), changeB = Path.Combine(roots[1], "external-b.txt");
        await File.WriteAllTextAsync(changeA, "a"); await File.WriteAllTextAsync(changeB, "b");
        await WaitAsync(() => workspace.Find(changeA) is not null && workspace.Find(changeB) is not null);
        for (int i = 3; i < 10; i++) await window.OpenWorkspaceAsync(roots[i]);
        Check(workspace.Nodes.Count == 10 && !workspace.CanAddFolder, "Ten roots must remain open together.");
        await window.OpenWorkspaceAsync(roots[10]);
        Check(workspace.Nodes.Count == 10 && workspace.Notice.Contains("최대 10개", StringComparison.Ordinal), "An eleventh root must be rejected without replacing an existing root.");
        await window.OpenWorkspaceAsync(roots[0].ToUpperInvariant() + "\\");
        Check(workspace.Nodes.Count == 10, "Case and trailing separators must not duplicate an already open root.");

        workspace.CollapseAll();
        await workspace.RevealAsync(roots[1]);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        TreeView tree = (TreeView)window.Explorer.FindName("FileTree");
        ExplorerNode closing = workspace.Nodes.Single(n => n.FullPath == roots[1]);
        TreeViewItem row = ExplorerChecks.FindRow(tree, closing)!;
        Button close = VisualChildren<Button>(row).Single(b => b.DataContext == closing && Equals(b.Content, "×"));
        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(workspace.Nodes.Count == 9 && window.Documents.Contains(editing) && editing.Editor.Text == buffer, "A root's close button must remove only that folder and keep dirty documents.");
        workspace.Query = "server.js"; await workspace.SearchTask;
        Check(workspace.Matches.Count == 0, "Closed roots must not leave stale search results.");
        workspace.Query = "";

        Button more = (Button)window.Explorer.FindName("MoreButton");
        more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        MenuItem recent = more.ContextMenu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "최근 작업 폴더"));
        MenuItem reopen = recent.Items.OfType<MenuItem>().Single(i => Equals(i.ToolTip, roots[1]));
        reopen.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        more.ContextMenu.IsOpen = false;
        await WaitAsync(() => workspace.Nodes.Count == 10 && workspace.Nodes.Last().IsLoaded);
        Check(editing.Editor.Text == buffer && editing.Editor.IsModified, "Reopening a folder must not reload or overwrite its unsaved document.");
        workspace.MoveFolder(roots[1], -9);
        await workspace.RevealAsync(secondFile);
        workspace.Nodes.Single(n => n.FullPath == roots[2]).IsExpanded = false;
        string[] order = workspace.Nodes.Select(n => n.FullPath).ToArray();
        await Program.RenderAsync(window, Path.Combine(output, "workspaces-ten-small.png"), 960, 600);
        ((ColumnDefinition)window.FindName("SidebarColumn")).Width = new GridLength(170);
        window.UpdateLayout();
        string[] toolbarNames = ["AddFolderButton", "MoreButton", "NewFileButton", "NewFolderButton", "RefreshButton", "CollapseButton", "RevealButton"];
        Rect[] buttons = toolbarNames.Select(name =>
        {
            Button button = (Button)window.Explorer.FindName(name);
            return button.TransformToAncestor(window.Explorer).TransformBounds(new Rect(button.RenderSize));
        }).ToArray();
        Check(buttons.All(b => b.Left >= 0 && b.Right <= window.Explorer.ActualWidth + 1), "Explorer actions must remain inside the narrowest supported sidebar.");
        for (int i = 0; i < buttons.Length; i++)
            for (int j = i + 1; j < buttons.Length; j++)
                Check(Rect.Intersect(buttons[i], buttons[j]).IsEmpty || Rect.Intersect(buttons[i], buttons[j]).Width < 1 || Rect.Intersect(buttons[i], buttons[j]).Height < 1, "Explorer action buttons must not overlap at minimum sidebar width.");
        TreeViewItem narrowRoot = ExplorerChecks.FindRow(tree, workspace.Nodes[0])!;
        Button narrowClose = VisualChildren<Button>(narrowRoot).Single(b => b.DataContext == workspace.Nodes[0] && Equals(b.Content, "×"));
        Rect closeBounds = narrowClose.TransformToAncestor(tree).TransformBounds(new Rect(narrowClose.RenderSize));
        Check(closeBounds.Right <= tree.ActualWidth - 10, $"The folder close button must remain visible beside the scrollbar: right={closeBounds.Right}, tree={tree.ActualWidth}.");
        await Program.RenderAsync(window, Path.Combine(output, "workspaces-narrow.png"), 960, 600);
        await Program.CloseWindowAsync(window);

        MainWindow restored = new(new EditorPreferencesService(new PreferencesStore(settings)));
        Program.ShowOffscreen(restored);
        await Program.WaitForIdleAsync(restored); await restored.Explorer.Restoration;
        ExplorerWorkspace reopened = restored.Explorer.Workspace;
        Check(reopened.Nodes.Select(n => n.FullPath).SequenceEqual(order), "All ten roots and their order must survive restart.");
        Check(reopened.Find(Path.Combine(roots[1], "src")) is { IsExpanded: true } && !reopened.Nodes.Single(n => n.FullPath == roots[2]).IsExpanded, "Root and child expansion states must be independent and restored.");
        Check(restored.Documents.Any(d => d.FilePath == secondFile && d.Editor.Text == buffer && d.Editor.IsModified), "Workspace restoration must preserve the restored unsaved buffer.");
        int documents = restored.Documents.Count;
        more = (Button)restored.Explorer.FindName("MoreButton");
        more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        more.ContextMenu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "모든 작업 폴더 닫기")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        more.ContextMenu.IsOpen = false;
        Check(!reopened.HasRoot && restored.Documents.Count == documents, "Close-all from the menu must retain editor tabs.");
        Task pending = reopened.OpenFolderAsync(roots[0]);
        reopened.CloseAllFolders();
        await pending;
        Check(!reopened.HasRoot, "Close-all must cancel folder additions that are still checking disk access.");
        await reopened.OpenFolderAsync(roots[0]);
        Check(reopened.Nodes.Count == 1 && reopened.Nodes[0].IsLoaded, "Opening after close-all must use a fresh cancellation scope.");
        reopened.CloseAllFolders(); reopened.ClearRecentFolders();
        await reopened.FlushStateAsync();
        using (FileStream locked = new(Path.ChangeExtension(settings, ".workspace.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            restored.Close(); await restored.ClosingCompletion;
            Check(restored.IsVisible && restored.Documents.Count == documents, "Failure to persist the folder list must cancel shutdown and preserve documents.");
        }
        await Program.CloseWindowAsync(restored);
        WorkspaceState saved = new WorkspaceStateStore(Path.ChangeExtension(settings, ".workspace.json")).Load(out _);
        Check(saved.Folders.Length == 0 && saved.RecentFolders.Length == 0, "Closed folders and cleared history must not reappear after restart.");

        new WorkspaceStateStore(Path.Combine(sandbox, "missing.json")).Save(new() { Folders = [new() { Path = roots[10], ExpandedFolders = ["src"] }] });
        using ExplorerWorkspace unavailable = new(new WorkspaceStateStore(Path.Combine(sandbox, "missing.json")));
        Directory.Move(roots[10], roots[10] + "-moved");
        await unavailable.RestoreAsync();
        Check(unavailable.Nodes.Count == 1 && !unavailable.Nodes[0].IsLoaded, "Unavailable restored folders must remain visible and removable.");
        await unavailable.FlushStateAsync();
        Check(new WorkspaceStateStore(Path.Combine(sandbox, "missing.json")).Load(out _).Folders[0].ExpandedFolders.Contains("src"), "Unavailable roots must retain their saved expansion state.");
        Directory.Move(roots[10] + "-moved", roots[10]);
        await unavailable.RefreshAsync();
        Check(unavailable.Nodes[0].IsLoaded && unavailable.Find(Path.Combine(roots[10], "src")) is { IsExpanded: true }, "Refresh must reconnect an unavailable folder and restore its expansion.");
        new WorkspaceStateStore(Path.Combine(sandbox, "restoring.json")).Save(new() { Folders = order.Select(p => new WorkspaceFolderState { Path = p }).ToArray() });
        using ExplorerWorkspace restoring = new(new WorkspaceStateStore(Path.Combine(sandbox, "restoring.json")));
        Task restoration = restoring.RestoreAsync();
        restoring.CloseAllFolders();
        await restoration; await restoring.FlushStateAsync();
        Check(!restoring.HasRoot && new WorkspaceStateStore(Path.Combine(sandbox, "restoring.json")).Load(out _).Folders.Length == 0, "Close-all during restoration must cancel the remaining roots and persist the empty list.");
        Console.WriteLine("PASS: ten roots, legacy migration, global search, per-root watchers and operations, close/reopen, ordering, cancellation, restart, and unavailable-folder recovery.");
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
    private static async Task WaitAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition()) { if (DateTime.UtcNow > deadline) throw new TimeoutException("Workspace operation did not finish."); await Task.Delay(25); }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
