using System.Diagnostics;
using WookText.Core.Pdf;
using WookText.Testing;

namespace WookText.Core.Tests;

public sealed class PdfWorkerTests
{
    private static PdfWorkerClient Client(string? mode = null, TimeSpan? timeout = null, string? pidFile = null) => new(() =>
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string configuration = Directory.GetParent(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))!.Name;
        string host = Path.Combine(root, "tests/WookText.PdfWorker.TestHost/bin", configuration, "net10.0/WookText.PdfWorker.TestHost.dll");
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "../../../", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        ProcessStartInfo start = new(dotnet);
        start.ArgumentList.Add(host);
        if (mode is not null) start.ArgumentList.Add(mode);
        if (pidFile is not null) start.ArgumentList.Add(pidFile);
        return start;
    }, timeout);

    [Fact]
    public async Task Execute_RealPdfWorker_RendersSearchesEditsAndPreservesOriginal()
    {
        PdfWorkerClient client = Client(); byte[] original = PdfFixture.Sample();
        PdfRequest request = new() { Operation = PdfOperation.Inspect, Bytes = original };
        Assert.Equal(2, (await client.ExecuteAsync(request)).Info!.PageCount);
        PdfFrame before = (await client.ExecuteAsync(request with { Operation = PdfOperation.Render })).Frame!;
        Assert.Contains("hello", before.Text);
        Assert.Equal(before.Height * before.Stride, before.Pixels.Length);
        Assert.Equal(2, (await client.ExecuteAsync(request with { Operation = PdfOperation.Search, Text = "hello" })).Hits!.Length);
        PdfResponse edit = await client.ExecuteAsync(request with { Operation = PdfOperation.Rotate });
        Assert.NotEqual(original, edit.Bytes);
        Assert.Equal(2, edit.Info!.PageCount);
        PdfFrame rotated = (await client.ExecuteAsync(request with { Operation = PdfOperation.Render, Bytes = edit.Bytes! })).Frame!;
        Assert.Equal(before.Width, rotated.Height);
        Assert.Contains("hello", PdfEngine.Render(original, 0, 1).Text);
    }

    [Theory]
    [InlineData("exit")]
    [InlineData("invalid")]
    public async Task Execute_WorkerExitsOrReturnsInvalidData_PreservesBufferAndAllowsNextRequest(string mode)
    {
        byte[] bytes = PdfFixture.Sample(); byte[] original = bytes.ToArray();
        PdfRequest request = new() { Operation = PdfOperation.Inspect, Bytes = bytes };
        await Assert.ThrowsAsync<IOException>(() => Client(mode, TimeSpan.FromSeconds(5)).ExecuteAsync(request));
        Assert.Equal(original, bytes);
        Assert.Equal(2, (await Client().ExecuteAsync(request)).Info!.PageCount);
    }

    [Fact]
    public async Task Execute_InvalidPdf_ReturnsAnErrorWithoutTerminatingCaller()
    {
        await Assert.ThrowsAsync<IOException>(() => Client().ExecuteAsync(new() { Operation = PdfOperation.Inspect, Bytes = "invalid pdf"u8.ToArray() }));
        Assert.Equal(1, (await Client().ExecuteAsync(new() { Operation = PdfOperation.Inspect, Bytes = PdfFixture.Sample(1) })).Info!.PageCount);
    }

    [Fact]
    public async Task Execute_TimedOutWorker_StopsChildProcess()
    {
        string pidFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pid");
        try
        {
            await Assert.ThrowsAsync<IOException>(() => Client("hang", TimeSpan.FromSeconds(2), pidFile).ExecuteAsync(new() { Operation = PdfOperation.Inspect, Bytes = PdfFixture.Sample(1) }));
            Assert.True(File.Exists(pidFile));
            AssertStopped(int.Parse(await File.ReadAllTextAsync(pidFile)));
        }
        finally { File.Delete(pidFile); }
    }

    [Fact]
    public async Task Execute_CancelledWorker_StopsChildAndPreservesCancellation()
    {
        string pidFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pid");
        using CancellationTokenSource cancellation = new();
        try
        {
            Task<PdfResponse> pending = Client("hang", TimeSpan.FromSeconds(10), pidFile).ExecuteAsync(new() { Operation = PdfOperation.Inspect, Bytes = PdfFixture.Sample(1) }, cancellation.Token);
            for (int i = 0; i < 200 && !File.Exists(pidFile); i++) await Task.Delay(10);
            Assert.True(File.Exists(pidFile));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            AssertStopped(int.Parse(await File.ReadAllTextAsync(pidFile)));
        }
        finally { cancellation.Cancel(); File.Delete(pidFile); }
    }

    private static void AssertStopped(int id)
    {
        try { using Process process = Process.GetProcessById(id); Assert.True(process.HasExited); }
        catch (ArgumentException) { }
    }
}
