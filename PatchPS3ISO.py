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

# Paths for AppData storage
appdata_folder = os.path.join(os.getenv("LOCALAPPDATA"), "PS3Utils")
config_file_path = os.path.join(appdata_folder, "ISO-patched-config.json")
failed_conversions_file_path = os.path.join(appdata_folder, "failed_to_Patch.json")
log_file_path = os.path.join(appdata_folder, "patched_ISOs.log")