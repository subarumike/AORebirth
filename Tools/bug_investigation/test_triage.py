import json
from pathlib import Path
import sqlite3
import unittest
from unittest.mock import patch

import triage
import validation
from worker import initialize


PATH = "AORebirth/Server/ZoneEngine_New/Example.cs"
READ = {"path": PATH, "first": 1, "last": 30, "private": False, "tool": "read"}


def diagnosis(classification, *, missing=()):
    return {
        "classification": classification, "defect_identified": classification == "DIRECT_FIX",
        "suspected_root_cause": "Observed transition", "expected_behavior": "Persist state",
        "trace": [{"path": PATH, "line": line, "function": name, "stage": stage,
                   "finding": "Traced operation"}
                  for line, name, stage in [(5, "Handle", "entry"), (20, "Save", "runtime")]],
        "reproduction": "Trace a save followed by a read", "verification": "Existing save test",
        "evidence": [], "missing_source": list(missing), "repair_files": [PATH],
        "repair_ready": classification == "DIRECT_FIX", "next_action": "Check the saved state",
    }


class InvestigationTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(":memory:")
        self.addCleanup(self.db.close)
        initialize(self.db)
        self.db.execute("INSERT INTO jobs (id, fingerprint, payload, state) VALUES (1, 'r1', '{}', 'pending')")
        self.key = (1, "r1", "source")

    def test_unread_source_never_becomes_a_human_escalation(self):
        result, _, _ = triage.verified_result(diagnosis("HUMAN_REQUIRED"), [], [])
        self.assertEqual(result["classification"], triage.INCOMPLETE)
        self.assertTrue(result["missing_source"])

    def test_siblings_and_requeue_are_independent(self):
        calls = []

        def invoke(schema, prompt, context, **options):
            if schema is triage.SPLIT_SCHEMA:
                return {"symptoms": [{"title": title, "scope": title} for title in ("Save", "Display")]}
            number = context["symptom"]["id"]
            calls.append(number)
            if number == 1 and calls.count(1) == 1:
                return diagnosis(triage.INCOMPLETE, missing=["Inspect the DAO"]), [READ]
            return diagnosis("DIRECT_FIX" if number == 1 else "CAPTURE_REQUIRED"), [READ]

        first = triage.investigate(self.db, self.key, {"id": 1}, None, invoke)
        self.assertEqual(calls, [1, 2, 1])
        self.assertEqual(first["classification"], "MIXED")
        self.assertTrue(first["symptoms"][0]["repair_ready"])
        self.assertFalse(first["symptoms"][1]["repair_ready"])
        self.assertTrue(triage.requeue(self.db, 1, 2))
        pending = json.loads(self.db.execute("SELECT result FROM jobs WHERE id = 1").fetchone()[0])
        self.assertTrue(pending["symptoms"][0]["repair_ready"])
        self.assertEqual(pending["symptoms"][1]["classification"], triage.INCOMPLETE)
        self.assertEqual(triage.investigate(self.db, self.key, {"id": 1}, None, invoke), first)
        self.assertEqual(calls, [1, 2, 1, 2])

    def test_same_report_keeps_symptom_ids_on_new_source(self):
        triage.split(self.db, self.key, {}, None, lambda *_: {
            "symptoms": [{"title": title, "scope": title} for title in ("Save", "Display")],
        })
        next_key = (1, "r1", "next")
        triage.split(self.db, next_key, {}, None, lambda *_: self.fail("Report was split again"))
        self.assertEqual([tuple(row) for row in self.db.execute(triage.ROWS, next_key)],
                         [(1, "Save", "Save", None), (2, "Display", "Display", None)])

    def test_private_context_requires_independent_public_proof(self):
        invocations = []

        def invoke(schema, prompt, context, **options):
            if schema is triage.SPLIT_SCHEMA:
                return {"symptoms": [{"title": "Save", "scope": "Save"}]}
            invocations.append(options["public_only"])
            private = [] if options["public_only"] else [
                {**READ, "path": "evidence-1/Policy.json", "private": True},
            ]
            return diagnosis("DIRECT_FIX"), [READ, *private]

        result = triage.investigate(self.db, self.key, {"id": 1}, None, invoke)["symptoms"][0]
        self.assertEqual(invocations, [False, True])
        self.assertTrue(result["repair_ready"])
        self.assertFalse(result["private_evidence"])
        self.assertTrue(result["private_investigation"]["private_evidence"])

    def test_interrupted_sibling_keeps_completed_symptom_available(self):
        def invoke(schema, prompt, context, **options):
            if schema is triage.SPLIT_SCHEMA:
                return {"symptoms": [{"title": title, "scope": title} for title in ("Save", "Display")]}
            if context["symptom"]["id"] == 2:
                raise RuntimeError("Interrupted")
            return diagnosis("DIRECT_FIX"), [READ]

        with self.assertRaisesRegex(RuntimeError, "Interrupted"):
            triage.investigate(self.db, self.key, {"id": 1}, None, invoke, workers=2)
        saved = json.loads(self.db.execute("SELECT result FROM jobs WHERE id = 1").fetchone()[0])
        self.assertEqual(saved["classification"], triage.INCOMPLETE)
        self.assertTrue(saved["symptoms"][0]["repair_ready"])
        self.assertEqual(saved["symptoms"][1]["classification"], triage.INCOMPLETE)


class ValidationPlanTests(unittest.TestCase):
    def test_existing_zone_scope_is_forwarded_but_arbitrary_properties_are_refused(self):
        plan = {
            "mode": "aorebirth", "validation_timeout": 20,
            "validation_runner": ["trusted-runner"], "build_project": "Game.csproj",
            "test_project": validation.ZONE_TEST_PROJECT, "test_filter": "FullyQualifiedName~NpcBrain",
            "test_scope": "NpcAiFocusedTests",
        }
        with (patch.object(validation, "tracked_project", side_effect=lambda name, *_: name),
              patch.object(validation, "executable", side_effect=lambda name, *_: str(name)),
              patch.object(validation, "run_step", return_value="ok") as run):
            validation.validate(Path.cwd(), plan, baseline=False)
            self.assertIn("-p:NpcAiFocusedTests=true", run.call_args.args[0])
            with self.assertRaises(validation.ValidationError):
                validation.validate(Path.cwd(), {**plan, "test_scope": "ArbitraryProperty"}, baseline=False)


if __name__ == "__main__":
    unittest.main()
