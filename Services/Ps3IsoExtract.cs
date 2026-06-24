using System.Diagnostics;
using System.IO;
using System.Text;

namespace Ps3IsoTool.Services;

/// Native C# port of extractps3iso.c (Estwald/Hermes). Reads the Joliet (UTF-16BE)
/// supplementary volume descriptor at sector 17, walks the path table to enumerate
/// directories, then parses each directory's records and streams every file out.
/// Split-aware: the input can be a single .iso or the first part (.iso.0) of a split set.
public static class Ps3IsoExtract
{
    private const int Sector = 2048;
    private const long MB = 1024 * 1024;
    private const int Chunk = 0x40000;          // 256 KB read/write chunk (matches the C)
    private const long FatSplitBytes = 0x40000000; // 1 GB output parts when -s is used

    public static Task ExtractAsync(string isoOrFirstPart, string? outputFolder, bool splitBigFiles,
        IProgress<OpProgress> progress, CancellationToken ct)
    {
        if (!File.Exists(isoOrFirstPart))
            throw new FileNotFoundException("ISO / first part not found.", isoOrFirstPart);

        // Resolve the input part list (single .iso, or .iso.0 + .iso.1 + …) and the base name.
        var (parts, baseName) = ResolveInput(isoOrFirstPart);
        string outRoot = string.IsNullOrWhiteSpace(outputFolder)
            ? Path.Combine(Path.GetDirectoryName(isoOrFirstPart) ?? ".", baseName)
            : Path.Combine(outputFolder, baseName);

        return Task.Run(() =>
        {
            using var iso = new PartReader(parts);
            Directory.CreateDirectory(outRoot);

            // Joliet supplementary volume descriptor @ sector 17 (offset 0x8800).
            var desc = new byte[Sector];
            iso.ReadAt(0x8800, desc, 0, Sector);
            if (!(desc[0] == 2 && desc[1] == 'C' && desc[2] == 'D' && desc[3] == '0' && desc[4] == '0' && desc[5] == '1'))
                throw new InvalidDataException("Joliet (UTF-16) volume descriptor not found — not a standard PS3 ISO.");

            long toc = (uint)Num733(desc, 80);          // volume_space_size (total sectors)
            int lba0 = Num731(desc, 140);               // type_l_path_table LBA
            int size0 = Num733(desc, 132);              // path_table_size (bytes)

            // Read the whole path table.
            var pt = new byte[size0];
            iso.ReadAt((long)lba0 * Sector, pt, 0, size0);

            var dirs = new List<(int parent, string name)> { (0, "/") }; // idx 0 = root
            var sw = Stopwatch.StartNew();
            long lastTick = 0, lastFlba = 0; double speed = 0;
            long flba = 0; int fileCount = 0;

            void Report(string current)
            {
                long ms = sw.ElapsedMilliseconds;
                if (ms - lastTick < 500) return;
                double interval = (ms - lastTick) / 1000.0;
                if (interval > 0.01) speed = (flba - lastFlba) * Sector / interval / MB;
                lastTick = ms; lastFlba = flba;
                double pct = toc > 0 ? flba * 100.0 / toc : 0;
                double eta = speed > 0.1 ? (toc - flba) * (double)Sector / (speed * MB) : 0;
                progress.Report(new OpProgress(flba * Sector / MB, toc * Sector / MB, pct, speed, eta, fileCount, current));
            }

            // Walk the path table; each entry is a directory.
            int p = 0, idx = 0;
            while (p < size0)
            {
                int snamelen = Num721(pt, p);
                if (snamelen == 0) p = (p / Sector) * Sector + Sector; // pad to next sector
                p += 2;
                int dirLba = Num731(pt, p); p += 4;
                int parent = Num721(pt, p); p += 2;
                string name = Utf16Be(pt, p, snamelen);

                if (idx > 0) dirs.Add((parent, "/" + name));
                string relDir = GetIsoPath(dirs, idx);

                string dirOnDisk = outRoot + relDir.Replace('/', Path.DirectorySeparatorChar);
                Directory.CreateDirectory(dirOnDisk);

                ExtractDirectory(iso, dirLba, outRoot, relDir, splitBigFiles, ct,
                    ref flba, ref fileCount, Report);

                p += snamelen;
                if ((snamelen & 1) != 0) p++;
                idx++;
            }

            // final tick
            progress.Report(new OpProgress(toc * Sector / MB, toc * Sector / MB, 100, 0, 0, fileCount, ""));
        }, ct);
    }

