using System.Diagnostics;
using System.IO;

namespace Ps3IsoTool.Services;

/// One progress tick from a long-running ISO operation.
public readonly record struct OpProgress(
    long ProcessedMb,
    long TotalMb,
    double Percent,
    double SpeedMbs,
    double EtaSeconds,
    int PartNumber,
    string PartName);

/// Native C# ports of the PS3Utils split/merge operations — no external exe/dll/C.
/// Split matches splitps3iso.c exactly: 0xFFFF0000 (~4 GB) parts named base.0, base.1, …,
/// copied in 64 KB buffers. Merge is the inverse (concatenate base.0 + base.1 + …).
public static class Ps3IsoOps
{
    public const long SplitSize  = 0xFFFF0000; // 4,294,901,760 bytes — FAT32-safe, matches splitps3iso
    public const int  BufferSize = 0x10000;    // 64 KB — matches splitps3iso BUFFER_SIZE
    private const long MB = 1024 * 1024;

    public static Task SplitAsync(string isoPath, string? outputFolder,
        IProgress<OpProgress> progress, CancellationToken ct)
    {
        if (!File.Exists(isoPath))
            throw new FileNotFoundException("ISO file not found.", isoPath);

        long total = new FileInfo(isoPath).Length;
        string outBase = string.IsNullOrWhiteSpace(outputFolder)
            ? isoPath
            : Path.Combine(outputFolder, Path.GetFileName(isoPath));
        if (!string.IsNullOrWhiteSpace(outputFolder))
            Directory.CreateDirectory(outputFolder);

        return Task.Run(() =>
        {
            var buffer = new byte[BufferSize];
            var created = new List<string>();
            long processed = 0, count = 0;
            int part = 0;
            var sw = Stopwatch.StartNew();
            long lastTick = 0, lastBytes = 0;
            double speed = 0;

            using var input = new FileStream(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);

            FileStream OpenPart(int idx)
            {
                string p = $"{outBase}.{idx}";
                created.Add(p);
                return new FileStream(p, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
            }

            var output = OpenPart(part);
            try
            {
                int len;
                while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, len);
                    count += len;
                    processed += len;

                    long ms = sw.ElapsedMilliseconds;
                    if (ms - lastTick >= 500)
                    {
                        double interval = (ms - lastTick) / 1000.0;
                        if (interval > 0.01)
                            speed = (processed - lastBytes) / interval / MB;
                        lastTick = ms;
                        lastBytes = processed;

                        double pct = total > 0 ? processed * 100.0 / total : 0;
                        double eta = speed > 0.1 ? (total - processed) / (speed * MB) : 0;
                        progress.Report(new OpProgress(processed / MB, total / MB, pct, speed, eta,
                            part + 1, Path.GetFileName($"{outBase}.{part}")));
                    }

                    if (count >= SplitSize)
                    {
                        output.Dispose();
                        part++;
                        count = 0;
                        output = OpenPart(part);
                    }
                }
                output.Dispose();
            }
            catch
            {
                output.Dispose();
                foreach (var f in created) TryDelete(f); // clean up partial parts on cancel/error
                throw;
            }
        }, ct);
    }

    public static Task MergeAsync(string firstPartPath, string? outputIsoPath,
        IProgress<OpProgress> progress, CancellationToken ct)
    {
        if (!File.Exists(firstPartPath))
            throw new FileNotFoundException("First split part not found.", firstPartPath);

        // base = the part path with its trailing ".0"/".N" numeric extension stripped.
        string baseName = firstPartPath;
        string ext = Path.GetExtension(firstPartPath);
        if (ext.Length >= 2 && int.TryParse(ext.AsSpan(1), out int _))
            baseName = firstPartPath[..^ext.Length];

        var parts = new List<string>();
        for (int i = 0; File.Exists($"{baseName}.{i}"); i++)
            parts.Add($"{baseName}.{i}");
        if (parts.Count == 0) parts.Add(firstPartPath);

        string outPath = string.IsNullOrWhiteSpace(outputIsoPath) ? baseName : outputIsoPath!;
        if (parts.Any(p => string.Equals(p, outPath, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Output path would overwrite a source part — choose a different name.");

        long total = parts.Sum(p => new FileInfo(p).Length);

        return Task.Run(() =>
        {
            var buffer = new byte[BufferSize];
            long processed = 0;
            var sw = Stopwatch.StartNew();
            long lastTick = 0, lastBytes = 0;
            double speed = 0;
            bool ok = false;

            using var output = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
            try
            {
                for (int idx = 0; idx < parts.Count; idx++)
                {
                    using var input = new FileStream(parts[idx], FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                    int len;
                    while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        output.Write(buffer, 0, len);
                        processed += len;

                        long ms = sw.ElapsedMilliseconds;
                        if (ms - lastTick >= 500)
                        {
                            double interval = (ms - lastTick) / 1000.0;
                            if (interval > 0.01)
                                speed = (processed - lastBytes) / interval / MB;
                            lastTick = ms;
                            lastBytes = processed;

                            double pct = total > 0 ? processed * 100.0 / total : 0;
                            double eta = speed > 0.1 ? (total - processed) / (speed * MB) : 0;
                            progress.Report(new OpProgress(processed / MB, total / MB, pct, speed, eta,
                                idx + 1, Path.GetFileName(parts[idx])));
                        }
                    }
                }
                ok = true;
            }
            finally
            {
                if (!ok)
                {
                    output.Dispose();
                    TryDelete(outPath); // don't leave a half-merged ISO behind
                }
            }
        }, ct);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
