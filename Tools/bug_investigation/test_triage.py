import json
from pathlib import Path
import sqlite3
import sys
from tempfile import TemporaryDirectory
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import triage
import validation
from repair import prepare
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
    def test_validation_log_keeps_the_result_and_bounds_verbose_output(self):
        with TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            log = root / "build.log"
            command = [sys.executable, "-c",
                       "import sys; print('x' * 300000); print('build result', file=sys.stderr)"]
            validation.run_step(command, "Scoped build", root, None, 20, log=log)
            result = log.read_bytes()
            self.assertLess(len(result), 270_000)
            self.assertIn(b"Earlier output omitted", result)
            self.assertIn(b"build result", result)

    def test_missing_product_runner_fails_before_fetch_or_attempt(self):
        with TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            plan = root / "plan.json"
            plan.write_text(json.dumps({
                "mode": "aorebirth", "repository": "subarumike/AORebirth", "base": "master",
                "validation_runner": [str(root / "missing.exe")],
            }), encoding="utf-8")
            args = SimpleNamespace(repair_plan=plan, synthetic=False, source=root / "source", db=root / "queue")
            with patch("repair.git") as git:
                with self.assertRaisesRegex(validation.ValidationError, "validation_runner"):
                    prepare(args)
                git.assert_not_called()

    def test_existing_zone_scope_is_forwarded_but_arbitrary_properties_are_refused(self):
        plan = {
            "mode": "aorebirth", "validation_timeout": 20,
            "validation_runner": ["trusted-runner"], "build_project": "Game.csproj",
            "test_project": validation.ZONE_TEST_PROJECT, "test_filter": "FullyQualifiedName~NpcBrain",
            "test_scope": "NpcAiFocusedTests",
        }
        with (patch.object(validation, "tracked_project", side_effect=lambda name, *_: name),
              patch.object(validation, "executable", side_effect=lambda name, *_: str(name)),
              patch.object(validation, "run_step", return_value="ok"),
              patch.dict(validation.os.environ, {"OS": "Windows_NT", "GH_TOKEN": "not-inherited"}),
              patch.object(validation, "run_regression", return_value="ok") as run):
            validation.validate(Path.cwd(), plan, baseline=False)
            self.assertIn("-p:NpcAiFocusedTests=true", run.call_args.args[0])
            self.assertEqual(run.call_args.args[2]["OS"], "Windows_NT")
            self.assertNotIn("GH_TOKEN", run.call_args.args[2])
            with self.assertRaises(validation.ValidationError):
                validation.validate(Path.cwd(), {**plan, "test_scope": "ArbitraryProperty"}, baseline=False)

    def test_product_regression_requires_a_pass_matching_the_fully_qualified_filter(self):
        def trx(class_name, *, passed=1, failed=0, outcome="Passed"):
            return f"""
                <TestRun xmlns="{validation.TRX_NAMESPACE['trx']}">
                    <Results>
                        <UnitTestResult testId="test-1" testName="NpcBrain display label" outcome="{outcome}" />
                    </Results>
                    <TestDefinitions><UnitTest id="test-1">
                        <TestMethod className="{class_name}" name="HandlesMove" />
                    </UnitTest></TestDefinitions>
                    <ResultSummary><Counters executed="{passed + failed}" passed="{passed}" failed="{failed}" /></ResultSummary>
                </TestRun>
            """

        cases = (
            ("missing", None, False),
            ("malformed", "<TestRun", False),
            ("empty", trx("ZoneTests.NpcBrain", passed=0, outcome="NotExecuted"), False),
            ("failed despite a matching pass", trx("ZoneTests.NpcBrain", failed=1), False),
            ("wrong filter despite display name", trx("ZoneTests.Other"), False),
            ("matching fully qualified name", trx("ZoneTests.NpcBrain, ZoneTests"), True),
        )
        command = ["trusted-runner", "cmd.exe", "windows_release_contract.cmd", "test"]
        for name, contents, should_pass in cases:
            with self.subTest(name=name), TemporaryDirectory() as temporary:
                worktree = Path(temporary).resolve()

                def run_step(argv, *_):
                    self.assertEqual(argv[:len(command)], command)
                    self.assertIn("trx;LogFileName=regression.trx", argv)
                    output = Path(argv[argv.index("--results-directory") + 1])
                    self.assertEqual(output.parent, worktree / ".local")
                    self.assertEqual(list(output.iterdir()), [])
                    if contents is not None:
                        (output / "regression.trx").write_text(contents, encoding="utf-8")

                with (patch.object(validation.subprocess, "run", return_value=SimpleNamespace(returncode=0)),
                      patch.object(validation, "run_step", side_effect=run_step)):
                    if should_pass:
                        result = validation.run_regression(command, worktree, {}, 20, "FullyQualifiedName~npcbrain")
                        self.assertIn("1 matching test", result)
                    else:
                        with self.assertRaises(validation.ValidationError):
                            validation.run_regression(command, worktree, {}, 20, "FullyQualifiedName~npcbrain")
                self.assertEqual(list((worktree / ".local").iterdir()), [])


if __name__ == "__main__":
    unittest.main()
