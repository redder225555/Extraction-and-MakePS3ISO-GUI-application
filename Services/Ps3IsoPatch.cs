using System.IO;
using System.Text;

namespace Ps3IsoTool.Services;

/// Native C# port of patchps3iso.c (Estwald/Hermes). Patches an ISO in place so a game
/// that wants a newer firmware will run on a lower CFW: lowers PS3_SYSTEM_VER in PARAM.SFO
/// and the required-version field in EBOOT.BIN / *.SELF / *.SPRX. Split-aware (.iso.0…).
/// NOTE: edits the ISO in place — back up first.
public static class Ps3IsoPatch
{
    private const int Sector = 2048;
    private const long MB = 1024 * 1024;

    public static Task PatchAsync(string isoOrFirstPart, string cfwVersion,
        IProgress<OpProgress> progress, CancellationToken ct)
    {
        if (!File.Exists(isoOrFirstPart))
            throw new FileNotFoundException("ISO / first part not found.", isoOrFirstPart);

        if (string.IsNullOrWhiteSpace(cfwVersion)) cfwVersion = "4.21";
        if (cfwVersion.Length < 4 || cfwVersion[1] != '.' ||
            !char.IsDigit(cfwVersion[0]) || !char.IsDigit(cfwVersion[2]) || !char.IsDigit(cfwVersion[3]))
            throw new ArgumentException("Invalid CFW version (expected e.g. 4.21).");

        int firmware = ((cfwVersion[0] - '0') << 12) | ((cfwVersion[2] - '0') << 8) | ((cfwVersion[3] - '0') << 4) | 0xC;

        var parts = ResolveInput(isoOrFirstPart);

        return Task.Run(() =>
        {
            using var io = new PartIO(parts);
            var desc = new byte[Sector];
            io.ReadAt(0x8800, desc, 0, Sector);
            if (!(desc[0] == 2 && desc[1] == 'C' && desc[2] == 'D' && desc[3] == '0' && desc[4] == '0' && desc[5] == '1'))
                throw new InvalidDataException("Joliet volume descriptor not found — not a standard PS3 ISO.");

            long toc = (uint)Num733(desc, 80);
            int lba0 = Num731(desc, 140);
            int size0 = Num733(desc, 132);

            var pt = new byte[size0];
            io.ReadAt((long)lba0 * Sector, pt, 0, size0);

            var st = new PatchState { Io = io, Firmware = firmware, Toc = toc, Progress = progress, Ct = ct };

            int p = 0;
            while (p < size0)
            {
                int snamelen = Num721(pt, p);
                if (snamelen == 0) p = (p / Sector) * Sector + Sector;
                p += 2;
                int dirLba = Num731(pt, p); p += 4;
                p += 2; // parent (unused here)
                p += snamelen;
                if ((snamelen & 1) != 0) p++;

                PatchDirectory(st, dirLba);
            }

            progress.Report(new OpProgress(toc * Sector / MB, toc * Sector / MB, 100, 0, 0,
                st.SelfPatched, st.ParamPatched ? "PARAM.SFO patched" : ""));
        }, ct);
    }

    private sealed class PatchState
    {
        public PartIO Io = null!;
        public int Firmware;
        public long Toc;
        public long Flba;
        public bool ParamPatched;
        public int SelfPatched;
        public IProgress<OpProgress> Progress = null!;
        public CancellationToken Ct;
    }

