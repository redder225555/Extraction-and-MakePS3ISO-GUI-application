import tkinter as tk
from tkinter import filedialog, messagebox
from tkinter import ttk
import os
import threading
import ctypes

# -------------------------
# Global Variables and Configuration
# -------------------------
abort_flag = False
current_proc = None
patched_isos = []  # List to store successfully patched ISOs
failed_isos = set()  # Set to store ISOs that failed patching

# Paths for AppData storage
appdata_folder = os.path.join(os.getenv("LOCALAPPDATA"), "PS3Utils")
config_file_path = os.path.join(appdata_folder, "ISO-patched-config.json")
failed_conversions_file_path = os.path.join(appdata_folder, "failed_to_Patch.json")
log_file_path = os.path.join(appdata_folder, "patched_ISOs.log")
batch_file_path = os.path.join(appdata_folder, "delete_Patched_ISO.bat")

# Default path values
default_patchps3iso_path = ""
default_version = ""

def ensure_appdata_folder():
    if not os.path.exists(appdata_folder):
        os.makedirs(appdata_folder, exist_ok=True)

def open_appdata_folder():
    if os.path.exists(appdata_folder):
        os.startfile(appdata_folder)
    else:
        messagebox.showerror("Error", "AppData folder does not exist.")

def load_config():
    global default_patchps3iso_path, default_version
    if os.path.exists(config_file_path):
        import json
        with open(config_file_path, "r") as config_file:
            config = json.load(config_file)
            default_patchps3iso_path = config.get("patchps3iso_path", "")
            default_version = config.get("version", "")

def save_config(exe_entry, version_entry):
    import json
    config = {
        "patchps3iso_path": exe_entry.get().strip(),
        "version": version_entry.get().strip(),
    }
    with open(config_file_path, "w") as config_file:
        json.dump(config, config_file)

def load_failed_patches():
    global failed_isos
    if os.path.exists(failed_conversions_file_path):
        import json
        with open(failed_conversions_file_path, "r") as failed_file:
            failed_isos = set(json.load(failed_file))

def save_failed_patches():
    import json
    with open(failed_conversions_file_path, "w") as failed_file:
        json.dump(list(failed_isos), failed_file)

def save_completed_log(patched_isos, log_text):
    with open(log_file_path, "w") as log_file:
        for iso in patched_isos:
            log_file.write(iso + "\n")
    log_text.insert(tk.END, f"Completed log saved to: {log_file_path}\n")

def create_batch_file(patched_isos, log_text):
    with open(batch_file_path, "w") as batch_file:
        for iso in patched_isos:
            batch_file.write(f'del "{iso}"\n')
    log_text.insert(tk.END, f"Batch file created at: {batch_file_path}\n")

def select_exe(exe_entry, version_entry):
    filename = filedialog.askopenfilename(
        title="Select patchps3iso DLL",
        filetypes=[("DLL Files", "*.dll")]
    )
    if filename:
        exe_entry.delete(0, tk.END)
        exe_entry.insert(0, filename)
        save_config(exe_entry, version_entry)

def set_version(version_entry, exe_entry):
    # Save config when version is changed
    save_config(exe_entry, version_entry)

def add_iso(iso_queue_listbox, failed_isos):
    filename = filedialog.askopenfilename(
        title="Select PS3 ISO File",
        filetypes=[("PS3 ISO Files", "*.iso")]
    )
    if filename and filename not in failed_isos:
        iso_queue_listbox.insert(tk.END, filename)

def scan_base_folder(iso_queue_listbox, failed_isos):
    base_folder = filedialog.askdirectory(title="Select Base Folder to Scan for ISOs")
    if not base_folder:
        return
    matched_isos = set()
    for root_path, dirs, files in os.walk(base_folder):
        for f in files:
            if f.lower().endswith(".iso"):
                iso_path = os.path.join(root_path, f)
                if iso_path not in failed_isos:
                    matched_isos.add(iso_path)
    if matched_isos:
        for iso in matched_isos:
            iso_queue_listbox.insert(tk.END, iso)
    else:
        messagebox.showinfo("Scan Complete", "No PS3 ISO files found.")

