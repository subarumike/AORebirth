"""Offline byte-retention and packaging-admission tests."""

import importlib.util
import json
import os
from pathlib import Path
import tempfile
import subprocess
import unittest


TOOLS = Path(__file__).resolve().parents[1]


def load(name):
    spec = importlib.util.spec_from_file_location(name, TOOLS / f"{name}.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


dungeon = load("stage_mission_dungeon_content")
release = load("windows_release_contract")


class RetainedDungeonStagingTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory(prefix="aorebirth-dungeon-retention-test-")
        self.addCleanup(self.scratch.cleanup)
        self.root = Path(self.scratch.name)
        self.source = self.root / "active"
        self.source.mkdir()
        for name in (*dungeon.COMMON, *dungeon.NPC_FILES):
            (self.source / name).write_bytes(name.encode())
        (self.source / "DungeonGeneration.json").write_text(
            '{"Profiles":[{"Styles":[21]},{"Styles":[22,21]}]}', encoding="utf-8")
        for style in (21, 22):
            for name in dungeon.relative_style_files(style):
                file = self.source / name
                file.parent.mkdir(parents=True, exist_ok=True)
                file.write_bytes(name.encode())

    def test_second_revision_includes_all_old_style_and_npc_bytes(self):
        prior = self.root / "prior"
        candidate = self.root / "candidate"
        old = dungeon.stage(self.source, prior)
        (self.source / "Playfields/21/Rooms.json").write_bytes(b"new room data")
        (self.source / "NpcTemplates.json").write_bytes(b"new npc data")
        new = dungeon.stage(self.source, candidate, prior)
        self.assertNotEqual(old[21], new[21])
        self.assertNotEqual(old[22], new[22])
        root = candidate / dungeon.FOLDER
        self.assertEqual({"21": new[21], "22": new[22]},
                         json.loads((root / "Current.json").read_text())["Styles"])
        self.assertEqual(2, len(list((root / dungeon.NPC_FOLDER).iterdir())))
        for style in (21, 22):
            self.assertEqual(old[style], dungeon.identity(dungeon.gather(root / old[style], style)))
            self.assertEqual(new[style], dungeon.identity(dungeon.gather(root / new[style], style)))

    def test_corrupt_prior_bundle_does_not_publish_the_new_current_index(self):
        prior = self.root / "prior"
        candidate = self.root / "candidate"
        old = dungeon.stage(self.source, prior)
        corrupted = prior / dungeon.FOLDER / old[21] / "Playfields/21/Rooms.json"
        corrupted.write_bytes(b"corrupted")
        with self.assertRaisesRegex(ValueError, "Prior retained dungeon digest mismatch"):
            dungeon.stage(self.source, candidate, prior)
        self.assertFalse((candidate / dungeon.FOLDER / "Current.json").exists())
        self.assertEqual(b"corrupted", corrupted.read_bytes())

    @unittest.skipUnless(os.name == "nt", "Windows junction protection")
    def test_root_validation_rejects_source_and_output_junctions(self):
        link = self.root / "redirected"
        subprocess.run(["cmd.exe", "/d", "/c", "mklink", "/J", str(link), str(self.source)],
                       capture_output=True, check=True)
        self.addCleanup(link.rmdir)
        with self.assertRaisesRegex(ValueError, "Link or junction rejected"):
            dungeon.stage(link, self.root / "candidate")
        with self.assertRaisesRegex(ValueError, "Link or junction rejected"):
            dungeon.stage(self.source, link / "new")
        with self.assertRaisesRegex(ValueError, "Link or junction rejected"):
            dungeon.stage(self.source, self.root / "candidate", link)
        self.assertFalse((self.source / "new").exists())

    def test_root_validation_rejects_traversal(self):
        with self.assertRaisesRegex(ValueError, "Traversal rejected"):
            dungeon.stage(self.source, self.root / "nested" / ".." / "candidate")
        with self.assertRaisesRegex(ValueError, "Traversal rejected"):
            dungeon.checked(self.source, "../outside")

    def test_prior_archive_rejects_unowned_files_and_dangling_active_index(self):
        prior = self.root / "prior"
        candidate = self.root / "candidate"
        old = dungeon.stage(self.source, prior)
        root = prior / dungeon.FOLDER
        extra = root / old[21] / "unexpected.json"
        extra.write_text("{}", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Unexpected or missing retained dungeon members"):
            dungeon.stage(self.source, candidate, prior)
        extra.unlink()
        (root / "Current.json").write_text(
            json.dumps({"SchemaVersion": 1, "Styles": {"21": "e" * 64}}), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Missing or conflicting prior retained dungeon archive"):
            dungeon.stage(self.source, candidate, prior)
        self.assertFalse((candidate / dungeon.FOLDER / "Current.json").exists())

    def test_runtime_admission_is_limited_to_declared_dungeon_pack_members(self):
        sha = "a" * 64
        prefix = dungeon.FOLDER.as_posix()
        for path in (f"{prefix}/Current.json", f"{prefix}/{sha}/NpcCatalog.json",
                     f"{prefix}/{sha}/Playfields/21/DCGA.png",
                     f"{prefix}/NpcCatalog/{sha}/NpcTemplates.json"):
            self.assertTrue(release.runtime_content_path(path), path)
        for path in (f"{prefix}/{sha}/research/Rooms.json", f"{prefix}/{sha}/Playfields/21/Other.json",
                     f"{prefix}/NpcCatalog/{sha}/Playfields.json", f"{prefix}/invalid/DungeonGeneration.json"):
            self.assertFalse(release.runtime_content_path(path), path)


if __name__ == "__main__":
    unittest.main()
