using System.Windows.Threading;

namespace WookText.App;

public partial class MainWindow
{
    private void ShowGoToLine()
    {
        if (ActiveDocument is not { } document) return;
        EndLinkedEditing();
        CancelLargeSearch();
        document.CancelPendingScroll();
        var editor = document.Editor;
        var buffer = editor.Document;
        GoToLineWindow dialog = new(document) { Owner = this };
        if (dialog.ShowDialog() == true && ActiveDocument == document && editor.Document == buffer)
        {
            var target = dialog.Target;
            var line = buffer.GetLineByNumber(target.Line);
            editor.Select(line.Offset + target.Column - 1, 0);
            editor.ScrollTo(target.Line, target.Column);
        }
        // The command temporarily disables Root. Focus only after it is enabled again.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!_closing && IsVisible && ActiveDocument == document && editor.IsEnabled)
                editor.TextArea.Focus();
        });
    }
}
