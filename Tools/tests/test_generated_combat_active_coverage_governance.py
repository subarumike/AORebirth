import importlib.util
import json
from pathlib import Path
import sys
import unittest


REPO_ROOT = Path(__file__).resolve().parents[2]
GENERATOR_PATH = (
    REPO_ROOT
    / "tools-temp"
    / "AOSharpCaptureAnalyzer"
    / "generate_capture_backed_npc_active_coverage.py"
)


def load_generator():
    spec = importlib.util.spec_from_file_location(
        "aorebirth_active_coverage_generator", GENERATOR_PATH
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("could not load active-coverage generator")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class GeneratedCombatActiveCoverageGovernanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.generator = load_generator()

    def test_icc_shuttleport_has_one_accepted_entry_and_active_remainder(self):
        governance = self.generator.discover_icc_shuttleport_entry_governance(
            REPO_ROOT
        )

        self.assertEqual(governance["playfield"], 4582)
        self.assertEqual(governance["acceptedEntries"], 1)
        self.assertEqual(governance["activeEvidenceEntries"], 24)
        self.assertEqual(governance["blockedUnauditedEntries"], 0)
        self.assertEqual(governance["entries"][0]["name"], "Island Reet")
        self.assertEqual(
            governance["entries"][0]["state"], "ACCEPTED_RUNTIME_CONTENT"
        )
        self.assertTrue(
            all(
                entry["state"] == "ACTIVE_EVIDENCE"
                for entry in governance["entries"][1:]
            )
        )

    def test_icc_prepare_callsite_is_active_evidence_not_accepted_file_coverage(self):
        source = self.generator.ICC_SHUTTLEPORT_SOURCE
        self.assertNotIn(source, self.generator.RUNTIME_PREPARE_AUDIT_REFERENCES)
        self.assertIn(
            source, self.generator.RUNTIME_PREPARE_ACTIVE_EVIDENCE_REFERENCES
        )

        entry = next(
            row
            for row in self.generator.discover_runtime_prepare_entry_points(REPO_ROOT)
            if row["path"] == source
        )
        self.assertEqual(entry["prepareCallCount"], 1)
        self.assertEqual(entry["auditKind"], "active-evidence")
        self.assertEqual(entry["governanceState"], "ACTIVE_EVIDENCE")

    def test_published_coverage_excludes_icc_active_source_from_content_inputs(self):
        coverage_path = (
            REPO_ROOT
            / "docs"
            / "generated"
            / "capture_backed_npc_combat_active_coverage.json"
        )
        document = json.loads(coverage_path.read_text(encoding="utf-8"))
        content_input_paths = {
            row["path"] for row in document["contentInputs"]
        }
        self.assertNotIn(self.generator.ICC_SHUTTLEPORT_SOURCE, content_input_paths)
        governance = document["iccShuttleportEntryGovernance"]
        self.assertEqual(governance["acceptedEntries"], 1)
        self.assertEqual(governance["activeEvidenceEntries"], 24)
        self.assertEqual(governance["blockedUnauditedEntries"], 0)

    def test_external_staging_output_path_does_not_require_worktree_containment(self):
        output_path = (
            REPO_ROOT.parent
            / ".git"
            / "worktrees"
            / "linked"
            / "staging"
            / "capture_backed_npc_combat_active_coverage.json"
        )

        rendered = self.generator.format_generated_output_path(
            output_path, REPO_ROOT
        )

        self.assertEqual(
            rendered,
            "<external-staging>/capture_backed_npc_combat_active_coverage.json",
        )
        self.assertNotIn(str(REPO_ROOT), rendered)


if __name__ == "__main__":
    unittest.main()
