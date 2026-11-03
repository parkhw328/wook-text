using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WookText.App;
using WookText.Core;
using WookText.Core.Pdf;
using WookText.Testing;

namespace WookText.App.SmokeTests;

internal static class PdfPreviewChecks
{
    public static async Task RunAsync(string output)
    {
        string directory = Path.Combine(output, "fixtures", "pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string pdfPath = Path.Combine(directory, "review.pdf");
        await File.WriteAllBytesAsync(pdfPath, PdfFixture.Sample());
        string settingsPath = Path.Combine(directory, "settings.json");
        EditorPreferencesService preferences = new(new PreferencesStore(settingsPath));
        preferences.Update(preferences.Current with { PreviewRuntimeLicenseAccepted = true });
        MainWindow window = new(preferences);
        Program.ShowOffscreen(window);
        await window.InitializeSessionAsync();
        await window.OpenFileAsync(pdfPath);
        EditorDocument tab = window.ActiveTab!;
        Check(tab.IsPdf && window.ActiveDocument is null && tab.Editor.IsReadOnly, "PDF must bypass text decoding and text editing commands.");
        Check(!EditorCommands.GoToLine.CanExecute(null, window), "Text line navigation must be disabled for PDF tabs.");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var pdf = tab.PdfView!;
        await pdf.Rendering;
        Check(pdf.Info.PageCount == 2, "Both PDF pages must be available.");
        await window.OpenFileAsync(pdfPath);
        Check(window.Documents.Count == 1, "Opening the same PDF must reuse its tab.");
        ApplicationCommands.Find.Execute(null, window);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(Keyboard.FocusedElement is TextBox, "PDF find must focus its own search field.");
        ((TextBox)Keyboard.FocusedElement).Text = "hello";
        await pdf.SearchAsync();
        Check(pdf.SelectedText == "hello", "PDF search must select text for copying.");
        pdf.Copy(); Check(Clipboard.GetText() == "hello", "PDF copy must use extracted text.");
        await pdf.FindNextAsync(false); Check(pdf.PageIndex == 1, "Search must navigate across pages.");
        await pdf.SetZoomAsync(1.25);
        byte[] original = pdf.Bytes;
        Check(Descendants<Button>(pdf).Count(b => b.IsVisible) <= 11, "PDF must show only navigation and the active search tools.");
        Menu menu = Descendants<Menu>(pdf).Single();
        MenuItem editing = menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "편집"));
        MenuItem font = editing.Items.OfType<MenuItem>().Single(i => i.IsCheckable && i.Header.ToString()!.StartsWith("원본 글꼴 무시"));
        editing.IsSubmenuOpen = true;
        Check(font.IsChecked, "Ignoring original PDF fonts must default to enabled.");
        font.IsChecked = false; font.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(!new PreferencesStore(settingsPath).Load(out _).PdfIgnoreOriginalFont, "The PDF font option must persist.");
        font.IsChecked = true; font.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        editing.IsSubmenuOpen = false;
        MenuItem pages = menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "페이지"));
        MenuItem rotate = pages.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "페이지 회전"));
        rotate.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await WaitAsync(() => !pdf.IsBusy);
        Check(tab.IsModified && !pdf.Bytes.SequenceEqual(original), "Page rotation must mark the PDF tab modified.");
        await pdf.UndoAsync(); Check(pdf.Bytes.SequenceEqual(original) && !tab.IsModified, "Undo must restore the original PDF and saved state.");
        await pdf.RedoAsync(); Check(tab.IsModified, "Redo must restore the page edit.");
        ApplicationCommands.Save.Execute(null, window);
        await WaitAsync(() => !tab.IsModified);
        Check((await File.ReadAllBytesAsync(pdfPath)).SequenceEqual(pdf.Bytes), "Ctrl+S must save PDF bytes, never the placeholder editor text.");
        pdf.ExitTool();
        await Program.RenderAsync(window, Path.Combine(output, "pdf-reader.png"), 1200, 800);
        await Program.RenderAsync(window, Path.Combine(output, "pdf-reader-minimum.png"), 900, 600);

        EditorDocument markdown = window.NewDocument();
        markdown.Editor.Text = "# 검토 노트\n\nPDF와 함께 작성하는 **Markdown** 문서입니다.\n\n- [x] PDF 확인\n- [ ] 피드백 작성\n\n| 항목 | 상태 |\n|---|---|\n| 읽기 | 완료 |\n\n```json\n{ \"preview\": true }\n```\n";
        EditorCommands.DocumentPreview.Execute(null, window);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await markdown.PreviewView!.RenderAsync();
        string rendered = JsonSerializer.Deserialize<string>(await markdown.PreviewView.Browser!.ExecuteScriptAsync("document.body.innerText"))!;
        Check(rendered.Contains("검토 노트") && rendered.Contains("피드백"), "Markdown must render the current unsaved buffer.");
        markdown.Editor.AppendText(string.Concat(Enumerable.Repeat("\n\n스크롤 확인 줄", 70)));
        await markdown.PreviewView.RenderAsync();
        await markdown.PreviewView.Browser!.ExecuteScriptAsync("window.scrollTo(0,400)");
        markdown.Editor.AppendText("\n새 내용");
        await markdown.PreviewView.RenderAsync();
        Check(JsonSerializer.Deserialize<string>(await markdown.PreviewView.Browser!.ExecuteScriptAsync("document.body.innerText"))!.Contains("새 내용"), "Preview must track live edits.");
        Check(JsonSerializer.Deserialize<double>(await markdown.PreviewView.Browser!.ExecuteScriptAsync("window.scrollY")) >= 350, "Live preview must retain the reader's scroll position.");
        await markdown.PreviewView.Browser.ExecuteScriptAsync("window.scrollTo(0,0)");
        await Program.RenderAsync(window, Path.Combine(output, "markdown-preview.png"), 1200, 800);
        string htmlPath = Path.Combine(directory, "layout.html");
        await File.WriteAllTextAsync(Path.Combine(directory, "layout.css"), "#layout{display:grid;grid-template-columns:1fr 1fr;color:rgb(218,112,44)}");
        await File.WriteAllTextAsync(htmlPath, "<link rel='stylesheet' href='layout.css'><main id='layout'><h1>HTML 미리보기</h1><p>CSS 배치</p></main><script>document.body.innerHTML='SCRIPT RAN'</script>");
        await window.OpenFileAsync(htmlPath);
        EditorDocument html = window.ActiveDocument!;
        html.TogglePreview();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await html.PreviewView!.RenderAsync();
        Check(html.PreviewView.Browser is { Settings.IsScriptEnabled: false }, "HTML document scripts must be disabled.");
        Check(JsonSerializer.Deserialize<string>(await html.PreviewView.Browser!.ExecuteScriptAsync("getComputedStyle(document.getElementById('layout')).display")) == "grid", "Local CSS must render layout.");
        Check(JsonSerializer.Deserialize<string>(await html.PreviewView.Browser.ExecuteScriptAsync("document.body.innerText"))!.Contains("HTML 미리보기"), "Document script must not replace the layout.");
        await Program.RenderAsync(window, Path.Combine(output, "html-preview.png"), 1200, 800);
        // The fixture's settings directory gives it a browser profile isolated from real editor windows.
        uint browserId = html.PreviewView.Browser.BrowserProcessId;
        using (Process browser = Process.GetProcessById(checked((int)browserId)))
        {
            browser.Kill(entireProcessTree: true);
            await browser.WaitForExitAsync();
        }
        await WaitAsync(() => html.PreviewView.LastError is not null);
        Check(window.IsVisible && html.Editor.Text.Contains("HTML 미리보기"), "A failed preview browser must preserve the window and source buffer.");
        html.Editor.AppendText("<p>프로세스 복구 확인</p>");
        await html.PreviewView.RenderAsync();
        Check(html.PreviewView.LastError is null && html.PreviewView.Browser!.BrowserProcessId != browserId, "Refresh must replace the failed preview browser.");
        Check(JsonSerializer.Deserialize<string>(await html.PreviewView.Browser!.ExecuteScriptAsync("document.body.innerText"))!.Contains("프로세스 복구 확인"), "The restarted preview must render the preserved buffer.");
        // A manual type must survive unsaved buffers and session restoration.
        html.PreviewView.Kind = PreviewKind.Json;
        html.Editor.Text = "{\"updated\": true}";
        await html.PreviewView.RenderAsync();
        Check(html.PreviewView.ContentSnapshot?.Kind == PreviewKind.Json, "Manual preview kind must override the file extension.");
        ((TabControl)window.FindName("DocumentTabs")).SelectedItem = markdown;
        EditorCommands.DocumentPreview.Execute(null, window);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(!markdown.PreviewVisible && markdown.Editor.CanUndo, "Closing preview must retain editor undo history.");
        EditorCommands.DocumentPreview.Execute(null, window);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.Close(); await window.ClosingCompletion;
        Check(!window.IsVisible, "Mixed PDF/Markdown session must checkpoint on close.");
        MainWindow restored = new(new EditorPreferencesService(new PreferencesStore(settingsPath)));
        Program.ShowOffscreen(restored);
        await restored.InitializeSessionAsync();
        EditorDocument restoredPdf = restored.Documents.Single(d => d.IsPdf);
        Check(restoredPdf.PdfView!.PageIndex == 1 && restoredPdf.PdfView.Zoom == 1.25, "PDF page/zoom must restore.");
        Check(restored.Documents.Any(d => d.PreviewVisible && d.Editor.Text.Contains("새 내용")), "Unsaved Markdown and preview visibility must restore.");
        Check(restored.Documents.Any(d => d.PreviewView?.Kind == PreviewKind.Json && d.Editor.Text.Contains("updated")), "The manually selected preview format must restore.");
        restored.Close(); await restored.ClosingCompletion;
        await VerifyFailedPdfRestorationAsync(directory);
    }

    private static async Task VerifyFailedPdfRestorationAsync(string directory)
    {
        string settings = Path.Combine(directory, "failed-restore", "settings.json");
        EditorPreferencesService preferences = new(new PreferencesStore(settings));
        MainWindow seed = new(preferences);
        Program.ShowOffscreen(seed); await seed.InitializeSessionAsync();
        string sessionPath = seed.SessionFilePath!;
        seed.Close(); await seed.ClosingCompletion;
        byte[] broken = "%PDF-1.7\ntruncated recovery bytes"u8.ToArray();
        SessionDocumentState pdf = new() { Name = "broken.pdf", FilePath = Path.Combine(directory, "broken.pdf"), Kind = "pdf", PdfBytes = broken, IsModified = true, PdfPage = 20 };
        await File.WriteAllBytesAsync(pdf.FilePath, broken);
        SessionDocumentState text = new() { Name = "복구할 메모", Text = "아직 저장하지 않은 메모", IsModified = true };
        await new SessionStore(sessionPath).SaveAsync(new() { ActiveDocumentId = pdf.Id, Documents = [pdf, text] });
        MainWindow restored = new(preferences);
        Program.ShowOffscreen(restored); await restored.InitializeSessionAsync();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var view = restored.Documents.Single(d => d.IsPdf).PdfView!;
        await view.Rendering;
        Check(restored.IsVisible && !view.IsReady && !view.IsLoading, "A failed PDF restoration must leave the editor available.");
        Check(view.Bytes.SequenceEqual(broken) && view.IsModified, "Failed PDF restoration must preserve the recovery buffer and modified state.");
        Check(Descendants<Button>(view).Any(b => b.IsVisible && Equals(b.Content, "PDF 다시 불러오기")), "A failed PDF must offer explicit retry.");
        EditorDocument note = restored.Documents.Single(d => !d.IsPdf);
        ((TabControl)restored.FindName("DocumentTabs")).SelectedItem = note;
        note.Editor.AppendText(" · 계속 편집");
        restored.Close(); await restored.ClosingCompletion;
        SessionState saved = (await new SessionStore(sessionPath).LoadAsync()).State;
        Check(saved.Documents.Single(d => d.Kind == "pdf").PdfBytes!.SequenceEqual(broken), "Closing after a failed PDF load must not erase its recovery bytes.");
        Check(saved.Documents.Single(d => d.Kind == "text").Text.EndsWith("계속 편집"), "Other documents must remain editable and checkpoint correctly.");
    }

    private static async Task WaitAsync(Func<bool> predicate)
    {
        for (int i = 0; i < 1000; i++) { if (predicate()) return; await Task.Delay(10); }
        throw new InvalidOperationException("PDF operation timed out.");
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T result) yield return result;
            foreach (T descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
}
