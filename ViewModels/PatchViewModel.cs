using System.IO;
using Microsoft.Win32;
using Ps3IsoTool.Core;
using Ps3IsoTool.Services;

namespace Ps3IsoTool.ViewModels;

public sealed class PatchViewModel : OperationViewModel
{
    private string _isoPath = "";
    public string IsoPath { get => _isoPath; set => Set(ref _isoPath, value); }

    private string _cfwVersion = "4.21";
    public string CfwVersion { get => _cfwVersion; set => Set(ref _cfwVersion, value); }

    public RelayCommand BrowseIsoCommand { get; }

    public PatchViewModel()
    {
        BrowseIsoCommand = new RelayCommand(_ =>
        {
            var d = new OpenFileDialog
            {
                Title = "Select PS3 ISO (or first split part) to patch — edited in place",
                Filter = "PS3 ISO or first part (*.iso;*.0)|*.iso;*.0|All files (*.*)|*.*"
            };
            if (d.ShowDialog() == true) IsoPath = d.FileName;
        });
    }

    protected override bool Validate(out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(IsoPath) || !File.Exists(IsoPath)) { error = "Pick a valid .iso or .iso.0 file."; return false; }
        if (string.IsNullOrWhiteSpace(CfwVersion)) { error = "Enter a CFW version (e.g. 4.21)."; return false; }
        return true;
    }

    protected override Task ExecuteAsync(IProgress<OpProgress> progress, CancellationToken ct) =>
        Ps3IsoPatch.PatchAsync(IsoPath, CfwVersion, progress, ct);
}
