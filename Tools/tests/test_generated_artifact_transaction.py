from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import threading
import time
import unittest
import uuid
from pathlib import Path
from unittest import mock


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "Tools"))

import generated_artifact_transaction as transaction_module  # noqa: E402
from generated_artifact_transaction import (  # noqa: E402
    ArtifactLeaseBusy,
    ArtifactTransaction,
    ArtifactTransactionError,
    DelegationError,
    GeneratedArtifactError,
    GeneratedArtifactLease,
    InputChangedError,
    InputSnapshot,
    PendingRecoveryError,
    SimulatedCrash,
)


class RepositoryFixture:
    def __init__(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="aorebirth-generated-artifact-test-")
        self.root = Path(self.temporary.name) / "repo"
        self.root.mkdir()
        (self.root / ".git").mkdir()

    def close(self) -> None:
        self.temporary.cleanup()

    def file(self, relative: str, payload: bytes = b"") -> Path:
        path = self.root / Path(*relative.split("/"))
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(payload)
        return path


def create_directory_link(link: Path, target: Path) -> None:
    if os.name == "nt":
        completed = subprocess.run(
            ["cmd", "/d", "/c", "mklink", "/J", str(link), str(target)],
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            timeout=10,
            check=False,
        )
        if completed.returncode != 0:
            raise RuntimeError("could not create test junction: " + completed.stdout.strip())
    else:
        link.symlink_to(target, target_is_directory=True)


def remove_directory_link(link: Path) -> None:
    if not link.exists() and not link.is_symlink():
        return
    if os.name == "nt":
        os.rmdir(link)
    else:
        link.unlink()


