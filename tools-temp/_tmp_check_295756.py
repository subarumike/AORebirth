import os
import struct
import subprocess

path = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat"
size = os.path.getsize(path)
print("items.dat size", size)
with open(path, "rb") as f:
    data = f.read()
for mid in (295756, 248377, 87810):
    be = struct.pack(">I", mid)
    le = struct.pack("<I", mid)
    print(mid, "BE", data.count(be), "LE", data.count(le))

sql = "SELECT id,name FROM cellao_codex_clean.itemnames WHERE id IN (295756,248377,87810);"
p = subprocess.run(
    [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "-N", "-B", "-e", sql],
    capture_output=True,
    text=True,
)
print("mysql out:", p.stdout.strip())
print("mysql err:", p.stderr.strip())

sql2 = (
    "SELECT COUNT(*) FROM information_schema.tables "
    "WHERE table_schema='cellao_codex_clean' AND table_name LIKE '%char%inv%';"
)
p2 = subprocess.run(
    [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "-N", "-B", "-e", sql2],
    capture_output=True,
    text=True,
)
print("inv tables count:", p2.stdout.strip())
