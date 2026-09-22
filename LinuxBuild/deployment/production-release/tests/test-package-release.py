#!/usr/bin/env python3
"""Release-boundary regressions: deliberately damage complete extracted packages."""
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

MODULE_PATH = Path(__file__).resolve().parents[1] / "package-release.py"
SPEC = importlib.util.spec_from_file_location("release_package", MODULE_PATH)
package = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(package)


class PackageTests(unittest.TestCase):
    def test_release_tooling_and_runtime_must_be_same_commit(self):
        package.require_same_source("a" * 40, "a" * 40)
        with self.assertRaisesRegex(ValueError, "exact same"):
            package.require_same_source("a" * 40, "b" * 40)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="package-tests-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / "source"
        for relative in package.discover(package.ROOT):
            target = self.source / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(package.ROOT / relative, target)
        self.manifest = {}
        for key, relative in package.ARTIFACTS.items():
            folder = self.root / key
            folder.mkdir()
            apphost = "LoginEngine" if key.startswith("LOGIN") else "ZoneEngine_New"
            (folder / apphost).write_text("#!/bin/sh\nexit 0\n")
            (folder / apphost).chmod(0o755)
            (folder / (apphost + ".dll")).write_bytes(b"fixture-dll")
            self.manifest[key] = str(folder)
        for engine in ("loginengine", "zoneengine"):
            path = "LinuxBuild/deployment/systemd/ao-rebirth-" + engine + ".service"
            self.manifest[engine.upper() + "_UNIT_SHA256"] = package.sha((self.source / path).read_bytes())
        self.files = package.package_files(self.source, self.manifest, "/srv/test-release", {})
        self.extracted = self.root / "extracted"
        for relative, (data, mode, _) in self.files.items():
            path = self.extracted / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            path.chmod(mode)

    def guard(self, succeeds):
        result = subprocess.run(["bash", str(self.extracted / (package.PREFIX + "package-integrity.sh")), str(self.extracted)], capture_output=True)
        self.assertEqual(result.returncode == 0, succeeds, result.stderr.decode())

    def editable_manifest(self):
        zone = Path(self.manifest["ZONEENGINE_ARTIFACT_DIR"])
        (zone / "CONTENT_MANIFEST.json").write_text('{"fixture":"editable"}\n')
        (zone / "GameData/PlayfieldContent/127").mkdir(parents=True, exist_ok=True)
        (zone / "GameData/PlayfieldContent/127/Npcs.json").write_text('{"SchemaVersion":1,"PlayfieldId":127,"Npcs":[],"StandaloneShops":[]}\n')
        return dict(self.manifest, FORMAT="3", ZONEENGINE_IMPLEMENTATION="new", CONTENT_PROVENANCE_VERSION="1",
                    CONTENT_MANIFEST_SHA256=package.sha((zone / "CONTENT_MANIFEST.json").read_bytes()),
                    CONTENT_FILE_COUNT="1", CONTENT_TOTAL_BYTES="12")

    def test_source_manifest_builder_accepts_editable_content_and_rejects_mutation(self):
        # A disposable synthetic repository exercises the exact clean-SHA builder.
        subprocess.run(["git", "init", str(self.source)], check=True, capture_output=True)
        subprocess.run(["git", "-C", str(self.source), "add", "."], check=True, capture_output=True)
        subprocess.run(["git", "-C", str(self.source), "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid",
                        "commit", "-m", "synthetic release fixture"], check=True, capture_output=True)
        source_sha = subprocess.check_output(["git", "-C", str(self.source), "rev-parse", "HEAD"], text=True).strip()
        zone = Path(self.manifest["ZONEENGINE_ARTIFACT_DIR"])
        (zone / "GameData/PlayfieldContent/127").mkdir(parents=True)
        (zone / "Content").mkdir()
        (zone / "GameData/PlayfieldContent/127/Npcs.json").write_text('{"SchemaVersion":1,"PlayfieldId":127,"Npcs":[],"StandaloneShops":[]}\n')
        output = subprocess.check_output(["python3", str(self.source / "LinuxBuild/Tools/content_provenance.py"),
                                          "write", str(zone), source_sha, "linux", ""], text=True)
        content = dict(line.split("=", 1) for line in output.splitlines())
        for key in package.ARTIFACTS:
            artifact = Path(self.manifest[key])
            (artifact / "SOURCE_SHA").write_text(source_sha + "\n")
            (artifact / "BUILD_PROVENANCE.env").write_text("COMMIT_SHA=" + source_sha + "\n" + (output if artifact == zone else ""))
            (artifact / "LINUX_ACCEPTANCE.env").write_text("LINUX_ACCEPTANCE=PASS\n" +
                ("CONTENT_VALIDATION=PASS\nCONTENT_ARCHITECTURE_GUARD=PASS\nCONTENT_MANIFEST_SHA256=" + content["CONTENT_MANIFEST_SHA256"] + "\n" if artifact == zone else ""))
        manifest_path = self.root / "release.manifest"
        command = ["bash", str(self.source / (package.PREFIX + "create-release-manifest.sh")), "--expected-sha", source_sha,
                   "--login-artifact-dir", self.manifest["LOGINENGINE_ARTIFACT_DIR"], "--zone-artifact-dir", str(zone),
                   "--login-unit", str(self.source / "LinuxBuild/deployment/systemd/ao-rebirth-loginengine.service"),
                   "--zone-unit", str(self.source / "LinuxBuild/deployment/systemd/ao-rebirth-zoneengine.service"),
                   "--output", str(manifest_path)]
        result = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        manifest = package.read_manifest(manifest_path)
        self.assertEqual(manifest["FORMAT"], "3")
        self.assertEqual(manifest["ZONEENGINE_IMPLEMENTATION"], "new")
        self.assertEqual(manifest["CONTENT_MANIFEST_SHA256"], content["CONTENT_MANIFEST_SHA256"])
        self.assertFalse(any(k.startswith("PLACEMENT_") for k in manifest))
        (zone / "GameData/PlayfieldContent/127/Npcs.json").write_text('{"changed":true}')
        self.assertNotEqual(subprocess.run(command, capture_output=True).returncode, 0)

    def test_editable_content_helper_and_python_dependency_included(self):
        for relative in ("LinuxBuild/content-provenance.sh", "LinuxBuild/Tools/content_provenance.py"):
            self.assertIn(relative, self.files)

    def test_missing_content_python_dependency_at_build(self):
        (self.source / "LinuxBuild/Tools/content_provenance.py").unlink()
        with self.assertRaisesRegex(ValueError, "missing dependency"):
            package.discover(self.source)

    def test_editable_manifest_recorded_without_official_placements(self):
        manifest = self.editable_manifest()
        files = package.package_files(self.source, manifest, "/srv/test-release", {})
        identity = json.loads(files[package.METADATA][0])
        self.assertEqual(identity["world_content"]["mode"], "editable")
        self.assertEqual(identity["world_content"]["manifest_sha256"], manifest["CONTENT_MANIFEST_SHA256"])
        self.assertFalse(any("Content/Official" in p for p in files))

    def test_editable_manifest_digest_mismatch_rejected(self):
        manifest = self.editable_manifest()
        manifest["CONTENT_MANIFEST_SHA256"] = "a" * 64
        with self.assertRaisesRegex(ValueError, "hash mismatch"):
            package.package_files(self.source, manifest, "/srv/test-release", {})

    def test_editable_manifest_wrong_mode_rejected(self):
        manifest = self.editable_manifest()
        manifest["ZONEENGINE_IMPLEMENTATION"] = "legacy"
        with self.assertRaisesRegex(ValueError, "manifest mode"):
            package.package_files(self.source, manifest, "/srv/test-release", {})

    def test_editable_content_file_changes_rejected_by_package_inventory(self):
        manifest = self.editable_manifest()
        files = package.package_files(self.source, manifest, "/srv/test-release", {})
        for relative, (data, mode, _) in files.items():
            target = self.extracted / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            target.chmod(mode)
        (self.extracted / package.ARTIFACTS["ZONEENGINE_ARTIFACT_DIR"] / "GameData/PlayfieldContent/127/Npcs.json").write_text('{}')
        self.guard(False)

    def test_complete_extracted_entry_point(self):
        self.guard(True)
        subprocess.run(["bash", str(self.extracted / package.ENTRY), "--help"], check=True, capture_output=True, cwd=self.extracted)

    def test_missing_placement_helper(self):
        (self.extracted / "LinuxBuild/placement-provenance.sh").unlink()
        self.guard(False)
        result = subprocess.run(["bash", str(self.extracted / package.ENTRY), "--help"], capture_output=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b"placement-provenance.sh", result.stderr)

    def test_missing_transitive_helper_at_build(self):
        (self.source / "LinuxBuild/placement-provenance.sh").unlink()
        with self.assertRaisesRegex(ValueError, "missing dependency"):
            package.discover(self.source)

    def test_transitive_discovery_is_generic(self):
        helper = self.source / "LinuxBuild/placement-provenance.sh"
        helper.write_text('source "${SCRIPT_DIR}/extra-helper.sh"\n')
        (helper.parent / "extra-helper.sh").write_text("# fixture\n")
        self.assertIn("LinuxBuild/extra-helper.sh", package.discover(self.source))

    def test_unresolved_dependency_fails_closed(self):
        (self.source / "LinuxBuild/placement-provenance.sh").write_text('source "${UNKNOWN}/helper.sh"\n')
        with self.assertRaisesRegex(ValueError, "unresolved dependency"):
            package.discover(self.source)

    def test_dependency_outside_root_fails(self):
        (self.source / "LinuxBuild/placement-provenance.sh").write_text('source "${SCRIPT_DIR}/../../../outside.sh"\n')
        with self.assertRaises(ValueError):
            package.discover(self.source)

    def test_missing_executable_permission(self):
        (self.extracted / "LinuxBuild/placement-provenance.sh").chmod(0o644)
        self.guard(False)

    def test_changed_dll_is_rejected(self):
        (self.extracted / next(p for p in self.files if p.endswith("ZoneEngine_New.dll"))).write_bytes(b"changed")
        self.guard(False)

    def test_symlink_rejected(self):
        path = self.extracted / "LinuxBuild/placement-provenance.sh"
        path.unlink()
        path.symlink_to(self.source / "LinuxBuild/placement-provenance.sh")
        self.guard(False)

    def test_inventory_traversal_rejected(self):
        path = self.extracted / package.INVENTORY
        path.write_text(path.read_text() + "a" * 64 + "\t0644\tLinuxBuild/../../outside\n")
        self.guard(False)

    def test_archive_deterministic_and_extracted(self):
        first, second = self.root / "a.tar.gz", self.root / "b.tar.gz"
        package.write_archive(self.files, first, 1234567890)
        package.write_archive(self.files, second, 1234567890)
        self.assertEqual(first.read_bytes(), second.read_bytes())
        package.verify_archive(first, self.files)

    def test_archive_omission_rejected(self):
        damaged = dict(self.files)
        del damaged["LinuxBuild/placement-provenance.sh"]
        archive = self.root / "broken.tar.gz"
        package.write_archive(damaged, archive, 1234567890)
        with self.assertRaisesRegex(ValueError, "file set"):
            package.verify_archive(archive, self.files)

    def test_uninventoried_file_rejected(self):
        (self.extracted / "LinuxBuild/extra.sh").write_text("# unexpected\n")
        self.guard(False)

    def test_installed_dll_mismatch_rejected_before_start(self):
        login, zone = self.root / "installed-login", self.root / "installed-zone"
        shutil.copytree(self.extracted / package.ARTIFACTS["LOGINENGINE_ARTIFACT_DIR"], login)
        shutil.copytree(self.extracted / package.ARTIFACTS["ZONEENGINE_ARTIFACT_DIR"], zone)
        command = ['bash', '-c', 'source "$1"; package_verify_installed "$2" "$3" "$4"', 'installed-check',
                   str(self.extracted / (package.PREFIX + "package-integrity.sh")), str(self.extracted), str(login), str(zone)]
        self.assertEqual(subprocess.run(command, capture_output=True).returncode, 0)
        (zone / "ZoneEngine_New.dll").write_bytes(b"damaged installation")
        self.assertNotEqual(subprocess.run(command, capture_output=True).returncode, 0)


if __name__ == "__main__":
    unittest.main()
