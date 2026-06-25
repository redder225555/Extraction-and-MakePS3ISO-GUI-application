using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace Ps3IsoTool;

public partial class App : Application
{
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int processId);
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    private const int AttachParentProcess = -1;

    private static readonly string CrashLog = Path.Combine(Path.GetTempPath(), "ps3iso_crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (_, ev) => Log(ev.ExceptionObject as Exception, "AppDomain");
        DispatcherUnhandledException += (_, ev) => { Log(ev.Exception, "Dispatcher"); ev.Handled = false; };

        // CLI fallback: any args => run headless (no GUI window), print to the parent console.
        if (e.Args.Length > 0)
        {
            AttachConsole(AttachParentProcess);
            int code = Cli.Run(e.Args);
            FreeConsole();
            Shutdown(code);
            return;
        }

        try
        {
            new MainWindow().Show();
        }
        catch (Exception ex)
        {
            Log(ex, "MainWindow.Show");
            throw;
        }
    }

    private static void Log(Exception? ex, string where)
    {
        try { File.AppendAllText(CrashLog, $"=== {DateTime.Now:O} [{where}] ===\n{ex}\n\n"); } catch { }
    }
}
