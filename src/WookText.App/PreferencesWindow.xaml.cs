using System.Globalization;
using System.Windows;
using ICSharpCode.AvalonEdit;
using WookText.Core;

namespace WookText.App;

public partial class PreferencesWindow : Window
{
    private readonly EditorPreferences _original;
    private bool _ready;
    public TextEditor PreviewEditor { get; }
    public EditorPreferences? Result { get; private set; }

    public PreferencesWindow(EditorPreferences preferences)
    {
        _original = preferences;
        InitializeComponent();
        NativeWindowTheme.Attach(this);
        PreviewEditor = EditorFactory.Create(readOnly: true, preferences);
        PreviewEditor.Text = "// 한글과 English를 함께 편집합니다.\nconst message = \"안녕하세요, wText!\";\nconst count = 12345;\n\nfunction greet(name) {\n    return `반갑습니다, ${name}`;\n}";
        EditorFactory.ApplySyntax(PreviewEditor, "preview.js");
        PreviewHost.Content = PreviewEditor;
        EnglishFontBox.ItemsSource = KoreanFontBox.ItemsSource = EditorFonts.AvailableFamilies;
        Populate(preferences);
    }

    private void Populate(EditorPreferences preferences)
    {
        _ready = false;
        EnglishFontBox.SelectedItem = EditorFonts.AvailableFamilies.Contains(preferences.EnglishFont) ? preferences.EnglishFont : "JetBrains Mono";
        KoreanFontBox.SelectedItem = EditorFonts.AvailableFamilies.Contains(preferences.KoreanFont) ? preferences.KoreanFont : "Noto Sans KR";
        SizeBox.Text = preferences.FontSize.ToString(CultureInfo.CurrentCulture);
        LineNumbersBox.IsChecked = preferences.ShowLineNumbers;
        WordWrapBox.IsChecked = preferences.WordWrap;
        WhitespaceBox.IsChecked = preferences.ShowWhitespace;
        RememberSessionBox.IsChecked = preferences.RememberSession;
        _ready = true;
        RefreshPreview();
    }

    public bool TryGetPreferences(out EditorPreferences preferences)
    {
        preferences = _original;
        if (!EditorFonts.AvailableFamilies.Contains(EnglishFontBox.Text) || !EditorFonts.AvailableFamilies.Contains(KoreanFontBox.Text))
        { ValidationMessage.Text = "목록에 있는 글꼴을 선택해 주세요."; return false; }
        if (!double.TryParse(SizeBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double size) ||
            !double.IsFinite(size) || size < EditorPreferences.MinimumFontSize || size > EditorPreferences.MaximumFontSize)
        { ValidationMessage.Text = "글자 크기는 9에서 48 사이의 숫자로 입력해 주세요."; return false; }
        preferences = _original with
        {
            EnglishFont = EnglishFontBox.Text,
            KoreanFont = KoreanFontBox.Text,
            FontSize = size,
            ShowLineNumbers = LineNumbersBox.IsChecked == true,
            WordWrap = WordWrapBox.IsChecked == true,
            ShowWhitespace = WhitespaceBox.IsChecked == true,
            RememberSession = RememberSessionBox.IsChecked == true
        };
        ValidationMessage.Text = "다음 실행에도 이 설정을 사용합니다.";
        return true;
    }

    private void RefreshPreview()
    {
        if (!_ready || !TryGetPreferences(out EditorPreferences preferences)) return;
        EditorFactory.ApplyPreferences(PreviewEditor, preferences);
        PreviewEditor.ShowLineNumbers = preferences.ShowLineNumbers;
        PreviewEditor.WordWrap = preferences.WordWrap;
    }

    private void OnPreviewChanged(object sender, RoutedEventArgs e) => RefreshPreview();
    private void OnFontChanged(object sender, RoutedEventArgs e)
    {
        // ComboBox updates Text after SelectionChanged; preview the committed selection.
        if (_ready) Dispatcher.InvokeAsync(RefreshPreview, System.Windows.Threading.DispatcherPriority.Background);
    }
    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (!TryGetPreferences(out EditorPreferences preferences)) return;
        Result = preferences;
        DialogResult = true;
    }
    private void OnReset(object sender, RoutedEventArgs e) => Populate(new EditorPreferences());
    private void OnIncrease(object sender, RoutedEventArgs e) => Resize(1);
    private void OnDecrease(object sender, RoutedEventArgs e) => Resize(-1);
    private void Resize(int delta)
    {
        if (double.TryParse(SizeBox.Text, out double size))
            SizeBox.Text = Math.Clamp(size + delta, EditorPreferences.MinimumFontSize, EditorPreferences.MaximumFontSize).ToString(CultureInfo.CurrentCulture);
    }
}
