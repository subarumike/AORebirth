import hashlib
import os
import shutil
import stat
import struct
import tempfile
import unittest
import warnings
import zipfile
from pathlib import Path
from unittest import mock
from xml.etree import ElementTree

from Tools import php_runtime_supply as supply


VERSION = "8.5.9"
ARCHIVE_FILENAME = "php-8.5.9-nts-Win32-vs17-x64.zip"


def _pe_image(machine=supply.X64_PE_MACHINE):
    image = bytearray(128)
    image[:2] = b"MZ"
    struct.pack_into("<I", image, 0x3C, 64)
    image[64:68] = b"PE\x00\x00"
    struct.pack_into("<H", image, 68, machine)
    return bytes(image)


class Fixture:
    def __init__(
        self,
        root,
        zip_files=None,
        manifest_files=None,
        extra_zip_entries=None,
        root_overrides=None,
        manifest_size_overrides=None,
    ):
        self.root = Path(root)
        self.root.mkdir(parents=True)
        base_files = {
            "ext/php_pdo_mysql.dll": b"fixture-pdo-mysql",
            "php-cgi.exe": _pe_image(),
            "php8.dll": _pe_image(),
        }
        zip_files = dict(base_files if zip_files is None else zip_files)
        manifest_files = dict(base_files if manifest_files is None else manifest_files)
        extra_zip_entries = list(extra_zip_entries or [])
        self.archive = self.root / ARCHIVE_FILENAME
        with zipfile.ZipFile(self.archive, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            directory = zipfile.ZipInfo("ext/")
            directory.create_system = 0
            directory.external_attr = 0x10
            archive.writestr(directory, b"")
            for path, data in sorted(zip_files.items()):
                entry = zipfile.ZipInfo(path)
                entry.create_system = 0
                entry.external_attr = 0
                entry.compress_type = zipfile.ZIP_DEFLATED
                archive.writestr(entry, data)
            for entry, data in extra_zip_entries:
                archive.writestr(entry, data)

        self.ini = self.root / "WebEngine.php.ini"
        shutil.copyfile(supply.DEFAULT_INI, self.ini)
        archive_bytes = self.archive.read_bytes()
        ini_bytes = self.ini.read_bytes()
        overrides = dict(root_overrides or {})
        size_overrides = dict(manifest_size_overrides or {})
        file_sizes = {
            path: size_overrides.get(path, len(data))
            for path, data in manifest_files.items()
        }
        attributes = {
            "SchemaVersion": "1",
            "Id": "php-8.5.9-nts-win32-vs17-x64",
            "Authority": "The PHP Group official PHP for Windows archive",
            "OfficialUrl": (
                "https://downloads.php.net/~windows/releases/archives/"
                + ARCHIVE_FILENAME
            ),
            "Version": VERSION,
            "Architecture": "x64",
            "ThreadSafety": "NTS",
            "Toolchain": "VS17",
            "ArchiveFilename": ARCHIVE_FILENAME,
            "ArchiveSize": str(len(archive_bytes)),
            "ArchiveSha256": hashlib.sha256(archive_bytes).hexdigest(),
            "ArchiveRoot": "flat",
            "FileCount": str(len(manifest_files)),
            "DirectoryCount": "1",
            "TotalUncompressedBytes": str(sum(file_sizes.values())),
        }
        attributes.update(overrides)
        root_element = ElementTree.Element("PhpRuntimeManifest", attributes)
        ElementTree.SubElement(
            root_element,
            "Configuration",
            {
                "Source": "WebEngine.php.ini",
                "InstalledPath": "php.ini",
                "Sha256": hashlib.sha256(ini_bytes).hexdigest(),
            },
        )
        inventory = [("ext/", True)] + [(path, False) for path in manifest_files]
        for path, is_directory in sorted(inventory):
            if is_directory:
                ElementTree.SubElement(root_element, "Directory", {"Path": path})
            else:
                ElementTree.SubElement(
                    root_element,
                    "File",
                    {
                        "Path": path,
                        "Size": str(file_sizes[path]),
                        "Sha256": hashlib.sha256(manifest_files[path]).hexdigest(),
                    },
                )
        self.manifest = self.root / "PhpRuntime.manifest.xml"
        ElementTree.ElementTree(root_element).write(
            self.manifest, encoding="utf-8", xml_declaration=True)
        self.target = self.root / "runtime"

    def load(self):
        return supply.load_manifest(
            self.manifest, enforce_approved_authority=False)


class PhpRuntimeSupplyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="aorebirth-php-supply-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.fixture_index = 0

    def fixture(self, **kwargs):
        self.fixture_index += 1
        return Fixture(self.root / f"fixture-{self.fixture_index}", **kwargs)

    def test_checked_in_production_manifest_and_configuration_are_consistent(self):
        manifest = supply.load_manifest()
        supply.validate_configuration(supply.DEFAULT_INI, manifest)
        self.assertEqual("8.5.9", manifest.version)
        self.assertEqual("php-8.5.9-nts-Win32-vs17-x64.zip", manifest.archive_filename)
        self.assertEqual(78, len(manifest.files))
        self.assertEqual(6, len(manifest.directories))
        self.assertEqual(101963340, manifest.total_uncompressed_bytes)
        values, extensions = supply._parse_ini(supply.DEFAULT_INI)
        self.assertEqual(["php_pdo_mysql.dll"], extensions)
        self.assertNotIn("php_dom.dll", extensions)  # DOM is built into this pinned build.
        self.assertEqual("iso-8859-1", values["default_charset"])
        self.assertEqual("on", values["cgi.force_redirect"])
        self.assertEqual("lax", values["session.cookie_samesite"])
        self.assertEqual("off", values["session.cookie_secure"])
        self.assertEqual(
            "${aorebirth_webcore_root};${aorebirth_php_state_dir}",
            values["open_basedir"],
        )
        self.assertEqual("", values["user_ini.filename"])
        self.assertEqual(
            "exec,passthru,shell_exec,system,proc_open,popen",
            values["disable_functions"],
        )
        for key in ("error_log", "upload_tmp_dir", "sys_temp_dir", "session.save_path"):
            self.assertTrue(values[key].startswith("${aorebirth_php_state_dir}/"))
        self.assertEqual("off", values["file_uploads"])

    def test_production_manifest_version_tamper_is_rejected(self):
        path = self.root / "tampered-version.xml"
        path.write_bytes(supply.DEFAULT_MANIFEST.read_bytes().replace(
            b'Version="8.5.9"', b'Version="8.5.8"'))
        with self.assertRaisesRegex(supply.SupplyError, "manifest SHA-256"):
            supply.load_manifest(path)

    def test_production_manifest_archive_hash_tamper_is_rejected(self):
        path = self.root / "tampered-archive-hash.xml"
        path.write_bytes(supply.DEFAULT_MANIFEST.read_bytes().replace(
            supply.APPROVED_ARCHIVE_SHA256.encode("ascii"), b"0" * 64))
        with self.assertRaisesRegex(supply.SupplyError, "manifest SHA-256"):
            supply.load_manifest(path)

    def test_valid_archive_import_and_installed_tree_validation(self):
        fixture = self.fixture()
        manifest = fixture.load()
        supply.validate_archive(fixture.archive, VERSION, manifest)
        with mock.patch.object(supply, "load_manifest", return_value=manifest):
            result = supply.import_runtime(
                fixture.archive, VERSION, fixture.target, fixture.manifest, fixture.ini)
        self.assertEqual(fixture.target, result.target)
        self.assertIsNone(result.backup_cleanup_pending)
        supply.validate_installed_tree(fixture.target, manifest, fixture.ini)
        self.assertEqual(fixture.ini.read_bytes(), (fixture.target / "php.ini").read_bytes())

    def test_archive_hash_mismatch_is_rejected(self):
        fixture = self.fixture(root_overrides={"ArchiveSha256": "0" * 64})
        with self.assertRaisesRegex(supply.SupplyError, "archive SHA-256"):
            supply.validate_archive(fixture.archive, VERSION, fixture.load())

    def test_wrong_requested_version_is_rejected(self):
        fixture = self.fixture()
        with self.assertRaisesRegex(supply.SupplyError, "exactly 8.5.9"):
            supply.validate_archive(fixture.archive, "8.5.8", fixture.load())

    def test_mutable_or_renamed_archive_is_rejected(self):
        fixture = self.fixture()
        renamed = fixture.archive.with_name("php-latest.zip")
        fixture.archive.rename(renamed)
        with self.assertRaisesRegex(supply.SupplyError, "filename"):
            supply.validate_archive(renamed, VERSION, fixture.load())

    def test_remote_archive_paths_are_rejected(self):
        fixture = self.fixture()
        with self.assertRaisesRegex(supply.SupplyError, "local filesystem"):
            supply.validate_archive(
                "https://downloads.php.net/php-latest.zip", VERSION, fixture.load())

    def test_unsafe_archive_paths_are_rejected(self):
        unsafe_paths = (
            "../escape.dll",
            "/absolute.dll",
            "C:/drive.dll",
            "mixed\\separator.dll",
            "encoded%2fpath.dll",
        )
        for index, path in enumerate(unsafe_paths):
            with self.subTest(path=path):
                entry = zipfile.ZipInfo(path)
                entry.create_system = 0
                entry.external_attr = 0
                fixture = self.fixture(extra_zip_entries=[(entry, b"unsafe")])
                with self.assertRaises(supply.SupplyError):
                    supply.validate_archive(fixture.archive, VERSION, fixture.load())

    def test_archive_symlink_is_rejected(self):
        link = zipfile.ZipInfo("link.dll")
        link.create_system = 3
        link.external_attr = (stat.S_IFLNK | 0o777) << 16
        fixture = self.fixture(extra_zip_entries=[(link, b"php8.dll")])
        with self.assertRaisesRegex(supply.SupplyError, "link or special"):
            supply.validate_archive(fixture.archive, VERSION, fixture.load())

    def test_duplicate_archive_path_is_rejected(self):
        duplicate = zipfile.ZipInfo("php8.dll")
        duplicate.create_system = 0
        duplicate.external_attr = 0
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            fixture = self.fixture(extra_zip_entries=[(duplicate, _pe_image())])
        with self.assertRaisesRegex(supply.SupplyError, "duplicate or case-colliding"):
            supply.validate_archive(fixture.archive, VERSION, fixture.load())

    def test_unexpected_archive_inventory_is_rejected(self):
        unexpected = zipfile.ZipInfo("rogue.dll")
        unexpected.create_system = 0
        unexpected.external_attr = 0
        fixture = self.fixture(extra_zip_entries=[(unexpected, b"rogue")])
        with self.assertRaisesRegex(supply.SupplyError, "unexpected PHP archive file"):
            supply.validate_archive(fixture.archive, VERSION, fixture.load())

    def test_missing_required_archive_files_are_rejected(self):
        base = {
            "ext/php_pdo_mysql.dll": b"fixture-pdo-mysql",
            "php-cgi.exe": _pe_image(),
            "php8.dll": _pe_image(),
        }
        for missing in tuple(base):
            with self.subTest(missing=missing):
                zip_files = dict(base)
                del zip_files[missing]
                fixture = self.fixture(zip_files=zip_files)
                with self.assertRaisesRegex(supply.SupplyError, "inventory"):
                    supply.validate_archive(fixture.archive, VERSION, fixture.load())

    def test_manifest_missing_required_executable_dll_or_extension_is_rejected(self):
        base = {
            "ext/php_pdo_mysql.dll": b"fixture-pdo-mysql",
            "php-cgi.exe": _pe_image(),
            "php8.dll": _pe_image(),
        }
        for missing in tuple(base):
            with self.subTest(missing=missing):
                manifest_files = dict(base)
                del manifest_files[missing]
                fixture = self.fixture(
                    zip_files=manifest_files, manifest_files=manifest_files)
                with self.assertRaisesRegex(supply.SupplyError, "missing required runtime file"):
                    fixture.load()

    def test_missing_configuration_source_is_rejected(self):
        fixture = self.fixture()
        manifest = fixture.load()
        fixture.ini.unlink()
        with self.assertRaisesRegex(supply.SupplyError, "does not exist"):
            supply.validate_configuration(fixture.ini, manifest)

    def test_missing_installed_configuration_is_rejected(self):
        fixture = self.fixture()
        manifest = fixture.load()
        with mock.patch.object(supply, "load_manifest", return_value=manifest):
            supply.import_runtime(
                fixture.archive, VERSION, fixture.target, fixture.manifest, fixture.ini)
        (fixture.target / "php.ini").unlink()
        with self.assertRaisesRegex(supply.SupplyError, "inventory"):
            supply.validate_installed_tree(fixture.target, manifest, fixture.ini)

    def test_wrong_architecture_is_rejected_without_execution(self):
        files = {
            "ext/php_pdo_mysql.dll": b"fixture-pdo-mysql",
            "php-cgi.exe": _pe_image(0x014C),
            "php8.dll": _pe_image(),
        }
        fixture = self.fixture(zip_files=files, manifest_files=files)
        with self.assertRaisesRegex(supply.SupplyError, "not an x64 PE"):
            supply.validate_archive(fixture.archive, VERSION, fixture.load())

    def test_thread_safe_manifest_is_rejected(self):
        fixture = self.fixture(root_overrides={"ThreadSafety": "TS"})
        with self.assertRaisesRegex(supply.SupplyError, "only the pinned NTS"):
            fixture.load()

    def test_manifest_size_bound_is_enforced(self):
        fixture = self.fixture(
            manifest_size_overrides={"php8.dll": supply.MAX_FILE_BYTES + 1})
        with self.assertRaisesRegex(supply.SupplyError, "exceeds size bound"):
            fixture.load()

    def test_failed_activation_restores_previous_runtime(self):
        fixture = self.fixture()
        fixture.target.mkdir()
        marker = fixture.target / "old-runtime.txt"
        marker.write_text("preserve", encoding="utf-8")
        real_replace = supply.os.replace
        failed_once = False

        def fail_staging_activation(source, destination):
            nonlocal failed_once
            if (not failed_once and ".staging-" in Path(source).name
                    and Path(destination) == fixture.target):
                failed_once = True
                raise OSError("injected activation failure")
            return real_replace(source, destination)

        manifest = fixture.load()
        with mock.patch.object(supply, "load_manifest", return_value=manifest):
            with mock.patch.object(supply.os, "replace", side_effect=fail_staging_activation):
                with self.assertRaisesRegex(supply.SupplyError, "import failed"):
                    supply.import_runtime(
                        fixture.archive, VERSION, fixture.target,
                        fixture.manifest, fixture.ini)
        self.assertEqual("preserve", marker.read_text(encoding="utf-8"))
        self.assertFalse(list(fixture.root.glob("runtime.staging-*")))
        self.assertFalse(list(fixture.root.glob("runtime.backup-*")))

    @unittest.skipUnless(os.name == "nt", "Windows share-deny lease contract")
    def test_concurrent_import_is_rejected_by_share_deny_lease(self):
        fixture = self.fixture()
        lock_path = fixture.target.parent / supply.RUNTIME_LOCK_FILENAME
        with supply._RuntimeLease(lock_path):
            with self.assertRaisesRegex(supply.SupplyError, "holds the lease"):
                supply.import_runtime(
                    fixture.archive, VERSION, fixture.target,
                    fixture.manifest, fixture.ini)


if __name__ == "__main__":
    unittest.main(verbosity=2)
