using System.Globalization;
using System.Windows;
using ICSharpCode.AvalonEdit;
using WookText.Core;

namespace WookText.App;

public partial class PreferencesWindow : Window
{
    private readonly EditorPreferences _original;
    private bool _ready;
    private bool _loadingIndent;
    private string _indentKey = "default";
    private IndentationOptions _defaultIndent = new();
    private IndentationProfiles _indentProfiles = new();
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
        IndentSizeBox.ItemsSource = Enumerable.Range(1, 8);
        IndentLanguageBox.ItemsSource = new[] { new IndentLanguage("default", "공통 기본값") }
            .Concat(SyntaxCatalog.Languages.Select(language => new IndentLanguage(language.Id, language.Label)))
            .Concat(new[] { new IndentLanguage("jsonc", "JSONC"), new IndentLanguage("vue", "Vue"), new IndentLanguage("scss", "SCSS"), new IndentLanguage("less", "Less") }).ToArray();
        Populate(preferences);
    }

    private void Populate(EditorPreferences preferences)
    {
        _ready = false;
        _defaultIndent = preferences.Indentation;
        _indentProfiles = new();
        foreach (var pair in preferences.IndentationByLanguage) _indentProfiles[pair.Key] = pair.Value;
        _indentKey = "default";
        IndentLanguageBox.SelectedValue = "default";
        LoadIndentControls();
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
            RememberSession = RememberSessionBox.IsChecked == true,
            Indentation = _defaultIndent,
            IndentationByLanguage = new IndentationProfiles()
        };
        foreach (var pair in _indentProfiles) preferences.IndentationByLanguage[pair.Key] = pair.Value;
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
    private sealed record IndentLanguage(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private void OnIndentLanguageChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || IndentLanguageBox.SelectedValue is not string key) return;
        _indentKey = key;
        LoadIndentControls();
    }

    private void LoadIndentControls()
    {
        _loadingIndent = true;
        bool isDefault = _indentKey == "default";
        bool overridden = _indentProfiles.TryGetValue(_indentKey, out IndentationOptions? options);
        options ??= _defaultIndent;
        IndentOverrideBox.IsEnabled = !isDefault;
        IndentOverrideBox.IsChecked = !isDefault && overridden;
        IndentOptionsPanel.IsEnabled = isDefault || overridden;
        DetectIndentBox.IsChecked = options.DetectFromDocument;
        IndentStyleBox.IsEnabled = _indentKey != "yaml";
        IndentStyleBox.SelectedIndex = options.UseTabs && _indentKey != "yaml" ? 1 : 0;
        IndentSizeBox.SelectedItem = options.Size;
        _loadingIndent = false;
        ShowIndentPreview();
    }

    private void OnIndentOverrideChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || _indentKey == "default") return;
        if (IndentOverrideBox.IsChecked == true) _indentProfiles[_indentKey] = _defaultIndent;
        else _indentProfiles.Remove(_indentKey);
        LoadIndentControls();
    }

    private void OnIndentOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || _loadingIndent || IndentSizeBox.SelectedItem is not int size) return;
        IndentationOptions options = new() { Size = size, UseTabs = IndentStyleBox.SelectedIndex == 1 && _indentKey != "yaml", DetectFromDocument = DetectIndentBox.IsChecked == true };
        if (_indentKey == "default") _defaultIndent = options;
        else if (IndentOverrideBox.IsChecked == true) _indentProfiles[_indentKey] = options;
        ShowIndentPreview();
    }

    private void ShowIndentPreview()
    {
        int size = IndentSizeBox.SelectedItem is int selected ? selected : 4;
        string indent = new(' ', size);
        IndentPreview.Text = "{\n" + indent + "\"editor\": {\n" + indent + indent + "\"indent\": " + size + "\n" + indent + "}\n}";
    }
    private void OnIncrease(object sender, RoutedEventArgs e) => Resize(1);
    private void OnDecrease(object sender, RoutedEventArgs e) => Resize(-1);
    private void Resize(int delta)
    {
        if (double.TryParse(SizeBox.Text, out double size))
            SizeBox.Text = Math.Clamp(size + delta, EditorPreferences.MinimumFontSize, EditorPreferences.MaximumFontSize).ToString(CultureInfo.CurrentCulture);
    }
}
