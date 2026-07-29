import re, subprocess

def query(sql):
    p=subprocess.run([r"C:\xampp\mysql\bin\mysql.exe","-u","root","cellao_codex_test","-N","-B","-e",sql],capture_output=True,text=True,check=True)
    return [l.split('\t') for l in p.stdout.splitlines() if l.strip()]

def decode(h):
    for m in re.findall(r'CE00(0[0-9A-F]{5})', h.upper()):
        v=int(m,16)
        if v>=200000: return v
    return None

for pf in [4696,4697,4692,4693,4694,4695]:
    rows=query(f'SELECT HEX(stats) FROM staticdynels WHERE Playfield={pf}')
    print('PF',pf,'count',len(rows))
    for h in rows[:5]:
        t=decode(h)
        if t:
            n=query(f'SELECT name FROM itemnames WHERE id={t}')
            print(' ',t,n[0][0] if n else '?')
