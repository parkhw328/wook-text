using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;

namespace WookText.App;

/// <summary>A bounded, sampled document overview; scrolling never rebuilds the text drawing.</summary>
public sealed class EditorMinimap : FrameworkElement
{
    private readonly TextEditor _editor;
    private DrawingGroup? _overview;
    private readonly Brush _background = EditorFactory.Brush("#181716");
    private readonly Brush _ink = EditorFactory.Brush("#78756F");
    private readonly Brush _accent = EditorFactory.Brush("#DA702C");
    private readonly Brush _viewport = EditorFactory.Brush("#403124");

    public EditorMinimap(TextEditor editor)
    {
        _editor = editor;
        Width = 112;
        ClipToBounds = true;
        Cursor = Cursors.Hand;
        ToolTip = "문서 미니맵 · 클릭/드래그로 이동 · 주황색 선은 커서 위치";
        System.Windows.Automation.AutomationProperties.SetName(this, "문서 미니맵");
        Loaded += (_, _) =>
        {
            _editor.TextChanged += OnTextChanged;
            _editor.TextArea.TextView.VisualLinesChanged += OnViewChanged;
            _editor.TextArea.TextView.ScrollOffsetChanged += OnViewChanged;
            _editor.TextArea.Caret.PositionChanged += OnViewChanged;
            _overview = null;
            InvalidateVisual();
        };
        Unloaded += (_, _) =>
        {
            _editor.TextChanged -= OnTextChanged;
            _editor.TextArea.TextView.VisualLinesChanged -= OnViewChanged;
            _editor.TextArea.TextView.ScrollOffsetChanged -= OnViewChanged;
            _editor.TextArea.Caret.PositionChanged -= OnViewChanged;
            ReleaseMouseCapture();
        };
        SizeChanged += (_, _) => { _overview = null; InvalidateVisual(); };
    }

    private void OnTextChanged(object? sender, EventArgs e) { _overview = null; InvalidateVisual(); }
    private void OnViewChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualHeight <= 0 || ActualWidth <= 0) return;
        dc.DrawRectangle(_background, null, new Rect(RenderSize));
        int count = _editor.Document.LineCount;
        double scale = ActualHeight / count;
        var view = _editor.TextArea.TextView;
        if (view.VisualLinesValid && view.VisualLines.Count > 0)
        {
            int first = view.VisualLines[0].FirstDocumentLine.LineNumber;
            int last = view.VisualLines[^1].LastDocumentLine.LineNumber;
            dc.DrawRectangle(_viewport, new Pen(_ink, 1),
                new Rect(0.5, (first - 1) * scale, ActualWidth - 1, Math.Max(2, (last - first + 1) * scale)));
        }
        if (_overview is null)
        {
            _overview = new DrawingGroup();
            using (DrawingContext drawing = _overview.Open())
            {
                // Bound work even for million-line documents and very long individual lines.
                int samples = Math.Min(count, Math.Max(1, (int)(ActualHeight / 2)));
                for (int sample = 0; sample < samples; sample++)
                {
                    int number = 1 + (int)((long)sample * count / samples);
                    var line = _editor.Document.GetLineByNumber(number);
                    string text = _editor.Document.GetText(line.Offset, Math.Min(line.Length, 100));
                    double x = 5;
                    foreach (char c in text)
                    {
                        if (x >= ActualWidth - 4) break;
                        if (!char.IsWhiteSpace(c)) drawing.DrawRectangle(_ink, null, new Rect(x, (number - 1) * scale, 1, 1));
                        x += c == '\t' ? 4 : 1;
                    }
                }
            }
            _overview.Freeze();
        }
        dc.DrawDrawing(_overview);
        double caretY = Math.Min(ActualHeight - 2, (_editor.TextArea.Caret.Line - 1) * scale);
        dc.DrawRectangle(_accent, null, new Rect(0, caretY, ActualWidth, 2));
    }

    public void NavigateTo(double fraction)
    {
        int line = 1 + (int)Math.Round(Math.Clamp(fraction, 0, 1) * (_editor.Document.LineCount - 1));
        _editor.ScrollToLine(line);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        CaptureMouse();
        NavigateTo(e.GetPosition(this).Y / Math.Max(1, ActualHeight));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            NavigateTo(e.GetPosition(this).Y / Math.Max(1, ActualHeight));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
        _editor.TextArea.Focus();
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        _editor.ScrollToVerticalOffset(Math.Max(0, _editor.VerticalOffset - e.Delta));
        e.Handled = true;
    }
}
