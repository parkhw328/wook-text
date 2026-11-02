using System.Diagnostics;
using System.IO;
using Microsoft.Web.WebView2.Core;

namespace WookText.App.Preview;

internal static class PreviewBrowserEnvironment
{
    private static readonly Dictionary<string, Task<CoreWebView2Environment>> Environments = new(StringComparer.OrdinalIgnoreCase);

    public static void Reset(string profile) => Environments.Remove(profile);

    public static async Task<CoreWebView2Environment> GetAsync(string profile)
    {
        if (!Environments.TryGetValue(profile, out Task<CoreWebView2Environment>? task))
            Environments[profile] = task = CreateAsync(profile);
        try { return await task; }
        catch { if (Environments.GetValueOrDefault(profile) == task) Environments.Remove(profile); throw; }
    }

    private static async Task<CoreWebView2Environment> CreateAsync(string profile)
    {
        string runtime = Path.Combine(AppContext.BaseDirectory, "WebView2Runtime");
        if (!File.Exists(Path.Combine(runtime, "msedgewebview2.exe")))
            throw new IOException("미리보기 구성요소를 찾지 못했습니다. 배포 ZIP 전체를 압축 해제하거나 wText를 다시 설치하세요.");
        // Fixed Version 120+ needs AppContainer read/execute ACLs on Windows 10.
        // Grant only this application's runtime directory; never weaken the browser sandbox.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            foreach (string sid in new[] { "S-1-15-2-2", "S-1-15-2-1" })
            {
                ProcessStartInfo start = new(Path.Combine(Environment.SystemDirectory, "icacls.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add(runtime); start.ArgumentList.Add("/grant"); start.ArgumentList.Add($"*{sid}:(OI)(CI)(RX)");
                using Process process = Process.Start(start) ?? throw new IOException("미리보기 폴더 권한을 준비하지 못했습니다.");
                Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(); await Task.WhenAll(stdout, stderr);
                if (process.ExitCode != 0) throw new IOException("미리보기 폴더에 읽기 권한을 설정하지 못했습니다. 현재 사용자 폴더에 압축을 해제하거나 다시 설치하세요.");
            }
        }
        CoreWebView2EnvironmentOptions options = new() { AreBrowserExtensionsEnabled = false };
        return await CoreWebView2Environment.CreateAsync(runtime, profile, options);
    }
}
