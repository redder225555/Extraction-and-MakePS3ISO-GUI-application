import os
import subprocess

def is_admin():
    return os.geteuid() == 0

def run_as_admin(command):
    subprocess.run(['sudo'] + command)

if __name__ == "__main__":
    if is_admin():
        # Code to execute with admin privileges
        print("Running with admin privileges.")
        # Replace this with your actual code that requires admin access
    else:
        print("Re-launching with admin privileges...")
        run_as_admin([__file__] + os.sys.argv[1:])