#!/usr/bin/env python3
"""Focused regression tests for the corrected Arete movement audit."""

from __future__ import annotations

import unittest
from datetime import datetime, timedelta, timezone

import audit_movement_promotion_candidates as audit


NOW = datetime(2026, 7, 22, 13, 0, tzinfo=timezone.utc)
NPC = "SimpleChar:00000001"
PLAYER = "SimpleChar:00000002"


def metadata(
    *,
    captured_utc: datetime = NOW,
    generation: int = 1,
    identity: str = NPC,
    template: int = 17657,
) -> audit.Metadata:
    return audit.Metadata(
        captured_utc=captured_utc,
        identity=identity,
        generation=generation,
        name="Test NPC",
        playfield=1044525,
        family=25,
        template=template,
        level=5,
        character_info_type="NPCInfo",
        position=audit.Point(0, 0, 0),
        decode_complete=True,
    )


def movement(
    *,
    ordinal: int = 1,
    captured_utc: datetime = NOW,
    start: audit.Point = audit.Point(0, 0, 0),
    end: audit.Point = audit.Point(5, 0, 0),
) -> audit.MovementRow:
    return audit.MovementRow(
        ordinal=ordinal,
        captured_utc=captured_utc,
        sequence=ordinal,
        source_identity=NPC,
        source_name="Test NPC",
        target_identity=None,
        start=start,
        end=end,
        path_count=2,
    )


