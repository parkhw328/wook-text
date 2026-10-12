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
        await Program.RenderAsync(window, System.IO.Path.Combine(output, "search-focus.png"), 1240, 800);
        panel.Visibility = Visibility.Collapsed;
        editor.Select(0, 0);
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
