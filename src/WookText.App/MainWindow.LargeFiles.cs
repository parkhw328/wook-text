using System.IO;
using WookText.Core;

namespace WookText.App;

public partial class MainWindow
{
    private CancellationTokenSource? _largeSearchCancellation;
    private readonly SemaphoreSlim _textReadGate = new(1, 1);

    private async Task<LoadedEditorFile> ReadEditorFileAsync(string path, TextFileEncoding? encoding = null)
    {
        await _textReadGate.WaitAsync();
        try
        {
            // Bound aggregate editor storage as well as the individual file. Reload temporarily retains the old buffer.
            long characters = Documents.Where(d => !d.IsPdf).Sum(d => (long)d.Editor.Document.TextLength);
            long lines = Documents.Where(d => !d.IsPdf).Sum(d => (long)d.Editor.Document.LineCount);
            int availableCharacters = (int)Math.Max(0, LargeFilePolicy.MaximumWindowCharacters - characters);
            int availableLines = (int)Math.Max(0, LargeFilePolicy.MaximumWindowLines - lines);
            if (availableCharacters == 0 || availableLines == 0)
                throw new IOException("열린 문서의 메모리 보호 한도에 도달했습니다. 다른 대용량 탭을 닫은 뒤 다시 열어 주세요.");
            return await EditorFileLoader.ReadAsync(path, encoding, new TextReadLimits(
                Characters: Math.Min(LargeFilePolicy.MaximumCharacters, availableCharacters),
                Lines: Math.Min(LargeFilePolicy.MaximumLines, availableLines)));
        }
        finally { _textReadGate.Release(); }
    }

    private void CancelLargeSearch() => _largeSearchCancellation?.Cancel();

    private async Task FindLargeAsync(bool previous)
    {
        CancelLargeSearch();
        if (ActiveDocument is not { IsLargeFile: true } document || FindBox.Text.Length == 0) return;
        using CancellationTokenSource cancellation = new();
        _largeSearchCancellation = cancellation;
        var editor = document.Editor;
        var snapshot = editor.Document.CreateSnapshot();
        string query = FindBox.Text;
        bool matchCase = MatchCase.IsChecked == true;
        int start = previous ? editor.SelectionStart : editor.SelectionStart + editor.SelectionLength;
        Status.Text = "대용량 문서에서 찾는 중…";
        try
        {
            int found = await Task.Run(() => ChunkedTextSearch.Find(snapshot.TextLength, snapshot.GetText,
                query, start, previous, matchCase, cancellation.Token), cancellation.Token);
            if (cancellation.IsCancellationRequested || _closed || ActiveDocument != document) return;
            if (found < 0) { Status.Text = "일치하는 내용이 없습니다."; return; }
            editor.Select(found, query.Length);
            editor.ScrollToLine(editor.Document.GetLineByOffset(found).LineNumber);
            Status.Text = "일치하는 내용을 선택했습니다 · 대용량 모드";
        }
        catch (OperationCanceledException) { }
        catch (ArgumentException ex) { Status.Text = ex.Message; }
        finally
        {
            if (_largeSearchCancellation == cancellation) _largeSearchCancellation = null;
        }
    }
}
