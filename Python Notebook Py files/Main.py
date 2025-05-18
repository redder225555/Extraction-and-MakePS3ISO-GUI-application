import tkinter as tk
from tkinter import ttk, messagebox
import ctypes
import sys
import os
import traceback
import platform
import urllib.request

print(platform.architecture())

APPDATA_PATH = os.path.join(os.environ.get("LOCALAPPDATA", ""), "ps3utils")
DLLS_PATH = os.path.join(APPDATA_PATH, "DLLs")
LICENSE_FLAG = os.path.join(APPDATA_PATH, "license_accepted.txt")

def show_architecture():
    arch = platform.architecture()[0]
    messagebox.showinfo("Architecture Info", f"Running as {arch} Python")

def get_dll_urls():
    arch = platform.architecture()[0]
    if "64" in arch:
        return {
            "extractps3isox64.dll": "https://raw.githubusercontent.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/main/DLL%20Files/extractps3isox64.dll",
            "makeps3isox64.dll": "https://raw.githubusercontent.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/main/DLL%20Files/makeps3isox64.dll",
            "splitps3isox64.dll": "https://raw.githubusercontent.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/main/DLL%20Files/splitps3isox64.dll",
            "patchps3isox64.dll": "https://raw.githubusercontent.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/main/DLL%20Files/patchps3isox64.dll",
        }
    else:
        return {
            "extractps3iso.dll": "https://raw.githubusercontent.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/main/DLL%20Files/extractps3iso.dll",
            "makeps3iso2.dll": "https://raw.githubusercontent.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/main/DLL%20Files/makeps3iso2.dll",
            "splitps3iso.dll": "https://raw.githubusercontent.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/main/DLL%20Files/splitps3iso.dll",
            "patchps3iso.dll": "https://raw.githubusercontent.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/main/DLL%20Files/patchps3iso.dll",
        }

DLL_URLS = get_dll_urls()

# Add this after DLL_URLS = get_dll_urls()
def get_dll_name_map():
    arch = platform.architecture()[0]
    if "64" in arch:
        return {
            "extractps3iso": "extractps3isox64.dll",
            "makeps3iso": "makeps3isox64.dll",
            "splitps3iso": "splitps3isox64.dll",
            "patchps3iso": "patchps3isox64.dll",
        }
    else:
        return {
            "extractps3iso": "extractps3iso.dll",
            "makeps3iso": "makeps3iso2.dll",
            "splitps3iso": "splitps3iso.dll",
            "patchps3iso": "patchps3iso.dll",
        }

DLL_NAME_MAP = get_dll_name_map()

def ensure_appdata_dirs():
    os.makedirs(DLLS_PATH, exist_ok=True)

def download_dlls():
    for dll, url in DLL_URLS.items():
        dest = os.path.join(DLLS_PATH, dll)
        if not os.path.exists(dest):
            try:
                print(f"Downloading {dll}...")
                with urllib.request.urlopen(url) as response, open(dest, "wb") as out_file:
                    out_file.write(response.read())
            except Exception as e:
                messagebox.showerror("DLL Download Error", f"Failed to download {dll}:\n{e}")
                sys.exit(1)

def show_license_and_requirements():
    temp_root = tk.Tk()
    temp_root.withdraw()
    LICENSE_URL = "https://raw.githubusercontent.com/redder225555/Extraction-and-MakePS3ISO-GUI-application/main/license.txt"
    try:
        with urllib.request.urlopen(LICENSE_URL) as response:
            license_text = response.read().decode("utf-8")
    except Exception as e:
        messagebox.showerror("License Error", f"Could not fetch license from online.\n{e}", parent=temp_root)
        temp_root.destroy()
        sys.exit(1)
    license_win = tk.Toplevel(temp_root)
    license_win.title("License Agreement")
    license_win.geometry("600x400")
    license_win.resizable(False, False)
    license_win.grab_set()
    text = tk.Text(license_win, wrap="word", height=15)
    text.insert("1.0", license_text)
    text.config(state="disabled")
    text.pack(fill="both", expand=True, padx=10, pady=10)
    agree_var = tk.BooleanVar()
    agree_chk = tk.Checkbutton(license_win, text="I agree to the license terms", variable=agree_var)
    agree_chk.pack(pady=(0, 10))
    def on_accept():
        if not agree_var.get():
            messagebox.showwarning("Agreement Required", "You must agree to the license terms to continue.", parent=license_win)
            return
        with open(LICENSE_FLAG, "w") as f:
            f.write("accepted")
        license_win.destroy()
        show_requirements(temp_root)
    accept_btn = tk.Button(license_win, text="Accept", command=on_accept)
    accept_btn.pack(pady=(0, 10))
    license_win.protocol("WM_DELETE_WINDOW", sys.exit)
    license_win.wait_window()
    temp_root.destroy()

