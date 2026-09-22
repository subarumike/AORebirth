"""Verify fresh-build retirement without changing historical recovery controls."""
import hashlib
import json
from pathlib import Path
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]
# Normalized controls from private operations f91a762; no historical Git objects required.
RECOVERY_BASELINE = {
    "LinuxBuild/deployment/production-release/upgrade-active-services.sh": "f60abe6d8edf61376ef72e3bfee70ff790bbc749e2db63413a562c929e03f785",
    "LinuxBuild/deployment/systemd/ao-rebirth-zoneengine-legacy.service": "a40f3f2f747089e91595dba312d3a977ff587c7c8b481f277ac773efc09844fc",
    "LinuxBuild/deployment/zone-stage9/upgrade-live-service.sh": "8c95ef70be4729579d15b9278c7815f40c4d7f49b035ef25892fd2557f9523db",
    "LinuxBuild/placement-provenance.sh": "c01b705bf56bea86413a2b0caa86d2c020e5d1e2bd35d00a3d25a8c5b5707bc3",
}

class EngineRetirementTests(unittest.TestCase):
    def test_no_fresh_legacy_engine_build_inputs(self):
        self.assertFalse((ROOT / "LinuxBuild/Projects/ZoneEngine.Linux.csproj").exists())
        self.assertNotIn("ZoneEngine.Linux.csproj", (ROOT / "LinuxBuild/AORebirth.Linux.slnx").read_text())
        inventory = json.loads((ROOT / "LinuxBuild/source-inventory/inventory.json").read_text())
        self.assertNotIn("AORebirth/Server/ZoneEngine/ZoneEngine.csproj", [p["legacyProject"] for p in inventory["projects"]])
    def test_newengine_only_publication(self):
        for extension in ("sh", "cmd"):
            source = (ROOT / ("LinuxBuild/publish-zoneengine." + extension)).read_text()
            self.assertIn("ZoneEngine_New", source)
            self.assertNotIn("ZoneEngine.Linux.csproj", source)
            self.assertNotIn("Stage8OfflineSmokeTests", source)
            self.assertIn("BackendIntegrationGuard", source)
            self.assertIn("content_provenance", source)
    def test_retired_selector_fails_before_build(self):
        for selector in ("legacy", "new"):
            result = subprocess.run(["bash", str(ROOT / "LinuxBuild/publish-zoneengine.sh"), "linux-x64", "true", selector], text=True, capture_output=True)
            self.assertEqual(2, result.returncode)
            self.assertIn("Engine selection is retired", result.stderr)
    def test_deleted_engine_has_no_private_patch_or_export_dependency(self):
        self.assertFalse((ROOT / "PRIVATE_BUILD_INPUTS.json").exists())
        self.assertFalse((ROOT / "patches/platform-compatibility.patch").exists())
        self.assertFalse((ROOT / "assemble_private_source.py").exists())
        self.assertFalse((ROOT / "AORebirth/Server/ZoneEngine").exists())
        self.assertNotIn("AORebirth/Server/ZoneEngine/", (ROOT / "LinuxBuild/source-inventory/AORebirth.Core.CompileItems.props").read_text())
    def test_historical_recovery_controls_are_unchanged(self):
        for name, expected in RECOVERY_BASELINE.items():
            actual = (ROOT / name).read_bytes().replace(b"\r\n", b"\n")
            self.assertEqual(expected, hashlib.sha256(actual).hexdigest(), name)
    def test_retained_login_handoff_coverage_is_active(self):
        project = (ROOT / "LinuxBuild/Tools/CompatibilitySmokeTests/CompatibilitySmokeTests.csproj").read_text()
        program = (ROOT / "LinuxBuild/Tools/CompatibilitySmokeTests/Program.cs").read_text()
        self.assertIn("LoginHandoffLifecycle.cs", project)
        self.assertIn("LoginHandoffLifecycleTests.Run()", program)
        self.assertTrue((ROOT / "LinuxBuild/Tools/CompatibilitySmokeTests/LoginHandoffLifecycleTests.cs").is_file())

if __name__ == "__main__":
    unittest.main()