class CorrectedMovementAuditTests(unittest.TestCase):
    def test_complete_capture_resolves_movement_preceding_scfu(self) -> None:
        future = metadata(captured_utc=NOW + timedelta(seconds=10))
        resolved, resolution, reason = audit.resolve_metadata(
            {NPC: [future]},
            NPC,
            1,
            NOW,
        )
        self.assertIs(resolved, future)
        self.assertEqual(resolution, "later_scfu_same_generation")
        self.assertIsNone(reason)

    def test_reused_identity_conflict_is_not_guessed(self) -> None:
        first = metadata(generation=1, template=17657)
        second = metadata(
            captured_utc=NOW + timedelta(minutes=1),
            generation=2,
            template=297023,
        )
        resolved, resolution, reason = audit.resolve_metadata(
            {NPC: [first, second]},
            NPC,
            0,
            NOW - timedelta(seconds=1),
        )
        self.assertIsNone(resolved)
        self.assertEqual(resolution, "conflict")
        self.assertEqual(reason, "metadata_conflict_across_reused_identity")

    def test_cross_packet_destination_gap_is_not_teleport(self) -> None:
        first = movement(end=audit.Point(100, 0, 0))
        second = movement(
            ordinal=2,
            captured_utc=NOW + timedelta(milliseconds=100),
            start=audit.Point(1, 0, 0),
            end=audit.Point(2, 0, 0),
        )
        for row in (first, second):
            behavior, reasons, influences = audit.classify_behavior(
                row, metadata(), {}, {}, {}, set(), {}
            )
            disposition, _, _ = audit.score_observation(
                metadata(), behavior, reasons, influences, row.path_count
            )
            self.assertEqual(behavior, "patrol")
            self.assertEqual(disposition, "Promotable")
            self.assertNotIn("explicit_setpos_teleport", reasons)

    def test_explicit_setpos_remains_exact_teleport_rejection(self) -> None:
        row = movement()
        controls = {
            NPC: [audit.TimedControl(NOW, "setpos", None)]
        }
        behavior, reasons, influences = audit.classify_behavior(
            row, metadata(), controls, {}, {}, set(), {}
        )
        disposition, _, decision_reasons = audit.score_observation(
            metadata(), behavior, reasons, influences, row.path_count
        )
        self.assertEqual(disposition, "Rejected")
        self.assertIn("explicit_setpos_teleport", decision_reasons)

    def test_single_open_patrol_observation_is_promotable(self) -> None:
        row = movement()
        behavior, reasons, influences = audit.classify_behavior(
            row, metadata(), {}, {}, {}, set(), {}
        )
        disposition, confidence, _ = audit.score_observation(
            metadata(), behavior, reasons, influences, row.path_count
        )
        self.assertEqual(behavior, "patrol")
        self.assertEqual(disposition, "Promotable")
        self.assertGreaterEqual(confidence, 85)

    def test_grouping_does_not_contaminate_clean_observation(self) -> None:
        clean_row = movement()
        rejected_row = movement(
            ordinal=2,
            captured_utc=NOW + timedelta(seconds=10),
        )
        generation_index = {
            NPC: ([NOW - timedelta(seconds=1)], [1])
        }
        metadata_index = {NPC: [metadata()]}
        controls = {
            NPC: [
                audit.TimedControl(
                    rejected_row.captured_utc,
                    "stop",
                    None,
                )
            ]
        }
        clean = audit.build_observation(
            1,
            clean_row,
            generation_index,
            metadata_index,
            controls,
            {},
            {},
            set(),
            {},
        )
        rejected = audit.build_observation(
            2,
            rejected_row,
            generation_index,
            metadata_index,
            controls,
            {},
            {},
            set(),
            {},
        )
        groups = audit.group_routes([clean, rejected])
        self.assertEqual(len(groups), 2)
        self.assertEqual(clean.disposition, "Promotable")
        self.assertEqual(rejected.disposition, "Rejected")

    def test_player_combat_path_is_preserved_as_chase(self) -> None:
        row = movement(end=audit.Point(8, 0, 0))
        interval = audit.CombatInterval(
            NOW - timedelta(seconds=1),
            NOW + timedelta(seconds=1),
            {PLAYER},
            True,
        )
        positions = {
            PLAYER: ([NOW], [audit.Point(10, 0, 0)])
        }
        behavior, reasons, influences = audit.classify_behavior(
            row,
            metadata(),
            {},
            {NPC: [interval]},
            positions,
            {PLAYER},
            {},
        )
        disposition, _, decision_reasons = audit.score_observation(
            metadata(), behavior, reasons, influences, row.path_count
        )
        self.assertEqual(behavior, "chase")
        self.assertEqual(disposition, "Promotable")
        self.assertIn("player", influences)
        self.assertIn(
            "player_influence_preserved_for_behavior", decision_reasons
        )

    def test_six_behavior_partitions_reconcile_without_overlap(self) -> None:
        row = movement()
        observations = [
            audit.Observation(
                f"m{index:05d}",
                row,
                1,
                metadata(),
                "preceding_scfu_same_generation",
                behavior,
                "Promotable",
                95,
                ("complete_decoded_path",),
                (),
                audit.route_signature(row.start, row.end),
                5.0,
            )
            for index, behavior in enumerate(audit.BEHAVIORS, start=1)
        ]
        partitions = audit.partition_observations(observations)
        self.assertEqual(set(partitions), set(audit.BEHAVIORS))
        self.assertTrue(all(len(values) == 1 for values in partitions.values()))
        self.assertEqual(sum(map(len, partitions.values())), len(observations))

    def test_runtime_export_deduplicates_exact_equivalents_and_excludes_scripted(
        self,
    ) -> None:
        row = movement()
        common = (
            row,
            1,
            metadata(),
            "preceding_scfu_same_generation",
        )
        observations = [
            audit.Observation(
                "m00001",
                *common,
                "patrol",
                "Promotable",
                95,
                ("complete_decoded_path",),
                (),
                audit.route_signature(row.start, row.end),
                5.0,
            ),
            audit.Observation(
                "m00002",
                *common,
                "patrol",
                "Promotable",
                95,
                ("complete_decoded_path",),
                (),
                audit.route_signature(row.start, row.end),
                5.0,
            ),
            audit.Observation(
                "m00003",
                *common,
                "scripted",
                "Promotable",
                95,
                ("complete_decoded_path",),
                (),
                audit.route_signature(row.start, row.end),
                5.0,
            ),
        ]

        rows, source_count = audit.build_runtime_rows(observations)

        self.assertEqual(2, source_count)
        self.assertEqual(1, len(rows["patrol"]))
        self.assertEqual("2", rows["patrol"][0]["EquivalentObservationCount"])
        self.assertNotIn("scripted", rows)


if __name__ == "__main__":
    unittest.main()
