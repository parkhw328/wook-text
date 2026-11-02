using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using WookText.Core;

namespace WookText.App.Preview;

public sealed class DocumentPreviewView : DockPanel, IDisposable
{
    private sealed record FormatOption(PreviewKind Kind, string Label);
    private readonly EditorPreferencesService _preferences;
    private readonly string _profileFolder;
    public string? LastError { get; private set; }
    private readonly ComboBox _format = new() { MinWidth = 100, Margin = new Thickness(6) };
    private readonly TextBlock _status = new() { Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _message = new() { Margin = new Thickness(16), TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _notice = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _noticeActions = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly Grid _body = new();
    private WebView2? _browser;
    private bool _browserFailed;
    private Task? _initialization;
    private TaskCompletionSource<bool>? _navigation;
    private ulong _navigationId;
    private long _version;
    private bool _disposed;
    private string _text = "";
    private string? _path, _language;
    private Uri? _pageUri;
    private string? _resourceDocumentPath;
    private PreviewKind? _displayedKind;
    private string? _displayedPath;
    public PreviewContent? ContentSnapshot { get; private set; }
    public CoreWebView2? Browser => _browser?.CoreWebView2;
    public Task Rendering { get; private set; } = Task.CompletedTask;
    public PreviewKind Kind
    {
        get => _format.SelectedValue is PreviewKind kind ? kind : PreviewKind.Auto;
        set => _format.SelectedValue = Enum.IsDefined(value) ? value : PreviewKind.Auto;
    }
    public event EventHandler? Changed;

    public DocumentPreviewView(EditorPreferencesService preferences)
    {
        _preferences = preferences;
        _profileFolder = Path.Combine(Path.GetDirectoryName(preferences.StoragePath)!, "preview-webview2");
        WrapPanel header = new();
        header.Children.Add(new TextBlock { Text = "미리보기", Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center });
        _format.ItemsSource = Enum.GetValues<PreviewKind>().Select(k => new FormatOption(k, DocumentPreview.Label(k))).ToArray();
        _format.DisplayMemberPath = nameof(FormatOption.Label); _format.SelectedValuePath = nameof(FormatOption.Kind); Kind = PreviewKind.Auto;
        header.Children.Add(_format);
        Button refresh = new() { Content = "새로 고침", Margin = new Thickness(4) };
        refresh.Click += (_, _) => Rendering = RenderAsync(); header.Children.Add(refresh);
        SetDock(header, Dock.Top); Children.Add(header);
        SetDock(_status, Dock.Bottom); Children.Add(_status);
        _notice.Children.Add(_message); _notice.Children.Add(_noticeActions);
        _body.Children.Add(_notice); Children.Add(_body);
        _format.SelectionChanged += (_, _) => { Changed?.Invoke(this, EventArgs.Empty); Schedule(); };
        _timer.Tick += (_, _) => Rendering = RenderAsync();
        Loaded += (_, _) => Schedule(); Unloaded += (_, _) => _timer.Stop();
    }

    public void Update(string text, string? path, string? language = null)
    {
        _text = text; _path = path; _language = language; Schedule();
    }

    private void Schedule() { _timer.Stop(); if (!_disposed && IsLoaded) _timer.Start(); }

    public async Task RenderAsync()
    {
        _timer.Stop(); if (_disposed) return;
        long version = ++_version; LastError = null;
        if (!_preferences.Current.PreviewRuntimeLicenseAccepted) { ShowTerms(); return; }
        string text = _text; string? path = _path, language = _language; PreviewKind kind = Kind;
        try
        {
            PreviewContent content = await Task.Run(() => DocumentPreview.Build(text, path, kind, language));
            if (_disposed || version != _version) return;
            if (_browserFailed) ResetBrowser();
            await (_initialization ??= InitializeAsync());
            if (_disposed || version != _version) return;
            double[] scroll = [0, 0];
            if (_pageUri is not null && _displayedKind == content.Kind && _displayedPath == path)
            {
                // Only fixed host commands run; document scripts remain disabled.
                string position = await Browser!.ExecuteScriptAsync("[window.scrollX,window.scrollY]");
                if (JsonSerializer.Deserialize<double[]>(position) is { Length: 2 } saved && saved.All(double.IsFinite)) scroll = saved;
                if (_disposed || version != _version) return;
            }
            ContentSnapshot = content;
            _resourceDocumentPath = path;
            _pageUri = new Uri($"https://{Guid.NewGuid():N}.preview.wtext.invalid/index.html");
            _navigation?.TrySetResult(false); _navigation = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _notice.Visibility = Visibility.Collapsed; _browser!.Visibility = Visibility.Visible;
            _status.Text = DocumentPreview.Label(content.Kind) + " · 레이아웃 미리보기 · JavaScript·외부 리소스 제외";
            Browser!.Navigate(_pageUri.AbsoluteUri);
            bool loaded = await _navigation.Task.WaitAsync(TimeSpan.FromSeconds(30));
            if (_disposed || version != _version) return;
            if (!loaded) ShowError("미리보기를 표시하지 못했습니다. 새로 고침을 눌러 다시 시도하세요.");
            else
            {
                _displayedKind = content.Kind; _displayedPath = path;
                await Browser.ExecuteScriptAsync($"window.scrollTo(...{JsonSerializer.Serialize(scroll)})");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or XmlException or JsonException or TimeoutException or WebView2RuntimeNotFoundException or System.ComponentModel.Win32Exception or TypeInitializationException or TypeLoadException or System.Runtime.InteropServices.COMException)
        {
            if (_disposed || version != _version) return;
            if (_initialization?.IsFaulted == true)
            { _browserFailed = true; PreviewBrowserEnvironment.Reset(_profileFolder); }
            ShowError(ex.Message);
        }
    }

    private void ShowError(string message)
    {
        LastError = message;
        _pageUri = null; ContentSnapshot = null;
        // Stop can itself throw after ProcessFailed. Error display must not call into a dead browser.
        _navigation?.TrySetResult(false);
        HideBrowser(); _notice.Visibility = Visibility.Visible;
        _noticeActions.Children.Clear(); _message.Text = message; _status.Text = "원문은 계속 편집할 수 있습니다.";
    }

    private void ShowTerms()
    {
        HideBrowser(); _notice.Visibility = Visibility.Visible;
        _message.Text = "문서 미리보기에는 Microsoft Edge WebView2가 포함되어 있습니다. 사용 조건을 확인한 뒤 미리보기를 시작하세요. 이 구성요소는 사용 정보를 Microsoft에 보낼 수 있습니다.";
        _noticeActions.Children.Clear();
        Button terms = new() { Content = "사용 조건·개인정보 안내", Margin = new Thickness(6) };
        terms.Click += (_, _) => { LicenseWindow window = new() { Owner = Window.GetWindow(this) }; window.SelectComponent("Microsoft Edge WebView2 Runtime"); window.ShowDialog(); };
        Button accept = new() { Content = "동의하고 미리보기 열기", Margin = new Thickness(6) };
        accept.Click += (_, _) => { _preferences.Update(_preferences.Current with { PreviewRuntimeLicenseAccepted = true }); Rendering = RenderAsync(); };
        _noticeActions.Children.Add(terms); _noticeActions.Children.Add(accept);
        _status.Text = "이 선택은 설치형과 포터블에서 한 번만 확인합니다.";
    }

    private async Task InitializeAsync()
    {
        // Construct native-backed controls inside the asynchronous error boundary.
        WebView2 view = new() { AllowExternalDrop = false, DefaultBackgroundColor = System.Drawing.Color.White };
        _browser = view; _body.Children.Insert(0, view);
        CoreWebView2Environment environment = await PreviewBrowserEnvironment.GetAsync(_profileFolder);
        if (_disposed) return;
        CoreWebView2ControllerOptions options = environment.CreateCoreWebView2ControllerOptions(); options.IsInPrivateModeEnabled = true;
        await view.EnsureCoreWebView2Async(environment, options);
        if (_disposed) return;
        CoreWebView2 browser = Browser!;
        browser.Settings.IsScriptEnabled = false;
        browser.Settings.IsWebMessageEnabled = false;
        browser.Settings.AreHostObjectsAllowed = false;
        browser.Settings.AreDevToolsEnabled = false;
        browser.Settings.AreDefaultContextMenusEnabled = false;
        browser.Settings.AreDefaultScriptDialogsEnabled = false;
        browser.Settings.AreBrowserAcceleratorKeysEnabled = false;
        browser.Settings.IsBuiltInErrorPageEnabled = false;
        browser.Settings.IsGeneralAutofillEnabled = false;
        browser.Settings.IsPasswordAutosaveEnabled = false;
        browser.Settings.IsNonClientRegionSupportEnabled = false;
        browser.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
        browser.WebResourceRequested += OnResourceRequested;
        browser.NavigationStarting += (_, e) =>
        {
            if (_pageUri is not null && Uri.TryCreate(e.Uri, UriKind.Absolute, out Uri? uri) && SamePage(uri, _pageUri)) _navigationId = e.NavigationId;
            else { e.Cancel = true; OpenUserLink(e.Uri, e.IsUserInitiated && !e.IsRedirected); }
        };
        browser.NavigationCompleted += (_, e) => { if (e.NavigationId == _navigationId) _navigation?.TrySetResult(e.IsSuccess); };
        browser.NewWindowRequested += (_, e) => { e.Handled = true; OpenUserLink(e.Uri, e.IsUserInitiated); };
        browser.DownloadStarting += (_, e) => e.Cancel = true;
        browser.PermissionRequested += (_, e) => { e.State = CoreWebView2PermissionState.Deny; e.Handled = true; };
        browser.LaunchingExternalUriScheme += (_, e) => e.Cancel = true;
        browser.ProcessFailed += (_, _) =>
        {
            if (_disposed || _browser != view) return;
            _browserFailed = true; PreviewBrowserEnvironment.Reset(_profileFolder);
            ShowError("미리보기 프로세스가 종료되었습니다. 원문은 유지했습니다. 새로 고침으로 다시 시도하세요.");
        };
    }

    private static bool SamePage(Uri a, Uri b) => a.GetLeftPart(UriPartial.Path) == b.GetLeftPart(UriPartial.Path) && a.Query == b.Query;

    private void OpenUserLink(string value, bool userInitiated)
    {
        if (!userInitiated || MarkdownPreview.WebLink(value) is not { } uri || uri.Host.EndsWith(".preview.wtext.invalid", StringComparison.OrdinalIgnoreCase)) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { _status.Text = "링크를 열지 못했습니다. " + ex.Message; }
    }

    private async void OnResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        CoreWebView2Deferral? deferral = null;
        CoreWebView2? browser = null; Uri? page = _pageUri;
        try
        {
            browser = Browser;
            if (browser is null) return;
            deferral = e.GetDeferral();
            byte[]? bytes = null; string mime = "text/plain; charset=utf-8";
            if (!_disposed && page is not null && e.Request.Method == "GET" && Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out Uri? uri) && uri.Scheme == page.Scheme && uri.Authority == page.Authority)
            {
                if (SamePage(uri, page) && e.ResourceContext == CoreWebView2WebResourceContext.Document && ContentSnapshot is { } content)
                { bytes = Encoding.UTF8.GetBytes(content.Html); mime = "text/html; charset=utf-8"; }
                else if (e.ResourceContext is CoreWebView2WebResourceContext.Stylesheet or CoreWebView2WebResourceContext.Image or CoreWebView2WebResourceContext.Font)
                {
                    string? path = DocumentPreview.ResourcePath(_resourceDocumentPath, uri.AbsolutePath.TrimStart('/'));
                    if (path is not null)
                    {
                        // Bound the read even if a file grows between path validation and opening.
                        using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, true);
                        if (file.Length <= DocumentPreview.MaximumResourceBytes)
                        {
                            bytes = new byte[(int)file.Length]; await file.ReadExactlyAsync(bytes); mime = DocumentPreview.ResourceMime(path)!;
                        }
                    }
                }
            }
            if (_disposed) return;
            bool allowed = bytes is not null && page == _pageUri;
            e.Response = browser.Environment.CreateWebResourceResponse(new MemoryStream(allowed ? bytes! : []), allowed ? 200 : 403, allowed ? "OK" : "Blocked",
                $"Content-Type: {mime}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: {DocumentPreview.ContentSecurityPolicy}\r\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            try { if (!_disposed && !_browserFailed && browser is not null) e.Response = browser.Environment.CreateWebResourceResponse(new MemoryStream(), 403, "Blocked", "Cache-Control: no-store\r\n"); }
            catch (Exception failure) when (failure is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        }
        finally
        {
            // A tab may close while its local resource is being read.
            try { deferral?.Complete(); }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
            try { deferral?.Dispose(); }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
        }
    }

    private void HideBrowser()
    {
        try { if (_browser is not null) _browser.Visibility = Visibility.Collapsed; }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
    }

    private void ResetBrowser()
    {
        WebView2? old = _browser; _browser = null;
        _initialization = null; _browserFailed = false; _pageUri = null;
        _navigation?.TrySetResult(false);
        if (old is null) return;
        try { _body.Children.Remove(old); }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        try { old.Dispose(); }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; ++_version; _timer.Stop(); _pageUri = null; _navigation?.TrySetResult(false); ResetBrowser();
    }
}
