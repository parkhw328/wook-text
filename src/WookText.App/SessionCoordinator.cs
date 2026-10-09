using System.IO;
using System.Windows.Threading;
using WookText.Core;

namespace WookText.App;

/// <summary>Captures immutable editor snapshots on the dispatcher and writes them on a worker.</summary>
public sealed class SessionCoordinator : IDisposable
{
    private readonly string _directory;
    private readonly Func<Func<SessionState>> _capture;
    private readonly Func<bool> _enabled;
    private readonly DispatcherTimer _idle = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _interval = new() { Interval = TimeSpan.FromSeconds(10) };
    private SessionStore? _store;
    private Task _pending = Task.CompletedTask;
    private long _revision, _savedRevision = -1;
    private bool _ready, _disposed;
    public string? StoragePath => _store?.FilePath;
    public string? LastWarning { get; private set; }
    public event Action<string>? Warning;

    public SessionCoordinator(string directory, Func<Func<SessionState>> capture, Func<bool> enabled)
    {
        _directory = directory; _capture = capture; _enabled = enabled;
        _idle.Tick += OnCheckpoint;
        _interval.Tick += OnCheckpoint;
    }

    public async Task<SessionState> InitializeAsync()
    {
        _store = await SessionStore.AcquireAsync(_directory);
        SessionLoadResult loaded = await Task.Run(() => _store.LoadAsync());
        if (!loaded.CanOverwrite)
        {
            _store.Dispose();
            _store = await SessionStore.AcquireAsync(_directory, fresh: true);
        }
        _ready = true;
        if (loaded.Warning is { } warning) Report(warning);
        return _enabled() ? loaded.State : new();
    }

    public void Changed()
    {
        if (_disposed) return;
        _revision++;
        if (!_ready || !_enabled()) { StopTimers(); return; }
        _idle.Stop(); _idle.Start();
        if (!_interval.IsEnabled) _interval.Start();
    }

    private void OnCheckpoint(object? sender, EventArgs e)
    {
        StopTimers();
        if (!_ready || _disposed || !_enabled() || !_pending.IsCompleted || _savedRevision == _revision) return;
        _pending = CheckpointAsync();
    }

    private async Task CheckpointAsync()
    {
        try { await SaveCurrentAsync(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { Report("작업 보관에 실패했습니다. 종료 전에 파일로 저장하거나 다시 시도하세요. " + ex.Message); }
        finally
        {
            if (!_disposed && _savedRevision != _revision && _enabled()) { _idle.Start(); _interval.Start(); }
        }
    }

    private async Task SaveCurrentAsync()
    {
        long revision = _revision;
        Func<SessionState> snapshot = _capture();
        SessionStore store = _store ?? throw new IOException("작업 보관 폴더를 열지 못했습니다.");
        await Task.Run(() => store.SaveAsync(snapshot()));
        _savedRevision = revision;
        LastWarning = null;
    }

    public async Task FlushAsync(bool retain)
    {
        StopTimers();
        await _pending;
        StopTimers();
        if (!_ready)
        {
            if (!retain) return;
            throw new IOException("작업 보관 폴더를 준비하지 못했습니다. 창을 열어 둡니다.");
        }
        if (retain)
        {
            // Capture again on close to include the final caret/selection and any input during a checkpoint.
            await SaveCurrentAsync();
        }
        else await Task.Run(() => _store!.ClearAsync());
    }

    private void Report(string warning) { LastWarning = warning; Warning?.Invoke(warning); }
    private void StopTimers() { _idle.Stop(); _interval.Stop(); }
    public void Dispose() { _disposed = true; StopTimers(); _store?.Dispose(); }
}
