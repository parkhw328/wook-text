using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using WookText.Core;

namespace WookText.App;

/// <summary>Bounds wheel-driven layout work while leaving navigation and selection synchronous.</summary>
internal sealed class LargeFileScrolling : IDisposable
{
    private readonly TextEditor _editor;
    private readonly ScrollWheelBatch _batch = new();
    private readonly DispatcherTimer _timer;
    private bool _enabled;

    public LargeFileScrolling(TextEditor editor)
    {
        _editor = editor;
        _timer = new DispatcherTimer(DispatcherPriority.Input, editor.Dispatcher) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        editor.PreviewMouseWheel += OnMouseWheel;
        editor.PreviewMouseDown += OnMouseDown;
        editor.PreviewKeyDown += OnKeyDown;
        editor.Unloaded += OnUnloaded;
        editor.IsVisibleChanged += OnVisibilityChanged;
        editor.TextChanged += OnNavigation;
        editor.TextArea.Caret.PositionChanged += OnNavigation;
        editor.TextArea.TextView.ScrollOffsetChanged += OnNavigation;
    }

    public void SetEnabled(bool enabled)
    {
        Cancel();
        _enabled = enabled;
        ScrollViewer.SetIsDeferredScrollingEnabled(_editor, enabled);
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Ctrl+wheel remains zoom; modified input and selection drags use the usual editor route.
        if (e.Handled || !_enabled || !_editor.IsVisible || Keyboard.Modifiers != ModifierKeys.None ||
            e.LeftButton != MouseButtonState.Released || e.RightButton != MouseButtonState.Released)
        {
            Cancel();
            return;
        }

        var view = _editor.TextArea.TextView;
        IScrollInfo scroll = view;
        if (scroll.ViewportHeight <= 0) return;
        _batch.Add(e.Delta, view.VerticalOffset, scroll.ExtentHeight, scroll.ViewportHeight,
            view.DefaultLineHeight, SystemParameters.WheelScrollLines);
        e.Handled = true;
        // Starting an enabled timer does not postpone it: continuous input cannot starve the flush.
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        double? target = _batch.Take();
        if (target is null || !_enabled || !_editor.IsVisible) return;
        IScrollInfo scroll = _editor.TextArea.TextView;
        scroll.SetVerticalOffset(Math.Clamp(target.Value, 0, Math.Max(0, scroll.ExtentHeight - scroll.ViewportHeight)));
    }

    public void Cancel()
    {
        _timer.Stop();
        _batch.Reset();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e) => Cancel();
    private void OnKeyDown(object sender, KeyEventArgs e) => Cancel();
    private void OnUnloaded(object sender, RoutedEventArgs e) => Cancel();
    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => Cancel();
    private void OnNavigation(object? sender, EventArgs e) => Cancel();

    public void Dispose()
    {
        Cancel();
        _timer.Tick -= OnTick;
        _editor.PreviewMouseWheel -= OnMouseWheel;
        _editor.PreviewMouseDown -= OnMouseDown;
        _editor.PreviewKeyDown -= OnKeyDown;
        _editor.Unloaded -= OnUnloaded;
        _editor.IsVisibleChanged -= OnVisibilityChanged;
        _editor.TextChanged -= OnNavigation;
        _editor.TextArea.Caret.PositionChanged -= OnNavigation;
        _editor.TextArea.TextView.ScrollOffsetChanged -= OnNavigation;
    }
}
