import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
TOOLS_ROOT = REPOSITORY_ROOT / "Tools"
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import webcore_php_compatibility as compatibility


class WebCorePhpCompatibilityTests(unittest.TestCase):
    def test_exact_replacement_rejects_drift(self):
        with self.assertRaises(compatibility.CompatibilityError):
            compatibility._replace_exact(
                "changed upstream text",
                "expected upstream text",
                "patched text",
                1,
                "tiny-fixture",
            )

    def test_tiny_fixture_transforms_remove_targeted_apis(self):
        register_fixture = (
            "<?php\n"
            "$a = sizeof($regArgs['username']);\n"
            "$b = sizeof($regArgs['password']);\n"
            "$c = sizeof($regArgs['email']);\n"
            "$d = sizeof($regArgs['captcha']);\n"
        ).encode("utf-8")
        patched_register = compatibility._patch_register(register_fixture).decode("utf-8")
        self.assertNotIn("sizeof($regArgs", patched_register)
        self.assertEqual(4, patched_register.count("strlen($regArgs"))

        notfound_fixture = (
            "<?php echo '404 error: http://' . $_SERVER['SERVER_NAME'] . $_SERVER['REQUEST_URI']; ?>\n"
            "<script>const referrer = \"<?=$_SERVER['HTTP_REFERER']?>\";</script>\n"
            "<?=($_SERVER['HTTP_REFERER'])?$_SERVER['HTTP_REFERER']:\"<span class='m1'>Not Defined</span>\"?>\n"
            "<?$p=getdate(); echo($p['year']); ?>\n"
        ).encode("utf-8")
        patched_notfound = compatibility._patch_notfound(notfound_fixture).decode("utf-8")
        self.assertIn("<?php $p=getdate();", patched_notfound)
        self.assertNotRegex(patched_notfound, r"<\?(?!php\b|=)")
        self.assertNotIn(". $_SERVER['SERVER_NAME'] . $_SERVER['REQUEST_URI']", patched_notfound)
        self.assertEqual(4, patched_notfound.count("htmlspecialchars"))
        self.assertNotIn("<?=$_SERVER['HTTP_REFERER']?>", patched_notfound)

        playfields_fixture = (
            '<?php\n$sql = "SELECT `Id`\n'
            'FROM `playfields` WHERE `playfields`.");\n'
        ).encode("utf-8")
        patched_playfields = compatibility._patch_playfields(playfields_fixture).decode("utf-8")
        self.assertIn('FROM `playfields` WHERE `playfields`.";', patched_playfields)
        self.assertNotIn('FROM `playfields` WHERE `playfields`.");', patched_playfields)

    def test_tiny_fixture_scan_counts_removed_and_clean_apis(self):
        with tempfile.TemporaryDirectory(prefix="webcore-compat-test-") as temporary:
            root = Path(temporary)
            sample = root / "sample.php"
            sample.write_text(
                "<? echo mysql_query('select 1'); "
                "$bytes = mcrypt_create_iv(30); "
                "$quoted = get_magic_quotes_gpc(); ?>",
                encoding="utf-8",
            )
            counts = compatibility.scan_php_categories(root, ["sample.php"])
            self.assertEqual(1, counts["mysql_star"])
            self.assertEqual(1, counts["mcrypt_star"])
            self.assertEqual(1, counts["get_magic_quotes_gpc"])
            self.assertEqual(1, counts["short_open_tags"])

            sample.write_text(
                "<?php $bytes = random_bytes(30); $length = strlen($value); ?>",
                encoding="utf-8",
            )
            clean_counts = compatibility.scan_php_categories(root, ["sample.php"])
            for category in (
                "mysql_star",
                "mcrypt_star",
                "get_magic_quotes_gpc",
                "short_open_tags",
            ):
                self.assertEqual(0, clean_counts[category])

    def test_tiny_fixture_complete_tree_hash_validation(self):
        with tempfile.TemporaryDirectory(prefix="webcore-manifest-test-") as temporary:
            root = Path(temporary)
            first = root / "first.php"
            nested = root / "assets" / "second.txt"
            nested.parent.mkdir()
            first.write_bytes(b"<?php echo 'ok'; ?>\n")
            nested.write_bytes(b"deterministic fixture\n")
            entries = [
                compatibility.FileEntry(
                    path="first.php",
                    size=first.stat().st_size,
                    sha256=compatibility.sha256_file(first),
                ),
                compatibility.FileEntry(
                    path="assets/second.txt",
                    size=nested.stat().st_size,
                    sha256=compatibility.sha256_file(nested),
                ),
            ]
            compatibility.validate_tree(root, entries)
            nested.write_bytes(b"tampered fixture\n")
            with self.assertRaises(compatibility.CompatibilityError):
                compatibility.validate_tree(root, entries)

    def test_checked_in_manifests_are_hash_linked_and_complete(self):
        compatibility_manifest = ET.parse(
            compatibility.COMPATIBILITY_MANIFEST_PATH
        ).getroot()
        patched_manifest_bytes = compatibility.PATCHED_MANIFEST_PATH.read_bytes()
        patched_manifest = ET.fromstring(patched_manifest_bytes)
        patched_files = patched_manifest.findall("File")
        patches = compatibility_manifest.findall("Patch")

        self.assertEqual("7140", compatibility_manifest.get("FileCount"))
        self.assertEqual("7140", patched_manifest.get("FileCount"))
        self.assertEqual(7140, len(patched_files))
        self.assertEqual(len(patched_files), len({node.get("Path") for node in patched_files}))
        self.assertEqual("7", compatibility_manifest.get("PatchFileCount"))
        self.assertEqual(7, len(patches))
        self.assertEqual(
            compatibility.sha256_file(compatibility.BASE_MANIFEST_PATH),
            compatibility_manifest.get("BaseManifestSha256"),
        )
        self.assertEqual(
            hashlib.sha256(patched_manifest_bytes).hexdigest(),
            compatibility_manifest.get("FinalManifestSha256"),
        )
        base_files = {
            node.get("Path"): node
            for node in ET.parse(compatibility.BASE_MANIFEST_PATH).getroot().findall("File")
        }
        patched_files_by_path = {node.get("Path"): node for node in patched_files}
        patches_by_path = {node.get("Path"): node for node in patches}
        self.assertEqual(
            "playfields-sql-syntax-v1",
            patches_by_path["includes/data/playfields.php"].get("OperationId"),
        )
        self.assertEqual(
            "notfound-php8-and-server-html-encoding-v2",
            patches_by_path["notfound.php"].get("OperationId"),
        )
        for patch in patches:
            self.assertRegex(patch.get("InputSha256", ""), r"^[0-9a-f]{64}$")
            self.assertRegex(patch.get("OutputSha256", ""), r"^[0-9a-f]{64}$")
            self.assertTrue(patch.get("OperationId"))
            self.assertTrue(patch.get("Path"))
            path = patch.get("Path")
            self.assertEqual(base_files[path].get("Size"), patch.get("InputSize"))
            self.assertEqual(base_files[path].get("Sha256"), patch.get("InputSha256"))
            self.assertEqual(patched_files_by_path[path].get("Size"), patch.get("OutputSize"))
            self.assertEqual(
                patched_files_by_path[path].get("Sha256"), patch.get("OutputSha256")
            )

    def test_generated_inventory_covers_every_requested_category(self):
        inventory = json.loads(compatibility.INVENTORY_PATH.read_text(encoding="utf-8"))
        categories = inventory["categories"]
        self.assertEqual(set(compatibility.REQUESTED_CATEGORIES), set(categories))
        self.assertEqual(1, categories["syntax_errors"]["base_count"])
        self.assertEqual(0, categories["syntax_errors"]["patched_count"])
        self.assertEqual(
            "fail-closed-unreachable",
            inventory["security_boundary"]["host_route_policy"][
                "state_mutation_and_admin_member_findings"
            ],
        )
        required_fields = {
            "category",
            "relative_path",
            "line",
            "pattern",
            "runtime_impact",
            "minimum_php",
            "maximum_php",
            "security_relevance",
            "reachable",
            "required_patch_action",
            "disposition",
        }
        for category_name, category in categories.items():
            self.assertIn("base_count", category)
            self.assertIn("patched_count", category)
            self.assertIn("findings", category)
            for finding in category["findings"]:
                self.assertTrue(required_fields.issubset(finding))
                self.assertEqual(category_name, finding["category"])


if __name__ == "__main__":
    unittest.main()
