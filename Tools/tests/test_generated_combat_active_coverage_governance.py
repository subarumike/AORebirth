import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

REPO_ROOT = Path(__file__).resolve().parents[2]
GENERATOR_PATH = REPO_ROOT / "tools-temp/AOSharpCaptureAnalyzer/generate_capture_backed_npc_active_coverage.py"


def load_generator():
    spec = importlib.util.spec_from_file_location("aorebirth_active_coverage_generator", GENERATOR_PATH)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class GeneratedCombatActiveCoverageGovernanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.generator = load_generator()

    def test_current_consumer_audit_rejects_removed_structural_validation(self):
        original = self.generator.read_source
        def missing_validation(repo_root, relative):
            source = original(repo_root, relative)
            return source.replace("SpawnContentValidation.IsValid(", "MissingValidation(")
        with mock.patch.object(self.generator, "read_source", side_effect=missing_validation):
            with self.assertRaisesRegex(self.generator.CoverageError, "consumer contract changed"):
                self.generator.discover_current_consumers(REPO_ROOT)

    def test_every_shared_combat_fragment_is_hashed_without_legacy_runtime(self):
        consumers = self.generator.discover_current_consumers(REPO_ROOT)
        paths = {row["path"] for row in consumers}
        self.assertTrue(set(self.generator.CAPTURED_COMBAT_SHARED_SOURCE_INPUTS) <= paths)
        self.assertTrue(all("/Server/ZoneEngine/" not in path for path in paths))
        self.assertTrue(all(len(row["sha256"]) == 64 for row in consumers))

    def test_historical_population_classification_is_preserved_as_evidence(self):
        document = self.generator.historical_coverage(REPO_ROOT)
        self.assertEqual(sum(row["actorCount"] for row in document["profiles"]), document["totals"]["initialActorCount"])
        self.assertEqual(document["totals"]["certified"] + document["totals"]["unresolved"], document["totals"]["initialActorCount"])

    def test_historical_snapshot_mutation_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            snapshot = root / self.generator.HISTORICAL_COVERAGE_PATH
            snapshot.parent.mkdir(parents=True)
            snapshot.write_text('{"totals": {}}\n', encoding="utf-8")
            with self.assertRaisesRegex(self.generator.CoverageError, "evidence changed"):
                self.generator.historical_coverage(root)

    def test_editable_content_audit_does_not_claim_private_or_historical_population(self):
        current = self.generator.editable_content_inventory(REPO_ROOT)
        world = json.loads((REPO_ROOT / "AORebirth/GameData/WorldContent.json").read_text())
        self.assertEqual(len(world["Npcs"]), current["authoredNpcDefinitionCount"])
        self.assertFalse(current["runtimeActivationPermissionFromEvidence"])
        self.assertFalse(current["historicalRosterIsCurrentPopulation"])
        self.assertFalse(current["privateHashCatalogPopulationEvaluated"])

    def test_external_staging_output_path_does_not_require_worktree_containment(self):
        output = REPO_ROOT.parent / ".git/worktrees/linked/staging/capture_backed_npc_combat_active_coverage.json"
        self.assertEqual("<external-staging>/capture_backed_npc_combat_active_coverage.json", self.generator.format_generated_output_path(output, REPO_ROOT))


if __name__ == "__main__":
    unittest.main()
