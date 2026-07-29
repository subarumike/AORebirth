import os
root = r'tools-temp/AOSharpLiveCapture/bin/Debug/captures'
count = 0
for dirpath, _, files in os.walk(root):
    for f in files:
        if f not in ('events.log', 'raw-packets.csv', 'npc-interactions.log'):
            continue
        path = os.path.join(dirpath, f)
        with open(path, 'r', encoding='utf-8', errors='ignore') as fh:
            for i, line in enumerate(fh, 1):
                low = line.lower()
                if 'type=mail' in low or 'mailaction' in low or 'returntosender' in low:
                    if 'out' in low or 'Return' in line or 'action=7' in low or 'Action=' in line:
                        print(path + ':' + str(i) + ':' + line[:260].rstrip())
                        count += 1
                        if count >= 60:
                            raise SystemExit
print('done count=' + str(count))
