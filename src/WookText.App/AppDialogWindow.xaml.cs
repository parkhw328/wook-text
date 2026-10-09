using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WookText.App;

public enum AppDialogResult { Cancel, Primary, Secondary }
public enum AppDialogKind { Information, Question, Warning, Error }
public sealed record AppDialogRequest(string Title, string Message, string PrimaryText = "확인",
    string? SecondaryText = null, string? CancelText = null, string? DocumentName = null,
    string? DocumentPath = null, string? Details = null, AppDialogKind Kind = AppDialogKind.Information);

public partial class AppDialogWindow : Window
{
    public AppDialogResult Result { get; private set; } = AppDialogResult.Cancel;

    public AppDialogWindow(AppDialogRequest request)
    {
        InitializeComponent();
        NativeWindowTheme.Attach(this);
        Title = request.Title;
        Heading.Text = request.Title;
        MessageText.Text = request.Message;
        PrimaryButton.Content = request.PrimaryText;
        SecondaryButton.Content = request.SecondaryText;
        SecondaryButton.Visibility = request.SecondaryText is null ? Visibility.Collapsed : Visibility.Visible;
        CancelButton.Content = request.CancelText;
        CancelButton.Visibility = request.CancelText is null ? Visibility.Collapsed : Visibility.Visible;
        DocumentCard.Visibility = request.DocumentName is null ? Visibility.Collapsed : Visibility.Visible;
        DocumentName.Text = request.DocumentName;
        DocumentPath.Text = request.DocumentPath ?? "아직 파일로 저장하지 않은 문서";
        DocumentCard.ToolTip = request.DocumentPath ?? request.DocumentName;
        DetailsText.Text = request.Details;
        DetailsSection.Visibility = string.IsNullOrEmpty(request.Details) ? Visibility.Collapsed : Visibility.Visible;
        KindLabel.Text = request.Kind switch
        {
            AppDialogKind.Question => "변경 내용 확인",
            AppDialogKind.Warning => "확인이 필요합니다",
            AppDialogKind.Error => "작업을 완료하지 못했습니다",
            _ => "알림"
        };
        if (request.Kind is AppDialogKind.Warning or AppDialogKind.Error)
            KindIcon.Data = Geometry.Parse("M 8,1 L 16,16 L 0,16 Z M 8,6 L 8,10 M 8,12 L 8,14");
        BodyScroll.MaxHeight = Math.Max(160, Math.Min(500, SystemParameters.WorkArea.Height - 180));
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Loaded += (_, _) => PrimaryButton.Focus();
    }

    private void OnPrimary(object sender, RoutedEventArgs e) { Result = AppDialogResult.Primary; Close(); }
    private void OnSecondary(object sender, RoutedEventArgs e) { Result = AppDialogResult.Secondary; Close(); }
    private void OnDismiss(object sender, RoutedEventArgs e) => Close();
    private void OnCaptionDrag(object sender, MouseButtonEventArgs e)
    {
        if (!DismissButton.IsMouseOver && e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}

public static class AppDialogs
{
    public static AppDialogRequest SaveRequest(string name, string? path) => new(
        "변경 내용을 저장할까요?", "저장하지 않고 닫으면 이 문서의 변경 내용은 사라집니다.",
        "저장", "저장 안 함", "계속 편집", name, path, Kind: AppDialogKind.Question);

    public static AppDialogResult Show(Window owner, AppDialogRequest request)
    {
        AppDialogWindow dialog = new(request) { Owner = owner };
        dialog.ShowDialog();
        return dialog.Result;
    }

    public static MessageBoxResult ConfirmSave(Window owner, string name, string? path) =>
        Show(owner, SaveRequest(name, path)) switch
        {
            AppDialogResult.Primary => MessageBoxResult.Yes,
            AppDialogResult.Secondary => MessageBoxResult.No,
            _ => MessageBoxResult.Cancel
        };

    public static bool ConfirmEncoding(Window owner, string path) => Show(owner, new(
        "다른 인코딩으로 열까요?", "UTF-8로 읽을 수 없는 파일입니다.\n한글 Windows 인코딩(CP949)으로 다시 열 수 있습니다.",
        "CP949로 열기", CancelText: "취소", DocumentName: System.IO.Path.GetFileName(path), DocumentPath: path,
        Kind: AppDialogKind.Warning)) == AppDialogResult.Primary;

    public static void Alert(Window owner, string title, string message, string? details = null) =>
        Show(owner, new(title, message, Details: details, Kind: AppDialogKind.Warning));
}
