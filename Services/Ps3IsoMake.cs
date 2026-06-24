using System.IO;
using System.Text;

namespace Ps3IsoTool.Services;

/// Native C# port of makeps3iso.c (Estwald/Hermes). Builds a fresh PS3 ISO from a
/// game folder: ISO9660 + Joliet path tables and directory records, the PS3 region
/// header + volume descriptors, then streams file data. Mirrors the C layout exactly
/// (record sizes, sector packing, parent-sort, NOPS3_UPDATE, multi-extent + .666xx).
public static class Ps3IsoMake
{
    private const int IDR = 34;          // sizeof(struct iso_directory_record)
    private const int IPT = 9;           // sizeof(struct iso_path_table)
    private const int MaxIsoPaths = 4096;
    private const long FilePartBytes = 0xFFFFF800L; // multi-extent split boundary

    public static Task MakeAsync(string folder, string? outputIso, bool split,
        IProgress<OpProgress> progress, CancellationToken ct) =>
        Task.Run(() => new IsoBuilder(folder, outputIso, split, progress, ct).Run(), ct);

    private sealed class DirEntry
    {
        public uint ldir, wdir, llba, wlba;
        public int parent;
        public string name = "";
    }

    private sealed class LogicalFile
    {
        public string Name = "";
        public int Lname;
        public long Size;
        public List<string> Phys = new();
    }

    private sealed class IsoBuilder
    {
        private readonly string _root;
        private readonly string? _outArg;
        private readonly bool _split;
        private readonly IProgress<OpProgress> _progress;
        private readonly CancellationToken _ct;

        private byte[] _sectors = Array.Empty<byte>();
        private readonly List<DirEntry> _dirs = new();
        private int _curIsop;
        private int _lpath, _wpath;
        private int _llba0, _llba1, _wlba0, _wlba1, _dllba, _dwlba, _dlsz, _dwsz;
        private long _flba, _toc, _metaSectors;
        private int _posL0, _posL1, _posW0, _posW1;
        private int _dd = 1, _mm = 1, _aa = 2013, _ho, _mi, _se = 2;
        private string _outputName = "";

        // progress
        private long _lastTick, _lastFlba;
        private double _speed;
        private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

        public IsoBuilder(string root, string? outArg, bool split,
            IProgress<OpProgress> progress, CancellationToken ct)
        {
            _root = root.TrimEnd('/', '\\');
            _outArg = outArg;
            _split = split;
            _progress = progress;
            _ct = ct;
        }

