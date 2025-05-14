import tkinter as tk
from tkinter import filedialog, messagebox
from tkinter.ttk import Progressbar
import subprocess
import os
import time
import threading
import json
from pathlib import Path
from tkinter import ttk

APPDATA_DIR = Path(os.getenv("LOCALAPPDATA")) / "ExtractionSoftware"
LOG_FILE = APPDATA_DIR / "log.txt"
CONFIG_FILE_JSON = APPDATA_DIR / "config.json"
LAYOUT_FILE = APPDATA_DIR / "layout.json"
APPDATA_DIR.mkdir(parents=True, exist_ok=True)

DEFAULT_LAYOUT = {
    "folder_queue_listbox": {"x": 6, "y": 2, "width": 884, "height": 314},
    "add_folder_btn": {"x": 7, "y": 324, "width": 120, "height": 30},
    "clear_queue_btn": {"x": 770, "y": 322, "width": 120, "height": 30},
    "extraction_tool_label": {"x": -9, "y": 384, "width": 120, "height": 30},
    "radio_7z": {"x": 101, "y": 372, "width": 80, "height": 30},
    "radio_winrar": {"x": 108, "y": 405, "width": 80, "height": 30},
    "seven_zip_label": {"x": 182, "y": 373, "width": 120, "height": 30},
    "seven_zip_entry": {"x": 300, "y": 373, "width": 300, "height": 30},
    "seven_zip_browse": {"x": 604, "y": 374, "width": 120, "height": 30},
    "winrar_label": {"x": 180, "y": 404, "width": 120, "height": 30},
    "winrar_entry": {"x": 301, "y": 405, "width": 300, "height": 30},
    "winrar_browse": {"x": 604, "y": 404, "width": 120, "height": 30},
    "password_checkbox": {"x": 399, "y": 497, "width": 120, "height": 30},
    "password_entry": {"x": 355, "y": 528, "width": 200, "height": 30},
    "password_visibility_checkbox": {"x": 556, "y": 527, "width": 120, "height": 30},
    "replace_checkbox": {"x": 210, "y": 496, "width": 200, "height": 30},
    "delete_checkbox": {"x": -18, "y": 495, "width": 250, "height": 30},
    "extract_here_checkbox": {"x": -16, "y": 522, "width": 150, "height": 30},
    "file_count_label": {"x": 98, "y": 442, "width": 200, "height": 30},
    "progress_bar": {"x": 298, "y": 446, "width": 300, "height": 20},
    "current_file_progress_bar": {"x": 298, "y": 477, "width": 300, "height": 20},
    "eta_label": {"x": 98, "y": 471, "width": 200, "height": 30},
    "extract_btn": {"x": 127, "y": 322, "width": 120, "height": 40},
    "abort_btn": {"x": 647, "y": 321, "width": 120, "height": 40},
    "headless_winrar_checkbox": {"x": 524, "y": 497, "width": 200, "height": 30}
}

