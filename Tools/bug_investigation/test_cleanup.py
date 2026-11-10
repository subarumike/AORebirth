"""Exercise the destructive boundary with a synthetic local repository."""

from contextlib import redirect_stdout
import io
import json
import os
from pathlib import Path
import sqlite3
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from cleanup import owner, registration, validation_artifacts
from repair import cleanup_published, git, initialize_repairs, read_repair, save


class CleanupTest(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="aor-cleanup-test-")
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name).resolve(strict=True)
        self.source = root / "source"
        self.source.mkdir()
        git(self.source, "init", "-q")
        git(self.source, "config", "user.email", "fixture@example.invalid")
        git(self.source, "config", "user.name", "Fixture")
        git(self.source, "config", "commit.gpgsign", "false")
        (self.source / ".gitignore").write_text(".local/\n", encoding="utf-8")
        (self.source / "source.txt").write_text("before\n", encoding="utf-8")
        git(self.source, "add", ".")
        git(self.source, "commit", "-qm", "Baseline")
        base = git(self.source, "rev-parse", "HEAD").strip()
        self.path = root / "repair-worktrees" / "7001-1"
        self.path.parent.mkdir()
        self.branch = "codex/bugfix-7001-attempt-1"
        git(self.source, "worktree", "add", "-b", self.branch, str(self.path), base)
        (self.path / "source.txt").write_text("after\n", encoding="utf-8")
        git(self.path, "commit", "-qam", "Repair")
        self.args = SimpleNamespace(source=self.source, db=root / "queue.sqlite3")
        self.db = sqlite3.connect(self.args.db)
        self.addCleanup(self.db.close)
        initialize_repairs(self.db)
        self.state = {
            "phase": "published", "attempts": 1, "base_commit": base,
            "commit": git(self.path, "rev-parse", "HEAD").strip(),
            "tree": git(self.path, "rev-parse", "HEAD^{tree}").strip(),
            "draft_pr": "https://github.com/example/aor-fixture/pull/1",
            "worktree_owner": owner(self.source, self.path, self.branch, git),
            "validation_artifacts": {},
        }

    def cleanup(self):
        with redirect_stdout(io.StringIO()):
            cleanup_published(self.db, self.args, 7001, self.state, "codex/bugfix-7001",
                              publication_verified=True)

    def test_clean_owned_worktree_removed_with_branch_preserved(self):
        self.cleanup()
        self.assertFalse(os.path.lexists(self.path))
        self.assertIsNone(registration(self.source, self.path, git))
        self.assertEqual(git(self.source, "rev-parse", self.branch).strip(), self.state["commit"])
        self.assertEqual(read_repair(self.db, 7001)["cleanup_status"], "removed")

    def test_ignored_capture_retains_worktree(self):
        (self.path / ".local").mkdir()
        capture = self.path / ".local" / "capture.txt"
        capture.write_text("unique fixture evidence\n", encoding="utf-8")
        self.cleanup()
        self.assertEqual(read_repair(self.db, 7001)["cleanup_status"], "retained")
        self.assertTrue(capture.is_file())

    def test_worktree_lock_retains_registration(self):
        git(self.source, "worktree", "lock", str(self.path))
        self.cleanup()
        self.assertEqual(read_repair(self.db, 7001)["cleanup_status"], "retained")
        self.assertTrue(self.path.is_dir())

    def test_bound_validation_receipt_is_archived_before_removal(self):
        bundle = self.path / ".local" / f"feature-validation-{'0' * 32}"
        bundle.mkdir(parents=True)
        receipt = bundle / "feature-validation.json"
        receipt.write_text(json.dumps({
            "BUILD_PURPOSE": "FEATURE_VALIDATION", "DEPLOYABLE": False,
            "WINDOWS_USER_ACCEPTED": "NO", "VALIDATION_RESULT": "PASS",
            "FEATURE_SOURCE": {"HEAD": self.state["base_commit"], "BRANCH": self.branch},
        }), encoding="utf-8")
        self.state["validation_artifacts"] = validation_artifacts(self.path, self.state, git)
        self.cleanup()
        stored = read_repair(self.db, 7001, internal=True)
        self.assertEqual(stored["cleanup_status"], "removed")
        self.assertIn(receipt.relative_to(self.path).as_posix(), stored["cleanup_archive"])
        self.assertNotIn("cleanup_archive", read_repair(self.db, 7001))

    def test_interrupted_pending_removal_can_be_reconciled(self):
        save(self.db, 7001, self.state, cleanup_status="pending", cleanup_bytes=1, cleanup_archive={})
        git(self.source, "worktree", "remove", "--", str(self.path))
        with redirect_stdout(io.StringIO()), patch("repair.existing_draft") as verify:
            cleanup_published(self.db, self.args, 7001, self.state, "codex/bugfix-7001")
        self.assertEqual(read_repair(self.db, 7001)["cleanup_status"], "removed")
        verify.assert_not_called()

    def test_preexisting_worktree_without_owner_is_ineligible(self):
        self.state.pop("worktree_owner")
        self.cleanup()
        self.assertTrue(self.path.is_dir())
        self.assertIsNone(read_repair(self.db, 7001))


if __name__ == "__main__":
    unittest.main()
