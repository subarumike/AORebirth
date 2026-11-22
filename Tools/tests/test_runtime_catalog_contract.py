import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock


MODULE = Path(__file__).resolve().parents[2] / "Tools/runtime_catalog_contract.py"
SPEC = importlib.util.spec_from_file_location("runtime_catalog_contract", MODULE)
assert SPEC is not None and SPEC.loader is not None
CONTRACT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CONTRACT)


class RuntimeCatalogContractTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.source = Path(directory.name) / "source"
        self.content = Path(directory.name) / "canonical"
        self.zone = self.source / CONTRACT.PRODUCTION_READERS[0]
        self.pork = self.source / CONTRACT.PORK_GAME_DATA
        self.pork_required = tuple(sorted(CONTRACT.PORK_SCHEMAS))
        self.admitted = set(self.pork_required)
        self.write(self.source / CONTRACT.GAME_DATA_PATHS, 'public const string TextFileName = "Text.json";')
        self.write(self.zone / "PvpLegacyTitleCatalog.cs", """
            const string FileName = "PvpLegacyTitles.json";
            object Read() => GameDataPaths.ResolveFile(root, FileName);
            object Text() => GameDataPaths.ResolveFile(root, GameDataPaths.TextFileName);
        """)
        self.write_pork(self.pork_required)
        for name, section in CONTRACT.PORK_NAME_SECTIONS.items():
            self.write_data(name, {section: {"1": {"Name": "Test", "Ranks": ["Leader"]}}})
        self.bands = {
            "TextCategory": 9,
            "Bands": [
                {"MinRating": 10, "TextId": 2, "Fallback": ""},
                {"MinRating": 20, "TextId": None, "Fallback": "Test fallback"},
            ],
        }
        self.write_data(CONTRACT.PVP_TITLES, self.bands)
        self.texts = [
            {"category": 9, "id": 2, "text": "Test reference"},
            {"category": 10, "id": 101, "text": "Test profession"},
            {"category": 11, "id": 100, "text": "Test rank"},
        ]
        self.write(self.content / CONTRACT.ROOT_TEXT, json.dumps(self.texts))

    @staticmethod
    def write(path, content):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    def write_pork(self, names, loader=""):
        declaration = ", ".join(f"'{name}'" for name in names)
        self.write(self.pork, f"""
            public const REQUIRED = [{declaration}];
            private const PROFESSION_TITLE_CATEGORY = 10;
            private const DEFENDER_RANK_CATEGORY = 11;
            {loader}
        """)

    def write_data(self, name, value):
        encoded = json.dumps(value)
        self.write(self.source / "AORebirth/GameData" / name, encoded)
        self.write(self.content / name, encoded)

    def verify(self, content=None):
        return CONTRACT.verify_runtime_catalogs(self.source, content, self.admitted.__contains__)

    def commit(self):
        command = ("git", "-C", str(self.source))
        for args in (("init", "--quiet"), ("add", "--all"),
                     ("-c", "user.name=Catalog Fixture", "-c", "user.email=fixture@example.invalid",
                      "commit", "--quiet", "--message", "Fixture")):
            subprocess.run((*command, *args), capture_output=True, check=True, timeout=15)
        return CONTRACT.git(self.source, "rev-parse", "HEAD").strip()

    def test_public_structure_and_canonical_catalogs_are_checked_separately(self):
        self.assertEqual(self.verify(), self.pork_required)
        self.assertEqual(self.verify(self.content), self.pork_required)
        (self.content / CONTRACT.ROOT_TEXT).unlink()
        self.assertEqual(self.verify(), self.pork_required)
        with self.assertRaisesRegex(ValueError, "Text.json"):
            self.verify(self.content)

    def test_missing_zone_admission_fails_in_public_ci(self):
        self.write(self.zone / "ExampleCatalog.cs", 'object Read() => GameDataPaths.ResolveFile(root, "Unknown.json");')
        with self.assertRaisesRegex(ValueError, "unadmitted.*Unknown.json"):
            self.verify()

    def test_stealth_multiplier_reader_requires_a_valid_staged_catalog(self):
        name = CONTRACT.STEALTH_MULTIPLIERS
        self.admitted.add(name)
        self.write(self.zone / "StealthMultiplierTable.cs",
                   f'object Read() => GameDataPaths.ResolveFile(root, "{name}");')
        public_table = MODULE.parents[1] / "AORebirth/GameData" / name
        self.write(self.source / "AORebirth/GameData" / name, public_table.read_text(encoding="utf-8"))
        with self.assertRaisesRegex(ValueError, "Required catalogs unavailable.*SpecialAttackMultipliers"):
            self.verify(self.content)

        for payload in ("{}", '{"rows": [', '{"rows": [{"skill": 1000, "weights": [0, 0]}]}',
                        '{"rows": [{"skill": 1000, "weights": [0, -0.5]}]}'):
            with self.subTest(payload=payload):
                self.write(self.content / name, payload)
                with self.assertRaisesRegex(ValueError, "Invalid stealth multiplier catalog.*SpecialAttackMultipliers"):
                    self.verify(self.content)

        self.write(self.content / name, public_table.read_text(encoding="utf-8"))
        self.assertIn(name, self.verify(self.content))

    def test_pork_load_requires_declaration_and_schema(self):
        self.write_pork(self.pork_required, "$this->load('Unknown.json');")
        with self.assertRaisesRegex(ValueError, "undeclared catalog"):
            self.verify()
        self.write_pork((*self.pork_required, "Unknown.json"))
        self.admitted.add("Unknown.json")
        with self.assertRaisesRegex(ValueError, "no structural validation"):
            self.verify()

    def test_duplicate_text_category_and_id_fail(self):
        self.write(self.content / CONTRACT.ROOT_TEXT, json.dumps([*self.texts, self.texts[0]]))
        with self.assertRaisesRegex(ValueError, "duplicate category and text identity"):
            self.verify(self.content)

    def test_pvp_reference_requires_matching_category_and_id(self):
        self.texts[0]["category"] = 21
        self.write(self.content / CONTRACT.ROOT_TEXT, json.dumps(self.texts))
        with self.assertRaisesRegex(ValueError, "unresolved.*reference"):
            self.verify(self.content)

    def test_duplicate_pvp_rating_fails_in_public_ci(self):
        self.bands["Bands"][1]["MinRating"] = self.bands["Bands"][0]["MinRating"]
        self.write_data(CONTRACT.PVP_TITLES, self.bands)
        with self.assertRaisesRegex(ValueError, "duplicate rating band"):
            self.verify()

    def test_invalid_pork_identity_fails_in_public_ci(self):
        self.write_data("Breed.json", {"Breeds": {"1": {"Name": ""}}})
        with self.assertRaisesRegex(ValueError, "invalid named identity"):
            self.verify()

    def test_exact_proposal_detects_an_admitted_new_reader_and_requires_fixture(self):
        base = self.commit()
        filename = "New.json"
        self.admitted.add(filename)
        self.write(self.zone / "NewCatalog.cs", 'object Read() => GameDataPaths.ResolveFile(root, "New.json");')
        proposed = self.commit()
        self.assertEqual(self.verify(), self.pork_required)
        changes = CONTRACT.proposed_catalog_changes(self.source, base)
        self.assertEqual(changes, {"added_dependencies": [filename], "removed_dependencies": [], "affected_catalogs": [filename]})
        with self.assertRaisesRegex(ValueError, "focused production reader fixture"):
            CONTRACT.require_new_catalog_coverage(self.source, changes["added_dependencies"])
        CONTRACT.exact_candidate(self.source, proposed)
        with self.assertRaisesRegex(ValueError, "requested head"):
            CONTRACT.exact_candidate(self.source, base)

    def test_new_reader_fixture_requires_schema(self):
        filename = "New.json"
        self.write(self.source / CONTRACT.ZONE_TEST_SOURCE, "[TestMethod]\npublic void ReadStaged() {}")
        with mock.patch.dict(CONTRACT.ZONE_CATALOG_TESTS, {filename: "ReadStaged"}):
            with self.assertRaisesRegex(ValueError, "New.schema.json"):
                CONTRACT.require_new_catalog_coverage(self.source, [filename])
            self.write(self.source / CONTRACT.schema_path(filename), '{"$schema":"https://json-schema.org/draft/2020-12/schema"}')
            CONTRACT.require_new_catalog_coverage(self.source, [filename])

    def test_changed_shared_filename_detects_retired_and_new_dependencies(self):
        base = self.commit()
        self.write(self.source / CONTRACT.GAME_DATA_PATHS, 'public const string TextFileName = "Shared.json";')
        self.commit()
        changes = CONTRACT.proposed_catalog_changes(self.source, base)
        self.assertEqual(changes["added_dependencies"], ["Shared.json"])
        self.assertEqual(changes["removed_dependencies"], [])  # PORK still needs Text.json.
        self.assertIn("Text.json", changes["affected_catalogs"])

    def test_retired_reader_is_visible_in_the_proposal(self):
        self.write(self.zone / "OtherCatalog.cs", 'object Read() => GameDataPaths.ResolveFile(root, "Old.json");')
        base = self.commit()
        (self.zone / "OtherCatalog.cs").unlink()
        self.commit()
        changes = CONTRACT.proposed_catalog_changes(self.source, base)
        self.assertEqual(changes["removed_dependencies"], ["Old.json"])

    def test_private_reconciliation_reports_missing_and_stale_without_overriding_retained_merge(self):
        rows = [{"path": "Merged.json", "blob": "retained local merge"}, {"path": "Stale.json", "blob": "prior"},
                {"path": "Unpublished.json", "blob": "local only"}]
        upstream = {"Merged.json": {"blob": "upstream"}, "Stale.json": {"blob": "fresh"}, "Pending.json": {"blob": "available"}}
        checkpoint = {"PRIVATE_CONTENT_UPSTREAM": {"AFTER": {"Merged.json": upstream["Merged.json"], "Stale.json": {"blob": "prior"}}},
                      "PUBLIC_PRODUCT_DATA": {"AFTER": {}}}
        work = CONTRACT.reconciliation_work(["Merged.json", "Pending.json", "Stale.json", "Unpublished.json"], rows,
                                            upstream, {}, checkpoint, {row["path"]: row for row in rows})
        self.assertEqual(work, {"missing_authority": ["Unpublished.json"], "requires_reconciliation": ["Pending.json", "Stale.json"]})

    def test_dirty_candidate_cannot_bind_a_private_receipt(self):
        head = self.commit()
        self.write(self.zone / "Uncommitted.cs", "class Candidate {}")
        with self.assertRaisesRegex(ValueError, "clean exact"):
            CONTRACT.exact_candidate(self.source, head)


if __name__ == "__main__":
    unittest.main()
