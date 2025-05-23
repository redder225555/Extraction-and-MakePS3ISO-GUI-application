import tkinter as tk
from tkinter import filedialog, messagebox
from tkinter import ttk
import os
import threading
import ctypes

abort_flag = False
current_proc = None
split_isos = []

appdata_folder = os.path.join(os.getenv("LOCALAPPDATA"), "PS3Utils")
config_file_path = os.path.join(appdata_folder, "SplitISO_config.json")
log_file_path = os.path.join(appdata_folder, "Split_ISOs.log")
batch_file_path = os.path.join(appdata_folder, "delete_Main_ISO.bat")
DLLS_PATH = os.path.join(os.environ.get("LOCALAPPDATA", ""), "PS3Utils", "DLLs")

default_splitps3iso_path = os.path.join(DLLS_PATH, "splitps3isox64.dll")

def ensure_appdata_folder():
    if not os.path.exists(appdata_folder):
        os.makedirs(appdata_folder, exist_ok=True)

def open_appdata_folder():
    if os.path.exists(appdata_folder):
        os.startfile(appdata_folder)
    else:
        messagebox.showerror("Error", "AppData folder does not exist.")

def load_config():
    global default_splitps3iso_path
    if os.path.exists(config_file_path):
        import json
        with open(config_file_path, "r") as config_file:
            config = json.load(config_file)
            default_splitps3iso_path = config.get("splitps3iso_path", "")

def save_config(output_entry):
    import json
    config = {
        "splitps3iso_path": default_splitps3iso_path,
        "output_folder": output_entry.get().strip(),
    }
    with open(config_file_path, "w") as config_file:
        json.dump(config, config_file)

def save_completed_log(split_isos, log_text):
    with open(log_file_path, "w") as log_file:
        for iso in split_isos:
            log_file.write(iso + "\n")
    log_text.insert(tk.END, f"Completed log saved to: {log_file_path}\n")

def create_batch_file(split_isos, log_text):
    with open(batch_file_path, "w") as batch_file:
        for iso in split_isos:
            batch_file.write(f'del "{iso}"\n')
    log_text.insert(tk.END, f"Batch file created at: {batch_file_path}\n")

def select_output_folder(output_entry):
    folder = filedialog.askdirectory(title="Select Output Folder for Split Files")
    if folder:
        output_entry.delete(0, tk.END)
        output_entry.insert(0, folder)
        save_config(output_entry)

def add_iso(iso_queue_listbox, _):
    filename = filedialog.askopenfilename(
        title="Select PS3 ISO File",
        filetypes=[("PS3 ISO Files", "*.iso")]
    )
    if filename:
        if os.path.getsize(filename) > 4 * 1024 * 1024 * 1024:
            iso_queue_listbox.insert(tk.END, filename)
        else:
            messagebox.showinfo("File Too Small", "Selected ISO is not larger than 4GB (FAT32 limit).")

def scan_base_folder(iso_queue_listbox, _):
    base_folder = filedialog.askdirectory(title="Select Base Folder to Scan for ISOs")
    if not base_folder:
        return
    matched_isos = set()
    for root_path, dirs, files in os.walk(base_folder):
        for f in files:
            if f.lower().endswith(".iso"):
                iso_path = os.path.join(root_path, f)
                if os.path.getsize(iso_path) > 4 * 1024 * 1024 * 1024:
                    matched_isos.add(iso_path)
    if matched_isos:
        for iso in matched_isos:
            iso_queue_listbox.insert(tk.END, iso)
    else:
        messagebox.showinfo("Scan Complete", "No PS3 ISO files larger than 4GB found.")

def remove_selected(iso_queue_listbox):
    selection = iso_queue_listbox.curselection()
    if selection:
        for i in reversed(selection):
            iso_queue_listbox.delete(i)

class SplitProgress(ctypes.Structure):
    _fields_ = [
        ("processed_mb", ctypes.c_uint64),
        ("total_mb", ctypes.c_uint64),
        ("percent", ctypes.c_double),
        ("eta_hours", ctypes.c_int),
        ("eta_mins", ctypes.c_int),
        ("eta_secs", ctypes.c_int),
        ("part_number", ctypes.c_int),
        ("part_filename", ctypes.c_char * 0x420),
        ("current_speed", ctypes.c_double), # MB/s
    ]

