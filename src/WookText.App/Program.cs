using System.IO;
using System.Diagnostics;
using System.Windows;
using WookText.Core.Pdf;

namespace WookText.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // The worker must branch before constructing Application or loading WPF resources.
        bool worker = args.Length == 2 && args[0] == "--pdf-worker" && int.TryParse(args[1], out _);
        using Mutex running = new(false, @"Local\wText.Running");
        if (Mutex.TryOpenExisting(@"Local\wText.Maintenance", out Mutex? maintenance))
        {
            maintenance.Dispose();
            if (!worker) MessageBox.Show("wText 설치 또는 제거가 진행 중입니다. 완료 후 다시 실행하세요.", "wText");
            return 3;
        }
        if (worker)
        {
            // Parent death must not leave a stuck native worker holding installed DLLs open.
            try
            {
                using Process parent = Process.GetProcessById(int.Parse(args[1]));
                _ = Task.Run(async () => { await parent.WaitForExitAsync(); Environment.Exit(2); });
                return PdfWorker.RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput()).GetAwaiter().GetResult();
            }
            catch (ArgumentException) { return 2; }
        }
        AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteCrashLog(e.ExceptionObject.ToString() ?? "Unknown startup error");
        try
        {
            App app = new(); app.InitializeComponent();
            app.DispatcherUnhandledException += (_, e) => WriteCrashLog(e.Exception.ToString());
            return app.Run();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex.ToString());
            MessageBox.Show("wText를 시작하지 못했습니다. 작업 보관 파일은 유지했습니다.\n오류 기록: %LocalAppData%\\wText\\logs\\startup-errors.log", "wText");
            return 1;
        }
    }

    private static void WriteCrashLog(string details)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wText", "logs");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "startup-errors.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.Move(path, path + ".previous", overwrite: true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} · {typeof(App).Assembly.GetName().Version}\n{details}\n\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
