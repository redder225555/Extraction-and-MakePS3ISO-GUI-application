import tkinter as tk
from tkinter import filedialog, messagebox
import subprocess
import os
import threading
import shutil
import time
import sys
import psutil
import ctypes
import json
from tkinter import ttk

# -------------------------
# Global Variables and Configuration
# -------------------------
abort_flag = False
current_proc = None
converted_folders = []  # List to store successfully converted folders
failed_folders = set()  # Set to store folders that failed conversion

# Paths for AppData storage
appdata_folder = os.path.join(os.getenv("LOCALAPPDATA"), "MakePS3ISO")
config_file_path = os.path.join(appdata_folder, "config.json")
failed_conversions_file_path = os.path.join(appdata_folder, "failed_conversions.json")
log_file_path = os.path.join(appdata_folder, "converted_folders.log")
batch_file_path = os.path.join(appdata_folder, "delete_folders.bat")

# Default path values
default_makeps3iso_path = r"J:\PS3 keys\makeps3iso\makeps3iso.exe"
default_output_folder = r"J:\ps3iso"
default_pkg_destination_folder = ""

# -------------------------
# Ensure AppData Folder Exists
# -------------------------
def ensure_appdata_folder():
    if not os.path.exists(appdata_folder):
        os.makedirs(appdata_folder)

# -------------------------
# Configuration Management
# -------------------------
def open_appdata_folder():
    if os.path.exists(appdata_folder):
        subprocess.Popen(f'explorer "{appdata_folder}"')
    else:
        messagebox.showerror("Error", "AppData folder does not exist.")

def load_config():
    global default_makeps3iso_path, default_output_folder, default_pkg_destination_folder
    if os.path.exists(config_file_path):
        with open(config_file_path, "r") as config_file:
            config = json.load(config_file)
            default_makeps3iso_path = config.get("makeps3iso_path", default_makeps3iso_path)
            default_output_folder = config.get("output_folder", default_output_folder)
            default_pkg_destination_folder = config.get("pkg_destination_folder", "")

def save_config(exe_entry, output_entry, pkg_destination_entry):
    config = {
        "makeps3iso_path": exe_entry.get().strip(),
        "output_folder": output_entry.get().strip(),
        "pkg_destination_folder": pkg_destination_entry.get().strip(),
    }
    with open(config_file_path, "w") as config_file:
        json.dump(config, config_file, indent=4)

def load_failed_conversions():
    global failed_folders
    if os.path.exists(failed_conversions_file_path):
        with open(failed_conversions_file_path, "r") as failed_file:
            failed_folders = set(json.load(failed_file))

def save_failed_conversions():
    with open(failed_conversions_file_path, "w") as failed_file:
        json.dump(list(failed_folders), failed_file, indent=4)

def save_completed_log(converted_folders, log_text):
    with open(log_file_path, "w") as log_file:
        for folder in converted_folders:
            log_file.write(f"{folder}\n")
    log_text.insert(tk.END, f"Completed log saved to: {log_file_path}\n")

def create_batch_file(converted_folders, log_text):
    with open(batch_file_path, "w") as batch_file:
        for folder in converted_folders:
            batch_file.write(f'rmdir /S /Q "{folder}"\n')
    log_text.insert(tk.END, f"Batch file created at: {batch_file_path}\n")

def select_exe(exe_entry, output_entry, pkg_destination_entry):
    filename = filedialog.askopenfilename(
        title="Select MakePS3ISO EXE",
        filetypes=[("Executable Files", "*.exe")]
    )
    if filename:
        exe_entry.delete(0, tk.END)
        exe_entry.insert(0, filename)
        save_config(exe_entry, output_entry, pkg_destination_entry)

def select_output_folder(output_entry, exe_entry, pkg_destination_entry):
    folder = filedialog.askdirectory(title="Select Output Folder")
    if folder:
        output_entry.delete(0, tk.END)
        output_entry.insert(0, folder)
        save_config(exe_entry, output_entry, pkg_destination_entry)

def select_folder(entry_field, exe_entry, output_entry, pkg_destination_entry):
    folder = filedialog.askdirectory(title="Select Folder")
    if folder:
        entry_field.delete(0, tk.END)
        entry_field.insert(0, folder)
        save_config(exe_entry, output_entry, pkg_destination_entry)

