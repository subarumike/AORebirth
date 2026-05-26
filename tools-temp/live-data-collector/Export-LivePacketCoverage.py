#!/usr/bin/env python3
"""Compare decoded live packet families with current CellAO implementation files."""

from __future__ import annotations

import argparse
import csv
import json
from collections import Counter
from pathlib import Path
from typing import Any


DEFAULT_REPO_ROOT = Path(r"C:\Users\Mike\Documents\Cellao-Clean")

HANDLER_ALIASES = {
    "CastNanoSpell": "CastNano",
    "InfoPacket": "CharacterInfoPacket",
    "N3Teleport": "Teleport",
}

PACKET_BUILDER_ALIASES = {
    "SimpleCharFullUpdate": "SimpleCharFullUpdate",
    "Stat": "Stat",
}

IMPORTANT_PACKET_NOTES = {
    "Attack": "Client attack toggle and server attack-state echo.",
    "AttackInfo": "Live combat swing/result packet; useful for damage timing comparison.",
    "HealthDamage": "Live damage feedback packet observed around combat.",
    "CorpseFullUpdate": "Distinct live corpse dynel packet; important for death/loot lifecycle.",
    "GenericCmd": "Use/Get/loot action command; corpse use path depends on this.",
    "ClientMoveItemToInventory": "Loot-window item transfer request.",
    "InventoryUpdate": "Loot-window/container contents packet.",
    "ContainerAddItem": "Inventory add/update packet observed near reward/loot actions.",
    "QuestFullUpdate": "Mission window objective/reward state packet.",
    "Quest": "Quest progress/completion signal paired with QuestFullUpdate.",
    "Feedback": "Client-rendered system feedback trigger.",
    "ChatText": "Visible system/NPC text packet.",
    "KnuBotAnswer": "NPC dialogue answer request.",
    "KnuBotAnswerList": "NPC dialogue option list.",
    "KnuBotAppendText": "NPC dialogue text append.",
    "KnuBotOpenChatWindow": "NPC dialogue window open.",
    "KnuBotCloseChatWindow": "NPC dialogue window close.",
    "SimpleCharFullUpdate": "Main dynamic character/NPC spawn/update packet.",
    "StopFight": "Fight-state clear packet.",
    "Stat": "Live stat layout is identity + unknown byte + count + stat/value pairs.",
}


