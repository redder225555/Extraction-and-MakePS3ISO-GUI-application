namespace Ps3IsoTool.ViewModels;

public sealed class MainViewModel
{
    public MakeViewModel Make { get; } = new();
    public ExtractViewModel Extract { get; } = new();
    public SplitViewModel Split { get; } = new();
    public MergeViewModel Merge { get; } = new();
    public PatchViewModel Patch { get; } = new();
    public BatchViewModel Batch { get; } = new();
}
