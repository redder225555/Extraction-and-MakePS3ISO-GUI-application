using Ps3IsoTool.Core;
using Ps3IsoTool.Services;

namespace Ps3IsoTool.ViewModels;

/// Shared start/cancel + live-progress plumbing for any long-running ISO operation.
/// Subclasses just implement Validate + ExecuteAsync.
public abstract class OperationViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    public RelayCommand StartCommand { get; }
    public RelayCommand CancelCommand { get; }

    protected OperationViewModel()
    {
        StartCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsRunning);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsRunning);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!Set(ref _isRunning, value)) return;
            OnPropertyChanged(nameof(IsIdle));
            StartCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
        }
    }
    public bool IsIdle => !IsRunning;

    private double _percent;
    public double Percent { get => _percent; private set => Set(ref _percent, value); }

    private string _status = "Ready";
    public string Status { get => _status; private set => Set(ref _status, value); }

    private string _detail = "";
    public string Detail { get => _detail; private set => Set(ref _detail, value); }

    private bool _isError;
    public bool IsError { get => _isError; private set => Set(ref _isError, value); }

    /// Opt-in: delete the source after a successful run (off by default). What gets
    /// deleted is defined per-operation in CleanupSource().
    private bool _deleteSource;
    public bool DeleteSource { get => _deleteSource; set => Set(ref _deleteSource, value); }

    protected abstract Task ExecuteAsync(IProgress<OpProgress> progress, CancellationToken ct);
    protected virtual bool Validate(out string error) { error = ""; return true; }

    /// Override to delete the source(s) when DeleteSource is checked and the run succeeds.
    protected virtual void CleanupSource() { }

    private async Task RunAsync()
    {
        if (!Validate(out var err))
        {
            IsError = true;
            Status = err;
            Detail = "";
            return;
        }

        _cts = new CancellationTokenSource();
        IsRunning = true;
        IsError = false;
        Percent = 0;
        Status = "Working…";
        Detail = "";

        // Progress<T> created on the UI thread => callbacks marshal back to the UI thread.
        var progress = new Progress<OpProgress>(OnProgress);
        try
        {
            await ExecuteAsync(progress, _cts.Token);
            Percent = 100;
            Status = "Done ✓";
            Detail = "";
            if (DeleteSource)
            {
                try { CleanupSource(); Status = "Done ✓ — source deleted"; }
                catch (Exception ex) { Status = $"Done ✓ — but source cleanup failed: {ex.Message}"; }
            }
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled";
        }
        catch (Exception ex)
        {
            IsError = true;
            Status = "Error: " + ex.Message;
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void OnProgress(OpProgress p)
    {
        Percent = p.Percent;
        Detail = $"{p.ProcessedMb:n0} / {p.TotalMb:n0} MB  ·  {p.Percent:n1}%  ·  {p.SpeedMbs:n1} MB/s  ·  ETA {FmtEta(p.EtaSeconds)}  ·  part {p.PartNumber}";
    }

    private static string FmtEta(double seconds)
    {
        if (seconds <= 0 || double.IsInfinity(seconds) || double.IsNaN(seconds)) return "--:--";
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";
    }
}
