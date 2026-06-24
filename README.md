# PS3 ISO Tool

A native **C# / .NET 8 / WPF** rewrite of the PS3 ISO GUI — for managing personal PS3 ISO
backups made from your own game discs. Same architecture and dark theme as the game launcher;
**zero NuGet dependencies**, and **no external exe/dll/C** — the operations are ported straight
into C#.

## Why the rewrite

The Python/tkinter version lagged because each tool was effectively its own embedded
"browser-tab" app. Here every tab is just a lightweight WPF view bound via MVVM, and every
operation runs `async` off the UI thread — the window stays responsive even mid-split, with a
real progress bar (MB done/total · % · MB/s · ETA · part) and a working **Cancel**.

## Status — all five tools native & verified on a real game

| Tool      | State                                                                                   |
|-----------|-----------------------------------------------------------------------------------------|
| Extract   | ✅ port of `extractps3iso.c` — Joliet walk, split-aware reads                            |
| Make ISO  | ✅ port of `makeps3iso.c` — ISO9660 + Joliet + PS3 header; **extract→make→extract = byte-identical** |
| Split     | ✅ `0xFFFF0000` (~4 GB) parts `game.iso.0/.1/…`, 64 KB buffers                           |
| Merge     | ✅ recombines `.0 + .1 + …` into one `.iso`                                              |
| Patch     | ✅ port of `patchps3iso.c` — lowers PS3_SYSTEM_VER + EBOOT/SELF/SPRX firmware field      |

Each is a faithful translation of the original C, so output matches the original tools. Verified
against a real split PS3 game (007 Legends): merge size + PVD, extract magics, full extract→make→extract
round-trip byte-identical, and patch lowering PS3_SYSTEM_VER to 04.2100. **The only untested step is
booting a rebuilt ISO on real hardware.**

## CLI (headless — no GUI window)

```
Ps3IsoTool extract <iso|iso.0> [outFolder] [-s]
Ps3IsoTool make    <gameFolder> [outIso]   [-s]
Ps3IsoTool split   <iso>        [outFolder]
Ps3IsoTool merge   <firstPart.0>[outIso]
Ps3IsoTool patch   <iso|iso.0>  [cfwVersion]    # in place; default 4.21
```
`-s` = split big output into ~4 GB FAT32 parts. Run via `Start-Process -Wait` to block the prompt.

## Build / run

.NET 8 SDK is installed per-user at `C:\Users\rich\.dotnet`.

```
dotnet run                                  # from this folder
```

Standalone single-file exe:

```
dotnet publish -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:PublishReadyToRun=true
```

(WPF has no NativeAOT — ReadyToRun + single-file is the fast path.)

## Credits / license

Native C# ports of the **PS3Utils** tools by **Estwald / Hermes** (makeps3iso, extractps3iso,
patchps3iso) and **Bucanero** (splitps3iso), upstream
[bucanero/ps3iso-utils](https://github.com/bucanero/ps3iso-utils). Those tools are GPLv3, so this
derivative is **GPLv3** too — see [LICENSE](LICENSE). For managing personal backups of discs you own.