def show_requirements(parent):
    req_win = tk.Toplevel(parent)
    req_win.title("Requirements")
    req_win.geometry("600x320")
    req_win.resizable(False, False)
    req_win.grab_set()
    req_text = (
        "Requirements:\n"
        "- 7-Zip or WinRAR must be installed and their executable paths provided in the app.\n"
        "- makeps3iso.exe is required for PS3 ISO creation.\n\n"
        "Download links:"
    )
    label = tk.Label(req_win, text=req_text, justify="left", anchor="w")
    label.pack(fill="both", expand=True, padx=10, pady=(10,0))
    def open_url_confirm(url, name):
        if messagebox.askyesno("Open Link", f"Are you sure you want to open the {name} website?\n\n{url}", parent=req_win):
            import webbrowser
            webbrowser.open(url)
    link_frame = tk.Frame(req_win)
    link_frame.pack(pady=(5,0))
    links = [
        ("makeps3iso.exe Release", "https://github.com/bucanero/ps3iso-utils/releases"),
        ("7-Zip Website", "https://www.7-zip.org/"),
        ("WinRAR Website", "https://www.win-rar.com/download.html"),
    ]
    for name, url in links:
        link = tk.Label(link_frame, text=name, fg="blue", cursor="hand2", underline=True)
        link.pack(anchor="w", padx=10)
        link.bind("<Button-1>", lambda e, url=url, name=name: open_url_confirm(url, name))
    def on_continue():
        req_win.destroy()
    tk.Button(req_win, text="Continue", command=on_continue).pack(pady=10)
    req_win.protocol("WM_DELETE_WINDOW", sys.exit)
    req_win.wait_window()

def is_admin():
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except:
        return False

try:
    if not is_admin():
        script_path = sys.executable if getattr(sys, 'frozen', False) else os.path.abspath(__file__)
        ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable, f'"{script_path}"', None, 1)
        sys.exit()
    ensure_appdata_dirs()
    download_dlls()
    if not os.path.exists(LICENSE_FLAG):
        show_license_and_requirements()
    root = tk.Tk()
    root.title("Archive Batch Extractor And PS3 ISO Maker")
    root.geometry("900x750")
    notebook = ttk.Notebook(root)
    import ExtractionSoftware
    import MakePS3ISO
    import ExtractPS3ISO
    import SplitPS3ISO
    import PatchPS3ISO
    import PKGFileMover
    extraction_frame = ExtractionSoftware.create_frame(notebook)
    makeps3iso_frame = MakePS3ISO.create_frame(notebook, os.path.join(DLLS_PATH, DLL_NAME_MAP["makeps3iso"]))
    ExtractPS3ISO_frame = ExtractPS3ISO.create_frame(notebook, os.path.join(DLLS_PATH, DLL_NAME_MAP["extractps3iso"]))
    SplitPS3ISO_frame = SplitPS3ISO.create_frame(notebook, os.path.join(DLLS_PATH, DLL_NAME_MAP["splitps3iso"]))
    PatchPS3ISO_frame = PatchPS3ISO.create_frame(notebook, os.path.join(DLLS_PATH, DLL_NAME_MAP["patchps3iso"]))
    PKGFileMover_frame = PKGFileMover.create_frame(notebook)
    notebook.add(extraction_frame, text="Extraction Software GUI")
    notebook.add(PKGFileMover_frame, text="Move PKG GUI")
    notebook.add(makeps3iso_frame, text="Make PS3 ISO GUI")
    notebook.add(ExtractPS3ISO_frame, text="Extract PS3 ISO GUI")
    notebook.add(SplitPS3ISO_frame, text="Split PS3 ISO GUI")
    notebook.add(PatchPS3ISO_frame, text="Patch PS3 ISO GUI")
    notebook.pack(fill=tk.BOTH, expand=True)
    root.mainloop()
except Exception as e:
    print("Exception occurred in main program:", file=sys.stderr)
    traceback.print_exc()
    try:
        messagebox.showerror("Startup Error", f"Failed to start:\n{e}")
    except Exception:
        pass
    sys.exit(1)