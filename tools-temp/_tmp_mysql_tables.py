import subprocess
sql = "SHOW TABLES"
proc = subprocess.run([r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "cellao_codex_test", "-e", sql], capture_output=True, text=True)
print(proc.stdout)
print(proc.stderr)
