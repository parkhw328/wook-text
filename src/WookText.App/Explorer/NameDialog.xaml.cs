using System.IO;
using System.Windows;
using WookText.Core;

namespace WookText.App.Explorer;

public partial class NameDialog : Window
{
    private readonly Func<string, Task> _apply;
    private bool _applying;
    public NameDialog(string title, string parent, string initial, Func<string, Task> apply, bool selectStem = true)
    {
        InitializeComponent();
        NativeWindowTheme.Attach(this);
        Title = title + " — wText"; Heading.Text = title;
        ParentPath.Text = parent; ParentPath.ToolTip = parent;
        NameBox.Text = initial; _apply = apply;
        Closing += (_, e) => { if (_applying) e.Cancel = true; };
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            int dot = initial.LastIndexOf('.');
            NameBox.Select(0, selectStem && dot > 0 ? dot : initial.Length);
        };
    }

    private async void OnAccept(object sender, RoutedEventArgs e)
    {
        try
        {
            WorkspaceFiles.ValidateName(NameBox.Text);
            _applying = true;
            AcceptButton.IsEnabled = false;
            NameBox.IsEnabled = false;
            await _apply(NameBox.Text);
            _applying = false;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { ErrorMessage.Text = ex.Message; NameBox.Focus(); }
        finally { _applying = false; AcceptButton.IsEnabled = true; NameBox.IsEnabled = true; if (IsVisible) NameBox.Focus(); }
    }
}
