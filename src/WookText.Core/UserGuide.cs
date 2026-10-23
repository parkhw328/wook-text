using System.Reflection;

namespace WookText.Core;

public sealed record HelpTopic(string Title, string Body);

/// <summary>One offline source shared by the application and the repository manual.</summary>
public static class UserGuide
{
    public static IReadOnlyList<HelpTopic> Topics { get; } = Load();

    public static IReadOnlyList<HelpTopic> Search(string query)
    {
        string[] terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return Topics.Where(topic => terms.All(term =>
            (topic.Title + "\n" + topic.Body).Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray();
    }

    private static IReadOnlyList<HelpTopic> Load()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WookText.UserGuide.md")
            ?? throw new InvalidOperationException("기능 설명서 리소스를 찾을 수 없습니다.");
        using StreamReader reader = new(stream);
        List<HelpTopic> topics = [];
        string? title = null;
        List<string> body = [];
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                AddTopic();
                title = line[3..].Trim();
                body.Clear();
            }
            else if (title is not null) body.Add(line);
        }
        AddTopic();
        return topics.AsReadOnly();

        void AddTopic()
        {
            if (title is not null) topics.Add(new HelpTopic(title, string.Join("\n", body).Trim()));
        }
    }
}
