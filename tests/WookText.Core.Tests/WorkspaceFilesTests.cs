namespace WookText.Core.Tests;

public sealed class WorkspaceFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wText-explorer-" + Guid.NewGuid().ToString("N"));
    private readonly WorkspaceFiles _files = new();
    public WorkspaceFilesTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task ReadDirectory_MixedEntries_PutsFoldersFirstAndUsesNaturalSort()
    {
        foreach (string name in new[] { "z-folder", "a-folder", "node_modules", ".git", "bin" }) Directory.CreateDirectory(Path.Combine(_root, name));
        foreach (string name in new[] { "file10.txt", "file2.txt", "file1.txt", ".env", "한글.txt" }) File.WriteAllText(Path.Combine(_root, name), "");
        DirectoryListing result = await _files.ReadDirectoryAsync(_root, new());
        Assert.Equal(new[] { "a-folder", "z-folder" }, result.Entries.Where(e => e.IsDirectory).Select(e => e.Name));
        Assert.Equal(new[] { "file1.txt", "file2.txt", "file10.txt" }, result.Entries.Where(e => e.Name.StartsWith("file", StringComparison.Ordinal)).Select(e => e.Name));
        Assert.Contains(result.Entries, e => e.Name == ".env");
        Assert.Contains(result.Entries, e => e.Name == "한글.txt");
        Assert.False(result.Truncated);
        DirectoryListing all = await _files.ReadDirectoryAsync(_root, new(HideGenerated: false));
        Assert.Contains(all.Entries, e => e.Name == "node_modules");
    }

    [Fact]
    public async Task ReadDirectory_HiddenEntry_RespectsDisplayOption()
    {
        string path = Path.Combine(_root, "hidden.txt");
        File.WriteAllText(path, "keep");
        File.SetAttributes(path, FileAttributes.Hidden);
        Assert.Empty((await _files.ReadDirectoryAsync(_root, new())).Entries);
        Assert.Single((await _files.ReadDirectoryAsync(_root, new(ShowHidden: true))).Entries);
    }

    [Fact]
    public async Task Search_NestedPaths_MatchesEveryTermAndOmitsGeneratedFolders()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src", "화면"));
        Directory.CreateDirectory(Path.Combine(_root, "node_modules"));
        string expected = Path.Combine(_root, "src", "화면", "MainView.JAVA");
        File.WriteAllText(expected, "class MainView {}");
        File.WriteAllText(Path.Combine(_root, "node_modules", "MainView.java"), "");
        WorkspaceSearch result = await _files.SearchAsync(_root, "SRC/화면 java", new());
        Assert.Equal(expected, Assert.Single(result.Entries).FullPath);
        Assert.Single((await _files.SearchAsync(_root, "mainview", new())).Entries);
        Assert.Equal(2, (await _files.SearchAsync(_root, "mainview", new(HideGenerated: false))).Entries.Count);
        Assert.Empty((await _files.SearchAsync(_root, " ", new())).Entries);
    }

    [Fact]
    public async Task Search_ManyMatches_ReportsLimitWithoutReturningUnboundedResults()
    {
        for (int i = 0; i < WorkspaceFiles.SearchResultLimit + 1; i++) File.WriteAllText(Path.Combine(_root, $"item{i}.txt"), "");
        WorkspaceSearch result = await _files.SearchAsync(_root, "item", new());
        Assert.True(result.Truncated);
        Assert.Equal(WorkspaceFiles.SearchResultLimit, result.Entries.Count);
    }

    [Fact]
    public async Task Search_CancelledRequest_StopsBeforeEnumerating()
    {
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _files.SearchAsync(_root, "file", new(), cancelled.Token));
    }

    [Fact]
    public async Task ReadDirectory_MissingFolder_ReportsFailure()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => _files.ReadDirectoryAsync(Path.Combine(_root, "missing"), new()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../outside.txt")]
    [InlineData("nested\\file.txt")]
    [InlineData("file:stream")]
    [InlineData("CON.txt")]
    [InlineData("LPT1")]
    [InlineData("COM¹.txt")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    public void Create_InvalidWindowsName_DoesNotCreateAnything(string name)
    {
        Assert.Throws<ArgumentException>(() => WorkspaceFiles.Create(_root, name, false));
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public void Create_ExistingFile_NeverOverwritesContents()
    {
        string path = WorkspaceFiles.Create(_root, "existing.txt", false);
        File.WriteAllText(path, "preserve");
        Assert.Throws<IOException>(() => WorkspaceFiles.Create(_root, "existing.txt", false));
        Assert.Equal("preserve", File.ReadAllText(path));
    }

    [Fact]
    public void Rename_FileAndDirectory_PreservesContentsAndSupportsCaseChanges()
    {
        string directory = WorkspaceFiles.Create(_root, "source", true);
        string path = WorkspaceFiles.Create(directory, "한글.txt", false);
        File.WriteAllText(path, "original\r\n");
        string renamedFile = WorkspaceFiles.Rename(new(path, "한글.txt", false), "Renamed.txt");
        string renamedDirectory = WorkspaceFiles.Rename(new(directory, "source", true), "Source");
        Assert.Equal("Source", new DirectoryInfo(renamedDirectory).Name);
        Assert.Equal("original\r\n", File.ReadAllText(Path.Combine(renamedDirectory, Path.GetFileName(renamedFile))));
    }

    [Fact]
    public void Rename_NameCollision_PreservesBothFiles()
    {
        string first = WorkspaceFiles.Create(_root, "first.txt", false);
        string second = WorkspaceFiles.Create(_root, "second.txt", false);
        File.WriteAllText(first, "first"); File.WriteAllText(second, "second");
        Assert.Throws<IOException>(() => WorkspaceFiles.Rename(new(first, "first.txt", false), "second.txt"));
        Assert.Equal("first", File.ReadAllText(first)); Assert.Equal("second", File.ReadAllText(second));
    }

    [Fact]
    public void IsWithin_SiblingPrefixAndParentTraversal_AreNotDescendants()
    {
        Assert.True(WorkspaceFiles.IsWithin(_root, Path.Combine(_root, "sub", "file.txt")));
        Assert.False(WorkspaceFiles.IsWithin(_root, _root + "-other\\file.txt"));
        Assert.False(WorkspaceFiles.IsWithin(_root, Path.Combine(_root, "..", "outside.txt")));
        Assert.True(WorkspaceFiles.IsWithin(Path.GetPathRoot(_root)!, _root));
    }

    [Fact]
    public void State_SaveAndRestore_KeepsWorkspaceOptionsAndValidRelativeFolders()
    {
        WorkspaceStateStore store = new(Path.Combine(_root, "workspace.json"));
        store.Save(new WorkspaceState { RootPath = _root, ExpandedFolders = ["src", "src\\nested", "..\\outside", "C:\\outside", "src"], RecentFolders = [_root, _root.ToUpperInvariant()], ShowHidden = true, HideGenerated = false });
        WorkspaceState state = new WorkspaceStateStore(store.FilePath).Load(out string? warning);
        Assert.Null(warning);
        WorkspaceFolderState folder = Assert.Single(state.Folders);
        Assert.Equal(_root, folder.Path);
        Assert.Equal(new[] { "src", "src\\nested" }, folder.ExpandedFolders);
        Assert.Single(state.RecentFolders);
        Assert.True(state.ShowHidden); Assert.False(state.HideGenerated);
    }

    [Fact]
    public void State_CorruptFile_ReportsWarningAndPreservesOriginal()
    {
        string path = Path.Combine(_root, "workspace.json");
        File.WriteAllText(path, "{broken}");
        WorkspaceState state = new WorkspaceStateStore(path).Load(out string? warning);
        Assert.Null(state.RootPath); Assert.NotNull(warning);
        Assert.Equal("{broken}", File.ReadAllText(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
