using System.Security.Cryptography;
using System.Text;

namespace WookText.Core.Tests;

public sealed class LargeFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wText-large-" + Guid.NewGuid().ToString("N"));
    private readonly TextFileService _files = new();
    public LargeFileTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task ReadAndSave_HundredMiBLog_StreamsAndPreservesBytes()
    {
        string path = Path.Combine(_directory, "100MiB.log");
        byte[] line = Encoding.UTF8.GetBytes("2026-11-03 INFO 한글 event=" + new string('x', 97) + "\r\n");
        const int size = 100 * 1024 * 1024;
        await using (FileStream output = File.Create(path))
        {
            byte[] block = Enumerable.Repeat(line, 1024).SelectMany(b => b).ToArray();
            while (output.Length + block.Length <= size) await output.WriteAsync(block);
            while (output.Length + line.Length <= size) await output.WriteAsync(line);
        }
        int count = 0, largestChunk = 0;
        LoadedTextFile file = await Task.Run(() => _files.ReadChunks(path, chunk =>
        {
            count += chunk.Length;
            largestChunk = Math.Max(largestChunk, chunk.Length);
        }));
        Assert.True(new FileInfo(path).Length >= size - line.Length);
        Assert.Equal(count, file.CharacterCount);
        Assert.True(largestChunk <= 65536);
        Assert.Empty(file.Text);
        Assert.Equal("CRLF", file.LineEndings);
        Assert.True(LargeFilePolicy.IsLarge(file.FileBytes, file.CharacterCount, file.LineCount));
        string copy = Path.Combine(_directory, "copy.log");
        string fingerprint = await _files.SaveAsync(copy, () => new StreamReader(path, TextFileEncoding.Utf8.GetEncoding(), false), file.Encoding, null);
        Assert.Equal(file.Fingerprint, fingerprint);
        Assert.Equal(fingerprint, await TextFileService.GetFingerprintAsync(copy));
    }

    [Theory]
    [InlineData(TextFileEncoding.Utf8)]
    [InlineData(TextFileEncoding.Utf8Bom)]
    [InlineData(TextFileEncoding.Utf16LittleEndian)]
    [InlineData(TextFileEncoding.Utf16BigEndian)]
    [InlineData(TextFileEncoding.Utf32LittleEndian)]
    [InlineData(TextFileEncoding.Utf32BigEndian)]
    [InlineData(TextFileEncoding.Korean949)]
    public async Task ReadAndSave_ChunkBoundaries_PreservesEncodingAndNewlines(TextFileEncoding kind)
    {
        string path = Path.Combine(_directory, "boundary.log");
        string content = string.Concat(Enumerable.Repeat(new string('a', 32761) + "한글\r\n끝\r", 5));
        if (kind != TextFileEncoding.Korean949) content += "😀\n";
        Encoding encoding = kind.GetEncoding();
        byte[] original = [.. encoding.GetPreamble(), .. encoding.GetBytes(content)];
        await File.WriteAllBytesAsync(path, original);
        LoadedTextFile file = await _files.ReadAsync(path, kind);
        Assert.Equal(content, file.Text);
        Assert.Equal(TextAnalysis.LineEndings(content), file.LineEndings);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(original)), file.Fingerprint);
        await _files.SaveAsync(path, file.Text, kind, file.Fingerprint);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Read_CrlfSplitBetweenChunks_CountsOneLineEnding()
    {
        string path = Path.Combine(_directory, "crlf.log");
        // The BOM probe consumes four bytes before the first 64 KiB block.
        await File.WriteAllTextAsync(path, new string('x', 65539) + "\r\nend");
        LoadedTextFile file = await _files.ReadAsync(path);
        Assert.Equal(2, file.LineCount);
        Assert.Equal("CRLF", file.LineEndings);
    }

    [Theory]
    [InlineData("123456", 5, 20, 20)]
    [InlineData("a\r\nb\nc\rd", 20, 3, 20)]
    [InlineData("abcdef", 20, 20, 5)]
    public async Task Read_TextShapeOverLimit_RejectsWithBoundedConsumer(string content, int characters, int lines, int lineLength)
    {
        string path = Path.Combine(_directory, "limit.txt");
        await File.WriteAllTextAsync(path, content);
        await Assert.ThrowsAsync<IOException>(() => _files.ReadAsync(path, limits: new(Characters: characters, Lines: lines, LineLength: lineLength)));
    }

    [Fact]
    public async Task Read_CancelDuringChunks_StopsBeforeRemainingContent()
    {
        string path = Path.Combine(_directory, "cancel.log");
        await File.WriteAllTextAsync(path, string.Concat(Enumerable.Repeat("line\n", 30000)));
        using CancellationTokenSource cancellation = new();
        int chunks = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Run(() => _files.ReadChunks(path,
            _ => { chunks++; cancellation.Cancel(); }, cancellationToken: cancellation.Token)));
        Assert.Equal(1, chunks);
    }

    [Fact]
    public async Task Save_InvalidSurrogateInFinalChunk_PreservesOriginalAndRemovesTemporary()
    {
        string path = Path.Combine(_directory, "original.txt");
        await File.WriteAllTextAsync(path, "original");
        string fingerprint = await TextFileService.GetFingerprintAsync(path);
        await Assert.ThrowsAsync<EncoderFallbackException>(() => _files.SaveAsync(path,
            () => new StringReader(new string('a', 65535) + "\ud83d"), TextFileEncoding.Utf8, fingerprint));
        Assert.Equal("original", await File.ReadAllTextAsync(path));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Save_SurrogateSplitAtBufferBoundary_PreservesCharacter()
    {
        string path = Path.Combine(_directory, "emoji.txt");
        string text = new string('a', 65535) + "😀\n";
        await _files.SaveAsync(path, () => new StringReader(text), TextFileEncoding.Utf8, null);
        Assert.Equal(text, await File.ReadAllTextAsync(path));
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
