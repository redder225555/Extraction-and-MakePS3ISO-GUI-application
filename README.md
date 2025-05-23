# Archive Batch Extractor & PS3 ISO Maker

## Overview

**Archive Batch Extractor & PS3 ISO Maker** is a comprehensive Windows GUI toolkit for PlayStation 3 ISO management and archive extraction. It provides:
- Batch extraction of `.zip`, `.rar`, and `.7z` archives using 7-Zip or WinRAR.
- Creation, extraction, splitting, and patching of PS3 ISO images using official tools.
- PKG file management and movement.
- Real-time progress reporting, logs, and batch file generation.
- All settings and layouts are saved for future sessions.

---

## Table of Contents
- [Requirements](#requirements)
- [Installation & Build](#installation--build)
- [General Usage](#general-usage)
- [Python Scripts Overview](#python-scripts-overview)
- [GUI Tabs & Button Guide](#gui-tabs--button-guide)
- [Running in Terminal](#running-in-terminal)
- [Screenshots](#screenshots)
- [Troubleshooting](#troubleshooting)

---

## Requirements
- **7-Zip or WinRAR** (must be installed and their executable paths provided in the app)
  - [7-Zip Download](https://www.7-zip.org/download.html)
  - [WinRAR Download](https://www.win-rar.com/start.html?&L=0)
- **makeps3iso.exe** (required for PS3 ISO creation)
  - [PS3Utils by Release](https://github.com/bucanero/ps3iso-utils/releases)
- **Python 3.8+** (for running from source)
- **pip** (Python package manager)

---

## Installation & Build

1. **Install Python and pip**
   - Download and install [Python 3.8+](https://www.python.org/downloads/).
   - Ensure `pip` is included in your installation.

2. **Install PyInstaller**
   - Open a terminal (PowerShell or Command Prompt) and run:
     ```sh
     pip install pyinstaller
     ```

3. **Clone or Download the Repository**
   - Download the repository as a ZIP and extract it, or clone with:
     ```sh
     git clone https://github.com/redder225555/Extraction-and-MakePS3ISO-GUI-application.git
     ```

4. **Build the Executable**
   - Open a terminal in the folder containing `Main.py` and run:
     ```sh
     pyinstaller --noconfirm --onefile --add-data "license.txt;." Main.py
     ```
   - The executable will be in the `dist` folder.

5. **(Optional) Run from Source**
   - You can also run the app directly with:
     ```sh
     python Main.py
     ```

---

## General Usage

- On first launch, you will see a license agreement. You must agree to continue.
- The requirements window will show links to download needed tools.
- The main window uses tabs for each major function.
- All logs, settings, and batch files are saved in `%LOCALAPPDATA%/PS3Utils`.

---

## Python Scripts Overview

| Script              | Purpose                                                                 |
|---------------------|-------------------------------------------------------------------------|
| **Main.py**         | Main launcher and tabbed GUI. Loads all other modules.                  |
| **ExtractionSoftware.py** | Batch extraction of archives using 7-Zip or WinRAR.                |
| **MakePS3ISO.py**   | Batch convert folders to PS3 ISOs. Optionally split output.             |
| **ExtractPS3ISO.py**| Extract files from PS3 ISOs. Optionally split output.                   |
| **SplitPS3ISO.py**  | Split large PS3 ISOs into 4GB parts. Real-time progress reporting.      |
| **PatchPS3ISO.py**  | Patch PS3 ISOs to a specific version.                                   |
| **PKGFileMover.py** | Move all `.pkg` files from subfolders to a destination.                 |

---

## GUI Tabs & Button Guide

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
- **Start Extraction:** Begin batch extraction.
- **Save Log:** Save extraction log to file.

### Make PS3 ISO Tab
- **Add Folder:** Add a folder to convert to ISO.
- **Scan Base Folder:** Scan for folders containing PS3 games.
- **Remove Selected:** Remove selected folders from the queue.
- **Output Folder:** Set the output folder for ISOs.
- **Split ISO into 4GB parts:** Enable splitting for FAT32 drives.
- **Unattended (-h):** Run in headless mode.
- **Start Conversion:** Begin ISO creation.
- **Save Completed Log:** Save log of completed conversions.
- **Create Batch File:** Generate a batch file to delete source folders.

### Extract PS3 ISO Tab
- **Add ISO:** Add an ISO file to extract.
- **Scan Base Folder:** Scan for ISOs in a folder.
- **Remove Selected:** Remove selected ISOs from the queue.
- **Output Folder:** Set the output folder for extraction.
- **Split extracted files (-s):** Enable splitting of extracted files.
- **Unattended (-h):** Run in headless mode.
- **Start Extraction:** Begin extraction.
- **Save Completed Log:** Save log of extracted ISOs.
- **Create Batch File:** Generate a batch file to delete extracted ISOs.

### Split PS3 ISO Tab
- **Add ISO:** Add a large ISO file (>4GB) to split.
- **Scan Base Folder:** Scan for large ISOs in a folder.
- **Remove Selected:** Remove selected ISOs from the queue.
- **Output Folder:** Set the output folder for split files.
- **Unattended (-h):** Run in headless mode.
- **Start Splitting:** Begin splitting process.
- **Progress Bar:** Shows real-time progress, percent, ETA, and MB/s.
- **Save Completed Log:** Save log of split ISOs.
- **Create Batch File:** Generate a batch file to delete original ISOs.

### Patch PS3 ISO Tab
- **Add ISO:** Add an ISO file to patch.
- **Scan Base Folder:** Scan for ISOs in a folder.
- **Remove Selected:** Remove selected ISOs from the queue.
- **patchps3iso DLL:** Select the DLL for patching.
- **Version:** Set the patch version.
- **Unattended (-h):** Run in headless mode.
- **Start Patching:** Begin patching process.
- **Save Completed Log:** Save log of patched ISOs.
- **Create Batch File:** Generate a batch file to delete patched ISOs.

### Move PKG GUI Tab
- **Source Folder:** Select the folder to search for `.pkg` files.
- **Destination Folder:** Select where to move `.pkg` files.
- **Move PKG Files:** Move all `.pkg` files from source (and subfolders) to destination.
- **Log:** Shows moved files and errors.

---

## Running in Terminal

To see real-time logs and error messages, it is recommended to run the app from a terminal:

```sh
python Main.py
```

Or, if you built with PyInstaller:

```sh
cd dist
./Main.exe
```

This will keep the terminal open so you can see all output, including progress, errors, and debug info.

---

## Screenshots

> **Note:** As an AI, I cannot generate actual screenshots. Please run the application and use the Print Screen key or Snipping Tool to capture the interface. You can then add images here for reference.

---

## Troubleshooting
- **DLL Not Found:** Ensure all required DLLs are in the `DLLs` folder under `%LOCALAPPDATA%/PS3Utils`.
- **Permission Errors:** Run the app as administrator if you encounter file access issues.
- **Missing Dependencies:** Install all required Python packages with `pip install -r requirements.txt` (if provided).
- **7-Zip/WinRAR Not Detected:** Set the correct path to the executable in the Extraction Software tab.
- **For more help:** Open an issue on the [GitHub repository](https://github.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/issues).

---

**Enjoy batch extracting, PS3 ISO making, splitting, patching, extracting, and PKG file management!**