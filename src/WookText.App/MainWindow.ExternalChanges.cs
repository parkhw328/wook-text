using System.IO;
using System.Text;
using System.Windows.Threading;
using WookText.Core;

namespace WookText.App;

public partial class MainWindow
{
    private readonly DispatcherTimer _fileCheckTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Dictionary<Guid, string> _fileNotices = [];
    private bool _checkingFiles;
    private bool _closed;

    // Injectable dialog keeps the same update path testable without a modal message loop.
    public Func<AppDialogRequest, AppDialogResult>? ExternalChangeDialog { private get; set; }

    public async Task CheckExternalChangesAsync()
    {
        if (_checkingFiles || _busy || _closing || _closed || _restoringSession || !IsEnabled) return;
        _checkingFiles = true;
        try
        {
            foreach (Guid id in _fileNotices.Keys.Where(id => Documents.All(d => d.SessionId != id)).ToArray())
                _fileNotices.Remove(id);
            foreach (EditorDocument document in Documents.ToArray())
            {
                if (_busy || _closing || _closed) return;
                if (document.FilePath is not { } path) continue;
                string? baseline = document.Fingerprint;
                bool IsCurrent() => !_busy && !_closing && !_closed && IsEnabled && Documents.Contains(document)
                    && document.FilePath == path && document.Fingerprint == baseline;
                try
                {
                    string fingerprint = await Task.Run(() => TextFileService.GetFingerprintAsync(path));
                    if (!IsCurrent()) continue;
                    if (fingerprint == baseline)
                    {
                        _fileNotices.Remove(document.SessionId);
                        continue;
                    }
                    string notice = path + "|" + fingerprint;
                    if (_fileNotices.GetValueOrDefault(document.SessionId) == notice) continue;
                    TextFileEncoding encoding = document.Encoding;
                    LoadedTextFile file = await Task.Run(async () =>
                    {
                        try { return await _files.ReadAsync(path); }
                        catch (DecoderFallbackException) when (encoding == TextFileEncoding.Korean949)
                        { return await _files.ReadAsync(path, TextFileEncoding.Korean949); }
                    });
                    if (!IsCurrent()) continue;
                    if (file.Fingerprint == baseline) continue;
                    notice = path + "|" + file.Fingerprint;
                    if (_fileNotices.GetValueOrDefault(document.SessionId) == notice) continue;
                    await RunAsync(() =>
                    {
                        _fileNotices[document.SessionId] = notice;
                        if (document.Editor.IsModified)
                        {
                            AppDialogResult choice = ShowExternalChange(new(
                                "외부에서 파일이 변경되었습니다",
                                "이 탭에도 저장하지 않은 수정이 있습니다. 다시 불러오면 현재 수정 내용이 사라집니다.\n계속 편집하면 내용을 유지하며, 다른 이름으로 저장할 수 있습니다.",
                                "계속 편집", "수정 버리고 다시 불러오기", "취소", document.Name, path,
                                Kind: AppDialogKind.Warning));
                            if (choice != AppDialogResult.Secondary) return Task.CompletedTask;
                        }
                        EndLinkedEditing();
                        document.Reload(file);
                        Status.Text = $"외부 변경 다시 불러옴 · {document.Name}";
                        ShowExternalChange(new("파일을 다시 불러왔습니다",
                            "외부에서 변경된 파일 내용을 반영했습니다.", DocumentName: document.Name, DocumentPath: path));
                        return Task.CompletedTask;
                    });
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    if (!IsCurrent()) continue;
                    string notice = path + "|error|" + ex.GetType().Name;
                    if (_fileNotices.GetValueOrDefault(document.SessionId) == notice) continue;
                    await RunAsync(() =>
                    {
                        _fileNotices[document.SessionId] = notice;
                        // A missing/unreadable source must not turn a clean retained buffer into disposable data.
                        document.Editor.IsModified = true;
                        ShowExternalChange(new("원본 파일을 읽을 수 없습니다",
                            "파일이 삭제·이동되었거나 읽을 수 없는 상태입니다. 현재 편집 내용은 유지했습니다.\n필요하면 다른 이름으로 저장해 주세요. 파일을 다시 읽을 수 있으면 변경 확인을 재개합니다.",
                            DocumentName: document.Name, DocumentPath: path, Details: ex.Message, Kind: AppDialogKind.Warning));
                        return Task.CompletedTask;
                    });
                }
            }
        }
        finally { _checkingFiles = false; }
    }

    private async Task ReloadActiveDocumentAsync()
    {
        if (ActiveDocument is not { FilePath: { } path } document) return;
        if (document.Editor.IsModified && ShowExternalChange(new(
            "파일을 다시 불러올까요?", "현재 수정 내용을 버리고 디스크의 파일을 다시 불러옵니다.",
            "계속 편집", "수정 버리고 다시 불러오기", "취소", document.Name, path,
            Kind: AppDialogKind.Warning)) != AppDialogResult.Secondary) return;
        LoadedTextFile file;
        try { file = await _files.ReadAsync(path); }
        catch (DecoderFallbackException)
        {
            if (document.Encoding != TextFileEncoding.Korean949 && !AppDialogs.ConfirmEncoding(this, path)) return;
            file = await _files.ReadAsync(path, TextFileEncoding.Korean949);
        }
        EndLinkedEditing();
        document.Reload(file);
        _fileNotices.Remove(document.SessionId);
        Status.Text = $"파일 다시 불러옴 · {document.Name}";
    }

    private AppDialogResult ShowExternalChange(AppDialogRequest request) =>
        ExternalChangeDialog?.Invoke(request) ?? AppDialogs.Show(this, request);
}
