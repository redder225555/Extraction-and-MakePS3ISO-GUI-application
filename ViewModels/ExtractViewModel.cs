using System.IO;
using Microsoft.Win32;
using Ps3IsoTool.Core;
using Ps3IsoTool.Services;

namespace Ps3IsoTool.ViewModels;

public sealed class ExtractViewModel : OperationViewModel
{
    private string _isoPath = "";
    public string IsoPath { get => _isoPath; set => Set(ref _isoPath, value); }

    private string _outputFolder = "";
    public string OutputFolder { get => _outputFolder; set => Set(ref _outputFolder, value); }

    private bool _splitBigFiles;
    public bool SplitBigFiles { get => _splitBigFiles; set => Set(ref _splitBigFiles, value); }

    public RelayCommand BrowseIsoCommand { get; }
    public RelayCommand BrowseOutCommand { get; }

    public ExtractViewModel()
    {
        BrowseIsoCommand = new RelayCommand(_ =>
        {
            var d = new OpenFileDialog
            {
                Title = "Select PS3 ISO (or first split part) to extract",
                Filter = "PS3 ISO or first part (*.iso;*.0)|*.iso;*.0|All files (*.*)|*.*"
            };
            if (d.ShowDialog() == true) IsoPath = d.FileName;
        });
        BrowseOutCommand = new RelayCommand(_ =>
        {
            var d = new OpenFolderDialog { Title = "Output folder (a sub-folder named after the game is created)" };
            if (d.ShowDialog() == true) OutputFolder = d.FolderName;
        });
    }

    protected override bool Validate(out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(IsoPath) || !File.Exists(IsoPath)) { error = "Pick a valid .iso or .iso.0 file."; return false; }
        return true;
    }

    protected override Task ExecuteAsync(IProgress<OpProgress> progress, CancellationToken ct) =>
        Ps3IsoExtract.ExtractAsync(IsoPath, string.IsNullOrWhiteSpace(OutputFolder) ? null : OutputFolder, SplitBigFiles, progress, ct);
}
