using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using WookText.Core.Pdf;

namespace WookText.App.Pdf;

public sealed class PdfDocumentView : Grid, IDisposable
{
    private readonly Canvas _page = new() { Background = Brushes.White, Focusable = true };
    private readonly Image _image = new();
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBox _pageNumber = new() { Width = 48, Text = "1", ToolTip = "페이지 번호 · Enter" };
    private readonly TextBlock _pageCount = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5) };
    private readonly TextBox _query = new() { Width = 150, ToolTip = "PDF 전체 텍스트 검색 · Enter" };
    private readonly CheckBox _matchCase = new() { Content = "대소문자", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4) };
    private int _mode;
    private readonly EditorPreferencesService _preferences;
    private readonly WrapPanel _searchBar = new() { Margin = new Thickness(6, 0, 6, 6), Visibility = Visibility.Collapsed };
    private readonly WrapPanel _objectTools = new() { Margin = new Thickness(6, 0, 6, 6), Visibility = Visibility.Collapsed };
    private readonly List<(MenuItem Item, int Mode)> _modeItems = [];
    private readonly TextBlock _status = new() { Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _zoomLabel = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5) };
    private readonly List<byte[]> _undo = [], _redo = [];
    private PdfFrame? _frame;
    private PdfObject? _selectedObject;
    private int _selectionStart = -1, _selectionEnd = -1;
    private int _anchorOffset, _anchorLength;
    private string? _savedFingerprint;
    private Point? _dragStart;
    private long _renderVersion;
    private bool _disposed;
    private bool _loadedOnce;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Button _retry = new() { Content = "PDF 다시 불러오기", Margin = new Thickness(8), Visibility = Visibility.Collapsed };
    public bool IsLoading { get; private set; }
    public bool IsReady { get; private set; }
    private PdfSearchHit[] _hits = [];
    private int _hitIndex = -1;
    private CancellationTokenSource? _searchCancellation;
    private readonly StackPanel _toolbar = new();
    public byte[] Bytes { get; private set; }
    public PdfInfo Info { get; private set; }
    public bool IsModified { get; private set; }
    public bool IsBusy { get; private set; }
    public int PageIndex { get; private set; }
    public double Zoom { get; private set; } = 1;
    public Task Rendering { get; private set; } = Task.CompletedTask;
    public event EventHandler? Changed;
    public string SelectedText => _frame is { } f && _selectionStart >= 0 ? f.Text.Substring(Math.Min(_selectionStart, _selectionEnd), Math.Abs(_selectionEnd - _selectionStart)) : "";
    private Window OwnerWindow => Window.GetWindow(this);

    public PdfDocumentView(byte[] bytes, PdfInfo? info, EditorPreferencesService preferences, bool modified = false, int page = 0, double zoom = 1)
    {
        _preferences = preferences;
        Bytes = bytes; Info = info ?? new(1, false, false, "PDF 복원 대기 중"); IsReady = info is not null; IsModified = modified; _savedFingerprint = modified ? null : PdfFileService.Fingerprint(bytes); PageIndex = info is null ? Math.Max(0, page) : Math.Clamp(page, 0, info.PageCount - 1); Zoom = double.IsFinite(zoom) ? Math.Clamp(zoom, .25, 4) : 1;
        RowDefinitions.Add(new() { Height = GridLength.Auto }); RowDefinitions.Add(new()); RowDefinitions.Add(new() { Height = GridLength.Auto });
        Children.Add(_toolbar);
        WrapPanel navigation = new() { Margin = new Thickness(6) };
        _toolbar.Children.Add(navigation);
        AddButton(navigation, "이전", () => NavigateAsync(PageIndex - 1));
        navigation.Children.Add(_pageNumber); navigation.Children.Add(_pageCount);
        AddButton(navigation, "다음", () => NavigateAsync(PageIndex + 1));
        AddButton(navigation, "−", () => SetZoomAsync(Zoom / 1.25)); navigation.Children.Add(_zoomLabel);
        AddButton(navigation, "+", () => SetZoomAsync(Zoom * 1.25));
        _zoomLabel.ToolTip = "확대율 · Ctrl+휠로 조절";
        AddButton(navigation, "찾기", () => { FocusSearch(); return Task.CompletedTask; });
        Menu menus = new() { VerticalAlignment = VerticalAlignment.Center };
        navigation.Children.Add(menus);
        MenuItem annotations = new() { Header = "주석" };
        MenuItem editing = new() { Header = "편집" };
        MenuItem pages = new() { Header = "페이지" };
        menus.Items.Add(annotations); menus.Items.Add(editing); menus.Items.Add(pages);
        AddAction(annotations, "선택한 글자에 형광펜", () => EditAsync(new() { Operation = PdfOperation.Highlight, Bytes = Bytes, Page = PageIndex, Boxes = SelectedGlyphs().Where(g => g.Bounds.Width > 0 && g.Bounds.Height > 0).Select(g => g.Bounds).ToArray() }), () => Info.CanEdit && SelectedText.Length > 0);
        AddMode(annotations, "메모 추가", 2); AddMode(annotations, "사각형 추가", 3); AddMode(annotations, "새 텍스트 추가", 4);
        annotations.Items.Add(new Separator()); AddMode(annotations, "텍스트 선택으로 돌아가기", 0);
        AddMode(editing, "텍스트 선택", 0); AddMode(editing, "기존 글자·이미지 선택", 1);
        AddAction(editing, "복사", () => { Copy(); return Task.CompletedTask; }, () => Info.CanCopy && SelectedText.Length > 0, "Ctrl+C");
        AddAction(editing, "선택 개체 수정…", EditSelectedAsync, () => Info.CanEdit && _selectedObject is not null);
        AddAction(editing, "선택 개체 삭제…", DeleteObjectAsync, () => Info.CanEdit && _selectedObject is not null);
        editing.Items.Add(new Separator());
        MenuItem ignoreFont = new() { Header = "원본 글꼴 무시 · 수정한 글자에 기본 글꼴", IsCheckable = true };
        editing.SubmenuOpened += (_, _) => ignoreFont.IsChecked = _preferences.Current.PdfIgnoreOriginalFont;
        ignoreFont.Click += (_, _) => SaveFontPreference(ignoreFont.IsChecked);
        editing.Items.Add(ignoreFont); editing.Items.Add(new Separator());
        AddAction(editing, "실행 취소", UndoAsync, () => _undo.Count > 0, "Ctrl+Z");
        AddAction(editing, "다시 실행", RedoAsync, () => _redo.Count > 0, "Ctrl+Y");
        AddAction(pages, "페이지 회전", () => EditAsync(new() { Operation = PdfOperation.Rotate, Bytes = Bytes, Page = PageIndex }), () => Info.CanEdit);
        AddAction(pages, "페이지 삭제…", DeletePageAsync, () => Info.CanEdit && Info.PageCount > 1);
        AddAction(pages, "앞으로 이동", () => MovePageAsync(-1), () => Info.CanEdit && PageIndex > 0);
        AddAction(pages, "뒤로 이동", () => MovePageAsync(1), () => Info.CanEdit && PageIndex < Info.PageCount - 1);
        AddAction(pages, "PDF 병합…", MergeAsync, () => Info.CanEdit);
        pages.Items.Add(new Separator()); AddAction(pages, "확대율 100%", () => SetZoomAsync(1));
        _toolbar.Children.Add(_searchBar);
        _searchBar.Children.Add(_query); _searchBar.Children.Add(_matchCase);
        AddButton(_searchBar, "찾기", SearchAsync);
        AddButton(_searchBar, "이전 결과", () => FindNextAsync(true)); AddButton(_searchBar, "다음 결과", () => FindNextAsync(false));
        AddButton(_searchBar, "닫기", () => { CloseSearch(); return Task.CompletedTask; });
        _toolbar.Children.Add(_objectTools);
        AddButton(_objectTools, "선택 개체 수정…", EditSelectedAsync); AddButton(_objectTools, "삭제…", DeleteObjectAsync);
        AddButton(_objectTools, "선택 모드 종료", () => { SetMode(0); return Task.CompletedTask; });
        _page.Children.Add(_image); _page.Children.Add(_overlay); _scroll.Content = _page;
        _page.HorizontalAlignment = HorizontalAlignment.Center; _page.VerticalAlignment = VerticalAlignment.Top;
        SetRow(_scroll, 1); Children.Add(_scroll);
        StackPanel footer = new(); footer.Children.Add(_status); footer.Children.Add(_retry);
        SetRow(footer, 2); Children.Add(footer);
        _retry.Click += (_, _) => Rendering = LoadInitialAsync();
        _pageNumber.KeyDown += async (_, e) => { if (e.Key == Key.Enter && int.TryParse(_pageNumber.Text, out int target)) { e.Handled = true; await GuardAsync(() => NavigateAsync(target - 1)); } };
        _query.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await GuardAsync(_hits.Length == 0 ? SearchAsync : () => FindNextAsync(Keyboard.Modifiers == ModifierKeys.Shift)); } else if (e.Key == Key.Escape) { e.Handled = true; CloseSearch(); } };
        _matchCase.Checked += (_, _) => InvalidateSearch();
        _matchCase.Unchecked += (_, _) => InvalidateSearch();
        _query.TextChanged += (_, _) => { _searchCancellation?.Cancel(); _hits = []; _hitIndex = -1; };
        _page.MouseLeftButtonDown += OnPointerDown; _page.MouseMove += OnPointerMove; _page.MouseLeftButtonUp += OnPointerUp;
        _page.LostMouseCapture += (_, _) => _dragStart = null;
        PreviewMouseWheel += async (_, e) => { if (Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; await GuardAsync(() => SetZoomAsync(Zoom * (e.Delta > 0 ? 1.25 : .8))); } };
        Loaded += (_, _) => { if (!_loadedOnce) { _loadedOnce = true; Rendering = LoadInitialAsync(); } };
    }

    private void AddButton(Panel panel, string label, Func<Task> action)
    {
        Button button = new() { Content = label, Margin = new Thickness(3, 0, 3, 0), Padding = new Thickness(7, 4, 7, 4) };
        button.Click += async (_, _) => await GuardAsync(action); panel.Children.Add(button);
    }

    private void AddAction(MenuItem parent, string label, Func<Task> action, Func<bool>? available = null, string shortcut = "")
    {
        MenuItem item = new() { Header = label, InputGestureText = shortcut };
        item.Click += async (_, e) => { e.Handled = true; await GuardAsync(action); };
        parent.SubmenuOpened += (_, _) => item.IsEnabled = !IsBusy && (available?.Invoke() ?? true);
        parent.Items.Add(item);
    }

    private void AddMode(MenuItem parent, string label, int mode)
    {
        MenuItem item = new() { Header = label, IsCheckable = true, IsChecked = mode == _mode };
        item.Click += (_, e) => { e.Handled = true; SetMode(mode); };
        parent.SubmenuOpened += (_, _) => item.IsEnabled = !IsBusy && (mode == 0 || Info.CanEdit);
        parent.Items.Add(item); _modeItems.Add((item, mode));
    }

    private void SetMode(int mode)
    {
        _mode = mode; ClearSelection();
        foreach (var entry in _modeItems) entry.Item.IsChecked = entry.Mode == mode;
        _status.Text = ModeHint;
        _page.Cursor = mode is 2 or 3 or 4 ? Cursors.Cross : mode == 1 ? Cursors.Hand : Cursors.IBeam;
    }

    private string ModeHint => Info.ReadOnlyReason ?? _mode switch
    {
        1 => "글자·이미지를 클릭해 선택한 뒤 수정하세요. · 편집 메뉴에서 텍스트 선택으로 복귀",
        2 => "메모를 넣을 위치를 클릭하세요. · 주석 메뉴에서 텍스트 선택으로 복귀",
        3 => "드래그하여 사각형을 그리세요. · 주석 메뉴에서 텍스트 선택으로 복귀",
        4 => "텍스트를 넣을 위치를 클릭하세요. · 주석 메뉴에서 텍스트 선택으로 복귀",
        _ => "드래그로 텍스트 선택 · 주석 / 편집 / 페이지 메뉴 · Ctrl+S 저장"
    };

    private void SaveFontPreference(bool value)
    {
        if (value != _preferences.Current.PdfIgnoreOriginalFont && !_preferences.Update(_preferences.Current with { PdfIgnoreOriginalFont = value }))
            _status.Text = _preferences.LastWarning;
    }

    private void InvalidateSearch() { _searchCancellation?.Cancel(); _hits = []; _hitIndex = -1; }
    private void CloseSearch() { InvalidateSearch(); _searchBar.Visibility = Visibility.Collapsed; ClearSelection(); _page.Focus(); _status.Text = ModeHint; }

    public async Task GuardAsync(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or System.Runtime.InteropServices.ExternalException)
        {
            if (_disposed) return;
            _status.Text = ex.Message;
            AppDialogs.Alert(OwnerWindow, "PDF 작업을 완료하지 못했습니다", ex.Message);
        }
    }

    private async Task LoadInitialAsync()
    {
        if (_disposed || IsLoading) return;
        IsLoading = true; IsReady = false; _toolbar.IsEnabled = false; _retry.Visibility = Visibility.Collapsed;
        _status.Text = "PDF를 불러오는 중… 다른 탭을 사용하거나 이 탭을 닫을 수 있습니다.";
        try
        {
            var loaded = await PdfBackend.OpenAsync(Bytes, PageIndex, Zoom, _lifetime.Token);
            if (_disposed) return;
            Info = loaded.Info; PageIndex = Math.Clamp(PageIndex, 0, Info.PageCount - 1);
            ShowFrame(loaded.Frame); IsReady = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            if (_disposed) return;
            _status.Text = "PDF를 불러오지 못했습니다. 원본과 보관한 내용은 유지했습니다. 다른 문서는 계속 사용할 수 있습니다.\n" + ex.Message;
            _retry.Visibility = Visibility.Visible;
        }
        finally { IsLoading = false; _toolbar.IsEnabled = IsReady; }
    }

    private void ShowFrame(PdfFrame frame)
    {
        _frame = frame;
        _pageNumber.Text = (PageIndex + 1).ToString(); _pageCount.Text = $"/ {Info.PageCount}"; _zoomLabel.Text = $"{Zoom:P0}";
        _image.Source = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, frame.Pixels, frame.Stride);
        _image.Width = _page.Width = _overlay.Width = frame.Width;
        _image.Height = _page.Height = _overlay.Height = frame.Height;
        DrawSelection(); _status.Text = ModeHint; _page.IsEnabled = !IsBusy;
    }

    public async Task RenderAsync()
    {
        if (_disposed) return;
        long version = ++_renderVersion;
        byte[] snapshot = Bytes; int page = PageIndex; double zoom = Zoom;
        _page.IsEnabled = false; _frame = null; _image.Source = null; _overlay.Children.Clear();
        try
        {
            PdfFrame frame = await PdfBackend.RenderAsync(snapshot, page, zoom, _lifetime.Token);
            if (_disposed || version != _renderVersion) return;
            ShowFrame(frame);
        }
        finally { if (!_disposed && version == _renderVersion) _page.IsEnabled = !IsBusy; }
    }

    public async Task NavigateAsync(int index)
    {
        if (!IsReady || IsBusy || index < 0 || index >= Info.PageCount) return;
        PageIndex = index; ClearSelection(); _scroll.ScrollToTop(); Changed?.Invoke(this, EventArgs.Empty);
        await (Rendering = RenderAsync());
    }

    public async Task SetZoomAsync(double zoom)
    {
        if (!IsReady || IsBusy) return;
        Zoom = Math.Clamp(zoom, .25, 4); Changed?.Invoke(this, EventArgs.Empty);
        await (Rendering = RenderAsync());
    }

    private async Task EditAsync(PdfRequest request)
    {
        if (!IsReady || IsBusy || _disposed) return;
        if (!Info.CanEdit) throw new IOException(Info.ReadOnlyReason);
        IsBusy = true; _toolbar.IsEnabled = false; _page.IsEnabled = false;
        _searchCancellation?.Cancel(); _hits = []; _hitIndex = -1;
        byte[] before = Bytes;
        try
        {
            PdfResponse edited = await PdfBackend.EditAsync(request, _lifetime.Token);
            if (_disposed) return;
            byte[] after = edited.Bytes!; PdfInfo info = edited.Info!;
            _undo.Add(before); TrimHistory(_undo); _redo.Clear();
            Bytes = after; Info = info; IsModified = true; PageIndex = Math.Min(PageIndex, info.PageCount - 1);
            ClearSelection(); Changed?.Invoke(this, EventArgs.Empty);
            await (Rendering = RenderAsync());
        }
        finally { IsBusy = false; _toolbar.IsEnabled = true; _page.IsEnabled = true; }
    }

    private static void TrimHistory(List<byte[]> history)
    {
        while (history.Count > 1 && (history.Count > 20 || history.Sum(b => (long)b.Length) > 64 * 1024 * 1024)) history.RemoveAt(0);
    }

    public Task UndoAsync() => HistoryAsync(_undo, _redo);
    public Task RedoAsync() => HistoryAsync(_redo, _undo);
    private async Task HistoryAsync(List<byte[]> source, List<byte[]> destination)
    {
        if (IsBusy || source.Count == 0) return;
        IsBusy = true; _toolbar.IsEnabled = false;
        try
        {
            byte[] next = source[^1];
            PdfInfo info = await PdfBackend.InspectAsync(next);
            source.RemoveAt(source.Count - 1); destination.Add(Bytes); TrimHistory(destination);
            Bytes = next; Info = info; IsModified = PdfFileService.Fingerprint(next) != _savedFingerprint; PageIndex = Math.Min(PageIndex, info.PageCount - 1);
            _searchCancellation?.Cancel(); _hits = []; ClearSelection(); Changed?.Invoke(this, EventArgs.Empty);
            await (Rendering = RenderAsync());
        }
        finally { IsBusy = false; _toolbar.IsEnabled = true; _page.IsEnabled = true; }
    }

    public void MarkSaved() { _savedFingerprint = PdfFileService.Fingerprint(Bytes); IsModified = false; Changed?.Invoke(this, EventArgs.Empty); }
    public void MarkModified() { _savedFingerprint = null; IsModified = true; Changed?.Invoke(this, EventArgs.Empty); }
    public void FocusSearch() { _searchBar.Visibility = Visibility.Visible; _query.Focus(); _query.SelectAll(); }
    public bool ExitTool()
    {
        if (_searchBar.Visibility == Visibility.Visible) { CloseSearch(); return true; }
        if (_mode != 0) { SetMode(0); return true; }
        return false;
    }
    public void Copy() { if (SelectedText.Length > 0 && Info.CanCopy) Clipboard.SetText(SelectedText); }

    public async Task SearchAsync()
    {
        if (!IsReady || IsBusy) return;
        _searchCancellation?.Cancel(); _searchCancellation?.Dispose(); _searchCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        CancellationToken token = _searchCancellation.Token;
        byte[] snapshot = Bytes; string query = _query.Text; bool matchCase = _matchCase.IsChecked == true;
        _status.Text = "PDF 전체에서 검색하는 중…";
        PdfSearchHit[] hits = await PdfBackend.SearchAsync(snapshot, query, matchCase, token);
        if (_disposed || token.IsCancellationRequested || snapshot != Bytes) return;
        _hits = hits; _hitIndex = -1;
        if (hits.Length == 0) { _status.Text = "검색 결과 없음 · 스캔 이미지는 OCR이 필요합니다."; return; }
        await FindNextAsync(false);
    }

    public async Task FindNextAsync(bool previous)
    {
        if (IsBusy || _hits.Length == 0) return;
        _hitIndex = (_hitIndex + (previous ? -1 : 1) + _hits.Length) % _hits.Length;
        PdfSearchHit hit = _hits[_hitIndex];
        byte[] snapshot = Bytes; PdfSearchHit[] hits = _hits;
        await NavigateAsync(hit.Page);
        if (_disposed || IsBusy || snapshot != Bytes || hits != _hits || PageIndex != hit.Page || _frame is null) return;
        _selectionStart = hit.Offset; _selectionEnd = hit.Offset + hit.Length;
        DrawSelection();
        if (SelectedGlyphs().FirstOrDefault() is { } glyph) _scroll.ScrollToVerticalOffset(Math.Max(0, glyph.ScreenBounds.Bottom - 80));
        _status.Text = $"검색 결과 {_hitIndex + 1} / {_hits.Length}";
    }

    private IEnumerable<PdfGlyph> SelectedGlyphs() => _frame?.Glyphs.Where(g => _selectionStart >= 0 && g.TextOffset >= Math.Min(_selectionStart, _selectionEnd) && g.TextOffset < Math.Max(_selectionStart, _selectionEnd)) ?? [];
    private void ClearSelection() { _selectionStart = _selectionEnd = -1; _selectedObject = null; _overlay.Children.Clear(); _objectTools.Visibility = Visibility.Collapsed; }
    private void DrawSelection()
    {
        _overlay.Children.Clear();
        foreach (PdfGlyph glyph in SelectedGlyphs()) DrawBox(glyph.ScreenBounds, Color.FromArgb(85, 50, 135, 255));
        if (_selectedObject is { } obj) DrawBox(obj.ScreenBounds, Color.FromArgb(65, 255, 135, 30));
    }
    private void DrawBox(PdfBox box, Color color)
    {
        Rectangle rectangle = new() { Width = Math.Max(1, box.Width), Height = Math.Max(1, box.Height), Fill = new SolidColorBrush(color), Stroke = Brushes.DarkOrange, StrokeThickness = .5 };
        Canvas.SetLeft(rectangle, box.Left); Canvas.SetTop(rectangle, box.Bottom); _overlay.Children.Add(rectangle);
    }
    private static bool Contains(PdfBox b, Point p) => p.X >= b.Left - 2 && p.X <= b.Right + 2 && p.Y >= b.Bottom - 2 && p.Y <= b.Top + 2;
    private PdfGlyph? ClosestGlyph(Point point) => _frame?.Glyphs.Where(g => g.ScreenBounds.Width > 0 && g.ScreenBounds.Height > 0)
        .MinBy(g => Math.Pow(Math.Clamp(point.X, g.ScreenBounds.Left, g.ScreenBounds.Right) - point.X, 2) + Math.Pow(Math.Clamp(point.Y, g.ScreenBounds.Bottom, g.ScreenBounds.Top) - point.Y, 2));

    private void OnPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (IsBusy || _frame is null) return;
        Point point = e.GetPosition(_page); _page.Focus(); ClearSelection();
        if (_mode == 0 && _frame.Notes.FirstOrDefault(n => Contains(n.ScreenBounds, point)) is { } note)
        { AppDialogs.Show(OwnerWindow, new("PDF 메모", note.Text)); return; }
        if (_mode == 1)
        {
            _selectedObject = _frame.Objects.LastOrDefault(o => Contains(o.ScreenBounds, point)); DrawSelection();
            _objectTools.Visibility = _selectedObject is not null && Info.CanEdit ? Visibility.Visible : Visibility.Collapsed;
            _status.Text = _selectedObject is { } obj ? (obj.Type == 1 ? "선택한 글자: " + obj.Text : "선택한 이미지 · 수정으로 이미지 교체 / 삭제") : "편집 가능한 글자·이미지를 선택하세요. 그룹 안 개체와 윤곽선 글자는 지원하지 않습니다.";
            return;
        }
        _dragStart = point;
        if (_mode == 0 && ClosestGlyph(point) is { } glyph) { _anchorOffset = glyph.TextOffset; _anchorLength = glyph.Text.Length; _selectionStart = _selectionEnd = glyph.TextOffset; }
        _page.CaptureMouse(); e.Handled = true;
    }
    private void OnPointerMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start || _frame is null) return;
        Point point = e.GetPosition(_page);
        if (_mode == 0)
        {
            if (ClosestGlyph(point) is { } glyph) { _selectionStart = Math.Min(_anchorOffset, glyph.TextOffset); _selectionEnd = Math.Max(_anchorOffset + _anchorLength, glyph.TextOffset + glyph.Text.Length); }
            DrawSelection();
        }
        else { _overlay.Children.Clear(); DrawBox(ScreenBox(start, point), Color.FromArgb(50, 255, 135, 30)); }
    }
    private async void OnPointerUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is not { } start || _frame is null) return;
        Point point = e.GetPosition(_page); _dragStart = null; _page.ReleaseMouseCapture();
        if (_mode == 0) return;
        await GuardAsync(async () =>
        {
            PdfBox screen = ScreenBox(start, point);
            byte[] snapshot = Bytes; int pageIndex = PageIndex, mode = _mode;
            int width = _frame.Width, height = _frame.Height;
            PdfBox box = await PdfBackend.FromScreenAsync(snapshot, pageIndex, screen, width, height, _lifetime.Token);
            if (_disposed || IsBusy || snapshot != Bytes || pageIndex != PageIndex || mode != _mode) return;
            switch (mode)
            {
                case 2:
                    PdfTextDialog note = new("메모 추가", multiline: true) { Owner = OwnerWindow };
                    if (note.ShowDialog() == true) { string value = note.Value; await EditAsync(new() { Operation = PdfOperation.Note, Bytes = Bytes, Page = PageIndex, Box = box, Text = value }); }
                    break;
                case 3:
                    if (box.Width < 2 || box.Height < 2) return;
                    await EditAsync(new() { Operation = PdfOperation.Rectangle, Bytes = Bytes, Page = PageIndex, Box = box }); break;
                case 4:
                    PdfTextDialog text = new("텍스트 추가 · 14pt Noto Sans KR") { Owner = OwnerWindow };
                    if (text.ShowDialog() == true) { string value = text.Value; await EditAsync(new() { Operation = PdfOperation.AddText, Bytes = Bytes, Page = PageIndex, Box = box, Text = value, Attachment = FontBytes() }); }
                    break;
            }
            _overlay.Children.Clear();
        });
    }
    private static PdfBox ScreenBox(Point a, Point b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    private static byte[] FontBytes() => File.ReadAllBytes(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSansKR-Regular.otf"));

    private async Task EditSelectedAsync()
    {
        if (_selectedObject is not { } obj) { _status.Text = "개체 편집 모드에서 글자·이미지를 선택하세요."; return; }
        if (obj.Type == 1)
        {
            PdfTextDialog dialog = new("기존 글자 수정", obj.Text, edit: true, ignoreOriginalFont: _preferences.Current.PdfIgnoreOriginalFont) { Owner = OwnerWindow };
            if (dialog.ShowDialog() == true) { string value = dialog.Value; bool replaceFont = dialog.ReplaceFont; SaveFontPreference(replaceFont); await EditAsync(new() { Operation = PdfOperation.ReplaceText, Bytes = Bytes, Page = PageIndex, Index = obj.Index, Text = value, Attachment = replaceFont ? FontBytes() : null }); }
        }
        else
        {
            OpenFileDialog dialog = new() { Title = "선택한 이미지 교체", Filter = "이미지 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dialog.ShowDialog(OwnerWindow) != true) return;
            if (new FileInfo(dialog.FileName).Length > 32 * 1024 * 1024) throw new IOException("이미지 파일은 32 MiB 이하로 선택하세요.");
            using FileStream stream = File.OpenRead(dialog.FileName);
            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            BitmapSource source = decoder.Frames[0];
            if (source.PixelWidth > 4096 || source.PixelHeight > 4096) throw new IOException("이미지는 최대 4,096 × 4,096 픽셀입니다.");
            FormatConvertedBitmap image = new(source, PixelFormats.Bgra32, null, 0);
            byte[] pixels = new byte[checked(image.PixelWidth * image.PixelHeight * 4)];
            image.CopyPixels(pixels, image.PixelWidth * 4, 0);
            int width = image.PixelWidth, height = image.PixelHeight;
            await EditAsync(new() { Operation = PdfOperation.ReplaceImage, Bytes = Bytes, Page = PageIndex, Index = obj.Index, Attachment = pixels, Width = width, Height = height });
        }
    }
    private async Task DeleteObjectAsync()
    {
        if (_selectedObject is not { } obj) return;
        if (AppDialogs.Show(OwnerWindow, new("선택 개체를 삭제할까요?", "선택한 글자 또는 이미지를 제거합니다. 실행 취소로 복원할 수 있습니다.", "삭제", CancelText: "취소")) == AppDialogResult.Primary)
            await EditAsync(new() { Operation = PdfOperation.DeleteObject, Bytes = Bytes, Page = PageIndex, Index = obj.Index });
    }
    private async Task DeletePageAsync()
    {
        if (AppDialogs.Show(OwnerWindow, new("페이지를 삭제할까요?", $"{PageIndex + 1}페이지를 삭제합니다. 실행 취소로 복원할 수 있습니다.", "삭제", CancelText: "취소")) == AppDialogResult.Primary)
            await EditAsync(new() { Operation = PdfOperation.DeletePage, Bytes = Bytes, Page = PageIndex });
    }
    private async Task MovePageAsync(int delta)
    {
        int target = PageIndex + delta;
        if (target < 0 || target >= Info.PageCount) return;
        await EditAsync(new() { Operation = PdfOperation.MovePage, Bytes = Bytes, Page = PageIndex, Index = target });
        await NavigateAsync(target);
    }
    private async Task MergeAsync()
    {
        OpenFileDialog dialog = new() { Title = "뒤에 붙일 PDF 선택", Filter = "PDF (*.pdf)|*.pdf" };
        if (dialog.ShowDialog(OwnerWindow) != true) return;
        LoadedPdfFile source = await PdfFileService.ReadAsync(dialog.FileName);
        await EditAsync(new() { Operation = PdfOperation.Merge, Bytes = Bytes, Attachment = source.Bytes });
    }
    public void Dispose() { _disposed = true; _lifetime.Cancel(); ++_renderVersion; _searchCancellation?.Cancel(); _searchCancellation?.Dispose(); }
}
