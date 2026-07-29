import pathlib
roots = [
    pathlib.Path(r'AORebirth/Built/Debug'),
    pathlib.Path(r'AORebirth/Server/ZoneEngine'),
]
needle = (53032).to_bytes(4, 'little')
needle_be = (53032).to_bytes(4, 'big')
for root in roots:
    if not root.exists():
        continue
    for p in root.rglob('*'):
        if not p.is_file():
            continue
        if p.suffix.lower() not in ('.dat', '.bin', '.db', '.cdb'):
            continue
        if p.stat().st_size > 250_000_000:
            continue
        try:
            d = p.read_bytes()
        except Exception as ex:
            print('skip', p, ex)
            continue
        c = d.count(needle)
        cbe = d.count(needle_be)
        if c or cbe:
            print(f'LE={c} BE={cbe} {p} size={p.stat().st_size}')
print('scan done')