def read_csv(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def read_capture_meta(capture_dir: Path) -> dict[str, Any]:
    path = capture_dir / "capture_meta.json"
    if not path.exists():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError:
        return {}


def collect_counts(capture_dir: Path) -> dict[str, Counter[str]]:
    counts: dict[str, Counter[str]] = {}
    for direction, filename in (("c2s", "ao_frames.csv"), ("s2c", "s2c_frames.csv")):
        for row in read_csv(capture_dir / filename):
            name = (row.get("name") or "").strip()
            if not name:
                continue
            counts.setdefault(name, Counter())[direction] += 1
    return counts


def classify_capture(capture_dir: Path, counts: dict[str, Counter[str]]) -> dict[str, str]:
    meta = read_capture_meta(capture_dir)
    label = str(meta.get("label") or meta.get("capture_type") or "").lower()
    filter_mode = str(meta.get("filter_mode_used") or "").lower()
    server_host = str(meta.get("server_host") or meta.get("server_ip") or "").lower()
    path_text = str(capture_dir).lower()
    c2s_total = sum(counter["c2s"] for counter in counts.values())
    s2c_total = sum(counter["s2c"] for counter in counts.values())

    if "live-official" in label or "live-official" in filter_mode or "37.18." in server_host:
        source = "official_live"
    elif "private" in label or "private" in path_text or "199.241.136.157" in server_host:
        source = "private_server_199"
    else:
        source = "unknown"

    if s2c_total == 0 and c2s_total > 0:
        authority = "c2s_only_request_flow"
        note = "Use this capture for client request/order evidence only; absence of S2C packets is not evidence that a server packet is missing or unused."
    elif source == "official_live":
        authority = "official_live_full_duplex"
        note = "Use this capture as official live evidence for both client requests and server responses."
    elif source == "private_server_199":
        authority = "private_server_response_flow"
        note = "Use this capture for rich S2C response shape/timing, but keep it labeled as private-server evidence."
    else:
        authority = "unclassified_capture"
        note = "Capture source is not classified; verify server and direction coverage before using it as source-of-truth evidence."

    return {
        "capture_source": source,
        "coverage_authority": authority,
        "authority_note": note,
    }


def implementation_lookup(repo_root: Path) -> tuple[dict[str, Path], dict[str, Path], dict[str, Path]]:
    handlers_root = repo_root / "CellAO" / "Server" / "ZoneEngine" / "Core" / "MessageHandlers"
    packets_root = repo_root / "CellAO" / "Server" / "ZoneEngine" / "Core" / "Packets"
    message_root = (
        repo_root
        / "CellAO"
        / "Libraries"
        / "Source"
        / "AOtomation"
        / "AOtomation.Messaging"
        / "src"
        / "SmokeLounge.AOtomation.Messaging"
        / "Messages"
    )

    handlers: dict[str, Path] = {}
    for path in handlers_root.glob("*MessageHandler.cs"):
        stem = path.stem
        if stem.endswith("MessageHandler"):
            handlers[stem[: -len("MessageHandler")]] = path

    builders = {path.stem: path for path in packets_root.glob("*.cs")}

    message_models: dict[str, Path] = {}
    for path in message_root.rglob("*.cs"):
        stem = path.stem
        if stem.endswith("Message"):
            message_models[stem[: -len("Message")]] = path
        else:
            message_models[stem] = path

    return handlers, builders, message_models


def relative(path: Path | None, repo_root: Path) -> str:
    if not path:
        return ""
    try:
        return str(path.relative_to(repo_root))
    except ValueError:
        return str(path)


def build_rows(capture_dir: Path, repo_root: Path) -> list[dict[str, Any]]:
    counts = collect_counts(capture_dir)
    capture_authority = classify_capture(capture_dir, counts)
    handlers, builders, message_models = implementation_lookup(repo_root)
    rows: list[dict[str, Any]] = []

    for name in sorted(counts):
        c2s = counts[name]["c2s"]
        s2c = counts[name]["s2c"]
        handler_key = HANDLER_ALIASES.get(name, name)
        builder_key = PACKET_BUILDER_ALIASES.get(name, name)
        handler_path = handlers.get(handler_key)
        builder_path = builders.get(builder_key)
        message_model_path = message_models.get(name)
        status = "missing"
        if handler_path and builder_path:
            status = "handler_and_builder"
        elif handler_path:
            status = "handler"
        elif builder_path:
            status = "packet_builder"
        elif message_model_path:
            status = "message_model_only"

        rows.append(
            {
                "packet_name": name,
                "total_count": c2s + s2c,
                "c2s_count": c2s,
                "s2c_count": s2c,
                "status": status,
                "handler_file": relative(handler_path, repo_root),
                "packet_builder_file": relative(builder_path, repo_root),
                "message_model_file": relative(message_model_path, repo_root),
                "notes": IMPORTANT_PACKET_NOTES.get(name, ""),
                "capture_source": capture_authority["capture_source"],
                "coverage_authority": capture_authority["coverage_authority"],
            }
        )

    rows.sort(key=lambda row: (-int(row["total_count"]), row["packet_name"]))
    return rows


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    fieldnames = [
        "packet_name",
        "total_count",
        "c2s_count",
        "s2c_count",
        "status",
        "handler_file",
        "packet_builder_file",
        "message_model_file",
        "notes",
        "capture_source",
        "coverage_authority",
    ]
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def write_markdown(path: Path, rows: list[dict[str, Any]], capture_dir: Path) -> None:
    counts = collect_counts(capture_dir)
    capture_authority = classify_capture(capture_dir, counts)
    missing = [row for row in rows if row["status"] == "missing"]
    partial = [row for row in rows if row["status"] in {"handler", "packet_builder"}]
    message_model_only = [row for row in rows if row["status"] == "message_model_only"]
    implemented = [row for row in rows if row["status"] == "handler_and_builder"]
    important_missing = [row for row in missing + message_model_only if row["packet_name"] in IMPORTANT_PACKET_NOTES]

    lines = [
        "# Live Packet Coverage",
        "",
        f"- Capture: `{capture_dir}`",
        f"- Capture source: **{capture_authority['capture_source']}**",
        f"- Coverage authority: **{capture_authority['coverage_authority']}**",
        f"- Authority note: {capture_authority['authority_note']}",
        f"- Observed packet families: **{len(rows)}**",
        f"- Handler and packet builder: **{len(implemented)}**",
        f"- Partial server implementation: **{len(partial)}**",
        f"- Message model only: **{len(message_model_only)}**",
        f"- No local model/server match: **{len(missing)}**",
        "",
        "## Interpretation Guardrails",
        "",
        "- `official_live` captures are official-client/server evidence, but only for directions that decoded into frames.",
        "- `c2s_only_request_flow` captures prove client request order and payloads; do not use them to mark S2C packet families as absent or unnecessary.",
        "- `private_server_199` captures are useful for rich S2C packet shape and timing, but keep that source label attached when turning observations into CellAO changes.",
        "",
        "## Important Missing Or Externalized Packets",
    ]

    if important_missing:
        for row in important_missing:
            lines.append(
                f"- `{row['packet_name']}` count={row['total_count']} c2s={row['c2s_count']} s2c={row['s2c_count']}: {row['notes']}"
            )
    else:
        lines.append("- None from the currently marked important set.")

    lines.extend(["", "## Partial Matches"])
    for row in partial:
        handler = row["handler_file"] or "-"
        builder = row["packet_builder_file"] or "-"
        model = row["message_model_file"] or "-"
        lines.append(f"- `{row['packet_name']}` status={row['status']} handler={handler} builder={builder} model={model}")

    lines.extend(["", "## Message Model Only"])
    for row in message_model_only:
        model = row["message_model_file"] or "-"
        note = f": {row['notes']}" if row["notes"] else ""
        lines.append(
            f"- `{row['packet_name']}` count={row['total_count']} c2s={row['c2s_count']} s2c={row['s2c_count']} model={model}{note}"
        )

    lines.extend(["", "## Top Observed Packets"])
    for row in rows[:30]:
        lines.append(
            f"- `{row['packet_name']}` total={row['total_count']} c2s={row['c2s_count']} s2c={row['s2c_count']} status={row['status']}"
        )

    lines.extend(["", "## Files", "", "- `live_packet_coverage.csv`", "- `live_packet_coverage.md`"])
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture_dir", type=Path)
    parser.add_argument("--repo-root", type=Path, default=DEFAULT_REPO_ROOT)
    args = parser.parse_args()

    rows = build_rows(args.capture_dir, args.repo_root)
    write_csv(args.capture_dir / "live_packet_coverage.csv", rows)
    write_markdown(args.capture_dir / "live_packet_coverage.md", rows, args.capture_dir)
    missing = sum(1 for row in rows if row["status"] == "missing")
    print(f"packet_families={len(rows)} missing={missing}")
    print(args.capture_dir / "live_packet_coverage.md")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
