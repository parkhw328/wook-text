using System.Text;

namespace WookText.Core.Tests;

public sealed class TextFileServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wText-tests-" + Guid.NewGuid().ToString("N"));
    private readonly TextFileService _service = new();

    public TextFileServiceTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData(TextFileEncoding.Utf8)]
    [InlineData(TextFileEncoding.Utf8Bom)]
    [InlineData(TextFileEncoding.Utf16LittleEndian)]
    [InlineData(TextFileEncoding.Utf16BigEndian)]
    [InlineData(TextFileEncoding.Utf32LittleEndian)]
    [InlineData(TextFileEncoding.Utf32BigEndian)]
    public async Task ReadAndSave_UnicodeFile_PreservesBytesAndLineEndings(TextFileEncoding kind)
    {
        string path = Path.Combine(_directory, "한글 파일.txt");
        const string text = "첫째 줄🙂\r\nsecond\n마지막\r끝";
        Encoding encoding = kind.GetEncoding();
        byte[] original = [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
        await File.WriteAllBytesAsync(path, original);

        LoadedTextFile loaded = await _service.ReadAsync(path);
        Assert.Equal(kind, loaded.Encoding);
        Assert.Equal(text, loaded.Text);
        await _service.SaveAsync(path, loaded.Text, loaded.Encoding, loaded.Fingerprint);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Read_LegacyKorean_RequiresExplicitEncodingAndRoundTrips()
    {
        string path = Path.Combine(_directory, "cp949.txt");
        byte[] original = TextFileEncoding.Korean949.GetEncoding().GetBytes("한글 테스트\r\n내용");
        await File.WriteAllBytesAsync(path, original);
        await Assert.ThrowsAsync<DecoderFallbackException>(() => _service.ReadAsync(path));
        LoadedTextFile loaded = await _service.ReadAsync(path, TextFileEncoding.Korean949);
        Assert.Equal("한글 테스트\r\n내용", loaded.Text);
        await _service.SaveAsync(path, loaded.Text, loaded.Encoding, loaded.Fingerprint);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Save_UnrepresentableCharacter_LeavesOriginalIntact()
    {
        string path = Path.Combine(_directory, "legacy.txt");
        await File.WriteAllTextAsync(path, "original");
        LoadedTextFile loaded = await _service.ReadAsync(path);
        await Assert.ThrowsAsync<EncoderFallbackException>(() => _service.SaveAsync(path, "🙂", TextFileEncoding.Korean949, loaded.Fingerprint));
        Assert.Equal("original", await File.ReadAllTextAsync(path));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Save_ExternalModification_RejectsOverwrite()
    {
        string path = Path.Combine(_directory, "conflict.txt");
        await File.WriteAllTextAsync(path, "before");
        LoadedTextFile loaded = await _service.ReadAsync(path);
        await File.WriteAllTextAsync(path, "external change");
        await Assert.ThrowsAsync<FileConflictException>(() => _service.SaveAsync(path, "my edit", loaded.Encoding, loaded.Fingerprint));
        Assert.Equal("external change", await File.ReadAllTextAsync(path));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Save_ExistingFileWithoutFingerprint_RejectsOverwrite()
    {
        string path = Path.Combine(_directory, "existing.txt");
        await File.WriteAllTextAsync(path, "existing");
        await Assert.ThrowsAsync<FileConflictException>(() => _service.SaveAsync(path, "replacement", TextFileEncoding.Utf8, null));
        Assert.Equal("existing", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Save_DeletedOriginal_DoesNotRecreateWithoutConsent()
    {
        string path = Path.Combine(_directory, "removed.txt");
        await File.WriteAllTextAsync(path, "original");
        LoadedTextFile loaded = await _service.ReadAsync(path);
        File.Delete(path);
        await Assert.ThrowsAsync<FileConflictException>(() => _service.SaveAsync(path, "edit", loaded.Encoding, loaded.Fingerprint));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Save_LockedOriginal_LeavesOriginalAndCleansTemporaryFile()
    {
        string path = Path.Combine(_directory, "locked.txt");
        await File.WriteAllTextAsync(path, "original");
        LoadedTextFile loaded = await _service.ReadAsync(path);
        using (FileStream locked = new(path, FileMode.Open, FileAccess.Read, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => _service.SaveAsync(path, "edit", loaded.Encoding, loaded.Fingerprint));
        Assert.Equal("original", await File.ReadAllTextAsync(path));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Save_NewFile_WritesUtf8WithoutBom()
    {
        string path = Path.Combine(_directory, "new.txt");
        string fingerprint = await _service.SaveAsync(path, "안녕\nworld", TextFileEncoding.Utf8, null);
        byte[] bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(new UTF8Encoding(false).GetBytes("안녕\nworld"), bytes);
        Assert.Equal(fingerprint, (await _service.ReadAsync(path)).Fingerprint);
    }

    [Fact]
    public async Task Save_Cancelled_DoesNotCreateFile()
    {
        string path = Path.Combine(_directory, "cancel.txt");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.SaveAsync(path, "content", TextFileEncoding.Utf8, null, new CancellationToken(true)));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Read_EmptyFile_ReturnsEmptyUtf8()
    {
        string path = Path.Combine(_directory, "empty.txt");
        await File.WriteAllBytesAsync(path, []);
        LoadedTextFile file = await _service.ReadAsync(path);
        Assert.Empty(file.Text);
        Assert.Equal(TextFileEncoding.Utf8, file.Encoding);
    }

    [Theory]
    [InlineData(new byte[] { 65, 0, 66 })]
    [InlineData(new byte[] { 65, 1, 66 })]
    public async Task Read_BinaryContent_RejectsFile(byte[] content)
    {
        string path = Path.Combine(_directory, "binary.dat");
        await File.WriteAllBytesAsync(path, content);
        await Assert.ThrowsAsync<IOException>(() => _service.ReadAsync(path));
    }

    [Fact]
    public async Task Read_TooLarge_RejectsBeforeAllocatingContent()
    {
        string path = Path.Combine(_directory, "large.txt");
        using (FileStream stream = File.Create(path)) stream.SetLength(TextFileService.MaximumFileBytes + 1L);
        await Assert.ThrowsAsync<IOException>(() => _service.ReadAsync(path));
    }

    public void Dispose()
    {
        foreach (string file in Directory.EnumerateFiles(_directory)) File.Delete(file);
        Directory.Delete(_directory);
    }
}
