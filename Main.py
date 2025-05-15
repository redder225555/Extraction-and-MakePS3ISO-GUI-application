import tkinter as tk
from tkinter import ttk, messagebox
import ctypes
import sys
import os

def show_license_and_requirements():
    # Find license.txt (works for PyInstaller and normal run)
    if getattr(sys, 'frozen', False):
        base_path = sys._MEIPASS
    else:
        base_path = os.path.dirname(os.path.abspath(__file__))
    license_path = os.path.join(base_path, "license.txt")
    with open(license_path, "r", encoding="utf-8") as f:
        license_text = f.read()

    # License dialog
    license_win = tk.Tk()
    license_win.title("License Agreement")
    license_win.geometry("600x400")
    license_win.resizable(False, False)

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
        license_win.destroy()
        show_requirements()

    accept_btn = tk.Button(license_win, text="Accept", command=on_accept)
    accept_btn.pack(pady=(0, 10))

    license_win.protocol("WM_DELETE_WINDOW", sys.exit)
    license_win.mainloop()

def show_requirements():
    req_win = tk.Tk()
    req_win.title("Requirements")
    req_win.geometry("600x320")
    req_win.resizable(False, False)

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

    # Hyperlink-style labels
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
    req_win.mainloop()

# Check if the script is running with administrator privileges
def is_admin():
    """Check if the script is running with administrator privileges."""
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except:
        return False

if not is_admin():
    # Restart the script with elevated privileges
    script_path = sys.executable if getattr(sys, 'frozen', False) else os.path.abspath(__file__)
    ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable, f'"{script_path}"', None, 1)
    sys.exit()

# Show license and requirements before launching main app
show_license_and_requirements()

# Create the main application window
root = tk.Tk()
root.title("Archive Batch Extractor And PS3 ISO Maker")
root.geometry("900x750")

# Create a notebook (tabbed interface)
notebook = ttk.Notebook(root)

# Import the ExtractionSoftware and MakePS3ISO modules
import ExtractionSoftware
import MakePS3ISO
import ExtractPS3ISO
import SplitPS3ISO
import PatchPS3ISO
# Create frames for each tab
extraction_frame = ExtractionSoftware.create_frame(notebook)
makeps3iso_frame = MakePS3ISO.create_frame(notebook)
ExtractPS3ISO_frame = PS3Extract.create_frame(notebook)
SplitPS3ISO_frame = SplitPS3ISO.create_frame(notebook)
PatchPS3ISO_fram = PatchPS3ISO.create_frame(notebook)
# Add tabs to the notebook
notebook.add(extraction_frame, text="Extraction Software GUI")
notebook.add(makeps3iso_frame, text="Make PS3 ISO GUI")
notebook.add(PS3Extract_frame, text="Extract PS3 ISO GUI")
notebook.add(SplitPS3ISO_frame, text="Split PS3 ISO GUI")
noteboot.add(PatchPS3ISO_fram, text="Patch PS3 ISO GUI")
# Pack the notebook into the main window
notebook.pack(fill=tk.BOTH, expand=True)

# Start the main event loop
root.mainloop()