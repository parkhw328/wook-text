using System.Diagnostics;
using System.IO;
using WookText.Core.Pdf;

namespace WookText.App.Pdf;

public static class PdfBackend
{
    private static readonly PdfWorkerClient Client = new(() =>
    {
        ProcessStartInfo start = new(Path.Combine(AppContext.BaseDirectory, "wText.exe"));
        start.ArgumentList.Add("--pdf-worker");
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        start.WorkingDirectory = AppContext.BaseDirectory;
        return start;
    });

    public static async Task<PdfInfo> InspectAsync(byte[] bytes) =>
        (await Client.ExecuteAsync(new() { Operation = PdfOperation.Inspect, Bytes = bytes })).Info ?? throw MissingResult();

    public static async Task<(PdfInfo Info, PdfFrame Frame)> OpenAsync(byte[] bytes, int page, double zoom, CancellationToken cancellationToken)
    {
        PdfInfo info = (await Client.ExecuteAsync(new() { Operation = PdfOperation.Inspect, Bytes = bytes }, cancellationToken)).Info ?? throw MissingResult();
        return (info, await RenderAsync(bytes, Math.Clamp(page, 0, info.PageCount - 1), zoom, cancellationToken));
    }

    public static async Task<PdfFrame> RenderAsync(byte[] bytes, int page, double zoom, CancellationToken cancellationToken) =>
        (await Client.ExecuteAsync(new() { Operation = PdfOperation.Render, Bytes = bytes, Page = page, Zoom = zoom }, cancellationToken)).Frame ?? throw MissingResult();

    public static async Task<PdfSearchHit[]> SearchAsync(byte[] bytes, string query, bool matchCase, CancellationToken cancellationToken) =>
        (await Client.ExecuteAsync(new() { Operation = PdfOperation.Search, Bytes = bytes, Text = query, MatchCase = matchCase }, cancellationToken)).Hits ?? throw MissingResult();

    public static async Task<PdfBox> FromScreenAsync(byte[] bytes, int page, PdfBox box, int width, int height, CancellationToken cancellationToken) =>
        (await Client.ExecuteAsync(new() { Operation = PdfOperation.FromScreen, Bytes = bytes, Page = page, Box = box, Width = width, Height = height }, cancellationToken)).Box ?? throw MissingResult();

    public static async Task<PdfResponse> EditAsync(PdfRequest request, CancellationToken cancellationToken)
    {
        PdfResponse response = await Client.ExecuteAsync(request, cancellationToken);
        if (response.Bytes is null || response.Info is null) throw MissingResult();
        return response;
    }

    private static IOException MissingResult() => new("PDF 처리 결과가 올바르지 않습니다. 문서 내용은 유지했습니다.");
}
