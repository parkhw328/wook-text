using System.Windows.Input;

namespace WookText.App;

public static class EditorCommands
{
    public static RoutedUICommand SaveAs { get; } = new("다른 이름으로 저장", nameof(SaveAs), typeof(EditorCommands));
    public static RoutedUICommand CloseTab { get; } = new("탭 닫기", nameof(CloseTab), typeof(EditorCommands));
    public static RoutedUICommand Compare { get; } = new("두 파일 비교", nameof(Compare), typeof(EditorCommands));
    public static RoutedUICommand OpenFolder { get; } = new("폴더 열기", nameof(OpenFolder), typeof(EditorCommands));
    public static RoutedUICommand FindFile { get; } = new("작업 폴더에서 파일 찾기", nameof(FindFile), typeof(EditorCommands));
    public static RoutedUICommand RevealFile { get; } = new("탐색기에서 현재 문서 찾기", nameof(RevealFile), typeof(EditorCommands));
    public static RoutedUICommand ToggleSidebar { get; } = new("사이드바 표시", nameof(ToggleSidebar), typeof(EditorCommands));
    public static RoutedUICommand ToggleFocus { get; } = new("집중 모드", nameof(ToggleFocus), typeof(EditorCommands));
    public static RoutedUICommand Preferences { get; } = new("글꼴 및 편집 설정", nameof(Preferences), typeof(EditorCommands));
    public static RoutedUICommand ZoomIn { get; } = new("글자 크게", nameof(ZoomIn), typeof(EditorCommands));
    public static RoutedUICommand ZoomOut { get; } = new("글자 작게", nameof(ZoomOut), typeof(EditorCommands));
    public static RoutedUICommand ResetZoom { get; } = new("기본 글자 크기", nameof(ResetZoom), typeof(EditorCommands));
}
