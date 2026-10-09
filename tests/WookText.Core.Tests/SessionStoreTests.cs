namespace WookText.Core.Tests;

public sealed class SessionStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wText-session-" + Guid.NewGuid().ToString("N"));
    private SessionStore Store => new(Path.Combine(_directory, "session-test.json"));
    private static SessionState Sample(string text = "한글\r\nunsaved 😀\n")
    {
        SessionDocumentState document = new()
        {
            Name = "새 문서 7",
            Text = text,
            IsModified = true,
            Encoding = TextFileEncoding.Korean949,
            Language = "java",
            CaretOffset = 4,
            SelectionStart = 1,
            SelectionLength = 3,
            VerticalOffset = 80
        };
        return new() { ActiveDocumentId = document.Id, Documents = [document] };
    }

    [Fact]
    public async Task Save_UnicodeAndViewState_RoundTripsWithoutChangingDocumentData()
    {
        SessionState expected = Sample();
        await Store.SaveAsync(expected);
        SessionLoadResult actual = await Store.LoadAsync();
        Assert.Equal(expected.ActiveDocumentId, actual.State.ActiveDocumentId);
        Assert.Equal(expected.Documents, actual.State.Documents);
        Assert.Null(actual.Warning);
    }

    [Fact]
    public async Task Load_DamagedLatest_RecoversPreviousSnapshotAndProtectsDamagedFile()
    {
        SessionState previous = Sample("previous");
        await Store.SaveAsync(previous);
        await Store.SaveAsync(Sample("latest"));
        await File.WriteAllTextAsync(Store.FilePath, "{broken");
        SessionLoadResult actual = await Store.LoadAsync();
        Assert.Equal("previous", Assert.Single(actual.State.Documents).Text);
        Assert.NotNull(actual.Warning);
        Assert.False(actual.CanOverwrite);
        Assert.Equal("{broken", await File.ReadAllTextAsync(Store.FilePath));
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"FormatVersion\":999}")]
    [InlineData("{\"Documents\":[null]}")]
    public async Task Load_InvalidOrFutureSnapshot_PreservesOriginalAndReportsFailure(string json)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Store.FilePath, json);
        SessionLoadResult actual = await Store.LoadAsync();
        Assert.Empty(actual.State.Documents);
        Assert.False(actual.CanOverwrite);
        Assert.NotNull(actual.Warning);
        Assert.Equal(json, await File.ReadAllTextAsync(Store.FilePath));
    }

    [Fact]
    public async Task Save_LockedDestination_PreservesLastSnapshotAndRemovesTemporaryFiles()
    {
        SessionState expected = Sample();
        await Store.SaveAsync(expected);
        using (FileStream held = new(Store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Exception? error = await Record.ExceptionAsync(() => Store.SaveAsync(Sample("replacement")));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(expected.Documents, (await Store.LoadAsync()).State.Documents);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task Save_Cancelled_PreservesPriorSnapshot()
    {
        SessionState expected = Sample();
        await Store.SaveAsync(expected);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store.SaveAsync(Sample("new"), cancelled.Token));
        Assert.Equal(expected.Documents, (await Store.LoadAsync()).State.Documents);
    }

    [Fact]
    public async Task Acquire_SimultaneousWindows_UsesIndependentLeasesAndRecoversClosedWindow()
    {
        using SessionStore first = await SessionStore.AcquireAsync(_directory);
        await first.SaveAsync(Sample("first window"));
        string secondPath;
        using (SessionStore second = await SessionStore.AcquireAsync(_directory))
        {
            Assert.NotEqual(first.FilePath, second.FilePath);
            await second.SaveAsync(Sample("second window"));
            secondPath = second.FilePath;
        }
        using SessionStore reopened = await SessionStore.AcquireAsync(_directory);
        Assert.Equal(secondPath, reopened.FilePath);
        Assert.Equal("second window", Assert.Single((await reopened.LoadAsync()).State.Documents).Text);
        Assert.Equal("first window", Assert.Single((await first.LoadAsync()).State.Documents).Text);
    }

    [Fact]
    public async Task Clear_DiscardedSession_RemovesSnapshotAndBackup()
    {
        await Store.SaveAsync(Sample());
        await Store.SaveAsync(Sample("later"));
        await Store.ClearAsync();
        Assert.False(File.Exists(Store.FilePath));
        Assert.False(File.Exists(Store.BackupPath));
        Assert.Empty((await Store.LoadAsync()).State.Documents);
    }

    [Fact]
    public async Task Save_InvalidSurrogate_PreservesOriginalInsteadOfReplacingCharacters()
    {
        SessionState expected = Sample();
        await Store.SaveAsync(expected);
        await Assert.ThrowsAsync<System.Text.EncoderFallbackException>(() => Store.SaveAsync(Sample("invalid \uD800")));
        Assert.Equal(expected.Documents, (await Store.LoadAsync()).State.Documents);
    }

    [Fact]
    public async Task Save_TooManyDocuments_DoesNotReplaceLastSnapshot()
    {
        SessionState expected = Sample();
        await Store.SaveAsync(expected);
        await Assert.ThrowsAsync<IOException>(() => Store.SaveAsync(new() { Documents = Enumerable.Range(0, SessionStore.MaximumDocuments + 1).Select(_ => new SessionDocumentState()).ToArray() }));
        Assert.Equal(expected.Documents, (await Store.LoadAsync()).State.Documents);
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
}
