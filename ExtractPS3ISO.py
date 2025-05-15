import tkinter as tk
from tkinter import filedialog, messagebox
from tkinter import ttk
import os
import threading
import ctypes
import urllib.request

# -------------------------
# Global Variables and Configuration
# -------------------------
abort_flag = False
current_proc = None
extracted_isos = []  # List to store successfully extracted ISOs
failed_isos = set()  # Set to store ISOs that failed extraction


# Paths for AppData storage
appdata_folder = os.path.join(os.getenv("LOCALAPPDATA"), "PS3Utils")
config_file_path = os.path.join(appdata_folder, "ISO-E-config.json")
failed_conversions_file_path = os.path.join(appdata_folder, "failed_Extractions.json")
log_file_path = os.path.join(appdata_folder, "Extracted_ISOs.log")
batch_file_path = os.path.join(appdata_folder, "delete_Extracted_ISO.bat")
extractps3iso_dll_path = os.path.join(appdata_folder, dll_host_file)
# Default path values
default_extractps3iso_path = ""
default_output_folder = ""

def download_file(url, filename):
        url = "https://raw.githubusercontent.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/blob/main/extractps3iso.dll"
        filename = "extractps3iso.dll"
    
    # Create the downloads directory if it doesn't exist
        os.makedirs(appdata_folder, exist_ok=True)
    
    # Join the directory and filename to create the full path
        filepath = os.path.join(appdata_folder, filename)
    
    # Download the file
        urllib.request.urlretrieve(url, filepath)
        print(f"Downloaded {url} to {filepath}")

def ensure_appdata_folder():
    if not os.path.exists(appdata_folder):
        os.makedirs(appdata_folder, exist_ok=True)

def open_appdata_folder():
    if os.path.exists(appdata_folder):
        os.startfile(appdata_folder)
    else:
        messagebox.showerror("Error", "AppData folder does not exist.")

def load_config():
    global default_extractps3iso_path, default_output_folder
    if os.path.exists(config_file_path):
        import json
        with open(config_file_path, "r") as config_file:
            config = json.load(config_file)
            default_extractps3iso_path = config.get("extractps3iso_path", "")
            default_output_folder = config.get("output_folder", "")

def save_config(exe_entry, output_entry):
    import json
    config = {
        "extractps3iso_path": exe_entry.get().strip(),
        "output_folder": output_entry.get().strip(),
    }
    with open(config_file_path, "w") as config_file:
        json.dump(config, config_file)

def load_failed_extractions():
    global failed_isos
    if os.path.exists(failed_conversions_file_path):
        import json
        with open(failed_conversions_file_path, "r") as failed_file:
            failed_isos = set(json.load(failed_file))

def save_failed_extractions():
    import json
    with open(failed_conversions_file_path, "w") as failed_file:
        json.dump(list(failed_isos), failed_file)

def save_completed_log(extracted_isos, log_text):
    with open(log_file_path, "w") as log_file:
        for iso in extracted_isos:
            log_file.write(iso + "\n")
    log_text.insert(tk.END, f"Completed log saved to: {log_file_path}\n")

def create_batch_file(extracted_isos, log_text):
    with open(batch_file_path, "w") as batch_file:
        for iso in extracted_isos:
            batch_file.write(f'del "{iso}"\n')
    log_text.insert(tk.END, f"Batch file created at: {batch_file_path}\n")

def select_exe(exe_entry, output_entry):
    filename = filedialog.askopenfilename(
        title="Select extractps3iso DLL",
        filetypes=[("DLL Files", "*.dll")]
    )
    if filename:
        exe_entry.delete(0, tk.END)
        exe_entry.insert(0, filename)
        save_config(exe_entry, output_entry)

def select_output_folder(output_entry, exe_entry):
    folder = filedialog.askdirectory(title="Select Output Folder")
    if folder:
        output_entry.delete(0, tk.END)
        output_entry.insert(0, folder)
        save_config(exe_entry, output_entry)

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

def run_extraction_external(iso_path, exe_entry, output_entry, split_var, unattended_var, log_text, status_var):
    status_var.set(f"Extracting: {iso_path}")
    log_text.insert(tk.END, f"Extracting ISO: {iso_path}\n")

    dll_path = exe_entry.get().strip()
    if not dll_path:
        log_text.insert(tk.END, "extractps3iso DLL path not set.\n")
        return False
    current_output_folder = output_entry.get().strip()
    if not current_output_folder:
        log_text.insert(tk.END, "Output folder not set.\n")
        return False

    try:
        dll = ctypes.CDLL(dll_path)
        args = [dll_path.encode("utf-8")]
        if split_var.get():
            args.append(b"-s")
        args.append(iso_path.encode("utf-8"))
        args.append(current_output_folder.encode("utf-8"))
        if unattended_var.get():
            args.append(b"-h")
        argc = len(args)
        argv = (ctypes.c_char_p * (argc + 1))()
        for i, arg in enumerate(args):
            argv[i] = arg
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

