#!/usr/bin/env python3
"""Promote reviewed formula packet observations into canonical repository data."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import tempfile
from pathlib import Path
from typing import Any, Iterator


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = REPO_ROOT / "docs/generated/enemy_combat_setup_formula_dataset.json"
DEFAULT_OUTPUT = (
    REPO_ROOT
    / "docs/accepted/combat/enemy_combat_formula_packet_evidence.json"
)
TEMPLE_LOADOUT_CATALOG = (
    REPO_ROOT
    / "AORebirth/Server/ZoneEngine/Core/Playfields/"
    "CapturedTempleOfThreeWindsOrdinaryCombatLoadoutCatalog.g.cs"
)
PACKET_FIELDS = {
    "packetId",
    "captureSession",
    "timestampUtc",
    "direction",
    "sequence",
    "messageType",
    "bodyHex",
    "unknown1",
    "unknown2",
    "unknown3",
    "unknown4",
    "unknown5",
}


def walk(value: Any) -> Iterator[dict[str, Any]]:
    if isinstance(value, dict):
        if PACKET_FIELDS <= set(value):
            yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)


def build_document(source: Path) -> dict[str, Any]:
    payload = source.read_bytes()
    dataset = json.loads(payload.decode("utf-8"))
    packets: dict[tuple[str, int], dict[str, Any]] = {}
    for row in walk(dataset):
        packet = {field: row[field] for field in sorted(PACKET_FIELDS)}
        body = bytes.fromhex(packet["bodyHex"])
        packet["bodySha256"] = hashlib.sha256(body).hexdigest()
        key = (packet["captureSession"], packet["sequence"])
        previous = packets.get(key)
        if previous is not None and previous != packet:
            raise ValueError(f"conflicting reviewed formula packet: {key}")
        packets[key] = packet
    if not packets:
        raise ValueError("reviewed formula dataset contains no promotable packets")
    temple_payload = TEMPLE_LOADOUT_CATALOG.read_bytes()
    temple_text = temple_payload.decode("utf-8")
    temple_pattern = re.compile(
        r"\{ unchecked\(\(int\)0x([0-9A-Fa-f]{8})u\), new Entry\("
        r"(\d+), (\d+), (\d+), (\d+), (\d+), \"([^\"]+)\"\) \}"
    )
    temple_loadouts = []
    for match in temple_pattern.finditer(temple_text):
        capture_match = re.search(r"\b(20\d{6}-\d{6})\b", match.group(7))
        if capture_match is None:
            raise ValueError("Temple accepted loadout lacks capture provenance")
        temple_loadouts.append(
            {
                "sourceIdentity": int(match.group(1), 16),
                "monsterData": int(match.group(2)),
                "level": int(match.group(3)),
                "lowTemplate": int(match.group(4)),
                "highTemplate": int(match.group(5)),
                "quality": int(match.group(6)),
                "slot": 6,
                "capture": capture_match.group(1),
            }
        )
    if not temple_loadouts:
        raise ValueError("Temple accepted loadout catalog contains no entries")
    return {
        "schemaVersion": 1,
        "promotionSource": source.relative_to(REPO_ROOT).as_posix(),
        "promotionSourceSha256": hashlib.sha256(payload).hexdigest(),
        "packets": [packets[key] for key in sorted(packets)],
        "templeLoadoutPromotionSource": TEMPLE_LOADOUT_CATALOG.relative_to(
            REPO_ROOT
        ).as_posix(),
        "templeLoadoutPromotionSourceSha256": hashlib.sha256(
            temple_payload
        ).hexdigest(),
        "templeActiveLoadouts": sorted(
            temple_loadouts, key=lambda row: row["sourceIdentity"]
        ),
    }


def render(document: dict[str, Any]) -> bytes:
    return (json.dumps(document, indent=2, sort_keys=True) + "\n").encode("utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    arguments = parser.parse_args()
    source = arguments.source.resolve(strict=True)
    output = arguments.output.resolve()
    source.relative_to(REPO_ROOT)
    output.relative_to(REPO_ROOT)
    document = build_document(source)
    rendered = render(document)
    if arguments.check:
        if not output.is_file() or output.read_bytes().replace(b"\r\n", b"\n") != rendered:
            raise SystemExit("ERROR: accepted formula packet evidence is stale")
        print("accepted formula packet evidence PASS")
        return 0

    output.parent.mkdir(parents=True, exist_ok=True)
    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="wb",
            prefix=f".{output.name}.",
            suffix=".tmp",
            dir=output.parent,
            delete=False,
        ) as handle:
            temporary_path = Path(handle.name)
            handle.write(rendered)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary_path, output)
        temporary_path = None
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)
    print(
        "accepted formula packet evidence promoted "
        f"packets={len(document['packets'])}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