def remove_selected(iso_queue_listbox):
    selection = iso_queue_listbox.curselection()
    if selection:
        for i in reversed(selection):
            iso_queue_listbox.delete(i)

def run_patch_external(iso_path, dll_entry, version_entry, log_text, status_var, unattended_var):
    status_var.set(f"Patching: {iso_path}")
    log_text.insert(tk.END, f"Patching ISO: {iso_path}\n")

    dll_path = dll_entry.get().strip()
    if not dll_path:
        log_text.insert(tk.END, "patchps3iso DLL path not set.\n")
        return False
    version = version_entry.get().strip()
    if not version:
        log_text.insert(tk.END, "Version not set.\n")
        return False

    try:
        dll = ctypes.CDLL(dll_path)
        if unattended_var.get():
            argc = 4
            argv = (ctypes.c_char_p * 5)()
            argv[0] = dll_path.encode("utf-8")
            argv[1] = iso_path.encode("utf-8")
            argv[2] = version.encode("utf-8")
            argv[3] = b"-h"
        else:
            argc = 3
            argv = (ctypes.c_char_p * 4)()
            argv[0] = dll_path.encode("utf-8")
            argv[1] = iso_path.encode("utf-8")
            argv[2] = version.encode("utf-8")
        result = dll.main(argc, argv)
        if result == 0:
            log_text.insert(tk.END, f"Success: {iso_path}\n")
            return True
        else:
            log_text.insert(tk.END, f"Failed: {iso_path} (DLL returned {result})\n")
            return False
    except Exception as e:
        log_text.insert(tk.END, f"Exception: {e}\n")
        return False

def start_patch_thread(iso_queue_listbox, dll_entry, version_entry, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button, unattended_var):
    threading.Thread(target=process_isos, args=(iso_queue_listbox, dll_entry, version_entry, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button, unattended_var), daemon=True).start()

def abort_patch(status_var):
    global abort_flag
    abort_flag = True
    status_var.set("Abort requested!")

def execute_batch_file(log_text):
    if os.path.exists(batch_file_path):
        os.startfile(batch_file_path)
    else:
        log_text.insert(tk.END, "Batch file does not exist.\n")

def process_isos(iso_queue_listbox, dll_entry, version_entry, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button, unattended_var):
    isos = iso_queue_listbox.get(0, tk.END)
    if not isos:
        log_text.insert(tk.END, "No ISOs in queue.\n")
        return

    global abort_flag, patched_isos, failed_isos
    abort_flag = False
    patched_isos = []

    add_button.config(state=tk.DISABLED)
    scan_button.config(state=tk.DISABLED)
    remove_button.config(state=tk.DISABLED)
    start_button.config(state=tk.DISABLED)
    abort_button.config(state=tk.NORMAL)

    for iso in isos:
        if abort_flag:
            log_text.insert(tk.END, "Aborted by user.\n")
            break
        success = run_patch_external(iso, dll_entry, version_entry, log_text, status_var, unattended_var)
        if success:
            patched_isos.append(iso)
        else:
            failed_isos.add(iso)

    iso_queue_listbox.delete(0, tk.END)
    if not abort_flag:
        log_text.insert(tk.END, "All patching complete.\n")
    else:
        log_text.insert(tk.END, "Patching aborted.\n")

    save_failed_patches()

    add_button.config(state=tk.NORMAL)
    scan_button.config(state=tk.NORMAL)
    remove_button.config(state=tk.NORMAL)
    start_button.config(state=tk.NORMAL)
    abort_button.config(state=tk.DISABLED)

