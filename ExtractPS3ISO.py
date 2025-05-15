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
config_file_path = os.path.join(appdata_folder, "ISO-E-config.json")
failed_conversions_file_path = os.path.join(appdata_folder, "failed_Extractions.json")
log_file_path = os.path.join(appdata_folder, "Extracted_ISOs.log")
batch_file_path = os.path.join(appdata_folder, "delete_Extracted_ISO.bat")