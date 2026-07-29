# -*- coding: utf-8 -*-
from __future__ import print_function
import json, csv, collections, os, re

base = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260726-spawn-mob-tll-alien"
out_path = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_tll_alien_out.txt"
lines = []

def p(s=""):
    lines.append(s)

with open(os.path.join(base, "enemy-dossier.json"), "r", encoding="utf-8-sig") as f:
    d = json.load(f)

NPC_SKIP = {
    "Wounded Dockworker", "ICC Immigration Officer Bill", "Vernon Godfray",
    "Lorelei the Bartender", "Dr. Mason", "Vaughn Hammond", "Remi Gallois",
    "Stan Goodman", "Marco Spida", "Flint Novak", "Patrick Sun", "Leonora Marty",
    "Rex Larsson", "Sarah the Thief", "Sifu", "Alex Area", "Marcus Stone",
}

# Combat wildlife: deathObserved OR monsterData in known animal set OR focused fight names
WILDLIFE = {
    "Rollerrat", "Desert Reet", "Greedy Desert Reet", "Gnarl the Roller",
    "Saltworm", "Angry Minibull", "Harvey the Bully", "Lolly",
}

by = collections.defaultdict(list)
for e in d["enemies"]:
    name = e.get("name") or "?"
    if name in NPC_SKIP:
        continue
    md = str(e.get("monsterData") or "")
    by[(name, md)].append(e)

p("=== UNIQUE NAME/MD ===")
for (name, md), arr in sorted(by.items(), key=lambda k: (k[0][0], k[0][1])):
    deaths = sum(1 for e in arr if e.get("deathObserved"))
    lvls = sorted({e.get("level") for e in arr if e.get("level") is not None})
    hps = sorted({e.get("maxHealth") for e in arr if e.get("maxHealth") is not None})
    fams = sorted({str(e.get("npcFamily") or "") for e in arr})
    scales = sorted({str(e.get("monsterScale") or "") for e in arr})
    run = sorted({str(e.get("runSpeed") or "") for e in arr})
    flag = "WILDLIFE" if name in WILDLIFE or deaths else "other"
    if flag != "WILDLIFE" and deaths == 0 and name not in WILDLIFE:
        # skip distant survey clutter unless name looks animal
        if not any(x in name.lower() for x in ("reet", "rat", "worm", "bull", "alien", "gnarl", "harvey", "lolly", "minibu")):
            continue
    p("%s | md=%s | n=%d deaths=%d lvl=%s hp=%s fam=%s scale=%s run=%s [%s]" % (
        name, md, len(arr), deaths, lvls, hps, fams, scales, run, flag))

p()
p("=== SPAWN POSITIONS (wildlife, firstSeen positions clustered) ===")
for name in sorted(WILDLIFE):
    slots = []
    for e in d["enemies"]:
        if e.get("name") != name:
            continue
        pos = e.get("position") or {}
        slots.append((
            e.get("level"), e.get("maxHealth"),
            float(pos.get("x") or 0), float(pos.get("y") or 0), float(pos.get("z") or 0),
            str(e.get("monsterData") or ""),
            str(e.get("monsterScale") or ""),
            e.get("deathObserved"),
            e.get("identity"),
        ))
    if not slots:
        continue
    p("-- %s count=%d --" % (name, len(slots)))
    # dedupe near-identical positions (0.5m)
    kept = []
    for s in slots:
        dup = False
        for k in kept:
            if abs(s[2]-k[2]) < 0.8 and abs(s[4]-k[4]) < 0.8 and s[5] == k[5]:
                dup = True
                break
        if not dup:
            kept.append(s)
    for s in sorted(kept, key=lambda t: (t[2], t[4])):
        p("  lvl=%s hp=%s scale=%s md=%s xyz=(%.3f, %.3f, %.3f) death=%s id=%s" % (
            s[0], s[1], s[6], s[5], s[2], s[3], s[4], s[7], s[8]))

p()
p("=== RESPAWNS ===")
with open(os.path.join(base, "enemy-respawns.csv"), "r", encoding="utf-8") as f:
    for row in csv.DictReader(f):
        p("%s | %s | delay=%s afterGone=%s delta=%s death=(%s,%s,%s) respawn=(%s,%s,%s)" % (
            row["Status"], row["Name"], row["RespawnDelaySeconds"], row["RespawnAfterCorpseGoneSeconds"],
            row["PositionDelta"], row["DeathX"], row["DeathY"], row["DeathZ"],
            row["RespawnX"], row["RespawnY"], row["RespawnZ"]))

