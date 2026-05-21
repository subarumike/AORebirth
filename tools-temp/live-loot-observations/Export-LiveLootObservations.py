#!/usr/bin/env python3
"""Export observed live AO corpse loot from decoded packet captures.

Input capture folders must already contain:
- s2c_frames.jsonl from the server-to-client decoder
- ao_frames.jsonl from ao_capture_analyzer.py

The exporter correlates:
- PlayfieldAnarchyF -> current playfield id
- CorpseFullUpdate -> corpse name and death/corpse location
- GenericCmd Use -> corpse-open attempts
- InventoryUpdate -> loot-window item low/high ids and QL
- Stat Cash updates -> credits gained per body
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import re
import struct
from collections import defaultdict
from pathlib import Path
from typing import Any


IDENTITY_NAMES = {
    0: "None",
    0x68: "Inventory",
    0x6B: "Backpack",
    0x6F: "TradeWindow",
    0x9C50: "Playfield2",
    0xC350: "CanbeAffected",
    0xC74A: "WeaponInstance",
    0xC75B: "VendingMachine",
    0xC767: "TempBag",
    0xC76A: "Corpse",
}


def be_int(data: bytes) -> int:
    return int.from_bytes(data, "big")


def le_int(data: bytes) -> int:
    return int.from_bytes(data, "little")


def be_float(data: bytes) -> float:
    return struct.unpack(">f", data)[0]


def identity(type_value: int, instance: int) -> str:
    return f"{IDENTITY_NAMES.get(type_value, f'0x{type_value:08X}')}:{instance:08X}"


def parse_identity_string(value: str) -> tuple[int | None, int | None]:
    if not value:
        return None, None

    name_to_type = {name: key for key, name in IDENTITY_NAMES.items()}
    left, _, right = value.partition(":")
    if not right:
        return None, None

    type_value: int | None
    if left.startswith("0x"):
        type_value = int(left, 16)
    else:
        type_value = name_to_type.get(left)

    return type_value, int(right, 16)


def read_jsonl(path: Path) -> list[dict[str, Any]]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def parse_strings(value: Any) -> list[str]:
    if isinstance(value, list):
        return [str(item) for item in value]

    try:
        parsed = json.loads(value) if value else []
    except (json.JSONDecodeError, TypeError):
        return []
    return parsed if isinstance(parsed, list) else []


def clean_remains_name(strings: list[str]) -> str:
    for text in strings:
        marker = "Remains of "
        index = text.find(marker)
        if index >= 0:
            return text[index + len(marker) :].strip("\x00 ")
    return ""


def load_item_names(repo_root: Path) -> dict[int, dict[str, str]]:
    path = repo_root / "CellAO" / "Libraries" / "Source" / "CellAO.Database" / "SqlTables" / "itemnames.sql"
    names: dict[int, dict[str, str]] = {}
    if not path.exists():
        return names

    text = path.read_text(encoding="utf-8", errors="replace")
    pattern = re.compile(r"\(\s*(\d+)\s*,\s*'((?:''|[^'])*)'\s*,\s*'([^']*)'\s*,\s*'([^']*)'\s*\)")
    for match in pattern.finditer(text):
        item_id = int(match.group(1))
        names[item_id] = {
            "name": match.group(2).replace("''", "'"),
            "type": match.group(3),
            "icon": match.group(4),
        }
    return names


def load_playfield_names(repo_root: Path, client_root: Path | None) -> dict[int, str]:
    path = repo_root / "CellAO" / "Documentation" / "ClientRdbZoneEnemyHints.csv"
    names: dict[int, str] = {}
    if not path.exists():
        pass
    else:
        with path.open("r", encoding="utf-8-sig", newline="") as handle:
            for row in csv.DictReader(handle):
                try:
                    names[int(row["PlayfieldId"])] = row["PlayfieldName"]
                except (KeyError, TypeError, ValueError):
                    continue

    if client_root and client_root.exists():
        positions = client_root / "cd_image" / "twk" / "Tweak_RubiKa_PF_Positions.txt"
        if positions.exists():
            text = positions.read_text(encoding="utf-8", errors="replace")
            pattern = re.compile(r"Vector\s+(\S+)\s+v\([^#]+#\s*Playfield:\s*(\d+)", re.IGNORECASE)
            for match in pattern.finditer(text):
                names.setdefault(int(match.group(2)), match.group(1).replace("_", " "))

        for map_path in (client_root / "cd_image" / "textures" / "PlanetMap").rglob("MapCoordinates.xml"):
            text = map_path.read_text(encoding="utf-8", errors="replace")
            pattern = re.compile(r'<Playfield\s+id="(\d+)"\s+name="([^"]+)"', re.IGNORECASE)
            for match in pattern.finditer(text):
                names.setdefault(int(match.group(1)), match.group(2))

    return names


def parse_frame_bytes(row: dict[str, Any]) -> bytes:
    value = row.get("hex") or row.get("raw_prefix") or row.get("frame_hex") or ""
    return bytes.fromhex(value) if value else b""


def parse_playfield_row(row: dict[str, Any]) -> dict[str, Any] | None:
    type_value, instance = parse_identity_string(row.get("identity", ""))
    if type_value is None or instance is None:
        return None
    return {
        "time": float(row["relative_timestamp"]),
        "playfield_identity": row.get("identity", ""),
        "playfield_type": type_value,
        "playfield_id": instance,
    }


def parse_corpse_row(row: dict[str, Any]) -> dict[str, Any]:
    data = parse_frame_bytes(row)
    body = data[20:] if len(data) >= 20 else b""
    strings = parse_strings(row.get("strings", ""))

    x = y = z = None
    # Live CorpseFullUpdate has identity/common fields followed by one byte
    # before the Vector3. The same offset matched multiple corpse captures.
    if len(body) >= 37:
        values = [be_float(body[offset : offset + 4]) for offset in (25, 29, 33)]
        if all(math.isfinite(value) and -100000.0 < value < 100000.0 for value in values):
            x, y, z = values

    return {
        "time": float(row["relative_timestamp"]),
        "packet_number": row["packet_number"],
        "corpse": row.get("identity", ""),
        "enemy_name": clean_remains_name(strings),
        "enemy_identity": "",
        "location_x": x,
        "location_y": y,
        "location_z": z,
    }


def parse_simple_char(row: dict[str, Any]) -> dict[str, Any] | None:
    data = parse_frame_bytes(row)
    body = data[20:] if len(data) >= 20 else b""
    if len(body) < 30:
        return None

    try:
        pos = 0
        dynel_identity = identity(be_int(body[pos : pos + 4]), be_int(body[pos + 4 : pos + 8]))
        pos += 8
        pos += 1  # N3 unknown byte
        version = body[pos]
        pos += 1
        flags = be_int(body[pos : pos + 4])
        pos += 4

        playfield_id = None
        if flags & 0x00000040:
            playfield_id = be_int(body[pos : pos + 4])
            pos += 4

        coordinates = [be_float(body[pos + offset : pos + offset + 4]) for offset in (0, 4, 8)]
        pos += 12

        if flags & 0x00000200:
            pos += 16

        appearance = be_int(body[pos : pos + 4])
        pos += 4

        name_len = body[pos]
        pos += 1
        name = body[pos : pos + name_len].decode("ascii", errors="replace").rstrip("\x00")
        pos += name_len

        character_flags = be_int(body[pos : pos + 4])
        pos += 4
        account_flags = be_int(body[pos : pos + 2])
        pos += 2
        expansions = be_int(body[pos : pos + 2])
        pos += 2

        if flags & 0x00000001:
            if flags & 0x00020000:
                npc_family = body[pos]
                pos += 1
            else:
                npc_family = be_int(body[pos : pos + 2])
                pos += 2

            if flags & 0x00080000:
                npc_los_height = body[pos]
                pos += 1
            else:
                npc_los_height = be_int(body[pos : pos + 2])
                pos += 2

            pos += 1 if flags & 0x02000000 else 2
            pos += 2
        else:
            npc_family = ""
            npc_los_height = ""

        if flags & 0x00001000:
            level = be_int(body[pos : pos + 2])
            pos += 2
        else:
            level = body[pos]
            pos += 1

        if flags & 0x00000800:
            max_health = be_int(body[pos : pos + 2])
            pos += 2
        else:
            max_health = be_int(body[pos : pos + 4])
            pos += 4

        if flags & 0x00004000:
            health_damage = body[pos]
            pos += 1
        elif flags & 0x00000800:
            health_damage = be_int(body[pos : pos + 2])
            pos += 2
        else:
            health_damage = be_int(body[pos : pos + 4])
            pos += 4

        return {
            "time": float(row["relative_timestamp"]),
            "packet_number": row["packet_number"],
            "enemy_identity": dynel_identity,
            "enemy_name": name,
            "enemy_level": level,
            "enemy_max_health": max_health,
            "enemy_health_damage_at_update": health_damage,
            "enemy_current_health_at_update": max_health - health_damage,
            "enemy_playfield_id": playfield_id,
            "enemy_location_x": coordinates[0],
            "enemy_location_y": coordinates[1],
            "enemy_location_z": coordinates[2],
            "enemy_npc_family": npc_family,
            "enemy_npc_los_height": npc_los_height,
            "simple_char_version": version,
            "simple_char_flags": f"0x{flags:08X}",
            "appearance": appearance,
            "character_flags": character_flags,
            "account_flags": account_flags,
            "expansions": expansions,
        }
    except (IndexError, struct.error, ValueError):
        return None


def parse_despawn(row: dict[str, Any]) -> dict[str, Any] | None:
    identity_type, identity_instance = parse_identity_string(row.get("identity", ""))
    if identity_type is None or identity_instance is None:
        return None

    return {
        "time": float(row["relative_timestamp"]),
        "packet_number": row["packet_number"],
        "identity": row.get("identity", ""),
        "identity_type": identity_type,
        "identity_instance": identity_instance,
    }


def parse_inventory_update(row: dict[str, Any]) -> dict[str, Any] | None:
    data = parse_frame_bytes(row)
    if len(data) < 20:
        return None

    body = data[20:]
    for shift in (9, 8):
        try:
            pos = shift
            number_of_slots = be_int(body[pos : pos + 4])
            pos += 4
            unknown1 = be_int(body[pos : pos + 4])
            pos += 4
            length3f1 = be_int(body[pos : pos + 4])
            pos += 4
            if length3f1 % 0x3F1 != 0:
                continue

            count = (length3f1 // 0x3F1) - 1
            if count < 0 or count > 200:
                continue
            if pos + count * 32 + 16 > len(body):
                continue

            entries = []
            for _ in range(count):
                slot = be_int(body[pos : pos + 4])
                pos += 4
                flags = be_int(body[pos : pos + 2])
                pos += 2
                quantity_or_unknown = be_int(body[pos : pos + 2])
                pos += 2
                item_type = be_int(body[pos : pos + 4])
                pos += 4
                item_instance = be_int(body[pos : pos + 4])
                pos += 4
                low_id = be_int(body[pos : pos + 4])
                pos += 4
                high_id = be_int(body[pos : pos + 4])
                pos += 4
                quality = be_int(body[pos : pos + 4])
                pos += 4
                unknown2 = be_int(body[pos : pos + 4])
                pos += 4

                entries.append(
                    {
                        "loot_slot": slot,
                        "item_identity": identity(item_type, item_instance),
                        "low_id": low_id,
                        "high_id": high_id,
                        "quality": quality,
                        "quantity": quantity_or_unknown,
                        "flags": flags,
                        "entry_unknown2": unknown2,
                    }
                )

            bag_type = be_int(body[pos : pos + 4])
            bag_instance = be_int(body[pos + 4 : pos + 8])
            pos += 8
            temp_container_slot = be_int(body[pos : pos + 4])
            pos += 4
            unknown2 = be_int(body[pos : pos + 4]) if pos + 4 <= len(body) else None

            return {
                "time": float(row["relative_timestamp"]),
                "packet_number": row["packet_number"],
                "corpse": identity(bag_type, bag_instance),
                "number_of_slots": number_of_slots,
                "unknown1": unknown1,
                "entries": entries,
                "entry_count": len(entries),
                "temp_container_slot": temp_container_slot,
                "unknown2": unknown2,
            }
        except (IndexError, struct.error, ValueError):
            continue
    return None


def parse_generic_cmd(row: dict[str, Any]) -> dict[str, Any] | None:
    data = parse_frame_bytes(row)
    body = data[20:] if len(data) >= 20 else b""
    if len(body) < 41:
        return None

    return {
        "time": float(row["relative_timestamp"]),
        "packet_number": row["packet_number"],
        "identity": identity(be_int(body[0:4]), be_int(body[4:8])),
        "count": be_int(body[13:17]),
        "action": be_int(body[17:21]),
        "user": identity(be_int(body[25:29]), be_int(body[29:33])),
        "target": identity(be_int(body[33:37]), be_int(body[37:41])),
        "target_type": be_int(body[33:37]),
    }


def parse_cash_stat(row: dict[str, Any]) -> dict[str, Any] | None:
    data = parse_frame_bytes(row)
    body = data[20:] if len(data) >= 20 else b""
    if len(body) < 21:
        return None

    owner = identity(be_int(body[0:4]), be_int(body[4:8]))
    pos = 9
    count = be_int(body[pos : pos + 4])
    pos += 4
    if count < 0 or count > 200:
        return None

    for _ in range(count):
        if pos + 8 > len(body):
            return None
        stat_id = be_int(body[pos : pos + 4])
        stat_value = be_int(body[pos + 4 : pos + 8])
        pos += 8
        if stat_id == 0x3D:
            return {
                "time": float(row["relative_timestamp"]),
                "packet_number": row["packet_number"],
                "owner": owner,
                "cash": stat_value,
            }
    return None


def parse_full_character_cash(row: dict[str, Any]) -> int | None:
    data = parse_frame_bytes(row)
    body = data[20:] if len(data) >= 20 else b""
    if len(body) < 32:
        return None

    try:
        pos = 0
        pos += 8  # identity
        pos += 1  # N3 unknown byte
        pos += 4  # MsgVersion

        def read_x3f1_count() -> int:
            nonlocal pos
            value = be_int(body[pos : pos + 4])
            pos += 4
            return (value // 0x3F1) - 1

        inventory_count = read_x3f1_count()
        pos += inventory_count * 32

        uploaded_nano_count = read_x3f1_count()
        pos += uploaded_nano_count * 4

        unknown2_count = read_x3f1_count()
        pos += unknown2_count * 3

        pos += 4  # Unknown3

        unknown4_count = be_int(body[pos : pos + 4])
        pos += 4 + unknown4_count * 20

        pos += 4  # UnknownI2

        unknown5_count = be_int(body[pos : pos + 4])
        pos += 4 + unknown5_count * 20

        pos += 4  # UnknownI3

        unknown6_count = be_int(body[pos : pos + 4])
        pos += 4 + unknown6_count * 20

        for _ in range(2):
            stat_count = read_x3f1_count()
            for _ in range(stat_count):
                stat_id = be_int(body[pos : pos + 4])
                stat_value = be_int(body[pos + 4 : pos + 8])
                pos += 8
                if stat_id == 0x3D:
                    return stat_value
    except (IndexError, ValueError):
        return None

    return None


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    if not rows:
        path.write_text("", encoding="utf-8")
        return

    keys: list[str] = []
    for row in rows:
        for key in row:
            if key not in keys:
                keys.append(key)

    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=keys)
        writer.writeheader()
        writer.writerows(rows)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("capture_dir", type=Path)
    parser.add_argument("--repo-root", type=Path, default=Path(r"C:\Users\Mike\Documents\Cellao-Clean"))
    parser.add_argument("--client-root", type=Path, default=Path(r"C:\Funcom\Anarchy Online"))
    parser.add_argument("--initial-cash", type=int, default=None)
    args = parser.parse_args()

    capture_dir: Path = args.capture_dir
    s2c_rows = read_jsonl(capture_dir / "s2c_frames.jsonl")
    c2s_rows = read_jsonl(capture_dir / "ao_frames.jsonl")
    item_names = load_item_names(args.repo_root)
    playfield_names = load_playfield_names(args.repo_root, args.client_root)

    playfields = [parsed for row in s2c_rows if row.get("name") == "PlayfieldAnarchyF" for parsed in [parse_playfield_row(row)] if parsed]
    enemy_updates = {
        parsed["enemy_identity"]: parsed
        for row in s2c_rows
        if row.get("name") == "SimpleCharFullUpdate"
        for parsed in [parse_simple_char(row)]
        if parsed
    }
    despawns = [
        parsed
        for row in s2c_rows
        if row.get("name") == "Despawn"
        for parsed in [parse_despawn(row)]
        if parsed and parsed["identity_type"] == 0xC350
    ]
    corpses = {}
    for row in s2c_rows:
        if row.get("name") != "CorpseFullUpdate":
            continue

        corpse = parse_corpse_row(row)
        candidates = [
            despawn
            for despawn in despawns
            if 0 <= corpse["time"] - despawn["time"] <= 1.0
            and int(corpse["packet_number"]) - int(despawn["packet_number"]) <= 5
        ]
        if candidates:
            corpse["enemy_identity"] = candidates[-1]["identity"]
        corpses[corpse["corpse"]] = corpse

    inventory_updates = [parsed for row in s2c_rows if row.get("name") == "InventoryUpdate" for parsed in [parse_inventory_update(row)] if parsed]
    corpse_uses = [
        parsed
        for row in c2s_rows
        if row.get("name") == "GenericCmd"
        for parsed in [parse_generic_cmd(row)]
        if parsed and parsed["action"] == 3 and parsed["target_type"] == 0xC76A
    ]
    cash_updates = [parsed for row in s2c_rows if row.get("name") == "Stat" for parsed in [parse_cash_stat(row)] if parsed]
    initial_cash = args.initial_cash
    if initial_cash is None:
        for row in s2c_rows:
            if row.get("name") != "FullCharacter":
                continue
            initial_cash = parse_full_character_cash(row)
            if initial_cash is not None:
                break

    uses_by_corpse: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for use in corpse_uses:
        uses_by_corpse[use["target"]].append(use)

    cash_updates.sort(key=lambda row: row["time"])

    body_rows: list[dict[str, Any]] = []
    drop_rows: list[dict[str, Any]] = []

    for inventory_update in sorted(inventory_updates, key=lambda row: row["time"]):
        corpse_id = inventory_update["corpse"]
        corpse = corpses.get(corpse_id, {})
        playfield = next((pf for pf in reversed(playfields) if pf["time"] <= inventory_update["time"]), None)
        playfield_id = playfield["playfield_id"] if playfield else ""
        playfield_identity = playfield["playfield_identity"] if playfield else ""

        open_candidates = [use for use in uses_by_corpse.get(corpse_id, []) if 0 <= inventory_update["time"] - use["time"] <= 3.0]
        open_cmd = open_candidates[-1] if open_candidates else None

        cash_candidates = [cash for cash in cash_updates if 0 <= cash["time"] - inventory_update["time"] <= 3.0]
        cash_update = cash_candidates[0] if cash_candidates else None
        previous_cash = None
        credits = None
        if cash_update:
            previous = [cash for cash in cash_updates if cash["time"] < cash_update["time"]]
            if previous:
                previous_cash = previous[-1]["cash"]
            elif initial_cash is not None:
                previous_cash = initial_cash

            if previous_cash is not None:
                credits = cash_update["cash"] - previous_cash

        base = {
            "capture": capture_dir.name,
            "playfield_id": playfield_id,
            "playfield_name": playfield_names.get(playfield_id, "") if isinstance(playfield_id, int) else "",
            "playfield_identity": playfield_identity,
            "corpse": corpse_id,
            "enemy_identity": corpse.get("enemy_identity", ""),
            "enemy_name": corpse.get("enemy_name", ""),
            "enemy_level": "",
            "enemy_max_health": "",
            "enemy_current_health_at_update": "",
            "enemy_health_damage_at_update": "",
            "location_x": f'{corpse["location_x"]:.3f}' if corpse.get("location_x") is not None else "",
            "location_y": f'{corpse["location_y"]:.3f}' if corpse.get("location_y") is not None else "",
            "location_z": f'{corpse["location_z"]:.3f}' if corpse.get("location_z") is not None else "",
            "corpse_packet": corpse.get("packet_number", ""),
            "open_packet": open_cmd["packet_number"] if open_cmd else "",
            "loot_window_packet": inventory_update["packet_number"],
            "loot_window_time_sec": f'{inventory_update["time"]:.3f}',
            "item_entry_count": inventory_update["entry_count"],
            "credits": credits if credits is not None else "",
            "cash_after": cash_update["cash"] if cash_update else "",
            "cash_stat_packet": cash_update["packet_number"] if cash_update else "",
        }
        enemy = enemy_updates.get(corpse.get("enemy_identity", ""))
        if enemy:
            base["enemy_name"] = base["enemy_name"] or enemy["enemy_name"]
            base["enemy_level"] = enemy["enemy_level"]
            base["enemy_max_health"] = enemy["enemy_max_health"]
            base["enemy_current_health_at_update"] = enemy["enemy_current_health_at_update"]
            base["enemy_health_damage_at_update"] = enemy["enemy_health_damage_at_update"]

        body_rows.append(base)

        if credits is not None:
            drop_rows.append(
                {
                    **base,
                    "loot_kind": "Credits",
                    "loot_type": "Currency",
                    "loot_name": "Credits",
                    "loot_quality": "",
                    "quantity": credits,
                    "low_id": "",
                    "high_id": "",
                    "item_identity": "",
                }
            )

        for entry in inventory_update["entries"]:
            low_info = item_names.get(entry["low_id"], {})
            high_info = item_names.get(entry["high_id"], {})
            drop_rows.append(
                {
                    **base,
                    "loot_kind": "Item",
                    "loot_type": low_info.get("type") or high_info.get("type") or "",
                    "loot_name": low_info.get("name") or high_info.get("name") or "",
                    "loot_quality": entry["quality"],
                    "quantity": entry["quantity"],
                    "low_id": entry["low_id"],
                    "high_id": entry["high_id"],
                    "item_identity": entry["item_identity"],
                }
            )

    write_csv(capture_dir / "loot_body_observations.csv", body_rows)
    write_csv(capture_dir / "loot_drop_observations.csv", drop_rows)

    item_rows = [row for row in drop_rows if row["loot_kind"] == "Item"]
    credit_rows = [row for row in drop_rows if row["loot_kind"] == "Credits"]
    lines = [
        "# Live Loot Observation Export",
        "",
        f"- Bodies with loot windows: **{len(body_rows)}**",
        f"- Item drops: **{len(item_rows)}**",
        f"- Credit drops with computed deltas: **{len(credit_rows)}**",
        f"- Playfields observed: **{len({row['playfield_id'] for row in body_rows if row['playfield_id'] != ''})}**",
        f"- Initial cash: **{initial_cash if initial_cash is not None else 'not found'}**",
        "",
        "## Files",
        "- `loot_body_observations.csv`",
        "- `loot_drop_observations.csv`",
    ]
    (capture_dir / "live_loot_observation_export.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(f"bodies={len(body_rows)} items={len(item_rows)} credit_rows={len(credit_rows)}")
    print(capture_dir / "loot_body_observations.csv")
    print(capture_dir / "loot_drop_observations.csv")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
