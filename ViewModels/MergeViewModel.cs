using System.IO;
using Microsoft.Win32;
using Ps3IsoTool.Core;
using Ps3IsoTool.Services;

namespace Ps3IsoTool.ViewModels;

public sealed class MergeViewModel : OperationViewModel
{
    private string _firstPart = "";
    public string FirstPart { get => _firstPart; set => Set(ref _firstPart, value); }

    private string _outputIso = "";
    public string OutputIso { get => _outputIso; set => Set(ref _outputIso, value); }

    public RelayCommand BrowsePartCommand { get; }
    public RelayCommand BrowseOutCommand { get; }

    public MergeViewModel()
    {
        BrowsePartCommand = new RelayCommand(_ =>
        {
            var d = new OpenFileDialog
            {
                Title = "Select the first split part (…iso.0)",
                Filter = "First split part (*.0)|*.0|All files (*.*)|*.*"
            };
            if (d.ShowDialog() != true) return;
            FirstPart = d.FileName;

            // Suggest the reassembled name (strip the trailing .0).
            if (string.IsNullOrWhiteSpace(OutputIso))
            {
                string ext = Path.GetExtension(d.FileName);
                if (ext.Length >= 2 && int.TryParse(ext.AsSpan(1), out int _))
                    OutputIso = d.FileName[..^ext.Length];
            }
        });

        BrowseOutCommand = new RelayCommand(_ =>
        {
            var d = new SaveFileDialog
            {
                Title = "Save merged ISO as",
                Filter = "PS3 ISO (*.iso)|*.iso|All files (*.*)|*.*"
            };
            if (d.ShowDialog() == true) OutputIso = d.FileName;
        });
    }

    protected override bool Validate(out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(FirstPart) || !File.Exists(FirstPart))
        {
            error = "Pick the first split part (…iso.0).";
            return false;
        }
        return true;
    }

    protected override Task ExecuteAsync(IProgress<OpProgress> progress, CancellationToken ct) =>
        Ps3IsoOps.MergeAsync(FirstPart, string.IsNullOrWhiteSpace(OutputIso) ? null : OutputIso, progress, ct);

    protected override void CleanupSource()
    {
        // delete every part .0/.1/.2…
        string ext = Path.GetExtension(FirstPart);
        string baseName = (ext.Length >= 2 && int.TryParse(ext.AsSpan(1), out int _)) ? FirstPart[..^ext.Length] : FirstPart;
        for (int i = 0; File.Exists($"{baseName}.{i}"); i++) File.Delete($"{baseName}.{i}");
    }
}
