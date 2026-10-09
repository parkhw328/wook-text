using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using WookText.Core;

namespace WookText.App;

public static class EditorFactory
{
    private static readonly ConditionalWeakTable<TextEditor, EditorPreferences> AppliedPreferences = new();

    public static TextEditor Create(bool readOnly = false, EditorPreferences? preferences = null)
    {
        TextEditor editor = new()
        {
            Background = Brush("#100F0F"),
            Foreground = Brush("#CECDC3"),
            LineNumbersForeground = Brush("#78756F"),
            ShowLineNumbers = !readOnly,
            IsReadOnly = readOnly,
            Padding = new System.Windows.Thickness(10, 12, 10, 12),
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
        };
        editor.Options.ConvertTabsToSpaces = true;
        editor.Options.IndentationSize = 4;
        editor.Options.HighlightCurrentLine = !readOnly;
        editor.TextArea.SelectionBrush = Brush("#563821");
        editor.TextArea.SelectionForeground = Brush("#FFFCF0");
        editor.TextArea.Caret.CaretBrush = Brush("#DA702C");
        editor.TextArea.TextView.CurrentLineBackground = Brush("#1C1916");
        editor.TextArea.TextView.CurrentLineBorder = new Pen(Brush("#1C1916"), 0);
        ApplyPreferences(editor, preferences ?? new EditorPreferences());
        return editor;
    }

    public static void ApplyPreferences(TextEditor editor, EditorPreferences preferences)
    {
        AppliedPreferences.TryGetValue(editor, out EditorPreferences? previous);
        if (previous is null || previous.EnglishFont != preferences.EnglishFont || previous.KoreanFont != preferences.KoreanFont)
            editor.FontFamily = EditorFonts.Create(preferences);
        editor.FontSize = preferences.FontSize;
        editor.WordWrap = !editor.IsReadOnly && preferences.WordWrap;
        editor.ShowLineNumbers = !editor.IsReadOnly && preferences.ShowLineNumbers;
        editor.Options.ShowSpaces = preferences.ShowWhitespace;
        editor.Options.ShowTabs = preferences.ShowWhitespace;
        AppliedPreferences.Remove(editor);
        AppliedPreferences.Add(editor, preferences);
    }

    public static void BindZoom(TextEditor editor, EditorPreferencesService preferences) => editor.PreviewMouseWheel += (_, e) =>
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        e.Handled = true;
        preferences.Update(preferences.Current with { FontSize = Math.Clamp(preferences.Current.FontSize + Math.Sign(e.Delta), EditorPreferences.MinimumFontSize, EditorPreferences.MaximumFontSize) });
    };

    public static void ApplySyntax(TextEditor editor, string? path, string? overrideId = null) => editor.SyntaxHighlighting = SyntaxCatalog.Resolve(path, overrideId).Definition;

    public static SolidColorBrush Brush(string hex)
    {
        SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