    private static void PatchDirectory(PatchState st, int dirLba)
    {
        var buf = new byte[Sector * 2];
        int lba = dirLba, q2 = 0;
        st.Io.ReadAt((long)lba * Sector, buf, 0, Sector);
        Array.Clear(buf, Sector, Sector);

        if (!(buf[32] == 1 && buf[33] == 0 && Num731(buf, 2) == lba && buf[25] == 0x2)) return;
        int sizeDirectory = Num733(buf, 10);

        int q = 0;
        bool correction = false;
        string fileAux = "";
        int fileLba = 0; long fileSize = 0;

        while (true)
        {
            st.Ct.ThrowIfCancellationRequested();
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
            if (len != 0 && len + q > Sector)
            {
                st.Io.ReadAt((long)lba * Sector + Sector, buf, Sector, Sector);
                correction = true;
            }
            if (len == 0 && (Sector - q) > 255) return;
            if ((len == 0 && q != 0) || q == Sector)
            {
                lba++; q2 += Sector;
                if (q2 >= sizeDirectory) return;
                st.Io.ReadAt((long)lba * Sector, buf, 0, Sector);
                Array.Clear(buf, Sector, Sector);
                q = 0;
                len = buf[q];
                if (len == 0 || (buf[q + 32] == 1 && buf[q + 33] == 0)) return;
            }

            int nameLen = buf[q + 32];
            int flags = buf[q + 25];
            if (nameLen > 1 && flags != 0x2 &&
                buf[q + 33 + nameLen - 1] == (byte)'1' && buf[q + 33 + nameLen - 3] == (byte)';')
            {
                string nm = Utf16Be(buf, q + 33, nameLen);
                if (fileAux.Length != 0)
                {
                    if (nm != fileAux) throw new InvalidDataException($"Batch-file mismatch: {fileAux}");
                    fileSize += (uint)Num733(buf, q + 10);
                    if (flags == 0x80) { q += len; continue; }
                    fileAux = "";
                }
                else
                {
                    fileLba = Num733(buf, q + 2);
                    fileSize = (uint)Num733(buf, q + 10);
                    if (flags == 0x80) { fileAux = nm; q += len; continue; }
                }

                string clean = nm[..^2]; // strip ";1"
                PatchFile(st, clean, fileLba, fileSize);

                st.Flba += (fileSize + 2047) / 2048;
                Report(st, clean);
            }
            q += len;
        }
    }

    private static void PatchFile(PatchState st, string name, int fileLba, long fileSize)
    {
        if (name == "PARAM.SFO") { PatchParamSfo(st, fileLba, (int)fileSize); return; }

        bool isExe = name == "EBOOT.BIN"
            || name.EndsWith(".sprx", StringComparison.Ordinal) || name.EndsWith(".SPRX", StringComparison.Ordinal)
            || name.EndsWith(".self", StringComparison.Ordinal) || name.EndsWith(".SELF", StringComparison.Ordinal);
        if (isExe) PatchExe(st, fileLba, name);
    }

    /// patch_exe_error_09: lower the required-firmware field in a SELF/SPRX header.
    private static void PatchExe(PatchState st, int fileLba, string filename)
    {
        long filePos = (long)fileLba * Sector;
        var b4 = new byte[4];
        st.Io.ReadAt(filePos + 0xC, b4, 0, 4);
        uint offsetFw = (uint)((b4[0] << 24) | (b4[1] << 16) | (b4[2] << 8) | b4[3]); // BE32
        offsetFw += 0x1E;

        var b2 = new byte[2];
        st.Io.ReadAt(filePos + offsetFw, b2, 0, 2);
        ushort ver = (ushort)((b2[0] << 8) | b2[1]); // BE16

        int fw = st.Firmware;
        ushort curFirm = (ushort)(((fw >> 12) & 0xF) * 10000 + ((fw >> 8) & 0xF) * 1000 + ((fw >> 4) & 0xF) * 100);

        if (fw >= 0x421C && fw < 0x490C && ver > 42100 && ver <= 49000 && ver > curFirm)
        {
            var outb = new[] { (byte)(curFirm >> 8), (byte)(curFirm & 0xFF) }; // BE16
            st.Io.WriteAt(filePos + offsetFw, outb, 0, 2);
            st.SelfPatched++;
        }
        else if (ver > curFirm)
        {
            throw new InvalidDataException(
                $"{filename} requires firmware {ver / 10000}.{(ver % 10000) / 100:00} — higher than the patchable range for {curFirm / 10000}.{(curFirm % 10000) / 100:00}C.");
        }
    }

