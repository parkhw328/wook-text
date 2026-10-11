using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WookText.App;

public static class ComboBoxInteraction
{
    public static readonly DependencyProperty OpenOnClickProperty = DependencyProperty.RegisterAttached(
        "OpenOnClick", typeof(bool), typeof(ComboBoxInteraction), new PropertyMetadata(false, OnOpenOnClickChanged));

    public static bool GetOpenOnClick(DependencyObject element) => (bool)element.GetValue(OpenOnClickProperty);
    public static void SetOpenOnClick(DependencyObject element, bool value) => element.SetValue(OpenOnClickProperty, value);

    private static void OnOpenOnClickChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not TextBox textBox) return;
        if ((bool)args.NewValue) textBox.PreviewMouseLeftButtonUp += OnEditableClick;
        else textBox.PreviewMouseLeftButtonUp -= OnEditableClick;
    }

    private static void OnEditableClick(object sender, MouseButtonEventArgs args)
    {
        // Open after caret/selection placement; do not intercept typing or text selection.
        if (sender is TextBox { IsEnabled: true, TemplatedParent: ComboBox { IsEditable: true, IsEnabled: true } combo })
            combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, true);
    }
}
