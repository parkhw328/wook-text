using System.IO;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;

namespace WookText.App;

public sealed record LicenseComponent(string Name, string License, string[] Files);

public partial class LicenseWindow : Window
{
    public static IReadOnlyList<LicenseComponent> Components { get; } =
    [
        new("wText", "MIT", ["LICENSE"]),
        new("구성요소 전체 고지", "배포 구성요소와 저작권", ["THIRD-PARTY-NOTICES.md"]),
        new("AvalonEdit", "MIT", ["licenses/AvalonEdit.txt"]),
        new("DiffPlex", "Apache-2.0", ["licenses/DiffPlex.txt"]),
        new("JetBrains Mono", "SIL OFL 1.1", ["licenses/JetBrainsMono-OFL.txt"]),
        new("Noto Sans KR", "SIL OFL 1.1", ["licenses/NotoSansKR-OFL.txt"]),
        new("Flexoki", "MIT", ["licenses/Flexoki-MIT.txt"]),
        new("NSIS", "zlib/libpng · 구성요소 고지", ["licenses/NSIS.txt"]),
        new(".NET / WPF", "MIT · 타사 구성요소 고지", ["licenses/Microsoft.NETCore.App-LICENSE.txt", "licenses/Microsoft.WindowsDesktop.App-LICENSE.txt", "licenses/Microsoft.NETCore.App-THIRD-PARTY-NOTICES.txt", "licenses/Microsoft.WindowsDesktop.App-THIRD-PARTY-NOTICES.txt", "licenses/WPF-THIRD-PARTY-NOTICES.txt", "licenses/WindowsForms-THIRD-PARTY-NOTICES.txt"])
    ];
    public TextEditor NoticeEditor { get; }
    public Task Loading { get; private set; } = Task.CompletedTask;
    private int _selection;

    public LicenseWindow()
    {
        InitializeComponent();
        NativeWindowTheme.Attach(this);
        NoticeEditor = EditorFactory.Create(readOnly: true);
        NoticeEditor.WordWrap = true;
        NoticeEditor.FontSize = 13;
        NoticeHost.Content = NoticeEditor;
        ComponentList.ItemsSource = Components;
        ComponentList.SelectedIndex = 0;
    }

    private void OnComponentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComponentList.SelectedItem is LicenseComponent component)
            Loading = ShowNoticeAsync(component, ++_selection);
    }

    private async Task ShowNoticeAsync(LicenseComponent component, int selection)
    {
        ComponentName.Text = component.Name;
        LicenseName.Text = component.License;
        NoticeEditor.Text = "고지 원문을 불러오는 중…";
        try
        {
            string text = await Task.Run(async () =>
            {
                List<string> notices = [];
                foreach (string relative in component.Files)
                {
                    string path = Path.Combine(AppContext.BaseDirectory, relative);
                    if (File.Exists(path)) notices.Add(await File.ReadAllTextAsync(path));
                }
                return notices.Count == 0 ? "고지 파일을 찾지 못했습니다. 배포본을 전체 압축 해제하거나 다시 설치해 주세요." : string.Join("\n\n────────────────────────────\n\n", notices);
            });
            if (selection == _selection) { NoticeEditor.Text = text; NoticeEditor.ScrollToHome(); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { if (selection == _selection) NoticeEditor.Text = "고지 파일을 읽지 못했습니다. " + ex.Message; }
    }
}
