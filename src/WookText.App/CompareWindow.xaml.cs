using System.IO;
using System.Text;
using System.Windows;
using DiffPlex.DiffBuilder.Model;
using ICSharpCode.AvalonEdit;
using Microsoft.Win32;
using WookText.Core;

namespace WookText.App;

public partial class CompareWindow : Window
{
    private readonly TextEditor _leftEditor;
    private readonly TextEditor _rightEditor;
    private readonly EditorPreferencesService _preferences;
    private readonly TextFileService _files = new();
    private LoadedTextFile? _left;
    private LoadedTextFile? _right;
    private bool _syncing;
    private bool _busy;
    private readonly List<int> _changeLines = [];
    private int _changeIndex = -1;
    public ComparisonResult? Result { get; private set; }

    public CompareWindow(EditorPreferencesService? preferences = null)
    {
        _preferences = preferences ?? new EditorPreferencesService();
        _leftEditor = EditorFactory.Create(readOnly: true, _preferences.Current);
        _rightEditor = EditorFactory.Create(readOnly: true, _preferences.Current);
        InitializeComponent();
        NativeWindowTheme.Attach(this);
        EditorFactory.BindZoom(_leftEditor, _preferences);
        EditorFactory.BindZoom(_rightEditor, _preferences);
        _preferences.Changed += OnPreferencesChanged;
        Closed += (_, _) => _preferences.Changed -= OnPreferencesChanged;
        _leftEditor.ShowLineNumbers = _rightEditor.ShowLineNumbers = false;
        LeftHost.Content = _leftEditor;
        RightHost.Content = _rightEditor;
        _leftEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => Synchronize(_leftEditor, _rightEditor);
        _rightEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => Synchronize(_rightEditor, _leftEditor);
    }

    private void OnPreferencesChanged(object? sender, EventArgs e)
    {
        EditorFactory.ApplyPreferences(_leftEditor, _preferences.Current);
        EditorFactory.ApplyPreferences(_rightEditor, _preferences.Current);
    }

    public async Task CompareFilesAsync(string left, string right)
    {
        _left = await _files.ReadAsync(left);
        _right = await _files.ReadAsync(right);
        await RefreshComparisonAsync();
    }

    private async Task ChooseFileAsync(bool left)
    {
        if (_busy) return;
        OpenFileDialog dialog = new() { Title = left ? "원본 파일 선택" : "수정 파일 선택", Filter = "모든 파일 (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        SetBusy(true);
        try
        {
            LoadedTextFile file;
            try { file = await _files.ReadAsync(dialog.FileName); }
            catch (DecoderFallbackException)
            {
                if (MessageBox.Show(this, "UTF-8로 읽을 수 없습니다. CP949로 열까요?", "인코딩 선택", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                file = await _files.ReadAsync(dialog.FileName, TextFileEncoding.Korean949);
            }
            if (left) _left = file; else _right = file;
            await RefreshComparisonAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            ClearComparison();
            Summary.Text = "비교할 수 없습니다.";
            MessageBox.Show(this, ex.Message, "파일 비교", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private async Task RefreshComparisonAsync()
    {
        LeftPath.Text = _left is null ? "비교할 원본을 선택하세요" : Path.GetFileName(_left.Path);
        RightPath.Text = _right is null ? "비교할 수정본을 선택하세요" : Path.GetFileName(_right.Path);
        LeftPath.ToolTip = _left?.Path;
        RightPath.ToolTip = _right?.Path;
        ClearComparison();
        if (_left is null || _right is null) return;
        Summary.Text = "파일을 비교하는 중…";
        bool ignoreWhitespace = IgnoreWhitespace.IsChecked == true;
        string leftText = _left.Text;
        string rightText = _right.Text;
        ComparisonResult result = await Task.Run(() => new TextDiffService().Compare(leftText, rightText, ignoreWhitespace));
        Result = result;
        _leftEditor.Text = string.Join('\n', result.Model.OldText.Lines.Select(DiffLineColorizer.Format));
        _rightEditor.Text = string.Join('\n', result.Model.NewText.Lines.Select(DiffLineColorizer.Format));
        _leftEditor.TextArea.TextView.LineTransformers.Add(new DiffLineColorizer(result.Model.OldText.Lines));
        _rightEditor.TextArea.TextView.LineTransformers.Add(new DiffLineColorizer(result.Model.NewText.Lines));
        bool previousLineChanged = false;
        for (int i = 0; i < result.Model.NewText.Lines.Count; i++)
        {
            bool changed = result.Model.NewText.Lines[i].Type != ChangeType.Unchanged || result.Model.OldText.Lines[i].Type != ChangeType.Unchanged;
            if (changed && !previousLineChanged) _changeLines.Add(i + 1);
            previousLineChanged = changed;
        }
        Summary.Text = result.ExactMatch ? "텍스트 내용이 같습니다." :
            result.Added + result.Deleted + result.Modified == 0 ? "비교 옵션 기준으로 같은 내용입니다." :
            $"+ {result.Added:N0}줄 추가    − {result.Deleted:N0}줄 삭제    ~ {result.Modified:N0}줄 수정";
        if (result.LineEndingsDiffer) Summary.Text += "  · 줄바꿈 차이 있음";
        if (_left.Encoding != _right.Encoding) Summary.Text += "  · 인코딩 차이 있음";
        _leftEditor.ScrollToHome();
        _rightEditor.ScrollToHome();
    }

    private void ClearComparison()
    {
        Result = null;
        _changeLines.Clear();
        _changeIndex = -1;
        _leftEditor.TextArea.TextView.LineTransformers.Clear();
        _rightEditor.TextArea.TextView.LineTransformers.Clear();
        _leftEditor.Text = _rightEditor.Text = "";
    }

    private void Synchronize(TextEditor source, TextEditor target)
    {
        if (_syncing) return;
        _syncing = true;
        try { target.ScrollToVerticalOffset(source.VerticalOffset); target.ScrollToHorizontalOffset(source.HorizontalOffset); }
        finally { _syncing = false; }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        LeftBrowse.IsEnabled = RightBrowse.IsEnabled = SwapButton.IsEnabled = IgnoreWhitespace.IsEnabled = !busy;
    }

    private async Task RecompareAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try { await RefreshComparisonAsync(); }
        catch (InvalidOperationException ex) { ClearComparison(); Summary.Text = ex.Message; }
        finally { SetBusy(false); }
    }

    private async void OnChooseLeft(object sender, RoutedEventArgs e) => await ChooseFileAsync(true);
    private async void OnChooseRight(object sender, RoutedEventArgs e) => await ChooseFileAsync(false);
    private async void OnSwap(object sender, RoutedEventArgs e) { if (_busy) return; (_left, _right) = (_right, _left); await RecompareAsync(); }
    private async void OnIgnoreWhitespaceChanged(object sender, RoutedEventArgs e) => await RecompareAsync();
    private void OnPreviousChange(object sender, RoutedEventArgs e) => Navigate(-1);
    private void OnNextChange(object sender, RoutedEventArgs e) => Navigate(1);
    private void Navigate(int direction)
    {
        if (_changeLines.Count == 0) return;
        _changeIndex = _changeIndex < 0 ? direction < 0 ? _changeLines.Count - 1 : 0
            : (_changeIndex + direction + _changeLines.Count) % _changeLines.Count;
        int line = _changeLines[_changeIndex];
        _leftEditor.ScrollTo(line, 1);
        _rightEditor.ScrollTo(line, 1);
        _leftEditor.TextArea.Caret.Line = _rightEditor.TextArea.Caret.Line = line;
    }
}
