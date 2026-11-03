using System.Security.Cryptography;
using System.Text;

namespace WookText.Core;

public sealed record LoadedTextFile(string Path, string Text, TextFileEncoding Encoding, string Fingerprint,
    long FileBytes = 0, int CharacterCount = 0, int LineCount = 1, string LineEndings = "줄바꿈 없음");

public sealed class FileConflictException(string message) : IOException(message);

public sealed class TextFileService
{
    public const int MaximumFileBytes = LargeFilePolicy.MaximumFileBytes;
    private const int BufferSize = 65536;

    public Task<LoadedTextFile> ReadAsync(string path, TextFileEncoding? explicitEncoding = null,
        CancellationToken cancellationToken = default, TextReadLimits? limits = null) => Task.Run(() =>
    {
        StringBuilder text = new();
        LoadedTextFile file = ReadChunks(path, chunk => text.Append(chunk), explicitEncoding, cancellationToken, limits);
        return file with { Text = text.ToString() };
    }, cancellationToken);

    // The consumer owns its buffer. Publish it only after decoding and validation succeed.
    // Call on a worker: decoding, hashing and building the line tree must not run on the dispatcher.
    public LoadedTextFile ReadChunks(string path, Action<string> append, TextFileEncoding? explicitEncoding = null,
        CancellationToken cancellationToken = default, TextReadLimits? limits = null)
    {
        limits ??= new();
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = System.IO.Path.GetFullPath(path);
        using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.SequentialScan);
        if (stream.Length > limits.Bytes) throw new IOException($"파일 열기 한도는 {limits.Bytes / (1024d * 1024):0.##} MiB입니다.");
        byte[] bytes = new byte[BufferSize];
        int count = stream.ReadAtLeast(bytes.AsSpan(0, 4), 4, throwOnEndOfStream: false);
        TextFileEncoding kind = explicitEncoding ?? DetectEncoding(bytes.AsSpan(0, count));
        Encoding encoding = kind.GetEncoding();
        byte[] preamble = encoding.GetPreamble();
        int skip = preamble.Length > 0 && bytes.AsSpan(0, count).StartsWith(preamble) ? preamble.Length : 0;
        Decoder decoder = encoding.GetDecoder();
        char[] chars = new char[encoding.GetMaxCharCount(BufferSize)];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long byteCount = 0;
        int characters = 0, lines = 1, lineLength = 0;
        bool pendingCr = false, crlf = false, cr = false, lf = false;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            byteCount += count;
            if (byteCount > limits.Bytes) throw new IOException("읽는 동안 파일이 열기 한도를 초과했습니다.");
            hash.AppendData(bytes, 0, count);
            int decoded = decoder.GetChars(bytes, skip, count - skip, chars, 0, flush: count == 0);
            characters += decoded;
            if (characters > limits.Characters) throw new IOException($"텍스트 열기 한도는 {limits.Characters:N0}자입니다.");
            for (int i = 0; i < decoded; i++)
            {
                char c = chars[i];
                if (c == '\0' || (char.IsControl(c) && c is not '\r' and not '\n' and not '\t' and not '\f'))
                    throw new IOException("바이너리 또는 지원하지 않는 제어 문자가 포함된 파일입니다.");
                if (pendingCr)
                {
                    if (c == '\n') { crlf = true; pendingCr = false; continue; }
                    cr = true;
                }
                pendingCr = c == '\r';
                if (c is '\r' or '\n')
                {
                    if (c == '\n') lf = true;
                    lineLength = 0;
                    if (++lines > limits.Lines) throw new IOException($"텍스트 열기 한도는 {limits.Lines:N0}줄입니다.");
                }
                else if (++lineLength > limits.LineLength)
                    throw new IOException($"한 줄이 {limits.LineLength:N0}자를 초과합니다. 파일을 나누거나 줄바꿈을 추가한 뒤 열어 주세요.");
            }
            if (decoded > 0) append(new string(chars, 0, decoded));
            skip = 0;
            if (count == 0) break;
            count = stream.Read(bytes);
        } while (true);
        cr |= pendingCr;
        string endings = (crlf ? 1 : 0) + (cr ? 1 : 0) + (lf ? 1 : 0) > 1 ? "혼합 줄바꿈"
            : crlf ? "CRLF" : lf ? "LF" : cr ? "CR" : "줄바꿈 없음";
        return new(fullPath, "", kind, Convert.ToHexString(hash.GetHashAndReset()), byteCount, characters, lines, endings);
    }

    public Task<string> SaveAsync(string path, string text, TextFileEncoding kind,
        string? expectedFingerprint, CancellationToken cancellationToken = default) =>
        SaveAsync(path, () => new StringReader(text), kind, expectedFingerprint, cancellationToken);

    // A snapshot reader streams the existing rope without flattening it into another large string.
    public Task<string> SaveAsync(string path, Func<TextReader> openReader, TextFileEncoding kind,
        string? expectedFingerprint, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        Encoding encoding = kind.GetEncoding();
        string fullPath = System.IO.Path.GetFullPath(path);
        string temporaryPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fullPath)!,
            $".{System.IO.Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (TextReader reader = openReader())
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, BufferSize, FileOptions.SequentialScan))
            {
                byte[] preamble = encoding.GetPreamble();
                stream.Write(preamble);
                hash.AppendData(preamble);
                Encoder encoder = encoding.GetEncoder();
                char[] chars = new char[BufferSize];
                byte[] bytes = new byte[encoding.GetMaxByteCount(chars.Length)];
                long total = preamble.Length;
                int count;
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    count = reader.Read(chars, 0, chars.Length);
                    int encoded = encoder.GetBytes(chars, 0, count, bytes, 0, flush: count == 0);
                    total += encoded;
                    if (total > MaximumFileBytes) throw new IOException("저장 한도는 256 MiB입니다. 원본은 유지했습니다.");
                    stream.Write(bytes, 0, encoded);
                    hash.AppendData(bytes, 0, encoded);
                } while (count != 0);
                stream.Flush(flushToDisk: true);
            }

            // All characters have been strictly encoded before the original can be replaced.
            if (File.Exists(fullPath))
            {
                string actualFingerprint = await GetFingerprintAsync(fullPath, cancellationToken).ConfigureAwait(false);
                if (expectedFingerprint is null || actualFingerprint != expectedFingerprint)
                    throw new FileConflictException("다른 프로그램에서 파일이 변경되었습니다. 다른 이름으로 저장하거나 파일을 다시 열어 주세요.");
                cancellationToken.ThrowIfCancellationRequested();
                File.Replace(temporaryPath, fullPath, null);
            }
            else
            {
                if (expectedFingerprint is not null)
                    throw new FileConflictException("원본 파일이 이동되거나 삭제되었습니다. 다른 이름으로 저장해 주세요.");
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, fullPath);
            }
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }, cancellationToken);

    public static async Task<string> GetFingerprintAsync(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static TextFileEncoding DetectEncoding(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xff, 0xfe, 0x00, 0x00 })) return TextFileEncoding.Utf32LittleEndian;
        if (bytes.StartsWith(new byte[] { 0x00, 0x00, 0xfe, 0xff })) return TextFileEncoding.Utf32BigEndian;
        if (bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) return TextFileEncoding.Utf8Bom;
        if (bytes.StartsWith(new byte[] { 0xff, 0xfe })) return TextFileEncoding.Utf16LittleEndian;
        if (bytes.StartsWith(new byte[] { 0xfe, 0xff })) return TextFileEncoding.Utf16BigEndian;
        return TextFileEncoding.Utf8;
    }
}
