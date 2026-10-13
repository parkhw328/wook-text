using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WookText.App;

namespace WookText.App.SmokeTests;

internal static class SearchFocusChecks
{
    internal static async Task RunAsync(MainWindow window, string output)
    {
        await Program.WaitForIdleAsync(window);
        window.Activate();
        TextBox find = (TextBox)window.FindName("FindBox");
        Border panel = (Border)window.FindName("SearchPanel");
        UIElement replacePanel = (UIElement)window.FindName("ReplacePanel");
        var editor = window.ActiveDocument!.Editor;
        string original = editor.Text;
        foreach (bool menu in new[] { false, true })
            foreach (bool replace in new[] { false, true })
                foreach (bool visible in new[] { false, true })
                {
                    panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                    editor.Select(0, 1);
                    editor.Focus();
                    RoutedUICommand command = replace ? ApplicationCommands.Replace : ApplicationCommands.Find;
                    if (menu)
                    {
                        MenuItem item = Descendants(window).OfType<MenuItem>().Single(i => i.Command == command);
                        ((MenuItem)ItemsControl.ItemsControlFromItemContainer(item)).IsSubmenuOpen = true;
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        ((IInvokeProvider)new MenuItemAutomationPeer(item).GetPattern(PatternInterface.Invoke)).Invoke();
                    }
                    else
                    {
                        KeyBinding binding = window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == (replace ? Key.H : Key.F) && b.Modifiers == ModifierKeys.Control);
                        ((RoutedCommand)binding.Command).Execute(binding.CommandParameter, editor.TextArea);
                    }
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    Check(panel.IsVisible && replacePanel.IsVisible == replace, "Find/replace must show the requested panel.");
                    Check(find.IsKeyboardFocused && Keyboard.FocusedElement == find, $"Find input must own keyboard focus: menu={menu}, replace={replace}, alreadyVisible={visible}.");
                    Check(find.SelectedText == editor.SelectedText, "Opening search must select the prefilled search text.");
                    TextComposition composition = new(InputManager.Current, (IInputElement)Keyboard.FocusedElement, "focus-check");
                    TextCompositionManager.StartComposition(composition);
                    Check(find.Text == "focus-check" && editor.Text == original, "Typing after opening search must replace the query without editing the document.");
                }
        editor.Text = "apple Apple apple\r\n사과 apple";
        find.Text = "apple";
        TextBlock summary = (TextBlock)window.FindName("SearchSummary");
        CheckBox matchCase = (CheckBox)window.FindName("MatchCase");
        TextBox replacement = (TextBox)window.FindName("ReplaceBox");
        SearchMatchColorizer highlight = editor.TextArea.TextView.LineTransformers.OfType<SearchMatchColorizer>().Single();
        Check(highlight.Offsets.Count == 4 && summary.Text.Contains("4개"), "All matches must be highlighted and counted.");
        matchCase.IsChecked = true;
        Check(highlight.Offsets.Count == 3, "Case changes must refresh highlights immediately.");
        matchCase.IsChecked = false;
        editor.Select(0, 0);
        Click("이전 찾기");
        Check(editor.SelectionStart == 22 && editor.SelectedText == "apple", "Previous search must wrap to the last match.");
        Click("다음 찾기");
        Check(editor.SelectionStart == 0 && editor.SelectedText == "apple", "Next search must wrap and select the entire match.");
        replacement.Text = "pear";
        Click("바꾸기");
        Check(editor.Text == "pear Apple apple\r\n사과 apple" && editor.SelectedText == "Apple", "Replace one must replace the selected match and navigate to the next.");
        editor.Undo();
        Click("모두 바꾸기");
        Check(editor.Text == "pear pear pear\r\n사과 pear" && highlight.Offsets.Count == 0, "Replace all must preserve line endings and refresh highlights.");
        editor.Undo();
        Check(editor.Text == "apple Apple apple\r\n사과 apple" && highlight.Offsets.Count == 4, "One undo must restore the entire replacement and highlights.");
        editor.Document.Insert(editor.Document.TextLength, " apple");
        Check(highlight.Offsets.Count == 5, "Editing the document must refresh all matches.");
        editor.Undo();
        EditorDocument first = window.ActiveDocument!;
        EditorDocument second = window.NewDocument();
        second.Editor.Text = "apple";
        Check(!editor.TextArea.TextView.LineTransformers.OfType<SearchMatchColorizer>().Any()
            && second.Editor.TextArea.TextView.LineTransformers.OfType<SearchMatchColorizer>().Single().Offsets.Count == 1,
            "Switching tabs must move highlights to the active document.");
        ((TabControl)window.FindName("DocumentTabs")).SelectedItem = first;
        window.Documents.Remove(second);
        second.Dispose();
        Check(highlight.Offsets.Count == 4, "Returning to a tab must restore its matches.");
        find.Text = "";
        Click("모두 바꾸기");
        Check(highlight.Offsets.Count == 0 && editor.Text.StartsWith("apple"), "An empty query must clear highlights without editing text.");
        find.Text = "apple";
        Click("닫기");
        Check(!editor.TextArea.TextView.LineTransformers.OfType<SearchMatchColorizer>().Any(), "Closing search must remove highlights.");
        ApplicationCommands.Replace.Execute(null, editor.TextArea);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        find.Text = "apple";
        editor.Select(0, 5);
        await Program.RenderAsync(window, System.IO.Path.Combine(output, "search-focus.png"), 1240, 800);
        Click("닫기");
        editor.Text = original;
        editor.Select(0, 0);
        void Click(string content) => Descendants(panel).OfType<Button>().Single(button => Equals(button.Content, content))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Console.WriteLine("PASS: search highlights, counts, case matching, wrap navigation, replace all and undo.");
        Console.WriteLine("PASS: find/replace keyboard focus, query selection and typing through menu automation and shortcut command routes (closed/open panels).");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject dependency) continue;
            yield return dependency;
            foreach (DependencyObject descendant in Descendants(dependency)) yield return descendant;
        }
    }
}
