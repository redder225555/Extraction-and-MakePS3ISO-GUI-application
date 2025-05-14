# Archive Batch Extractor & PS3 ISO Maker

# Requirements

# 7-Zip or WinRAR (must be installed and their executable paths provided in the app)
## 7-Zip [Download](https://www.7-zip.org/download.html)
## WinRAR [Download](https://www.win-rar.com/start.html?&L=0)

# makeps3iso.exe (required for PS3 ISO creation)
## makeps3iso.exe [Release](https://github.com/bucanero/ps3iso-utils/releases/tag/277db7de)

# Python 3.8+ (for running from source)
## pip (Python package manager)

# What is this?
## Archive Batch Extractor & PS3 ISO Maker is a Windows GUI tool for:

Batch extracting .zip, .rar, and .7z archives from multiple folders using 7-Zip or WinRAR.

Creating PS3 ISO images from folders using makeps3iso.exe.

Managing extraction/conversion queues, progress, and logs.

Saving your layout and settings for future sessions.

# Features

Batch Extraction: Extract multiple archives from multiple folders at once.

Tool Selection: Choose between 7-Zip and WinRAR for extraction.

Password Support: Optionally provide a password for encrypted archives.

Replace/Overwrite: Choose whether to overwrite existing files.

Delete After Extraction: Optionally delete archives after extraction.

Extract Here: Extract to the archive’s folder or a subfolder.

Headless WinRAR: Run WinRAR extractions in the background (no window).

Movable/Resizable UI: Drag and resize widgets; layout is saved automatically.

PS3 ISO Creation: Batch convert folders to PS3 ISOs using makeps3iso.exe.

Split ISO: Optionally split ISOs into 4GB parts for FAT32 compatibility.

PKG Destination: Set a destination for PKG files.

Queue Management: Add, scan, and remove folders from the processing queue.

Logs & Batch Files: Save logs and generate batch files for folder deletion.

Configurable: All settings and layouts are saved for next time.

# Installation & Build

run `pip install pyinstaller` if you don't have it already.

Download the repository then `cd` to the folder with `Main.py`.

Run the command below in the location of the repo files you downloaded to build it, or go to the releases page to download the last built version of this program:

```
pyinstaller --noconfirm --onefile --add-data "license.txt;." Main.py
```

# General Usage

## On First Launch:
You’ll see a license agreement. You must agree to continue.
Next, you’ll see the requirements window with links to download needed tools.

# Extraction Software Tab:
## Add Folder: Add a folder containing archives to the queue.
## Clear Queue: Remove all folders from the queue.
## Extraction Tool: Select 7-Zip or WinRAR.
## 7-Zip/WinRAR Location: Browse to the executable for your chosen tool.
## Use Password: Enable and enter a password for encrypted archives.
## Show Password: Toggle password visibility.
## Replace Existing Files: Overwrite files if they already exist.
## Delete Archive After Extraction: Delete archives after extraction.
## Extract Here: Extract to the archive’s folder or a subfolder.
## Headless WinRAR Extraction: Run WinRAR in background mode (no window).
## Extract: Start extraction for all queued folders.
## Abort: Stop the extraction process.
## Progress Bars: Show overall and per-file progress.
## Files Completed/ETA: Show extraction status.

# Make PS3 ISO Tab:

## MakePS3ISO EXE: Browse to your makeps3iso.exe.
## Output Folder: Where ISOs will be saved.
## PKG Destination Folder: (Optional) Where PKG files will be saved.
## Split ISO into 4GB parts: Enable for FAT32 drives.
## Add Folder: Add a folder for ISO conversion.
## Scan Base Folder: Recursively find folders with PS3_GAME.
## Remove Selected: Remove a folder from the queue.
## Start Conversion: Begin ISO creation for all queued folders.
## Abort: Stop the conversion process.
## Execute Batch File: Run a batch file to delete converted folders.
## Open AppData Folder: Open the folder where logs/configs are stored.
## Log: Shows conversion progress and results.

## Button & Checkbox Reference

| Button/Checkbox                  | Function                                                                 |
|----------------------------------|--------------------------------------------------------------------------|
| Add Folder                       | Add a folder to the queue                                               |
| Clear Queue                      | Remove all folders from the queue                                       |
| Extraction Tool                  | Select 7-Zip or WinRAR                                                  |
| 7-Zip/WinRAR Location            | Set the path to the extraction tool executable                          |
| Use Password                     | Enable password for encrypted archives                                  |
| Show Password                    | Show/hide the password                                                  |
| Replace Existing Files           | Overwrite files if they already exist                                   |
| Delete Archive After Extraction  | Delete the archive after extraction                                     |
| Extract Here                     | Extract files to the same folder as the archive                         |
| Headless WinRAR Extraction       | Run WinRAR in background mode (no window)                               |
| Extract                          | Start extraction                                                        |
| Abort                            | Abort extraction                                                        |
| MakePS3ISO EXE                   | Set the path to makeps3iso.exe                                          |
| Output Folder                    | Set the output folder for ISOs                                          |
| PKG Destination Folder           | Set the destination for PKG files                                       |
| Split ISO into 4GB parts         | Split ISOs for FAT32 compatibility                                      |
| Scan Base Folder                 | Recursively add folders with PS3_GAME                                   |
| Remove Selected                  | Remove selected folder from queue                                       |
| Start Conversion                 | Start ISO creation                                                      |
| Execute Batch File               | Run batch file to delete converted folders                              |
| Open AppData Folder              | Open the folder where logs/configs are stored                           |