def run_split_external(iso_path, output_folder, log_text, status_var, unattended_var, progress_bar=None, percent_label=None, eta_label=None, speed_label=None):
    status_var.set(f"Splitting: {iso_path}")
    log_text.insert(tk.END, f"Splitting ISO: {iso_path}\n")
    
    # Get ISO file size in MB
    file_size_bytes = os.path.getsize(iso_path)
    file_size_mb = file_size_bytes / (1024 * 1024)
    log_text.insert(tk.END, f"Total size: {file_size_mb:.2f} MB\n")

    dll_path = default_splitps3iso_path
    if not dll_path:
        log_text.insert(tk.END, "splitps3iso DLL path not set.\n")
        return False

    try:
        dll = ctypes.CDLL(dll_path)
        dll.get_split_progress.restype = ctypes.POINTER(SplitProgress)
        # Always use -gdata for polling, and always put - options at the end
        args = [dll_path.encode("utf-8"), iso_path.encode("utf-8")]
        if output_folder.get().strip():
            args.append(output_folder.get().strip().encode("utf-8"))
        if unattended_var.get():
            args.append(b"-h")
        args.append(b"-gdata")
        argc = len(args)
        argv = (ctypes.c_char_p * (argc + 1))()
        for i, arg in enumerate(args):
            argv[i] = arg
        progress_ptr = dll.get_split_progress()
        def poll_progress():
            while True:
                progress = progress_ptr.contents
                percent = progress.percent
                processed_mb = progress.processed_mb
                total_mb = progress.total_mb
                eta = f"ETA: {progress.eta_hours:02}:{progress.eta_mins:02}:{progress.eta_secs:02}"
                mbps = progress.current_speed
                if progress_bar:
                    progress_bar['value'] = percent
                if percent_label:
                    percent_label.config(text=f"{percent:.2f}% ({processed_mb}/{total_mb} MB)")
                if eta_label:
                    eta_label.config(text=eta)
                if speed_label:
                    speed_label.config(text=f"Speed: {mbps:.2f} MB/s")
                if percent >= 100.0:
                    break
                progress_bar.update_idletasks()
                percent_label.update_idletasks()
                eta_label.update_idletasks()
                speed_label.update_idletasks()
                import time
                time.sleep(0.2)
        import threading
        poll_thread = None
        if progress_bar and percent_label and eta_label and speed_label:
            poll_thread = threading.Thread(target=poll_progress, daemon=True)
            poll_thread.start()
        result = dll.splitps3iso_entry(argc, argv)
        if poll_thread:
            poll_thread.join()
        if result == 0:
            log_text.insert(tk.END, f"Success: {iso_path}\n")
            return True
        else:
            log_text.insert(tk.END, f"Failed: {iso_path} (DLL returned {result})\n")
            return False
    except Exception as e:
        log_text.insert(tk.END, f"Exception: {e}\n")
        return False

def start_split_thread(iso_queue_listbox, output_entry, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button, unattended_var, progress_bar, percent_label, eta_label, speed_label):
    threading.Thread(target=process_isos, args=(iso_queue_listbox, output_entry, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button, unattended_var, progress_bar, percent_label, eta_label, speed_label), daemon=True).start()

def abort_split(status_var):
    global abort_flag
    abort_flag = True
    status_var.set("Abort requested!")

def execute_batch_file(log_text):
    if os.path.exists(batch_file_path):
        os.startfile(batch_file_path)
    else:
        log_text.insert(tk.END, "Batch file does not exist.\n")

def process_isos(iso_queue_listbox, output_entry, log_text, status_var, add_button, scan_button, remove_button, start_button, abort_button, unattended_var, progress_bar, percent_label, eta_label, speed_label):
    isos = iso_queue_listbox.get(0, tk.END)
    if not isos:
        log_text.insert(tk.END, "No ISOs in queue.\n")
        return

    global abort_flag, split_isos
    abort_flag = False
    split_isos = []

    add_button.config(state=tk.DISABLED)
    scan_button.config(state=tk.DISABLED)
    remove_button.config(state=tk.DISABLED)
    start_button.config(state=tk.DISABLED)
    abort_button.config(state=tk.NORMAL)

    for iso in isos:
        if abort_flag:
            log_text.insert(tk.END, "Aborted by user.\n")
            break
        success = run_split_external(iso, output_entry, log_text, status_var, unattended_var, progress_bar, percent_label, eta_label, speed_label)
        if success:
            split_isos.append(iso)

    iso_queue_listbox.delete(0, tk.END)
    if not abort_flag:
        log_text.insert(tk.END, "All splits complete.\n")
    else:
        log_text.insert(tk.END, "Splitting aborted.\n")

    add_button.config(state=tk.NORMAL)
    scan_button.config(state=tk.NORMAL)
    remove_button.config(state=tk.NORMAL)
    start_button.config(state=tk.NORMAL)
    abort_button.config(state=tk.DISABLED)

