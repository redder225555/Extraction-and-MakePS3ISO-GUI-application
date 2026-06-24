using System.IO;
using Microsoft.Win32;
using Ps3IsoTool.Core;
using Ps3IsoTool.Services;

namespace Ps3IsoTool.ViewModels;

public sealed class MakeViewModel : OperationViewModel
{
    private string _gameFolder = "";
    public string GameFolder { get => _gameFolder; set => Set(ref _gameFolder, value); }

    private string _outputIso = "";
    public string OutputIso { get => _outputIso; set => Set(ref _outputIso, value); }

    private bool _split;
    public bool Split { get => _split; set => Set(ref _split, value); }

    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand BrowseOutCommand { get; }

    public MakeViewModel()
    {
        BrowseFolderCommand = new RelayCommand(_ =>
        {
            var d = new OpenFolderDialog { Title = "Select the game folder (must contain PS3_GAME/PARAM.SFO)" };
            if (d.ShowDialog() == true)
            {
                GameFolder = d.FolderName;
                if (string.IsNullOrWhiteSpace(OutputIso))
                    OutputIso = d.FolderName.TrimEnd('\\', '/') + ".iso";
            }
        });
        BrowseOutCommand = new RelayCommand(_ =>
        {
            var d = new SaveFileDialog { Title = "Save ISO as", Filter = "PS3 ISO (*.iso)|*.iso|All files (*.*)|*.*" };
            if (d.ShowDialog() == true) OutputIso = d.FileName;
        });
    }

    protected override bool Validate(out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(GameFolder) || !Directory.Exists(GameFolder)) { error = "Pick the game folder."; return false; }
        if (!File.Exists(Path.Combine(GameFolder, "PS3_GAME", "PARAM.SFO"))) { error = "No PS3_GAME/PARAM.SFO in that folder."; return false; }
        return true;
    }

    protected override Task ExecuteAsync(IProgress<OpProgress> progress, CancellationToken ct) =>
        Ps3IsoMake.MakeAsync(GameFolder, string.IsNullOrWhiteSpace(OutputIso) ? null : OutputIso, Split, progress, ct);

    protected override void CleanupSource()
    {
        if (Directory.Exists(GameFolder)) Directory.Delete(GameFolder, true);
    }
}
