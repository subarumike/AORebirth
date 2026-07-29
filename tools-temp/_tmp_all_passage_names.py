import re, subprocess
from collections import defaultdict

def query(sql):
    p=subprocess.run([r"C:\xampp\mysql\bin\mysql.exe","-u","root","cellao_codex_test","-N","-B","-e",sql],capture_output=True,text=True,check=True)
    return [l.split('\t') for l in p.stdout.splitlines() if l.strip()]

def decode(h):
    if isinstance(h, list): h=h[0]
    for m in re.findall(r'CE00(0[0-9A-F]{5})', h.upper()):
        v=int(m,16)
        if v>=200000: return v
    return None

rows=query('SELECT Playfield, HEX(stats) FROM staticdynels WHERE Playfield BETWEEN 4676 AND 4699')
names=set()
for pf,h in rows:
    t=decode(h)
    if not t: continue
    n=query(f'SELECT name FROM itemnames WHERE id={t}')
    if n and n[0][0].startswith('Passage to'):
        names.add(n[0][0])
print('unique passage names in gardens:', len(names))
for n in sorted(names):
    print(n)
