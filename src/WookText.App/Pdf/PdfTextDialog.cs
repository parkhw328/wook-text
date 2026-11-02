using System.Windows;
using System.Windows.Controls;

namespace WookText.App.Pdf;

internal sealed class PdfTextDialog : Window
{
    private readonly TextBox _text;
    private readonly CheckBox _font;
    public string Value => _text.Text;
    public bool ReplaceFont => _font.IsChecked == true;

    public PdfTextDialog(string title, string value = "", bool edit = false, bool multiline = false, bool ignoreOriginalFont = true)
    {
        Title = title; Width = 540; Height = multiline ? 360 : 260; MinWidth = 360; MinHeight = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (System.Windows.Media.Brush)Application.Current.FindResource("Panel");
        Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("Bright");
        NativeWindowTheme.Attach(this);
        DockPanel panel = new() { Margin = new Thickness(20) };
        Content = panel;
        StackPanel buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Button apply = new() { Content = "적용", IsDefault = !multiline, MinWidth = 80, Margin = new Thickness(8) };
        Button cancel = new() { Content = "취소", IsCancel = true, MinWidth = 80, Margin = new Thickness(8) };
        buttons.Children.Add(apply); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        _font = new() { Content = "원본 글꼴 무시 · 수정한 글자에 기본 글꼴 사용", IsChecked = ignoreOriginalFont, ToolTip = "수정한 텍스트 개체에만 Noto Sans KR을 적용합니다. 다음 편집에도 이 설정을 사용합니다.", Visibility = edit ? Visibility.Visible : Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 10) };
        DockPanel.SetDock(_font, Dock.Bottom); panel.Children.Add(_font);
        TextBlock hint = new() { Text = edit ? "선택한 글자 개체를 수정합니다. 긴 문장은 주변 내용과 겹칠 수 있습니다." : title, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(hint, Dock.Top); panel.Children.Add(hint);
        _text = new() { Text = value, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 10000 };
        panel.Children.Add(_text);
        apply.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(Value)) DialogResult = true; };
        Loaded += (_, _) => { _text.Focus(); _text.SelectAll(); };
    }
}