        public void Run()
        {
            if (!Directory.Exists(_root)) throw new DirectoryNotFoundException(_root);

            var sfo = ParseParamSfo(Path.Combine(_root, "PS3_GAME", "PARAM.SFO"))
                ?? throw new InvalidDataException("PARAM.SFO not found (need PS3_GAME/PARAM.SFO).");
            string titleId = sfo.titleId;

            _outputName = ResolveOutputName(_outArg, sfo.titleName, titleId);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_outputName))!);

            _lpath = IPT;
            _wpath = IPT;
            _curIsop = 0;
            CalcEntries(_root, 1);

            _metaSectors = _flba;                 // first file LBA = metadata sector count
            _sectors = new byte[_metaSectors * 2048];

            _posL0 = _llba0 * 2048;
            _posL1 = _llba1 * 2048;
            _posW0 = _wlba0 * 2048;
            _posW1 = _wlba1 * 2048;

            FillEntries(_root, 0);                // writes records, advances _flba to TOC
            _toc = _flba;

            WriteDescriptors(titleId);

            using var writer = new IsoOutputWriter(_outputName, _split);
            writer.Write(_sectors, 0, (int)(_metaSectors * 2048));
            _flba = _metaSectors;
            BuildFileIso(writer, _root, 0);

            _progress.Report(new OpProgress(_toc * 2048 / (1024 * 1024), _toc * 2048 / (1024 * 1024), 100, 0, 0, _curIsop, ""));
        }

        // ── number / date encoders (731 LE32, 732 BE32, 733 both, 721 LE16, 722 BE16, 723 both) ──
        private void Set731(int o, int n) { _sectors[o] = (byte)n; _sectors[o + 1] = (byte)(n >> 8); _sectors[o + 2] = (byte)(n >> 16); _sectors[o + 3] = (byte)(n >> 24); }
        private void Set732(int o, int n) { _sectors[o] = (byte)(n >> 24); _sectors[o + 1] = (byte)(n >> 16); _sectors[o + 2] = (byte)(n >> 8); _sectors[o + 3] = (byte)n; }
        private void Set733(int o, int n) { Set731(o, n); Set732(o + 4, n); }
        private void Set721(int o, int n) { _sectors[o] = (byte)n; _sectors[o + 1] = (byte)(n >> 8); }
        private void Set722(int o, int n) { _sectors[o] = (byte)(n >> 8); _sectors[o + 1] = (byte)n; }
        private void Set723(int o, int n) { Set721(o, n); Set722(o + 2, n); }
        private void SetDate(int o) { _sectors[o] = (byte)((_aa - 1900) & 255); _sectors[o + 1] = (byte)(_mm & 15); _sectors[o + 2] = (byte)(_dd & 31); _sectors[o + 3] = (byte)_ho; _sectors[o + 4] = (byte)_mi; _sectors[o + 5] = (byte)_se; _sectors[o + 6] = 0; }

        private void PutNameUpper(int o, string name)
        {
            for (int i = 0; i < name.Length; i++) _sectors[o + i] = (byte)char.ToUpperInvariant(name[i]);
        }

        private void DateFrom(string path, bool isDir)
        {
            try
            {
                var t = (isDir ? Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path));
                _dd = t.Day; _mm = t.Month; _aa = t.Year; _ho = t.Hour; _mi = t.Minute; _se = t.Second;
            }
            catch { /* keep previous */ }
        }

        private static int Utf16Units(string s) => Encoding.BigEndianUnicode.GetByteCount(s) / 2;

        // ── deterministic directory scan (files + subdirs), with .666xx rejoin ──
        private (List<LogicalFile> files, List<string> subdirs) ScanDir(string fsDir)
        {
            var files = new List<LogicalFile>();
            var subdirs = new List<string>();
            var entries = Directory.GetFileSystemEntries(fsDir);
            Array.Sort(entries, (a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));

            foreach (var full in entries)
            {
                string nm = Path.GetFileName(full);
                if (Directory.Exists(full)) { subdirs.Add(nm); continue; }

                int lname = nm.Length;
                if (lname >= 6 && nm.EndsWith(".66600", StringComparison.Ordinal))
                {
                    string baseNm = nm[..^6];
                    long size = new FileInfo(full).Length;
                    var phys = new List<string> { full };
                    for (int n = 1; n < 100; n++)
                    {
                        string p = Path.Combine(fsDir, $"{baseNm}.666{n:00}");
                        if (!File.Exists(p)) break;
                        size += new FileInfo(p).Length;
                        phys.Add(p);
                    }
                    files.Add(new LogicalFile { Name = baseNm, Lname = baseNm.Length, Size = size, Phys = phys });
                }
                else if (lname >= 6 && nm.Substring(lname - 6, 4) == ".666")
                {
                    // .666xx continuation part — folded into its .66600 above
                }
                else
                {
                    files.Add(new LogicalFile { Name = nm, Lname = lname, Size = new FileInfo(full).Length, Phys = new List<string> { full } });
                }
            }
            return (files, subdirs);
        }

        // ── precompute per-directory record sizes (sectors) + global LBA layout ──
        private void CalcEntries(string fsDir, int parent)
        {
            int ldir = 80, wdir = 80;     // base = "." (40) + ".." (40)
            int cldir = ldir, cwdir = wdir;
            _lpath += _lpath & 1;
            _wpath += _wpath & 1;

            int cur = _curIsop;
            if (cur >= MaxIsoPaths) throw new InvalidDataException("Too many folders.");
            while (_dirs.Count <= cur) _dirs.Add(new DirEntry());
            _dirs[cur].parent = parent;
            if (cur == 0) _dirs[cur].name = "";

            int cur2 = cur;
            _curIsop++;

            var (files, subdirs) = ScanDir(fsDir);

            foreach (var f in files)
            {
                if (f.Lname > 222) throw new InvalidDataException("File name too long.");
                int ls = Utf16Units(f.Name);
                if (ls > 222) throw new InvalidDataException("File name too long.");
                int parts = f.Size != 0 ? (int)(((ulong)f.Size + 0xFFFFF7FFUL) / 0xFFFFF800UL) : 1;
                for (int n = 0; n < parts; n++)
                {
                    int add = IDR + f.Lname - 1 + 8; add += add & 1;
                    cldir += add;
                    if (cldir > 2048) { ldir = (ldir & ~2047) + 2048; cldir = add; } else if (cldir == 2048) cldir = 0;
                    ldir += add;

                    add = IDR - 1 + ls * 2 + 4 + 6; add += add & 1;
                    cwdir += add;
                    if (cwdir > 2048) { wdir = (wdir & ~2047) + 2048; cwdir = add; } else if (cwdir == 2048) cwdir = 0;
                    wdir += add;
                }
            }

            foreach (var sd in subdirs)
            {
                if (sd.Length > 222) throw new InvalidDataException("Folder name too long.");
                int ls = Utf16Units(sd);
                if (ls > 222) throw new InvalidDataException("Folder name too long.");

                _lpath += IPT + sd.Length - 1; _lpath += _lpath & 1;
                int add = IDR + sd.Length - 1 + 6; add += add & 1;
                cldir += add;
                if (cldir > 2048) { ldir = (ldir & ~2047) + 2048; cldir = add; } else if (cldir == 2048) cldir = 0;
                ldir += add;

                _wpath += IPT + ls * 2 - 1; _wpath += _wpath & 1;
                add = IDR + ls * 2 - 1 + 6; add += add & 1;
                cwdir += add;
                if (cwdir > 2048) { wdir = (wdir & ~2047) + 2048; cwdir = add; } else if (cwdir == 2048) cwdir = 0;
                wdir += add;
            }

            _dirs[cur].ldir = (uint)((ldir + 2047) / 2048);
            _dirs[cur].wdir = (uint)((wdir + 2047) / 2048);

            foreach (var sd in subdirs)
            {
                int childIdx = _curIsop;
                while (_dirs.Count <= childIdx) _dirs.Add(new DirEntry());
                _dirs[childIdx].name = sd;
                CalcEntries(Path.Combine(fsDir, sd), cur2 + 1);
            }

            if (cur == 0) LayoutLbas();
        }

        private void LayoutLbas()
        {
            _llba0 = 20;
            int lps = (_lpath + 2047) / 2048, wps = (_wpath + 2047) / 2048;
            _llba1 = _llba0 + lps;
            _wlba0 = _llba1 + lps;
            _wlba1 = _wlba0 + wps;
            _dllba = _wlba1 + wps;
            if (_dllba < 32) _dllba = 32;

            SortByParent();

            _dlsz = 0; _dwsz = 0;
            for (int n = 0; n < _curIsop; n++) { _dlsz += (int)_dirs[n].ldir; _dwsz += (int)_dirs[n].wdir; }

            _dwlba = _dllba + _dlsz;
            _flba = _dwlba + _dwsz;

            int lba0 = _dllba, lba1 = _dwlba;
            for (int n = 0; n < _curIsop; n++)
            {
                _dirs[n].llba = (uint)lba0; _dirs[n].wlba = (uint)lba1;
                lba0 += (int)_dirs[n].ldir; lba1 += (int)_dirs[n].wdir;
            }
        }

        private void SortByParent()
        {
            for (int n = 1; n < _curIsop - 1; n++)
                for (int m = n + 1; m < _curIsop; m++)
                    if (_dirs[n].parent > _dirs[m].parent)
                    {
                        (_dirs[n], _dirs[m]) = (_dirs[m], _dirs[n]);
                        for (int l = n; l < _curIsop; l++)
                        {
                            if (n + 1 == _dirs[l].parent) _dirs[l].parent = m + 1;
                            else if (m + 1 == _dirs[l].parent) _dirs[l].parent = n + 1;
                        }
                    }
        }

        // ── write the four path tables (L/M × ASCII/Joliet) ──
        private void FillDirPath()
        {
            for (int n = 0; n < _curIsop; n++)
            {
                if (n == 0)
                {
                    Set721(_posL0, 1); Set731(_posL0 + 2, (int)_dirs[0].llba); Set721(_posL0 + 6, _dirs[0].parent); _sectors[_posL0 + 8] = 0;
                    _posL0 += IPT - 1 + 1; _posL0 += _posL0 & 1;
                    Set721(_posL1, 1); Set732(_posL1 + 2, (int)_dirs[0].llba); Set722(_posL1 + 6, _dirs[0].parent); _sectors[_posL1 + 8] = 0;
                    _posL1 += IPT - 1 + 1; _posL1 += _posL1 & 1;
                    Set721(_posW0, 1); Set731(_posW0 + 2, (int)_dirs[0].wlba); Set721(_posW0 + 6, _dirs[0].parent); _sectors[_posW0 + 8] = 0;
                    _posW0 += IPT - 1 + 1; _posW0 += _posW0 & 1;
                    Set721(_posW1, 1); Set732(_posW1 + 2, (int)_dirs[0].wlba); Set722(_posW1 + 6, _dirs[0].parent); _sectors[_posW1 + 8] = 0;
                    _posW1 += IPT - 1 + 1; _posW1 += _posW1 & 1;
                    continue;
                }

                string name = _dirs[n].name;
                byte[] w = Encoding.BigEndianUnicode.GetBytes(name);
                int ls = w.Length / 2;

                Set721(_posL0, name.Length); Set731(_posL0 + 2, (int)_dirs[n].llba); Set721(_posL0 + 6, _dirs[n].parent);
                PutNameUpper(_posL0 + 8, name);
                _posL0 += IPT - 1 + name.Length; _posL0 += _posL0 & 1;

                Set721(_posL1, name.Length); Set732(_posL1 + 2, (int)_dirs[n].llba); Set722(_posL1 + 6, _dirs[n].parent);
                PutNameUpper(_posL1 + 8, name);
                _posL1 += IPT - 1 + name.Length; _posL1 += _posL1 & 1;

                Set721(_posW0, ls * 2); Set731(_posW0 + 2, (int)_dirs[n].wlba); Set721(_posW0 + 6, _dirs[n].parent);
                Array.Copy(w, 0, _sectors, _posW0 + 8, ls * 2);
                _posW0 += IPT - 1 + ls * 2; _posW0 += _posW0 & 1;

                Set721(_posW1, ls * 2); Set732(_posW1 + 2, (int)_dirs[n].wlba); Set722(_posW1 + 6, _dirs[n].parent);
                Array.Copy(w, 0, _sectors, _posW1 + 8, ls * 2);
                _posW1 += IPT - 1 + ls * 2; _posW1 += _posW1 & 1;
            }
        }

        // ── write directory records for a level + recurse; assigns file LBAs into _flba ──
        private void FillEntries(string fsDir, int level)
        {
            int idrl0 = (int)_dirs[level].llba * 2048;
            int idrw0 = (int)_dirs[level].wlba * 2048;
            int idrl = idrl0, idrw = idrw0;
            Array.Clear(_sectors, idrl, 2048);
            Array.Clear(_sectors, idrw, 2048);

            int countSec1 = 1, countSec2 = 1;
            int maxSec1 = (int)_dirs[level].ldir, maxSec2 = (int)_dirs[level].wdir;
            int auxParent = _dirs[level].parent - 1;

            if (level == 0) FillDirPath();

            int dot = IDR + 6; dot += dot & 1; // 40

            // "." entries (date = this directory)
            DateFrom(fsDir, true);
            WriteDotRecord(idrl, dot, (int)_dirs[level].llba, (int)_dirs[level].ldir * 2048, 0); idrl += dot;
            WriteDotRecord(idrw, dot, (int)_dirs[level].wlba, (int)_dirs[level].wdir * 2048, 0); idrw += dot;

            // ".." entries (date = parent)
            if (level != 0) DateFrom(Path.GetDirectoryName(fsDir) ?? fsDir, true);
            int par = level == 0 ? 0 : auxParent;
            WriteDotRecord(idrl, dot, (int)_dirs[par].llba, (int)_dirs[par].ldir * 2048, 1); idrl += dot;
            WriteDotRecord(idrw, dot, (int)_dirs[par].wlba, (int)_dirs[par].wdir * 2048, 1); idrw += dot;

            var (files, subdirs) = ScanDir(fsDir);
            bool skipFiles = Path.GetFileName(fsDir) == "PS3_UPDATE"; // NOPS3_UPDATE

            if (!skipFiles)
                foreach (var f in files)
                {
                    _ct.ThrowIfCancellationRequested();
                    byte[] w = Encoding.BigEndianUnicode.GetBytes(f.Name);
                    int ls = w.Length / 2;
                    long remaining = f.Size;
                    int parts = f.Size != 0 ? (int)(((ulong)f.Size + 0xFFFFF7FFUL) / 0xFFFFF800UL) : 1;
                    DateFrom(f.Phys[0], false);

                    for (int n = 0; n < parts; n++)
                    {
                        uint fsize = (parts > 1 && (n + 1) != parts) ? 0xFFFFF800u : (uint)remaining;
                        if (parts > 1 && (n + 1) != parts) remaining -= 0xFFFFF800L;
                        byte flags = (byte)(((n + 1) != parts) ? 0x80 : 0x0);

                        int add = IDR - 1 + (f.Lname + 8); add += add & 1;
                        int cl = ((idrl - idrl0) & 2047) + add;
                        if (cl > 2048) { if (++countSec1 > maxSec1) throw new InvalidDataException("too many entries"); idrl += add - (cl - 2048); Array.Clear(_sectors, idrl, 2048); }
                        _sectors[idrl] = (byte)add; _sectors[idrl + 1] = 0;
                        Set733(idrl + 2, (int)_flba); Set733(idrl + 10, (int)fsize); SetDate(idrl + 18);
                        _sectors[idrl + 25] = flags; _sectors[idrl + 26] = 0; _sectors[idrl + 27] = 0; Set723(idrl + 28, 1);
                        _sectors[idrl + 32] = (byte)(f.Lname + 2);
                        PutNameUpper(idrl + 33, f.Name); _sectors[idrl + 33 + f.Lname] = (byte)';'; _sectors[idrl + 33 + f.Lname + 1] = (byte)'1';
                        idrl += add;

                        add = IDR - 1 + ls * 2 + 4 + 6; add += add & 1;
                        int cw = ((idrw - idrw0) & 2047) + add;
                        if (cw > 2048) { if (++countSec2 > maxSec2) throw new InvalidDataException("too many entries"); idrw += add - (cw - 2048); Array.Clear(_sectors, idrw, 2048); }
                        _sectors[idrw] = (byte)add; _sectors[idrw + 1] = 0;
                        Set733(idrw + 2, (int)_flba); Set733(idrw + 10, (int)fsize); SetDate(idrw + 18);
                        _sectors[idrw + 25] = flags; _sectors[idrw + 26] = 0; _sectors[idrw + 27] = 0; Set723(idrw + 28, 1);
                        _sectors[idrw + 32] = (byte)(ls * 2 + 4);
                        Array.Copy(w, 0, _sectors, idrw + 33, ls * 2);
                        _sectors[idrw + 33 + ls * 2] = 0; _sectors[idrw + 33 + ls * 2 + 1] = (byte)';'; _sectors[idrw + 33 + ls * 2 + 2] = 0; _sectors[idrw + 33 + ls * 2 + 3] = (byte)'1';
                        idrw += add;

                        _flba += ((fsize + 2047u) & ~2047u) / 2048;
                    }
                }

            // subdirectory entries (in directory_iso order)
            for (int n = 1; n < _curIsop; n++)
                if (_dirs[n].parent == level + 1)
                {
                    string name = _dirs[n].name;
                    byte[] w = Encoding.BigEndianUnicode.GetBytes(name);
                    int ls = w.Length / 2;
                    DateFrom(Path.Combine(fsDir, name), true);

                    int add = IDR - 1 + (name.Length + 6); add += add & 1;
                    int cl = ((idrl - idrl0) & 2047) + add;
                    if (cl > 2048) { if (++countSec1 > maxSec1) throw new InvalidDataException("too many entries"); idrl += add - (cl - 2048); Array.Clear(_sectors, idrl, 2048); }
                    _sectors[idrl] = (byte)add; _sectors[idrl + 1] = 0;
                    Set733(idrl + 2, (int)_dirs[n].llba); Set733(idrl + 10, (int)_dirs[n].ldir * 2048); SetDate(idrl + 18);
                    _sectors[idrl + 25] = 0x2; _sectors[idrl + 26] = 0; _sectors[idrl + 27] = 0; Set723(idrl + 28, 1);
                    _sectors[idrl + 32] = (byte)name.Length; PutNameUpper(idrl + 33, name);
                    idrl += add;

                    add = IDR - 1 + ls * 2 + 6; add += add & 1;
                    int cw = ((idrw - idrw0) & 2047) + add;
                    if (cw > 2048) { if (++countSec2 > maxSec2) throw new InvalidDataException("too many entries"); idrw += add - (cw - 2048); Array.Clear(_sectors, idrw, 2048); }
                    _sectors[idrw] = (byte)add; _sectors[idrw + 1] = 0;
                    Set733(idrw + 2, (int)_dirs[n].wlba); Set733(idrw + 10, (int)_dirs[n].wdir * 2048); SetDate(idrw + 18);
                    _sectors[idrw + 25] = 0x2; _sectors[idrw + 26] = 0; _sectors[idrw + 27] = 0; Set723(idrw + 28, 1);
                    _sectors[idrw + 32] = (byte)(ls * 2); Array.Copy(w, 0, _sectors, idrw + 33, ls * 2);
                    idrw += add;
                }

            for (int n = 1; n < _curIsop; n++)
                if (_dirs[n].parent == level + 1)
                    FillEntries(Path.Combine(fsDir, _dirs[n].name), n);
        }

        private void WriteDotRecord(int o, int len, int extent, int size, byte nameByte)
        {
            _sectors[o] = (byte)len; _sectors[o + 1] = 0;
            Set733(o + 2, extent); Set733(o + 10, size); SetDate(o + 18);
            _sectors[o + 25] = 0x2; _sectors[o + 26] = 0; _sectors[o + 27] = 0; Set723(o + 28, 1);
            _sectors[o + 32] = 1; _sectors[o + 33] = nameByte;
        }

        private void Ascii(int o, string s) { var b = Encoding.ASCII.GetBytes(s); Array.Copy(b, 0, _sectors, o, b.Length); }
        private void FillByte(int o, byte v, int n) { for (int i = 0; i < n; i++) _sectors[o + i] = v; }

        // ── PS3 region header (sectors 0-1) + PVD (16) + Joliet SVD (17) + terminator (18) ──
        private void WriteDescriptors(string titleId)
        {
            int toc = (int)_toc;

            _sectors[0x3] = 1;                       // one unencrypted range
            Set732(0x8, 0);                          // first unencrypted sector
            Set732(0xC, toc - 1);                    // last unencrypted sector
            Ascii(0x800, "PlayStation3");
            FillByte(0x810, 0x20, 0x20);
            { var t = Encoding.ASCII.GetBytes(titleId); Array.Copy(t, 0, _sectors, 0x810, Math.Min(10, t.Length)); }
            System.Security.Cryptography.RandomNumberGenerator.Fill(_sectors.AsSpan(0x840, 0x1B0));
            System.Security.Cryptography.RandomNumberGenerator.Fill(_sectors.AsSpan(0x9F0, 0x10));

            // Primary Volume Descriptor @ 0x8000 (ISO9660, ASCII)
            int isd = 0x8000;
            _sectors[isd] = 1; Ascii(isd + 1, "CD001"); _sectors[isd + 6] = 1;
            FillByte(isd + 8, 0x20, 32);
            Ascii(isd + 40, "PS3VOLUME"); FillByte(isd + 49, 0x20, 32 - 9);
            Set733(isd + 80, toc); Set723(isd + 120, 1); Set723(isd + 124, 1); Set723(isd + 128, 2048);
            Set733(isd + 132, _lpath); Set731(isd + 140, _llba0); Set731(isd + 144, 0); Set732(isd + 148, _llba1); Set732(isd + 152, 0);
            WriteRootRecord(isd + 156, (int)_dirs[0].llba, (int)_dirs[0].ldir * 2048);
            FillByte(isd + 190, 0x20, 128); Ascii(isd + 190, "PS3VOLUME");
            FillByte(isd + 318, 0x20, 128); FillByte(isd + 446, 0x20, 128); FillByte(isd + 574, 0x20, 128);
            FillByte(isd + 702, 0x20, 37); FillByte(isd + 739, 0x20, 37); FillByte(isd + 776, 0x20, 37);
            string fecha = MakeVolumeDate();
            Ascii(isd + 813, fecha); Ascii(isd + 830, "0000000000000000"); Ascii(isd + 847, "0000000000000000"); Ascii(isd + 864, "0000000000000000");
            _sectors[isd + 881] = 1;

            // Supplementary (Joliet) Volume Descriptor @ 0x8800 (UTF-16BE)
            isd = 0x8800;
            byte[] vol = Encoding.BigEndianUnicode.GetBytes("PS3VOLUME"); int volLen = vol.Length;
            _sectors[isd] = 2; Ascii(isd + 1, "CD001"); _sectors[isd + 6] = 1;
            FillByte(isd + 8, 0, 32); FillByte(isd + 40, 0, 32); Array.Copy(vol, 0, _sectors, isd + 40, volLen);
            Set733(isd + 80, toc); Set723(isd + 120, 1);
            _sectors[isd + 88] = 0x25; _sectors[isd + 89] = 0x2f; _sectors[isd + 90] = 0x40; // Joliet escape
            Set723(isd + 124, 1); Set723(isd + 128, 2048);
            Set733(isd + 132, _wpath); Set731(isd + 140, _wlba0); Set731(isd + 144, 0); Set732(isd + 148, _wlba1); Set732(isd + 152, 0);
            WriteRootRecord(isd + 156, (int)_dirs[0].wlba, (int)_dirs[0].wdir * 2048);
            FillByte(isd + 190, 0, 128); Array.Copy(vol, 0, _sectors, isd + 190, volLen);
            FillByte(isd + 318, 0, 128); FillByte(isd + 446, 0, 128); FillByte(isd + 574, 0, 128);
            FillByte(isd + 702, 0, 37); FillByte(isd + 739, 0, 37); FillByte(isd + 776, 0, 37);
            Ascii(isd + 813, fecha); Ascii(isd + 830, "0000000000000000"); Ascii(isd + 847, "0000000000000000"); Ascii(isd + 864, "0000000000000000");
            _sectors[isd + 881] = 1;

            // Volume Descriptor Set Terminator @ 0x9000
            _sectors[0x9000] = 255; Ascii(0x9001, "CD001");
        }

        private void WriteRootRecord(int o, int extent, int size)
        {
            _sectors[o] = 34; _sectors[o + 1] = 0;
            Set733(o + 2, extent); Set733(o + 10, size);
            // copy the date from this directory's "." record
            Array.Copy(_sectors, extent * 2048 + 18, _sectors, o + 18, 7);
            _sectors[o + 25] = 2; _sectors[o + 26] = 0; _sectors[o + 27] = 0; Set723(o + 28, 1);
            _sectors[o + 32] = 1; _sectors[o + 33] = 0;
        }

        private static string MakeVolumeDate()
        {
            var t = DateTime.Now;
            return $"{t.Year:0000}{t.Month:00}{t.Day:00}{t.Hour:00}{t.Minute:00}{t.Second:00}00";
        }

        // ── stream file data after the metadata, in the same order fill_entries assigned ──
        private void BuildFileIso(IsoOutputWriter writer, string fsDir, int level)
        {
            var (files, subdirs) = ScanDir(fsDir);
            bool skipFiles = Path.GetFileName(fsDir) == "PS3_UPDATE";

            if (!skipFiles)
                foreach (var f in files)
                {
                    _ct.ThrowIfCancellationRequested();
                    WriteFileData(writer, f);
                }

            for (int n = 1; n < _curIsop; n++)
                if (_dirs[n].parent == level + 1)
                    BuildFileIso(writer, Path.Combine(fsDir, _dirs[n].name), n);

            if (level == 0)
            {
                long gap = _toc - _flba;
                if (gap > 0)
                {
                    var zero = new byte[Math.Min(gap, 128) * 2048];
                    while (gap > 0) { int f = (int)Math.Min(gap, 128); writer.Write(zero, 0, f * 2048); _flba += f; gap -= f; }
                }
            }
        }

        private void WriteFileData(IsoOutputWriter writer, LogicalFile f)
        {
            var buf = new byte[0x40000];
            long remaining = f.Size;
            using var src = new ConcatReader(f.Phys);
            while (remaining > 0)
            {
                _ct.ThrowIfCancellationRequested();
                int fsize = remaining > 0x40000 ? 0x40000 : (int)remaining;
                if (fsize < 0x40000) Array.Clear(buf, 0, 0x40000);
                src.ReadExactly(buf, 0, fsize);
                int lsize = (fsize + 2047) & ~2047;
                writer.Write(buf, 0, lsize);
                _flba += (uint)(lsize / 2048);
                remaining -= fsize;
                Report(f.Name);
            }
        }

        private void Report(string current)
        {
            long ms = _sw.ElapsedMilliseconds;
            if (ms - _lastTick < 500) return;
            double interval = (ms - _lastTick) / 1000.0;
            if (interval > 0.01) _speed = (_flba - _lastFlba) * 2048.0 / interval / (1024 * 1024);
            _lastTick = ms; _lastFlba = _flba;
            double pct = _toc > 0 ? _flba * 100.0 / _toc : 0;
            double eta = _speed > 0.1 ? (_toc - _flba) * 2048.0 / (_speed * 1024 * 1024) : 0;
            _progress.Report(new OpProgress(_flba * 2048 / (1024 * 1024), _toc * 2048 / (1024 * 1024), pct, _speed, eta, _curIsop, current));
        }

        // ── PARAM.SFO: pull TITLE + TITLE_ID ──
        private static (string titleId, string titleName)? ParseParamSfo(string file)
        {
            if (!File.Exists(file)) return null;
            byte[] m = File.ReadAllBytes(file);
            if (m.Length < 0x20) return null;
            int str = m[8] | (m[9] << 8);
            int pos = m[0xc] | (m[0xd] << 8);
            int indx = 0, ct = 0;
            string? tName = null, tId = null;
            while (str < m.Length)
            {
                if (m[str] == 0) break;
                string key = CStr(m, str);
                if (key == "TITLE") { tName = CStr(m, pos, 63); ct++; }
                else if (key == "TITLE_ID")
                {
                    string a = Encoding.ASCII.GetString(m, pos, Math.Min(4, m.Length - pos));
                    string b = CStr(m, pos + 4, 58);
                    tId = a + "-" + b; ct++;
                }
                if (ct == 2) break;
                while (str < m.Length && m[str] != 0) str++;
                str++;
                if (0x1d + indx >= m.Length) break;
                pos += m[0x1c + indx] | (m[0x1d + indx] << 8);
                indx += 16;
            }
            return tId == null ? null : (tId, tName ?? "PS3");
        }

        private static string CStr(byte[] m, int off, int max = 255)
        {
            int end = off;
            while (end < m.Length && m[end] != 0 && end - off < max) end++;
            return Encoding.UTF8.GetString(m, off, end - off);
        }

        private string ResolveOutputName(string? outArg, string title, string titleId)
        {
            string name;
            if (!string.IsNullOrWhiteSpace(outArg))
            {
                name = outArg!;
                if (Directory.Exists(name)) name = Path.Combine(name, DefaultName(title, titleId));
            }
            else name = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_root))!, DefaultName(title, titleId));

            if (!name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) name += ".iso";
            return name;
        }

        private static string DefaultName(string title, string titleId)
        {
            var sb = new StringBuilder();
            foreach (char c in title)
            {
                if (c >= 128 || c < 32) continue;
                if (c == ':' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|') sb.Append('_');
                else if (c == '\\' || c == '/') sb.Append('-');
                else sb.Append(c);
                if (sb.Length >= 32) break;
            }
            string t = sb.ToString().Trim();
            if (t.Length == 0) t = "PS3";
            return $"{t}-{titleId}";
        }
    }

    /// Sequential reader spanning a file's physical parts (single file, or .66600 + .66601 …).
    internal sealed class ConcatReader : IDisposable
    {
        private readonly List<string> _paths;
        private int _idx;
        private FileStream? _fs;

        public ConcatReader(List<string> paths) { _paths = paths; }

        public void ReadExactly(byte[] buf, int off, int count)
        {
            while (count > 0)
            {
                _fs ??= new FileStream(_paths[_idx], FileMode.Open, FileAccess.Read, FileShare.Read, 0x40000);
                int r = _fs.Read(buf, off, count);
                if (r == 0)
                {
                    _fs.Dispose(); _fs = null;
                    if (++_idx >= _paths.Count) throw new EndOfStreamException("ran out of file parts");
                    continue;
                }
                off += r; count -= r;
            }
        }

        public void Dispose() => _fs?.Dispose();
    }

    /// Writes the ISO byte stream to one file, or rolls to .0/.1/… every 0xFFFF0000 bytes.
    internal sealed class IsoOutputWriter : IDisposable
    {
        private const long PartLimit = 0xFFFF0000L;
        private readonly string _base;
        private readonly bool _split;
        private FileStream _fs;
        private int _partIdx;       // 0 = still writing the un-suffixed base file
        private long _partBytes;

        public IsoOutputWriter(string baseName, bool split)
        {
            _base = baseName;
            _split = split;
            _fs = new FileStream(_base, FileMode.Create, FileAccess.Write, FileShare.None, 0x100000);
        }

        public void Write(byte[] buf, int off, int len)
        {
            while (len > 0)
            {
                if (_split && _partBytes == PartLimit) Rollover();
                int can = _split ? (int)Math.Min(len, PartLimit - _partBytes) : len;
                _fs.Write(buf, off, can);
                off += can; len -= can; _partBytes += can;
            }
        }

        private void Rollover()
        {
            _fs.Dispose();
            if (_partIdx == 0)
            {
                // first rollover: base -> base.0, then continue into base.1
                File.Move(_base, _base + ".0", overwrite: true);
                _fs = new FileStream(_base + ".1", FileMode.Create, FileAccess.Write, FileShare.None, 0x100000);
                _partIdx = 1;
            }
            else
            {
                _partIdx++;
                _fs = new FileStream($"{_base}.{_partIdx}", FileMode.Create, FileAccess.Write, FileShare.None, 0x100000);
            }
            _partBytes = 0;
        }

        public void Dispose() => _fs.Dispose();
    }
}
