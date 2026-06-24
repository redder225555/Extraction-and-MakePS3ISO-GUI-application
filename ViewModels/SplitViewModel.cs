using System.IO;
using Microsoft.Win32;
using Ps3IsoTool.Core;
using Ps3IsoTool.Services;

namespace Ps3IsoTool.ViewModels;

public sealed class SplitViewModel : OperationViewModel
{
    private string _isoPath = "";
    public string IsoPath { get => _isoPath; set => Set(ref _isoPath, value); }

    private string _outputFolder = "";
    public string OutputFolder { get => _outputFolder; set => Set(ref _outputFolder, value); }

    public RelayCommand BrowseIsoCommand { get; }
    public RelayCommand BrowseOutCommand { get; }

    public SplitViewModel()
    {
        BrowseIsoCommand = new RelayCommand(_ =>
        {
            var d = new OpenFileDialog
            {
                Title = "Select PS3 ISO to split",
                Filter = "PS3 ISO (*.iso)|*.iso|All files (*.*)|*.*"
            };
            if (d.ShowDialog() == true) IsoPath = d.FileName;
        });

        BrowseOutCommand = new RelayCommand(_ =>
        {
            var d = new OpenFolderDialog { Title = "Output folder (leave empty = same folder as the ISO)" };
            if (d.ShowDialog() == true) OutputFolder = d.FolderName;
        });
    }

    protected override bool Validate(out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(IsoPath) || !File.Exists(IsoPath))
        {
            error = "Pick a valid .iso file to split.";
            return false;
        }
        return true;
    }

    protected override Task ExecuteAsync(IProgress<OpProgress> progress, CancellationToken ct) =>
        Ps3IsoOps.SplitAsync(IsoPath, string.IsNullOrWhiteSpace(OutputFolder) ? null : OutputFolder, progress, ct);

    protected override void CleanupSource()
    {
        if (File.Exists(IsoPath)) File.Delete(IsoPath);
    }
}