def run_conversion_external(folder, exe_entry, output_entry, split_var, log_text, status_var):
    target_folder = folder
    status_var.set(f"Processing: {target_folder}")
    log_text.insert(tk.END, f"Processing folder: {target_folder}\n")

    current_makeps3iso = exe_entry.get().strip()
    if not current_makeps3iso:
        messagebox.showerror("Error", "MakePS3ISO executable path is not set.")
        return -1
    current_output_folder = output_entry.get().strip()
    if not current_output_folder:
        messagebox.showerror("Error", "Output folder is not set.")
        return -1

    split_option = "-s" if split_var.get() else ""
    cmd = f'"{current_makeps3iso}" {split_option} "{target_folder}" "{current_output_folder}"'

    log_text.insert(tk.END, f"Executing command: {cmd}\n")
    print(f"Executing command: {cmd}")

    try:
        proc = subprocess.Popen(cmd, shell=True)
        global current_proc
        current_proc = proc
        proc.wait()
        current_proc = None

        if proc.returncode == 0:
            log_text.insert(tk.END, f"Conversion succeeded for: {target_folder}\n")
        else:
            log_text.insert(tk.END, f"Conversion failed for: {target_folder}\n")
        return proc.returncode
    except Exception as e:
        messagebox.showerror("Error", f"Error running conversion command: {e}")
        return -1

def add_folder(folder_queue_listbox, failed_folders):
    folder = filedialog.askdirectory(title="Select Folder for Conversion")
    if folder and folder not in failed_folders:
        folder_queue_listbox.insert(tk.END, folder)

def scan_base_folder(folder_queue_listbox, failed_folders):
    base_folder = filedialog.askdirectory(title="Select Base Folder to Scan")
    if not base_folder:
        return
    matched_folders = set()
    for root_path, dirs, files in os.walk(base_folder):
        for d in dirs:
            if d.upper() == "PS3_GAME":
                parent_folder = os.path.dirname(os.path.join(root_path, d))
                if parent_folder not in failed_folders:
                    matched_folders.add(parent_folder)
                break
    if matched_folders:
        for folder in sorted(matched_folders):
            folder_queue_listbox.insert(tk.END, folder)
        messagebox.showinfo("Scan Complete", f"Found and added {len(matched_folders)} folder(s).")
    else:
        messagebox.showinfo("No Matches", "No folders containing a 'PS3_GAME' subfolder were found.")

def remove_selected(folder_queue_listbox):
    selection = folder_queue_listbox.curselection()
    if selection:
        folder_queue_listbox.delete(selection[0])

def start_conversion_thread(folder_queue_listbox, exe_entry, output_entry, split_var, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button):
    threading.Thread(target=process_folders, args=(folder_queue_listbox, exe_entry, output_entry, split_var, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button), daemon=True).start()

def abort_conversion(status_var):
    global abort_flag
    abort_flag = True
    status_var.set("Abort requested!")

def execute_batch_file(log_text):
    if os.path.exists(batch_file_path):
        try:
            subprocess.Popen(batch_file_path, shell=True)
            log_text.insert(tk.END, f"Executing batch file: {batch_file_path}\n")
        except Exception as e:
            messagebox.showerror("Error", f"Failed to execute batch file: {e}")
    else:
        messagebox.showerror("Error", "Batch file does not exist. Please run a conversion first.")

def process_folders(folder_queue_listbox, exe_entry, output_entry, split_var, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button):
    folders = folder_queue_listbox.get(0, tk.END)
    if not folders:
        messagebox.showinfo("No Folders", "No folders in the queue to process.")
        return

    global abort_flag, converted_folders, failed_folders
    abort_flag = False
    converted_folders = []

    add_button.config(state=tk.DISABLED)
    scan_button.config(state=tk.DISABLED)
    remove_button.config(state=tk.DISABLED)
    start_button.config(state=tk.DISABLED)
    abort_button.config(state=tk.NORMAL)

    for folder in folders:
        if abort_flag:
            status_var.set("Aborted.")
            log_text.insert(tk.END, "Processing aborted by user.\n")
            break

        status_var.set(f"Processing: {folder}")
        log_text.insert(tk.END, f"Processing folder: {folder}\n")
        retcode = run_conversion_external(folder, exe_entry, output_entry, split_var, log_text, status_var)
        if retcode == 0:
            log_text.insert(tk.END, f"Conversion succeeded for: {folder}\n")
            converted_folders.append(folder)
        else:
            log_text.insert(tk.END, f"Conversion failed for: {folder}\n")
            failed_folders.add(folder)

    folder_queue_listbox.delete(0, tk.END)
    if not abort_flag:
        status_var.set("All folders processed.")
        messagebox.showinfo("Conversion Complete", "Processing of queued folders has finished.")
        save_completed_log(converted_folders, log_text)
        create_batch_file(converted_folders, log_text)
    else:
        status_var.set("Processing aborted.")

    save_failed_conversions()

    add_button.config(state=tk.NORMAL)
    scan_button.config(state=tk.NORMAL)
    remove_button.config(state=tk.NORMAL)
    start_button.config(state=tk.NORMAL)
    abort_button.config(state=tk.DISABLED)

