import sys
import ctypes
import subprocess

def run_as_admin(command):
    """Runs a command as administrator, prompting for elevation if necessary."""
    try:
        if ctypes.windll.shell32.IsUserAnAdmin():
            # Already running as admin, execute command directly
            subprocess.run(command, shell=True, check=True)
        else:
            # Not admin, request elevation
            ctypes.windll.shell32.ShellExecuteW(
                None, "runas", sys.executable, ' '.join(sys.argv), None, 1
            )
    except subprocess.CalledProcessError as e:
        print(f"Command execution failed with error: {e}")
    except Exception as e:
        print(f"An error occurred: {e}")

if __name__ == "__main__":
    # Example usage: running ipconfig as admin
    command_to_run = "ipconfig /all"
    run_as_admin(command_to_run)