"""Regression tests for exact master identity, including private-child rejection."""
from pathlib import Path
import subprocess
import tempfile
import unittest

from linux_drift_audit import audit_repository


class SourceIdentityTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.git("init", "--quiet")
        self.git("config", "user.name", "Source identity fixture")
        self.git("config", "user.email", "fixture@example.invalid")
        self.git("config", "commit.gpgsign", "false")
        (self.root / "source.txt").write_text("accepted\n")
        self.git("add", "source.txt")
        self.git("commit", "--quiet", "-m", "accepted master")
        self.sha = self.git("rev-parse", "HEAD")
        self.git("update-ref", "refs/remotes/origin/master", self.sha)

    def git(self, *args):
        return subprocess.check_output(["git", "-C", str(self.root), *args], text=True).strip()

    def test_exact_clean_master_passes(self):
        result = audit_repository(self.root, self.sha, self.sha)
        self.assertEqual("PASS", result["LINUX_DRIFT_AUDIT"])
        self.assertEqual(self.sha, result["LINUX_BUILD_SOURCE_SHA"])

    def test_child_commit_fails_even_with_identical_tree(self):
        self.git("commit", "--allow-empty", "--quiet", "-m", "private child")
        with self.assertRaisesRegex(ValueError, "same commit"):
            audit_repository(self.root, self.git("rev-parse", "HEAD"), self.sha)

    def test_wrong_expected_sha_fails(self):
        with self.assertRaisesRegex(ValueError, "same commit"):
            audit_repository(self.root, "a" * 40, self.sha)

    def test_master_advancement_fails(self):
        with self.assertRaisesRegex(ValueError, "same commit"):
            audit_repository(self.root, self.sha, "b" * 40)

    def test_stale_fetched_master_fails(self):
        self.git("commit", "--allow-empty", "--quiet", "-m", "next master")
        head = self.git("rev-parse", "HEAD")
        with self.assertRaisesRegex(ValueError, "Fetched origin/master"):
            audit_repository(self.root, head, head)

    def test_tracked_and_staged_edits_fail(self):
        (self.root / "source.txt").write_text("changed\n")
        with self.assertRaisesRegex(ValueError, "dirty"):
            audit_repository(self.root, self.sha, self.sha)
        self.git("add", "source.txt")
        with self.assertRaisesRegex(ValueError, "dirty"):
            audit_repository(self.root, self.sha, self.sha)

    def test_untracked_source_fails(self):
        (self.root / "extra.cs").write_text("class Extra {}")
        with self.assertRaisesRegex(ValueError, "untracked"):
            audit_repository(self.root, self.sha, self.sha)

    def test_ignored_private_assembly_metadata_fails(self):
        (self.root / ".gitignore").write_text("PRIVATE_BUILD_INPUTS.json\n")
        self.git("add", ".gitignore")
        self.git("commit", "--quiet", "-m", "ignore fixture")
        head = self.git("rev-parse", "HEAD")
        self.git("update-ref", "refs/remotes/origin/master", head)
        (self.root / "PRIVATE_BUILD_INPUTS.json").write_text("{}")
        with self.assertRaisesRegex(ValueError, "Private source assembly"):
            audit_repository(self.root, head, head)


if __name__ == "__main__":
    unittest.main()