def create_frame(parent):
    ensure_appdata_folder()
    load_config()
    load_failed_conversions()

    frame = ttk.Frame(parent)
    # Settings Frame
    settings_frame = tk.Frame(frame)
    settings_frame.pack(pady=10)

    tk.Label(settings_frame, text="MakePS3ISO EXE:").grid(row=0, column=0, sticky="e")
    exe_entry = tk.Entry(settings_frame, width=60)
    exe_entry.grid(row=0, column=1, padx=5)
    exe_entry.insert(0, default_makeps3iso_path)
    exe_browse = tk.Button(settings_frame, text="Browse", command=lambda: select_exe(exe_entry, output_entry, pkg_destination_entry))
    exe_browse.grid(row=0, column=2, padx=5)

    split_var = tk.BooleanVar(value=False)
    split_checkbox = tk.Checkbutton(settings_frame, text="Split ISO into 4GB parts", variable=split_var)
    split_checkbox.grid(row=3, column=1, sticky="w", padx=5)

    tk.Label(settings_frame, text="Output Folder:").grid(row=1, column=0, sticky="e")
    output_entry = tk.Entry(settings_frame, width=60)
    output_entry.grid(row=1, column=1, padx=5)
    output_entry.insert(0, default_output_folder)
    output_browse = tk.Button(settings_frame, text="Browse", command=lambda: select_output_folder(output_entry, exe_entry, pkg_destination_entry))
    output_browse.grid(row=1, column=2, padx=5)

    tk.Label(settings_frame, text="PKG Destination Folder:").grid(row=2, column=0, sticky="e")
    pkg_destination_entry = tk.Entry(settings_frame, width=60)
    pkg_destination_entry.grid(row=2, column=1, padx=5)
    pkg_destination_entry.insert(0, default_pkg_destination_folder)
    pkg_destination_browse = tk.Button(settings_frame, text="Browse", command=lambda: select_folder(pkg_destination_entry, exe_entry, output_entry, pkg_destination_entry))
    pkg_destination_browse.grid(row=2, column=2, padx=5)

    # Folder Queue Frame
    queue_frame = tk.Frame(frame)
    queue_frame.pack(pady=10)

    tk.Label(queue_frame, text="Folder Queue:").grid(row=0, column=0, columnspan=5, sticky="w")
    folder_queue_listbox = tk.Listbox(queue_frame, width=100, height=10)
    folder_queue_listbox.grid(row=1, column=0, columnspan=5, padx=10, pady=5)

    add_button = tk.Button(queue_frame, text="Add Folder", command=lambda: add_folder(folder_queue_listbox, failed_folders), width=15)
    add_button.grid(row=2, column=0, padx=5)
    scan_button = tk.Button(queue_frame, text="Scan Base Folder", command=lambda: scan_base_folder(folder_queue_listbox, failed_folders), width=15)
    scan_button.grid(row=2, column=1, padx=5)
    remove_button = tk.Button(queue_frame, text="Remove Selected", command=lambda: remove_selected(folder_queue_listbox), width=15)
    remove_button.grid(row=2, column=2, padx=5)
    status_var = tk.StringVar()
    start_button = tk.Button(queue_frame, text="Start Conversion", command=lambda: start_conversion_thread(folder_queue_listbox, exe_entry, output_entry, split_var, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button), width=15)
    start_button.grid(row=2, column=3, padx=5)
    abort_button = tk.Button(queue_frame, text="Abort", command=lambda: abort_conversion(status_var), width=15)
    abort_button.grid(row=2, column=4, padx=5)
    abort_button.config(state=tk.DISABLED)

    execute_batch_button = tk.Button(queue_frame, text="Execute Batch File", command=lambda: execute_batch_file(log_text), width=20)
    execute_batch_button.grid(row=3, column=0, padx=5, pady=5)

    open_folder_button = tk.Button(queue_frame, text="Open AppData Folder", command=open_appdata_folder, width=20)
    open_folder_button.grid(row=3, column=1, padx=5, pady=5)

    log_text = tk.Text(frame, wrap=tk.WORD, height=15)
    log_text.pack(fill=tk.BOTH, expand=True)

    status_label = tk.Label(frame, textvariable=status_var, fg="blue")
    status_label.pack(pady=5)

    return frame