using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WookText.Core;

namespace WookText.App.Explorer;

public sealed class ExplorerNode : INotifyPropertyChanged
{
    private bool _expanded, _selected, _active;
    public WorkspaceEntry Entry { get; }
    public string Name => Entry.Name;
    public string FullPath => Entry.FullPath;
    public string RelativePath { get; }
    public bool IsDirectory => Entry.IsDirectory;
    public bool IsPlaceholder { get; }
    public bool IsActionable => !IsPlaceholder;
    public ObservableCollection<ExplorerNode> Children { get; } = [];
    public ImageSource? Icon => IsPlaceholder ? null : ExplorerIcons.Get(Entry, IsExpanded);
    public string Tooltip => Entry.IsLink ? FullPath + (IsDirectory ? "\n연결 폴더는 자동으로 탐색하지 않습니다." : "\n연결 파일") : FullPath;
    public bool IsLoaded { get; internal set; }
    internal Task? LoadingTask { get; set; }
    internal Action<ExplorerNode>? ExpansionChanged { get; init; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsExpanded
    {
        get => _expanded;
        set { if (_expanded == value) return; _expanded = value; Notify(nameof(IsExpanded)); Notify(nameof(Icon)); ExpansionChanged?.Invoke(this); }
    }
    public bool IsSelected { get => _selected; set { if (_selected == value) return; _selected = value; Notify(nameof(IsSelected)); } }
    public bool IsActive { get => _active; set { if (_active == value) return; _active = value; Notify(nameof(IsActive)); } }

    public ExplorerNode(WorkspaceEntry entry, string root, bool placeholder = false)
    {
        Entry = entry;
        RelativePath = placeholder ? "" : System.IO.Path.GetRelativePath(root, entry.FullPath);
        IsPlaceholder = placeholder;
        if (entry.IsDirectory && !placeholder) Children.Add(Message("불러오는 중…"));
    }

    internal static ExplorerNode Message(string text) => new(new WorkspaceEntry("", text, false), "", true);
    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal static class ExplorerIcons
{
    private static readonly Dictionary<string, ImageSource> Cache = [];
    public static ImageSource Get(WorkspaceEntry entry, bool expanded)
    {
        string extension = System.IO.Path.GetExtension(entry.Name).ToLowerInvariant();
        if (extension is not (".js" or ".mjs" or ".jsx" or ".ts" or ".tsx" or ".json" or ".yaml" or ".yml" or ".toml" or ".html" or ".htm" or ".xml" or ".xaml" or ".jsp" or ".vue" or ".cs" or ".java" or ".py" or ".md" or ".markdown" or ".css" or ".scss" or ".sh" or ".ps1")) extension = "";
        string key = entry.IsDirectory ? expanded ? "folder-open" : "folder" : extension;
        if (Cache.TryGetValue(key, out ImageSource? icon)) return icon;
        DrawingGroup drawing = new();
        using (DrawingContext context = drawing.Open())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 20, 20));
            if (entry.IsDirectory)
            {
                Geometry shape = Geometry.Parse(expanded
                    ? "M 2,6 L 2,4 L 8,4 L 10,6 L 18,6 L 18,8 M 2,8 L 19,8 L 16,16 L 2,16 Z"
                    : "M 2,6 L 2,4 L 8,4 L 10,6 L 18,6 L 18,16 L 2,16 Z");
                context.DrawGeometry(expanded ? EditorFactory.Brush("#563821") : null, new Pen(EditorFactory.Brush("#D99A61"), 1.25) { LineJoin = PenLineJoin.Round }, shape);
            }
            else
            {
                (string glyph, string color) = extension switch
                {
                    ".js" or ".mjs" or ".jsx" => ("JS", "#D0AE70"),
                    ".ts" or ".tsx" => ("TS", "#A2B8C9"),
                    ".json" or ".yaml" or ".yml" or ".toml" => ("{}", "#B9A1D7"),
                    ".html" or ".htm" or ".xml" or ".xaml" or ".jsp" or ".vue" => ("<>", "#DA905C"),
                    ".cs" => ("C#", "#B7B896"),
                    ".java" => ("J", "#DA905C"),
                    ".py" => ("Py", "#D0AE70"),
                    ".md" or ".markdown" => ("M↓", "#AAA69B"),
                    ".css" or ".scss" => ("#", "#B9A1D7"),
                    ".sh" or ".ps1" => (">_", "#B7B896"),
                    _ => ("", "#9C9990")
                };
                if (glyph.Length == 0)
                    context.DrawGeometry(null, new Pen(EditorFactory.Brush(color), 1.2), Geometry.Parse("M 5,2 L 12,2 L 16,6 L 16,18 L 5,18 Z M 12,2 L 12,6 L 16,6 M 8,10 L 13,10 M 8,13 L 13,13"));
                else
                {
                    FormattedText text = new(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 10, EditorFactory.Brush(color), 1);
                    context.DrawText(text, new Point((20 - text.Width) / 2, (20 - text.Height) / 2));
                }
            }
        }
        DrawingImage image = new(drawing);
        image.Freeze();
        Cache[key] = image;
        return image;
    }
}
