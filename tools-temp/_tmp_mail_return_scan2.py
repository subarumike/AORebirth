import os
root = r'tools-temp\AOSharpLiveCapture\bin\Debug\captures'
hits = 0
for dirpath, _, files in os.walk(root):
    for f in files:
        if f not in ('events.log', 'raw-packets.csv', 'npc-interactions.log', 'system-messages.log'):
            continue
        p = os.path.join(dirpath, f)
        try:
            with open(p, 'r', encoding='utf-8', errors='ignore') as fh:
                for i, line in enumerate(fh, 1):
                    if 'ReturnToSender' in line or 'Action=Return' in line or 'ReturnMail' in line:
                        print('%s:%d:%s' % (p, i, line[:240].rstrip()))
                        hits += 1
                    elif 'type=Mail' in line and 'OUT' in line:
                        print('%s:%d:%s' % (p, i, line[:240].rstrip()))
                        hits += 1
                    if hits >= 50:
                        raise SystemExit
print('hits', hits)
