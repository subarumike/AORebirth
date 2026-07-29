# -*- coding: utf-8 -*-
import json

d = json.load(open(
    r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-loralei/enemy-dossier.json",
    encoding="utf-8-sig"))

seen = set()
rows = []
for e in d.get("enemies", []):
    n = e.get("name") or ""
    if n not in (
        "Lolly the Reet",
        "Desert Reet",
        "Greedy Desert Reet",
        "Rollerrat",
        "Gnarl the Roller",
    ):
        continue
    hx = e["identity"].split(":")[1].rstrip(")").upper()
    if n == "Lolly the Reet" and hx != "7985CAEC":
        continue
    p = e["position"]
    key = (n, round(p["x"], 1), round(p["z"], 1))
    if key in seen:
        continue
    seen.add(key)
    md = int(e.get("monsterData") or (30365 if "Reet" in n else 17687))
    lv = int(e.get("level") or 5)
    hp = int(e.get("maxHealth") or 58)
    if n == "Lolly the Reet":
        sc, nf, cf, rs = 95, 53, 277352961, 17
    elif n == "Greedy Desert Reet":
        sc, nf, cf, rs = 130, 53, 268964353, 22
    elif n == "Gnarl the Roller":
        sc, nf, cf, rs = 200, 55, 268964353, 20
    elif n == "Rollerrat":
        sc, nf, cf, rs = 125, 55, 268964353, 17
    else:
        sc, nf, cf, rs = 93, 53, 268964353, (22 if lv >= 6 else 17)
    rows.append((n, hx, lv, hp, md, sc, nf, cf, rs, p["x"], p["y"], p["z"]))

lines = []
for n, hx, lv, hp, md, sc, nf, cf, rs, x, y, z in rows:
    lines.append("            new AreteNpc")
    lines.append("            {")
    lines.append("                // Capture 20260721-loralei %s" % hx)
    lines.append("                CaptureInstance = unchecked((int)0x%s)," % hx)
    lines.append('                Name = "%s",' % n)
    lines.append(
        "                Level = %d, Health = %d, MonsterData = %d, Scale = %d, "
        "VisualFlags = 31, HeadMesh = 0, RunSpeed = %d,"
        % (lv, hp, md, sc, rs)
    )
    lines.append(
        "                NpcFamily = %d, LosHeight = 0, CharacterFlags = %d, "
        "AppearanceValue = 0," % (nf, cf)
    )
    lines.append(
        "                Side = 3, Breed = 6, Gender = 1, Race = 1, Fatness = 1, "
        "MovementMode = 3,"
    )
    lines.append(
        "                X = %sf, Y = %sf, Z = %sf,"
        % (repr(float(x)), repr(float(y)), repr(float(z)))
    )
    lines.append("                Hx = 0.0f, Hy = 0.0f, Hz = 0.0f, Hw = 1.0f,")
    lines.append(
        "                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, "
        "new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },"
    )
    lines.append("                Meshes = null,")
    lines.append("            },")

out = r"tools-temp/_tmp_lorelei_oasis_spawn.csfrag"
open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("wrote", len(rows), "npcs to", out)
for r in rows:
    print(r[0], r[1], r[9], r[11])