def start_extraction_thread(iso_queue_listbox, exe_entry, output_entry, split_var, unattended_var, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button):
    threading.Thread(target=process_isos, args=(iso_queue_listbox, exe_entry, output_entry, split_var, unattended_var, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button), daemon=True).start()

def abort_extraction(status_var):
    global abort_flag
    abort_flag = True
    status_var.set("Abort requested!")

def execute_batch_file(log_text):
    if os.path.exists(batch_file_path):
        os.startfile(batch_file_path)
    else:
        log_text.insert(tk.END, "Batch file does not exist.\n")

def process_isos(iso_queue_listbox, exe_entry, output_entry, split_var, unattended_var, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button):
    isos = iso_queue_listbox.get(0, tk.END)
    if not isos:
        log_text.insert(tk.END, "No ISOs in queue.\n")
        return

    global abort_flag, extracted_isos, failed_isos
    abort_flag = False
    extracted_isos = []

    add_button.config(state=tk.DISABLED)
    scan_button.config(state=tk.DISABLED)
    remove_button.config(state=tk.DISABLED)
    start_button.config(state=tk.DISABLED)
    abort_button.config(state=tk.NORMAL)

    for iso in isos:
        if abort_flag:
            log_text.insert(tk.END, "Aborted by user.\n")
            break
        success = run_extraction_external(iso, exe_entry, output_entry, split_var, unattended_var, log_text, status_var)
        if success:
            extracted_isos.append(iso)
        else:
            failed_isos.add(iso)

    iso_queue_listbox.delete(0, tk.END)
    if not abort_flag:
        log_text.insert(tk.END, "All extractions complete.\n")
    else:
        log_text.insert(tk.END, "Extraction aborted.\n")

    save_failed_extractions()

    add_button.config(state=tk.NORMAL)
    scan_button.config(state=tk.NORMAL)
    remove_button.config(state=tk.NORMAL)
    start_button.config(state=tk.NORMAL)
    abort_button.config(state=tk.DISABLED)

def create_frame(parent):
    ensure_appdata_folder()
    load_config()
    load_failed_extractions()

    frame = ttk.Frame(parent)
    # Settings Frame
    settings_frame = tk.Frame(frame)
    settings_frame.pack(pady=10)

    tk.Label(settings_frame, text="extractps3iso DLL:").grid(row=0, column=0, sticky="e")
    exe_entry = tk.Entry(settings_frame, width=60)
    exe_entry.grid(row=0, column=1, padx=5)
    exe_entry.insert(0, default_extractps3iso_path)
    exe_browse = tk.Button(settings_frame, text="Browse", command=lambda: select_exe(exe_entry, output_entry))
    exe_browse.grid(row=0, column=2, padx=5)

    split_var = tk.BooleanVar(value=False)
    split_checkbox = tk.Checkbutton(settings_frame, text="Split extracted files (-s)", variable=split_var)
    split_checkbox.grid(row=2, column=1, sticky="w", padx=5)

    # Unattended checkbox
    unattended_var = tk.BooleanVar()
    unattended_checkbox = tk.Checkbutton(settings_frame, text="Unattended (-h)", variable=unattended_var)
    unattended_checkbox.grid(row=3, column=1, sticky="w", padx=5)

    tk.Label(settings_frame, text="Output Folder:").grid(row=1, column=0, sticky="e")
    output_entry = tk.Entry(settings_frame, width=60)
    output_entry.grid(row=1, column=1, padx=5)
    output_entry.insert(0, default_output_folder)
    output_browse = tk.Button(settings_frame, text="Browse", command=lambda: select_output_folder(output_entry, exe_entry))
    output_browse.grid(row=1, column=2, padx=5)

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
    start_button = tk.Button(control_frame, text="Start Extraction", command=lambda: start_extraction_thread(iso_queue_listbox, exe_entry, output_entry, split_var, unattended_var, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button))
    start_button.grid(row=0, column=0, padx=5)
    abort_button = tk.Button(control_frame, text="Abort", command=lambda: abort_extraction(status_var), state=tk.DISABLED)
    abort_button.grid(row=0, column=1, padx=5)
    save_log_button = tk.Button(control_frame, text="Save Completed Log", command=lambda: save_completed_log(extracted_isos, log_text))
    save_log_button.grid(row=0, column=2, padx=5)
    batch_file_button = tk.Button(control_frame, text="Create Batch File", command=lambda: create_batch_file(extracted_isos, log_text))
    batch_file_button.grid(row=0, column=3, padx=5)
    execute_batch_button = tk.Button(control_frame, text="Execute Batch File", command=lambda: execute_batch_file(log_text))
    execute_batch_button.grid(row=0, column=4, padx=5)
    open_appdata_button = tk.Button(control_frame, text="Open AppData Folder", command=open_appdata_folder)
    open_appdata_button.grid(row=0, column=5, padx=5)

    return frame