class DragManager:
    def __init__(self, parent):
        self.parent = parent
        self.locked = True
        self.widgets = []
        self.widget_ids = {}
        self.selected = None
        self.layout_file = LAYOUT_FILE
        self._add_menu()
        self._bind_keys()

    def _add_menu(self):
        self.menu = tk.Menu(self.parent, tearoff=0)
        self.menu.add_command(label="Unlock Layout", command=self.unlock)
        self.menu.add_command(label="Lock Layout", command=self.lock)
        self.parent.bind("<Button-3>", self._show_menu)

    def _show_menu(self, event):
        self.menu.tk_popup(event.x_root, event.y_root)

    def unlock(self):
        self.locked = False
        for widget in self.widgets:
            self._make_draggable(widget)
            self._disable_widget(widget)
        self._deselect()
        self.load_layout()

    def lock(self):
        self.locked = True
        for widget in self.widgets:
            self._remove_draggable(widget)
            self._enable_widget(widget)
        self._deselect()
        self.save_layout()

    def add_widget(self, widget, widget_id):
        self.widgets.append(widget)
        self.widget_ids[widget] = widget_id
        if not self.locked:
            self._make_draggable(widget)
            self._disable_widget(widget)

    def _make_draggable(self, widget):
        widget.bind("<ButtonPress-1>", self._on_start)
        widget.bind("<B1-Motion>", self._on_drag)
        widget.bind("<ButtonRelease-1>", self._on_release)

    def _remove_draggable(self, widget):
        widget.unbind("<ButtonPress-1>")
        widget.unbind("<B1-Motion>")
        widget.unbind("<ButtonRelease-1>")

    def _disable_widget(self, widget):
        try:
            widget.config(state="disabled")
        except Exception:
            pass

    def _enable_widget(self, widget):
        try:
            widget.config(state="normal")
        except Exception:
            pass

    def _on_start(self, event):
        widget = event.widget
        self._select(widget)
        widget._drag_start_x = event.x
        widget._drag_start_y = event.y

    def _on_drag(self, event):
        widget = event.widget
        try:
            x = widget.winfo_x() + event.x - widget._drag_start_x
            y = widget.winfo_y() + event.y - widget._drag_start_y
            widget.place(x=x, y=y)
            self._select(widget)
            # If this is the folder queue, move the handle too
            if hasattr(widget, "_resize_handle"):
                widget._resize_handle.place(x=x + widget.winfo_width() - 10, y=y + widget.winfo_height() - 10)
        except Exception:
            pass

    def _on_release(self, event):
        self.save_layout()

    def _select(self, widget):
        if self.selected and self.selected != widget:
            try:
                self.selected.config(highlightthickness=0)
            except Exception:
                pass
        self.selected = widget
        try:
            widget.config(highlightbackground="blue", highlightcolor="blue", highlightthickness=2)
        except Exception:
            pass

    def _deselect(self):
        if self.selected:
            try:
                self.selected.config(highlightthickness=0)
            except Exception:
                pass
        self.selected = None

    def _bind_keys(self):
        self.parent.bind_all("<Key>", self._on_key)

    def _on_key(self, event):
        if self.locked or not self.selected:
            return
        widget = self.selected
        x = widget.winfo_x()
        y = widget.winfo_y()
        width = widget.winfo_width()
        height = widget.winfo_height()
        move_delta = 2 if not event.state & 0x1 else 10  # Shift for bigger steps

        # Move with arrow keys
        if event.keysym == "Left":
            widget.place(x=x - move_delta, y=y)
        elif event.keysym == "Right":
            widget.place(x=x + move_delta, y=y)
        elif event.keysym == "Up":
            widget.place(x=x, y=y - move_delta)
        elif event.keysym == "Down":
            widget.place(x=x, y=y + move_delta)
        # Resize with Shift+Arrow
        if event.state & 0x1:  # Shift pressed
            if event.keysym == "Right":
                widget.place(width=width + move_delta)
            elif event.keysym == "Left":
                widget.place(width=max(10, width - move_delta))
            elif event.keysym == "Down":
                widget.place(height=height + move_delta)
            elif event.keysym == "Up":
                widget.place(height=max(10, height - move_delta))
            # If this is the folder queue, move the handle too
            if hasattr(widget, "_resize_handle"):
                info = widget.place_info()
                widget._resize_handle.place(
                    x=int(float(info["x"])) + int(float(info["width"])) - 10,
                    y=int(float(info["y"])) + int(float(info["height"])) - 10
                )
        self.save_layout()

    def save_layout(self):
        layout = {}
        for widget in self.widgets:
            info = widget.place_info()
            widget_id = self.widget_ids[widget]
            layout[widget_id] = {
                "x": int(float(info.get("x", 0))),
                "y": int(float(info.get("y", 0))),
                "width": int(float(info.get("width", widget.winfo_width()))),
                "height": int(float(info.get("height", widget.winfo_height())))
            }
        with open(self.layout_file, "w") as f:
            json.dump(layout, f, indent=2)

    def load_layout(self):
        layout = None
        if self.layout_file.exists():
            try:
                with open(self.layout_file, "r") as f:
                    layout = json.load(f)
            except Exception:
                layout = None
        if layout is None:
            layout = DEFAULT_LAYOUT
        for widget in self.widgets:
            widget_id = self.widget_ids[widget]
            if widget_id in layout:
                info = layout[widget_id]
                widget.place(x=info["x"], y=info["y"], width=info["width"], height=info["height"])
                # If this is the folder queue, move the handle too
                if hasattr(widget, "_resize_handle"):
                    widget._resize_handle.place(
                        x=info["x"] + info["width"] - 10,
                        y=info["y"] + info["height"] - 10
                    )

