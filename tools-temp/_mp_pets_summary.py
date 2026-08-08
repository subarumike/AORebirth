import re
t=open(r"tools-temp\_mp_pets_extract.txt",encoding="utf-8").read().splitlines()
print("=== CASTS ===")
for line in t:
    if "Action=CastNano" in line and "OUT" in line:
        m=re.search(r"Parameter2=(\d+).*?Target=\(([^)]+)\)", line)
        if not m:
            m=re.search(r"Target=\(([^)]+)\).*?Parameter2=(\d+)", line)
            if m:
                print("CAST nano=%s target=%s"%(m.group(2), m.group(1)))
        else:
            print("CAST nano=%s target=%s"%(m.group(1), m.group(2)))
    if "FinishNanoCasting" in line and "DETAIL" in line:
        m=re.search(r"Parameter2=(\d+)", line)
        if m: print("FINISH", m.group(1))
    if 'Name="' in line and "SimpleCharFullUpdate" in line:
        m=re.search(r'Name="([^"]+)".*?Level=(\d+) Health=(\d+).*?MonsterData=(\d+) MonsterScale=(\d+)', line)
        if m: print("PET", m.group(1), "lvl", m.group(2), "hp", m.group(3), "md", m.group(4), "scale", m.group(5))
    if "TemplateAction" in line and "DETAIL" in line:
        print("SHELL", line[line.find("TemplateAction"):line.find("TemplateAction")+180])
