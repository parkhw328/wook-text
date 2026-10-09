using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application application = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/wText;component/Themes/Dark.xaml", UriKind.Relative)
        });
        application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/wText;component/Themes/MenuStyles.xaml", UriKind.Relative) });
        application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/wText;component/Themes/Branding.xaml", UriKind.Relative) });
        int exitCode = 1;
        application.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                string output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/smoke");
                Directory.CreateDirectory(output);
                await RunAsync(output);
                Console.WriteLine("PASS: WPF startup, document tabs, undo/redo, search, replace, save, comparison, and rendered layouts.");
                exitCode = 0;
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); }
            finally { application.Shutdown(exitCode); }
        });
        application.Run();
        return exitCode;
    }

    private static async Task RunAsync(string output)
    {
        VerifyFonts();
        string sampleDirectory = Path.Combine(output, "fixtures");
        Directory.CreateDirectory(sampleDirectory);
        string original = Path.Combine(sampleDirectory, "appsettings.original.json");
        string modified = Path.Combine(sampleDirectory, "appsettings.modified.json");
        string originalText = "{\r\n    \"name\": \"wText\",\r\n    \"version\": \"0.2.0\",\r\n    \"theme\": \"dark\",\r\n    \"language\": \"한국어\",\r\n    \"editor\": {\r\n        \"fontSize\": 13,\r\n        \"wordWrap\": false\r\n    }\r\n}\r\n";
        string modifiedText = originalText.Replace("13", "15").Replace("false", "true").Replace("\"theme\": \"dark\",", "\"theme\": \"dark\",\r\n    \"compareFiles\": true,");
        await File.WriteAllTextAsync(original, originalText);
        await File.WriteAllTextAsync(modified, modifiedText);

        string settingsPath = Path.Combine(sampleDirectory, $"settings-{Guid.NewGuid():N}.json");
        EditorPreferencesService preferences = new(new PreferencesStore(settingsPath));
        MainWindow window = new(preferences);
        ShowOffscreen(window);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await window.OpenFileAsync(original);
        Check(window.Documents.Count == 1, "Opening a file should reuse the untouched empty tab.");
        await window.OpenFileAsync(original.ToUpperInvariant());
        Check(window.Documents.Count == 1, "The same Windows path should not open twice.");
        EditorDocument document = window.ActiveDocument!;
        Check(document.Editor.Text == originalText, "Opening a file must preserve Korean and CRLF.");
        document.Editor.AppendText("// 임시 수정");
        Check(document.Editor.IsModified && document.Header.Contains('●'), "Edits must mark the tab dirty.");
        document.Editor.Undo();
        Check(document.Editor.Text == originalText && !document.Editor.IsModified, "Undo to the saved state must clear the dirty marker.");
        document.Editor.Redo();
        Check(document.Editor.IsModified, "Redo must restore the modified state.");
        document.Editor.Undo();

        ApplicationCommands.Find.Execute(null, window);
        TextBox find = (TextBox)window.FindName("FindBox");
        find.Text = "한국어";
        Button next = Descendants<Button>((DependencyObject)window.Content).Single(b => Equals(b.Content, "다음 찾기"));
        next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(document.Editor.SelectedText == "한국어", "Find must select the matching Korean text.");

        ApplicationCommands.Replace.Execute(null, window);
        find.Text = "wText";
        ((TextBox)window.FindName("ReplaceBox")).Text = "wText test";
        Button replace = Descendants<Button>((DependencyObject)window.Content).Single(b => Equals(b.Content, "모두 바꾸기"));
        replace.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(document.Editor.Text.Contains("wText test"), "Replace all must update the buffer.");
        document.Editor.Undo();
        Check(document.Editor.Text == originalText, "Replace all must undo as one operation.");

        document.Editor.AppendText("// saved through UI\r\n");
        ApplicationCommands.Save.Execute(null, window);
        await WaitUntilAsync(() => ((Grid)window.FindName("Root")).IsEnabled);
        Check(!document.Editor.IsModified, "Successful UI save must clear the dirty state.");
        Check(await File.ReadAllTextAsync(original) == document.Editor.Text, "UI save must write the editor buffer.");

        // Restore the visual fixture using the same UI save path.
        document.Editor.Undo();
        ApplicationCommands.Save.Execute(null, window);
        await WaitUntilAsync(() => ((Grid)window.FindName("Root")).IsEnabled);
        ((Border)window.FindName("SearchPanel")).Visibility = Visibility.Collapsed;
        await window.OpenFileAsync(modified);
        Check(window.Documents.Count == 2, "Opening another file must preserve the original tab.");
        await RenderAsync(window, Path.Combine(output, "editor.png"), 1240, 800);

        CompareWindow comparison = new(preferences);
        ShowOffscreen(comparison);
        await comparison.CompareFilesAsync(original, modified);
        Check(comparison.Result is { Added: 1, Modified: 2 }, "The comparison screen should report one added and two modified lines.");
        await RenderAsync(comparison, Path.Combine(output, "comparison.png"), 1240, 780);
        await RenderAsync(window, Path.Combine(output, "editor-small.png"), 960, 600);
        await ExperienceChecks.RunAsync(window, comparison, preferences, settingsPath, output);
        comparison.Close();
        window.Close();
        await ExplorerChecks.RunAsync(output);
    }

    private static void VerifyFonts()
    {
        Typeface english = new(EditorFonts.BundledEnglish, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        Typeface korean = new(EditorFonts.BundledKorean, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        Check(english.TryGetGlyphTypeface(out GlyphTypeface englishGlyphs) && englishGlyphs.FamilyNames.Values.Any(n => n.Contains("JetBrains Mono")), "Bundled JetBrains Mono must load without relying on an installed font.");
        Check(korean.TryGetGlyphTypeface(out GlyphTypeface koreanGlyphs) && koreanGlyphs.FamilyNames.Values.Any(n => n.Contains("Noto Sans KR")), "Bundled Noto Sans KR must load without relying on an installed font.");
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
            context.DrawText(new FormattedText("ABC 가나다", System.Globalization.CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight,
                new Typeface(EditorFonts.Create(new EditorPreferences()), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 16, Brushes.White, 1), new Point());
        List<GlyphRun> runs = GlyphRuns(visual.Drawing).ToList();
        foreach (GlyphRun run in runs) Console.WriteLine($"Font run: {new string(run.Characters.ToArray())} -> {string.Join(", ", run.GlyphTypeface.FamilyNames.Values)}");
        Check(runs.Any(r => r.Characters.Contains('A') && r.GlyphTypeface.FamilyNames.Values.Any(n => n.Contains("JetBrains Mono"))), "Latin text must actually render with JetBrains Mono.");
        Check(runs.Any(r => r.Characters.Contains('가') && r.GlyphTypeface.FamilyNames.Values.Any(n => n.Contains("Noto Sans KR"))), "Hangul must actually render with Noto Sans KR.");
    }

    private static IEnumerable<GlyphRun> GlyphRuns(Drawing drawing)
    {
        if (drawing is GlyphRunDrawing glyph) yield return glyph.GlyphRun;
        if (drawing is DrawingGroup group)
            foreach (Drawing child in group.Children)
                foreach (GlyphRun run in GlyphRuns(child)) yield return run;
    }

    internal static async Task RenderAsync(Window window, string path, int width, int height)
    {
        window.Width = width;
        window.Height = height;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        RenderElement((FrameworkElement)window.Content, path);
    }

    internal static void RenderElement(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        RenderTargetBitmap bitmap = new((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        DrawingVisual drawing = new();
        using (DrawingContext context = drawing.RenderOpen())
        {
            // A root margin offsets Render(element); a visual brush captures its
            // local bounds so the right and bottom edges are not clipped.
            Vector offset = VisualTreeHelper.GetOffset(element);
            context.DrawRectangle(new VisualBrush(element) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(new Point(offset.X, offset.Y), element.RenderSize) }, null, new Rect(element.RenderSize));
        }
        bitmap.Render(drawing);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }

    internal static void ShowOffscreen(Window window)
    {
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.Show();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is T match) yield return match;
            if (child is DependencyObject dependency)
                foreach (T descendant in Descendants<T>(dependency)) yield return descendant;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The UI operation did not finish.");
            await Task.Delay(20);
        }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
