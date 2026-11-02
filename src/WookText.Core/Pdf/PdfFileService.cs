using System.Security.Cryptography;

namespace WookText.Core.Pdf;

public sealed record LoadedPdfFile(string Path, byte[] Bytes, string Fingerprint);

public static class PdfFileService
{
    public const int MaximumBytes = 32 * 1024 * 1024;

    public static async Task<LoadedPdfFile> ReadAsync(string path)
    {
        string fullPath = System.IO.Path.GetFullPath(path);
        await using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (stream.Length > MaximumBytes) throw new IOException("PDF 파일 한도는 32 MiB입니다.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes);
        return new(fullPath, bytes, Fingerprint(bytes));
    }

    public static string Fingerprint(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public static async Task<string> SaveAsync(string path, byte[] bytes, string? expectedFingerprint, Func<byte[], Task<PdfInfo>>? inspect = null)
    {
        if (bytes.Length > MaximumBytes) throw new IOException("PDF 파일 한도는 32 MiB입니다.");
        // Validate the complete candidate before touching the destination.
        _ = await (inspect is null ? Task.Run(() => PdfEngine.Inspect(bytes)) : inspect(bytes));
        string fullPath = System.IO.Path.GetFullPath(path);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await stream.WriteAsync(bytes);
                await stream.FlushAsync();
                stream.Flush(true);
            }
            if (File.Exists(fullPath))
            {
                if (expectedFingerprint is null || await TextFileService.GetFingerprintAsync(fullPath) != expectedFingerprint)
                    throw new FileConflictException("PDF 원본이 변경되었습니다. 다시 불러오거나 다른 이름으로 저장하세요.");
                File.Replace(temporary, fullPath, null);
            }
            else
            {
                if (expectedFingerprint is not null) throw new FileConflictException("PDF 원본이 삭제·이동되었습니다. 다른 이름으로 저장하세요.");
                File.Move(temporary, fullPath);
            }
            return Fingerprint(bytes);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
