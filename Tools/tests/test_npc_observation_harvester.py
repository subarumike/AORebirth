from __future__ import annotations

import csv
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = REPOSITORY_ROOT / "Tools" / "npc_observation_harvester.py"
SPEC = importlib.util.spec_from_file_location("npc_observation_harvester", MODULE_PATH)
harvester = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
sys.modules[SPEC.name] = harvester
SPEC.loader.exec_module(harvester)


BOREALIS = (
    REPOSITORY_ROOT
    / "Captures"
    / "Borealis Backyard 2 [PF 3081] - 20260826-222425"
)


def synthetic_observation(
    identity: str = "(SimpleChar:00000001)",
    position: tuple[float, float, float] = (1.0, 2.0, 3.0),
):
    return harvester.NpcObservation(
        observation_id="20260101-000000|" + identity,
        capture_id="20260101-000000",
        capture_path="Captures/Test [PF 1] - 20260101-000000",
        identity=identity,
        resource_playfield_id=1,
        runtime_playfield_id=1001,
        name="Friendly Test NPC",
        position=position,
    )


def official(record_id: str, position=(1.0, 2.0, 3.0)):
    return {
        "OfficialSpawnRecordId": record_id,
        "PlayfieldId": 1,
        "PositionX": position[0],
        "PositionY": position[1],
        "PositionZ": position[2],
    }


class NpcObservationHarvesterTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.record = harvester.CaptureRecord(
            capture_id="20260826-222425",
            path=BOREALIS,
            accepted=True,
            inventory_path="Captures/Borealis Backyard 2 [PF 3081] - 20260826-222425",
            resource_playfield_id=3081,
            has_raw=True,
        )
        observations, metrics = harvester.harvest_observations([cls.record], REPOSITORY_ROOT)
        cls.observations = observations
        cls.metrics = metrics
        cls.guide = next(row for row in observations if row.name == "Guide")
        cls.guard = next(row for row in observations if row.name == "Guard")

    def test_01_sentinel_rejection(self) -> None:
        field = harvester.evidence(
            harvester.UNSET_SENTINEL,
            "client-state-observed",
            {"artifact": "test"},
        )
        self.assertEqual(field["evidenceClassification"], "sentinel/default")
        self.assertIsNone(field["value"])
        observation = synthetic_observation()
        observation.fields["catMesh"] = field
        rows = harvester.promotion_candidates(
            [observation],
            [{"observationId": observation.observation_id, "status": "unmatched", "candidateOfficialSpawnRecordIds": []}],
        )
        self.assertNotIn("catMesh", rows[0]["authoritativeFields"])

    def test_02_legitimate_zero_remains_zero(self) -> None:
        field = harvester.evidence(0, "packet-observed", {"artifact": "test"})
        self.assertEqual(field["status"], "captured")
        self.assertEqual(field["value"], 0)

    def test_03_absent_stat_remains_absent(self) -> None:
        observation = synthetic_observation()
        coverage = harvester.coverage_for_observation(observation, "unmatched")
        self.assertEqual(coverage["clientVisibleStats"], "not observed")

    def test_04_texture_arrays_survive_harvest_and_promotion(self) -> None:
        self.assertEqual(
            [row["id"] for row in self.guide.fields["textures"]["value"]],
            [0, 42239, 42260, 42240, 42261],
        )
        candidate = harvester.promotion_candidates(
            [self.guide],
            [{"observationId": self.guide.observation_id, "status": "unmatched", "candidateOfficialSpawnRecordIds": []}],
        )[0]
        self.assertEqual(candidate["authoritativeFields"]["textures"], self.guide.fields["textures"]["value"])

    def test_05_mesh_arrays_survive_harvest_and_promotion(self) -> None:
        self.assertEqual(self.guide.fields["meshes"]["value"], [{"place": 0, "id": 40635, "unknown": 0, "slot": 4}])
        candidate = harvester.promotion_candidates(
            [self.guide],
            [{"observationId": self.guide.observation_id, "status": "unmatched", "candidateOfficialSpawnRecordIds": []}],
        )[0]
        self.assertEqual(candidate["authoritativeFields"]["meshes"], self.guide.fields["meshes"]["value"])

    def test_06_ordinary_stat_offline_replay_fixture(self) -> None:
        rows = harvester.read_csv(BOREALIS / "npc-stat-observations.csv")
        self.assertEqual(len(rows), 4)
        self.assertTrue(all(row["DecodeStatus"] == "decoded_complete" for row in rows))
        self.assertTrue(all(row["StatId"] == "521" and row["Value"] == "0" for row in rows))

    def test_07_offline_live_projection_parity(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with (root / "npc-stat-observations.csv").open("w", encoding="utf-8", newline="") as stream:
                writer = csv.DictWriter(stream, fieldnames=["DecodeStatus", "Identity", "StatId", "Value", "Direction", "Sequence", "GlobalOrdinal", "CapturedUtc"])
                writer.writeheader()
                writer.writerow({"DecodeStatus": "decoded_complete", "Identity": "npc", "StatId": "1", "Value": "0"})
            with (root / "enemy-stat-updates.csv").open("w", encoding="utf-8", newline="") as stream:
                writer = csv.DictWriter(stream, fieldnames=["Identity", "StatId", "Value"])
                writer.writeheader()
                writer.writerow({"Identity": "npc", "StatId": "1", "Value": "0"})
            record = harvester.CaptureRecord("20260101-000000", root, True, "capture", 1, True)
            observation = synthetic_observation("npc")
            metrics = harvester.attach_stats(record, {observation.observation_id: observation}, {1: "Health"})
            self.assertEqual(metrics["parity"], 1)

    def test_08_friendly_npc_uses_generic_path(self) -> None:
        candidate = harvester.promotion_candidates(
            [synthetic_observation()],
            [{"observationId": synthetic_observation().observation_id, "status": "unmatched", "candidateOfficialSpawnRecordIds": []}],
        )[0]
        self.assertEqual(candidate["npcCategory"], "npc")
        self.assertEqual(candidate["hostility"]["status"], "not observed")

    def test_09_exact_placement_match(self) -> None:
        observation = synthetic_observation()
        rows, links = harvester.reconcile([observation], [official("official-1")])
        self.assertEqual(rows[0]["status"], "unique")
        self.assertEqual(links["official-1"], [observation.observation_id])

    def test_10_ambiguous_placement_remains_ambiguous(self) -> None:
        rows, links = harvester.reconcile(
            [synthetic_observation()], [official("official-1"), official("official-2")]
        )
        self.assertEqual(rows[0]["status"], "ambiguous")
        self.assertEqual(links, {})

    def test_11_conflicting_observations_remain_conflict(self) -> None:
        fields = {}
        harvester.merge_field(fields, "headMesh", harvester.evidence(1, "packet-observed", {"sequence": 1}))
        harvester.merge_field(fields, "headMesh", harvester.evidence(2, "packet-observed", {"sequence": 2}))
        self.assertEqual(fields["headMesh"]["status"], "conflict")
        self.assertEqual(fields["headMesh"]["observedValues"], [1, 2])

    def test_12_capture_integrity_does_not_imply_stat_coverage(self) -> None:
        self.assertGreater(self.metrics["raw"], 0)
        self.assertTrue(all(not observation.stat_observations for observation in self.observations))
        self.assertTrue(all(harvester.coverage_for_observation(observation, "unmatched")["clientVisibleStats"] == "not observed" for observation in self.observations))

    def test_13_repository_capture_root_discovery(self) -> None:
        discovered = harvester.discover_capture_directories(REPOSITORY_ROOT)
        self.assertIn(BOREALIS.resolve(), discovered)

    def test_14_historical_capture_roots_remain_supported(self) -> None:
        records = harvester.inventory_records(REPOSITORY_ROOT)
        self.assertTrue(any(record.inventory_path.startswith("tools-temp/") for record in records))

    def test_15_inventory_read_does_not_prune(self) -> None:
        before = BOREALIS.stat().st_mtime_ns
        harvester.inventory_records(REPOSITORY_ROOT, [BOREALIS])
        self.assertTrue(BOREALIS.is_dir())
        self.assertEqual(before, BOREALIS.stat().st_mtime_ns)

    def test_16_acghash_cannot_reconcile_identity(self) -> None:
        rows, _ = harvester.reconcile([synthetic_observation()], [official("official-1")])
        self.assertIs(rows[0]["acgHashUsedAsIdentity"], False)
        self.assertEqual(rows[0]["matchBasis"], "playfield+exact-float32-coordinate")

    def test_17_deterministic_serialization(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            value = {"b": [2, 1], "a": 1}
            harvester.atomic_json(output / "value.json", value)
            first = (output / "value.json").read_bytes()
            harvester.atomic_json(output / "value.json", value)
            self.assertEqual(first, (output / "value.json").read_bytes())

    def test_18_borealis_guide_regression(self) -> None:
        self.assertEqual(self.guide.fields["headMesh"]["value"], 40635)
        self.assertEqual([row["id"] for row in self.guide.fields["textures"]["value"]][1:], [42239, 42260, 42240, 42261])
        self.assertEqual(self.guide.fields["catMesh"]["status"], "not observed")

    def test_19_borealis_guard_regression(self) -> None:
        self.assertEqual(self.guard.fields["headMesh"]["value"], 40111)
        self.assertEqual([row["id"] for row in self.guard.fields["textures"]["value"]][1:], [30848, 42260, 30831, 42261])
        self.assertEqual(self.guard.fields["catMesh"]["status"], "not observed")

    def test_20_official_corpus_count_and_coverage_states(self) -> None:
        placements = harvester.load_official_placements(REPOSITORY_ROOT)
        self.assertEqual(len(placements), 32805)
        self.assertEqual(set(harvester.FIELD_CATEGORIES), {
            "identity", "placement", "appearance", "clientVisibleStats", "combat",
            "movement", "lifecycle", "corpseDeath", "loot", "respawn",
        })


if __name__ == "__main__":
    unittest.main()
