using System.Text.Json;

namespace WookText.Core.Tests;

public sealed class MultiWorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wText-multi-" + Guid.NewGuid().ToString("N"));
    public MultiWorkspaceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Load_LegacyWorkspace_MigratesRootAndExpandedFolders()
    {
        string path = Path.Combine(_root, "state.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { RootPath = _root, ExpandedFolders = new[] { "src", "src\\components" }, RecentFolders = new[] { _root }, ShowHidden = true }));
        WorkspaceStateStore store = new(path);
        WorkspaceState state = store.Load(out string? warning);
        Assert.Null(warning);
        WorkspaceFolderState folder = Assert.Single(state.Folders);
        Assert.Equal(_root, folder.Path);
        Assert.Equal(new[] { "src", "src\\components" }, folder.ExpandedFolders);
        Assert.True(folder.IsExpanded);
        store.Save(state);
        Assert.DoesNotContain("\"RootPath\"", File.ReadAllText(path));
        Assert.Equal(folder.Path, Assert.Single(store.Load(out _).Folders).Path);
    }

    [Fact]
    public void Save_MultipleFolders_PreservesOrderAndIndependentExpansion()
    {
        WorkspaceStateStore store = new(Path.Combine(_root, "state.json"));
        WorkspaceFolderState[] folders = Enumerable.Range(0, 10).Select(i => new WorkspaceFolderState
        { Path = Path.Combine(_root, $"project-{i}"), IsExpanded = i % 2 == 0, ExpandedFolders = ["src", $"src\\module-{i}"] }).Reverse().ToArray();
        store.Save(new() { Folders = folders, RecentFolders = folders.Select(f => f.Path).ToArray() });
        WorkspaceState state = store.Load(out string? warning);
        Assert.Null(warning);
        Assert.Equal(folders.Select(f => f.Path), state.Folders.Select(f => f.Path));
        Assert.Equal(folders.Select(f => f.IsExpanded), state.Folders.Select(f => f.IsExpanded));
        Assert.All(Enumerable.Range(0, 10), i => Assert.Equal(folders[i].ExpandedFolders, state.Folders[i].ExpandedFolders));
        Assert.Equal(10, state.RecentFolders.Length);
    }

    [Fact]
    public void Load_InvalidDuplicateAndExcessRoots_NormalizesAndEnforcesTen()
    {
        string path = Path.Combine(_root, "state.json");
        WorkspaceFolderState[] valid = Enumerable.Range(0, 12).Select(i => new WorkspaceFolderState
        { Path = Path.Combine(_root, $"root{i}"), ExpandedFolders = ["src", "..\\outside", "C:\\outside", "src"] }).ToArray();
        WorkspaceFolderState[] folders = [null!, new() { Path = "relative" }, valid[0], valid[0] with { Path = valid[0].Path.ToUpperInvariant() + "\\" }, .. valid];
        File.WriteAllText(path, JsonSerializer.Serialize(new WorkspaceState { Folders = folders }));
        WorkspaceState state = new WorkspaceStateStore(path).Load(out string? warning);
        Assert.Null(warning);
        Assert.Equal(10, state.Folders.Length);
        Assert.All(state.Folders, f => Assert.Equal(["src"], f.ExpandedFolders));
        Assert.Equal(valid.Take(10).Select(f => f.Path), state.Folders.Select(f => f.Path));
    }

    [Fact]
    public void Save_CloseAllAndClearHistory_DoesNotRestoreLegacyRoot()
    {
        WorkspaceStateStore store = new(Path.Combine(_root, "state.json"));
        store.Save(new() { RootPath = _root });
        WorkspaceState state = store.Load(out _);
        store.Save(state with { Folders = [], RecentFolders = [] });
        state = store.Load(out _);
        Assert.Empty(state.Folders);
        Assert.Empty(state.RecentFolders);
    }

    [Fact]
    public async Task Search_MultipleOverlappingRoots_FindsAllAndDeduplicatesFiles()
    {
        string first = Directory.CreateDirectory(Path.Combine(_root, "client")).FullName;
        string nested = Directory.CreateDirectory(Path.Combine(first, "src")).FullName;
        string second = Directory.CreateDirectory(Path.Combine(_root, "server")).FullName;
        string a = Path.Combine(nested, "shared.java"), b = Path.Combine(second, "shared.java");
        File.WriteAllText(a, "client"); File.WriteAllText(b, "server");
        WorkspaceFiles files = new();
        WorkspaceSearch result = await files.SearchAsync([first, nested, second, Path.Combine(_root, "missing")], "shared", new());
        Assert.Equal(2, result.Entries.Count);
        Assert.Contains(result.Entries, e => e.FullPath == a);
        Assert.Contains(result.Entries, e => e.FullPath == b);
        Assert.Equal(1, result.SkippedFolders);
        Assert.Equal(b, Assert.Single((await files.SearchAsync([first, second], "server shared", new())).Entries).FullPath);
    }

    [Fact]
    public async Task Search_MultipleRoots_AppliesOneGlobalResultLimit()
    {
        string[] roots = Enumerable.Range(0, 3).Select(i => Directory.CreateDirectory(Path.Combine(_root, $"root{i}")).FullName).ToArray();
        foreach (string root in roots)
            for (int i = 0; i < 90; i++) File.WriteAllText(Path.Combine(root, $"file{i}.txt"), "");
        WorkspaceSearch result = await new WorkspaceFiles().SearchAsync(roots, "file", new());
        Assert.True(result.Truncated);
        Assert.Equal(WorkspaceFiles.SearchResultLimit, result.Entries.Count);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
