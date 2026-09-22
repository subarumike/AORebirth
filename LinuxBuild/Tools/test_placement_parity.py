import hashlib
import unittest
from verify_placement_parity import parity_digest


class PlacementParityTests(unittest.TestCase):
    sha = "a" * 40
    windows = ('{"SourceSHA":"' + sha + '","Count":32805,"Shard":"unchanged"}').encode()
    digest = hashlib.sha256(windows).hexdigest()

    def test_identical_source_and_bytes_pass(self):
        self.assertEqual(self.digest, parity_digest(self.windows, self.sha, self.digest))

    def test_private_child_identity_rejected(self):
        with self.assertRaisesRegex(ValueError, "source"):
            parity_digest(self.windows.replace(self.sha.encode(), b"b" * 40), self.sha, self.digest)

    def test_changed_placement_count_rejected(self):
        with self.assertRaises(ValueError):
            parity_digest(self.windows.replace(b"32805", b"32806"), self.sha, self.digest)

    def test_changed_shard_digest_rejected(self):
        with self.assertRaises(ValueError):
            parity_digest(self.windows.replace(b"unchanged", b"changed"), self.sha, self.digest)

    def test_duplicate_identity_rejected(self):
        duplicate = self.windows[:-1] + b',"SourceSHA":"' + self.sha.encode() + b'"}'
        with self.assertRaises(ValueError):
            parity_digest(duplicate, self.sha, self.digest)

    def test_arbitrary_format_changes_rejected(self):
        with self.assertRaises(ValueError):
            parity_digest(self.windows + b"\n", self.sha, self.digest)


if __name__ == "__main__":
    unittest.main()
