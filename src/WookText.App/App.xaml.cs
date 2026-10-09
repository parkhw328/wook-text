using System.Windows;

namespace WookText.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainWindow window = new();
        MainWindow = window;
        window.Show();
        await window.InitializeSessionAsync();
        foreach (string path in e.Args)
        {
            if (System.IO.File.Exists(path)) await window.OpenFileAsync(path);
        }
    }
}
