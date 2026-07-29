@echo off
REM Find Mail N3 type and action 7 in recent captures
set CAP=C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures
python -c "import os,re; root=r'%CAP%'; hits=0
for dirpath,_,files in os.walk(root):
  for f in files:
    if f not in ('events.log','raw-packets.csv','npc-interactions.log'): continue
    p=os.path.join(dirpath,f)
    try:
      text=open(p,'r',encoding='utf-8',errors='ignore').read()
    except Exception:
      continue
    if 'ReturnToSender' in text or 'Action=Return' in text or 'action=7' in text.lower() or 'Mail action' in text:
      for i,line in enumerate(text.splitlines(),1):
        if 'Return' in line or 'action=7' in line.lower() or ( 'type=Mail' in line and 'OUT' in line):
          print(p+':'+str(i)+':'+line[:220]); hits+=1
          if hits>40: raise SystemExit
print('hits',hits)"