def create_frame(parent):
    frame = ttk.Frame(parent)
    dragman = DragManager(frame)

    extraction_tool_var = tk.StringVar(value="7z")
    password_checkbox_var = tk.BooleanVar()
    password_visibility_var = tk.BooleanVar()
    replace_checkbox_var = tk.BooleanVar()
    delete_checkbox_var = tk.BooleanVar()
    extract_here_var = tk.BooleanVar(value=True)
    headless_winrar_var = tk.BooleanVar(value=True)
    progress_var = tk.IntVar()
    current_file_progress_var = tk.IntVar()

    def save_config():
        config_data = {
            "7z_path": seven_zip_entry.get(),
            "winrar_path": winrar_entry.get(),
            "use_password": password_checkbox_var.get(),
            "password": password_entry.get(),
            "replace_files": replace_checkbox_var.get(),
            "delete_after": delete_checkbox_var.get(),
            "extract_here": extract_here_var.get(),
            "headless_winrar": headless_winrar_var.get()
        }
        with open(CONFIG_FILE_JSON, "w") as config_file:
            json.dump(config_data, config_file, indent=4)

    def load_config():
        if CONFIG_FILE_JSON.exists():
            with open(CONFIG_FILE_JSON, "r") as config_file:
                return json.load(config_file)
        return {
            "7z_path": "",
            "winrar_path": "",
            "use_password": False,
            "password": "",
            "replace_files": False,
            "delete_after": False,
            "extract_here": True,
            "headless_winrar": True
        }

    def write_log(message):
        with open(LOG_FILE, "a") as log_file:
            log_file.write(f"{time.strftime('%Y-%m-%d %H:%M:%S')} - {message}\n")

    def select_7z_folder():
        folder = filedialog.askdirectory(title="Select 7-Zip Folder")
        if folder:
            seven_zip_path = os.path.join(folder, "7z.exe")
            seven_zip_entry.delete(0, tk.END)
            seven_zip_entry.insert(0, seven_zip_path)
            save_config()
            write_log(f"7-Zip path saved: {seven_zip_path}")

    def select_winrar_folder():
        folder = filedialog.askdirectory(title="Select WinRAR Folder")
        if folder:
            winrar_path = os.path.join(folder, "WinRAR.exe")
            winrar_entry.delete(0, tk.END)
            winrar_entry.insert(0, winrar_path)
            save_config()
            write_log(f"WinRAR path saved: {winrar_path}")

    def select_folders():
        folder = filedialog.askdirectory(title="Select Folder")
        if folder:
            folder_queue_listbox.insert(tk.END, folder)

    def update_progress(progress, eta_text):
        progress_var.set(progress)
        eta_label.config(text=eta_text)
        progress_bar.update()

    def update_current_file_progress(progress):
        current_file_progress_var.set(progress)
        current_file_progress_bar.update()

    def update_files_completed(completed, total):
        file_count_label.config(text=f"Files Completed: {completed}/{total}")

    def abort_extraction_function():
        nonlocal abort_extraction
        abort_extraction = True

    def format_eta(seconds):
        hours, remainder = divmod(int(seconds), 3600)
        minutes, seconds = divmod(remainder, 60)
        return f"{hours:02}:{minutes:02}:{seconds:02}"

    def toggle_password_visibility():
        if password_entry.cget("show") == "*":
            password_entry.config(show="")
        else:
            password_entry.config(show="*")

    abort_extraction = False
    def extract_archives():
        nonlocal abort_extraction
        abort_extraction = False

        extraction_tool = extraction_tool_var.get()
        exe_path = seven_zip_entry.get() if extraction_tool == "7z" else winrar_entry.get()
        if not exe_path or not os.path.exists(exe_path):
            messagebox.showerror("Error", f"Please select a valid {extraction_tool.upper()} executable.")
            return

        use_password   = password_checkbox_var.get()
        replace_files  = replace_checkbox_var.get()
        delete_after   = delete_checkbox_var.get()
        extract_here   = extract_here_var.get()
        password       = password_entry.get()

        folders = folder_queue_listbox.get(0, tk.END)
        if not folders:
            messagebox.showinfo("No Folders", "No folders selected for extraction.")
            return

        allowed_extensions = {".rar", ".zip", ".7z"}
        tasks = []
        for folder in folders:
            try:
                with os.scandir(folder) as entries:
                    for entry in entries:
                        if entry.is_file():
                            ext = os.path.splitext(entry.name)[1].lower()
                            if ext in allowed_extensions:
                                tasks.append((folder, entry.name))
            except Exception as e:
                print(f"Error scanning folder {folder}: {e}")

        total_files = len(tasks)
        if total_files == 0:
            messagebox.showinfo("No Files", "No archive files found in selected folders.")
            return

        progress_var.set(0)
        progress_bar["maximum"] = total_files
        frame.after(0, update_files_completed, 0, total_files)
        start_time = time.time()
        extracted_count = 0

        for folder, archive in tasks:
            if abort_extraction:
                messagebox.showinfo("Aborted", "Extraction process has been aborted.")
                return

            archive_path = os.path.join(folder, archive)
            dest_folder = folder if extract_here else os.path.join(folder, os.path.splitext(archive)[0])
            if not os.path.exists(dest_folder):
                os.makedirs(dest_folder, exist_ok=True)

            if extraction_tool == "7z":
                cmd = f'"{exe_path}" e "{archive_path}" -o"{dest_folder}" -bsp1'
                if use_password:
                    cmd += f' -p"{password}"'
                if replace_files:
                    cmd += " -y"
            else:
                cmd = f'"{exe_path}" x "{archive_path}" "{dest_folder}\\\"'
                if use_password:
                    cmd += f' -p"{password}"'
                if replace_files:
                    cmd += " -o+"
                if headless_winrar_var.get():
                    cmd += " -ibck -inul"
            print(f"Executing command: {cmd}")

            try:
                startupinfo = subprocess.STARTUPINFO()
                startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = subprocess.SW_HIDE
                with subprocess.Popen(
                    cmd,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                    shell=True,
                    universal_newlines=True,
                    startupinfo=startupinfo
                ) as proc:
                    for line in proc.stdout:
                        if "%" in line:
                            try:
                                percentage = int(line.strip().split('%')[0][-3:].strip())
                                frame.after(0, update_current_file_progress, percentage)
                            except (IndexError, ValueError):
                                pass
                    proc.wait()
                    err_output = proc.stderr.read().strip()
                    if err_output:
                        print(f"Error output from extraction tool: {err_output}")
            except Exception as e:
                messagebox.showerror("Error", f"Extraction failed for {archive}: {e}")
                return

            if delete_after:
                try:
                    os.remove(archive_path)
                except Exception as e:
                    print(f"Error deleting archive {archive_path}: {e}")

            extracted_count += 1
            frame.after(0, update_files_completed, extracted_count, total_files)
            progress_var.set(extracted_count)

            elapsed_time = time.time() - start_time
            avg_time = elapsed_time / extracted_count
            remaining = avg_time * (total_files - extracted_count)
            eta_text = f"ETA: {format_eta(remaining)}"
            frame.after(0, update_progress, extracted_count, eta_text)

        messagebox.showinfo("Done", "Extraction complete!")

    # --- Folder Queue Listbox with Resize Handle ---
    folder_queue_listbox = tk.Listbox(frame)
    folder_queue_listbox.place(x=6, y=2, width=884, height=314)
    dragman.add_widget(folder_queue_listbox, "folder_queue_listbox")

    # Add a resize handle (small label) at the bottom-right of the Listbox
    resize_handle = tk.Label(frame, cursor="bottom_right_corner", bg="#cccccc")
    def place_resize_handle():
        info = folder_queue_listbox.place_info()
        x = int(float(info["x"])) + int(float(info["width"])) - 10
        y = int(float(info["y"])) + int(float(info["height"])) - 10
        resize_handle.place(x=x, y=y, width=10, height=10)
    place_resize_handle()

    def start_resize(event):
        resize_handle._start_x = event.x
        resize_handle._start_y = event.y
        resize_handle._orig_width = folder_queue_listbox.winfo_width()
        resize_handle._orig_height = folder_queue_listbox.winfo_height()

    def do_resize(event):
        dx = event.x - resize_handle._start_x
        dy = event.y - resize_handle._start_y
        new_width = max(50, resize_handle._orig_width + dx)
        new_height = max(30, resize_handle._orig_height + dy)
        folder_queue_listbox.place(width=new_width, height=new_height)
        place_resize_handle()
        dragman.save_layout()

    resize_handle.bind("<Button-1>", start_resize)
    resize_handle.bind("<B1-Motion>", do_resize)

    # Attach handle to Listbox for DragManager to move it together
    folder_queue_listbox._resize_handle = resize_handle

    # --- Rest of your widgets ---
    add_folder_btn = tk.Button(frame, text="Add Folder", command=select_folders)
    add_folder_btn.place(x=7, y=324, width=120, height=30)
    dragman.add_widget(add_folder_btn, "add_folder_btn")

    clear_queue_btn = tk.Button(frame, text="Clear Queue", command=lambda: folder_queue_listbox.delete(0, tk.END))
    clear_queue_btn.place(x=770, y=322, width=120, height=30)
    dragman.add_widget(clear_queue_btn, "clear_queue_btn")

    extraction_tool_label = tk.Label(frame, text="Extraction Tool:")
    extraction_tool_label.place(x=-9, y=384, width=120, height=30)
    dragman.add_widget(extraction_tool_label, "extraction_tool_label")

    radio_7z = tk.Radiobutton(frame, text="7-Zip", variable=extraction_tool_var, value="7z")
    radio_7z.place(x=101, y=372, width=80, height=30)
    dragman.add_widget(radio_7z, "radio_7z")
    radio_winrar = tk.Radiobutton(frame, text="WinRAR", variable=extraction_tool_var, value="winrar")
    radio_winrar.place(x=108, y=405, width=80, height=30)
    dragman.add_widget(radio_winrar, "radio_winrar")

    seven_zip_label = tk.Label(frame, text="7-Zip Location:")
    seven_zip_label.place(x=182, y=373, width=120, height=30)
    dragman.add_widget(seven_zip_label, "seven_zip_label")
    seven_zip_entry = tk.Entry(frame, width=40)
    seven_zip_entry.place(x=300, y=373, width=300, height=30)
    dragman.add_widget(seven_zip_entry, "seven_zip_entry")
    seven_zip_browse = tk.Button(frame, text="Browse", command=select_7z_folder)
    seven_zip_browse.place(x=604, y=374, width=120, height=30)
    dragman.add_widget(seven_zip_browse, "seven_zip_browse")

    winrar_label = tk.Label(frame, text="WinRAR Location:")
    winrar_label.place(x=180, y=404, width=120, height=30)
    dragman.add_widget(winrar_label, "winrar_label")
    winrar_entry = tk.Entry(frame, width=40)
    winrar_entry.place(x=301, y=405, width=300, height=30)
    dragman.add_widget(winrar_entry, "winrar_entry")
    winrar_browse = tk.Button(frame, text="Browse", command=select_winrar_folder)
    winrar_browse.place(x=604, y=404, width=120, height=30)
    dragman.add_widget(winrar_browse, "winrar_browse")

    password_checkbox = tk.Checkbutton(frame, text="Use Password:", variable=password_checkbox_var, command=save_config)
    password_checkbox.place(x=399, y=497, width=120, height=30)
    dragman.add_widget(password_checkbox, "password_checkbox")
    password_entry = tk.Entry(frame, width=20, show="*")
    password_entry.place(x=355, y=526, width=200, height=30)
    dragman.add_widget(password_entry, "password_entry")
    password_entry.bind("<KeyRelease>", lambda event: save_config())
    password_visibility_checkbox = tk.Checkbutton(frame, text="Show Password", variable=password_visibility_var, command=toggle_password_visibility)
    password_visibility_checkbox.place(x=556, y=527, width=120, height=30)
    dragman.add_widget(password_visibility_checkbox, "password_visibility_checkbox")

    replace_checkbox = tk.Checkbutton(frame, text="Replace Existing Files", variable=replace_checkbox_var, command=save_config)
    replace_checkbox.place(x=210, y=496, width=200, height=30)
    dragman.add_widget(replace_checkbox, "replace_checkbox")
    delete_checkbox = tk.Checkbutton(frame, text="Delete Archive After Extraction", variable=delete_checkbox_var, command=save_config)
    delete_checkbox.place(x=-18, y=495, width=250, height=30)
    dragman.add_widget(delete_checkbox, "delete_checkbox")
    extract_here_checkbox = tk.Checkbutton(frame, text="Extract Here", variable=extract_here_var, command=save_config)
    extract_here_checkbox.place(x=-16, y=522, width=150, height=30)
    dragman.add_widget(extract_here_checkbox, "extract_here_checkbox")

    # Headless WinRAR Extraction Checkbox
    headless_winrar_checkbox = tk.Checkbutton(
        frame, text="Headless WinRAR Extraction", variable=headless_winrar_var, command=save_config
    )
    headless_winrar_checkbox.place(x=750, y=404, width=200, height=30)
    dragman.add_widget(headless_winrar_checkbox, "headless_winrar_checkbox")

    file_count_label = tk.Label(frame, text="Files Completed: 0/0")
    file_count_label.place(x=98, y=442, width=200, height=30)
    dragman.add_widget(file_count_label, "file_count_label")
    progress_bar = Progressbar(frame, variable=progress_var, length=300)
    progress_bar.place(x=298, y=446, width=300, height=20)
    dragman.add_widget(progress_bar, "progress_bar")
    current_file_progress_bar = Progressbar(frame, variable=current_file_progress_var, length=300)
    current_file_progress_bar.place(x=298, y=477, width=300, height=20)
    dragman.add_widget(current_file_progress_bar, "current_file_progress_bar")
    eta_label = tk.Label(frame, text="ETA: Calculating...")
    eta_label.place(x=98, y=471, width=200, height=30)
    dragman.add_widget(eta_label, "eta_label")

    extract_btn = tk.Button(frame, text="Extract", command=lambda: threading.Thread(target=extract_archives).start())
    extract_btn.place(x=127, y=322, width=120, height=40)
    dragman.add_widget(extract_btn, "extract_btn")
    abort_btn = tk.Button(frame, text="Abort", command=abort_extraction_function)
    abort_btn.place(x=647, y=321, width=120, height=40)
    dragman.add_widget(abort_btn, "abort_btn")

    config = load_config()
    seven_zip_entry.insert(0, config.get("7z_path", ""))
    winrar_entry.insert(0, config.get("winrar_path", ""))
    password_checkbox_var.set(config.get("use_password", False))
    password_entry.insert(0, config.get("password", ""))
    replace_checkbox_var.set(config.get("replace_files", False))
    delete_checkbox_var.set(config.get("delete_after", False))
    extract_here_var.set(config.get("extract_here", True))
    headless_winrar_var.set(config.get("headless_winrar", True))

    dragman.load_layout()
    return frame