def create_frame(parent, dll_path):
    ensure_appdata_folder()
    load_config()

    global default_splitps3iso_path
    default_splitps3iso_path = dll_path

    frame = ttk.Frame(parent)
    settings_frame = tk.Frame(frame)
    settings_frame.pack(pady=10)

    tk.Label(settings_frame, text="Output Folder:").grid(row=0, column=0, sticky="e")
    output_entry = tk.Entry(settings_frame, width=60)
    output_entry.grid(row=0, column=1, padx=5)
    output_browse = tk.Button(settings_frame, text="Browse", command=lambda: select_output_folder(output_entry))
    output_browse.grid(row=0, column=2, padx=5)

    unattended_var = tk.BooleanVar()
    unattended_checkbox = tk.Checkbutton(settings_frame, text="Unattended (-h)", variable=unattended_var)
    unattended_checkbox.grid(row=1, column=1, sticky="w", padx=5)

    queue_frame = tk.Frame(frame)
    queue_frame.pack(pady=10)

    tk.Label(queue_frame, text="ISO Queue:").grid(row=0, column=0, columnspan=5, sticky="w")
    iso_queue_listbox = tk.Listbox(queue_frame, width=100, height=10)
    iso_queue_listbox.grid(row=1, column=0, columnspan=5, padx=10, pady=5)

    add_button = tk.Button(queue_frame, text="Add ISO", command=lambda: add_iso(iso_queue_listbox, None))
    add_button.grid(row=2, column=0, padx=5, pady=5)
    scan_button = tk.Button(queue_frame, text="Scan Base Folder", command=lambda: scan_base_folder(iso_queue_listbox, None))
    scan_button.grid(row=2, column=1, padx=5, pady=5)
    remove_button = tk.Button(queue_frame, text="Remove Selected", command=lambda: remove_selected(iso_queue_listbox))
    remove_button.grid(row=2, column=2, padx=5, pady=5)

    log_frame = tk.Frame(frame)
    log_frame.pack(pady=10)
    log_text = tk.Text(log_frame, width=100, height=10)
    log_text.pack()

    # Progress bar and labels
    progress_frame = tk.Frame(frame)
    progress_frame.pack(pady=10)
    progress_bar = ttk.Progressbar(progress_frame, orient="horizontal", length=600, mode="determinate", maximum=100)
    progress_bar.pack()
    percent_label = tk.Label(progress_frame, text="0.00% (0/0 MB)")
    percent_label.pack()
    eta_label = tk.Label(progress_frame, text="ETA: 00:00:00")
    eta_label.pack()
    speed_label = tk.Label(progress_frame, text="Speed: 0.00 MB/s")
    speed_label.pack()

    status_var = tk.StringVar()
    status_label = tk.Label(frame, textvariable=status_var)
    status_label.pack()

    control_frame = tk.Frame(frame)
    control_frame.pack(pady=10)
    abort_button = tk.Button(control_frame, text="Abort", command=lambda: abort_split(status_var), state=tk.DISABLED)
    start_button = tk.Button(
        control_frame,
        text="Start Splitting",
        command=lambda: start_split_thread(
            iso_queue_listbox, output_entry, log_text, status_var,
            add_button, scan_button, remove_button, start_button, abort_button, unattended_var,
            progress_bar, percent_label, eta_label, speed_label
        )
    )
    start_button.grid(row=0, column=0, padx=5)
    abort_button.grid(row=0, column=1, padx=5)
    save_log_button = tk.Button(control_frame, text="Save Completed Log", command=lambda: save_completed_log(split_isos, log_text))
    save_log_button.grid(row=0, column=2, padx=5)
    batch_file_button = tk.Button(control_frame, text="Create Batch File", command=lambda: create_batch_file(split_isos, log_text))
    batch_file_button.grid(row=0, column=3, padx=5)
    execute_batch_button = tk.Button(control_frame, text="Execute Batch File", command=lambda: execute_batch_file(log_text))
    execute_batch_button.grid(row=0, column=4, padx=5)
    open_appdata_button = tk.Button(control_frame, text="Open AppData Folder", command=open_appdata_folder)
    open_appdata_button.grid(row=0, column=5, padx=5)

    return frame