# Archive Batch Extractor & PS3 ISO Maker

## Requirements

- **7-Zip or WinRAR** (must be installed and their executable paths provided in the app)
  - [7-Zip Download](https://www.7-zip.org/download.html)
  - [WinRAR Download](https://www.win-rar.com/start.html?&L=0)
- **makeps3iso.exe** (required for PS3 ISO creation)
  - [PS3Utils by Release](https://github.com/bucanero/ps3iso-utils/releases)
- **Python 3.8+** (for running from source)
- **pip** (Python package manager)

---

## What is this?

**Archive Batch Extractor & PS3 ISO Maker** is a Windows GUI tool for:

- Batch extracting `.zip`, `.rar`, and `.7z` archives from multiple folders using 7-Zip or WinRAR.
- Creating, extracting, splitting, and patching PS3 ISO images using the official PS3 ISO tools.
- Moving PKG files from multiple subfolders to a single destination.
- Managing extraction/conversion queues, progress, and logs.
- Saving your layout and settings for future sessions.

---

## Features

- **Batch Extraction:** Extract multiple archives from multiple folders at once.
- **Tool Selection:** Choose between 7-Zip and WinRAR for extraction.
- **Password Support:** Optionally provide a password for encrypted archives.
- **Replace/Overwrite:** Choose whether to overwrite existing files.
- **Delete After Extraction:** Optionally delete archives after extraction.
- **Extract Here:** Extract to the archive’s folder or a subfolder.
- **Headless WinRAR:** Run WinRAR extractions in the background (no window).
- **Movable/Resizable UI:** Drag and resize widgets; layout is saved automatically.
- **PS3 ISO Creation:** Batch convert folders to PS3 ISOs using makeps3iso.exe.
- **Split ISO:** Optionally split ISOs into 4GB parts for FAT32 compatibility.
- **PS3 ISO Extraction:** Extract ISO files using extractps3iso.exe, with optional split output.
- **PS3 ISO Splitter:** Split large ISO files (>4GB) using splitps3iso.exe.
- **PS3 ISO Patcher:** Patch ISO files to a specific version using patchps3iso.exe.
- **PKG File Mover:** Move all `.pkg` files from all subfolders to a destination folder.
- **Queue Management:** Add, scan, and remove folders from the processing queue.
- **Logs & Batch Files:** Save logs and generate batch files for folder deletion.
- **Configurable:** All settings and layouts are saved for next time.

---

## Installation & Build

1. Run `pip install pyinstaller` if you don't have it already.
2. Download the repository then `cd` to the folder with `Main.py`.
3. Run the command below to build it, or go to the [releases page](https://github.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/releases) to download the latest built version:

```
pyinstaller --noconfirm --onefile --add-data "license.txt;." Main.py
```

---

## General Usage

### On First Launch:
- You’ll see a license agreement. You must agree to continue.
- Next, you’ll see the requirements window with links to download needed tools.

---

## Tab Overview

### Extraction Software Tab
- **Add Folder:** Add a folder containing archives to the queue.
- **Clear Queue:** Remove all folders from the queue.
- **Extraction Tool:** Select 7-Zip or WinRAR.
- **7-Zip/WinRAR Location:** Browse to the executable for your chosen tool.
- **Use Password:** Enable and enter a password for encrypted archives.
- **Show Password:** Toggle password visibility.
- **Replace Existing Files:** Overwrite files if they already exist.
- **Delete Archive After Extraction:** Delete archives after extraction.
- **Extract Here:** Extract to the archive’s folder or a subfolder.
- **Headless WinRAR Extraction:** Run WinRAR in background mode (no window).
- **Extract:** Start extraction for all queued folders.
- **Abort:** Stop the extraction process.
- **Progress Bars:** Show overall and per-file progress.
- **Files Completed/ETA:** Show extraction status.

### Make PS3 ISO Tab
- **MakePS3ISO EXE:** Browse to your makeps3iso.exe.
- **Output Folder:** Where ISOs will be saved.
- **Split ISO into 4GB parts:** Enable for FAT32 drives.
- **Add Folder:** Add a folder for ISO conversion.
- **Scan Base Folder:** Recursively find folders with PS3_GAME.
- **Remove Selected:** Remove a folder from the queue.
- **Start Conversion:** Begin ISO creation for all queued folders.
- **Abort:** Stop the conversion process.
- **Execute Batch File:** Run a batch file to delete converted folders.
- **Open AppData Folder:** Open the folder where logs/configs are stored.
- **Log:** Shows conversion progress and results.

### Extract PS3 ISO Tab
- **extractps3iso EXE:** Browse to your extractps3iso.exe.
- **Output Folder:** Where extracted files will be saved.
- **Split extracted files (-s):** Enable to split output files for FAT32.
- **Add ISO:** Add a single ISO file to the queue.
- **Scan Base Folder:** Recursively find all `.iso` files in a folder and subfolders.
- **Remove Selected:** Remove selected ISOs from the queue.
- **Start Extraction:** Extract all queued ISOs.
- **Abort:** Stop the extraction process.
- **Save Completed Log:** Save a log of extracted ISOs.
- **Create Batch File:** Generate a batch file to delete extracted ISOs.
- **Execute Batch File:** Run the batch file.
- **Open AppData Folder:** Open the folder where logs/configs are stored.

### Split PS3 ISO Tab
- **splitps3iso EXE:** Browse to your splitps3iso.exe.
- **Add ISO:** Add a single ISO file to the queue (only ISOs larger than 4GB are accepted).
- **Scan Base Folder:** Recursively find all `.iso` files larger than 4GB in a folder and subfolders.
- **Remove Selected:** Remove selected ISOs from the queue.
- **Start Splitting:** Split all queued ISOs.
- **Abort:** Stop the splitting process.
- **Save Completed Log:** Save a log of split ISOs.
- **Create Batch File:** Generate a batch file to delete split ISOs.
- **Execute Batch File:** Run the batch file.
- **Open AppData Folder:** Open the folder where logs/configs are stored.

### Patch PS3 ISO Tab
- **patchps3iso EXE:** Browse to your patchps3iso.exe.
- **Version:** Enter the version string to patch ISOs to (e.g., `4.82`).
- **Add ISO:** Add a single ISO file to the queue.
- **Scan Base Folder:** Recursively find all `.iso` files in a folder and subfolders.
- **Remove Selected:** Remove selected ISOs from the queue.
- **Start Patching:** Patch all queued ISOs to the specified version.
- **Abort:** Stop the patching process.
- **Save Completed Log:** Save a log of patched ISOs.
- **Create Batch File:** Generate a batch file to delete patched ISOs.
- **Execute Batch File:** Run the batch file.
- **Open AppData Folder:** Open the folder where logs/configs are stored.

### Move PKG GUI Tab
- **Source Folder:** Select the root folder to search for `.pkg` files (recursively).
- **Destination Folder:** Select the folder to move all found `.pkg` files to.
- **Move PKG Files:** Move all `.pkg` files from all subfolders of the source to the destination.
- **Log:** Shows results of the move operation.

---

## Button & Checkbox Reference

| Button/Checkbox                  | Function                                                                 |
|----------------------------------|--------------------------------------------------------------------------|
| Add Folder / Add ISO             | Add a folder or ISO to the queue                                         |
| Clear Queue / Remove Selected    | Remove all or selected items from the queue                              |
| Extraction Tool                  | Select 7-Zip or WinRAR                                                  |
| 7-Zip/WinRAR Location            | Set the path to the extraction tool executable                          |
| Use Password                     | Enable password for encrypted archives                                  |
| Show Password                    | Show/hide the password                                                  |
| Replace Existing Files           | Overwrite files if they already exist                                   |
| Delete Archive After Extraction  | Delete the archive after extraction                                     |
| Extract Here                     | Extract files to the same folder as the archive                         |
| Headless WinRAR Extraction       | Run WinRAR in background mode (no window)                               |
| Extract / Start Conversion       | Start extraction or ISO creation                                        |
| Abort                            | Abort extraction, conversion, splitting, or patching                    |
| MakePS3ISO/ExtractPS3ISO/SplitPS3ISO/PatchPS3ISO EXE | Set the path to the respective tool executable         |
| Output Folder                    | Set the output folder for ISOs or extracted files                       |
| Split ISO into 4GB parts         | Split ISOs for FAT32 compatibility                                      |
| Scan Base Folder                 | Recursively add folders or ISOs (with size filter for Split)            |
| Start Splitting                  | Start splitting all queued ISOs                                         |
| Start Patching                   | Start patching all queued ISOs                                          |
| Version                          | Set the version for patching ISOs                                       |
| Save Completed Log               | Save a log of completed operations                                      |
| Create Batch File                | Generate a batch file for deletion                                      |
| Execute Batch File               | Run the generated batch file                                            |
| Open AppData Folder              | Open the folder where logs/configs are stored                           |
| Move PKG Files                   | Move all `.pkg` files from all subfolders to the destination            |

---

## Configuration & Layout Files

- **Extraction Software settings:**  
  `%LOCALAPPDATA%\PS3Utils\ES_config.json`
- **Extraction Software layout:**  
  `%LOCALAPPDATA%\PS3Utils\ES_layout.json`
- **MakePS3ISO settings:**  
  `%LOCALAPPDATA%\PS3Utils\ISO-M-config.json`
- **MakePS3ISO logs:**  
  `%LOCALAPPDATA%\PS3Utils\converted_folders.log`
- **MakePS3ISO batch file:**  
  `%LOCALAPPDATA%\PS3Utils\delete_folders.bat`
- **ExtractPS3ISO config/logs:**  
  `%LOCALAPPDATA%\PS3Utils\ISO-E-config.json`, `Extracted_ISOs.log`
- **SplitPS3ISO config/logs:**  
  `%LOCALAPPDATA%\PS3Utils\SplitISO_config.json`, `Split_ISOs.log`
- **PatchPS3ISO config/logs:**  
  `%LOCALAPPDATA%\PS3Utils\ISO-patched-config.json`, `patched_ISOs.log`
- **PKGFileMover config/logs:**  
  `%LOCALAPPDATA%\PS3Utils\PKGMover.json`, `Moved_PKGs.log`

---

## License

This project is licensed under the [GPLv3](https://www.gnu.org/licenses/gpl-3.0.html) to ensure it remains free and open source for all users.

## Support

If you have issues or suggestions, please open an [issue](https://github.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/issues) on GitHub.

---

**Enjoy batch extracting, PS3 ISO making, splitting, patching, extracting, and PKG file management!**