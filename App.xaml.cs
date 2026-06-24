using System.Runtime.InteropServices;
using System.Windows;

namespace Ps3IsoTool;

public partial class App : Application
{
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int processId);
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    private const int AttachParentProcess = -1;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // CLI fallback: any args => run headless (no GUI window), print to the parent console.
        // e.g.  Ps3IsoTool.exe split "game.iso"   /   Ps3IsoTool.exe merge "game.iso.0"
        if (e.Args.Length > 0)
        {
            AttachConsole(AttachParentProcess);
            int code = Cli.Run(e.Args);
            FreeConsole();
            Shutdown(code);
            return;
        }

        new MainWindow().Show();
    }
}
