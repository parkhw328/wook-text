using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using WookText.App;
using WookText.Core;

namespace WookText.App.SmokeTests;

internal static class HelpChecks
{
    public static async Task RunAsync(MainWindow main, string output)
    {
        string original = main.ActiveDocument!.Editor.Text;
        var menu = (MenuItem)main.FindName("UserGuideMenu");
        Check(menu.Command == ApplicationCommands.Help && menu.InputGestureText == "F1", "Help menu must advertise the same F1 command.");
        ApplicationCommands.Help.Execute(null, main.ActiveDocument.Editor.TextArea);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        HelpWindow help = Application.Current.Windows.OfType<HelpWindow>().Single();
        Check(help.Owner == main && help.IsVisible, "Help must open from the editor without a browser.");
        ApplicationCommands.Help.Execute(null, main);
        Check(Application.Current.Windows.OfType<HelpWindow>().Single() == help, "Repeated F1 must reuse the help window.");
        var list = (ListBox)help.FindName("TopicList");
        var search = (TextBox)help.FindName("SearchBox");
        var reader = (FlowDocumentScrollViewer)help.FindName("Reader");
        Check(list.Items.Count == UserGuide.Topics.Count, "Every embedded topic must be reachable.");
        foreach (HelpTopic topic in UserGuide.Topics)
        {
            list.SelectedItem = topic;
            string body = new TextRange(reader.Document.ContentStart, reader.Document.ContentEnd).Text;
            Check(body.Length > 100 && ((TextBlock)help.FindName("TopicTitle")).Text == topic.Title, "Each topic must render its own full body.");
        }
        search.Text = "블록 ctrl+shift+v";
        Check(list.Items.Count > 0 && list.Items.Count < UserGuide.Topics.Count, "Search must inspect title and body, ignoring shortcut case.");
        search.Text = "no_such_topic_4672";
        Check(list.Items.Count == 0 && ((TextBlock)help.FindName("TopicTitle")).Text.Contains("없습니다"), "No results must have an explicit recovery message.");
        search.Clear();
        list.SelectedItem = UserGuide.Topics.Single(t => t.Title == "전체 단축키");
        ApplicationCommands.Find.Execute(null, reader);
        Check(search.IsKeyboardFocusWithin, "Ctrl+F must target manual search, not the editor.");
        await Program.RenderAsync(help, Path.Combine(output, "user-guide.png"), 1060, 760);
        help.Close();
        PreferencesWindow settings = new(new EditorPreferences()) { Owner = main };
        Program.ShowOffscreen(settings);
        ApplicationCommands.Help.Execute(null, settings);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        help = Application.Current.Windows.OfType<HelpWindow>().Single();
        Check(help.Owner == settings, "Help from a dialog must belong to that dialog.");
        settings.Close();
        Check(!Application.Current.Windows.OfType<HelpWindow>().Any(), "Closing the owner must close its help window.");
        Check(main.ActiveDocument.Editor.Text == original, "Reading the guide must never change the document.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