    /// Parse one directory's records (starting at dirLba) and write its files.
    private static void ExtractDirectory(PartReader iso, int dirLba, string outRoot, string relDir,
        bool splitBigFiles, CancellationToken ct, ref long flba, ref int fileCount, Action<string> report)
    {
        var buf = new byte[Sector * 2]; // current sector in [0..2047], staged next sector in [2048..]
        int lba = dirLba;
        int q2 = 0, sizeDirectory = 0;

        iso.ReadAt((long)lba * Sector, buf, 0, Sector);
        Array.Clear(buf, Sector, Sector);

        // First record is "." — gives the directory's total size.
        if (!(buf[32] == 1 && buf[33] == 0 && Num731(buf, 2) == lba && buf[25] == 0x2))
            throw new InvalidDataException($"Bad first directory record at LBA {lba}.");
        sizeDirectory = Num733(buf, 10);

        int q = 0;
        bool correction = false;
        string fileAux = "";       // multi-extent batch: name of file being accumulated
        int fileLba = 0; long fileSize = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (correction)
            {
                correction = false;
                q -= Sector;
                Buffer.BlockCopy(buf, Sector, buf, 0, Sector);
                Array.Clear(buf, Sector, Sector);
                lba++; q2 += Sector;
            }

            if (q2 >= sizeDirectory) return;

            int len = buf[q];

            // Record spans into the next sector — stage it contiguously after the current one.
            if (len != 0 && len + q > Sector)
            {
                iso.ReadAt((long)lba * Sector + Sector, buf, Sector, Sector);
                correction = true;
            }

            if (len == 0 && (Sector - q) > 255) return;

            // End of this sector's records → advance to the next sector.
            if ((len == 0 && q != 0) || q == Sector)
            {
                lba++; q2 += Sector;
                if (q2 >= sizeDirectory) return;
                iso.ReadAt((long)lba * Sector, buf, 0, Sector);
                Array.Clear(buf, Sector, Sector);
                q = 0;
                len = buf[q];
                if (len == 0 || (buf[q + 32] == 1 && buf[q + 33] == 0)) return;
            }

            int nameLen = buf[q + 32];
            int flags = buf[q + 25];

            // File record? (not a directory, name ends with ";1")
            if (nameLen > 1 && flags != 0x2 &&
                buf[q + 33 + nameLen - 1] == (byte)'1' && buf[q + 33 + nameLen - 3] == (byte)';')
            {
                string nm = Utf16Be(buf, q + 33, nameLen);

                if (fileAux.Length != 0)
                {
                    if (nm != fileAux) throw new InvalidDataException($"Batch-file mismatch: {fileAux}");
                    fileSize += (uint)Num733(buf, q + 10);
                    if (flags == 0x80) { q += len; continue; }   // more extents follow
                    fileAux = "";
                }
                else
                {
                    fileLba = Num733(buf, q + 2);
                    fileSize = (uint)Num733(buf, q + 10);
                    if (flags == 0x80) { fileAux = nm; q += len; continue; }
                }

                string cleanName = nm[..^2]; // strip ";1"
                string rel = (relDir == "/" ? "/" : relDir + "/") + cleanName;
                string outPath = outRoot + rel.Replace('/', Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

                WriteFile(iso, outPath, fileLba, fileSize, splitBigFiles, ct, ref flba, () => report(rel));
                fileCount++;
            }

            q += len;
        }
    }

