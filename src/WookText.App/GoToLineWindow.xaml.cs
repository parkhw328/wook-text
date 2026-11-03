using System.Globalization;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using WookText.Core;

namespace WookText.App;

public partial class GoToLineWindow : Window
{
    private readonly TextDocument _buffer;
    public LineNavigationTarget Target { get; private set; }

    public GoToLineWindow(EditorDocument document)
    {
        _buffer = document.Editor.Document;
        InitializeComponent();
        NativeWindowTheme.Attach(this);
        DocumentName.Text = document.Name;
        DocumentName.ToolTip = document.Location;
        var caret = document.Editor.TextArea.Caret;
        PositionSummary.Text = $"현재 {caret.Line:N0}줄, {caret.Column:N0}열 · 전체 {_buffer.LineCount:N0}줄";
        PositionBox.MaxLength = LineNavigation.MaximumInputLength;
        PositionBox.Text = caret.Line.ToString(CultureInfo.InvariantCulture);
        PositionBox.TextChanged += (_, _) => ErrorMessage.Text = "";
        Loaded += (_, _) => { PositionBox.Focus(); PositionBox.SelectAll(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None) return;
            e.Handled = true;
            DialogResult = false;
        };
    }

    private void OnGo(object sender, RoutedEventArgs e)
    {
        if (!LineNavigation.TryResolve(PositionBox.Text, _buffer.LineCount, line => _buffer.GetLineByNumber(line).Length,
            out LineNavigationTarget target, out string error))
        {
            ErrorMessage.Text = error;
            PositionBox.Focus();
            return;
        }
        Target = target;
        DialogResult = true;
    }
}
