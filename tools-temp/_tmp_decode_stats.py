import subprocess, struct

# Decode first few MessagePack tuples from clean DB for pf 4677
p = subprocess.run(
    [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "cellao_codex_clean", "-N", "-e",
     "SELECT HEX(stats) FROM staticdynels WHERE Playfield=4677 AND Instance=14428396;"],
    capture_output=True, text=True)
hexdata = p.stdout.strip()
raw = bytes.fromhex(hexdata)
print('len', len(raw))
print(raw[:200])
# try msgpack if available
try:
    import msgpack
    # may be zlib compressed?
    import zlib
    try:
        dec = zlib.decompress(raw)
        print('zlib ok', len(dec))
        raw = dec
    except Exception as e:
        print('not zlib', e)
    obj = msgpack.unpackb(raw, raw=False, strict_map_key=False)
    print('unpacked', obj)
except Exception as e:
    print('msgpack fail', e)
