from pathlib import Path
p = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt")
lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
for line in lines[-60:]:
    print(line)