    private static void WriteFile(PartReader iso, string outPath, int fileLba, long fileSize,
        bool splitBigFiles, CancellationToken ct, ref long flba, Action report)
    {
        var data = new byte[Chunk];
        bool useSplit = splitBigFiles && fileSize >= 0xFFFF0001L;
        int splitIdx = 0;
        long splitWritten = 0;

        FileStream OpenOut() => new(useSplit ? $"{outPath}.666{splitIdx:00}" : outPath,
            FileMode.Create, FileAccess.Write, FileShare.None, Chunk);

        var outFs = OpenOut();
        try
        {
            long lba = fileLba;
            while (fileSize > 0)
            {
                ct.ThrowIfCancellationRequested();

                if (useSplit && splitWritten >= FatSplitBytes)
                {
                    splitWritten = 0;
                    outFs.Dispose();
                    splitIdx++;
                    outFs = OpenOut();
                }

                int fsize = fileSize > Chunk ? Chunk : (int)fileSize;
                iso.ReadAt(lba * Sector, data, 0, fsize);
                outFs.Write(data, 0, fsize);

                if (useSplit) splitWritten += fsize;
                fileSize -= fsize;
                lba += (fsize + 2047) / Sector;
                flba += (fsize + 2047) / Sector;
                report();
            }
        }
        finally { outFs.Dispose(); }
    }

    /// Build a directory's full ISO path by walking parents (mirrors get_iso_path).
    private static string GetIsoPath(List<(int parent, string name)> dirs, int idx)
    {
        if (idx == 0) return "/";
        string path = "";
        while (true)
        {
            path = dirs[idx].name + path;
            idx = dirs[idx].parent - 1;
            if (idx == 0) break;
        }
        return path;
    }

    private static (List<(string path, long size)> parts, string baseName) ResolveInput(string input)
    {
        var parts = new List<(string, long)>();
        string lower = input.ToLowerInvariant();
        if (lower.EndsWith(".iso"))
        {
            parts.Add((input, new FileInfo(input).Length));
            return (parts, Path.GetFileNameWithoutExtension(input));
        }
        if (lower.EndsWith(".iso.0"))
        {
            string stem = input[..^2]; // strip ".0"
            for (int i = 0; File.Exists($"{stem}.{i}"); i++)
                parts.Add(($"{stem}.{i}", new FileInfo($"{stem}.{i}").Length));
            return (parts, Path.GetFileNameWithoutExtension(stem)); // stem ends ".iso"
        }
        throw new InvalidDataException("Input must be a .iso or .iso.0 file.");
    }

    private static int Num731(byte[] p, int o) => p[o] | p[o + 1] << 8 | p[o + 2] << 16 | p[o + 3] << 24;
    private static int Num733(byte[] p, int o) => Num731(p, o);
    private static int Num721(byte[] p, int o) => p[o] | p[o + 1] << 8;

    private static string Utf16Be(byte[] p, int o, int len)
    {
        if (len <= 0) return "";
        return Encoding.BigEndianUnicode.GetString(p, o, len & ~1);
    }
}

/// Random-access reader over one file or a set of split parts (.0/.1/…), spanning seamlessly.
internal sealed class PartReader : IDisposable
{
    private readonly (string path, long start, long size)[] _parts;
    private readonly FileStream?[] _streams;

    public PartReader(List<(string path, long size)> parts)
    {
        _parts = new (string, long, long)[parts.Count];
        _streams = new FileStream?[parts.Count];
        long start = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            _parts[i] = (parts[i].path, start, parts[i].size);
            start += parts[i].size;
        }
    }

    public void ReadAt(long pos, byte[] buf, int off, int size)
    {
        while (size > 0)
        {
            int i = IndexOf(pos);
            ref var part = ref _parts[i];
            _streams[i] ??= new FileStream(part.path, FileMode.Open, FileAccess.Read, FileShare.Read, 0x10000);
            var fs = _streams[i]!;
            long local = pos - part.start;
            int can = (int)Math.Min(size, part.size - local);
            fs.Seek(local, SeekOrigin.Begin);
            fs.ReadExactly(buf, off, can);
            pos += can; off += can; size -= can;
        }
    }

    private int IndexOf(long pos)
    {
        for (int i = 0; i < _parts.Length; i++)
            if (pos >= _parts[i].start && pos < _parts[i].start + _parts[i].size)
                return i;
        throw new EndOfStreamException($"Read past end of ISO at offset {pos}.");
    }

    public void Dispose()
    {
        foreach (var s in _streams) s?.Dispose();
    }
}
