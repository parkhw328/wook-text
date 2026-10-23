using System.Windows.Input;

namespace WookText.App;

public static class EditorCommands
{
    public static RoutedUICommand SelectBlock { get; } = new("선택 영역을 직사각형 블록으로", nameof(SelectBlock), typeof(EditorCommands));
    public static RoutedUICommand CopyBlock { get; } = new("블록 복사", nameof(CopyBlock), typeof(EditorCommands));
    public static RoutedUICommand PasteBlock { get; } = new("블록 붙여넣기", nameof(PasteBlock), typeof(EditorCommands));
    public static RoutedUICommand Duplicate { get; } = new("줄 / 선택 복제", nameof(Duplicate), typeof(EditorCommands));
    public static RoutedUICommand MoveLinesUp { get; } = new("줄 위로 이동", nameof(MoveLinesUp), typeof(EditorCommands));
    public static RoutedUICommand MoveLinesDown { get; } = new("줄 아래로 이동", nameof(MoveLinesDown), typeof(EditorCommands));
    public static RoutedUICommand DeleteLines { get; } = new("줄 삭제", nameof(DeleteLines), typeof(EditorCommands));
    public static RoutedUICommand SelectLines { get; } = new("줄 선택", nameof(SelectLines), typeof(EditorCommands));

    public static RoutedUICommand FormatDocument { get; } = new("문서 자동 정렬", nameof(FormatDocument), typeof(EditorCommands));
    public static RoutedUICommand Reload { get; } = new("파일 다시 불러오기", nameof(Reload), typeof(EditorCommands));
    public static RoutedUICommand SaveAs { get; } = new("다른 이름으로 저장", nameof(SaveAs), typeof(EditorCommands));
    public static RoutedUICommand CloseTab { get; } = new("탭 닫기", nameof(CloseTab), typeof(EditorCommands));
    public static RoutedUICommand Compare { get; } = new("파일 · 텍스트 비교", nameof(Compare), typeof(EditorCommands));
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