class GeneratedArtifactLeaseTests(unittest.TestCase):
    def setUp(self) -> None:
        self.fixture = RepositoryFixture()

    def tearDown(self) -> None:
        self.fixture.close()

    def test_multiple_readers_are_allowed(self) -> None:
        with GeneratedArtifactLease(self.fixture.root, "combat", "read") as first:
            with GeneratedArtifactLease(self.fixture.root, "combat", "read") as second:
                with GeneratedArtifactLease(self.fixture.root, "combat", "read") as third:
                    self.assertNotEqual(first.generation_identity, second.generation_identity)
                    self.assertNotEqual(second.generation_identity, third.generation_identity)

    def test_read_write_and_write_write_contention_is_fail_fast(self) -> None:
        with GeneratedArtifactLease(self.fixture.root, "combat", "read"):
            with self.assertRaisesRegex(ArtifactLeaseBusy, "domain='combat'.*mode=write.*waitMs=0"):
                GeneratedArtifactLease(self.fixture.root, "combat", "write", 0)
        with GeneratedArtifactLease(self.fixture.root, "combat", "write"):
            with self.assertRaisesRegex(ArtifactLeaseBusy, "mode=write.*owners="):
                GeneratedArtifactLease(self.fixture.root, "combat", "write", 0)
            with self.assertRaisesRegex(ArtifactLeaseBusy, "mode=read"):
                GeneratedArtifactLease(self.fixture.root, "combat", "read", 0)

    def test_bounded_writer_waits_for_reader_then_acquires(self) -> None:
        reader = GeneratedArtifactLease(self.fixture.root, "combat", "read")
        release = threading.Thread(target=lambda: (time.sleep(0.15), reader.close()))
        release.start()
        started = time.monotonic()
        with GeneratedArtifactLease(self.fixture.root, "combat", "write", 1.0):
            elapsed = time.monotonic() - started
        release.join(2)
        self.assertGreaterEqual(elapsed, 0.10)
        self.assertLess(elapsed, 1.0)

    def test_bounded_reader_waits_for_writer_then_acquires(self) -> None:
        writer = GeneratedArtifactLease(self.fixture.root, "combat", "write")
        release = threading.Thread(target=lambda: (time.sleep(0.15), writer.close()))
        release.start()
        started = time.monotonic()
        with GeneratedArtifactLease(self.fixture.root, "combat", "read", 1.0):
            elapsed = time.monotonic() - started
        release.join(2)
        self.assertGreaterEqual(elapsed, 0.10)
        self.assertLess(elapsed, 1.0)

    def test_timeout_is_bounded_and_diagnostic(self) -> None:
        with GeneratedArtifactLease(self.fixture.root, "combat", "write"):
            started = time.monotonic()
            with self.assertRaisesRegex(ArtifactLeaseBusy, "waitMs=100.*osError=.*owners=") as caught:
                GeneratedArtifactLease(self.fixture.root, "combat", "read", 0.1)
            elapsed = time.monotonic() - started
            self.assertIn(f"owners={os.getpid()}:write:", str(caught.exception))
        self.assertGreaterEqual(elapsed, 0.08)
        self.assertLess(elapsed, 0.8)
        with self.assertRaisesRegex(GeneratedArtifactError, "between 0 and 600"):
            GeneratedArtifactLease(self.fixture.root, "combat", "write", 601)

    def test_live_cross_process_lock_is_not_stolen(self) -> None:
        ready = Path(self.fixture.temporary.name) / "ready"
        process = subprocess.Popen(
            [sys.executable, str(Path(__file__).resolve()), "--hold", str(self.fixture.root), "combat", str(ready)],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        try:
            deadline = time.monotonic() + 5
            while not ready.exists() and process.poll() is None and time.monotonic() < deadline:
                time.sleep(0.02)
            self.assertTrue(ready.exists(), f"child exited={process.poll()}")
            with self.assertRaises(ArtifactLeaseBusy):
                GeneratedArtifactLease(self.fixture.root, "combat", "write", 0.1)
            self.assertIsNone(process.poll())
            ready.with_suffix(".release").write_text("release", encoding="ascii")
            self.assertEqual(0, process.wait(timeout=5))
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=5)

    def test_preexisting_unowned_lock_file_is_opened_not_stolen(self) -> None:
        lease = GeneratedArtifactLease(self.fixture.root, "combat", "write")
        lock_path = lease.lock_path
        lease.close()
        lock_path.parent.mkdir(parents=True, exist_ok=True)
        lock_path.write_bytes(b"stale-unowned-record")
        with GeneratedArtifactLease(self.fixture.root, "combat", "write") as acquired:
            self.assertEqual(lock_path, acquired.lock_path)

    def test_delegation_is_bound_to_live_owner_pid_token_and_generation(self) -> None:
        with GeneratedArtifactLease(self.fixture.root, "combat", "write") as lease:
            delegation = lease.delegation()
            record = GeneratedArtifactLease.validate_delegation(
                self.fixture.root, delegation, required_mode="write"
            )
            self.assertEqual(lease.generation_identity, record["generationIdentity"])
            wrong_token = dict(delegation, token="0" * 64)
            with self.assertRaises(DelegationError):
                GeneratedArtifactLease.validate_delegation(self.fixture.root, wrong_token)
            mixed_identity = dict(delegation, generationIdentity=uuid.uuid4().hex)
            with self.assertRaises(DelegationError):
                GeneratedArtifactLease.validate_delegation(self.fixture.root, mixed_identity)
        with self.assertRaises(DelegationError):
            GeneratedArtifactLease.validate_delegation(self.fixture.root, delegation)

    def test_stale_staging_cleanup_removes_only_valid_dead_owner(self) -> None:
        seed = GeneratedArtifactLease(self.fixture.root, "combat", "read")
        base = seed.staging_root.parent
        seed.close()
        dead_identity = uuid.uuid4().hex
        live_identity = uuid.uuid4().hex
        malformed_identity = uuid.uuid4().hex
        self._write_staging_owner(base / dead_identity, dead_identity, 1073741823)
        self._write_staging_owner(base / live_identity, live_identity, os.getpid())
        (base / malformed_identity).mkdir()
        (base / malformed_identity / "owner.json").write_text("{", encoding="utf-8")
        with GeneratedArtifactLease(self.fixture.root, "combat", "write") as lease:
            self.assertFalse((base / dead_identity).exists())
            self.assertTrue((base / live_identity).is_dir())
            self.assertTrue((base / malformed_identity).is_dir())
            result = lease.cleanup_abandoned_staging()
            self.assertIn(live_identity, result["retained"])
            self.assertIn(malformed_identity, result["retained"])
            self.assertIn(lease.generation_identity, result["retained"])

    def test_exclusive_writer_removes_hard_exit_owner_but_retains_live_and_malformed(self) -> None:
        seed = GeneratedArtifactLease(self.fixture.root, "combat", "read")
        owners = seed.control_root / "locks"
        domain_key = seed.domain_key
        seed.close()
        child_code = (
            "import os,sys;from pathlib import Path;"
            "sys.path.insert(0,sys.argv[2]);"
            "from generated_artifact_transaction import GeneratedArtifactLease;"
            "lease=GeneratedArtifactLease(Path(sys.argv[1]),'combat','read');"
            "print(lease.generation_identity,flush=True);os._exit(0)"
        )
        child = subprocess.Popen(
            [sys.executable, "-c", child_code, str(self.fixture.root), str(REPOSITORY_ROOT / "Tools")],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        child_stdout, child_stderr = child.communicate(timeout=10)
        self.assertEqual(0, child.returncode, child_stderr)
        hard_exit_identity = child_stdout.strip()
        self.assertRegex(hard_exit_identity, r"^[0-9a-f]{32}$")
        hard_exit_owner = owners / f"{domain_key}.{hard_exit_identity}.owner.json"
        if os.name == "nt":
            self.assertFalse(hard_exit_owner.exists())
        hard_exit_owner.unlink(missing_ok=True)

        orphan_identity = uuid.uuid4().hex
        orphan = owners / f"{domain_key}.{orphan_identity}.owner.json"
        self._write_lock_owner(orphan, orphan_identity, 1073741823)
        self.assertTrue(orphan.is_file())

        live_identity = uuid.uuid4().hex
        malformed_identity = uuid.uuid4().hex
        live = owners / f"{domain_key}.{live_identity}.owner.json"
        malformed = owners / f"{domain_key}.{malformed_identity}.owner.json"
        self._write_lock_owner(live, live_identity, os.getpid())
        malformed.write_text("{", encoding="utf-8")

        with GeneratedArtifactLease(self.fixture.root, "combat", "write"):
            self.assertFalse(orphan.exists())
            self.assertTrue(live.is_file())
            self.assertTrue(malformed.is_file())

    @staticmethod
    def _write_lock_owner(path: Path, identity: str, pid: int) -> None:
        path.write_text(
            json.dumps({
                "schemaVersion": 1,
                "domain": "combat",
                "mode": "read",
                "ownerPid": pid,
                "generationIdentity": identity,
                "tokenSha256": "0" * 64,
            }),
            encoding="utf-8",
        )

    @staticmethod
    def _write_staging_owner(path: Path, identity: str, pid: int) -> None:
        path.mkdir(parents=True)
        (path / "owner.json").write_text(
            json.dumps({
                "schemaVersion": 1,
                "domain": "combat",
                "ownerPid": pid,
                "generationIdentity": identity,
                "tokenSha256": "0" * 64,
            }),
            encoding="utf-8",
        )


class InputSnapshotTests(unittest.TestCase):
    def setUp(self) -> None:
        self.fixture = RepositoryFixture()
        self.a = self.fixture.file("inputs/a.txt", b"alpha")
        self.b = self.fixture.file("inputs/b.txt", b"beta")

    def tearDown(self) -> None:
        self.fixture.close()

    def test_records_and_identity_are_sorted_order_and_location_invariant(self) -> None:
        with GeneratedArtifactLease(self.fixture.root, "snapshot", "read") as lease:
            reverse = InputSnapshot.capture(lease, [self.b, self.a])
            forward = InputSnapshot.capture(lease, [self.a, self.b])
            self.assertEqual(["inputs/a.txt", "inputs/b.txt"], [row.relative_path for row in reverse.records])
            self.assertEqual(reverse.identity, forward.identity)
            self.assertEqual(b"alpha", reverse.path_for("inputs/a.txt").read_bytes())
        other = RepositoryFixture()
        try:
            other_a = other.file("inputs/a.txt", b"alpha")
            other_b = other.file("inputs/b.txt", b"beta")
            with GeneratedArtifactLease(other.root, "snapshot", "read") as lease:
                relocated = InputSnapshot.capture(lease, [other_a, other_b])
                self.assertEqual(reverse.identity, relocated.identity)
        finally:
            other.close()

    def test_identity_is_hash_seed_invariant(self) -> None:
        command = [
            sys.executable,
            str(Path(__file__).resolve()),
            "--snapshot-identity",
            str(self.fixture.root),
            str(self.a),
            str(self.b),
        ]
        identities = []
        for seed in ("1", "987654"):
            environment = dict(os.environ)
            environment["PYTHONHASHSEED"] = seed
            identities.append(
                subprocess.check_output(command, text=True, env=environment, timeout=10).strip()
            )
        self.assertEqual(identities[0], identities[1])

    def test_revalidation_reports_added_removed_and_exact_change(self) -> None:
        with GeneratedArtifactLease(self.fixture.root, "snapshot", "read") as lease:
            snapshot = InputSnapshot.capture(lease, [self.a, self.b])
            self.a.write_bytes(b"alpha-changed")
            self.b.unlink()
            added = self.fixture.file("inputs/c.txt", b"gamma")
            with self.assertRaises(InputChangedError) as caught:
                snapshot.revalidate([self.a, added])
            changes = caught.exception.changes
            self.assertIn("added:inputs/c.txt", changes)
            self.assertIn("removed:inputs/b.txt", changes)
            changed = next(value for value in changes if value.startswith("changed:inputs/a.txt"))
            self.assertIn("expectedSize=5", changed)
            self.assertIn("actualSize=13", changed)
            self.assertIn("expectedSha256=", changed)
            self.assertIn("actualSha256=", changed)

    def test_revalidation_rejects_mutated_snapshot_copy(self) -> None:
        with GeneratedArtifactLease(self.fixture.root, "snapshot", "read") as lease:
            snapshot = InputSnapshot.capture(lease, [self.a])
            snapshot.path_for("inputs/a.txt").write_bytes(b"mutated-frozen-copy")
            with self.assertRaises(InputChangedError) as caught:
                snapshot.revalidate()
            self.assertTrue(any(
                value.startswith("snapshot-changed:inputs/a.txt")
                for value in caught.exception.changes
            ))

    def test_duplicate_or_alias_input_is_rejected(self) -> None:
        with GeneratedArtifactLease(self.fixture.root, "snapshot", "read") as lease:
            with self.assertRaisesRegex(GeneratedArtifactError, "collide"):
                InputSnapshot.capture(lease, [self.a, self.a])

    def test_capture_rejects_reparse_component_before_resolution(self) -> None:
        link = self.fixture.root / "linked-inputs"
        create_directory_link(link, self.a.parent)
        try:
            with GeneratedArtifactLease(self.fixture.root, "snapshot", "read") as lease:
                with self.assertRaisesRegex(GeneratedArtifactError, "reparse"):
                    InputSnapshot.capture(lease, [link / self.a.name])
        finally:
            remove_directory_link(link)


class ArtifactTransactionTests(unittest.TestCase):
    DOMAIN = "combat-publication"

    def setUp(self) -> None:
        self.fixture = RepositoryFixture()
        self.inventory = self.fixture.file("generated/inventory.json", b"old-inventory")
        self.catalog = self.fixture.file("generated/catalog.g.cs", b"old-catalog")
        self.manifest = self.fixture.file("generated/manifest.json", b'{"generation":"old"}')
        self.outputs = {
            self.manifest: b'{"generation":"new"}',
            self.inventory: b"new-inventory",
            self.catalog: b"new-catalog",
        }

    def tearDown(self) -> None:
        self.fixture.close()

    def test_recover_removes_provably_empty_domain_directory(self) -> None:
        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            domain_root = ArtifactTransaction._domain_root(lease)
            domain_root.mkdir(parents=True, exist_ok=True)
            self.assertEqual("clean", ArtifactTransaction.recover(lease))
            self.assertFalse(domain_root.exists())

    def test_publish_rejects_reparse_destination_component_before_resolution(self) -> None:
        target = self.fixture.root / "linked-generated-target"
        target.mkdir()
        link = self.fixture.root / "linked-generated"
        create_directory_link(link, target)
        destination = link / "artifact.json"
        try:
            with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
                with self.assertRaisesRegex(GeneratedArtifactError, "reparse"):
                    ArtifactTransaction.publish(
                        lease,
                        {destination: b"generated"},
                        artifact_order=[destination],
                        commit_marker=destination,
                    )
            self.assertFalse((target / "artifact.json").exists())
        finally:
            remove_directory_link(link)

    def test_success_uses_explicit_order_manifest_last_and_leaves_no_transaction(self) -> None:
        observed = []

        def hook(event: str, context: dict) -> None:
            if event == "before_replace":
                observed.append(context["relativePath"])

        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            transaction_id = ArtifactTransaction.publish(
                lease,
                self.outputs,
                artifact_order=[self.catalog, self.manifest, self.inventory],
                commit_marker=self.manifest,
                validators={self.manifest: lambda value: json.loads(value.decode("utf-8"))},
                fault_hook=hook,
            )
            self.assertRegex(transaction_id, r"^[0-9a-f]{32}$")
            self.assertEqual(
                ["generated/catalog.g.cs", "generated/inventory.json", "generated/manifest.json"],
                observed,
            )
            ArtifactTransaction.assert_readable(lease)
            self.assertEqual("clean", ArtifactTransaction.recover(lease))
        self._assert_new()
        self._assert_no_transactions()

    def test_partial_json_validator_rejects_before_transaction(self) -> None:
        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            with self.assertRaisesRegex(ArtifactTransactionError, "validator rejected"):
                ArtifactTransaction.publish(
                    lease,
                    {self.manifest: b'{"generation":'},
                    validators={self.manifest: lambda value: json.loads(value.decode("utf-8"))},
                )
        self.assertEqual(b'{"generation":"old"}', self.manifest.read_bytes())
        self._assert_no_transactions()

    def test_invalid_explicit_order_is_rejected(self) -> None:
        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            with self.assertRaisesRegex(ArtifactTransactionError, "exact unique"):
                ArtifactTransaction.publish(lease, self.outputs, artifact_order=[self.inventory, self.inventory])
            with self.assertRaisesRegex(ArtifactTransactionError, "commit marker"):
                ArtifactTransaction.publish(lease, self.outputs, commit_marker="generated/not-an-output.json")

    def test_caught_failure_before_publication_rolls_back_and_cleans(self) -> None:
        def fail(event: str, context: dict) -> None:
            if event == "after_prepared":
                raise RuntimeError("injected-before-publication")

        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            with self.assertRaisesRegex(ArtifactTransactionError, "rolled back"):
                ArtifactTransaction.publish(lease, self.outputs, commit_marker=self.manifest, fault_hook=fail)
        self._assert_old()
        self._assert_no_transactions()

    def test_caught_failure_during_publication_rolls_back_complete_cohort(self) -> None:
        def fail(event: str, context: dict) -> None:
            if event == "after_replace" and context["index"] == 1:
                raise OSError("injected-second-replace")

        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            with self.assertRaisesRegex(ArtifactTransactionError, "rolled back"):
                ArtifactTransaction.publish(lease, self.outputs, commit_marker=self.manifest, fault_hook=fail)
        self._assert_old()
        self._assert_no_transactions()

    def test_validation_failure_before_publish_preserves_old_complete_cohort(self) -> None:
        observed = []

        def fail(phase: str) -> None:
            observed.append((phase, self._current_bytes()))
            raise RuntimeError("inputs-changed-before-publish")

        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            with self.assertRaises(ArtifactTransactionError) as caught:
                ArtifactTransaction.publish(
                    lease,
                    self.outputs,
                    commit_marker=self.manifest,
                    validation_callback=fail,
                )
        self.assertEqual(
            "publication failed and rolled back: ArtifactTransactionError: "
            "publication validation failed phase=before_publish: "
            "inputs-changed-before-publish",
            str(caught.exception),
        )
        self.assertEqual([("before_publish", self._old_bytes())], observed)
        self._assert_old()
        self._assert_no_transactions()

    def test_validation_failure_before_commit_restores_old_complete_cohort(self) -> None:
        observed = []

        def fail(phase: str) -> None:
            observed.append((phase, self._current_bytes()))
            if phase == "before_commit":
                raise RuntimeError("inputs-changed-before-commit")

        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            with self.assertRaises(ArtifactTransactionError) as caught:
                ArtifactTransaction.publish(
                    lease,
                    self.outputs,
                    commit_marker=self.manifest,
                    validation_callback=fail,
                )
        self.assertEqual(
            "publication failed and rolled back: ArtifactTransactionError: "
            "publication validation failed phase=before_commit: "
            "inputs-changed-before-commit",
            str(caught.exception),
        )
        self.assertEqual(
            [
                ("before_publish", self._old_bytes()),
                ("before_commit", self._new_bytes()),
            ],
            observed,
        )
        self._assert_old()
        self._assert_no_transactions()

    def test_windows_transient_replace_errors_retry_then_succeed(self) -> None:
        real_replace = os.replace
        for winerror in (5, 32, 33):
            with self.subTest(winerror=winerror), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                source = root / "source.bin"
                destination = root / "destination.bin"
                source.write_bytes(b"new")
                destination.write_bytes(b"old")
                attempts = 0

                def transient_then_succeed(actual_source: Path, actual_destination: Path) -> None:
                    nonlocal attempts
                    attempts += 1
                    if attempts < 3:
                        error = PermissionError("transient sharing violation")
                        error.winerror = winerror
                        raise error
                    real_replace(actual_source, actual_destination)

                with (
                    mock.patch.object(transaction_module.os, "name", "nt"),
                    mock.patch.object(
                        transaction_module.os,
                        "replace",
                        side_effect=transient_then_succeed,
                    ),
                    mock.patch.object(transaction_module.time, "sleep"),
                ):
                    transaction_module._replace_with_retry(
                        source, destination, "test-transient"
                    )
                self.assertEqual(3, attempts)
                self.assertFalse(source.exists())
                self.assertEqual(b"new", destination.read_bytes())

    def test_windows_non_transient_replace_error_is_not_retried_and_is_diagnostic(self) -> None:
        source = self.fixture.root / "non-transient-source.bin"
        destination = self.fixture.root / "non-transient-destination.bin"
        error = PermissionError("access policy denied")
        error.winerror = 87
        with (
            mock.patch.object(transaction_module.os, "name", "nt"),
            mock.patch.object(transaction_module.os, "replace", side_effect=error) as replace,
        ):
            with self.assertRaises(ArtifactTransactionError) as caught:
                transaction_module._replace_with_retry(
                    source, destination, "test-non-transient"
                )
        self.assertEqual(1, replace.call_count)
        message = str(caught.exception)
        self.assertIn("operation=test-non-transient", message)
        self.assertIn(f"source={source}", message)
        self.assertIn(f"destination={destination}", message)
        self.assertIn("winerror=87 attempts=1", message)
        self.assertIn("access policy denied", message)

    def test_windows_transient_replace_exhaustion_is_diagnostic(self) -> None:
        source = self.fixture.root / "exhausted-source.bin"
        destination = self.fixture.root / "exhausted-destination.bin"

        def sharing_violation(*_arguments: object) -> None:
            error = PermissionError("sharing violation persisted")
            error.winerror = 32
            raise error

        with (
            mock.patch.object(transaction_module.os, "name", "nt"),
            mock.patch.object(
                transaction_module.os, "replace", side_effect=sharing_violation
            ) as replace,
            mock.patch.object(
                transaction_module.time,
                "monotonic",
                side_effect=(0.0, 0.1, 0.2, 0.6),
            ),
            mock.patch.object(transaction_module.time, "sleep"),
        ):
            with self.assertRaises(ArtifactTransactionError) as caught:
                transaction_module._replace_with_retry(
                    source, destination, "test-exhausted"
                )
        self.assertEqual(2, replace.call_count)
        message = str(caught.exception)
        self.assertIn("operation=test-exhausted", message)
        self.assertIn(f"source={source}", message)
        self.assertIn(f"destination={destination}", message)
        self.assertIn("winerror=32 attempts=2", message)
        self.assertIn("sharing violation persisted", message)

    def test_simulated_crash_at_every_uncommitted_fault_point_recovers_all_old(self) -> None:
        fault_points = [
            ("after_initialized", None),
            *(("after_prepare_artifact", index) for index in range(3)),
            ("after_prepared", None),
            *(("before_replace", index) for index in range(3)),
            *(("after_replace", index) for index in range(3)),
            ("before_commit", None),
        ]
        for fault_event, fault_index in fault_points:
            with self.subTest(fault_event=fault_event, fault_index=fault_index):
                self._write_old()

                def crash(event: str, context: dict) -> None:
                    if (event == fault_event
                            and (fault_index is None or context.get("index") == fault_index)):
                        raise SimulatedCrash("injected")

                with self.assertRaises(SimulatedCrash):
                    with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
                        ArtifactTransaction.publish(
                            lease, self.outputs, commit_marker=self.manifest, fault_hook=crash
                        )
                with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
                    self.assertEqual("rolled-back", ArtifactTransaction.recover(lease))
                self._assert_old()
                self._assert_no_transactions()

    def test_committed_crash_recovery_keeps_new_and_only_cleans(self) -> None:
        def crash(event: str, context: dict) -> None:
            if event == "after_committed":
                raise SimulatedCrash("committed")

        with self.assertRaises(SimulatedCrash):
            with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
                ArtifactTransaction.publish(lease, self.outputs, commit_marker=self.manifest, fault_hook=crash)
        self._assert_new()
        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            self.assertEqual("committed-cleanup", ArtifactTransaction.recover(lease))
        self._assert_new()
        self._assert_no_transactions()

    def test_read_mode_rejects_pending_recovery_without_mutation(self) -> None:
        self._leave_crash_after_first_replace()
        hybrid = self._current_bytes()
        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "read") as lease:
            with self.assertRaises(PendingRecoveryError):
                ArtifactTransaction.assert_readable(lease)
        self.assertEqual(hybrid, self._current_bytes())
        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            ArtifactTransaction.recover(lease)
        self._assert_old()

    def test_recovery_rejects_reparse_domain_root_without_artifact_mutation(self) -> None:
        self._assert_recovery_reparse_rejected("domain_root")

    def test_recovery_rejects_reparse_transaction_directory_without_artifact_mutation(self) -> None:
        self._assert_recovery_reparse_rejected("transaction_directory")

    def test_recovery_rejects_reparse_journal_without_artifact_mutation(self) -> None:
        self._assert_recovery_reparse_rejected("journal")

    def test_recovery_rejects_reparse_new_member_without_artifact_mutation(self) -> None:
        self._assert_recovery_reparse_rejected("new_member")

    def test_recovery_rejects_reparse_backup_member_without_artifact_mutation(self) -> None:
        self._assert_recovery_reparse_rejected("backup_member")

    def test_missing_backup_fails_closed_without_additional_mutation(self) -> None:
        self._leave_crash_after_first_replace()
        tx = self._transaction_directory()
        (tx / "backup" / "0000.bin").unlink()
        hybrid = self._current_bytes()
        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            with self.assertRaisesRegex(PendingRecoveryError, "backup"):
                ArtifactTransaction.recover(lease)
        self.assertEqual(hybrid, self._current_bytes())
        self.assertTrue(tx.exists())

    def test_partial_or_mixed_identity_journal_fails_closed(self) -> None:
        for mutation in ("partial", "mixed", "count"):
            with self.subTest(mutation=mutation):
                if self._transaction_root().exists():
                    import shutil

                    shutil.rmtree(self._transaction_root())
                self._write_old()
                self._leave_crash_after_first_replace()
                tx = self._transaction_directory()
                journal_path = tx / "journal.json"
                if mutation == "partial":
                    journal_path.write_text("{", encoding="utf-8")
                elif mutation == "mixed":
                    journal = json.loads(journal_path.read_text(encoding="utf-8"))
                    journal["transactionIdentity"] = uuid.uuid4().hex
                    journal_path.write_text(json.dumps(journal), encoding="utf-8")
                else:
                    journal = json.loads(journal_path.read_text(encoding="utf-8"))
                    journal["state"] = "committed"
                    journal["publishedCount"] = 0
                    journal_path.write_text(json.dumps(journal), encoding="utf-8")
                hybrid = self._current_bytes()
                with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
                    with self.assertRaises(PendingRecoveryError):
                        ArtifactTransaction.recover(lease)
                self.assertEqual(hybrid, self._current_bytes())
                self.assertTrue(tx.exists())

    def test_tampered_destination_fails_closed(self) -> None:
        self._leave_crash_after_first_replace()
        self.catalog.write_bytes(b"external-tamper")
        current = self._current_bytes()
        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            with self.assertRaisesRegex(PendingRecoveryError, "tampered or mixed"):
                ArtifactTransaction.recover(lease)
        self.assertEqual(current, self._current_bytes())

    def test_absent_targets_are_removed_by_uncommitted_recovery(self) -> None:
        self.inventory.unlink()
        self.catalog.unlink()
        self.manifest.unlink()

        def crash(event: str, context: dict) -> None:
            if event == "after_replace" and context["index"] == 0:
                raise SimulatedCrash("absent")

        with self.assertRaises(SimulatedCrash):
            with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
                ArtifactTransaction.publish(lease, self.outputs, commit_marker=self.manifest, fault_hook=crash)
        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
            self.assertEqual("rolled-back", ArtifactTransaction.recover(lease))
        self.assertFalse(self.inventory.exists())
        self.assertFalse(self.catalog.exists())
        self.assertFalse(self.manifest.exists())

    def _leave_crash_after_first_replace(self) -> None:
        def crash(event: str, context: dict) -> None:
            if event == "after_replace" and context["index"] == 0:
                raise SimulatedCrash("first")

        with self.assertRaises(SimulatedCrash):
            with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
                ArtifactTransaction.publish(lease, self.outputs, commit_marker=self.manifest, fault_hook=crash)

    def _assert_recovery_reparse_rejected(self, component: str) -> None:
        self._leave_crash_after_first_replace()
        hybrid = self._current_bytes()
        tx = self._transaction_directory()
        domain_root = tx.parent
        target = self.fixture.root / ("recovery-reparse-target-" + component)
        if component == "domain_root":
            link = domain_root
            os.replace(link, target)
        elif component == "transaction_directory":
            link = tx
            os.replace(link, target)
        else:
            if component == "journal":
                link = tx / "journal.json"
            elif component == "new_member":
                link = tx / "new" / "0001.bin"
            elif component == "backup_member":
                link = tx / "backup" / "0000.bin"
            else:
                self.fail("unknown recovery reparse fixture component: " + component)
            preserved = self.fixture.root / ("recovery-preserved-" + component)
            os.replace(link, preserved)
            target.mkdir()
        create_directory_link(link, target)
        try:
            with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "write") as lease:
                with self.assertRaisesRegex(PendingRecoveryError, "reparse"):
                    ArtifactTransaction.recover(lease)
            self.assertEqual(hybrid, self._current_bytes())
        finally:
            remove_directory_link(link)

    def _transaction_root(self) -> Path:
        with GeneratedArtifactLease(self.fixture.root, self.DOMAIN, "read") as lease:
            return lease.control_root / "transactions" / lease.domain_key

    def _transaction_directory(self) -> Path:
        entries = list(self._transaction_root().glob("tx-*"))
        self.assertEqual(1, len(entries))
        return entries[0]

    def _assert_no_transactions(self) -> None:
        root = self._transaction_root()
        self.assertFalse(root.exists() and any(root.iterdir()))

    def _current_bytes(self) -> tuple[bytes, bytes, bytes]:
        return self.inventory.read_bytes(), self.catalog.read_bytes(), self.manifest.read_bytes()

    @staticmethod
    def _old_bytes() -> tuple[bytes, bytes, bytes]:
        return b"old-inventory", b"old-catalog", b'{"generation":"old"}'

    @staticmethod
    def _new_bytes() -> tuple[bytes, bytes, bytes]:
        return b"new-inventory", b"new-catalog", b'{"generation":"new"}'

    def _write_old(self) -> None:
        self.inventory.write_bytes(b"old-inventory")
        self.catalog.write_bytes(b"old-catalog")
        self.manifest.write_bytes(b'{"generation":"old"}')

    def _assert_old(self) -> None:
        self.assertEqual(self._old_bytes(), self._current_bytes())

    def _assert_new(self) -> None:
        self.assertEqual(self._new_bytes(), self._current_bytes())


def _hold_lease(root: str, domain: str, ready_name: str) -> int:
    ready = Path(ready_name)
    with GeneratedArtifactLease(root, domain, "write"):
        ready.write_text("ready", encoding="ascii")
        release = ready.with_suffix(".release")
        deadline = time.monotonic() + 10
        while not release.exists() and time.monotonic() < deadline:
            time.sleep(0.02)
        return 0 if release.exists() else 2


def _snapshot_identity(root: str, *paths: str) -> int:
    with GeneratedArtifactLease(root, "hash-seed-snapshot", "read") as lease:
        print(InputSnapshot.capture(lease, paths).identity)
    return 0


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] == "--hold":
    raise SystemExit(_hold_lease(*sys.argv[2:]))
elif __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] == "--snapshot-identity":
    raise SystemExit(_snapshot_identity(sys.argv[2], *sys.argv[3:]))
elif __name__ == "__main__":
    unittest.main(verbosity=2)
