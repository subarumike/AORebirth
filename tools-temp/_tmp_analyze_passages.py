import re, subprocess

def query(sql):
    proc = subprocess.run(
        [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "cellao_codex_test", "-N", "-B", "-e", sql],
        capture_output=True, text=True, check=True)
    return [line.split("\t") for line in proc.stdout.splitlines() if line.strip()]

def decode_template(hexstats):
    for m in re.findall(r"CE00(0[0-9A-F]{5})", hexstats.upper()):
        val = int(m, 16)
        if val >= 200000:
            return val
    return None

rows = query(
    "SELECT s.Playfield, HEX(s.stats), n.name FROM staticdynels s "
    "JOIN itemnames n ON n.id = ("
    "SELECT CAST(CONV(SUBSTRING(HEX(s.stats), LOCATE('CE00', HEX(s.stats))+4, 10), 16, 10) AS UNSIGNED)"
    ") WHERE s.Playfield BETWEEN 4676 AND 4699"
)
# simpler: load passages from export script approach
rows = query(
    "SELECT Playfield, HEX(stats) FROM staticdynels WHERE Playfield BETWEEN 4676 AND 4699"
)
from collections import defaultdict
by_name = defaultdict(set)
for pf, hexstats in rows:
    t = decode_template(hexstats)
    if not t: continue
    names = query(f"SELECT name FROM itemnames WHERE id={t}")
    if not names: continue
    name = names[0][0]
    if name.startswith("Passage to"):
        by_name[name].add(int(pf))

dups = {k:v for k,v in by_name.items() if len(v)>1}
print("duplicate passage names:", len(dups))
for k,v in sorted(dups.items()):
    print(k, "-> gardens", sorted(v))

print("\nzone statues:")
rows = query("SELECT Playfield, X, Y, Z, HEX(stats) FROM staticdynels WHERE Playfield IN (4310,4311,4312,4313,4540,4541,4542,4543,4544,4880,4881,4872,4873,4320,4321,4322,4605,4328)")
for pf,x,y,z,h in rows:
    t = decode_template(h)
    if t and t >= 220000:
        n = query(f"SELECT name FROM itemnames WHERE id={t}")
        nm = n[0][0] if n else "?"
        print(f"PF {pf}: template {t} ({nm}) at {x},{y},{z}")
