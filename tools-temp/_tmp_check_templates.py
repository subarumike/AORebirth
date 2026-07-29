import re, subprocess

def q(sql):
    p=subprocess.run([r'C:\xampp\mysql\bin\mysql.exe','-u','root','-N','-B','-e',sql],capture_output=True,text=True,check=True)
    return [l.split('\t') for l in p.stdout.splitlines() if l.strip()]

def decode(h):
    for m in re.findall(r'CE00(0[0-9A-F]{5})', h.upper()):
        v=int(m,16)
        if v>=200000: return v
    return None

# sample templates from clean garden 4677
rows=q('SELECT HEX(stats) FROM cellao_codex_clean.staticdynels WHERE Playfield=4677')
ids=[]
for (h,) in rows:
    t=decode(h)
    if t: ids.append(t)
print('4677 templates', ids)
# check itemnames
for t in ids:
    n=q(f'SELECT name FROM cellao_codex_clean.itemnames WHERE id={t}')
    print(t, n[0][0] if n else 'NO NAME')
