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
from explore import Explorer
from repair import ApprovalError, RepairError, prepare, review_candidate
from worker import InvestigationError, initialize


PATH = "AORebirth/Server/ZoneEngine_New/Example.cs"
READ = {"path": PATH, "first": 1, "last": 30, "private": False, "tool": "read"}
SWEEP = {"path": "", "first": 0, "last": 0, "private": True, "tool": "evidence_sweep"}


class EvidenceSearchTests(unittest.TestCase):
    def test_each_approved_root_is_searched_even_if_the_first_read_fails(self):
        explorer = Explorer.__new__(Explorer)
        explorer.aliases = {"evidence-1": "GameData", "evidence-2": "captures",
                            "evidence-3": "RDB extracts"}
        explorer.private = {f"{alias}/example.txt": None for alias in explorer.aliases}
        match = {"matches": [{"path": "evidence-3/example.txt", "lines": [1]}], "bounded": False}
        with (patch.object(explorer, "search", side_effect=[OSError, {"matches": []}, match]) as search,
              patch.object(explorer, "record") as record):
            result = explorer.search_evidence({"query": "SOSH"})
        self.assertEqual([item.args[0]["path"] for item in search.call_args_list], list(explorer.aliases))
        self.assertIn("unavailable", result["roots"]["evidence-1"])
        self.assertEqual(result["roots"]["evidence-3"]["matches"], match["matches"])
        record.assert_called_once_with("evidence_sweep", "", 0, 0, private=True)


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

    def test_complete_source_trace_still_needs_local_evidence_search_before_escalation(self):
        skipped, _, _ = triage.verified_result(diagnosis("CAPTURE_REQUIRED"), [READ], [])
        searched, _, _ = triage.verified_result(diagnosis("CAPTURE_REQUIRED"), [READ, SWEEP], [])
        self.assertEqual(skipped["classification"], triage.INCOMPLETE)
        self.assertEqual(searched["classification"], "CAPTURE_REQUIRED")

    def test_siblings_and_requeue_are_independent(self):
        calls = []

        def invoke(schema, prompt, context, **options):
            if schema is triage.SPLIT_SCHEMA:
                return {"symptoms": [{"title": title, "scope": title} for title in ("Save", "Display")]}
            number = context["symptom"]["id"]
            calls.append(number)
            if number == 1 and calls.count(1) == 1:
                return diagnosis(triage.INCOMPLETE, missing=["Inspect the DAO"]), [READ]
            return (diagnosis("DIRECT_FIX"), [READ]) if number == 1 else (
                diagnosis("CAPTURE_REQUIRED"), [READ, SWEEP])

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
                raise InvestigationError("Interrupted", interrupted_reads=[READ])
            return diagnosis("DIRECT_FIX"), [READ]

        with self.assertRaisesRegex(InvestigationError, "Interrupted"):
            triage.investigate(self.db, self.key, {"id": 1}, None, invoke, workers=2)
        saved = json.loads(self.db.execute("SELECT result FROM jobs WHERE id = 1").fetchone()[0])
        self.assertEqual(saved["classification"], triage.INCOMPLETE)
        self.assertTrue(saved["symptoms"][0]["repair_ready"])
        self.assertEqual(saved["symptoms"][1]["classification"], triage.INCOMPLETE)
        self.assertEqual(triage.progress(self.db, 1)[1]["interrupted_reads"], [READ])
        self.assertEqual(saved["symptoms"][1]["source_reads"], [])

        def retry(_schema, _prompt, context, **_options):
            self.assertEqual(context["symptom"]["id"], 2)
            self.assertEqual(context["interrupted_read_hints"], [READ])
            return diagnosis("DIRECT_FIX"), []

        retried = triage.investigate(self.db, self.key, {"id": 1}, saved, retry)
        self.assertEqual(retried["symptoms"][1]["classification"], triage.INCOMPLETE)
        self.assertFalse(retried["symptoms"][1]["repair_ready"])

    def test_followup_reclassifies_only_reviewed_symptom_and_preserves_raw_checkpoint(self):
        def invoke(schema, *_args, **_opts):
            if schema is triage.SPLIT_SCHEMA:
                return {"symptoms": [{"title": title, "scope": title} for title in ("Save", "Display")]}
            return diagnosis(triage.INCOMPLETE), [READ]

        triage.investigate(self.db, self.key, {"id": 1}, None, invoke)
        original = triage.progress(self.db, 1)[0]["result"]
        finding = {"classification": "DIRECT_FIX", "finding": "Proven defect",
                   "evidence": ["Focused source trace"], "next_action": "Review candidate"}
        with self.assertRaisesRegex(ValueError, "source commit"):
            triage.record_followup(self.db, 1, 1, "wrong", finding)
        self.assertEqual(triage.record_followup(self.db, 1, 1, "source", finding), triage.INCOMPLETE)
        first = json.loads(self.db.execute("SELECT result FROM jobs WHERE id = 1").fetchone()[0])
        self.assertEqual(first["symptoms"][0]["classification"], "DIRECT_FIX")
        self.assertFalse(first["symptoms"][0]["repair_ready"])
        self.assertEqual(first["symptoms"][1]["classification"], triage.INCOMPLETE)
        second = {**finding, "classification": "CAPTURE_REQUIRED"}
        self.assertEqual(triage.record_followup(self.db, 1, 2, "source", second), "MIXED")
        self.assertEqual(self.db.execute("SELECT state FROM jobs WHERE id = 1").fetchone()[0], "done")
        self.assertTrue(triage.requeue(self.db, 1, 1))
        self.assertEqual(triage.progress(self.db, 1)[0]["result"], original)
        self.assertEqual(triage.progress(self.db, 1)[0]["followups"], [finding])

    def test_operator_review_can_complete_an_interrupted_tracked_symptom(self):
        triage.split(self.db, self.key, {}, None, lambda *_: {
            "symptoms": [{"title": "Save", "scope": "Save"}],
        })
        finding = {"classification": "CAPTURE_REQUIRED", "finding": "Traced the save",
                   "evidence": ["Operator source trace"], "next_action": "Capture the response"}
        self.assertEqual(triage.record_followup(self.db, 1, 1, "source", finding), "CAPTURE_REQUIRED")
        progress = triage.progress(self.db, 1)[0]
        self.assertIsNone(progress["result"])
        self.assertEqual(progress["passes"], 0)
        self.assertEqual(progress["followups"], [finding])
        self.assertTrue(triage.register_evidence(self.db, 1, 1, "r1", "source", "a" * 64, "new capture"))

    def test_new_evidence_resumes_only_its_pinned_symptom_once(self):
        def initial(schema, *_args, **_options):
            if schema is triage.SPLIT_SCHEMA:
                return {"symptoms": [{"title": title, "scope": title} for title in ("Save", "Display")]}
            return diagnosis("CAPTURE_REQUIRED"), [READ, SWEEP]

        blocked = triage.investigate(self.db, self.key, {"id": 1}, None, initial)
        self.db.execute("UPDATE jobs SET state = 'done' WHERE id = 1")
        self.assertEqual(triage.investigate(self.db, self.key, {"id": 1}, blocked,
                                            lambda *_: self.fail("Reopened without new evidence")), blocked)
        sha256 = "a" * 64
        reference = "approved-capture-summary.txt"
        with self.assertRaisesRegex(ValueError, "fingerprint or source commit"):
            triage.register_evidence(self.db, 1, 2, "old", "source", sha256, reference)
        self.assertTrue(triage.register_evidence(self.db, 1, 2, "r1", "source", sha256, reference))
        self.assertFalse(triage.register_evidence(self.db, 1, 2, "r1", "source", sha256, "duplicate.txt"))
        state, raw = self.db.execute("SELECT state, result FROM jobs WHERE id = 1").fetchone()
        self.assertEqual(state, "pending")
        queued = json.loads(raw)
        self.assertEqual(queued["symptoms"][0], blocked["symptoms"][0])
        self.assertEqual(queued["symptoms"][1]["classification"], triage.INCOMPLETE)

        calls = []

        def resume(_schema, _prompt, context, *, public_only):
            calls.append((context["symptom"]["id"], context["source_commit"], public_only))
            self.assertEqual(context["registered_evidence"],
                             [] if public_only else [{"sha256": sha256, "reference": reference}])
            return diagnosis("DIRECT_FIX"), [READ]

        updated = triage.investigate(self.db, (1, "r1", "new-tip"), {"id": 1}, queued, resume)
        self.assertEqual(calls, [(2, "source", False), (2, "source", True)])
        self.assertEqual(updated["symptoms"][0], blocked["symptoms"][0])
        self.assertEqual(updated["symptoms"][1]["classification"], "DIRECT_FIX")
        progress = triage.progress(self.db, 1)[1]
        self.assertFalse(progress["evidence_pending"])
        self.assertEqual(progress["evidence_revisions"][0]["previous_checkpoint"]["result"]["classification"],
                         "CAPTURE_REQUIRED")
        self.assertFalse(triage.register_evidence(self.db, 1, 2, "r1", "source", sha256, reference))


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

    def test_existing_runner_cannot_enable_unattended_product_execution(self):
        with TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            plan = root / "plan.json"
            plan.write_text(json.dumps({
                "mode": "aorebirth", "repository": "subarumike/AORebirth", "base": "master",
                "validation_runner": [str(root / "missing.exe")],
            }), encoding="utf-8")
            args = SimpleNamespace(repair_plan=plan, synthetic=False, source=root / "source", db=root / "queue",
                                   supervised_validation=False)
            with patch("repair.git") as git:
                with self.assertRaisesRegex(RepairError, "Unattended product validation is disabled"):
                    prepare(args)
                git.assert_not_called()

    def test_approval_requires_an_operator_and_rechecks_the_exact_candidate(self):
        args = SimpleNamespace(approve_tree="tree", plan={})
        state = {"approval_tree": "tree"}
        with (patch("repair.check_files"), patch("repair.sys.stdout.isatty", return_value=True),
              patch("repair.sys.stdin.isatty", return_value=False),
              patch("repair.git", side_effect=["base", "tree", "", PATH + "\0", ""]),
              patch("builtins.input") as read):
            with self.assertRaisesRegex(ApprovalError, "interactive operator"):
                review_candidate(args, state, Path.cwd(), "base", "tree", {}, {PATH: "review"})
            read.assert_not_called()
        with (patch("repair.check_files"), patch("repair.sys.stdout.isatty", return_value=True),
              patch("repair.sys.stdin.isatty", return_value=True),
              patch("repair.git", side_effect=["base", "tree", "", PATH + "\0", "", "base", "changed"]),
              patch("builtins.input", return_value="VALIDATE tree")):
            with self.assertRaisesRegex(ApprovalError, "changed while awaiting approval"):
                review_candidate(args, state, Path.cwd(), "base", "tree", {}, {PATH: "review"})

    def test_existing_zone_scope_is_forwarded_but_arbitrary_properties_are_refused(self):
        plan = {
            "mode": "aorebirth", "validation_timeout": 20,
            "build_project": "Game.csproj",
            "test_project": validation.ZONE_TEST_PROJECT, "test_filter": "FullyQualifiedName~NpcBrain",
            "test_scope": "NpcAiFocusedTests",
        }
        with (TemporaryDirectory() as temporary,
              patch.object(validation, "tracked_project", side_effect=lambda name, *_: name),
              patch.object(validation, "executable", side_effect=lambda name, *_: str(name)),
              patch.object(validation, "run_step", return_value="ok"),
              patch.dict(validation.os.environ, {"OS": "Windows_NT", "GH_TOKEN": "not-inherited"}),
              patch.object(validation, "run_regression", return_value="ok") as run):
            evidence = Path(temporary).resolve()
            with self.assertRaisesRegex(validation.ValidationError, "supervised"):
                validation.validate(Path.cwd(), plan, baseline=False, evidence_dir=evidence)
            validation.validate(Path.cwd(), plan, baseline=False, evidence_dir=evidence, supervised=True)
            self.assertIn("-p:NpcAiFocusedTests=true", run.call_args.args[0])
            self.assertIn("run-feature-validation", run.call_args.args[0])
            self.assertEqual(run.call_args.args[2]["OS"], "Windows_NT")
            self.assertNotIn("GH_TOKEN", run.call_args.args[2])
            with self.assertRaises(validation.ValidationError):
                validation.validate(Path.cwd(), {**plan, "test_scope": "ArbitraryProperty"},
                                    baseline=False, evidence_dir=evidence, supervised=True)

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
