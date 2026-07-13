import argparse
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).resolve().parents[1] / "subway_visibility_diagnostic.py"
SPEC = importlib.util.spec_from_file_location("subway_visibility_diagnostic", MODULE_PATH)
diagnostic = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(diagnostic)


def arguments(**overrides):
    values = {
        "slice": None,
        "first": None,
        "ordinal_range": None,
        "identity_list": None,
        "family": None,
    }
    values.update(overrides)
    return argparse.Namespace(**values)


class SubwayVisibilityDiagnosticTests(unittest.TestCase):
    def setUp(self):
        self.rows = diagnostic.load_manifest()

    def test_manifest_ordinals_and_groups_are_stable(self):
        self.assertEqual(list(range(1, 39)), [row["ordinal"] for row in self.rows])
        self.assertEqual(38, len({row["source_instance"] for row in self.rows}))
        self.assertEqual(29, len(diagnostic.selected_rows(arguments(slice="SUPPORTED_29"), self.rows)[1]))
        self.assertEqual(9, len(diagnostic.selected_rows(arguments(slice="ORDINARY_9"), self.rows)[1]))

    def test_default_none_and_explicit_all_38(self):
        self.assertEqual([], diagnostic.selected_rows(arguments(slice="NONE"), self.rows)[1])
        self.assertEqual(38, len(diagnostic.selected_rows(arguments(slice="ALL_38"), self.rows)[1]))
        with self.assertRaises(ValueError):
            diagnostic.selected_rows(arguments(), self.rows)

    def test_first_n_and_inclusive_ordinal_range_are_deterministic(self):
        first = diagnostic.selected_rows(arguments(first=4), self.rows)[1]
        ranged = diagnostic.selected_rows(arguments(ordinal_range="3-5"), self.rows)[1]
        self.assertEqual([1, 2, 3, 4], [row["ordinal"] for row in first])
        self.assertEqual([3, 4, 5], [row["ordinal"] for row in ranged])

    def test_identity_list_fails_closed_for_unknown_identity(self):
        selected = diagnostic.selected_rows(
            arguments(identity_list="79557C09,SimpleChar:79557C26"), self.rows
        )[1]
        self.assertEqual([1, 2], [row["ordinal"] for row in selected])
        with self.assertRaises(ValueError):
            diagnostic.selected_rows(arguments(identity_list="FFFFFFFF"), self.rows)

    def test_family_selection_is_manifest_ordered(self):
        selected = diagnostic.selected_rows(arguments(family="Mugger"), self.rows)[1]
        self.assertEqual([12, 31, 32, 33, 34], [row["ordinal"] for row in selected])

    def test_manifest_excludes_bosses_and_owned_summons(self):
        classifications = {row["Classification"] for row in self.rows}
        self.assertEqual(
            {"SUPPORTED_FAMILY_RESTORE", "ORDINARY_ENEMY_REGENERATE"}, classifications
        )
        self.assertFalse(any("boss" in row["Name"].casefold() for row in self.rows))

    def test_prepare_isolated_session_and_operator_outcome(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with mock.patch.object(diagnostic, "local_root", return_value=root):
                args = arguments(session_id="pf127-test", slice="NONE")
                self.assertEqual(0, diagnostic.prepare(args))
                session = json.loads((root / "pf127-test" / "session.json").read_text())
                self.assertEqual([], session["selected_identities"])
                active = (root / "active-session.cfg").read_text()
                self.assertIn("selected_source_instances=\n", active)
                record_args = argparse.Namespace(
                    session_id="pf127-test",
                    outcome="FAIL_CLIENT_CRASH",
                    time_to_failure=15.0,
                    login_completed="YES",
                    world_rendered="YES",
                    movement_possible="NO",
                    last_visible_log_timestamp=None,
                    note="operator observed",
                )
                diagnostic.record(record_args)
                outcome = json.loads((root / "pf127-test" / "outcome.json").read_text())
                self.assertEqual("operator_observed", outcome["client_state_source"])

    def test_analyzer_does_not_claim_causality_from_one_failed_session(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            target = root / "single-failure"
            target.mkdir(parents=True)
            session = {
                "session_id": "single-failure",
                "slice": "IDENTITY_LIST",
                "expected_quarantined_row_count": 1,
                "selected_identities": ["SimpleChar:79557C09"],
            }
            outcome = {"outcome": "FAIL_CLIENT_CRASH"}
            summary = {
                "snapshot_completed": True,
                "total_npcs_sent": 222,
                "total_serialized_bytes": 100000,
            }
            (target / "session.json").write_text(json.dumps(session))
            (target / "outcome.json").write_text(json.dumps(outcome))
            (target / "snapshot-summary.jsonl").write_text(json.dumps(summary) + "\n")
            with mock.patch.object(diagnostic, "local_root", return_value=root):
                diagnostic.analyze(argparse.Namespace(session_id="single-failure"))
            report = json.loads((target / "analysis.json").read_text())
            self.assertNotIn("FAILURE_FOLLOWS_SPECIFIC_IDENTITY", report["findings"])
            self.assertIn("not a PROVEN_CAUSAL_ENEMY", report["causality_warning"])

    def test_runtime_summary_accepts_dotnet_utf8_bom(self):
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary)
            summary = {"snapshot_completed": True, "total_npcs_sent": 221}
            (target / "snapshot-summary.jsonl").write_text(
                json.dumps(summary) + "\n", encoding="utf-8-sig"
            )
            self.assertEqual(summary, diagnostic.load_last_summary(target))


if __name__ == "__main__":
    unittest.main()
