using System.Security.Cryptography;
using System.Text;

namespace WookText.Core;

public sealed record LoadedTextFile(string Path, string Text, TextFileEncoding Encoding, string Fingerprint);

public sealed class FileConflictException(string message) : IOException(message);

public sealed class TextFileService
{
    public const int MaximumFileBytes = 16 * 1024 * 1024;

    public async Task<LoadedTextFile> ReadAsync(string path, TextFileEncoding? explicitEncoding = null,
        CancellationToken cancellationToken = default)
    {
        string fullPath = System.IO.Path.GetFullPath(path);
        await using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > MaximumFileBytes)
        {
            throw new IOException("첫 버전에서는 16 MiB 이하의 텍스트 파일을 열 수 있습니다.");
        }

        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        TextFileEncoding kind = explicitEncoding ?? DetectEncoding(bytes);
        Encoding encoding = kind.GetEncoding();
        byte[] preamble = encoding.GetPreamble();
        int offset = preamble.Length > 0 && bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        string text = encoding.GetString(bytes, offset, bytes.Length - offset);
        if (text.Any(c => c == '\0' || (char.IsControl(c) && c is not '\r' and not '\n' and not '\t' and not '\f')))
        {
            throw new IOException("바이너리 또는 지원하지 않는 제어 문자가 포함된 파일입니다.");
        }

        return new LoadedTextFile(fullPath, text, kind, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public async Task<string> SaveAsync(string path, string text, TextFileEncoding kind,
        string? expectedFingerprint, CancellationToken cancellationToken = default)
    {
        // Encode first: unsupported characters must never truncate the original file.
        Encoding encoding = kind.GetEncoding();
        byte[] content = encoding.GetBytes(text);
        byte[] preamble = encoding.GetPreamble();
        if (content.Length + preamble.Length > MaximumFileBytes)
        {
            throw new IOException("첫 버전에서는 16 MiB 이하의 파일만 저장할 수 있습니다.");
        }

        string fullPath = System.IO.Path.GetFullPath(path);
        string parent = System.IO.Path.GetDirectoryName(fullPath)!;
        string temporaryPath = System.IO.Path.Combine(parent, $".{System.IO.Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(preamble, cancellationToken);
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(fullPath))
            {
                string actualFingerprint = await GetFingerprintAsync(fullPath, cancellationToken);
                if (expectedFingerprint is null || actualFingerprint != expectedFingerprint)
                {
                    throw new FileConflictException("다른 프로그램에서 파일이 변경되었습니다. 다른 이름으로 저장하거나 파일을 다시 열어 주세요.");
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Replace(temporaryPath, fullPath, null);
            }
            else
            {
                if (expectedFingerprint is not null)
                {
                    throw new FileConflictException("원본 파일이 이동되거나 삭제되었습니다. 다른 이름으로 저장해 주세요.");
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, fullPath);
            }

            byte[] complete = new byte[preamble.Length + content.Length];
            preamble.CopyTo(complete, 0);
            content.CopyTo(complete, preamble.Length);
            return Convert.ToHexString(SHA256.HashData(complete));
        }
        finally
        {
            // A cleanup failure should not hide the original save error.
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static async Task<string> GetFingerprintAsync(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static TextFileEncoding DetectEncoding(ReadOnlySpan<byte> bytes)
    {
        // UTF-32 LE starts with the UTF-16 LE marker, so check it first.
        if (bytes.StartsWith(new byte[] { 0xff, 0xfe, 0x00, 0x00 })) return TextFileEncoding.Utf32LittleEndian;
        if (bytes.StartsWith(new byte[] { 0x00, 0x00, 0xfe, 0xff })) return TextFileEncoding.Utf32BigEndian;
        if (bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) return TextFileEncoding.Utf8Bom;
        if (bytes.StartsWith(new byte[] { 0xff, 0xfe })) return TextFileEncoding.Utf16LittleEndian;
        if (bytes.StartsWith(new byte[] { 0xfe, 0xff })) return TextFileEncoding.Utf16BigEndian;
        return TextFileEncoding.Utf8;
    }
}
