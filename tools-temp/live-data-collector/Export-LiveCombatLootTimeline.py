#!/usr/bin/env python3
"""Export a readable combat/death/corpse/loot timeline from decoded AO frames."""

from __future__ import annotations

import argparse
import csv
import json
import re
import struct
from collections import Counter
from pathlib import Path
from typing import Any


IDENTITY_NAMES = {
    0: "None",
    0x68: "Inventory",
    0x6B: "Backpack",
    0x6F: "TradeWindow",
    0x9C50: "Playfield2",
    0xC350: "CanbeAffected",
    0xC767: "TempBag",
    0xC76A: "Corpse",
}

GENERIC_ACTIONS = {
    1: "Get",
    2: "Drop",
    3: "Use",
    4: "UseItemOnItem",
}

C2S_TIMELINE_NAMES = {
    "Attack",
    "CharacterAction",
    "CharSecSpecAttack",
    "ClientMoveItemToInventory",
    "GenericCmd",
    "KnuBotAnswer",
    "KnuBotCloseChatWindow",
    "KnuBotOpenChatWindow",
    "LookAt",
    "StopFight",
}

S2C_TIMELINE_NAMES = {
    "Attack",
    "AttackInfo",
    "CharacterAction",
    "CharSecSpecAttack",
    "ContainerAddItem",
    "CorpseFullUpdate",
    "Despawn",
    "Feedback",
    "GenericCmd",
    "HealthDamage",
    "InventoryUpdate",
    "MissedAttackInfo",
    "Quest",
    "QuestFullUpdate",
    "ReflectAttack",
    "SpecialAttackInfo",
    "SpecialAttackWeapon",
    "Stat",
    "StopFight",
}

CORPSE_SESSION_FIELDNAMES = [
    "corpse",
    "corpse_name",
    "spawn_time",
    "spawn_packet",
    "last_update_time",
    "corpse_update_count",
    "mob_despawn_subject",
    "mob_despawn_time",
    "mob_despawn_packet",
    "first_open_time",
    "first_open_packet",
    "open_count",
    "loot_window_count",
    "first_loot_window_time",
    "first_loot_window_packet",
    "item_move_count",
    "first_item_move_time",
    "last_item_move_time",
    "despawn_time",
    "despawn_packet",
    "lifetime_seconds",
    "opened_before_despawn",
    "looted_before_despawn",
]


def read_u32(data: bytes, offset: int) -> int:
    return struct.unpack_from(">I", data, offset)[0]


def read_i32(data: bytes, offset: int) -> int:
    return struct.unpack_from(">i", data, offset)[0]


def identity(type_value: int, instance: int) -> str:
    if type_value == 0 and instance == 0:
        return "None:00000000"
    return f"{IDENTITY_NAMES.get(type_value, str(type_value))}:{instance:08X}"


def compact_identity(data: bytes, type_offset: int, instance_offset: int) -> str:
    if len(data) <= max(type_offset, instance_offset):
        return ""
    return identity(data[type_offset], data[instance_offset])


def parse_hex(value: str) -> bytes:
    try:
        return bytes.fromhex(value or "")
    except ValueError:
        return b""


