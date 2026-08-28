#!/usr/bin/env python3
"""Stream a completed identity-bridge JSONL into an identity linkage audit."""

from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path


def identity_key(record: dict) -> tuple[int, int] | None:
    identity_type = record.get("runtime_identity_type")
    identity_instance = record.get("runtime_identity_instance")
    if isinstance(identity_type, int) and isinstance(identity_instance, int):
        return identity_type, identity_instance
    return None


def audit(path: Path) -> dict:
    snapshots: dict[tuple[int, int], list[dict]] = defaultdict(list)
    packets: dict[str, dict[tuple[int, int], list[dict]]] = {
        "scfu": defaultdict(list),
        "stat": defaultdict(list),
    }
    epochs = []
    with path.open("r", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            if not line.strip():
                continue
            record = json.loads(line)
            kind = record.get("record_type")
            if kind == "zone_epoch":
                epochs.append(record)
            elif kind == "npc_snapshot":
                key = identity_key(record)
                if key is not None:
                    snapshots[key].append(record)
            elif kind in {"packet_scfu", "packet_stat"}:
                key = identity_key(record)
                if key is not None:
                    packets["scfu" if kind == "packet_scfu" else "stat"][key].append(record)

    def classify(key: tuple[int, int], packet_kind: str) -> str:
        identity_packets = packets[packet_kind].get(key, [])
        if not identity_packets:
            return "packet-not-received"
        if all(
            str(packet.get("decode_error", "")).strip()
            or packet.get("decode_fully_consumed") is not True
            for packet in identity_packets
        ):
            return "packet-received-decode-failed"
        eligible = [
            packet
            for packet in identity_packets
            if not str(packet.get("decode_error", "")).strip()
            and packet.get("decode_fully_consumed") is True
        ]
        if all(packet.get("zone_epoch_id") is None for packet in eligible):
            return "packet-received-outside-epoch"
        identity_snapshots = snapshots[key]
        linked = {
            (
                str(reference.get("kind", "")),
                str(reference.get("direction", "")),
                int(reference.get("sequence", -1)),
                int(reference.get("global_ordinal", -1)),
            )
            for snapshot in identity_snapshots
            for reference in snapshot.get("packet_provenance", [])
            if isinstance(reference, dict)
        }
        if any(
            (
                packet_kind,
                str(packet.get("direction", "")),
                int(packet.get("sequence", -1)),
                int(packet.get("global_ordinal", -1)),
            )
            in linked
            for packet in eligible
        ):
            return "packet-received-decoded-linked"
        ordinals = [int(snapshot["observation_global_ordinal"]) for snapshot in identity_snapshots]
        packet_ordinals = [int(packet["global_ordinal"]) for packet in eligible]
        if max(packet_ordinals) < min(ordinals):
            return "packet-received-before-snapshot"
        if min(packet_ordinals) > max(ordinals):
            return "packet-received-after-snapshot"
        return "packet-received-decoded-unlinked"

    identities = []
    for key in sorted(snapshots):
        identity_snapshots = snapshots[key]
        identities.append(
            {
                "runtime_identity_type": key[0],
                "runtime_identity_instance": key[1],
                "runtime_identity": f"({key[0]}:{key[1]:X})",
                "snapshots": len(identity_snapshots),
                "first_observation_global_ordinal": min(
                    int(item["observation_global_ordinal"]) for item in identity_snapshots
                ),
                "last_observation_global_ordinal": max(
                    int(item["observation_global_ordinal"]) for item in identity_snapshots
                ),
                "scfu_status": classify(key, "scfu"),
                "stat_status": classify(key, "stat"),
            }
        )
    snapshot_keys = set(snapshots)
    return {
        "artifact": str(path),
        "epochs": len(epochs),
        "snapshots": sum(len(values) for values in snapshots.values()),
        "unique_runtime_npc_identities": len(snapshots),
        "identities": identities,
        "unmatched_raw_scfu_identities": [
            f"({key[0]}:{key[1]:X})" for key in sorted(set(packets["scfu"]) - snapshot_keys)
        ],
        "unmatched_raw_stat_identities": [
            f"({key[0]}:{key[1]:X})" for key in sorted(set(packets["stat"]) - snapshot_keys)
        ],
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("live_jsonl", type=Path)
    args = parser.parse_args(argv)
    if not args.live_jsonl.is_file():
        parser.error(f"not a file: {args.live_jsonl}")
    print(json.dumps(audit(args.live_jsonl.resolve()), indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
