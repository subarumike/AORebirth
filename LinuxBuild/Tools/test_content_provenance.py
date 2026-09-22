import json
from pathlib import Path
import stat
import tempfile
import unittest
import zipfile
from content_provenance import execute
from import_editable_playfields import import_archive

SHA = "a" * 40
class ContentTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / "GameData").mkdir()
        self.npcs = self.root / "GameData/PlayfieldContent/127/Npcs.json"
        self.npcs.parent.mkdir(parents=True)
        self.npcs.write_text('{"SchemaVersion":1,"PlayfieldId":127,"Npcs":[],"StandaloneShops":[]}')
    def tearDown(self):
        self.temp.cleanup()
    def test_edit_requires_new_manifest_and_then_is_accepted(self):
        first = execute("write", self.root, SHA, "linux")
        self.npcs.write_text('{"SchemaVersion":1,"PlayfieldId":127,"Npcs":[{"Name":"Operator choice"}],"StandaloneShops":[]}')
        with self.assertRaises(ValueError): execute("check", self.root, SHA, "linux")
        second = execute("write", self.root, SHA, "linux")
        self.assertNotEqual(first["CONTENT_MANIFEST_SHA256"], second["CONTENT_MANIFEST_SHA256"])
        self.assertEqual(second, execute("check", self.root, SHA, "linux"))
    def test_omission_and_extra_files_are_rejected(self):
        execute("write", self.root, SHA, "linux")
        path = self.root / "GameData/extra.json"
        path.write_text('{}')
        with self.assertRaises(ValueError): execute("check", self.root, SHA, "linux")
        path.unlink()
        self.npcs.unlink()
        with self.assertRaises(ValueError): execute("check", self.root, SHA, "linux")
    def test_source_platform_digest_and_manifest_tampering_rejected(self):
        execute("write", self.root, SHA, "linux")
        for sha, platform, digest in (("b"*40,"linux",None),(SHA,"windows",None),(SHA,"linux","f"*64)):
            with self.assertRaises(ValueError): execute("check", self.root, sha, platform, digest)
        manifest = self.root / "CONTENT_MANIFEST.json"
        data = json.loads(manifest.read_text())
        data["files"][0]["path"] = "../outside"
        manifest.write_text(json.dumps(data))
        with self.assertRaises(ValueError): execute("check", self.root, SHA, "linux")
    def test_invalid_duplicate_nonfinite_json_rejected(self):
        for data in ('{bad', '{"a":1,"a":2}', '{"a":NaN}'):
            self.npcs.write_text(data)
            with self.assertRaises(ValueError): execute("write", self.root, SHA, "linux")
    def test_no_fixed_corpus_or_provenance_permission(self):
        self.npcs.write_text('{"SchemaVersion":1,"PlayfieldId":127,"Npcs":[{"Provenance":"","Name":"Local editable NPC"}],"StandaloneShops":[]}')
        execute("write", self.root, SHA, "linux")
        self.assertEqual("1", execute("check", self.root, SHA, "linux")["CONTENT_FILE_COUNT"])
    def test_symlink_file_and_directory_rejected(self):
        for is_dir in (False, True):
            link = self.root / "GameData/link"
            target = self.root / ("GameData" if is_dir else "GameData/PlayfieldContent/127/Npcs.json")
            try:
                link.symlink_to(target, target_is_directory=is_dir)
            except OSError:
                self.skipTest("Symlink creation unsupported in this environment")
            with self.assertRaises(ValueError): execute("write", self.root, SHA, "linux")
            link.unlink()

class ArchiveTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.archive = self.root / "content.zip"
        self.destination = self.root / "destination"
    def tearDown(self):
        self.temp.cleanup()
    def write(self, entries):
        with zipfile.ZipFile(self.archive, "w") as output:
            for name, data in entries: output.writestr(name, data)
    def test_editable_archive_preserves_bytes_and_records_identity(self):
        self.write([("800/Spawns.json", b'{"editable":true}')])
        receipt = import_archive(self.archive, self.destination)
        self.assertEqual(1, receipt["fileCount"])
        self.assertEqual(64, len(receipt["archiveSha256"]))
        self.assertEqual(b'{"editable":true}', (self.destination / "800/Spawns.json").read_bytes())
    def test_paths_collisions_and_existing_target_rejected(self):
        for entries in ([('../escape',b'x')],[('/absolute',b'x')],[('C:/drive',b'x')],[('a\\b',b'x')],[('a',b'x'),('A',b'y')],[('a',b'x'),('a/b',b'y')]):
            self.write(entries)
            with self.assertRaises(ValueError): import_archive(self.archive, self.destination)
            self.assertFalse(self.destination.exists())
        self.write([('valid',b'x')])
        self.destination.mkdir()
        (self.destination / 'preserve').write_text('old')
        with self.assertRaises(ValueError): import_archive(self.archive, self.destination)
        self.assertEqual('old', (self.destination / 'preserve').read_text())
    def test_archive_symlink_rejected_before_extracting(self):
        entry = zipfile.ZipInfo('link')
        entry.create_system = 3
        entry.external_attr = (stat.S_IFLNK | 0o777) << 16
        self.write([(entry, b'../escape')])
        with self.assertRaises(ValueError): import_archive(self.archive, self.destination)
        self.assertFalse(self.destination.exists())

if __name__ == '__main__':
    unittest.main()
