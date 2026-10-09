using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace WookText.App;

public static class AppInfo
{
    public const string Author = "Hyunwook Park";
    public const string RepositoryUrl = "https://github.com/parkhw328/wook-text";
    public static Uri RepositoryUri { get; } = new(RepositoryUrl);
    public static string Version { get; } = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
        .InformationalVersion.Split('+')[0];
    public static string VersionLabel => $"wText  ·  v{Version}";

    public static void OpenRepository(Window owner)
    {
        try { Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { AppDialogs.Alert(owner, "브라우저를 열지 못했습니다", "정보 창에서 GitHub 주소를 복사해 브라우저에 붙여 넣어 주세요.", ex.Message); }
    }
}
