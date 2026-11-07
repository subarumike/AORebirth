"""Focused source-only package-tool receipts; no builds, engines or databases."""
from contextlib import redirect_stdout
import importlib.util
import io
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("windows_package_release_contract", ROOT / "tools/windows_release_contract.py")
gate = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(gate)
PROJECT = "Tools/WindowsLocalPackage/Tests/WindowsLocalPackageValidation/WindowsLocalPackageValidation.csproj"
ASSEMBLY = "Tools/WindowsLocalPackage/Tests/WindowsLocalPackageValidation/bin/Release/net48/WindowsLocalPackageValidation.exe"


class WindowsPackageFeatureValidationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        canonical = self.root / "canonical"
        canonical.mkdir()
        (canonical / "one.json").write_bytes(b"{}")
        project = self.root / PROJECT
        project.parent.mkdir(parents=True)
        project.write_text("<Project />", encoding="utf-8")
        self.profile = {"STATE_ROOT": str(self.root / "ignored"), "CANONICAL_GAMEDATA_ROOT": str(canonical)}
        self.source = {"HEAD": "a" * 40, "BRANCH": "codex/package-fixture",
                       "TRACKED_DIFF_SHA256": gate.digest(b"draft"), "UNTRACKED_SOURCE": []}
        self.child = ["dotnet", "run", "--project", PROJECT, "--configuration", "Release", "--", "--launcher", "fixture.exe"]

    def receipt(self):
        return gate.read_json(next((self.root / "ignored").glob("feature-validation-*/feature-validation.json")))

    def run_validation(self, child, action):
        with mock.patch.object(gate, "feature_source", return_value=self.source), \
                mock.patch.object(gate.subprocess, "run", side_effect=action), redirect_stdout(io.StringIO()):
            return gate.run_feature_validation(self.root, self.profile, child)

    def test_exact_package_tool_records_measured_exe_without_product_hooks_or_release_acceptance(self):
        data = b"synthetic compiled offline package validation assembly"

        def child(command, cwd, env, check):
            self.assertEqual(command, self.child)
            self.assertEqual(cwd, self.root)
            active = gate.read_json(Path(env["AO_REBIRTH_BUILD_STATE"]))
            self.assertEqual(active["BUILD_PURPOSE"], "FEATURE_VALIDATION")
            output = self.root / ASSEMBLY
            output.parent.mkdir(parents=True)
            output.write_bytes(data)
            with mock.patch.dict(gate.os.environ, env):
                with self.assertRaisesRegex(ValueError, "cannot publish"):
                    gate.verify_build_token(self.root, self.profile, product="WorldEngine", publishing=True)
            return subprocess.CompletedProcess(command, 0)

        self.assertEqual(self.run_validation(self.child, child), 0)
        receipt = self.receipt()
        self.assertEqual(receipt["VALIDATION_RESULT"], "PASS")
        self.assertEqual(receipt["PRODUCT_COMPILER_HOOKS"], [])
        self.assertEqual(receipt["SOURCE_ONLY_VALIDATION_TOOL"], {
            "path": ASSEMBLY, "bytes": len(data), "sha256": gate.digest(data),
            "METHOD": "EXACT_WINDOWS_PACKAGE_TOOL_COMPILES_OFFLINE_VALIDATION_SOURCES"})
        self.assertIn({"path": ASSEMBLY, "bytes": len(data), "sha256": gate.digest(data)}, receipt["AVAILABLE_OUTPUTS_POST_COMMAND"])
        self.assertFalse(receipt["DEPLOYABLE"])
        self.assertEqual(receipt["WINDOWS_USER_ACCEPTED"], "NO")
        self.assertEqual(receipt["FEATURE_SOURCE"], self.source)
        self.assertTrue(receipt["SELECTED_GAMEDATA_MANIFEST"])
        self.assertFalse(next((self.root / "ignored").glob("feature-validation-*")).joinpath("active.json").exists())

    def test_package_tool_requires_nonempty_stable_actual_output(self):
        for kind in ("missing", "empty", "changed"):
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary).resolve()
                project = root / PROJECT
                project.parent.mkdir(parents=True)
                project.write_text("<Project />", encoding="utf-8")
                canonical = root / "canonical"
                canonical.mkdir()
                (canonical / "one.json").write_bytes(b"{}")
                profile = {"STATE_ROOT": str(root / "ignored"), "CANONICAL_GAMEDATA_ROOT": str(canonical)}
                output = root / ASSEMBLY
                original_file_state = gate.file_state
                observations = 0

                def measured_state(path):
                    nonlocal observations
                    state = original_file_state(path)
                    if Path(path) == output:
                        observations += 1
                        if kind == "changed" and observations > 1:
                            return (*state[:-1], state[-1] + 1)
                    return state

                def child(command, cwd, env, check):
                    if kind != "missing":
                        output.parent.mkdir(parents=True)
                        output.write_bytes(b"" if kind == "empty" else b"synthetic tool assembly")
                    return subprocess.CompletedProcess(command, 0)

                with mock.patch.object(gate, "feature_source", return_value=self.source), \
                        mock.patch.object(gate, "file_state", side_effect=measured_state), \
                        mock.patch.object(gate.subprocess, "run", side_effect=child), redirect_stdout(io.StringIO()):
                    with self.assertRaises((ValueError, FileNotFoundError)):
                        gate.run_feature_validation(root, profile, self.child)
                receipt = gate.read_json(next((root / "ignored").glob("feature-validation-*/feature-validation.json")))
                self.assertEqual(receipt["VALIDATION_RESULT"], "FAIL")
                self.assertFalse(receipt["DEPLOYABLE"])

    def test_other_validation_project_still_requires_real_product_compiler_observations(self):
        unknown = "Tools/OtherValidation/OtherValidation.csproj"
        project = self.root / unknown
        project.parent.mkdir(parents=True)
        project.write_text("<Project />", encoding="utf-8")
        commands = (["dotnet", "run", "--project", unknown, "--configuration", "Release", "--"],
                    ["dotnet", "run", "--project", PROJECT, "--configuration", "Debug", "--"])

        def child(command, cwd, env, check):
            (Path(env["AO_REBIRTH_BUILD_STATE"]).parent / "build-observations").mkdir()
            return subprocess.CompletedProcess(command, 0)

        self.assertEqual(gate.feature_command(self.root, commands[0]), project)
        for command in commands:
            with self.subTest(command=command):
                with self.assertRaisesRegex(ValueError, "did not observe the product compiler guard"):
                    self.run_validation(command, child)
        for path in (self.root / "ignored").glob("feature-validation-*/feature-validation.json"):
            receipt = gate.read_json(path)
            self.assertEqual(receipt["VALIDATION_RESULT"], "FAIL")
            self.assertNotIn("SOURCE_ONLY_VALIDATION_TOOL", receipt)
            self.assertFalse(receipt["DEPLOYABLE"])


if __name__ == "__main__":
    unittest.main()