    /// param_sfo_util: lower PS3_SYSTEM_VER in PARAM.SFO to the target version.
    private static void PatchParamSfo(PatchState st, int fileLba, int len)
    {
        int fw = st.Firmware;
        int curFirm = ((fw >> 12) & 0xF) * 10000 + ((fw >> 8) & 0xF) * 1000 + ((fw >> 4) & 0xF) * 100;
        string strVersion = $"{curFirm / 10000:00}.{curFirm % 10000:0000}"; // e.g. "04.2100"

        var mem = new byte[len];
        st.Io.ReadAt((long)fileLba * Sector, mem, 0, len);

        int str = mem[8] | (mem[9] << 8);
        int pos = mem[0xc] | (mem[0xd] << 8);
        int indx = 0;
        bool patched = false;
        while (str < len)
        {
            if (mem[str] == 0) break;
            string key = CStr(mem, str);
            if (key == "PS3_SYSTEM_VER")
            {
                string val = CStr(mem, pos);
                if (string.CompareOrdinal(val, strVersion) > 0)
                {
                    var vb = Encoding.ASCII.GetBytes(strVersion);
                    Array.Copy(vb, 0, mem, pos, Math.Min(8, vb.Length));
                    patched = true;
                    break;
                }
            }
            while (str < len && mem[str] != 0) str++;
            str++;
            if (0x1d + indx >= mem.Length) break;
            pos += mem[0x1c + indx] | (mem[0x1d + indx] << 8);
            indx += 16;
        }

        if (patched)
        {
            st.Io.WriteAt((long)fileLba * Sector, mem, 0, len);
            st.ParamPatched = true;
        }
    }

    private static void Report(PatchState st, string current)
    {
        double pct = st.Toc > 0 ? st.Flba * 100.0 / st.Toc : 0;
        st.Progress.Report(new OpProgress(st.Flba * Sector / MB, st.Toc * Sector / MB, pct, 0, 0, st.SelfPatched, current));
    }

    private static List<(string path, long size)> ResolveInput(string input)
    {
        var parts = new List<(string, long)>();
        string lower = input.ToLowerInvariant();
        if (lower.EndsWith(".iso")) { parts.Add((input, new FileInfo(input).Length)); return parts; }
        if (lower.EndsWith(".iso.0"))
        {
            string stem = input[..^2];
            for (int i = 0; File.Exists($"{stem}.{i}"); i++)
                parts.Add(($"{stem}.{i}", new FileInfo($"{stem}.{i}").Length));
            return parts;
        }
        throw new InvalidDataException("Input must be a .iso or .iso.0 file.");
    }

    private static int Num731(byte[] p, int o) => p[o] | p[o + 1] << 8 | p[o + 2] << 16 | p[o + 3] << 24;
    private static int Num733(byte[] p, int o) => Num731(p, o);
    private static int Num721(byte[] p, int o) => p[o] | p[o + 1] << 8;
    private static string Utf16Be(byte[] p, int o, int len) => len <= 0 ? "" : Encoding.BigEndianUnicode.GetString(p, o, len & ~1);

    private static string CStr(byte[] m, int off)
    {
        int end = off;
        while (end < m.Length && m[end] != 0) end++;
        return Encoding.UTF8.GetString(m, off, end - off);
    }
}

/// Random-access read/write over one ISO file or a split set (.0/.1/…), spanning seamlessly.
internal sealed class PartIO : IDisposable
{
    private readonly (string path, long start, long size)[] _parts;
    private readonly FileStream?[] _streams;

    public PartIO(List<(string path, long size)> parts)
    {
        _parts = new (string, long, long)[parts.Count];
        _streams = new FileStream?[parts.Count];
        long start = 0;
        for (int i = 0; i < parts.Count; i++) { _parts[i] = (parts[i].path, start, parts[i].size); start += parts[i].size; }
    }

    private FileStream Stream(int i) =>
        _streams[i] ??= new FileStream(_parts[i].path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 0x10000);

    public void ReadAt(long pos, byte[] buf, int off, int size)
    {
        while (size > 0)
        {
            int i = IndexOf(pos);
            var fs = Stream(i);
            long local = pos - _parts[i].start;
            int can = (int)Math.Min(size, _parts[i].size - local);
            fs.Seek(local, SeekOrigin.Begin);
            fs.ReadExactly(buf, off, can);
            pos += can; off += can; size -= can;
        }
    }

    public void WriteAt(long pos, byte[] buf, int off, int size)
    {
        while (size > 0)
        {
            int i = IndexOf(pos);
            var fs = Stream(i);
            long local = pos - _parts[i].start;
            int can = (int)Math.Min(size, _parts[i].size - local);
            fs.Seek(local, SeekOrigin.Begin);
            fs.Write(buf, off, can);
            pos += can; off += can; size -= can;
        }
    }

    private int IndexOf(long pos)
    {
        for (int i = 0; i < _parts.Length; i++)
            if (pos >= _parts[i].start && pos < _parts[i].start + _parts[i].size) return i;
        throw new EndOfStreamException($"Position {pos} past end of ISO.");
    }

    public void Dispose() { foreach (var s in _streams) s?.Dispose(); }
}
