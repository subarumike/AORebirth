import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


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

    def test_unbound_or_unpinned_private_check_cannot_satisfy_premerge(self):
        rule_id = 1
        rules = [{"type": kind, "ruleset_id": rule_id} for kind in ("pull_request", "deletion", "non_fast_forward")]
        checks = [{"context": CONTRACT.PUBLIC_CHECK, "integration_id": CONTRACT.GITHUB_ACTIONS_APP}]
        rules.append({"type": "required_status_checks", "ruleset_id": rule_id, "parameters": {
            "strict_required_status_checks_policy": True, "required_status_checks": checks,
        }})
        with self.assertRaisesRegex(ValueError, "trusted validation App"):
            CONTRACT.require_trusted_binding(rules)
        trusted = {"context": CONTRACT.TRUSTED_CHECK, "integration_id": CONTRACT.GITHUB_ACTIONS_APP}
        checks.append(trusted)
        with self.assertRaisesRegex(ValueError, "trusted validation App"):
            CONTRACT.require_trusted_binding(rules)
        trusted["integration_id"] = 20001
        CONTRACT.require_trusted_binding(rules)


if __name__ == "__main__":
    unittest.main()
