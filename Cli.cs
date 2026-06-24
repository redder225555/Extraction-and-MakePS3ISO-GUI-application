using Ps3IsoTool.Services;

namespace Ps3IsoTool;

/// Headless command-line entry — lets every operation be scripted/tested without the GUI.
/// Progress is written synchronously from the worker thread (no SynchronizationContext),
/// so the \r-updating line shows live even though the UI thread is blocked waiting.
internal static class Cli
{
    public static int Run(string[] args)
    {
        try
        {
            return RunAsync(args).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nCancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        string cmd = args[0].ToLowerInvariant().TrimStart('-');
        var progress = new ConsoleProgress();

        switch (cmd)
        {
            case "split":
                if (args.Length < 2) return Usage();
                Console.WriteLine($"Splitting: {args[1]}");
                await Ps3IsoOps.SplitAsync(args[1], args.Length > 2 ? args[2] : null, progress, CancellationToken.None);
                Console.WriteLine("\nDone.");
                return 0;

            case "merge":
                if (args.Length < 2) return Usage();
                Console.WriteLine($"Merging from: {args[1]}");
                await Ps3IsoOps.MergeAsync(args[1], args.Length > 2 ? args[2] : null, progress, CancellationToken.None);
                Console.WriteLine("\nDone.");
                return 0;

            case "extract":
                if (args.Length < 2) return Usage();
                bool split = args.Contains("-s") || args.Contains("-S");
                var posArgs = args.Skip(1).Where(a => !a.StartsWith('-')).ToArray();
                string iso = posArgs[0];
                string? outFolder = posArgs.Length > 1 ? posArgs[1] : null;
                Console.WriteLine($"Extracting: {iso}");
                await Ps3IsoExtract.ExtractAsync(iso, outFolder, split, progress, CancellationToken.None);
                Console.WriteLine("\nDone.");
                return 0;

            case "make":
                if (args.Length < 2) return Usage();
                bool msplit = args.Contains("-s") || args.Contains("-S");
                var mArgs = args.Skip(1).Where(a => !a.StartsWith('-')).ToArray();
                string folder = mArgs[0];
                string? outIso = mArgs.Length > 1 ? mArgs[1] : null;
                Console.WriteLine($"Building ISO from: {folder}");
                await Ps3IsoMake.MakeAsync(folder, outIso, msplit, progress, CancellationToken.None);
                Console.WriteLine("\nDone.");
                return 0;

            case "patch":
                if (args.Length < 2) return Usage();
                var pArgs = args.Skip(1).Where(a => !a.StartsWith('-')).ToArray();
                string piso = pArgs[0];
                string ver = pArgs.Length > 1 ? pArgs[1] : "4.21";
                Console.WriteLine($"Patching {piso} to CFW {ver} (in place)…");
                await Ps3IsoPatch.PatchAsync(piso, ver, progress, CancellationToken.None);
                Console.WriteLine("\nDone.");
                return 0;

            case "h":
            case "help":
                return Usage();

            default:
                Console.WriteLine($"Unknown command: {args[0]}");
                return Usage();
        }
    }

    private static int Usage()
    {
        Console.WriteLine(
            "PS3 ISO Tool — CLI\n" +
            "  Ps3IsoTool split <iso> [outputFolder]\n" +
            "  Ps3IsoTool merge <firstPart.0> [outputIso]\n" +
            "  Ps3IsoTool extract <iso|iso.0> [outputFolder] [-s]\n" +
            "  Ps3IsoTool make <gameFolder> [outputIso] [-s]\n" +
            "  Ps3IsoTool patch <iso|iso.0> [cfwVersion]   (in place; e.g. 4.21)\n");
        return 2;
    }
}

internal sealed class ConsoleProgress : IProgress<OpProgress>
{
    public void Report(OpProgress p) =>
        Console.Write($"\r{p.ProcessedMb,9:n0} / {p.TotalMb:n0} MB   {p.Percent,6:n2}%   {p.SpeedMbs,7:n1} MB/s   ETA {Fmt(p.EtaSeconds)}   part {p.PartNumber}   ");

    private static string Fmt(double s)
    {
        if (s <= 0 || double.IsInfinity(s) || double.IsNaN(s)) return "--:--";
        var t = TimeSpan.FromSeconds(s);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";
    }
}
