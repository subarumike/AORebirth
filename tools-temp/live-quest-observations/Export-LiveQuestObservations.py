#!/usr/bin/env python3
"""Export useful quest observations from decoded AO packet captures.

Input capture folders must already contain:
- s2c_frames.jsonl from the server-to-client decoder
- ao_frames.jsonl from ao_capture_analyzer.py

The exporter intentionally favors clean, corroborated signals over noisy
scanner hits. QuestFullUpdate rows are trusted only for the logged-in player
identity, which avoids false positives from decompressed stream resync noise.
"""

from __future__ import annotations

import argparse
import csv
import html
import json
import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


PLAYER_TYPE = "CanbeAffected"
IDENTITY_NAMES = {
    0: "None",
    0x68: "Inventory",
    0x6B: "Backpack",
    0x6F: "TradeWindow",
    0x9C50: "Playfield2",
    0xC350: "CanbeAffected",
    0xC76A: "Corpse",
}
QUEST_FULL_UPDATE = "QuestFullUpdate"
PLAYER_SIGNAL_NAMES = {
    "ChatText",
    "ContainerAddItem",
    "Feedback",
    "InventoryUpdate",
    "NewLevel",
    "Quest",
    "Stat",
}
ACTION_NAMES = {
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

OBJECTIVE_COLORS = {
    "909090": "done",
    "FFFFFF": "current",
    "006699": "pending",
    "D60000": "warning",
}


def read_jsonl(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        return []
    return [json.loads(line) for line in path.read_text(encoding="utf-8", errors="replace").splitlines() if line.strip()]


def as_float(value: Any) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return 0.0


def parse_hex(value: Any) -> bytes:
    try:
        return bytes.fromhex(str(value or ""))
    except ValueError:
        return b""


def frame_body(row: dict[str, Any]) -> bytes:
    data = parse_hex(row.get("frame_hex"))
    return data[20:] if len(data) >= 20 else b""


def read_u32(data: bytes, offset: int) -> int:
    return int.from_bytes(data[offset : offset + 4], "big", signed=False)


def read_i32(data: bytes, offset: int) -> int:
    return int.from_bytes(data[offset : offset + 4], "big", signed=True)


def identity(type_value: int, instance: int) -> str:
    if type_value == 0 and instance == 0:
        return "None:00000000"
    return f"{IDENTITY_NAMES.get(type_value, str(type_value))}:{instance:08X}"


def identity_at(data: bytes, offset: int) -> str:
    if len(data) < offset + 8:
        return ""
    return identity(read_u32(data, offset), read_u32(data, offset + 4))


def normalize_strings(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        raw = value
    elif isinstance(value, str):
        if not value:
            return []
        try:
            parsed = json.loads(value)
            raw = parsed if isinstance(parsed, list) else [value]
        except json.JSONDecodeError:
            raw = [value]
    else:
        raw = [str(value)]

    cleaned: list[str] = []
    for item in raw:
        text = str(item).replace("\x00", "").strip()
        if text:
            cleaned.append(text)
    return cleaned


def strip_tags(value: str) -> str:
    value = value.replace("<BR>", " | ").replace("<br>", " | ").replace("<br/>", " | ")
    value = re.sub(r"<[^>]+>", "", value)
    value = html.unescape(value)
    return re.sub(r"\s+", " ", value).strip(" |")


def printable_score(value: str) -> float:
    if not value:
        return 0.0
    printable = sum(1 for ch in value if ch.isprintable())
    return printable / len(value)


def is_player_identity(identity: str, player_identity: str) -> bool:
    return bool(player_identity) and identity == player_identity


def find_player_identity(s2c_rows: list[dict[str, Any]]) -> str:
    for row in s2c_rows:
        if row.get("name") == "FullCharacter":
            identity = str(row.get("identity") or "")
            if identity.startswith(f"{PLAYER_TYPE}:"):
                return identity

    # Fallback for captures where FullCharacter was missed: use a player-owned
    # quest/chat signal rather than mob simple-character rows.
    candidates: Counter[str] = Counter()
    for row in s2c_rows:
        if row.get("name") in {"QuestFullUpdate", "ChatText", "Feedback", "Quest"}:
            identity = str(row.get("identity") or "")
            if identity.startswith(f"{PLAYER_TYPE}:"):
                candidates[identity] += 1
    if candidates:
        return candidates.most_common(1)[0][0]
    return ""


def build_npc_name_index(s2c_rows: list[dict[str, Any]]) -> dict[str, str]:
    names: dict[str, str] = {}
    for row in s2c_rows:
        if row.get("name") != "SimpleCharFullUpdate":
            continue
        identity = str(row.get("identity") or "")
        if not identity.startswith(f"{PLAYER_TYPE}:"):
            continue
        strings = normalize_strings(row.get("strings"))
        useful = [text for text in strings if printable_score(text) > 0.8 and len(text) >= 3]
        if useful:
            names.setdefault(identity, useful[-1])
    return names


def guess_target_name(frame_hex: str, npc_names: dict[str, str]) -> tuple[str, str]:
    lowered = (frame_hex or "").lower()
    for identity, name in npc_names.items():
        _, _, instance = identity.partition(":")
        if instance and instance.lower() in lowered:
            return identity, name
    return "", ""


def extract_reward(description: str) -> str:
    match = re.search(r'<font color="#33FF66">\s*Final reward:\s*</font>\s*([^<]+)', description, re.IGNORECASE)
    if not match:
        return ""
    return strip_tags(match.group(1))


def extract_objectives(description: str) -> list[dict[str, str]]:
    objectives: list[dict[str, str]] = []
    for color, text in re.findall(r'<font color="#([0-9A-Fa-f]{6})">(.*?)</font>', description, re.IGNORECASE | re.DOTALL):
        color_key = color.upper()
        if color_key == "33FF66":
            continue
        state = OBJECTIVE_COLORS.get(color_key, f"color_{color_key}")
        objectives.append({"state": state, "text": strip_tags(text)})
    return objectives


def looks_like_quest_pair(title: str, description: str) -> bool:
    if len(title) < 4 or len(title) > 80:
        return False
    if "<" in title or ">" in title:
        return False
    if printable_score(title) < 0.9:
        return False
    if "<BR>" not in description and "Final reward" not in description:
        return False
    if printable_score(description.replace("<BR>", "")) < 0.75:
        return False
    return True


def extract_quest_records(row: dict[str, Any], player_identity: str) -> list[dict[str, Any]]:
    if row.get("name") != QUEST_FULL_UPDATE:
        return []
    if not is_player_identity(str(row.get("identity") or ""), player_identity):
        return []

    strings = normalize_strings(row.get("strings"))
    records: list[dict[str, Any]] = []
    for index in range(len(strings) - 1):
        title = strings[index].strip()
        description = strings[index + 1].strip()
        if not looks_like_quest_pair(title, description):
            continue

        objectives = extract_objectives(description)
        event_type = "login_mission_replay" if as_float(row.get("relative_timestamp")) < 705 else "mission_update"
        records.append(
            {
                "time": f"{as_float(row.get('relative_timestamp')):.3f}",
                "packet_number": row.get("packet_number", ""),
                "event_type": event_type,
                "identity": row.get("identity", ""),
                "title": title,
                "reward": extract_reward(description),
                "objectives": " | ".join(f"{item['state']}:{item['text']}" for item in objectives),
                "description_plain": strip_tags(description),
            }
        )
    return records


def extract_dialogue_rows(c2s_rows: list[dict[str, Any]], s2c_rows: list[dict[str, Any]], npc_names: dict[str, str]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    latest_choices: list[str] = []

    for row in sorted(s2c_rows, key=lambda item: as_float(item.get("relative_timestamp"))):
        name = str(row.get("name") or "")
        if not name.startswith("KnuBot"):
            continue
        strings = normalize_strings(row.get("strings"))
        if name == "KnuBotAnswerList":
            latest_choices = strings
        rows.append(
            {
                "direction": "s2c",
                "time": f"{as_float(row.get('relative_timestamp')):.3f}",
                "packet_number": row.get("packet_number", ""),
                "name": name,
                "target_identity": "",
                "target_name": "",
                "choice_count": len(strings) if name == "KnuBotAnswerList" else "",
                "text": " | ".join(strings),
                "raw_tail": "",
            }
        )

    for row in sorted(c2s_rows, key=lambda item: as_float(item.get("relative_timestamp"))):
        name = str(row.get("name") or "")
        if not name.startswith("KnuBot"):
            continue
        target_identity, target_name = guess_target_name(str(row.get("frame_hex") or ""), npc_names)
        frame_hex = str(row.get("frame_hex") or "")
        raw_tail = frame_hex[-16:] if frame_hex else ""
        rows.append(
            {
                "direction": "c2s",
                "time": f"{as_float(row.get('relative_timestamp')):.3f}",
                "packet_number": row.get("packet_number", ""),
                "name": name,
                "target_identity": target_identity,
                "target_name": target_name,
                "choice_count": len(latest_choices) if name == "KnuBotAnswer" and latest_choices else "",
                "text": "",
                "raw_tail": raw_tail,
            }
        )

    rows.sort(key=lambda item: float(item["time"]))
    return rows


def clean_player_signals(s2c_rows: list[dict[str, Any]], player_identity: str) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for row in s2c_rows:
        name = str(row.get("name") or "")
        identity = str(row.get("identity") or "")
        if name not in PLAYER_SIGNAL_NAMES:
            continue
        if identity and not is_player_identity(identity, player_identity):
            continue

        strings = normalize_strings(row.get("strings"))
        text = " | ".join(strings)
        if name == "ChatText" and (not text or printable_score(text) < 0.9):
            continue

        event_kind, detail = classify_player_signal(row, text)
        rows.append(
            {
                "time": f"{as_float(row.get('relative_timestamp')):.3f}",
                "packet_number": row.get("packet_number", ""),
                "name": name,
                "identity": identity,
                "event_kind": event_kind,
                "stat_summary": row.get("stat_summary") or "",
                "text": text,
                "detail": detail,
                "frame_prefix": str(row.get("frame_hex") or "")[:120],
            }
        )
    return rows


def classify_player_signal(row: dict[str, Any], text: str) -> tuple[str, str]:
    name = str(row.get("name") or "")
    stat_summary = str(row.get("stat_summary") or "")
    body = frame_body(row)

    try:
        if name == "ChatText":
            lowered = text.lower()
            if "received mission reward" in lowered:
                return "mission_reward_chat", text
            if re.search(r"\breceived\b.*\bxp\b", lowered):
                return "mission_xp_chat", text
            if "mission complete" in lowered:
                return "mission_complete_chat", text
            return "chat", text

        if name == "ContainerAddItem" and len(body) >= 29:
            source_container = identity_at(body, 9)
            target = identity_at(body, 17)
            source_instance = read_u32(body, 13)
            return (
                "inventory_item_add",
                f"source_container={source_container} source_slot_hint={source_instance & 0xff} target={target} target_placement={read_i32(body, 25)} base_unknown={body[8]}",
            )

        if name == "Feedback" and len(body) >= 21:
            return (
                "feedback",
                f"unknown1={read_i32(body, 9)} category_id={read_i32(body, 13)} message_id=0x{read_u32(body, 17):08X} base_unknown={body[8]}",
            )

        if name == "Quest" and len(body) >= 13:
            values = [read_u32(body, offset) for offset in range(9, len(body) - 3, 4)]
            return "quest_packet", "fields=" + ",".join(f"0x{value:08X}" for value in values)

        if name == "NewLevel" and len(body) >= 13:
            values = [read_u32(body, offset) for offset in range(9, len(body) - 3, 4)]
            return "new_level_or_reward", "fields=" + ",".join(f"0x{value:08X}" for value in values)

        if name == "InventoryUpdate":
            return "inventory_update", "inventory update"

        if name == "Stat":
            if re.search(r"\b(XP|Cash)\b", stat_summary):
                return "stat_reward", stat_summary
            if "Quest" in stat_summary or "MissionBits" in stat_summary:
                return "stat_quest", stat_summary
            return "stat_state", stat_summary
    except (IndexError, ValueError):
        return "parse_error", ""

    return name.lower(), text or stat_summary or name


def reward_event_rows(signal_rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    reward_kinds = {
        "feedback",
        "inventory_item_add",
        "mission_complete_chat",
        "mission_reward_chat",
        "mission_xp_chat",
        "new_level_or_reward",
        "quest_packet",
        "stat_quest",
        "stat_reward",
    }
    return [row for row in signal_rows if row.get("event_kind") in reward_kinds]


def extract_action_rows(c2s_rows: list[dict[str, Any]], npc_names: dict[str, str]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for row in c2s_rows:
        name = str(row.get("name") or "")
        if name not in ACTION_NAMES:
            continue
        target_identity, target_name = guess_target_name(str(row.get("frame_hex") or ""), npc_names)
        rows.append(
            {
                "time": f"{as_float(row.get('relative_timestamp')):.3f}",
                "packet_number": row.get("packet_number", ""),
                "name": name,
                "sender": row.get("sender", ""),
                "target": row.get("target", ""),
                "target_identity": target_identity,
                "target_name": target_name,
                "action": row.get("action", ""),
                "action_guess": row.get("action_guess", ""),
                "tail_hex": row.get("tail_hex") or row.get("action_raw_hex") or str(row.get("frame_hex") or "")[-24:],
            }
        )
    return rows


def write_csv(path: Path, rows: list[dict[str, Any]], fieldnames: list[str]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def build_markdown(
    cap_dir: Path,
    player_identity: str,
    quest_rows: list[dict[str, Any]],
    dialogue_rows: list[dict[str, Any]],
    signal_rows: list[dict[str, Any]],
    reward_rows: list[dict[str, Any]],
    action_rows: list[dict[str, Any]],
    excluded_rows: list[dict[str, Any]],
) -> str:
    quest_by_title: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in quest_rows:
        quest_by_title[row["title"]].append(row)

    lines = [
        "# Live Quest Observation Export",
        "",
        f"- Capture folder: `{cap_dir}`",
        f"- Player identity: `{player_identity or 'unknown'}`",
        f"- Clean quest records: **{len(quest_rows)}**",
        f"- Distinct quest titles: **{len(quest_by_title)}**",
        f"- Dialogue/NPC chat events: **{len(dialogue_rows)}**",
        f"- Player reward/inventory/chat/stat signals: **{len(signal_rows)}**",
        f"- Focused reward/completion events: **{len(reward_rows)}**",
        f"- Client action/combat/use events: **{len(action_rows)}**",
        f"- Excluded noisy quest candidates: **{len(excluded_rows)}**",
        "",
        "## Interpretation",
        "",
        "This run captured one-shot missions that were already in the mission window. The first clean QuestFullUpdate is treated as a login mission replay, not quest-start evidence. Later clean QuestFullUpdate rows are mission progress or completion updates.",
        "",
        "## Quest State",
    ]

    for title, rows in quest_by_title.items():
        latest = rows[-1]
        lines.append(f"- {title}: updates={len(rows)} latest={latest['objectives'] or 'no parsed objective'} reward={latest['reward']}")

    lines.extend(["", "## Clean Quest Events"])
    for row in quest_rows:
        lines.append(
            f"- t+{row['time']} {row['event_type']} `{row['title']}` reward={row['reward']} objectives={row['objectives']}"
        )

    lines.extend(["", "## Dialogue / Turn-In Events"])
    for row in dialogue_rows[:40]:
        target = f" {row['target_name']}" if row.get("target_name") else ""
        text = row.get("text") or row.get("raw_tail") or ""
        lines.append(f"- t+{row['time']} {row['direction']} {row['name']}{target}: {text}")
    if len(dialogue_rows) > 40:
        lines.append(f"- ... {len(dialogue_rows) - 40} more dialogue rows in CSV")

    lines.extend(["", "## Reward / Inventory / Chat Signals"])
    for row in signal_rows:
        summary = row["detail"] or row["text"] or row["stat_summary"] or row["name"]
        lines.append(f"- t+{row['time']} {row['name']} [{row['event_kind']}] {summary}")

    lines.extend(["", "## Focused Reward Event Sequence"])
    for row in reward_rows:
        summary = row["detail"] or row["text"] or row["stat_summary"] or row["name"]
        lines.append(f"- t+{row['time']} {row['name']} [{row['event_kind']}] {summary}")

    if excluded_rows:
        lines.extend(["", "## Excluded Scanner Noise"])
        for row in excluded_rows[:10]:
            strings = " | ".join(normalize_strings(row.get("strings")))[:180]
            lines.append(f"- t+{as_float(row.get('relative_timestamp')):.3f} {row.get('name')} {row.get('identity')}: {strings}")

    lines.extend(
        [
            "",
            "## Files",
            "",
            "- `quest_batch_observation_export.md`",
            "- `quest_dialog_observations.csv`",
            "- `quest_update_observations.csv`",
            "- `quest_action_observations.csv`",
            "- `quest_reward_inventory_observations.csv`",
            "- `quest_reward_events.csv`",
        ]
    )
    return "\n".join(lines) + "\n"


def export(cap_dir: Path) -> None:
    s2c_rows = read_jsonl(cap_dir / "s2c_frames.jsonl")
    c2s_rows = read_jsonl(cap_dir / "ao_frames.jsonl")
    player_identity = find_player_identity(s2c_rows)
    npc_names = build_npc_name_index(s2c_rows)

    quest_rows: list[dict[str, Any]] = []
    excluded_rows: list[dict[str, Any]] = []
    for row in s2c_rows:
        if row.get("name") != QUEST_FULL_UPDATE:
            continue
        extracted = extract_quest_records(row, player_identity)
        if extracted:
            quest_rows.extend(extracted)
        else:
            excluded_rows.append(row)

    quest_rows.sort(key=lambda item: (float(item["time"]), item["title"]))
    dialogue_rows = extract_dialogue_rows(c2s_rows, s2c_rows, npc_names)
    signal_rows = clean_player_signals(s2c_rows, player_identity)
    reward_rows = reward_event_rows(signal_rows)
    action_rows = extract_action_rows(c2s_rows, npc_names)

    write_csv(
        cap_dir / "quest_update_observations.csv",
        quest_rows,
        ["time", "packet_number", "event_type", "identity", "title", "reward", "objectives", "description_plain"],
    )
    write_csv(
        cap_dir / "quest_dialog_observations.csv",
        dialogue_rows,
        ["direction", "time", "packet_number", "name", "target_identity", "target_name", "choice_count", "text", "raw_tail"],
    )
    write_csv(
        cap_dir / "quest_reward_inventory_observations.csv",
        signal_rows,
        ["time", "packet_number", "name", "identity", "event_kind", "stat_summary", "text", "detail", "frame_prefix"],
    )
    write_csv(
        cap_dir / "quest_reward_events.csv",
        reward_rows,
        ["time", "packet_number", "name", "identity", "event_kind", "stat_summary", "text", "detail", "frame_prefix"],
    )
    write_csv(
        cap_dir / "quest_action_observations.csv",
        action_rows,
        ["time", "packet_number", "name", "sender", "target", "target_identity", "target_name", "action", "action_guess", "tail_hex"],
    )
    (cap_dir / "quest_batch_observation_export.md").write_text(
        build_markdown(cap_dir, player_identity, quest_rows, dialogue_rows, signal_rows, reward_rows, action_rows, excluded_rows),
        encoding="utf-8",
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture_dir", type=Path)
    args = parser.parse_args()
    export(args.capture_dir)


if __name__ == "__main__":
    main()
