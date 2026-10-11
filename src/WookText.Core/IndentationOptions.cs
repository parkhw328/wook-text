namespace WookText.Core;

public sealed record IndentationOptions
{
    public int Size { get; init; } = 4;
    public bool UseTabs { get; init; }
    public bool DetectFromDocument { get; init; } = true;
    public IndentationOptions Normalize() => this with { Size = Math.Clamp(Size, 1, 8) };
}

// Value equality keeps preference snapshots comparable after JSON round trips.
public sealed class IndentationProfiles : Dictionary<string, IndentationOptions>, IEquatable<IndentationProfiles>
{
    public bool Equals(IndentationProfiles? other) => other is not null && Count == other.Count &&
        this.All(pair => other.TryGetValue(pair.Key, out IndentationOptions? value) && pair.Value == value);
    public override bool Equals(object? obj) => obj is IndentationProfiles other && Equals(other);
    public override int GetHashCode() => this.Aggregate(0, (hash, pair) => hash ^ HashCode.Combine(pair.Key, pair.Value));
}

public static class IndentationService
{
    public static string ProfileKey(string language, string? path) => (language, Path.GetExtension(path ?? "").ToLowerInvariant()) switch
    {
        ("javascript", ".jsonc") => "jsonc",
        ("html", ".vue") => "vue",
        ("css", ".scss") => "scss",
        ("css", ".less") => "less",
        _ => language
    };

    public static IndentationOptions Resolve(EditorPreferences preferences, string language, string? path, string text)
    {
        string key = ProfileKey(language, path);
        IndentationOptions options = preferences.IndentationByLanguage.GetValueOrDefault(key, preferences.Indentation).Normalize();
        if (options.DetectFromDocument) options = Detect(text, options);
        return language == "yaml" ? options with { UseTabs = false } : options;
    }

    public static IndentationOptions Detect(string text, IndentationOptions fallback)
    {
        List<int> spaces = [];
        int tabs = 0;
        int mixed = 0;
        using StringReader reader = new(text);
        for (int line = 0; line < 2000 && reader.ReadLine() is { } value; line++)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            string content = value.TrimStart();
            if (content.StartsWith('*') || content.StartsWith("//", StringComparison.Ordinal) || content.StartsWith('#')) continue;
            int length = value.Length - content.Length;
            if (length == 0) continue;
            ReadOnlySpan<char> prefix = value.AsSpan(0, length);
            if (prefix[0] == '\t') tabs++;
            else if (prefix.Contains('\t')) mixed++;
            else if (!prefix.Contains('\t') && prefix.Length <= 128) spaces.Add(prefix.Length);
        }
        int total = tabs + spaces.Count + mixed;
        if (total == 0) return fallback;
        if (tabs >= total * 0.8) return fallback with { UseTabs = true };
        if (spaces.Count < total * 0.8) return fallback;
        int minimum = spaces.Min();
        if (minimum is >= 1 and <= 8 && spaces.Count(size => size % minimum == 0) >= spaces.Count * 0.8)
            return fallback with { Size = minimum, UseTabs = false };
        return fallback;
    }
}
