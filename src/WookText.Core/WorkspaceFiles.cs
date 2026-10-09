namespace WookText.Core;

public sealed record WorkspaceEntry(string FullPath, string Name, bool IsDirectory, bool IsLink = false);
public sealed record DirectoryListing(IReadOnlyList<WorkspaceEntry> Entries, bool Truncated);
public sealed record WorkspaceSearch(IReadOnlyList<WorkspaceEntry> Entries, bool Truncated, int SkippedFolders);
public sealed record ExplorerOptions(bool ShowHidden = false, bool HideGenerated = true);

public sealed class WorkspaceFiles
{
    public const int DirectoryLimit = 10000;
    public const int SearchResultLimit = 200;
    public const int SearchScanLimit = 100000;
    private static readonly HashSet<string> GeneratedFolders = new(StringComparer.OrdinalIgnoreCase)
    { ".git", ".vs", ".idea", ".next", ".cache", ".venv", "node_modules", "bin", "obj", "__pycache__" };
    public static bool IsGeneratedFolder(string name) => GeneratedFolders.Contains(name);

    public Task<DirectoryListing> ReadDirectoryAsync(string path, ExplorerOptions options, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        List<WorkspaceEntry> entries = [];
        bool truncated = false;
        foreach (FileSystemInfo item in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Include(item, options)) continue;
            if (entries.Count == DirectoryLimit) { truncated = true; break; }
            entries.Add(ToEntry(item));
        }
        entries.Sort(CompareEntries);
        return new DirectoryListing(entries, truncated);
    }, cancellationToken);

    public Task<WorkspaceSearch> SearchAsync(string root, string query, ExplorerOptions options, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        string[] terms = query.Trim().Replace('\\', '/').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0) return new WorkspaceSearch([], false, 0);
        List<WorkspaceEntry> matches = [];
        Stack<string> pending = new();
        pending.Push(root);
        int scanned = 0, skipped = 0;
        bool truncated = false;
        while (pending.TryPop(out string? directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (FileSystemInfo item in new DirectoryInfo(directory).EnumerateFileSystemInfos())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++scanned > SearchScanLimit) { truncated = true; break; }
                    if (!Include(item, options)) continue;
                    WorkspaceEntry entry = ToEntry(item);
                    if (entry.IsDirectory)
                    {
                        if (!entry.IsLink) pending.Push(entry.FullPath);
                    }
                    else
                    {
                        string relative = Path.GetRelativePath(root, entry.FullPath).Replace('\\', '/');
                        if (terms.All(term => relative.Contains(term, StringComparison.OrdinalIgnoreCase))) matches.Add(entry);
                        if (matches.Count >= SearchResultLimit) { truncated = true; break; }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { skipped++; }
            if (truncated) break;
        }
        matches.Sort(CompareEntries);
        return new WorkspaceSearch(matches, truncated, skipped);
    }, cancellationToken);

    public static string Create(string parent, string name, bool directory)
    {
        ValidateName(name);
        string path = Path.Combine(Path.GetFullPath(parent), name);
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException("같은 이름의 항목이 이미 있습니다.");
        if (directory) Directory.CreateDirectory(path);
        else using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
        return path;
    }

    public static string Rename(WorkspaceEntry entry, string name)
    {
        ValidateName(name);
        string destination = Path.Combine(Path.GetDirectoryName(entry.FullPath)!, name);
        if (string.Equals(entry.FullPath, destination, StringComparison.Ordinal)) return entry.FullPath;
        if (!string.Equals(entry.FullPath, destination, StringComparison.OrdinalIgnoreCase) && (File.Exists(destination) || Directory.Exists(destination)))
            throw new IOException("같은 이름의 항목이 이미 있습니다.");
        if (entry.IsDirectory) Directory.Move(entry.FullPath, destination);
        else File.Move(entry.FullPath, destination);
        return destination;
    }

    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name is "." or ".." || name.EndsWith(' ') || name.EndsWith('.') ||
            name.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c)))
            throw new ArgumentException("이름은 1–200자로 입력하세요. 경로 구분자, 특수문자, 끝의 공백·마침표는 사용할 수 없습니다.");
        string stem = name.Split('.')[0].TrimEnd().ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".Contains(stem[3]))
            throw new ArgumentException("Windows에서 예약한 이름입니다. 다른 이름을 사용하세요.");
    }

    public static bool IsWithin(string root, string path)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string normalizedPath = Path.GetFullPath(path);
        return string.Equals(normalizedRoot, Path.TrimEndingDirectorySeparator(normalizedPath), StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(Path.EndsInDirectorySeparator(normalizedRoot) ? normalizedRoot : normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Include(FileSystemInfo item, ExplorerOptions options) =>
        (options.ShowHidden || (item.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0) &&
        (!options.HideGenerated || item is not DirectoryInfo || !GeneratedFolders.Contains(item.Name));

    private static WorkspaceEntry ToEntry(FileSystemInfo item) => new(item.FullName, item.Name, item is DirectoryInfo, (item.Attributes & FileAttributes.ReparsePoint) != 0);

    private static int CompareEntries(WorkspaceEntry a, WorkspaceEntry b)
    {
        if (a.IsDirectory != b.IsDirectory) return a.IsDirectory ? -1 : 1;
        int natural = CompareNames(a.Name, b.Name);
        return natural != 0 ? natural : StringComparer.OrdinalIgnoreCase.Compare(a.FullPath, b.FullPath);
    }

    internal static int CompareNames(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                int startA = i, startB = j;
                while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                string numberA = a[startA..i].TrimStart('0'), numberB = b[startB..j].TrimStart('0');
                int result = numberA.Length.CompareTo(numberB.Length);
                if (result == 0) result = StringComparer.Ordinal.Compare(numberA, numberB);
                if (result != 0) return result;
            }
            else
            {
                int result = char.ToUpperInvariant(a[i++]).CompareTo(char.ToUpperInvariant(b[j++]));
                if (result != 0) return result;
            }
        }
        int length = (a.Length - i).CompareTo(b.Length - j);
        return length != 0 ? length : StringComparer.OrdinalIgnoreCase.Compare(a, b);
    }
}
