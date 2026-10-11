using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class ThemeChecks
{
    // Drive WPF dependency-property states deterministically, even on an offscreen host.
    // These checks test templates and their rendered states, not native pointer delivery.
    private static void State(UIElement element, string property, bool value)
    {
        var key = (DependencyPropertyKey)typeof(UIElement).GetField(property + "PropertyKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        element.SetValue(key, value);
        element.UpdateLayout();
    }

    public static async Task RunAsync(MainWindow window, string output)
    {
        PreferencesWindow preferences = new(new EditorPreferences());
        Program.ShowOffscreen(preferences);
        try
        {
            TabControl settings = (TabControl)preferences.FindName("SettingsTabs");
            await CheckTabsAsync(settings, output, "settings");
            await CheckComboBoxesAsync(preferences, settings);
            await Program.RenderAsync(preferences, Path.Combine(output, "theme-settings.png"), 680, 690);
        }
        finally { preferences.Close(); }
        await CheckTabsAsync((TabControl)window.FindName("DocumentTabs"), output, "documents");
        ListBox documents = (ListBox)window.FindName("OpenDocumentsList");
        documents.UpdateLayout();
        ListBoxItem row = (ListBoxItem)documents.ItemContainerGenerator.ContainerFromIndex(documents.SelectedIndex);
        Check(documents.FocusVisualStyle is null && row.FocusVisualStyle is null, "Document list and rows must not use OS focus adorners.");
        Check(documents.Template.FindName("ListSurface", documents) is Border { BorderThickness: { Left: 0, Top: 0, Right: 0, Bottom: 0 } }, "Document list must own its borderless template.");
        Border surface = (Border)row.Template.FindName("Row", row);
        Color selected = ColorOf(surface.Background);
        State(row, "IsMouseOver", true);
        State(row, "IsKeyboardFocused", true);
        Check(ColorOf(surface.Background) == selected, "Selected row fill must stay stable on hover and focus.");
        Check(ColorOf(surface.BorderBrush) == ColorOf((Brush)window.FindResource("FocusStroke")), "Row keyboard focus must use a muted marker.");
        Program.RenderElement(documents, Path.Combine(output, "theme-document-list-focus.png"));
        State(row, "IsMouseOver", false);
        State(row, "IsKeyboardFocused", false);
        State(documents, "IsKeyboardFocused", true);
        Program.RenderElement(documents, Path.Combine(output, "theme-document-list-container-focus.png"));
        State(documents, "IsKeyboardFocused", false);

        CompareWindow comparison = new();
        Program.ShowOffscreen(comparison);
        try { await CheckTabsAsync((TabControl)comparison.FindName("CompareTabs"), output, "comparison"); }
        finally { comparison.Close(); }
        Console.WriteLine("PASS: controlled tab header/body hover, selected/unselected/focus/disabled states; stable list selection, muted focus, and no OS focus border.");
    }

    private static async Task CheckTabsAsync(TabControl tabs, string output, string name)
    {
        tabs.UpdateLayout();
        TabItem selected = (TabItem)tabs.ItemContainerGenerator.ContainerFromIndex(tabs.SelectedIndex);
        selected.ApplyTemplate();
        Border header = (Border)selected.Template.FindName("Tab", selected);
        Color background = ColorOf(header.Background);
        // TabItem.IsMouseOver includes its logical content, not just the header.
        State(selected, "IsMouseOver", true);
        State(selected, "IsKeyboardFocusWithin", true);
        Check(ColorOf(header.Background) == background, $"{name}: content hover/focus must not recolor the selected header.");
        State(header, "IsMouseOver", true);
        State(selected, "IsKeyboardFocused", true);
        Check(ColorOf(header.Background) == background, $"{name}: selected header must keep its fill when hovered/focused.");
        Check(selected.FocusVisualStyle is null && tabs.FocusVisualStyle is null, $"{name}: system focus adorners must be replaced.");
        Program.RenderElement(header, Path.Combine(output, $"theme-{name}-selected-focus.png"));
        State(header, "IsMouseOver", false);
        State(selected, "IsMouseOver", false);
        State(selected, "IsKeyboardFocused", false);
        State(selected, "IsKeyboardFocusWithin", false);
        if (tabs.Items.Count > 1)
        {
            TabItem other = (TabItem)tabs.ItemContainerGenerator.ContainerFromIndex(tabs.SelectedIndex == 0 ? 1 : 0);
            other.ApplyTemplate();
            Border otherHeader = (Border)other.Template.FindName("Tab", other);
            bool enabled = other.IsEnabled;
            other.IsEnabled = true;
            State(otherHeader, "IsMouseOver", true);
            Check(ColorOf(otherHeader.Background) == ColorOf((Brush)tabs.FindResource("Raised")), "Only unselected header hover should use the dark hover fill.");
            State(otherHeader, "IsMouseOver", false);
            other.IsEnabled = false;
            Program.RenderElement(otherHeader, Path.Combine(output, $"theme-{name}-disabled.png"));
            other.IsEnabled = enabled;
        }
        await Task.CompletedTask;
    }

    private static Color ColorOf(Brush brush) => ((SolidColorBrush)brush).Color;

    private static async Task CheckComboBoxesAsync(PreferencesWindow preferences, TabControl settings)
    {
        foreach (string name in new[] { "EnglishFontBox", "KoreanFontBox", "IndentLanguageBox", "IndentStyleBox", "IndentSizeBox" })
        {
            settings.SelectedIndex = name.StartsWith("Indent", StringComparison.Ordinal) ? 1 : 0;
            preferences.UpdateLayout();
            await System.Windows.Threading.Dispatcher.Yield();
            ComboBox combo = (ComboBox)preferences.FindName(name);
            combo.ApplyTemplate();
            ToggleButton button = (ToggleButton)combo.Template.FindName("DropDownButton", combo);
            foreach (double x in new[] { 3d, combo.ActualWidth / 2, combo.ActualWidth - 16 })
            {
                DependencyObject? hit = combo.InputHitTest(new Point(x, combo.ActualHeight / 2)) as DependencyObject;
                while (hit is not null && hit is not ToggleButton && hit is not TextBox)
                    hit = VisualTreeHelper.GetParent(hit);
                Check(hit is ToggleButton || combo.IsEditable && hit is TextBox, $"{name}: body, padding, and arrow must accept clicks.");
                if (hit is TextBox text)
                    text.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
                else
                    typeof(ToggleButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
                await System.Windows.Threading.Dispatcher.Yield();
                Check(combo.IsDropDownOpen, $"{name}: click target must open the list.");
                combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(preferences), 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
                Check(!combo.IsDropDownOpen, $"{name}: Escape must close the list.");
                await System.Windows.Threading.Dispatcher.Yield();
            }
            combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(preferences), 0, Key.F4) { RoutedEvent = Keyboard.KeyDownEvent });
            Check(combo.IsDropDownOpen, $"{name}: F4 must still open the list.");
            combo.IsDropDownOpen = false;
            await System.Windows.Threading.Dispatcher.Yield();
        }
        settings.SelectedIndex = 0;
        Console.WriteLine("PASS: all five settings combos accept body/padding/arrow hit targets, open via routed editable clicks or toggle activation, and support Escape/F4.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
