using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WookText.App;
using WookText.App.Explorer;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class ExplorerChecks
{
    internal static async Task RunAsync(string output)
    {
        string sandbox = Path.Combine(output, "fixtures", "explorer-" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(sandbox, "orange-workspace");
        foreach (string directory in new[] { "src", "src\\components", "src\\services", "resources", "docs", "tests", "node_modules" }) Directory.CreateDirectory(Path.Combine(root, directory));
        Dictionary<string, string> samples = new()
        {
            ["README.md"] = "# Orange workspace\n한글과 코드를 함께 편집합니다.\n",
            ["package.json"] = "{\n  \"name\": \"orange-workspace\",\n  \"version\": \"1.0.0\"\n}\n",
            ["src\\app.js"] = "// 편안한 편집 환경\nimport { Editor } from './components/Editor';\n\nconst preferences = {\n    language: '한국어',\n    theme: 'black-orange',\n    fontSize: 15\n};\n\nexport function start() {\n    return new Editor(preferences);\n}\n",
            ["src\\theme.css"] = "body { color: #cecdc3; background: #100f0f; }\n",
            ["src\\components\\Editor.tsx"] = "export function Editor() { return 'wText'; }\n",
            ["src\\components\\Sidebar.tsx"] = "export function Sidebar() { return '탐색기'; }\n",
            ["src\\services\\FileService.java"] = "public class FileService {}\n",
            ["resources\\settings.yml"] = "theme: orange\nfontSize: 15\n",
            ["docs\\editing.md"] = "# Editing\n",
            ["tests\\editor2.test.js"] = "const value = true;\n",
            ["tests\\editor10.test.js"] = "const value = false;\n",
            ["node_modules\\ignored.js"] = "ignored"
        };
        foreach ((string path, string text) in samples) await File.WriteAllTextAsync(Path.Combine(root, path), text);
        string settings = Path.Combine(sandbox, "settings.json");
        EditorPreferencesService preferences = new(new PreferencesStore(settings));
        preferences.Update(new EditorPreferences { SidebarWidth = 296 });
        MainWindow window = new(preferences);
        Program.ShowOffscreen(window);
        await window.InitializeSessionAsync();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await window.OpenWorkspaceAsync(root);
        ExplorerWorkspace workspace = window.Explorer.Workspace;
        Check(workspace.HasRoot && workspace.Nodes.Single().Children.All(n => n.Name != "node_modules"), "Opening a workspace must hide dependency folders by default.");
        string appFile = Path.Combine(root, "src", "app.js");
        await window.OpenFileAsync(appFile);
        ExplorerNode? active = await workspace.RevealAsync(appFile);
        Check(active is { IsActive: true, IsSelected: true } && workspace.Find(Path.Combine(root, "src")) is { IsExpanded: true }, "Reveal must expand the ancestor and mark the active document.");
        ExplorerNode components = workspace.Find(Path.Combine(root, "src", "components"))!;
        components.IsExpanded = true;
        await workspace.EnsureLoadedAsync(components);
        ExplorerNode resources = workspace.Find(Path.Combine(root, "resources"))!;
        resources.IsExpanded = true; await workspace.EnsureLoadedAsync(resources);
        await window.OpenFileAsync(Path.Combine(root, "package.json"));
        await window.OpenFileAsync(appFile);
        await workspace.RevealAsync(appFile);
        await Program.RenderAsync(window, Path.Combine(output, "explorer.png"), 1240, 800);
        TreeView fileTree = (TreeView)window.Explorer.FindName("FileTree");
        ExplorerNode readme = workspace.Find(Path.Combine(root, "README.md"))!;
        await workspace.RevealAsync(readme.FullPath);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        TreeViewItem readmeRow = FindRow(fileTree, readme)!;
        readmeRow.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = Mouse.PreviewMouseDownEvent });
        Check(workspace.SelectedNode == readme && fileTree.ContextMenu.Items.OfType<MenuItem>().Any(i => Equals(i.Header, "이름 바꾸기…")), "Right-click must select the clicked item and expose its file actions.");
        fileTree.ContextMenu.IsOpen = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Program.RenderElement(fileTree.ContextMenu, Path.Combine(output, "explorer-menu.png"));
        fileTree.ContextMenu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "파일 열기")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        fileTree.ContextMenu.IsOpen = false;
        await WaitAsync(() => window.ActiveDocument?.Name == "README.md");
        EditorCommands.CloseTab.Execute(null, window);
        await Program.WaitForIdleAsync(window);
        await window.OpenFileAsync(appFile);
        await workspace.RevealAsync(appFile);
        await Program.RenderAsync(window, Path.Combine(output, "explorer-small.png"), 960, 600);
        for (int i = 0; i < 9; i++) window.NewDocument();
        window.UpdateLayout();
        Check(((TreeView)window.Explorer.FindName("FileTree")).ActualHeight >= 100, "Even with many open documents, the tree must retain usable space in the smallest window.");
        for (int i = 0; i < 9; i++) { EditorCommands.CloseTab.Execute(null, window); await Program.WaitForIdleAsync(window); }
        await VerifyNameDialogAsync(workspace, root, output);

        workspace.Query = "nonexistent";
        workspace.Query = "src component";
        await workspace.SearchTask;
        Check(workspace.Matches.Count == 2 && workspace.Matches.All(n => n.RelativePath.Contains("components", StringComparison.Ordinal)), "Only the latest debounced path search may populate the results.");
        await Program.RenderAsync(window, Path.Combine(output, "explorer-search.png"), 1240, 800);
        workspace.Query = "FileService.java";
        await workspace.SearchTask;
        TextBox filter = (TextBox)window.Explorer.FindName("FilterBox");
        KeyEventArgs enter = new(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        filter.RaiseEvent(enter);
        await WaitAsync(() => window.ActiveDocument?.Name == "FileService.java");
        Check(enter.Handled, "Enter in file search must open the matching document.");
        workspace.Query = "";

        string created = (await workspace.CreateAsync(Path.Combine(root, "src"), "new-file.txt", false))!;
        Check(File.Exists(created) && workspace.SelectedNode?.FullPath == created, "New files must appear and be selected in their parent folder.");
        string folder = (await workspace.CreateAsync(root, "new-folder", true))!;
        Check(Directory.Exists(folder), "New folders must be created on disk.");
        await window.OpenFileAsync(created);
        EditorDocument editing = window.ActiveDocument!;
        editing.Editor.AppendText("아직 저장하지 않은 내용\n");
        int caret = editing.Editor.CaretOffset;
        await workspace.RenameAsync(workspace.Find(created)!, "renamed.txt");
        string renamed = Path.Combine(root, "src", "renamed.txt");
        Check(editing.FilePath == renamed && editing.Editor.IsModified && editing.Editor.CaretOffset == caret && editing.Editor.Text.Contains("저장하지 않은", StringComparison.Ordinal), "Renaming an open file must preserve its unsaved buffer and caret.");
        editing.Editor.Undo();
        Check(editing.Editor.Text == "" && !editing.Editor.IsModified, "Renaming must preserve undo history.");
        await workspace.RenameAsync(workspace.Find(Path.Combine(root, "src"))!, "source");
        Check(window.Documents.Where(d => d.FilePath is not null).All(d => !d.FilePath!.StartsWith(Path.Combine(root, "src") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)), "Renaming a folder must retarget every open descendant document.");
        editing.Editor.AppendText("saved at renamed path\n");
        ApplicationCommands.Save.Execute(null, window);
        await WaitAsync(() => ((Grid)window.FindName("Root")).IsEnabled);
        Check(await File.ReadAllTextAsync(editing.FilePath!) == editing.Editor.Text, "Saving after a folder rename must use the new path.");
        await Task.Delay(650);
        Check(editing.FilePath == Path.Combine(root, "source", "renamed.txt"), "Atomic-save temporary renames must never redirect an open document's path.");
        string external = Path.Combine(root, "external-created.txt");
        await File.WriteAllTextAsync(external, "external");
        await WaitAsync(() => workspace.Find(external) is not null);
        File.Delete(external);
        await WaitAsync(() => workspace.Find(external) is null);
        Check(workspace.Find(Path.Combine(root, "source")) is { IsExpanded: true }, "Automatic refresh must preserve expanded folders.");
        ExplorerNode collapsed = workspace.Find(Path.Combine(root, "resources"))!;
        collapsed.IsExpanded = false;
        string insideCollapsed = Path.Combine(root, "resources", "added-later.yml");
        await File.WriteAllTextAsync(insideCollapsed, "enabled: true");
        await workspace.RefreshAsync();
        collapsed.IsExpanded = true;
        await workspace.EnsureLoadedAsync(collapsed);
        Check(workspace.Find(insideCollapsed) is not null, "Reopening a cached collapsed folder must include external changes.");
        string externallyRenamed = Path.Combine(root, "source", "external-renamed.txt");
        File.Move(editing.FilePath!, externallyRenamed);
        await WaitAsync(() => editing.FilePath == externallyRenamed);
        Check(!editing.Editor.IsModified && editing.Editor.Text.Contains("saved at renamed path", StringComparison.Ordinal), "External renames must update open document paths without altering their contents.");
        Task inProgress = workspace.RefreshAsync();
        await workspace.SetOptionsAsync(false, false);
        await inProgress;
        Check(workspace.Find(Path.Combine(root, "node_modules")) is not null, "The folder filter must be reversible.");
        await workspace.SetOptionsAsync(false, true);
        string finalFile = Path.Combine(root, "source", "components", "Editor.tsx");
        await workspace.RevealAsync(finalFile);
        await VerifyLargeFolderAsync(window, sandbox, root);
        await workspace.RevealAsync(finalFile);
        int documentCount = window.Documents.Count;
        await Program.CloseWindowAsync(window);

        MainWindow reopened = new(new EditorPreferencesService(new PreferencesStore(settings)));
        Program.ShowOffscreen(reopened);
        await reopened.InitializeSessionAsync();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await reopened.Explorer.Restoration;
        Check(reopened.Explorer.Workspace.RootPath == root && reopened.Explorer.Workspace.Find(Path.Combine(root, "source", "components")) is { IsExpanded: true }, "A new window must restore the workspace and expanded paths.");
        reopened.Explorer.Workspace.CollapseAll();
        Check(reopened.Explorer.Workspace.Nodes.Where(n => n.IsDirectory).All(n => !n.IsExpanded), "Collapse all must close top-level folders.");
        reopened.Explorer.Workspace.CloseFolder();
        Check(!reopened.Explorer.Workspace.HasRoot && reopened.Documents.Count == documentCount, "Closing a workspace must leave restored editor documents alone.");
        await Program.CloseWindowAsync(reopened);
        Console.WriteLine("PASS: explorer async loading, path search, keyboard open, reveal, create/rename with unsaved buffers, live refresh, filters, and workspace restoration.");
    }

    private static async Task VerifyLargeFolderAsync(MainWindow window, string sandbox, string restoreRoot)
    {
        string large = Path.Combine(sandbox, "large-folder");
        Directory.CreateDirectory(large);
        for (int i = 0; i < 1200; i++) File.WriteAllText(Path.Combine(large, $"file{i}.txt"), "");
        Task first = window.Explorer.Workspace.OpenFolderAsync(restoreRoot);
        Task second = window.Explorer.Workspace.OpenFolderAsync(large);
        await Task.WhenAll(first, second);
        ExplorerWorkspace workspace = window.Explorer.Workspace;
        Check(workspace.Nodes.Count == 2 && workspace.Nodes.Single(n => n.FullPath == large).Children.Count == 1200, "Adding a folder must retain the previous root and support 1200 files.");
        ExplorerNode? last = await workspace.RevealAsync(Path.Combine(large, "file1199.txt"));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        TreeView tree = (TreeView)window.Explorer.FindName("FileTree");
        TreeViewItem? row = FindRow(tree, last!);
        Check(row is not null && row.TranslatePoint(new Point(), tree).Y < tree.ActualHeight && row.TranslatePoint(new Point(), tree).Y >= 0, "Reveal must scroll an initially unrealized file into the visible tree viewport.");
        TreeViewItem rootRow = FindRow(tree, workspace.Nodes.Single(n => n.FullPath == large))!;
        int realized = Enumerable.Range(0, rootRow.Items.Count).Count(i => rootRow.ItemContainerGenerator.ContainerFromIndex(i) is not null);
        Check(realized < 200, "Large folders must virtualize rows instead of constructing every file's controls.");
        Console.WriteLine($"PASS: 1200-file folder, {realized} realized rows, and offscreen-item reveal.");
        workspace.CloseFolder(large);
        await window.OpenWorkspaceAsync(restoreRoot);
    }

    internal static TreeViewItem? FindRow(ItemsControl parent, ExplorerNode target)
    {
        foreach (ExplorerNode node in parent.Items.OfType<ExplorerNode>())
        {
            if (!WorkspaceFiles.IsWithin(node.FullPath, target.FullPath)) continue;
            TreeViewItem? row = parent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
            if (node == target) return row;
            if (row is not null && FindRow(row, target) is { } child) return child;
        }
        return null;
    }

    private static async Task VerifyNameDialogAsync(ExplorerWorkspace workspace, string root, string output)
    {
        NameDialog dialog = new("새 파일", root, "", async name => await workspace.CreateAsync(root, name, false))
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -32000,
            Top = -32000
        };
        Exception? failure = null;
        _ = dialog.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                TextBox name = (TextBox)dialog.FindName("NameBox");
                Button accept = (Button)dialog.FindName("AcceptButton");
                name.Text = "../outside.txt";
                accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(((TextBlock)dialog.FindName("ErrorMessage")).Text.Length > 0 && dialog.IsVisible, "Invalid names must show an inline error and keep the dialog open.");
                await Program.RenderAsync(dialog, Path.Combine(output, "explorer-name-dialog.png"), 480, 285);
                name.Text = "from-dialog.txt";
                accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitAsync(() => !dialog.IsVisible);
            }
            catch (Exception ex) { failure = ex; if (dialog.IsVisible) dialog.Close(); }
        }), DispatcherPriority.ApplicationIdle);
        bool? result = dialog.ShowDialog();
        if (failure is not null) throw new InvalidOperationException("Name dialog check failed.", failure);
        Check(result == true && File.Exists(Path.Combine(root, "from-dialog.txt")), "Confirming a valid name must create the file.");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Explorer operation did not finish.");
            await Task.Delay(25);
        }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
