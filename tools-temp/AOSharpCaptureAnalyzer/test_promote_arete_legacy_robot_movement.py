import csv
import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).with_name("promote_arete_legacy_robot_movement.py")
SPEC = importlib.util.spec_from_file_location("promote_arete_legacy_robot_movement", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class PromoteAreteLegacyRobotMovementTests(unittest.TestCase):
    def write_source(self, path: Path, rows: list[dict[str, str]]) -> None:
        with path.open("w", encoding="utf-8", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=MODULE.SOURCE_COLUMNS)
            writer.writeheader()
            writer.writerows(rows)

    def row(
        self,
        identity: str,
        captured_utc: str,
        sequence: int,
        destination_x: str,
    ) -> dict[str, str]:
        instance = identity.split(":", 1)[1]
        row = {column: "" for column in MODULE.SOURCE_COLUMNS}
        row.update(
            {
                "CapturedUtc": captured_utc,
                "Direction": "IN",
                "Sequence": str(sequence),
                "MessageType": "FollowTarget",
                "SourceType": "SimpleChar",
                "SourceInstance": instance,
                "SourceIdentity": identity,
                "SourceName": MODULE.NPC_NAME,
                "FollowKind": "NpcPath",
                "CurrentX": "10.1234567",
                "CurrentY": "20.7654321",
                "CurrentZ": "30.25",
                "DestinationX": destination_x,
                "DestinationY": "20.7654321",
                "DestinationZ": "31.25",
                "Animation": "25",
                "Flags": "base_unknown=0;follow_type=1;follow_unknown=25",
                "PathCount": "2",
                "RawParams": "base_unknown=0;follow_type=1;follow_unknown=25;path_count=2;decoded_path_count=2",
            }
        )
        return row

    def complete_identity_rows(self) -> list[dict[str, str]]:
        return [
            self.row(identity, "2026-07-21T19:55:03.4000000Z", index, str(11 + index))
            for index, identity in enumerate(sorted(MODULE.EXPECTED_IDENTITIES), start=1)
        ]

    def test_preserves_exact_geometry_deduplicates_and_does_not_wrap_terminal_row(self) -> None:
        rows = self.complete_identity_rows()
        identity = sorted(MODULE.EXPECTED_IDENTITIES)[0]
        rows[0] = self.row(identity, "2026-07-21T19:55:03.4000000Z", 2, "40.7654321")
        rows.insert(1, self.row(identity, "2026-07-21T19:55:03.4000000Z", 3, "40.7654321"))
        rows.insert(2, self.row(identity, "2026-07-21T19:55:03.6500000Z", 4, "41.7654321"))
        rows.sort(key=lambda row: (row["CapturedUtc"], int(row["Sequence"])))
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source.csv"
            self.write_source(source, rows)
            observations = MODULE.load_observations(source)
            runtime = MODULE.build_runtime_rows(observations)
        selected = [row for row in runtime if row["SourceIdentity"] == identity]
        self.assertEqual(2, len(selected))
        self.assertEqual("2", selected[0]["EquivalentObservationCount"])
        self.assertEqual("10.1234567", selected[0]["StartX"])
        self.assertEqual("40.7654321", selected[0]["EndX"])
        self.assertEqual("0.25", selected[0]["DelayAfterSeconds"])
        self.assertEqual("0", selected[1]["DelayAfterSeconds"])

    def test_rejects_non_patrol_source_instead_of_inventing_behavior(self) -> None:
        rows = self.complete_identity_rows()
        rows[0]["FollowKind"] = "Target"
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "source.csv"
            self.write_source(source, rows)
            with self.assertRaisesRegex(RuntimeError, "unsupported FollowKind"):
                MODULE.load_observations(source)

    def test_rejects_an_identity_outside_the_capture_scoped_spawn_cohort(self) -> None:
        rows = self.complete_identity_rows()
        rows[0]["SourceInstance"] = "DEADBEEF"
        rows[0]["SourceIdentity"] = "SimpleChar:DEADBEEF"
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "source.csv"
            self.write_source(source, rows)
            with self.assertRaisesRegex(RuntimeError, "outside captured robot cohort"):
                MODULE.load_observations(source)

    def test_committed_projection_reconciles_and_each_identity_terminates(self) -> None:
        observations = MODULE.load_observations(MODULE.DEFAULT_INPUT)
        evidence = MODULE.validate_metadata_evidence(
            MODULE.DEFAULT_METADATA_EVIDENCE, observations
        )
        runtime = MODULE.build_runtime_rows(observations)
        self.assertEqual(2612, len(observations))
        self.assertEqual(2531, len(runtime))
        identities = {row["SourceIdentity"] for row in runtime}
        self.assertEqual(10, len(identities))
        self.assertNotIn("SimpleChar:7986653C", identities)
        self.assertEqual(2, len(evidence))
        for identity in identities:
            selected = [row for row in runtime if row["SourceIdentity"] == identity]
            self.assertEqual("0", selected[-1]["DelayAfterSeconds"])


if __name__ == "__main__":
    unittest.main()
