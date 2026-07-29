from pathlib import Path
events = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\events.log").read_text(encoding="utf-8", errors="replace")
replay = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\mission-flow.replay.log").read_text(encoding="utf-8", errors="replace")

out = []
# CharacterAction 146 around terminal
for i, line in enumerate(events.splitlines()):
    if "Action=146" in line and "57AC323C" in line:
        start = max(0, i-5)
        end = min(len(events.splitlines()), i+40)
        lines = events.splitlines()
        out.append("\n".join(lines[start:end]))
        out.append("====")
        break

# ContainerAddItem Terminal
for i, line in enumerate(events.splitlines()):
    if "ContainerAddItem" in line and "57AC323C" in line:
        lines = events.splitlines()
        start = max(0, i-15)
        end = min(len(lines), i+25)
        out.append("\n".join(lines[start:end]))
        out.append("====CAI====")
        break

# GenericCmd Get/Use on terminal near pickup time 08:10
for i, line in enumerate(events.splitlines()):
    if "08:10:1" in line and ("GenericCmd" in line or "PickUp" in line or "Action=146" in line or "ContainerAddItem" in line):
        out.append(line[:350])

Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_pickup_flow.txt").write_text("\n".join(out), encoding="utf-8")
print("done", len(out))

# replay container add
for line in replay.splitlines():
    if "57AC323C" in line and ("CONTAINER" in line or "INTERACTION" in line or "ACTION" in line):
        if "CONTAINER" in line or "PickUp" in line or "Action=146" in line:
            print(line[:400])
