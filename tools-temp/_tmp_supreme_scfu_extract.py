from pathlib import Path
import re

caps = [
    "20260720-204431",
    "20260722-cap-mob-drop-cred",
    "20260720-080123",
    "20260722-212421",
]
root = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")

for cap_id in caps:
    ev = root / cap_id / "events.log"
    if not ev.exists():
        print("MISSING", cap_id)
        continue
    print("====", cap_id)
    for line in ev.read_text(encoding="utf-8", errors="ignore").splitlines():
        if "IN-N3-DETAIL" in line and "SimpleCharFullUpdate" in line and "Supreme Collector of Waste" in line:
            # print key fields
            for key in [
                "Name=",
                "MonsterData=",
                "MonsterScale=",
                "Flags=",
                "ScfuUnk1=",
                "Textures=",
                "Meshes=",
                "TextureOverrides=",
                "ExtendedTextures=",
                "VisualFlags=",
                "Level=",
                "Health=",
                "Position=",
            ]:
                m = re.search(key + r"[^,}\]]+", line)
                if m:
                    print(" ", m.group(0)[:300])
            # also dump any Material
            for m in re.finditer(r"Material #[0-9]+", line):
                print(" ", m.group(0))
            print("  LINE_LEN", len(line))
            print("---")
