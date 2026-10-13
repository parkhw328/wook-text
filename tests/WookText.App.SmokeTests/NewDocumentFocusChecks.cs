using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WookText.App;

namespace WookText.App.SmokeTests;

internal static class NewDocumentFocusChecks
{
    internal static async Task RunAsync(MainWindow window, string output)
    {
        await Program.WaitForIdleAsync(window);
        window.Activate();
        EditorDocument original = window.ActiveDocument!;
        string originalText = original.Editor.Text;
        TabControl tabs = (TabControl)window.FindName("DocumentTabs");
        Border search = (Border)window.FindName("SearchPanel");
        TextBox find = (TextBox)window.FindName("FindBox");
        Visibility visibility = search.Visibility;
        foreach (bool menu in new[] { false, true })
            foreach (bool fromSearch in new[] { false, true })
            {
                search.Visibility = fromSearch ? Visibility.Visible : Visibility.Collapsed;
                if (fromSearch) find.Focus();
                else original.Editor.TextArea.Focus();
                if (menu)
                {
                    MenuItem item = Descendants(window).OfType<MenuItem>().Single(i => i.Command == ApplicationCommands.New);
                    ((MenuItem)ItemsControl.ItemsControlFromItemContainer(item)).IsSubmenuOpen = true;
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    ((IInvokeProvider)new MenuItemAutomationPeer(item).GetPattern(PatternInterface.Invoke)).Invoke();
                }
                else
                {
                    KeyBinding binding = window.InputBindings.OfType<KeyBinding>().Single(b => b.Key == Key.N && b.Modifiers == ModifierKeys.Control);
                    ((RoutedCommand)binding.Command).Execute(binding.CommandParameter, (IInputElement)Keyboard.FocusedElement);
                }
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                EditorDocument created = window.ActiveDocument!;
                Check(created != original && Keyboard.FocusedElement == created.Editor.TextArea,
                    $"New document must own input focus: menu={menu}, fromSearch={fromSearch}.");
                Check(created.Editor.CaretOffset == 0, "A new document must start at the first insertion position.");
                TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, Keyboard.FocusedElement, "new "));
                Clipboard.SetText("문서 붙여넣기");
                ApplicationCommands.Paste.Execute(null, (IInputElement)Keyboard.FocusedElement);
                Check(created.Editor.Text == "new 문서 붙여넣기" && original.Editor.Text == originalText,
                    "Typing and paste must reach the new document without modifying the previous buffer.");
                if (menu && fromSearch)
                    await Program.RenderAsync(window, System.IO.Path.Combine(output, "new-document-focus.png"), 1240, 800);
                tabs.SelectedItem = original;
                window.Documents.Remove(created);
                created.Dispose();
            }
        EditorDocument stale = window.NewDocument();
        tabs.SelectedItem = original;
        original.Editor.TextArea.Focus();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(Keyboard.FocusedElement == original.Editor.TextArea, "A pending focus request must not steal focus after switching tabs.");
        window.Documents.Remove(stale);
        stale.Dispose();
        search.Visibility = visibility;
        Console.WriteLine("PASS: new document focus, typing and paste through shortcut/menu routes from editor/search; inactive tab guard.");
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
