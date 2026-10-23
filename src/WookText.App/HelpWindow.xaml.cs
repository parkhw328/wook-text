using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WookText.Core;

namespace WookText.App;

public partial class HelpWindow : Window
{
    private static bool _registered;
    private static HelpWindow? _current;

    public static void RegisterCommands()
    {
        if (_registered) return;
        _registered = true;
        CommandManager.RegisterClassInputBinding(typeof(Window), new KeyBinding(ApplicationCommands.Help, Key.F1, ModifierKeys.None));
        CommandManager.RegisterClassCommandBinding(typeof(Window), new CommandBinding(ApplicationCommands.Help,
            (sender, e) => { e.Handled = true; ShowFor((Window)sender); },
            (_, e) => { e.CanExecute = true; e.Handled = true; }));
    }

    public static void ShowFor(Window owner)
    {
        if (owner is HelpWindow help) { help.Activate(); return; }
        if (_current is { } existing && existing.Owner == owner) { existing.Activate(); return; }
        _current?.Close();
        HelpWindow window = new() { Owner = owner };
        _current = window;
        window.Closed += (_, _) => { if (_current == window) _current = null; };
        window.Show();
    }

    public HelpWindow()
    {
        InitializeComponent();
        NativeWindowTheme.Attach(this);
        InputBindings.Add(new KeyBinding(ApplicationCommands.Find, Key.F, ModifierKeys.Control));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Find,
            (_, e) => { e.Handled = true; SearchBox.Focus(); SearchBox.SelectAll(); },
            (_, e) => { e.CanExecute = true; e.Handled = true; }));
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Loaded += (_, _) => SearchBox.Focus();
        RefreshTopics();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (TopicList is not null) RefreshTopics();
    }

    private void RefreshTopics()
    {
        HelpTopic? previous = TopicList.SelectedItem as HelpTopic;
        var topics = UserGuide.Search(SearchBox.Text);
        TopicList.ItemsSource = topics;
        ResultCount.Text = $"{topics.Count} / {UserGuide.Topics.Count}개 주제 · Ctrl+F 검색";
        TopicList.SelectedItem = previous is not null && topics.Contains(previous) ? previous : topics.FirstOrDefault();
        if (topics.Count == 0) ShowTopic("검색 결과가 없습니다", "검색어를 줄이거나 검색 지우기를 눌러 전체 목차를 확인하세요.");
    }

    private void OnTopicChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TopicList.SelectedItem is HelpTopic topic) ShowTopic(topic.Title, topic.Body);
    }

    private void ShowTopic(string title, string body)
    {
        TopicTitle.Text = title;
        FlowDocument document = new()
        {
            FontFamily = EditorFonts.Create(new EditorPreferences()),
            FontSize = 15,
            Foreground = (Brush)FindResource("Ink"),
            Background = (Brush)FindResource("Canvas"),
            PagePadding = new Thickness(0, 0, 12, 0)
        };
        foreach (string line in body.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            bool heading = line.StartsWith("### ", StringComparison.Ordinal);
            bool bullet = line.StartsWith("- ", StringComparison.Ordinal);
            Paragraph paragraph = new(new Run(heading ? line[4..] : bullet ? "• " + line[2..] : line))
            {
                Margin = new Thickness(0, heading ? 12 : 0, 0, bullet ? 7 : 14),
                FontSize = heading ? 18 : 15,
                FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal,
                LineHeight = 25
            };
            document.Blocks.Add(paragraph);
        }
        Reader.Document = document;
        Dispatcher.InvokeAsync(() =>
        {
            if (Reader.Document == document) document.Blocks.FirstBlock?.BringIntoView();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnClearSearch(object sender, RoutedEventArgs e) { SearchBox.Clear(); SearchBox.Focus(); }
    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