def read_csv(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def parse_strings(value: Any) -> list[str]:
    if isinstance(value, list):
        raw = value
    elif isinstance(value, str) and value:
        try:
            parsed = json.loads(value)
            raw = parsed if isinstance(parsed, list) else [value]
        except json.JSONDecodeError:
            raw = [value]
    else:
        raw = []

    cleaned: list[str] = []
    for item in raw:
        text = str(item).replace("\x00", "").strip()
        if text:
            cleaned.append(text)
    return cleaned


def plain_text(value: str) -> str:
    value = value.replace("<BR>", " | ").replace("<br>", " | ")
    value = re.sub(r"<[^>]+>", "", value)
    return re.sub(r"\s+", " ", value).strip(" |")


def time_value(row: dict[str, Any]) -> float:
    try:
        return float(row.get("time") or 0)
    except (TypeError, ValueError):
        return 0.0


def packet_value(row: dict[str, Any]) -> int:
    try:
        return int(row.get("packet_number") or 0)
    except (TypeError, ValueError):
        return 0


def is_corpse_identity(value: str) -> bool:
    return value.startswith("Corpse:")


def body_from_frame(row: dict[str, Any]) -> bytes:
    data = parse_hex(str(row.get("frame_hex") or ""))
    return data[20:] if len(data) >= 20 else b""


def identity_at(data: bytes, offset: int) -> str:
    return identity(read_u32(data, offset), read_u32(data, offset + 4))


def parse_c2s_detail(row: dict[str, Any]) -> tuple[str, str, str]:
    name = str(row.get("name") or "")
    body = body_from_frame(row)
    subject = str(row.get("actor") or "")
    target = str(row.get("target") or "")
    detail = ""

    try:
        if name == "Attack" and len(body) >= 18:
            target = identity_at(body, 9)
            detail = f"base_unknown={body[8]} tail={body[17:].hex()}"
        elif name == "LookAt" and len(body) >= 20:
            target = identity_at(body, 9)
            detail = f"return_info={read_u32(body, 17)}"
        elif name == "CharacterAction" and len(body) >= 36:
            action = read_u32(body, 9)
            target = identity_at(body, 17)
            detail = f"action={action} unknown1={read_i32(body, 13)} p1={read_i32(body, 25)} p2={read_i32(body, 29)} tail={body[33:].hex()}"
        elif name == "GenericCmd" and len(body) >= 41:
            action = read_u32(body, 17)
            user = identity_at(body, 25)
            target = identity_at(body, 33)
            detail = f"action={GENERIC_ACTIONS.get(action, str(action))}({action}) user={user}"
        elif name == "ClientMoveItemToInventory" and len(body) >= 21:
            source = identity_at(body, 9)
            target = source
            source_instance = read_u32(body, 13)
            target_placement = read_u32(body, 17)
            tail = body[21:].hex()
            detail = f"source_container={source} source_slot_hint={source_instance & 0xff} target_placement={target_placement}"
            if tail:
                detail += f" trailing={tail}"
        elif name.startswith("KnuBot") and len(body) >= 24:
            target = identity_at(body, 16)
            detail = f"tail={body[20:].hex()}"
        elif name == "StopFight":
            detail = "client_stop_fight"
    except (struct.error, IndexError):
        detail = "parse_error"

    return subject, target, detail


def parse_s2c_detail(row: dict[str, Any]) -> tuple[str, str, str]:
    name = str(row.get("name") or "")
    body = body_from_frame(row)
    subject = str(row.get("identity") or "")
    target = ""
    strings = [plain_text(text) for text in parse_strings(row.get("strings"))]
    stat_summary = str(row.get("stat_summary") or "")

    if name == "Stat":
        detail = stat_summary
    elif strings:
        detail = " | ".join(strings)
    else:
        detail = ""

    try:
        if name == "Attack" and len(body) >= 18:
            target = identity_at(body, 9)
            detail = f"base_unknown={body[8]} tail={body[17:].hex()}"
        elif name == "AttackInfo" and len(body) >= 41:
            target = identity_at(body, 21)
            detail = (
                f"damage={read_i32(body, 9)} unknown2={read_i32(body, 13)} "
                f"unknown3={read_i32(body, 17)} target={target} "
                f"unknown4={read_i32(body, 29)} unknown5={read_i32(body, 33)} "
                f"unknown6={read_i32(body, 37)}"
            )
        elif name == "SpecialAttackInfo" and len(body) >= 37:
            target = identity_at(body, 21)
            detail = (
                f"unknown1={read_i32(body, 9)} unknown2={read_i32(body, 13)} "
                f"unknown3={read_i32(body, 17)} target={target} "
                f"unknown4={read_i32(body, 29)} unknown5={read_i32(body, 33)}"
            )
        elif name == "HealthDamage" and len(body) >= 37:
            target = identity_at(body, 25)
            detail = (
                f"health_value={read_i32(body, 9)} delta={read_i32(body, 13)} "
                f"value3={read_i32(body, 17)} value4={read_i32(body, 21)} "
                f"source={target} unknown5={read_i32(body, 33)}"
            )
        elif name == "MissedAttackInfo" and len(body) >= 37:
            source = identity_at(body, 17)
            target = identity_at(body, 25)
            detail = (
                f"result={read_i32(body, 9)} unknown2={read_i32(body, 13)} "
                f"source={source} target={target} unknown5={read_i32(body, 33)}"
            )
        elif name == "CorpseFullUpdate":
            target = subject
            corpse_name = next((text for text in strings if "Remains" in text), "")
            if corpse_name:
                detail = corpse_name
        elif name == "Despawn":
            detail = "despawn"
        elif name == "StopFight":
            target = subject
            detail = f"server_stop_fight unknown1={read_i32(body, 9)}" if len(body) >= 13 else "server_stop_fight"
        elif name in {"ReflectAttack"} and not detail:
            detail = "combat_result_signal"
    except (struct.error, IndexError):
        detail = "parse_error"

    return subject, target, detail


def build_rows(capture_dir: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []

    for row in read_csv(capture_dir / "ao_frames.csv"):
        name = str(row.get("name") or "")
        if name not in C2S_TIMELINE_NAMES:
            continue
        subject, target, detail = parse_c2s_detail(row)
        rows.append(
            {
                "time": f"{float(row.get('relative_timestamp') or 0):.3f}",
                "packet_number": row.get("packet_number", ""),
                "direction": "c2s",
                "packet_name": name,
                "subject": subject,
                "target": target,
                "detail": detail,
                "frame_prefix": str(row.get("frame_hex") or "")[:96],
            }
        )

    for row in read_csv(capture_dir / "s2c_frames.csv"):
        name = str(row.get("name") or "")
        if name not in S2C_TIMELINE_NAMES:
            continue
        subject, target, detail = parse_s2c_detail(row)
        rows.append(
            {
                "time": f"{float(row.get('relative_timestamp') or 0):.3f}",
                "packet_number": row.get("packet_number", ""),
                "direction": "s2c",
                "packet_name": name,
                "subject": subject,
                "target": target,
                "detail": detail,
                "frame_prefix": str(row.get("frame_hex") or "")[:96],
            }
        )

    rows.sort(key=lambda item: (float(item["time"]), int(item["packet_number"] or 0), item["direction"]))
    return rows


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    fieldnames = ["time", "packet_number", "direction", "packet_name", "subject", "target", "detail", "frame_prefix"]
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def build_corpse_sessions(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    sessions: dict[str, dict[str, Any]] = {}
    recent_mob_despawns: list[dict[str, Any]] = []
    last_open_corpse = ""
    active_loot_corpse = ""

    for row in rows:
        packet_name = str(row.get("packet_name") or "")
        direction = str(row.get("direction") or "")
        subject = str(row.get("subject") or "")
        target = str(row.get("target") or "")
        now = time_value(row)

        if packet_name == "Despawn" and subject.startswith("CanbeAffected:"):
            recent_mob_despawns.append(row)
            recent_mob_despawns = [despawn for despawn in recent_mob_despawns if now - time_value(despawn) <= 2.0]
            continue

        if packet_name == "CorpseFullUpdate" and is_corpse_identity(subject):
            session = sessions.setdefault(
                subject,
                {
                    "corpse": subject,
                    "corpse_name": "",
                    "spawn_time": f"{now:.3f}",
                    "spawn_packet": row.get("packet_number", ""),
                    "last_update_time": f"{now:.3f}",
                    "corpse_update_count": 0,
                    "mob_despawn_subject": "",
                    "mob_despawn_time": "",
                    "mob_despawn_packet": "",
                    "first_open_time": "",
                    "first_open_packet": "",
                    "open_count": 0,
                    "loot_window_count": 0,
                    "first_loot_window_time": "",
                    "first_loot_window_packet": "",
                    "item_move_count": 0,
                    "first_item_move_time": "",
                    "last_item_move_time": "",
                    "despawn_time": "",
                    "despawn_packet": "",
                    "lifetime_seconds": "",
                    "opened_before_despawn": "no",
                    "looted_before_despawn": "no",
                },
            )
            session["corpse_update_count"] = int(session["corpse_update_count"]) + 1
            session["last_update_time"] = f"{now:.3f}"
            session["corpse_name"] = session["corpse_name"] or str(row.get("detail") or "")

            mob_despawn = next(
                (
                    despawn
                    for despawn in reversed(recent_mob_despawns)
                    if 0 <= now - time_value(despawn) <= 1.0 and packet_value(row) - packet_value(despawn) <= 5
                ),
                None,
            )
            if mob_despawn and not session["mob_despawn_subject"]:
                session["mob_despawn_subject"] = mob_despawn.get("subject", "")
                session["mob_despawn_time"] = mob_despawn.get("time", "")
                session["mob_despawn_packet"] = mob_despawn.get("packet_number", "")
            continue

        if direction == "c2s" and packet_name == "GenericCmd" and is_corpse_identity(target):
            session = sessions.setdefault(
                target,
                {
                    "corpse": target,
                    "corpse_name": "",
                    "spawn_time": "",
                    "spawn_packet": "",
                    "last_update_time": "",
                    "corpse_update_count": 0,
                    "mob_despawn_subject": "",
                    "mob_despawn_time": "",
                    "mob_despawn_packet": "",
                    "first_open_time": "",
                    "first_open_packet": "",
                    "open_count": 0,
                    "loot_window_count": 0,
                    "first_loot_window_time": "",
                    "first_loot_window_packet": "",
                    "item_move_count": 0,
                    "first_item_move_time": "",
                    "last_item_move_time": "",
                    "despawn_time": "",
                    "despawn_packet": "",
                    "lifetime_seconds": "",
                    "opened_before_despawn": "no",
                    "looted_before_despawn": "no",
                },
            )
            session["open_count"] = int(session["open_count"]) + 1
            if not session["first_open_time"]:
                session["first_open_time"] = row.get("time", "")
                session["first_open_packet"] = row.get("packet_number", "")
            last_open_corpse = target
            active_loot_corpse = target
            continue

        if direction == "s2c" and packet_name == "InventoryUpdate" and last_open_corpse:
            session = sessions.get(last_open_corpse)
            if session:
                session["loot_window_count"] = int(session["loot_window_count"]) + 1
                if not session["first_loot_window_time"]:
                    session["first_loot_window_time"] = row.get("time", "")
                    session["first_loot_window_packet"] = row.get("packet_number", "")
                active_loot_corpse = last_open_corpse
            continue

        if direction == "c2s" and packet_name == "ClientMoveItemToInventory" and active_loot_corpse:
            session = sessions.get(active_loot_corpse)
            if session:
                session["item_move_count"] = int(session["item_move_count"]) + 1
                if not session["first_item_move_time"]:
                    session["first_item_move_time"] = row.get("time", "")
                session["last_item_move_time"] = row.get("time", "")
            continue

        if packet_name == "Despawn" and is_corpse_identity(subject):
            session = sessions.setdefault(
                subject,
                {
                    "corpse": subject,
                    "corpse_name": "",
                    "spawn_time": "",
                    "spawn_packet": "",
                    "last_update_time": "",
                    "corpse_update_count": 0,
                    "mob_despawn_subject": "",
                    "mob_despawn_time": "",
                    "mob_despawn_packet": "",
                    "first_open_time": "",
                    "first_open_packet": "",
                    "open_count": 0,
                    "loot_window_count": 0,
                    "first_loot_window_time": "",
                    "first_loot_window_packet": "",
                    "item_move_count": 0,
                    "first_item_move_time": "",
                    "last_item_move_time": "",
                    "despawn_time": "",
                    "despawn_packet": "",
                    "lifetime_seconds": "",
                    "opened_before_despawn": "no",
                    "looted_before_despawn": "no",
                },
            )
            session["despawn_time"] = row.get("time", "")
            session["despawn_packet"] = row.get("packet_number", "")
            if active_loot_corpse == subject:
                active_loot_corpse = ""
            if last_open_corpse == subject:
                last_open_corpse = ""

    for session in sessions.values():
        spawn_time = float(session["spawn_time"]) if session["spawn_time"] else None
        despawn_time = float(session["despawn_time"]) if session["despawn_time"] else None
        if spawn_time is not None and despawn_time is not None:
            session["lifetime_seconds"] = f"{despawn_time - spawn_time:.3f}"
        if int(session["open_count"]) > 0:
            session["opened_before_despawn"] = "yes"
        if int(session["item_move_count"]) > 0:
            session["looted_before_despawn"] = "yes"

    return sorted(sessions.values(), key=lambda session: (float(session["spawn_time"] or "999999"), session["corpse"]))


def write_corpse_sessions_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=CORPSE_SESSION_FIELDNAMES)
        writer.writeheader()
        writer.writerows(rows)


def write_markdown(path: Path, rows: list[dict[str, Any]], corpse_sessions: list[dict[str, Any]], capture_dir: Path) -> None:
    counts = Counter(row["packet_name"] for row in rows)
    corpse_rows = [row for row in rows if row["packet_name"] in {"CorpseFullUpdate", "GenericCmd", "InventoryUpdate", "ClientMoveItemToInventory"}]
    combat_rows = [row for row in rows if row["packet_name"] in {"Attack", "AttackInfo", "HealthDamage", "MissedAttackInfo", "StopFight", "Despawn"}]
    opened_sessions = [session for session in corpse_sessions if int(session["open_count"]) > 0]
    looted_sessions = [session for session in corpse_sessions if int(session["item_move_count"]) > 0]

    lines = [
        "# Live Combat/Loot Timeline",
        "",
        f"- Capture: `{capture_dir}`",
        f"- Timeline rows: **{len(rows)}**",
        f"- Combat/death rows: **{len(combat_rows)}**",
        f"- Corpse/loot rows: **{len(corpse_rows)}**",
        f"- Corpse sessions: **{len(corpse_sessions)}**",
        f"- Opened corpse sessions: **{len(opened_sessions)}**",
        f"- Looted corpse sessions: **{len(looted_sessions)}**",
        "",
        "## Packet Counts",
    ]
    for name, count in counts.most_common():
        lines.append(f"- {name}: {count}")

    lines.extend(["", "## Corpse/Loot Events"])
    for row in corpse_rows[:80]:
        target = f" target={row['target']}" if row["target"] else ""
        detail = f" {row['detail']}" if row["detail"] else ""
        lines.append(f"- t+{row['time']} {row['direction']} {row['packet_name']} {row['subject']}{target}{detail}")
    if len(corpse_rows) > 80:
        lines.append(f"- ... {len(corpse_rows) - 80} more corpse/loot rows in CSV")

    lines.extend(["", "## Corpse Sessions"])
    for session in corpse_sessions[:80]:
        name = f" {session['corpse_name']}" if session["corpse_name"] else ""
        lifetime = f" lifetime={session['lifetime_seconds']}s" if session["lifetime_seconds"] else ""
        mob = f" mob={session['mob_despawn_subject']}" if session["mob_despawn_subject"] else ""
        lines.append(
            "- "
            f"{session['corpse']}{name}"
            f" spawned=t+{session['spawn_time'] or '?'}"
            f" opened={session['open_count']}"
            f" loot_windows={session['loot_window_count']}"
            f" item_moves={session['item_move_count']}"
            f" despawn=t+{session['despawn_time'] or '?'}"
            f"{lifetime}{mob}"
        )
    if len(corpse_sessions) > 80:
        lines.append(f"- ... {len(corpse_sessions) - 80} more corpse sessions in CSV")

    lines.extend(["", "## First Combat/Death Events"])
    for row in combat_rows[:120]:
        target = f" target={row['target']}" if row["target"] else ""
        detail = f" {row['detail']}" if row["detail"] else ""
        lines.append(f"- t+{row['time']} {row['direction']} {row['packet_name']} {row['subject']}{target}{detail}")
    if len(combat_rows) > 120:
        lines.append(f"- ... {len(combat_rows) - 120} more combat rows in CSV")

    lines.extend(["", "## Files", "", "- `live_combat_loot_timeline.csv`", "- `live_corpse_sessions.csv`", "- `live_combat_loot_timeline.md`"])
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture_dir", type=Path)
    args = parser.parse_args()

    rows = build_rows(args.capture_dir)
    corpse_sessions = build_corpse_sessions(rows)
    write_csv(args.capture_dir / "live_combat_loot_timeline.csv", rows)
    write_corpse_sessions_csv(args.capture_dir / "live_corpse_sessions.csv", corpse_sessions)
    write_markdown(args.capture_dir / "live_combat_loot_timeline.md", rows, corpse_sessions, args.capture_dir)
    print(f"timeline_rows={len(rows)}")
    print(f"corpse_sessions={len(corpse_sessions)}")
    print(args.capture_dir / "live_combat_loot_timeline.md")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
