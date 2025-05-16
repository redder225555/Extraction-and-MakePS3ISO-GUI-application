import tkinter as tk
from tkinter import filedialog, messagebox
import os
import shutil
from tkinter import ttk

# Paths for AppData storage
appdata_folder = os.path.join(os.getenv("LOCALAPPDATA"), "PS3Utils")
config_file_path = os.path.join(appdata_folder, "PKGMover.json")
log_file_path = os.path.join(appdata_folder, "Moved_PKGs.log")

def create_frame(parent):
    frame = ttk.Frame(parent)

    # Source and destination folder selection
    tk.Label(frame, text="Source Folder:").grid(row=0, column=0, sticky="e", padx=5, pady=5)
    src_entry = tk.Entry(frame, width=60)
    src_entry.grid(row=0, column=1, padx=5, pady=5)
    def browse_src():
        folder = filedialog.askdirectory()
        if folder:
            src_entry.delete(0, tk.END)
            src_entry.insert(0, folder)
    tk.Button(frame, text="Browse", command=browse_src).grid(row=0, column=2, padx=5, pady=5)

    tk.Label(frame, text="Destination Folder:").grid(row=1, column=0, sticky="e", padx=5, pady=5)
    dst_entry = tk.Entry(frame, width=60)
    dst_entry.grid(row=1, column=1, padx=5, pady=5)
    def browse_dst():
        folder = filedialog.askdirectory()
        if folder:
            dst_entry.delete(0, tk.END)
            dst_entry.insert(0, folder)
    tk.Button(frame, text="Browse", command=browse_dst).grid(row=1, column=2, padx=5, pady=5)

    log_text = tk.Text(frame, height=10, width=80)
    log_text.grid(row=3, column=0, columnspan=3, padx=5, pady=5)

    def move_pkgs():
        src = src_entry.get().strip()
        dst = dst_entry.get().strip()
        if not os.path.isdir(src) or not os.path.isdir(dst):
            messagebox.showerror("Error", "Please select valid source and destination folders.")
            return
        moved = 0
        for root, dirs, files in os.walk(src):
            for fname in files:
                if fname.lower().endswith(".pkg"):
                    src_path = os.path.join(root, fname)
                    dst_path = os.path.join(dst, fname)
                    try:
                        shutil.move(src_path, dst_path)
                        log_text.insert(tk.END, f"Moved: {src_path} -> {dst_path}\n")
                        moved += 1
                    except Exception as e:
                        log_text.insert(tk.END, f"Failed to move {src_path}: {e}\n")
        if moved == 0:
            log_text.insert(tk.END, "No PKG files found to move.\n")
        else:
            log_text.insert(tk.END, f"Moved {moved} PKG files.\n")

    tk.Button(frame, text="Move PKG Files", command=move_pkgs).grid(row=2, column=1, pady=10)

    return frame