def create_frame(parent):
    ensure_appdata_folder()
    load_config()
    load_failed_patches()

    frame = ttk.Frame(parent)
    # Settings Frame
    settings_frame = tk.Frame(frame)
    settings_frame.pack(pady=10)

    tk.Label(settings_frame, text="patchps3iso DLL:").grid(row=0, column=0, sticky="e")
    dll_entry = tk.Entry(settings_frame, width=60)
    dll_entry.grid(row=0, column=1, padx=5)
    dll_entry.insert(0, default_patchps3iso_path)
    dll_browse = tk.Button(settings_frame, text="Browse", command=lambda: select_exe(dll_entry, version_entry))
    dll_browse.grid(row=0, column=2, padx=5)

    tk.Label(settings_frame, text="Version:").grid(row=1, column=0, sticky="e")
    version_entry = tk.Entry(settings_frame, width=20)
    version_entry.grid(row=1, column=1, sticky="w", padx=5)
    version_entry.insert(0, default_version)
    version_entry.bind("<FocusOut>", lambda e: set_version(version_entry, dll_entry))

    # Unattended checkbox
    unattended_var = tk.BooleanVar()
    unattended_checkbox = tk.Checkbutton(settings_frame, text="Unattended (-h)", variable=unattended_var)
    unattended_checkbox.grid(row=2, column=1, sticky="w", padx=5)

    # ISO Queue Frame
    queue_frame = tk.Frame(frame)
    queue_frame.pack(pady=10)

    tk.Label(queue_frame, text="ISO Queue:").grid(row=0, column=0, columnspan=5, sticky="w")
    iso_queue_listbox = tk.Listbox(queue_frame, width=100, height=10)
    iso_queue_listbox.grid(row=1, column=0, columnspan=5, padx=10, pady=5)

    add_button = tk.Button(queue_frame, text="Add ISO", command=lambda: add_iso(iso_queue_listbox, failed_isos))
    add_button.grid(row=2, column=0, padx=5, pady=5)
    scan_button = tk.Button(queue_frame, text="Scan Base Folder", command=lambda: scan_base_folder(iso_queue_listbox, failed_isos))
    scan_button.grid(row=2, column=1, padx=5, pady=5)
    remove_button = tk.Button(queue_frame, text="Remove Selected", command=lambda: remove_selected(iso_queue_listbox))
    remove_button.grid(row=2, column=2, padx=5, pady=5)

    # Log Frame
    log_frame = tk.Frame(frame)
    log_frame.pack(pady=10)
    log_text = tk.Text(log_frame, width=100, height=10)
    log_text.pack()

    # Status
    status_var = tk.StringVar()
    status_label = tk.Label(frame, textvariable=status_var)
    status_label.pack()

    # Control Buttons
    control_frame = tk.Frame(frame)
    control_frame.pack(pady=10)
    start_button = tk.Button(control_frame, text="Start Patching", command=lambda: start_patch_thread(iso_queue_listbox, dll_entry, version_entry, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button, unattended_var))
    start_button.grid(row=0, column=0, padx=5)
    abort_button = tk.Button(control_frame, text="Abort", command=lambda: abort_patch(status_var), state=tk.DISABLED)
    abort_button.grid(row=0, column=1, padx=5)
    save_log_button = tk.Button(control_frame, text="Save Completed Log", command=lambda: save_completed_log(patched_isos, log_text))
    save_log_button.grid(row=0, column=2, padx=5)
    batch_file_button = tk.Button(control_frame, text="Create Batch File", command=lambda: create_batch_file(patched_isos, log_text))
    batch_file_button.grid(row=0, column=3, padx=5)
    execute_batch_button = tk.Button(control_frame, text="Execute Batch File", command=lambda: execute_batch_file(log_text))
    execute_batch_button.grid(row=0, column=4, padx=5)
    open_appdata_button = tk.Button(control_frame, text="Open AppData Folder", command=open_appdata_folder)
    open_appdata_button.grid(row=0, column=5, padx=5)

    return frame