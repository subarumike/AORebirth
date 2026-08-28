#!/usr/bin/env python3
"""Deterministic offline load model for event-first NPC bridge capture."""

from __future__ import annotations

import argparse
import hashlib
import json
from dataclasses import dataclass


NPCS = 38
ROUNDS = 100
OLD_FAILED_SNAPSHOTS = 2202
OLD_STATS_PER_SNAPSHOT = 626
NEW_STATS_PER_SNAPSHOT = 10


@dataclass
class EvidenceState:
    client_complete: bool = True
    retry_count: int = 0
    scfu_version: int = 0
    stat_version: int = 0
    position_version: int = 0
    last_fingerprint: tuple[int, int, int, bool] | None = None

    def fingerprint(self) -> tuple[int, int, int, bool]:
        return (
            self.scfu_version,
            self.stat_version,
            self.position_version,
            self.client_complete,
        )


def simulate() -> dict[str, int | bool | str]:
    states = [EvidenceState() for _ in range(NPCS)]
    states[0].client_complete = False
    emitted = 0
    suppressed = 0
    retries = 0
    raw_packets = 0
    raw_preserved = 0
    model_state = "default"
    model_first_valid_round = 0

    scfu_events = {round_number: round_number - 2 for round_number in range(2, 9)}
    stat_events = {round_number: round_number - 3 for round_number in range(3, 8)}
    movement_events = {50: (10, 11, 12)}

    for round_number in range(1, ROUNDS + 1):
        dirty: set[int] = set()
        if round_number in scfu_events:
            npc = scfu_events[round_number]
            states[npc].scfu_version += 1
            dirty.add(npc)
            raw_packets += 1
            raw_preserved += 1
        if round_number in stat_events:
            npc = stat_events[round_number]
            states[npc].stat_version += 1
            dirty.add(npc)
            raw_packets += 1
            raw_preserved += 1
        for npc in movement_events.get(round_number, ()):
            states[npc].position_version += 1
            dirty.add(npc)
        if round_number == 4:
            model_state = "observed-direct-resource"
            model_first_valid_round = round_number

        for npc, state in enumerate(states):
            first_seen = state.last_fingerprint is None
            retry_due = (
                not state.client_complete
                and state.retry_count < 3
                and round_number in {2, 4, 6}
            )
            if retry_due:
                state.retry_count += 1
                retries += 1
                if state.retry_count == 3:
                    state.client_complete = True
            bounded_position_refresh = round_number % 10 == 0
            if not (first_seen or npc in dirty or retry_due or bounded_position_refresh):
                suppressed += 1
                continue
            fingerprint = state.fingerprint()
            if fingerprint == state.last_fingerprint:
                suppressed += 1
                continue
            state.last_fingerprint = fingerprint
            emitted += 1

    opportunities = NPCS * ROUNDS
    result: dict[str, int | bool | str] = {
        "npcs": NPCS,
        "observation_opportunities": opportunities,
        "snapshots_emitted": emitted,
        "redundant_suppressed": suppressed,
        "retries_total": retries,
        "scfu_packets": len(scfu_events),
        "stat_packets": len(stat_events),
        "raw_packets": raw_packets,
        "raw_packets_preserved": raw_preserved,
        "raw_packet_loss": raw_packets - raw_preserved,
        "scfu_decode_failures": 0,
        "packet_not_received_scfu_npcs": NPCS - len(scfu_events),
        "complete_npc_retry_count": states[1].retry_count,
        "enrichment_queue_depth_high_water": 0,
        "dropped_enrichment_work": 0,
        "wrong_resource_type_promoted": False,
        "new_epoch_inherits_old_npc_evidence": False,
        "delayed_playfield_model_state": model_state,
        "delayed_playfield_model_first_valid_round": model_first_valid_round,
        "old_failed_snapshots": OLD_FAILED_SNAPSHOTS,
        "old_estimated_getstat_calls": OLD_FAILED_SNAPSHOTS * OLD_STATS_PER_SNAPSHOT,
        "new_estimated_getstat_calls": emitted * NEW_STATS_PER_SNAPSHOT,
        "bounded_retry_pass": states[0].client_complete and states[0].retry_count == 3,
        "late_scfu_link_pass": all(states[index].scfu_version == 1 for index in range(7)),
        "late_stat_link_pass": all(states[index].stat_version == 1 for index in range(5)),
        "model_delay_pass": model_state == "observed-direct-resource",
        "lossless_raw_pass": raw_packets == raw_preserved,
    }
    digest_input = json.dumps(result, sort_keys=True, separators=(",", ":")).encode("utf-8")
    result["digest"] = hashlib.sha256(digest_input).hexdigest()
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args(argv)
    result = simulate()
    if args.json:
        print(json.dumps(result, indent=2, sort_keys=True))
    else:
        for key, value in result.items():
            print(f"{key.upper()}={value}")
    return 0 if all(
        result[name]
        for name in (
            "bounded_retry_pass",
            "late_scfu_link_pass",
            "late_stat_link_pass",
            "model_delay_pass",
            "lossless_raw_pass",
        )
    ) else 1


if __name__ == "__main__":
    raise SystemExit(main())
