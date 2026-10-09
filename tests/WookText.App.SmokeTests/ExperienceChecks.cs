using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class ExperienceChecks
{
    internal static async Task RunAsync(MainWindow window, CompareWindow comparison, EditorPreferencesService preferences, string settingsPath, string output)
    {
        VerifySyntax();
        await VerifyLayoutAsync(window, preferences, settingsPath, output);
        await VerifyPreferencesAsync(window, comparison, preferences, settingsPath, output);
        await VerifyMenusAndAboutAsync(window, output);
        string sample = Path.Combine(output, "fixtures", "welcome.jsp");
        await File.WriteAllTextAsync(sample, "<%@ page contentType=\"text/html; charset=UTF-8\" %>\n<%-- 한글과 코드를 함께 편집하는 wText --%>\n<!DOCTYPE html>\n<html lang=\"ko\">\n<head>\n    <title>wText · 코드 미리보기</title>\n</head>\n<body>\n    <h1>안녕하세요, ${user.name}!</h1>\n    <%\n        String message = \"Welcome to wText\";\n        int count = 3;\n        for (int i = 0; i < count; i++) {\n            out.println(message);\n        }\n    %>\n</body>\n</html>\n");
        await window.OpenFileAsync(sample);
        await Program.RenderAsync(window, Path.Combine(output, "editor-source.png"), 1240, 800);
        EditorCommands.CloseTab.Execute(null, window);
        Console.WriteLine("PASS: syntax modes, mixed JSP, preferences preview/apply/cancel/persistence, preserved editing state, sidebar/focus restoration, menus, and attribution.");
    }

    private static void VerifySyntax()
    {
        (string Path, string Id, string Text)[] cases =
        [
            ("settings.YML", "yaml", "# 설정\nname: \"wText\"\nenabled: true\nport: 8080"),
            ("index.html", "html", "<!-- 주석 -->\n<h1 class=\"title\">Hello</h1>"),
            ("index.js", "javascript", "// 주석\nconst name = \"wText\";\nasync function greet() { return `hello ${name}`; }"),
            ("index.ts", "typescript", "// 주석\ninterface User { name: string; }\nconst name: string = \"wText\";"),
            ("index.jsp", "jsp", "<%-- 주석 --%>\n<h1 class=\"title\">${user.name}</h1>\n<% String name = \"wText\"; int n = 3; %>"),
            ("Main.java", "java", "// 주석\npublic class Main { String name = \"wText\"; }\nrecord User(String name) {}"),
            ("settings.json", "json", "{\"name\": \"wText\", \"count\": 42}"),
            ("view.xaml", "xml", "<!-- 주석 -->\n<Window Title=\"wText\" />"),
            ("theme.css", "css", "/* 주석 */\nbody { color: #da702c; }"),
            ("Program.cs", "csharp", "// 주석\npublic class Program { string name = \"wText\"; }"),
            ("main.cpp", "cpp", "// comment\nint main() { return 42; }"),
            ("main.py", "python", "# comment\ndef greet():\n    return \"wText\""),
            ("query.sql", "sql", "-- comment\nSELECT name FROM users WHERE id = 42;"),
            ("main.ps1", "powershell", "# comment\n$name = \"wText\"\nif ($true) { Write-Host $name }"),
            (".bashrc", "shell", "# comment\nif [ -n \"$HOME\" ]; then echo \"hello\"; fi"),
            (".env", "config", "# comment\nNAME=\"wText\"\nPORT=8080"),
            ("README.md", "markdown", "# wText\n**Hello** `code`\n[link](https://example.com)"),
            ("index.php", "php", "<?php\n// comment\necho \"wText\";\n?>")
        ];
        foreach ((string path, string id, string text) in cases)
        {
            SyntaxLanguage language = SyntaxCatalog.Resolve(path);
            Check(language.Id == id, $"Wrong language detection for {path}.");
            TextDocument document = new(text);
            using DocumentHighlighter highlighter = new(document, language.Definition!);
            HighlightedSection[] sections = Enumerable.Range(1, document.LineCount).SelectMany(n => highlighter.HighlightLine(n).Sections).ToArray();
            Check(sections.Any(s => s.Length > 0 && s.Color.Foreground is not null), $"No syntax colors produced for {path}.");
            if (id is "yaml" or "javascript" or "java" or "jsp" or "html")
                Check(sections.Select(s => s.Color.Foreground?.GetColor(null!)).Where(c => c is not null).Distinct().Count() >= 3, $"Comments, strings, and code should be distinct in {path}.");
        }
        Check(SyntaxCatalog.Resolve("unknown.xyz").Id == "text", "Unknown files should remain plain text.");
        Check(SyntaxCatalog.Resolve(null, "yaml").Id == "yaml", "Untitled documents must allow a manual language choice.");
        Check(SyntaxCatalog.Resolve("page.jsp", "text").Definition is null, "Plain-text override should disable highlighting.");

        const string mixed = "<h1 class=\"title\">Hi</h1>\n<% String name = \"wText\"; int n = 3; %>\n<%-- comment --%>";
        TextDocument jsp = new(mixed);
        using DocumentHighlighter jspHighlighter = new(jsp, SyntaxCatalog.Resolve("page.jsp").Definition!);
        Check(ColorAt(jspHighlighter, mixed.IndexOf("h1", StringComparison.Ordinal)) == ColorAt(jspHighlighter, mixed.IndexOf("<%", StringComparison.Ordinal)), "HTML tags and JSP delimiters should share the orange accent.");
        Check(ColorAt(jspHighlighter, mixed.IndexOf("wText", StringComparison.Ordinal)) != ColorAt(jspHighlighter, mixed.IndexOf("comment", StringComparison.Ordinal)), "Embedded Java strings and JSP comments must use different colors.");
    }

    private static Color? ColorAt(DocumentHighlighter highlighter, int offset) => highlighter.HighlightLine(highlighter.Document.GetLineByOffset(offset).LineNumber)
        .Sections.LastOrDefault(s => s.Offset <= offset && s.Offset + s.Length > offset && s.Color.Foreground is not null)?.Color.Foreground?.GetColor(null!);

    private static async Task VerifyLayoutAsync(MainWindow window, EditorPreferencesService preferences, string settingsPath, string output)
    {
        Expander documents = (Expander)window.FindName("OpenDocumentsSection");
        Expander explorer = (Expander)window.FindName("ExplorerSection");
        documents.IsExpanded = explorer.IsExpanded = false;
        Check(!preferences.Current.OpenDocumentsExpanded && !preferences.Current.ExplorerExpanded, "Both section states must persist.");
        GridSplitter splitter = (GridSplitter)window.FindName("SidebarSplitter");
        ColumnDefinition sidebar = (ColumnDefinition)window.FindName("SidebarColumn");
        sidebar.Width = new GridLength(310);
        window.UpdateLayout();
        splitter.RaiseEvent(new DragCompletedEventArgs(70, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
        Check(Math.Abs(preferences.Current.SidebarWidth - 310) < 1, "Resized sidebar width must persist.");
        await Program.RenderAsync(window, Path.Combine(output, "sidebar-folded.png"), 1240, 800);
        EditorCommands.ToggleSidebar.Execute(null, window);
        window.UpdateLayout();
        Check(sidebar.ActualWidth == 0 && splitter.Visibility == Visibility.Collapsed, "Hidden sidebar must give its entire width to the editor.");
        await Program.RenderAsync(window, Path.Combine(output, "editor-only.png"), 1240, 800);
        EditorCommands.ToggleFocus.Execute(null, window);
        Check(((Menu)window.FindName("MainMenu")).Visibility == Visibility.Collapsed, "Focus mode must hide menu chrome.");
        Check(((DockPanel)window.FindName("FocusToolbar")).Visibility == Visibility.Visible, "Focus mode must retain an obvious exit.");
        await Program.RenderAsync(window, Path.Combine(output, "focus.png"), 1240, 800);
        KeyEventArgs escape = new(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        window.RaiseEvent(escape);
        Check(escape.Handled && ((Menu)window.FindName("MainMenu")).Visibility == Visibility.Visible, "Escape must leave focus mode.");
        Check(!preferences.Current.SidebarVisible && sidebar.Width.Value == 0, "Leaving focus mode must preserve an already-hidden sidebar.");

        MainWindow reopened = new(new EditorPreferencesService(new PreferencesStore(settingsPath)));
        Program.ShowOffscreen(reopened);
        await reopened.InitializeSessionAsync();
        Check(((Border)reopened.FindName("Sidebar")).Visibility == Visibility.Collapsed && !((Expander)reopened.FindName("OpenDocumentsSection")).IsExpanded, "A new window must restore saved visibility and section state.");
        EditorCommands.ToggleSidebar.Execute(null, reopened);
        reopened.UpdateLayout();
        Check(Math.Abs(((ColumnDefinition)reopened.FindName("SidebarColumn")).ActualWidth - 310) < 1, "Reopening the sidebar must recover its saved width.");
        await Program.CloseWindowAsync(reopened);
        EditorCommands.ToggleFocus.Execute(null, window);
        EditorCommands.ToggleSidebar.Execute(null, window);
        Check(((Menu)window.FindName("MainMenu")).Visibility == Visibility.Visible && preferences.Current.SidebarVisible, "Sidebar shortcut must provide another way out of focus mode.");
        preferences.Update(new EditorPreferences());
    }

    private static async Task VerifyPreferencesAsync(MainWindow window, CompareWindow comparison, EditorPreferencesService preferences, string settingsPath, string output)
    {
        EditorPreferences before = preferences.Current;
        PreferencesWindow cancelled = new(before);
        bool? cancelledResult = RunDialog(cancelled, async dialog =>
        {
            ((TextBox)dialog.FindName("SizeBox")).Text = "999";
            Check(!dialog.TryGetPreferences(out _), "Out-of-range font sizes must be rejected.");
            ((TextBox)dialog.FindName("SizeBox")).Text = "20";
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(dialog.PreviewEditor.FontSize == 20, "Size changes must update the preview before applying.");
            Check(preferences.Current == before, "Preview changes must not alter the editor before Apply.");
            dialog.Close();
        });
        Check(cancelledResult != true && cancelled.Result is null && preferences.Current == before, "Cancelling must preserve existing preferences.");

        PreferencesWindow applied = new(before);
        bool? appliedResult = RunDialog(applied, async dialog =>
        {
            await Program.RenderAsync(dialog, Path.Combine(output, "preferences.png"), 680, 690);
            ((ComboBox)dialog.FindName("EnglishFontBox")).SelectedItem = "Consolas";
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(dialog.PreviewEditor.FontFamily.FamilyMaps.Last().Target.StartsWith("Consolas", StringComparison.Ordinal), "Choosing a font must immediately update the preview.");
            ((TextBox)dialog.FindName("SizeBox")).Text = "18";
            ((CheckBox)dialog.FindName("WordWrapBox")).IsChecked = true;
            ((CheckBox)dialog.FindName("WhitespaceBox")).IsChecked = true;
            ((CheckBox)dialog.FindName("LineNumbersBox")).IsChecked = false;
            Check(dialog.PreviewEditor.WordWrap && dialog.PreviewEditor.Options.ShowSpaces && !dialog.PreviewEditor.ShowLineNumbers, "Preview must reflect all editor options.");
            await Program.RenderAsync(dialog, Path.Combine(output, "preferences-small.png"), 620, 660);
            ((Button)dialog.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        Check(appliedResult == true && applied.Result is not null, "Apply must return validated settings.");

        EditorDocument active = window.ActiveDocument!;
        TextDocument buffer = active.Editor.Document;
        string original = active.Editor.Text;
        active.Editor.AppendText("// unsaved preference test\n");
        active.Editor.Select(3, 6);
        int caret = active.Editor.CaretOffset;
        Check(preferences.Update(applied.Result!), "Preferences should save successfully.");
        Check(window.Documents.All(d => d.Editor.FontSize == 18 && d.Editor.WordWrap && !d.Editor.ShowLineNumbers && d.Editor.Options.ShowSpaces), "Preferences must apply to every open tab.");
        Check(ReferenceEquals(buffer, active.Editor.Document) && active.Editor.IsModified && active.Editor.SelectionStart == 3 && active.Editor.SelectionLength == 6 && active.Editor.CaretOffset == caret, "Applying settings must preserve the document, unsaved state, caret, and selection.");
        active.Editor.Undo();
        Check(active.Editor.Text == original && !active.Editor.IsModified, "Settings must preserve the complete undo history.");
        Check(new EditorPreferencesService(new PreferencesStore(settingsPath)).Current == applied.Result, "Preferences must survive a new process/service instance.");
        foreach (string host in new[] { "LeftHost", "RightHost" })
        {
            TextEditor editor = (TextEditor)((ContentControl)comparison.FindName(host)).Content;
            Check(editor.FontSize == 18 && !editor.WordWrap && !editor.ShowLineNumbers, "Comparison font changes must retain aligned, unwrapped rows.");
        }
        EditorDocument fresh = window.NewDocument();
        Check(fresh.Editor.FontSize == 18 && fresh.Editor.WordWrap, "New tabs must inherit current preferences.");
        fresh.SetLanguage("yaml");
        Check(fresh.LanguageName == "YAML" && !fresh.Editor.IsModified, "Choosing a language must not dirty a document.");
        EditorCommands.CloseTab.Execute(null, window);
        await Program.WaitForIdleAsync(window);
        Check(window.ActiveDocument is not null, "Closing the selected tab must activate a remaining document.");
        EditorCommands.ZoomIn.Execute(null, window);
        Check(preferences.Current.FontSize == 19 && window.Documents.All(d => d.Editor.FontSize == 19), "Zoom command must update all tabs.");
        EditorCommands.ResetZoom.Execute(null, window);
        Check(preferences.Current.FontSize == EditorPreferences.DefaultFontSize, "Reset zoom should restore the default size.");
        preferences.Update(before);
        using (FileStream lockedSettings = new(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Check(!preferences.Update(before with { FontSize = 17 }), "An unwritable settings file must report a save failure.");
            Check(window.Documents.All(d => d.Editor.FontSize == 17) && ((TextBlock)window.FindName("Status")).Text.Contains("저장하지 못했습니다", StringComparison.Ordinal), "A settings save failure must still apply preferences and explain the failure.");
        }
        preferences.Update(before);
        Console.WriteLine("PASS: preferences keep buffers, undo, selection, caret, comparison alignment, and persisted state intact.");
    }

    private static bool? RunDialog(PreferencesWindow dialog, Func<PreferencesWindow, Task> interact)
    {
        dialog.ShowActivated = false;
        dialog.ShowInTaskbar = false;
        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Left = dialog.Top = -32000;
        Exception? failure = null;
        dialog.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try { await interact(dialog); }
            catch (Exception ex) { failure = ex; }
            finally { if (dialog.IsVisible) dialog.Close(); }
        }), DispatcherPriority.ApplicationIdle);
        bool? result = dialog.ShowDialog();
        if (failure is not null) throw new InvalidOperationException("Preferences dialog check failed.", failure);
        return result;
    }

    private static async Task VerifyMenusAndAboutAsync(MainWindow window, string output)
    {
        MenuItem file = (MenuItem)window.FindName("FileMenu");
        file.IsSubmenuOpen = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Popup popup = (Popup)file.Template.FindName("PART_Popup", file);
        Check(popup.IsOpen && popup.Child is Border { BorderThickness.Left: 0 }, "Top menu popup must open without a border.");
        Check(file.Items.OfType<MenuItem>().Where(i => i.Command is not null).All(i => i.IsEnabled), "Menu commands must target the editor window even after a dialog closes.");
        Program.RenderElement((FrameworkElement)popup.Child, Path.Combine(output, "file-menu.png"));
        file.IsSubmenuOpen = false;
        MenuItem language = (MenuItem)window.FindName("LanguageMenu");
        language.IsSubmenuOpen = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(language.Items.OfType<MenuItem>().Count() == SyntaxCatalog.Languages.Count + 1, "Language menu must expose every supported mode plus automatic detection.");
        language.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "YAML")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(window.ActiveDocument!.LanguageOverride == "yaml", "Language menu selection must change the active editor.");
        language.IsSubmenuOpen = false;
        window.ActiveDocument.SetLanguage(null);
        AboutWindow about = new();
        Program.ShowOffscreen(about);
        Check(((TextBlock)about.FindName("AuthorLabel")).Text == "Hyunwook Park", "Help must identify the author.");
        Check(((Hyperlink)about.FindName("RepositoryLink")).NavigateUri.AbsoluteUri == "https://github.com/parkhw328/wook-text", "Help must link to the actual GitHub repository.");
        Check(about.Icon is not null && window.Icon is not null, "All windows must use the wText icon.");
        await Program.RenderAsync(about, Path.Combine(output, "about.png"), 540, 550);
        Check(about.FindName("LicensesButton") is Button && window.FindName("LicensesMenu") is MenuItem, "Help and About must expose the license notices.");
        about.Close();
        LicenseWindow licenses = new();
        Program.ShowOffscreen(licenses);
        await licenses.Loading;
        Check(licenses.NoticeEditor.IsReadOnly && licenses.NoticeEditor.Text.Contains("MIT License", StringComparison.Ordinal), "The in-app license reader must show the original MIT text.");
        await Program.RenderAsync(licenses, Path.Combine(output, "licenses.png"), 960, 700);
        ListBox components = (ListBox)licenses.FindName("ComponentList");
        foreach (LicenseComponent component in LicenseWindow.Components)
        {
            components.SelectedItem = component;
            await licenses.Loading;
            Check(licenses.NoticeEditor.Text.Length > 100 && !licenses.NoticeEditor.Text.StartsWith("고지 파일", StringComparison.Ordinal), $"Missing notice: {component.Name}");
        }
        components.SelectedItem = LicenseWindow.Components.Single(c => c.Name == "Noto Sans KR");
        await licenses.Loading;
        Check(licenses.NoticeEditor.Text.Contains("SIL OPEN FONT LICENSE", StringComparison.Ordinal), "Font notices must include the original OFL text.");
        licenses.Close();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