p()
p("=== LOOT ===")
with open(os.path.join(base, "corpse-loot-observations.csv"), "r", encoding="utf-8") as f:
    for row in csv.DictReader(f):
        p("%s md=%s lvl=%s credits=%s items=%s" % (
            row["EnemyName"], row["MonsterData"], row["EnemyLevel"], row["CorpseCredits"], row["Items"]))

p()
p("=== XP / SYSTEM REWARDS ===")
xp_re = re.compile(r"(XP|experience|credits|You received|Received)", re.I)
with open(os.path.join(base, "system-messages.log"), "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if "XP" in line or "experience" in line.lower() or "gained" in line.lower() or "Received reward" in line:
            p(line.rstrip()[:300])

p()
p("=== DAMAGE STATS (AttackInfo by enemy name via fight log + combat csv) ===")
# Map identity -> name from dossier
id2name = {}
for e in d["enemies"]:
    id2name[e["identity"]] = e.get("name") or "?"

dmg_to_player = collections.defaultdict(list)  # name -> amounts enemy hit player
dmg_from_player = collections.defaultdict(list)
with open(os.path.join(base, "enemy-combat.csv"), "r", encoding="utf-8") as f:
    for row in csv.DictReader(f):
        if row["MessageType"] != "AttackInfo":
            continue
        amt = row.get("Amount") or ""
        if not amt.isdigit():
            continue
        amount = int(amt)
        src = row["SourceIdentity"]
        tgt = row["TargetIdentity"]
        src_name = id2name.get(src, row.get("SourceRole") or src)
        tgt_name = id2name.get(tgt, row.get("TargetRole") or tgt)
        if row["SourceRole"] == "enemy" or (src in id2name and id2name[src] in WILDLIFE):
            if row["TargetRole"] == "local-player" or "641D0C3C" in tgt:
                dmg_to_player[src_name].append(amount)
        if row["SourceRole"] == "local-player" or "641D0C3C" in src:
            if src_name or True:
                ename = id2name.get(tgt, tgt)
                if ename in WILDLIFE or tgt in id2name:
                    dmg_from_player[ename].append(amount)

for name in sorted(set(list(dmg_to_player) + list(dmg_from_player))):
    if name not in WILDLIFE and not any(x in name.lower() for x in ("reet", "rat", "worm", "bull", "gnarl", "harvey")):
        continue
    to_p = dmg_to_player.get(name, [])
    fr_p = dmg_from_player.get(name, [])
    def stats(arr):
        if not arr:
            return "n/a"
        return "n=%d min=%d max=%d avg=%.1f vals=%s" % (
            len(arr), min(arr), max(arr), sum(arr)/float(len(arr)),
            sorted(collections.Counter(arr).items()))
    p("%s HIT_PLAYER: %s" % (name, stats(to_p)))
    p("%s HIT_BY_PLAYER: %s" % (name, stats(fr_p)))

p()
p("=== SPECIAL ATTACK / ANIM (Unique SpecialAttackWeapon Unknowns for wildlife) ===")
# Pull from fight events for wildlife identities
wild_ids = {e["identity"] for e in d["enemies"] if e.get("name") in WILDLIFE}
saw = collections.defaultdict(set)
with open(os.path.join(base, "enemy-combat.csv"), "r", encoding="utf-8") as f:
    for row in csv.DictReader(f):
        if row["MessageType"] != "SpecialAttackWeapon":
            continue
        src = row["SourceIdentity"]
        if src not in wild_ids and row["SourceRole"] != "enemy":
            continue
        name = id2name.get(src, src)
        if name not in WILDLIFE:
            continue
        detail = row.get("Detail") or ""
        # Unknown1..5
        u = (row.get("Unknown1"), row.get("Unknown2"), row.get("Unknown3"), row.get("Unknown4"), row.get("Unknown5"))
        saw[name].add(u)
        # also WeaponSlot from AttackInfo
for name, vals in sorted(saw.items()):
    p("%s SAW unknowns: %s" % (name, sorted(vals)[:10]))

weapon_slots = collections.defaultdict(set)
with open(os.path.join(base, "enemy-combat.csv"), "r", encoding="utf-8") as f:
    for row in csv.DictReader(f):
        if row["MessageType"] != "AttackInfo":
            continue
        src = row["SourceIdentity"]
        name = id2name.get(src, "")
        if name not in WILDLIFE:
            continue
        # parse WeaponSlot from Detail
        m = re.search(r"WeaponSlot=(\d+)", row.get("Detail") or "")
        if m:
            weapon_slots[name].add(int(m.group(1)))
for name, slots in sorted(weapon_slots.items()):
    p("%s WeaponSlots: %s" % (name, sorted(slots)))

with open(out_path, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))
print("Wrote", out_path, "lines", len(lines))
