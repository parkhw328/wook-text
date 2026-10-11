using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using DiffPlex.DiffBuilder.Model;
using ICSharpCode.AvalonEdit;
using Microsoft.Win32;
using WookText.Core;

namespace WookText.App;

public partial class CompareWindow : Window
{
    private readonly TextEditor _leftEditor;
    private readonly TextEditor _rightEditor;
    private readonly TextEditor _leftInput;
    private readonly TextEditor _rightInput;
    private readonly EditorPreferencesService _preferences;
    private readonly TextFileService _files = new();
    private LoadedTextFile? _left;
    private LoadedTextFile? _right;
    private Vector _leftScrollOffset;
    private Vector _rightScrollOffset;
    private bool _syncing;
    private bool _updatingInputs;
    private bool _busy;
    private bool _closed;
    private readonly List<int> _changeLines = [];
    private int _changeIndex = -1;
    public ComparisonResult? Result { get; private set; }

    public CompareWindow(EditorPreferencesService? preferences = null)
    {
        _preferences = preferences ?? new EditorPreferencesService();
        _leftEditor = EditorFactory.Create(readOnly: true, _preferences.Current);
        _rightEditor = EditorFactory.Create(readOnly: true, _preferences.Current);
        _leftInput = EditorFactory.Create(preferences: _preferences.Current);
        _rightInput = EditorFactory.Create(preferences: _preferences.Current);
        // Matching viewport heights keep aligned diff rows aligned at the bottom too.
        _leftEditor.HorizontalScrollBarVisibility = _rightEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
        InitializeComponent();
        NativeWindowTheme.Attach(this);
        foreach (TextEditor editor in new[] { _leftEditor, _rightEditor, _leftInput, _rightInput })
            EditorFactory.BindZoom(editor, _preferences);
        _preferences.Changed += OnPreferencesChanged;
        Closed += (_, _) => { _closed = true; _preferences.Changed -= OnPreferencesChanged; };
        LeftHost.Content = _leftEditor;
        RightHost.Content = _rightEditor;
        LeftInputHost.Content = _leftInput;
        RightInputHost.Content = _rightInput;
        _leftEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => Synchronize(_leftEditor, _rightEditor);
        _rightEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => Synchronize(_rightEditor, _leftEditor);
        _leftInput.TextChanged += OnInputChanged;
        _rightInput.TextChanged += OnInputChanged;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreferencesChanged(object? sender, EventArgs e)
    {
        foreach (TextEditor editor in new[] { _leftEditor, _rightEditor, _leftInput, _rightInput })
            EditorFactory.ApplyPreferences(editor, _preferences.Current);
    }

    public Task CompareFilesAsync(string left, string right) => LoadFilesAsync([left, right]);

    public async Task LoadFilesAsync(IReadOnlyList<string> paths, bool leftSide = true)
    {
        if (_busy || _closed) return;
        if (paths.Count is < 1 or > 2 || paths.Any(Directory.Exists))
            throw new ArgumentException("텍스트 파일을 한 개 또는 두 개 놓아 주세요. 폴더는 비교할 수 없습니다.");
        SetBusy(true);
        try
        {
            // Read and validate the whole drop before replacing either input.
            List<LoadedTextFile> loaded = [];
            foreach (string path in paths)
            {
                LoadedTextFile? file = await ReadFileAsync(path);
                if (file is null || _closed) return;
                TextDiffService.Validate(file.Text);
                loaded.Add(file);
            }
            _updatingInputs = true;
            try
            {
                if (loaded.Count == 2 || leftSide)
                {
                    _left = loaded[0];
                    ReplaceInput(_leftInput, _left.Text);
                }
                if (loaded.Count == 2 || !leftSide)
                {
                    _right = loaded[^1];
                    ReplaceInput(_rightInput, _right.Text);
                }
            }
            finally { _updatingInputs = false; }
            InvalidateComparison();
            if (loaded.Count == 2 || (leftSide ? _right is not null || _rightInput.Document.TextLength > 0 : _left is not null || _leftInput.Document.TextLength > 0))
                await RefreshComparisonAsync();
        }
        finally { SetBusy(false); }
    }

    private async Task<LoadedTextFile?> ReadFileAsync(string path)
    {
        try { return await _files.ReadAsync(path); }
        catch (DecoderFallbackException)
        {
            if (_closed || !AppDialogs.ConfirmEncoding(this, path)) return null;
            return await _files.ReadAsync(path, TextFileEncoding.Korean949);
        }
    }

    private async Task ChooseFileAsync(bool left)
    {
        if (_busy) return;
        OpenFileDialog dialog = new() { Title = left ? "원본 파일 선택" : "수정 파일 선택", Filter = "모든 파일 (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true) await LoadFilesFromUiAsync([dialog.FileName], left);
    }

    private async Task LoadFilesFromUiAsync(string[] paths, bool left)
    {
        try { await LoadFilesAsync(paths, left); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            if (!_closed) AppDialogs.Alert(this, "파일을 비교할 수 없습니다", "입력 내용은 유지됩니다. 파일과 비교 한도를 확인해 주세요.", ex.Message);
        }
    }

    public async Task CompareTextAsync()
    {
        if (_busy || _closed) return;
        SetBusy(true);
        try { await RefreshComparisonAsync(); }
        finally { SetBusy(false); }
    }

    private async Task RefreshComparisonAsync()
    {
        Summary.Text = "내용을 비교하는 중…";
        bool ignoreWhitespace = IgnoreWhitespace.IsChecked == true;
        string leftText = _leftInput.Text;
        string rightText = _rightInput.Text;
        ComparisonResult result;
        try { result = await Task.Run(() => new TextDiffService().Compare(leftText, rightText, ignoreWhitespace)); }
        catch (InvalidOperationException ex) { InvalidateComparison(); Summary.Text = ex.Message; return; }
        if (_closed) return;
        _syncing = true;
        try
        {
            _leftEditor.TextArea.TextView.LineTransformers.Clear();
            _rightEditor.TextArea.TextView.LineTransformers.Clear();
            _leftEditor.Text = string.Join('\n', result.Model.OldText.Lines.Select(DiffLineColorizer.Format));
            _rightEditor.Text = string.Join('\n', result.Model.NewText.Lines.Select(DiffLineColorizer.Format));
            _leftEditor.TextArea.TextView.LineTransformers.Add(new DiffLineColorizer(result.Model.OldText.Lines));
            _rightEditor.TextArea.TextView.LineTransformers.Add(new DiffLineColorizer(result.Model.NewText.Lines));
        }
        finally { _syncing = false; }
        Result = result;
        _changeLines.Clear();
        _changeIndex = -1;
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
        if (_left is not null && _right is not null && leftText == _left.Text && rightText == _right.Text && _left.Encoding != _right.Encoding)
            Summary.Text += "  · 인코딩 차이 있음";
        ResultTab.IsEnabled = true;
        ResultTab.IsSelected = true;
        _leftEditor.ScrollToHome();
        _rightEditor.ScrollToHome();
    }

    private void OnInputChanged(object? sender, EventArgs e)
    {
        if (!_updatingInputs) InvalidateComparison();
    }

    private void InvalidateComparison()
    {
        Result = null;
        _changeLines.Clear();
        _changeIndex = -1;
        InputTab.IsSelected = true;
        ResultTab.IsEnabled = false;
        PreviousButton.IsEnabled = NextButton.IsEnabled = false;
        Summary.Text = "비교하기 또는 Ctrl+Enter로 현재 입력 내용을 비교하세요. 빈 내용도 비교할 수 있습니다.";
        UpdateSource(_left, _leftInput, LeftPath, LeftHint);
        UpdateSource(_right, _rightInput, RightPath, RightHint);
    }

    private static void UpdateSource(LoadedTextFile? file, TextEditor input, TextBlock label, TextBlock hint)
    {
        label.Text = file is null ? input.Document.TextLength == 0 ? "직접 입력 / 붙여넣기 / 파일 드롭" : $"입력한 텍스트 · {input.Document.TextLength:N0}자"
            : Path.GetFileName(file.Path) + (input.Text == file.Text ? "" : " · 편집됨");
        label.ToolTip = file?.Path;
        hint.Visibility = input.Document.TextLength == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Synchronize(TextEditor source, TextEditor target)
    {
        Vector offset = source.TextArea.TextView.ScrollOffset;
        Vector previous = source == _leftEditor ? _leftScrollOffset : _rightScrollOffset;
        if (source == _leftEditor) _leftScrollOffset = offset; else _rightScrollOffset = offset;
        if (_syncing || Result is null) return;
        _syncing = true;
        try
        {
            // TextEditor offsets belong to ScrollViewer and lag TextView's event.
            // Use the live offsets and update IScrollInfo synchronously so the
            // reciprocal notification is covered by the guard, not queued later.
            IScrollInfo scroll = target.TextArea.TextView;
            // A vertical move in the shorter pane must not reset the other
            // pane's horizontal offset just because this pane cannot reach it.
            if (offset.Y != previous.Y)
                scroll.SetVerticalOffset(Math.Clamp(offset.Y, 0, Math.Max(0, scroll.ExtentHeight - scroll.ViewportHeight)));
            if (offset.X != previous.X)
                scroll.SetHorizontalOffset(Math.Clamp(offset.X, 0, Math.Max(0, scroll.ExtentWidth - scroll.ViewportWidth)));
            scroll.ScrollOwner?.InvalidateScrollInfo();
        }
        finally { _syncing = false; }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        LeftBrowse.IsEnabled = RightBrowse.IsEnabled = LeftPaste.IsEnabled = RightPaste.IsEnabled =
            LeftClear.IsEnabled = RightClear.IsEnabled = SwapButton.IsEnabled = IgnoreWhitespace.IsEnabled = CompareButton.IsEnabled = !busy;
        _leftInput.IsEnabled = _rightInput.IsEnabled = !busy;
        PreviousButton.IsEnabled = NextButton.IsEnabled = !busy && _changeLines.Count > 0;
    }

    private static void ReplaceInput(TextEditor editor, string text) => editor.Document.Replace(0, editor.Document.TextLength, text);

    private void Paste(bool left)
    {
        if (_busy) return;
        try
        {
            if (!Clipboard.ContainsText()) { Summary.Text = "클립보드에 텍스트가 없습니다. 텍스트를 복사한 뒤 다시 붙여넣어 주세요."; return; }
            TextEditor input = left ? _leftInput : _rightInput;
            ReplaceInput(input, Clipboard.GetText());
            InputTab.IsSelected = true;
            input.Focus();
        }
        catch (ExternalException ex) { AppDialogs.Alert(this, "붙여넣을 수 없습니다", "클립보드를 사용 중입니다. 잠시 후 다시 시도해 주세요.", ex.Message); }
    }

    private void OnFilesDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return; // Keep ordinary text dragging in the input editors.
        e.Effects = !_busy && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 or 2 } paths && !paths.Any(Directory.Exists)
            && (e.AllowedEffects & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnFilesDropped(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        e.Handled = true; // Do not let AvalonEdit paste the file's path into its document.
        e.Effects = DragDropEffects.None;
        if (_busy || (e.AllowedEffects & DragDropEffects.Copy) == 0) return;
        if (paths.Length is 1 or 2 && !paths.Any(Directory.Exists)) e.Effects = DragDropEffects.Copy;
        DependencyObject? origin = e.OriginalSource as DependencyObject;
        if (origin is FrameworkContentElement content) origin = content.Parent;
        bool right = origin is not null && new FrameworkElement[] { RightSource, RightInputHost, RightHost }.Any(element => element == origin || element.IsAncestorOf(origin));
        await LoadFilesFromUiAsync(paths, !right);
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            await CompareTextAsync();
        }
        else if (e.Key == Key.F7 && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift))
        {
            e.Handled = true;
            Navigate(Keyboard.Modifiers == ModifierKeys.Shift ? -1 : 1);
        }
    }

    private async void OnCompare(object sender, RoutedEventArgs e) => await CompareTextAsync();
    private async void OnChooseLeft(object sender, RoutedEventArgs e) => await ChooseFileAsync(true);
    private async void OnChooseRight(object sender, RoutedEventArgs e) => await ChooseFileAsync(false);
    private void OnPasteLeft(object sender, RoutedEventArgs e) => Paste(true);
    private void OnPasteRight(object sender, RoutedEventArgs e) => Paste(false);
    private void OnClearLeft(object sender, RoutedEventArgs e) { if (!_busy) ReplaceInput(_leftInput, ""); }
    private void OnClearRight(object sender, RoutedEventArgs e) { if (!_busy) ReplaceInput(_rightInput, ""); }
    private async void OnSwap(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _updatingInputs = true;
        try
        {
            (_left, _right) = (_right, _left);
            (_leftInput.Document, _rightInput.Document) = (_rightInput.Document, _leftInput.Document);
        }
        finally { _updatingInputs = false; }
        InvalidateComparison();
        await CompareTextAsync();
    }
    private async void OnIgnoreWhitespaceChanged(object sender, RoutedEventArgs e) => await CompareTextAsync();
    private void OnPreviousChange(object sender, RoutedEventArgs e) => Navigate(-1);
    private void OnNextChange(object sender, RoutedEventArgs e) => Navigate(1);
    private void Navigate(int direction)
    {
        if (_busy || _changeLines.Count == 0) return;
        ResultTab.IsSelected = true;
        _changeIndex = _changeIndex < 0 ? direction < 0 ? _changeLines.Count - 1 : 0
            : (_changeIndex + direction + _changeLines.Count) % _changeLines.Count;
        int line = _changeLines[_changeIndex];
        _leftEditor.TextArea.Caret.Line = _rightEditor.TextArea.Caret.Line = line;
        _leftEditor.ScrollTo(line, 1);
        _rightEditor.ScrollTo(line, 1);
    }
}
