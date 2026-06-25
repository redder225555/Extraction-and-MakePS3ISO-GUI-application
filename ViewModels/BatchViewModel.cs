using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using Ps3IsoTool.Core;
using Ps3IsoTool.Models;
using Ps3IsoTool.Services;

namespace Ps3IsoTool.ViewModels;

/// Queue many operations (any mix of extract/make/split/merge/patch) and run them
/// sequentially — one disk-heavy job at a time, with per-job + overall progress.
public sealed class BatchViewModel : ObservableObject
{
    public ObservableCollection<BatchJob> Jobs { get; } = new();

    private CancellationTokenSource? _cts;

    public RelayCommand AddExtractCommand { get; }
    public RelayCommand AddMakeCommand { get; }
    public RelayCommand AddSplitCommand { get; }
    public RelayCommand AddMergeCommand { get; }
    public RelayCommand AddPatchCommand { get; }
    public RelayCommand RemoveJobCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand RunAllCommand { get; }
    public RelayCommand CancelCommand { get; }

    public BatchViewModel()
    {
        AddExtractCommand = new RelayCommand(_ => AddFiles(BatchOp.Extract));
        AddMakeCommand = new RelayCommand(_ => AddFolders());
        AddSplitCommand = new RelayCommand(_ => AddFiles(BatchOp.Split));
        AddMergeCommand = new RelayCommand(_ => AddFiles(BatchOp.Merge));
        AddPatchCommand = new RelayCommand(_ => AddFiles(BatchOp.Patch));
        RemoveJobCommand = new RelayCommand(j => { if (!IsRunning && j is BatchJob bj) Jobs.Remove(bj); });
        ClearCommand = new RelayCommand(_ => { if (!IsRunning) { Jobs.Clear(); UpdateOverall(); } });
        RunAllCommand = new RelayCommand(async _ => await RunAllAsync(), _ => !IsRunning && Jobs.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsRunning);
    }

    private string _cfwVersion = "4.21";
    public string CfwVersion { get => _cfwVersion; set => Set(ref _cfwVersion, value); }

    private bool _splitOutput;
    public bool SplitOutput { get => _splitOutput; set => Set(ref _splitOutput, value); }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set { if (Set(ref _isRunning, value)) { OnPropertyChanged(nameof(IsIdle)); RunAllCommand.RaiseCanExecuteChanged(); CancelCommand.RaiseCanExecuteChanged(); } }
    }
    public bool IsIdle => !IsRunning;

    private double _overallPercent;
    public double OverallPercent { get => _overallPercent; private set => Set(ref _overallPercent, value); }

    private string _overallStatus = "No jobs queued.";
    public string OverallStatus { get => _overallStatus; private set => Set(ref _overallStatus, value); }

    private void AddFiles(BatchOp op)
    {
        var d = new OpenFileDialog
        {
            Title = $"Add {op} job(s)",
            Multiselect = true,
            Filter = op == BatchOp.Merge
                ? "First split part (*.0)|*.0|All files (*.*)|*.*"
                : "PS3 ISO or first part (*.iso;*.0)|*.iso;*.0|All files (*.*)|*.*"
        };
        if (d.ShowDialog() != true) return;
        foreach (var f in d.FileNames)
            Jobs.Add(new BatchJob { Op = op, Input = f, CfwVersion = CfwVersion });
        UpdateOverall();
    }

    private void AddFolders()
    {
        var d = new OpenFolderDialog { Title = "Add Make job(s) — pick game folder(s)", Multiselect = true };
        if (d.ShowDialog() != true) return;
        foreach (var folder in d.FolderNames)
            Jobs.Add(new BatchJob { Op = BatchOp.Make, Input = folder, OutputOverride = folder.TrimEnd('\\', '/') + ".iso" });
        UpdateOverall();
    }

    private void UpdateOverall()
    {
        if (!IsRunning) OverallStatus = Jobs.Count == 0 ? "No jobs queued." : $"{Jobs.Count} job(s) queued.";
    }

    private async Task RunAllAsync()
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IsRunning = true;
        foreach (var j in Jobs) { j.Status = "Queued"; j.Percent = 0; j.IsError = false; }

        int done = 0, total = Jobs.Count;
        try
        {
            foreach (var job in Jobs.ToList())
            {
                ct.ThrowIfCancellationRequested();
                job.Status = "Running…";
                OverallStatus = $"Running {done + 1} of {total}: {job.OpLabel} {job.InputName}";
                var prog = new Progress<OpProgress>(p =>
                {
                    job.Percent = p.Percent;
                });
                try
                {
                    await RunJobAsync(job, prog, ct);
                    job.Percent = 100;
                    job.Status = "Done ✓";
                }
                catch (OperationCanceledException) { job.Status = "Cancelled"; throw; }
                catch (Exception ex) { job.IsError = true; job.Status = "Error: " + ex.Message; }
                done++;
                OverallPercent = total > 0 ? done * 100.0 / total : 0;
            }
            OverallStatus = $"Finished — {done}/{total} done.";
        }
        catch (OperationCanceledException)
        {
            OverallStatus = $"Cancelled after {done}/{total}.";
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private static Task RunJobAsync(BatchJob job, IProgress<OpProgress> prog, CancellationToken ct) => job.Op switch
    {
        BatchOp.Extract => Ps3IsoExtract.ExtractAsync(job.Input, null, false, prog, ct),
        BatchOp.Make => Ps3IsoMake.MakeAsync(job.Input, job.OutputOverride, false, prog, ct),
        BatchOp.Split => Ps3IsoOps.SplitAsync(job.Input, null, prog, ct),
        BatchOp.Merge => Ps3IsoOps.MergeAsync(job.Input, null, prog, ct),
        BatchOp.Patch => Ps3IsoPatch.PatchAsync(job.Input, job.CfwVersion, prog, ct),
        _ => Task.CompletedTask
    };
}
