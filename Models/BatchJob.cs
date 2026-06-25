using System.IO;
using Ps3IsoTool.Core;

namespace Ps3IsoTool.Models;

public enum BatchOp { Extract, Make, Split, Merge, Patch }

/// One queued operation in the Batch tab.
public sealed class BatchJob : ObservableObject
{
    public BatchOp Op { get; init; }
    public string Input { get; init; } = "";
    /// Explicit output (used by Make); null = auto-derive.
    public string? OutputOverride { get; init; }
    public string CfwVersion { get; init; } = "4.21";

    public string OpLabel => Op.ToString();
    public string InputName => Path.GetFileName(Input.TrimEnd('\\', '/'));

    private string _status = "Queued";
    public string Status { get => _status; set => Set(ref _status, value); }

    private double _percent;
    public double Percent { get => _percent; set => Set(ref _percent, value); }

    private bool _isError;
    public bool IsError { get => _isError; set => Set(ref _isError, value); }
}
