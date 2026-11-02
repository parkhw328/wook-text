using System.Diagnostics;
using System.Text.Json;

namespace WookText.Core.Pdf;

public enum PdfOperation { Inspect, Render, Search, FromScreen, Rotate, DeletePage, MovePage, Merge, Highlight, Note, Rectangle, AddText, ReplaceText, ReplaceImage, DeleteObject }

public sealed record PdfRequest
{
    public PdfOperation Operation { get; init; }
    public required byte[] Bytes { get; init; }
    public int Page { get; init; }
    public double Zoom { get; init; } = 1;
    public int Index { get; init; }
    public string Text { get; init; } = "";
    public bool MatchCase { get; init; }
    public byte[]? Attachment { get; init; }
    public PdfBox Box { get; init; }
    public PdfBox[] Boxes { get; init; } = [];
    public int Width { get; init; }
    public int Height { get; init; }
    public float FontSize { get; init; } = 14;
}

public sealed record PdfResponse
{
    public string? Error { get; init; }
    public PdfInfo? Info { get; init; }
    public PdfFrame? Frame { get; init; }
    public PdfSearchHit[]? Hits { get; init; }
    public PdfBox? Box { get; init; }
    public byte[]? Bytes { get; init; }
}

/// <summary>One request per child process: native faults and timeouts cannot terminate the editor.</summary>
public static class PdfWorker
{
    public const int MaximumMessageBytes = 256 * 1024 * 1024;

    public static async Task<int> RunAsync(Stream input, Stream output)
    {
        PdfResponse response;
        try
        {
            using LimitedReadStream limited = new(input, MaximumMessageBytes);
            PdfRequest request = await JsonSerializer.DeserializeAsync<PdfRequest>(limited) ?? throw new IOException("PDF 요청이 비어 있습니다.");
            if (request.Bytes is not { Length: > 0 } || request.Bytes.Length > PdfFileService.MaximumBytes || !Enum.IsDefined(request.Operation))
                throw new IOException("PDF 요청 정보가 올바르지 않습니다.");
            response = Execute(request);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or NotSupportedException or JsonException or System.Runtime.InteropServices.ExternalException)
        { response = new() { Error = ex.Message }; }
        await JsonSerializer.SerializeAsync(output, response);
        await output.FlushAsync();
        return 0;
    }

    private static PdfResponse Execute(PdfRequest r)
    {
        if (r.Operation == PdfOperation.Inspect) return new() { Info = PdfEngine.Inspect(r.Bytes) };
        if (r.Operation == PdfOperation.Render) return new() { Frame = PdfEngine.Render(r.Bytes, r.Page, r.Zoom) };
        if (r.Operation == PdfOperation.Search) return new() { Hits = PdfEngine.Search(r.Bytes, r.Text, r.MatchCase) };
        if (r.Operation == PdfOperation.FromScreen) return new() { Box = PdfEngine.FromScreen(r.Bytes, r.Page, r.Box, r.Width, r.Height) };
        byte[] edited = r.Operation switch
        {
            PdfOperation.Rotate => PdfEngine.Rotate(r.Bytes, r.Page),
            PdfOperation.DeletePage => PdfEngine.DeletePage(r.Bytes, r.Page),
            PdfOperation.MovePage => PdfEngine.MovePage(r.Bytes, r.Page, r.Index),
            PdfOperation.Merge => PdfEngine.Merge(r.Bytes, r.Attachment!),
            PdfOperation.Highlight => PdfEngine.AddHighlight(r.Bytes, r.Page, r.Boxes),
            PdfOperation.Note => PdfEngine.AddNote(r.Bytes, r.Page, r.Box, r.Text),
            PdfOperation.Rectangle => PdfEngine.AddRectangle(r.Bytes, r.Page, r.Box),
            PdfOperation.AddText => PdfEngine.AddText(r.Bytes, r.Page, r.Box, r.Text, r.FontSize, r.Attachment!),
            PdfOperation.ReplaceText => PdfEngine.ReplaceText(r.Bytes, r.Page, r.Index, r.Text, r.Attachment),
            PdfOperation.ReplaceImage => PdfEngine.ReplaceImage(r.Bytes, r.Page, r.Index, r.Attachment!, r.Width, r.Height),
            PdfOperation.DeleteObject => PdfEngine.DeleteObject(r.Bytes, r.Page, r.Index),
            _ => throw new ArgumentOutOfRangeException(nameof(r.Operation))
        };
        return new() { Bytes = edited, Info = PdfEngine.Inspect(edited) };
    }
}

public sealed class PdfWorkerClient(Func<ProcessStartInfo> startInfo, TimeSpan? timeout = null)
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(60);

    public async Task<PdfResponse> ExecuteAsync(PdfRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProcessStartInfo start = startInfo();
        start.UseShellExecute = false; start.CreateNoWindow = true;
        start.RedirectStandardInput = start.RedirectStandardOutput = start.RedirectStandardError = true;
        using Process process = new() { StartInfo = start };
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        CancellationToken token = deadline.Token;
        Task? writing = null, exited = null, errors = null;
        Task<PdfResponse?>? reading = null;
        try
        {
            if (!process.Start()) throw new IOException("PDF 처리 프로세스를 시작하지 못했습니다.");
            // Start draining both pipes before writing a potentially large PDF request.
            errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null, token);
            using LimitedReadStream output = new(process.StandardOutput.BaseStream, PdfWorker.MaximumMessageBytes);
            reading = JsonSerializer.DeserializeAsync<PdfResponse>(output, cancellationToken: token).AsTask();
            exited = process.WaitForExitAsync(token);
            writing = WriteAsync();
            await Task.WhenAll(reading, writing, exited, errors).WaitAsync(token);
            if (process.ExitCode != 0) throw new IOException($"PDF 처리 프로세스가 종료되었습니다 (코드 {process.ExitCode}). 문서 내용은 유지했습니다.");
            PdfResponse response = await reading ?? throw new IOException("PDF 처리 결과가 없습니다. 문서 내용은 유지했습니다.");
            if (response.Error is { } message) throw new IOException(message);
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new IOException($"PDF 처리가 {_timeout.TotalSeconds:0}초 안에 끝나지 않아 중단했습니다. 문서 내용은 유지했습니다."); }
        catch (Exception ex) when (ex is JsonException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { throw new IOException("PDF 처리 프로세스가 응답하지 않거나 올바른 결과를 반환하지 못했습니다. 문서 내용은 유지했습니다.", ex); }
        finally
        {
            deadline.Cancel();
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                // The request's exit task may already be cancelled; wait independently for cleanup.
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException) { }
            // Observe pipe failures after a native crash/cancellation and release all handles.
            foreach (Task? task in new Task?[] { writing, reading, exited, errors })
                if (task is not null) try { await task; } catch (Exception) { }
        }

        async Task WriteAsync()
        {
            await JsonSerializer.SerializeAsync(process.StandardInput.BaseStream, request, cancellationToken: token);
            await process.StandardInput.BaseStream.FlushAsync(token);
            process.StandardInput.Close();
        }
    }
}

internal sealed class LimitedReadStream(Stream inner, long maximum) : Stream
{
    private long _read;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _read; set => throw new NotSupportedException(); }
    private int Count(int count) { if ((_read += count) > maximum) throw new IOException("PDF 처리 메시지가 허용 크기를 초과했습니다."); return count; }
    public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => Count(await inner.ReadAsync(buffer, cancellationToken));
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
