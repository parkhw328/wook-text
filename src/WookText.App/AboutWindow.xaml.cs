using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Navigation;

namespace WookText.App;

public partial class AboutWindow : Window
{
    public AboutWindow() { InitializeComponent(); NativeWindowTheme.Attach(this); }
    private void OnLicenses(object sender, RoutedEventArgs e) => new LicenseWindow { Owner = this }.ShowDialog();
    private void OnRepositoryNavigate(object sender, RequestNavigateEventArgs e)
    { e.Handled = true; AppInfo.OpenRepository(this); }
    private void OnCopyLink(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(AppInfo.RepositoryUrl); CopyLinkButton.Content = "복사했습니다"; }
        catch (ExternalException) { CopyLinkButton.Content = "복사하지 못했습니다. 다시 시도"; }
    }
}
