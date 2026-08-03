from __future__ import annotations

import importlib.util
import json
import os
import shutil
import struct
import subprocess
import sys
import tempfile
import time
import unittest
import zlib
from pathlib import Path
from unittest import mock

from Tools import generated_combat_pipeline as pipeline
from Tools import generated_artifact_transaction as transaction


class GeneratedCombatPipelineTests(unittest.TestCase):
    @staticmethod
    def _load_module_from_path(name: str, path: Path):
        specification = importlib.util.spec_from_file_location(name, path)
        if specification is None or specification.loader is None:
            raise AssertionError(f"could not load test module: {path}")
        module = importlib.util.module_from_spec(specification)
        sys.modules[name] = module
        try:
            specification.loader.exec_module(module)
        finally:
            sys.modules.pop(name, None)
        return module

    def test_formula_item_loader_retains_only_requested_templates(self):
        formula = self._load_module_from_path(
            "formula_item_loader_test",
            Path("tools-temp/AOSharpCaptureAnalyzer/analyze_enemy_combat_setup_formula.py"),
        )

        def pack(value):
            if isinstance(value, int) and 0 <= value <= 0x7F:
                return bytes((value,))
            if isinstance(value, list) and len(value) <= 0x0F:
                return bytes((0x90 | len(value),)) + b"".join(pack(row) for row in value)
            raise AssertionError(f"unsupported fixture value: {value!r}")

        slices = (
            [[1, 0, 0, [], 0, 101], [2, 0, 0, [], 0, 102]],
            [[3, 0, 0, [], 0, 101]],
        )
        payload = bytearray(b"\x01v")
        payload.extend(struct.pack("<iii", 0, 0, len(slices)))
        for rows in slices:
            compressor = zlib.compressobj(level=9)
            compressed = compressor.compress(pack(rows)) + compressor.flush(
                zlib.Z_SYNC_FLUSH
            )
            payload.extend(struct.pack("<i", len(compressed)))
            payload.extend(compressed)

        with tempfile.TemporaryDirectory() as temporary:
            items = Path(temporary) / "items.dat"
            items.write_bytes(payload)
            selected = formula.load_item_templates(items, {101})
            unfiltered = formula.load_item_templates(items)

        self.assertEqual(set(selected), {101})
        self.assertEqual(selected[101][0], 3)
        self.assertEqual(set(unfiltered), {101, 102})

    def test_formula_item_loader_rejects_truncated_or_trailing_data(self):
        formula = self._load_module_from_path(
            "formula_item_loader_validation_test",
            Path("tools-temp/AOSharpCaptureAnalyzer/analyze_enemy_combat_setup_formula.py"),
        )

        def database(compressed: bytes, declared_size: int | None = None) -> bytes:
            return (
                b"\x01v"
                + struct.pack("<iii", 0, 0, 1)
                + struct.pack(
                    "<i", len(compressed) if declared_size is None else declared_size
                )
                + compressed
            )

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            truncated = root / "truncated.dat"
            compressed = zlib.compress(b"\x91\x96\x00\x00\x00\x90\x00\x65")
            truncated.write_bytes(database(compressed, len(compressed) + 1))
            with self.assertRaisesRegex(ValueError, "truncated compressed payload"):
                formula.load_item_templates(truncated, {101})

            trailing = root / "trailing.dat"
            trailing.write_bytes(database(zlib.compress(b"\x90\x00")))
            with self.assertRaisesRegex(ValueError, "trailing MessagePack data"):
                formula.load_item_templates(trailing, set())

            invalid_zlib = root / "invalid-zlib.dat"
            invalid_zlib.write_bytes(database(b"not-zlib"))
            with self.assertRaisesRegex(ValueError, "invalid zlib data"):
                formula.load_item_templates(invalid_zlib, set())

            non_array = root / "non-array.dat"
            non_array.write_bytes(database(zlib.compress(b"\x00")))
            with self.assertRaisesRegex(ValueError, "root must be an array"):
                formula.load_item_templates(non_array, set())

            malformed_template = root / "malformed-template.dat"
            malformed_template.write_bytes(database(zlib.compress(b"\x91\x91\x00")))
            with self.assertRaisesRegex(ValueError, "slice 0 is invalid"):
                formula.load_item_templates(malformed_template, set())

            trailing_database = root / "trailing-database.dat"
            trailing_database.write_bytes(database(zlib.compress(b"\x90")) + b"\x00")
            with self.assertRaisesRegex(ValueError, "trailing data after"):
                formula.load_item_templates(trailing_database, set())

    def test_generated_json_rejects_checkout_absolute_paths(self):
        portable = {"path": "tools-temp/captures/20260701-000001/packets.hex.log"}
        pipeline._validate_json_bytes(pipeline.canonical_json_bytes(portable))
        pipeline._validate_json_bytes(
            pipeline.canonical_json_bytes({"url": "https://example.invalid/a/b"})
        )
        with self.assertRaisesRegex(
            pipeline.CohortValidationError,
            "repository-location-dependent",
        ):
            pipeline._validate_json_bytes(
                pipeline.canonical_json_bytes(
                    {"path": r"C:\\checkout\\AORebirth\\packets.hex.log"}
                )
            )
        with self.assertRaisesRegex(
            pipeline.CohortValidationError,
            "repository-location-dependent",
        ):
            pipeline._validate_json_bytes(
                pipeline.canonical_json_bytes(
                    {
                        "diagnostic": (
                            r"decode failed at C:\\checkout\\AORebirth\\packets.hex.log"
                        )
                    }
                )
            )
    def _write_fixture_artifacts(self, root: Path) -> dict[str, Path]:
        artifacts = {
            role: root / Path(relative)
            for role, relative in pipeline.ARTIFACT_RELATIVE_PATHS.items()
        }
        documents = {
            "inventory": {
                "schemaVersion": 1,
                "summary": {
                    "captureSessionsDiscovered": 7,
                    "canonicalValidSessions": 6,
                    "completeAttackInfoChains": 11,
                    "captureCertifiedProfiles": 2,
                    "runtimeReadyProfiles": 2,
                    "captureCertifiedSemanticDefinitions": 4,
                    "runtimeReadyGeneratedSemanticDefinitions": 3,
                    "unresolvedProfiles": 3,
                    "decodeOrProjectionErrors": 0,
                },
            },
            "activeCoverage": {
                "schemaVersion": 1,
                "totals": {
                    "initialActorCount": 5,
                    "certified": 2,
                    "unresolved": 3,
                },
                "profiles": [],
            },
            "formulaDataset": {
                "schemaVersion": 1,
                "profiles": [],
                "acceptedFormula": {"activeBindings": []},
                "stimFiendFormula": {"activeBindings": []},
                "meldedPatternsFormula": {"activeBindings": []},
                "fragmentedSoulFormula": {"activeBindings": []},
                "incompleteRebuildFormula": {"activeBindings": []},
                "molestedMoleculesFormula": {"activeBindings": []},
                "fixedScopeSelectorBindings": {"activeBindings": []},
            },
        }
        for role, path in artifacts.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            if role in documents:
                path.write_bytes(pipeline.canonical_json_bytes(documents[role]))
            else:
                path.write_text(f"fixture-{role}\n", encoding="utf-8", newline="\n")
        return artifacts

    def _generator_descriptors(self, reverse: bool = False):
        items = list(pipeline.GENERATOR_PATHS.items())
        if reverse:
            items.reverse()
        stable_indexes = {
            name: index + 1
            for index, name in enumerate(sorted(pipeline.GENERATOR_PATHS))
        }
        return {
            name: {
                "path": path.as_posix(),
                "sha256": (str(stable_indexes[name]) * 64)[:64],
                "byteLength": stable_indexes[name],
            }
            for name, path in items
        }

    @staticmethod
    def _runtime_descriptor():
        return {
            "implementation": "CPython",
            "version": "3.test",
            "executableSha256": "f" * 64,
            "executableByteLength": 123,
        }

    @staticmethod
    def _snapshot():
        plan_core = {
            "schemaVersion": 1,
            "generatorSources": [],
            "captures": [],
        }
        core = {
            **plan_core,
            "planIdentity": pipeline.sha256_bytes(
                pipeline.identity_json_bytes(plan_core)
            ),
        }
        return {
            **core,
            "snapshotIdentity": pipeline.sha256_bytes(
                pipeline.identity_json_bytes(core)
            ),
        }

    def _complete_cohort(self, root: Path) -> tuple[dict[str, Path], dict]:
        artifacts = self._write_fixture_artifacts(root)
        manifest, rendered = pipeline.build_generation_manifest(
            cohort_root=root,
            artifacts=artifacts,
            input_snapshot=self._snapshot(),
            auxiliary_input_identity="c" * 64,
            generators=self._generator_descriptors(),
            runtime=self._runtime_descriptor(),
        )
        manifest_path = root / Path(pipeline.MANIFEST_RELATIVE_PATH)
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_bytes(rendered)
        return artifacts, manifest

    def test_active_formula_pair_reaches_fixed_point(self):
        states = (
            pipeline.PairState(b"active-1", b"formula-1"),
            pipeline.PairState(b"active-2", b"formula-2"),
            pipeline.PairState(b"active-2", b"formula-2"),
        )

        result = pipeline.iterate_pair_to_fixed_point(
            lambda _previous, round_number: states[round_number - 1],
            pipeline.PairState(b"{}\n", b"{}\n"),
            max_rounds=4,
        )

        self.assertEqual(3, result.rounds)
        self.assertEqual(states[-1], result.state)

    def test_active_formula_cycle_is_rejected(self):
        initial = pipeline.PairState(b"active-0", b"formula-0")
        other = pipeline.PairState(b"active-1", b"formula-1")

        with self.assertRaisesRegex(pipeline.FixedPointError, "cycle"):
            pipeline.iterate_pair_to_fixed_point(
                lambda _previous, round_number: other if round_number == 1 else initial,
                initial,
                max_rounds=3,
            )

    def test_active_formula_max_rounds_is_rejected(self):
        with self.assertRaisesRegex(pipeline.FixedPointError, "did not converge"):
            pipeline.iterate_pair_to_fixed_point(
                lambda _previous, round_number: pipeline.PairState(
                    f"active-{round_number}".encode(),
                    f"formula-{round_number}".encode(),
                ),
                pipeline.PairState(b"active-0", b"formula-0"),
                max_rounds=2,
            )

    def test_manifest_is_hash_seed_order_and_location_invariant(self):
        with tempfile.TemporaryDirectory() as first_name, tempfile.TemporaryDirectory() as second_name:
            first = Path(first_name)
            second = Path(second_name)
            first_artifacts = self._write_fixture_artifacts(first)
            second_artifacts = self._write_fixture_artifacts(second)
            first_manifest, first_bytes = pipeline.build_generation_manifest(
                cohort_root=first,
                artifacts=first_artifacts,
                input_snapshot=self._snapshot(),
                auxiliary_input_identity="c" * 64,
                generators=self._generator_descriptors(),
                runtime=self._runtime_descriptor(),
            )
            second_manifest, second_bytes = pipeline.build_generation_manifest(
                cohort_root=second,
                artifacts=dict(reversed(list(second_artifacts.items()))),
                input_snapshot=dict(reversed(list(self._snapshot().items()))),
                auxiliary_input_identity="c" * 64,
                generators=self._generator_descriptors(reverse=True),
                runtime=dict(reversed(list(self._runtime_descriptor().items()))),
            )

        self.assertEqual(first_bytes, second_bytes)
        self.assertEqual(
            first_manifest["generationIdentity"],
            second_manifest["generationIdentity"],
        )
        self.assertNotIn(first_name.encode(), first_bytes)
        self.assertNotIn(second_name.encode(), second_bytes)

    def test_mixed_identity_artifact_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifacts, _manifest = self._complete_cohort(root)
            pipeline.validate_cohort(root, verify_toolchain=False)
            artifacts["catalog"].write_text(
                "fixture-catalog-from-another-generation\n",
                encoding="utf-8",
                newline="\n",
            )

            with self.assertRaisesRegex(pipeline.CohortValidationError, "stale or mixed"):
                pipeline.validate_cohort(root, verify_toolchain=False)

    def test_partial_manifest_json_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._complete_cohort(root)
            manifest_path = root / Path(pipeline.MANIFEST_RELATIVE_PATH)
            manifest_path.write_text(
                '{"schemaVersion":1,"pipeline":',
                encoding="utf-8",
                newline="\n",
            )

            with self.assertRaisesRegex(pipeline.CohortValidationError, "valid UTF-8 JSON"):
                pipeline.validate_cohort(root, verify_toolchain=False)

    def test_partial_artifact_json_is_rejected_even_when_descriptor_matches(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifacts, manifest = self._complete_cohort(root)
            partial = b'{"schemaVersion":1,"totals":'
            artifacts["activeCoverage"].write_bytes(partial)
            for row in manifest["artifacts"]:
                if row["role"] == "activeCoverage":
                    row["sha256"] = pipeline.sha256_bytes(partial)
                    row["byteLength"] = len(partial)
            manifest["generationIdentity"] = pipeline.sha256_bytes(
                pipeline.identity_json_bytes(
                    pipeline._manifest_identity_payload(manifest)
                )
            )
            (root / Path(pipeline.MANIFEST_RELATIVE_PATH)).write_bytes(
                pipeline.canonical_json_bytes(manifest)
            )

            with self.assertRaisesRegex(pipeline.CohortValidationError, "valid UTF-8 JSON"):
                pipeline.validate_cohort(root, verify_toolchain=False)

    def test_dirty_detection_names_only_changed_targets(self):
        with tempfile.TemporaryDirectory() as published_name, tempfile.TemporaryDirectory() as candidate_name:
            published = Path(published_name)
            candidate = Path(candidate_name)
            self._complete_cohort(published)
            self._complete_cohort(candidate)
            self.assertEqual([], pipeline.cohort_differences(candidate, published))
            changed = candidate / Path(
                pipeline.ARTIFACT_RELATIVE_PATHS["formulaDataset"]
            )
            changed.write_bytes(changed.read_bytes() + b"\n")

            differences = pipeline.cohort_differences(candidate, published)

        self.assertEqual(
            [pipeline.ARTIFACT_RELATIVE_PATHS["formulaDataset"].as_posix()],
            differences,
        )

    def test_shared_transaction_publishes_and_read_lease_validates_fixture(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".git").mkdir()
            candidate_root = root / "candidate"
            candidate_root.mkdir()
            artifacts, manifest = self._complete_cohort(candidate_root)
            candidate = pipeline.CandidateCohort(
                root=candidate_root,
                artifacts=artifacts,
                manifest_path=candidate_root / Path(pipeline.MANIFEST_RELATIVE_PATH),
                capture_snapshot=self._snapshot(),
                generation_identity=manifest["generationIdentity"],
                input_snapshot_identity=manifest["inputSnapshot"]["identity"],
                fixed_point_rounds=1,
            )

            with pipeline._shared_lease(root, "write") as lease:
                transaction_identity = pipeline._shared_publish(
                    lease, candidate, lambda _phase: None
                )
            with pipeline._shared_lease(root, "read"):
                published = pipeline.validate_cohort(root, verify_toolchain=False)

        self.assertRegex(transaction_identity, r"^[0-9a-f]{32}$")
        self.assertEqual(candidate.generation_identity, published["generationIdentity"])

    def test_absolute_or_volatile_manifest_values_are_rejected(self):
        with self.assertRaisesRegex(pipeline.CohortValidationError, "absolute"):
            pipeline.assert_manifest_is_path_independent(
                {"path": r"C:\\Users\\fixture\\candidate.json"}
            )
        with self.assertRaisesRegex(pipeline.CohortValidationError, "volatile"):
            pipeline.assert_manifest_is_path_independent({"pid": 123})

    def test_read_lease_supervisor_preserves_command_after_separator(self):
        arguments = pipeline.parse_arguments(
            ["--run-read-lease", "--", "Tools\\build_aorebirth_debug.cmd", "arg"]
        )
        self.assertTrue(arguments.run_read_lease)
        self.assertEqual(
            ["--", "Tools\\build_aorebirth_debug.cmd", "arg"],
            arguments.command,
        )

    def test_frozen_candidate_tamper_is_rejected_without_changing_published_cohort(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".git").mkdir()
            self._complete_cohort(root)
            governed_paths = tuple(pipeline.ARTIFACT_RELATIVE_PATHS.values()) + (
                pipeline.MANIFEST_RELATIVE_PATH,
            )
            published_before = {
                path.as_posix(): (root / Path(path)).read_bytes()
                for path in governed_paths
            }

            candidate_root = root / "candidate"
            artifacts, manifest = self._complete_cohort(candidate_root)
            candidate = pipeline.CandidateCohort(
                root=candidate_root,
                artifacts=artifacts,
                manifest_path=candidate_root / Path(pipeline.MANIFEST_RELATIVE_PATH),
                capture_snapshot=self._snapshot(),
                generation_identity=manifest["generationIdentity"],
                input_snapshot_identity=manifest["inputSnapshot"]["identity"],
                fixed_point_rounds=1,
            )
            original = artifacts["catalog"].read_bytes()
            artifacts["catalog"].write_bytes(
                bytes((original[0] ^ 1,)) + original[1:]
            )

            with transaction.GeneratedArtifactLease(
                root, pipeline.PIPELINE_NAME, mode="write", timeout_seconds=0
            ) as lease:
                transaction.ArtifactTransaction.recover(lease)
                with self.assertRaisesRegex(
                    pipeline.CohortValidationError, "stale or mixed"
                ):
                    pipeline._shared_publish(lease, candidate, lambda _phase: None)

            published_after = {
                path.as_posix(): (root / Path(path)).read_bytes()
                for path in governed_paths
            }
            self.assertEqual(published_before, published_after)
            pipeline.validate_cohort(root, verify_toolchain=False)

    def test_capture_snapshot_plan_and_snapshot_identities_are_strict(self):
        valid = self._snapshot()
        failures = {
            "plan": {**valid, "planIdentity": "0" * 64},
            "snapshot": {**valid, "snapshotIdentity": "0" * 64},
        }
        for label, snapshot in failures.items():
            with self.subTest(identity=label):
                with self.assertRaisesRegex(
                    pipeline.CohortValidationError, "identity does not match"
                ):
                    pipeline._portable_snapshot_descriptor(snapshot, "c" * 64)

    def test_lease_delegation_rejects_missing_and_forged_but_accepts_live(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".git").mkdir()
            with mock.patch.dict(os.environ, {}, clear=False):
                os.environ.pop(pipeline.LEASE_DELEGATION_ENVIRONMENT, None)
                with self.assertRaisesRegex(pipeline.PipelineError, "missing"):
                    pipeline._validate_delegated_lease(root, "read")

                with transaction.GeneratedArtifactLease(
                    root,
                    pipeline.PIPELINE_NAME,
                    mode="read",
                    timeout_seconds=0,
                ) as lease:
                    live = lease.delegation()
                    os.environ[pipeline.LEASE_DELEGATION_ENVIRONMENT] = json.dumps(live)
                    pipeline._validate_delegated_lease(root, "read")

                    forged = {**live, "token": "0" * 64}
                    os.environ[pipeline.LEASE_DELEGATION_ENVIRONMENT] = json.dumps(
                        forged
                    )
                    with self.assertRaisesRegex(pipeline.PipelineError, "invalid"):
                        pipeline._validate_delegated_lease(root, "read")

    def test_run_checked_timeout_terminates_parent_and_descendant(self):
        child_source = "\n".join(
            (
                "import os, sys, time",
                "from pathlib import Path",
                "Path(sys.argv[1]).write_text(str(os.getpid()), encoding='ascii')",
                "time.sleep(60)",
            )
        )
        parent_source = "\n".join(
            (
                "import os, subprocess, sys, time",
                "from pathlib import Path",
                "parent_path = Path(sys.argv[1])",
                "child_path = Path(sys.argv[2])",
                "child = subprocess.Popen([sys.executable, '-c', sys.argv[3], str(child_path)])",
                "parent_path.write_text(str(os.getpid()), encoding='ascii')",
                "deadline = time.monotonic() + 5",
                "while not child_path.exists() and time.monotonic() < deadline:",
                "    time.sleep(0.01)",
                "time.sleep(60)",
            )
        )
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".git").mkdir()
            parent_pid_path = root / "parent.pid"
            child_pid_path = root / "child.pid"
            with transaction.GeneratedArtifactLease(
                root, pipeline.PIPELINE_NAME, mode="read", timeout_seconds=0
            ) as lease, mock.patch.object(
                pipeline, "CHILD_PROCESS_TIMEOUT_SECONDS", 2.0
            ):
                with self.assertRaisesRegex(pipeline.PipelineError, "timed out"):
                    pipeline.run_checked(
                        (
                            sys.executable,
                            "-c",
                            parent_source,
                            str(parent_pid_path),
                            str(child_pid_path),
                            child_source,
                        ),
                        repo_root=root,
                        lease=lease,
                    )

            self.assertTrue(parent_pid_path.is_file())
            self.assertTrue(child_pid_path.is_file())
            pids = (
                int(parent_pid_path.read_text(encoding="ascii")),
                int(child_pid_path.read_text(encoding="ascii")),
            )
            deadline = time.monotonic() + 5
            while any(transaction._pid_alive(pid) for pid in pids) and time.monotonic() < deadline:
                time.sleep(0.025)
            self.assertFalse(transaction._pid_alive(pids[0]), "timed-out parent survived")
            self.assertFalse(transaction._pid_alive(pids[1]), "timed-out child survived")

    def test_transaction_publish_breaks_existing_hardlink_without_mutating_peer(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".git").mkdir()
            target = root / "target.txt"
            peer = root / "peer.txt"
            target.write_bytes(b"published-before\n")
            os.link(target, peer)
            with transaction.GeneratedArtifactLease(
                root, "hardlink-fixture", mode="write", timeout_seconds=0
            ) as lease:
                transaction.ArtifactTransaction.recover(lease)
                transaction.ArtifactTransaction.publish(
                    lease,
                    {"target.txt": b"published-after\n"},
                    artifact_order=("target.txt",),
                    commit_marker="target.txt",
                )
            self.assertEqual(b"published-after\n", target.read_bytes())
            self.assertEqual(b"published-before\n", peer.read_bytes())

    def test_direct_readers_reject_other_checkout_lease_even_through_hardlink_alias(self):
        repository_root = Path(pipeline.__file__).resolve().parents[1]
        readers = (
            (
                "active",
                repository_root
                / "tools-temp"
                / "AOSharpCaptureAnalyzer"
                / "generate_capture_backed_npc_active_coverage.py",
                "CoverageError",
            ),
            (
                "formula",
                repository_root
                / "tools-temp"
                / "AOSharpCaptureAnalyzer"
                / "analyze_enemy_combat_setup_formula.py",
                "RuntimeError",
            ),
        )
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            lease_root = root / "lease-checkout"
            checkout_root = root / "reader-checkout"
            (lease_root / ".git").mkdir(parents=True)
            (checkout_root / ".git").mkdir(parents=True)

            loaded = []
            hardlink_aliases = 0
            for name, source, _error_name in readers:
                alias = (
                    checkout_root
                    / "tools-temp"
                    / "AOSharpCaptureAnalyzer"
                    / source.name
                )
                alias.parent.mkdir(parents=True, exist_ok=True)
                try:
                    os.link(source, alias)
                except OSError:
                    shutil.copy2(source, alias)
                else:
                    hardlink_aliases += 1
                    self.assertTrue(os.path.samefile(source, alias))
                loaded.append(
                    (
                        self._load_module_from_path(
                            f"generated_combat_{name}_root_binding_test", alias
                        ),
                        _error_name,
                    )
                )

            previous_transaction_module = sys.modules.get(
                "generated_artifact_transaction"
            )
            original_sys_path = list(sys.path)
            try:
                sys.modules["generated_artifact_transaction"] = transaction
                with transaction.GeneratedArtifactLease(
                    lease_root,
                    pipeline.PIPELINE_NAME,
                    mode="read",
                    timeout_seconds=0,
                ) as lease, mock.patch.dict(
                    os.environ,
                    {
                        pipeline.LEASE_DELEGATION_ENVIRONMENT: json.dumps(
                            lease.delegation(), sort_keys=True
                        ),
                        pipeline.LEASE_REPO_ROOT_ENVIRONMENT: str(lease_root),
                    },
                    clear=False,
                    ):
                    for module, error_name in loaded:
                        with self.subTest(reader=module.__name__):
                            error_type = getattr(module, error_name, RuntimeError)
                            with self.assertRaisesRegex(error_type, "checkout|root"):
                                module.enter_governed_read_lease(checkout_root, ())
            finally:
                sys.path[:] = original_sys_path
                if previous_transaction_module is None:
                    sys.modules.pop("generated_artifact_transaction", None)
                else:
                    sys.modules[
                        "generated_artifact_transaction"
                    ] = previous_transaction_module

            if os.name == "nt":
                self.assertEqual(len(readers), hardlink_aliases)

    def test_candidate_primary_uses_frozen_generator_and_live_capture_root_override(self):
        class StopAfterPrimaryCommand(RuntimeError):
            pass

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".git").mkdir()
            candidate_root = root / "candidate"
            candidate_root.mkdir()
            frozen_primary = root / "frozen" / "primary.py"
            frozen_primary.parent.mkdir()
            frozen_primary.write_text("# frozen primary\n", encoding="ascii")
            auxiliary_snapshot = mock.Mock()
            auxiliary_snapshot.path_for.return_value = frozen_primary
            observed = {}

            def intercept(command, **kwargs):
                observed["command"] = tuple(command)
                observed["kwargs"] = kwargs
                raise StopAfterPrimaryCommand

            with mock.patch.object(
                pipeline, "generator_descriptors", return_value={}
            ), mock.patch.object(
                pipeline, "runtime_descriptor", return_value={}
            ), mock.patch.object(
                pipeline, "run_checked", side_effect=intercept
            ):
                with self.assertRaises(StopAfterPrimaryCommand):
                    pipeline.build_candidate_cohort(
                        root,
                        candidate_root,
                        auxiliary_snapshot=auxiliary_snapshot,
                        lease=object(),
                    )

            auxiliary_snapshot.path_for.assert_called_once_with(
                pipeline.PRIMARY_GENERATOR.as_posix()
            )
            self.assertIn(str(frozen_primary), observed["command"])
            self.assertEqual("primary aggregation", observed["kwargs"]["label"])
            self.assertEqual(
                {
                    pipeline.PRIMARY_CAPTURE_REPO_ROOT_ENVIRONMENT: str(
                        root.resolve()
                    )
                },
                observed["kwargs"]["environment_overrides"],
            )

    def test_primary_publication_revalidation_uses_frozen_private_validator_and_fails_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".git").mkdir()
            frozen_primary = root / "frozen" / "primary.py"
            frozen_primary.parent.mkdir()
            frozen_primary.write_text("# frozen primary\n", encoding="ascii")
            auxiliary_snapshot = mock.Mock()
            auxiliary_snapshot.path_for.return_value = frozen_primary
            candidate = pipeline.CandidateCohort(
                root=root,
                artifacts={},
                manifest_path=root / "manifest.json",
                capture_snapshot=self._snapshot(),
                generation_identity="a" * 64,
                input_snapshot_identity="b" * 64,
                fixed_point_rounds=1,
            )
            observed = {}

            def reject_changed_capture(command, **kwargs):
                observed["command"] = tuple(command)
                observed["kwargs"] = kwargs
                snapshot_path = Path(command[-1])
                self.assertEqual(
                    pipeline.canonical_json_bytes(candidate.capture_snapshot),
                    snapshot_path.read_bytes(),
                )
                raise pipeline.PipelineError("live capture input snapshot changed")

            with mock.patch.object(
                pipeline, "revalidate_auxiliary_inputs"
            ) as revalidate_auxiliary, mock.patch.object(
                pipeline, "run_checked", side_effect=reject_changed_capture
            ):
                with self.assertRaisesRegex(pipeline.PipelineError, "changed"):
                    pipeline.revalidate_candidate_inputs(
                        auxiliary_snapshot, candidate, root, object()
                    )

            revalidate_auxiliary.assert_called_once_with(auxiliary_snapshot, root)
            auxiliary_snapshot.path_for.assert_called_once_with(
                pipeline.PRIMARY_GENERATOR.as_posix()
            )
            self.assertIn(
                "--_validate-exported-input-snapshot", observed["command"]
            )
            self.assertIn(str(frozen_primary), observed["command"])
            self.assertEqual(
                "primary input revalidation", observed["kwargs"]["label"]
            )
            self.assertEqual(
                {
                    pipeline.PRIMARY_CAPTURE_REPO_ROOT_ENVIRONMENT: str(
                        root.resolve()
                    )
                },
                observed["kwargs"]["environment_overrides"],
            )

    def test_cmd_supervised_recursion_executes_once_and_propagates_nonzero_exit(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".git").mkdir()
            self._complete_cohort(root)
            tools_root = root / "Tools"
            tools_root.mkdir()
            shutil.copy2(
                Path(pipeline.__file__), tools_root / "generated_combat_pipeline.py"
            )
            shutil.copy2(
                Path(transaction.__file__),
                tools_root / "generated_artifact_transaction.py",
            )
            marker = root / "executions.txt"
            wrapper = root / "supervised-recursion.cmd"
            wrapper.write_text(
                "\n".join(
                    (
                        "@echo off",
                        f'if not "%{pipeline.LEASE_DELEGATION_ENVIRONMENT}%"=="" goto :leased',
                        f'"{sys.executable}" "%~dp0Tools\\generated_combat_pipeline.py" --run-read-lease -- "%~f0" %*',
                        "exit /b %errorlevel%",
                        ":leased",
                        f'"{sys.executable}" "%~dp0Tools\\generated_combat_pipeline.py" --_validate-read-delegation --repo-root "%~dp0."',
                        "if errorlevel 1 exit /b %errorlevel%",
                        '>>"%~1" echo executed',
                        "exit /b 23",
                        "",
                    )
                ),
                encoding="ascii",
                newline="\r\n",
            )
            environment = os.environ.copy()
            environment.pop(pipeline.LEASE_DELEGATION_ENVIRONMENT, None)
            environment.pop(pipeline.LEASE_REPO_ROOT_ENVIRONMENT, None)
            command_line = subprocess.list2cmdline((str(wrapper), str(marker)))
            completed = subprocess.run(
                (
                    os.environ.get("COMSPEC", "cmd.exe"),
                    "/d",
                    "/s",
                    "/c",
                    command_line,
                ),
                cwd=root,
                env=environment,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                check=False,
                timeout=20,
            )
            self.assertEqual(
                23,
                completed.returncode,
                completed.stdout + "\n" + completed.stderr,
            )
            self.assertEqual(["executed"], marker.read_text(encoding="ascii").splitlines())

    def test_auxiliary_snapshot_discovers_formula_and_provider_capture_inputs(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            formula = root / pipeline.FORMULA_GENERATOR
            formula.parent.mkdir(parents=True)
            formula.write_text('FORMULA = "20260101-010101"\n', encoding="utf-8")
            provider = root / pipeline.FORMULA_STATIC_INPUTS[0]
            provider.parent.mkdir(parents=True)
            provider.write_text('CAPTURE = "20260202-020202"\n', encoding="utf-8")
            analyzer = root / pipeline.SCFU_ANALYZER
            analyzer.parent.mkdir(parents=True)
            analyzer.write_bytes(b"fixture analyzer")
            runtime = (
                root
                / "AORebirth"
                / "Server"
                / "ZoneEngine"
                / "Core"
                / "Fixture.cs"
            )
            runtime.parent.mkdir(parents=True, exist_ok=True)
            runtime.write_text("// fixture\n", encoding="utf-8")
            expected = []
            for capture_id in ("20260101-010101", "20260202-020202"):
                capture = (
                    root
                    / "tools-temp"
                    / "AOSharpLiveCapture"
                    / "bin"
                    / "Debug"
                    / "captures"
                    / capture_id
                )
                capture.mkdir(parents=True)
                for source_name in pipeline.FORMULA_CAPTURE_SOURCE_NAMES:
                    source = capture / source_name
                    source.write_bytes(source_name.encode("ascii"))
                    expected.append(source.relative_to(root).as_posix())

            discovered = pipeline.auxiliary_input_paths(root)

            for relative in expected:
                self.assertIn(relative, discovered)


if __name__ == "__main__":
    unittest.main()
