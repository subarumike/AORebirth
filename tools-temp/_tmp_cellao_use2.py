path = r'C:\Users\nermi\source\repos\CellAO-NightPredator-private prije AOrebirth\CellAO\Built\Debug\ZoneEngineLog.txt'
lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
# find Use 14428401 and next 30 lines
for i, l in enumerate(lines):
    if '2026-07-16 11:11:37' in l and '14428401' in l:
        for j in range(i, min(len(lines), i+40)):
            print(lines[j][:400])
        break

print('--- Config DB for CellAO ---')
cfg = r'C:\Users\nermi\source\repos\CellAO-NightPredator-private prije AOrebirth\CellAO\Built\Debug\Config.xml'
import re
text = open(cfg, encoding='utf-8', errors='replace').read()
m = re.search(r'MysqlConnection>([^<]+)', text)
print(m.group(1) if m else 'no mysql')
