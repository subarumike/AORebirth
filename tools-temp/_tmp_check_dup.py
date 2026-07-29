import re, subprocess
from collections import defaultdict

def query(sql):
    p = subprocess.run([r"C:\xampp\mysql\bin\mysql.exe","-u","root","cellao_codex_test","-N","-B","-e",sql],capture_output=True,text=True,check=True)
    return [l.split('\t') for l in p.stdout.splitlines() if l.strip()]

def decode(hexstats):
    for m in re.findall(r'CE00(0[0-9A-F]{5})', hexstats.upper()):
        v=int(m,16)
        if v>=200000: return v
    return None

rows=query('SELECT Playfield, HEX(stats) FROM staticdynels WHERE Playfield BETWEEN 4676 AND 4699')
by_name=defaultdict(set)
for pf,h in rows:
    t=decode(h)
    if not t: continue
    n=query(f'SELECT name FROM itemnames WHERE id={t}')
    if n and n[0][0].startswith('Passage to'):
        by_name[n[0][0]].add(int(pf))
for k,v in sorted(by_name.items()):
    if 'Inferno' in k or 'Pandemon' in k or k in ('Passage to Path to fire','Passage to Path to Fire','Passage to Misty Marshes','Passage to Dark Hill',"Passage to Razor's Lair"):
        print(k, sorted(v))

print('proxy 4322', query('SELECT * FROM proxydestinations WHERE Playfield=4322'))
