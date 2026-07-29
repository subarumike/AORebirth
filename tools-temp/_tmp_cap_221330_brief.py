# -*- coding: utf-8 -*-
"""Brief analysis of AOSharp capture 20260723-221330. Analysis-only; no game source changes."""
from __future__ import annotations

import csv
import json
import re
from collections import defaultdict
from pathlib import Path

CAP = Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260723-221330"
)
OUT = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_cap_221330_out.txt")

ENC = "utf-8-sig"


def read_text(name: str) -> str:
    return (CAP / name).read_text(encoding=ENC, errors="replace")


def read_json(name: str):
    return json.loads(read_text(name))


def read_csv(name: str):
    path = CAP / name
    if not path.exists():
        return []
    with path.open(encoding=ENC, newline="") as fh:
        return list(csv.DictReader(fh))


def section(lines: list[str], title: str) -> None:
    lines.append("")
    lines.append("=" * 78)
    lines.append(title)
    lines.append("=" * 78)


def pos_str(pos) -> str:
    if not isinstance(pos, dict):
        return str(pos)
    return f"({pos.get('x')}, {pos.get('y')}, {pos.get('z')})"


def waypoint_summary(coords: list[tuple[float, float, float]]) -> str:
    if not coords:
        return "(no coords)"
    n = len(coords)
    mid = coords[n // 2]
    return (
        f"n={n} first={coords[0]} mid={mid} last={coords[-1]}"
    )


def parse_float(v, default=None):
    try:
        if v is None or v == "":
            return default
        return float(v)
    except (TypeError, ValueError):
        return default


def norm_id(ident: str | None) -> str:
    if not ident:
        return ""
    m = re.search(r"SimpleChar:[0-9A-Fa-f]+", ident)
    if not m:
        return ident.strip()
    return f"({m.group(0)})"


def extract_identity(text: str) -> str | None:
    m = re.search(r"SimpleChar:[0-9A-Fa-f]+", text or "")
    return f"({m.group(0)})" if m else None


def main() -> None:
    lines: list[str] = []
    dossier = read_json("enemy-dossier.json")
    enemies = dossier.get("enemies") or []
    by_id = {e.get("identity"): e for e in enemies if e.get("identity")}
    focused = list(dossier.get("focusedEnemyIdentities") or [])

    lines.append(f"Capture: {CAP}")
    lines.append(f"generatedUtc: {dossier.get('generatedUtc')}")
    lines.append(
        f"playfield runtime={dossier.get('runtimePlayfieldId')} "
        f"identity={dossier.get('capturePlayfieldIdentity')} "
        f"objectId={dossier.get('capturePlayfieldObjectId')} "
        f"resource={dossier.get('resourcePlayfieldId')!r}"
    )
    lines.append(f"enemy count: {len(enemies)}")
    lines.append(f"focusedEnemyIdentities count: {len(focused)}")

    # --- focused names ---
    section(lines, "0) focusedEnemyIdentities — unique names")
    focused_names = []
    seen_names = set()
    for fid in focused:
        e = by_id.get(fid)
        name = (e or {}).get("name") or "?"
        lines.append(f"  {fid} -> {name}")
        if name not in seen_names:
            seen_names.add(name)
            focused_names.append(name)
    lines.append(f"UNIQUE NAMES in focusedEnemyIdentities ({len(focused_names)}):")
    for n in sorted(focused_names):
        lines.append(f"  - {n}")

    # --- 1 unique NPCs ---
    section(lines, "1) Unique NPC/mob names from enemy-dossier.json")
    # unique by (name, identity) — user asked all unique names with identity details
    # List every enemy entry (each identity), then unique name set
    unique_names = sorted({(e.get("name") or "") for e in enemies})
    lines.append(f"Unique name count: {len(unique_names)}")
    for name in unique_names:
        lines.append(f"  NAME: {name}")
        for e in enemies:
            if (e.get("name") or "") != name:
                continue
            pf = (
                e.get("runtimePlayfieldId")
                or e.get("capturePlayfieldObjectId")
                or e.get("resourcePlayfieldId")
                or ""
            )
            lines.append(
                f"    identity={e.get('identity')} level={e.get('level')} "
                f"monsterData={e.get('monsterData')} headMesh={e.get('headMesh')!r} "
                f"pos={pos_str(e.get('position'))} deathObserved={e.get('deathObserved')} "
                f"playfield={pf}"
            )

    # --- 2 Dreaming Silvertail + blue name ---
    section(lines, "2) Dreaming Silvertail + CharacterFlags / HasBlueName / blue-name")
    silver = [e for e in enemies if "silvertail" in (e.get("name") or "").lower()]
    dreaming = [e for e in enemies if (e.get("name") or "") == "Dreaming Silvertail"]
    lines.append(f"Dreaming Silvertail entries: {len(dreaming)}")
    for e in dreaming:
        lines.append(
            f"  {e.get('identity')} lvl={e.get('level')} md={e.get('monsterData')} "
            f"headMesh={e.get('headMesh')!r} pos={pos_str(e.get('position'))} "
            f"death={e.get('deathObserved')} visualFlags={e.get('visualFlags')}"
        )
    lines.append(f"All *Silvertail* entries: {len(silver)}")

    scfu = read_csv("scfu-appearance.csv")
    efu = read_csv("enemy-full-updates.csv")

    def blueish_row(row: dict) -> bool:
        blob = " ".join(str(row.get(k, "")) for k in row)
        return bool(
            re.search(
                r"HasBlueName|BlueName|CharacterFlags|blue.?name",
                blob,
                re.I,
            )
        )

    silver_ids = {e.get("identity") for e in silver}
    silver_names = {e.get("name") for e in silver}

    lines.append("--- scfu-appearance.csv rows for *Silvertail* / Dreaming ---")
    scfu_cols_interest = [
        "Identity",
        "Name",
        "CharacterFlags",
        "Flags",
        "FlagsNumeric",
        "Flags2",
        "Flags2Numeric",
        "VisualFlags",
        "Level",
        "MonsterData",
        "HeadMesh",
        "Textures",
        "TextureOverrides",
        "Meshes",
    ]
    scfu_silver_rows = [
        r
        for r in scfu
        if (norm_id(r.get("Identity")) in silver_ids)
        or (r.get("Name") in silver_names)
        or ("silvertail" in (r.get("Name") or "").lower())
    ]
    lines.append(f"matching scfu rows: {len(scfu_silver_rows)}")
    # unique CharacterFlags per identity
    by_scfu_id = defaultdict(list)
    for r in scfu_silver_rows:
        by_scfu_id[r.get("Identity")].append(r)
    for ident, rows in sorted(by_scfu_id.items(), key=lambda x: x[0] or ""):
        names = sorted({r.get("Name") or "" for r in rows})
        cflags = sorted({r.get("CharacterFlags") or "" for r in rows})
        flags = sorted({r.get("Flags") or "" for r in rows})
        vflags = sorted({r.get("VisualFlags") or "" for r in rows})
        lines.append(f"  {ident} names={names}")
        lines.append(f"    CharacterFlags unique: {cflags}")
        lines.append(f"    Flags unique: {flags}")
        lines.append(f"    VisualFlags unique: {vflags}")
        # sample last row fields
        r = rows[-1]
        lines.append(
            f"    last: Level={r.get('Level')} MonsterData={r.get('MonsterData')} "
            f"HeadMesh={r.get('HeadMesh')} FlagsNumeric={r.get('FlagsNumeric')} "
            f"Flags2={r.get('Flags2')} Textures={r.get('Textures')!r} "
            f"TextureOverrides={r.get('TextureOverrides')!r} Meshes={r.get('Meshes')!r}"
        )

    lines.append("--- enemy-full-updates.csv CharacterFlags for Silvertail ---")
    efu_silver = [
        r
        for r in efu
        if (norm_id(r.get("Identity")) in silver_ids)
        or ("silvertail" in (r.get("Name") or "").lower())
    ]
    lines.append(f"matching efu rows: {len(efu_silver)}")
    efu_by_id = defaultdict(list)
    for r in efu_silver:
        efu_by_id[r.get("Identity")].append(r)
    for ident, rows in sorted(efu_by_id.items(), key=lambda x: x[0] or ""):
        cflags = sorted({r.get("CharacterFlags") or "" for r in rows})
        names = sorted({r.get("Name") or "" for r in rows})
        lines.append(f"  {ident} names={names} CharacterFlags={cflags}")

    # Scan events for HasBlueName / blue-name near Silvertail
    lines.append("--- events.log / enemy-fight-events.log blue-name / HasBlueName hits ---")
    for fname in ["events.log", "enemy-fight-events.log", "npc-interactions.log"]:
        text = read_text(fname)
        hits = []
        for i, line in enumerate(text.splitlines()):
            if re.search(r"HasBlueName|BlueName|blue.?name", line, re.I):
                hits.append((i + 1, line[:400]))
            elif "Silvertail" in line and re.search(
                r"CharacterFlags|Flags=", line, re.I
            ):
                if len(hits) < 80:
                    hits.append((i + 1, line[:400]))
        lines.append(f"{fname}: blue-name pattern lines={sum(1 for h in hits if re.search(r'HasBlueName|BlueName|blue.?name', h[1], re.I))}")
        # show first 15 relevant
        shown = 0
        for ln, content in hits:
            if re.search(r"HasBlueName|BlueName|blue.?name|Dreaming Silvertail|Silvertail", content, re.I):
                lines.append(f"  L{ln}: {content}")
                shown += 1
                if shown >= 25:
                    break
        if not hits:
            lines.append(f"  (no HasBlueName/BlueName hits; checking CharacterFlags samples for Dreaming)")
            dream_lines = [
                (i + 1, l[:400])
                for i, l in enumerate(text.splitlines())
                if "Dreaming Silvertail" in l and "CharacterFlags" in l
            ][:10]
            for ln, content in dream_lines:
                lines.append(f"  L{ln}: {content}")

    # Also dump unique CharacterFlags values for Dreaming from scfu
    dream_ids = {e.get("identity") for e in dreaming}
    lines.append("Dreaming Silvertail CharacterFlags (all scfu+efu unique values):")
    vals = set()
    for r in scfu:
        if r.get("Identity") in dream_ids or r.get("Name") == "Dreaming Silvertail":
            vals.add(("scfu", r.get("CharacterFlags"), r.get("Flags"), r.get("FlagsNumeric")))
    for r in efu:
        if r.get("Identity") in dream_ids or r.get("Name") == "Dreaming Silvertail":
            vals.add(("efu", r.get("CharacterFlags"), r.get("Flags"), None))
    for v in sorted(vals, key=lambda x: str(x)):
        lines.append(f"  {v}")

    # --- 3 Dialog NPCs ---
    section(lines, "3) Dialog NPCs (KnubotOpenChatWindow) + AnswerList + bodies")
    dialog_ids = []
    for fname in ["npc-interactions.log", "chat-dialogue.log"]:
        for line in read_text(fname).splitlines():
            if "KnubotOpenChatWindow" in line or "KnuBotOpenChatWindow" in line:
                ident = extract_identity(
                    re.search(r"Target=\([^)]+\)", line).group(0)
                    if re.search(r"Target=\([^)]+\)", line)
                    else line
                )
                # Prefer Target=
                mt = re.search(r"Target=(\(SimpleChar:[0-9A-Fa-f]+\))", line)
                if mt:
                    ident = mt.group(1)
                if ident and ident not in dialog_ids:
                    dialog_ids.append(ident)
                lines.append(f"[{fname}] open -> {ident} | {(by_id.get(ident) or {}).get('name','?')}")
                lines.append(f"  {line[:350]}")

    lines.append("Dialog NPC map (identity -> name):")
    dialog_names = []
    for did in dialog_ids:
        name = (by_id.get(did) or {}).get("name") or "?"
        dialog_names.append(name)
        lines.append(f"  {did} -> {name}")

    # AnswerList options
    lines.append("--- AnswerList option texts ---")
    answer_re = re.compile(r"AnswerList[^\n]*", re.I)
    # Try to extract quoted strings / Options from detail
    opt_re = re.compile(r'"([^"]{1,200})"')
    for fname in ["npc-interactions.log", "chat-dialogue.log", "events.log"]:
        for line in read_text(fname).splitlines():
            if "AnswerList" not in line:
                continue
            opts = opt_re.findall(line)
            # filter noise
            opts = [o for o in opts if o and o not in ("chat-protocol",)]
            mt2 = re.search(r"text=(.*?) detail=", line)
            if mt2:
                opts = [x.strip() for x in mt2.group(1).split(" | ") if x.strip()]
            lines.append(f"[{fname}] options={opts}")
            lines.append(f"  {line[:500]}")

    # Dialog bodies: NPCMessage / Feedback / systemtext mentioning dialog NPC names
    lines.append("--- NPCMessage / Feedback / systemtext for dialog NPC names ---")
    search_names = [n for n in dialog_names if n and n != "?"]
    # also common prefixes
    for fname in ["chat-dialogue.log", "events.log", "system-messages.log", "npc-interactions.log"]:
        text = read_text(fname)
        for line in text.splitlines():
            if not re.search(r"NPCMessage|Feedback|SystemMessage|systemtext|Text=", line, re.I):
                continue
            if any(n in line for n in search_names) or "NPCMessage" in line:
                if "ChannelList" in line:
                    continue
                lines.append(f"[{fname}] {line[:450]}")

    # --- 4 Corpse loot ---
    section(lines, "4) Corpse loot (corpse-loot-observations + corpse-full-updates)")
    loot = read_csv("corpse-loot-observations.csv")
    corpse_fu = read_csv("corpse-full-updates.csv")
    lines.append(f"corpse-loot-observations rows: {len(loot)}")
    if not loot:
        lines.append("  (empty CSV / no observations)")
    for r in loot:
        lines.append(
            f"  corpse={r.get('CorpseIdentity')} enemy={r.get('EnemyName')} "
            f"deadNpc={r.get('DeadNpcIdentity')} itemCount={r.get('ItemCount')} "
            f"credits={r.get('CorpseCredits')} items={r.get('Items')!r} "
            f"status={r.get('CorrelationStatus')}"
        )
    lines.append(f"corpse-full-updates rows: {len(corpse_fu)}")
    for r in corpse_fu:
        lines.append(
            f"  corpse={r.get('CorpseIdentity')} name={r.get('CorpseName')} "
            f"deadNpc={r.get('DeadNpcIdentity')} deadName={r.get('DeadNpcName')} "
            f"monsterData={r.get('CorpseMonsterData')} credits={r.get('CorpseCredits')} "
            f"pos=({r.get('PositionX')},{r.get('PositionY')},{r.get('PositionZ')}) "
            f"pf={r.get('PlayfieldId')}"
        )

    # --- 5 Combat ---
    section(lines, "5) Combat — fought mobs / attack types / animation fields")
    combat = read_csv("enemy-combat.csv")
    fight_log = read_text("enemy-fight-events.log")
    lines.append(f"enemy-combat.csv rows: {len(combat)}")
    fought = defaultdict(lambda: {"actions": defaultdict(int), "msg": defaultdict(int), "samples": []})
    anim_fields = defaultdict(set)
    for r in combat:
        src = norm_id(r.get("SourceIdentity") or "")
        tgt = norm_id(r.get("TargetIdentity") or "")
        action = r.get("Action") or ""
        msg = r.get("MessageType") or ""
        detail = r.get("Detail") or ""
        for role_id in (src, tgt):
            if role_id and role_id in by_id:
                fought[role_id]["actions"][action] += 1
                fought[role_id]["msg"][msg] += 1
                if len(fought[role_id]["samples"]) < 3:
                    fought[role_id]["samples"].append(
                        f"{msg}/{action} amt={r.get('Amount')} detail={detail[:120]}"
                    )
        # animation-ish in detail
        if re.search(r"anim|AttackType|SpecialAttack|PlayAnim", detail, re.I):
            key = f"{src}->{tgt}"
            anim_fields[key].add(detail[:200])

    lines.append(f"mobs appearing in combat with dossier identity: {len(fought)}")
    for ident, info in sorted(fought.items(), key=lambda x: (by_id.get(x[0]) or {}).get("name") or ""):
        name = (by_id.get(ident) or {}).get("name") or "?"
        lines.append(f"  {ident} ({name})")
        lines.append(f"    actions: {dict(info['actions'])}")
        lines.append(f"    messageTypes: {dict(info['msg'])}")
        for s in info["samples"]:
            lines.append(f"    sample: {s}")

    # Also summarize combat by name even if not only dossier
    by_name_combat = defaultdict(lambda: defaultdict(int))
    for r in combat:
        for idkey, role in (
            (norm_id(r.get("SourceIdentity")), r.get("SourceRole")),
            (norm_id(r.get("TargetIdentity")), r.get("TargetRole")),
        ):
            e = by_id.get(idkey or "")
            name = (e or {}).get("name") or idkey or "?"
            by_name_combat[name][r.get("Action") or r.get("MessageType") or "?"] += 1
    lines.append("Combat action tallies by name/id:")
    for name, acts in sorted(by_name_combat.items(), key=lambda x: -sum(x[1].values()))[:40]:
        lines.append(f"  {name}: {dict(acts)}")

    lines.append("Animation-related detail snippets from enemy-combat.csv:")
    if not anim_fields:
        lines.append("  (none matched anim/AttackType/SpecialAttack/PlayAnim in Detail)")
        # show unique MessageType/Action/Unknown fields instead
        msgs = sorted({r.get("MessageType") or "" for r in combat})
        acts = sorted({r.get("Action") or "" for r in combat})
        lines.append(f"  unique MessageTypes: {msgs}")
        lines.append(f"  unique Actions: {acts}")
        # sample Unknown fields
        for r in combat[:8]:
            lines.append(
                f"  sample U1-6={r.get('Unknown1')},{r.get('Unknown2')},{r.get('Unknown3')},"
                f"{r.get('Unknown4')},{r.get('Unknown5')},{r.get('Unknown6')} "
                f"detail={ (r.get('Detail') or '')[:180]}"
            )
    else:
        for k, vals in list(anim_fields.items())[:20]:
            for v in vals:
                lines.append(f"  {k}: {v}")

    # fight events log: extract Fight / Attack / Cast lines for focused + dead
    lines.append("--- enemy-fight-events.log summary (Fight/Attack/Death for focused/dead/Silvertail) ---")
    interest_ids = set(focused) | {e.get("identity") for e in enemies if e.get("deathObserved")} | dream_ids | silver_ids
    fight_hits = []
    for i, line in enumerate(fight_log.splitlines()):
        if not re.search(r"Fight|Attack|Death|Cast|Hit|Miss|Special", line, re.I):
            continue
        # skip movement spam
        if "FollowTarget" in line and "Fight" not in line and "Attack" not in line:
            continue
        if any(iid and iid in line for iid in interest_ids) or any(
            n and n in line for n in ("Dreaming Silvertail", "Swift Silvertail")
        ):
            fight_hits.append((i + 1, line[:350]))
    lines.append(f"relevant fight-event lines: {len(fight_hits)} (showing up to 40)")
    for ln, content in fight_hits[:40]:
        lines.append(f"  L{ln}: {content}")

    # --- 6 Movement FollowTarget ---
    section(lines, "6) FollowTarget path samples (focused / dead / fought / Silvertail)")
    ms = read_json("movement-summary.json")
    lines.append(f"movement-summary: {json.dumps(ms.get('counts'), indent=None)}")
    lines.append(f"followTargetDecodedWithUsablePath={ms.get('followTargetDecodedWithUsablePath')}")

    mov_pkt = read_csv("movement-packets.csv")
    enemy_mov = read_csv("enemy-movement.csv")

    focus_path_ids = set(focused)
    focus_path_ids |= {e.get("identity") for e in enemies if e.get("deathObserved")}
    focus_path_ids |= set(fought.keys())
    focus_path_ids |= silver_ids

    # From movement-packets FollowTarget
    paths = defaultdict(list)  # identity -> list of (x,y,z) from dest or current
    follow_rows = 0
    for r in mov_pkt:
        msg = (r.get("MessageType") or "")
        if "FollowTarget" not in msg and (r.get("FollowKind") or "") == "":
            # still include if MessageType says Follow
            if "Follow" not in msg:
                continue
        follow_rows += 1
        sid = norm_id(r.get("SourceIdentity") or "")
        tid = norm_id(r.get("TargetIdentity") or "")
        # Path coords: Destination preferred, else Current; also RawParams if needed
        coords = []
        for prefix in ("Destination", "Current"):
            x = parse_float(r.get(f"{prefix}X"))
            y = parse_float(r.get(f"{prefix}Y"))
            z = parse_float(r.get(f"{prefix}Z"))
            if x is not None and y is not None and z is not None:
                coords.append((x, y, z))
        # PathCount may encode multi-point; Dest is primary waypoint sample
        for ident in (sid, tid):
            if ident in focus_path_ids and coords:
                # store destination if present else current
                dest = (
                    parse_float(r.get("DestinationX")),
                    parse_float(r.get("DestinationY")),
                    parse_float(r.get("DestinationZ")),
                )
                if dest[0] is not None:
                    paths[ident].append(dest)
                else:
                    paths[ident].append(coords[0])

    lines.append(f"movement-packets Follow-ish rows scanned: {follow_rows}")
    lines.append(f"identities with FollowTarget path samples: {len(paths)}")
    for ident in sorted(paths.keys()):
        name = (by_id.get(ident) or {}).get("name") or "?"
        coords = paths[ident]
        lines.append(f"  {ident} ({name}): {waypoint_summary(coords)}")

    # enemy-movement.csv MoveType FollowTarget
    lines.append("--- enemy-movement.csv FollowTarget ---")
    em_paths = defaultdict(list)
    for r in enemy_mov:
        if "Follow" not in (r.get("MoveType") or "") and "Follow" not in (r.get("MessageType") or ""):
            continue
        ident = norm_id(r.get("Identity") or "")
        if ident not in focus_path_ids:
            continue
        x, y, z = parse_float(r.get("PositionX")), parse_float(r.get("PositionY")), parse_float(r.get("PositionZ"))
        if x is not None:
            em_paths[ident].append((x, y, z))
    for ident in sorted(em_paths.keys()):
        name = (by_id.get(ident) or {}).get("name") or "?"
        lines.append(f"  {ident} ({name}): {waypoint_summary(em_paths[ident])}")
    if not em_paths:
        # show unique MoveTypes
        mtypes = sorted({(r.get("MessageType"), r.get("MoveType")) for r in enemy_mov})
        lines.append(f"  (no Follow matches for focused set); unique msg/move types: {mtypes[:30]}")

    # --- 7 SCFU textures for focused + interacted ---
    section(lines, "7) scfu-appearance Texture/ExtTex/mesh for focused + interacted names")
    # interacted: InfoRequest targets + dialog + combat
    info_ids = set()
    for line in read_text("npc-interactions.log").splitlines():
        if "InfoRequest" in line:
            mt = re.search(r"Target=(\(SimpleChar:[0-9A-Fa-f]+\))", line)
            if mt:
                info_ids.add(mt.group(1))
    interact_ids = set(focused) | set(dialog_ids) | info_ids | set(fought.keys()) | silver_ids
    interact_names = set()
    for iid in interact_ids:
        n = (by_id.get(iid) or {}).get("name")
        if n:
            interact_names.add(n)
    # also names from combat dossier
    lines.append(f"interact identity count: {len(interact_ids)}")
    lines.append(f"interact unique names: {sorted(interact_names)}")

    # Aggregate scfu appearance per identity
    scfu_by_id = defaultdict(list)
    for r in scfu:
        nid = norm_id(r.get("Identity"))
        if nid in interact_ids or (r.get("Name") or "") in interact_names:
            scfu_by_id[nid].append(r)

    # Detect ExtTex-like columns
    if scfu:
        ext_cols = [c for c in scfu[0].keys() if re.search(r"tex|mesh|ext", c, re.I)]
        lines.append(f"scfu texture/mesh-related columns: {ext_cols}")

    for ident in sorted(scfu_by_id.keys(), key=lambda x: x or ""):
        rows = scfu_by_id[ident]
        name = rows[-1].get("Name") or (by_id.get(ident) or {}).get("name") or "?"
        tex = sorted({r.get("Textures") or "" for r in rows})
        texo = sorted({r.get("TextureOverrides") or "" for r in rows})
        meshes = sorted({r.get("Meshes") or "" for r in rows})
        head = sorted({r.get("HeadMesh") or "" for r in rows})
        md = sorted({r.get("MonsterData") or "" for r in rows})
        lines.append(f"  {ident} ({name})")
        lines.append(f"    MonsterData={md} HeadMesh={head}")
        lines.append(f"    Textures={tex}")
        lines.append(f"    TextureOverrides(ExtTex?)={texo}")
        lines.append(f"    Meshes={meshes}")

    # names without scfu
    missing = sorted(interact_names - {(r.get("Name") or "") for rows in scfu_by_id.values() for r in rows})
    if missing:
        lines.append(f"Interact names with no scfu rows by name match: {missing}")

    section(lines, "END")
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {OUT} ({OUT.stat().st_size} bytes, {len(lines)} lines)")


if __name__ == "__main__":
    main()
