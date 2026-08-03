#!/usr/bin/env python3
"""Offline, fail-closed supply and validation for AORebirth's PHP runtime."""

from __future__ import annotations

import argparse
import ctypes
import hashlib
import os
import re
import shutil
import stat
import struct
import sys
import unicodedata
import uuid
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import BinaryIO, Dict, Iterable, List, Optional, Sequence, Set, Tuple
from xml.etree import ElementTree


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MANIFEST = REPO_ROOT / "AORebirth" / "Config" / "PhpRuntime.manifest.xml"
DEFAULT_INI = REPO_ROOT / "AORebirth" / "Config" / "WebEngine.php.ini"

MAX_ARCHIVE_BYTES = 128 * 1024 * 1024
MAX_ENTRIES = 4096
MAX_FILE_BYTES = 256 * 1024 * 1024
MAX_TOTAL_BYTES = 512 * 1024 * 1024
MAX_PATH_LENGTH = 240
MAX_COMPRESSION_RATIO = 500
WINDOWS_REPARSE_ATTRIBUTE = 0x400
X64_PE_MACHINE = 0x8664
RUNTIME_LOCK_FILENAME = "PhpRuntime.runtime.lock"
HEX_64 = re.compile(r"^[0-9a-f]{64}$")
VERSION_PATTERN = re.compile(r"^[0-9]+\.[0-9]+\.[0-9]+$")
APPROVED_VERSION = "8.5.9"
APPROVED_ARCHIVE_FILENAME = "php-8.5.9-nts-Win32-vs17-x64.zip"
APPROVED_ARCHIVE_SIZE = 36_015_210
APPROVED_ARCHIVE_SHA256 = "516c2d72231bd035c8a910120834add0ad208098b790b4909b2cbeb93ce135fc"
APPROVED_MANIFEST_SHA256 = "dc962aa41501a23d993cf667c546593ef36b122f8002d8ab3fc56d1a888cd735"
WINDOWS_RESERVED_NAMES = {
    "CON", "PRN", "AUX", "NUL", "CLOCK$",
    *(f"COM{i}" for i in range(1, 10)),
    *(f"LPT{i}" for i in range(1, 10)),
}


class SupplyError(RuntimeError):
    """A deterministic PHP runtime supply contract failure."""


@dataclass(frozen=True)
class FileRecord:
    path: str
    size: int
    sha256: str


@dataclass(frozen=True)
class PhpRuntimeManifest:
    version: str
    architecture: str
    thread_safety: str
    toolchain: str
    archive_filename: str
    archive_size: int
    archive_sha256: str
    files: Tuple[FileRecord, ...]
    directories: Tuple[str, ...]
    total_uncompressed_bytes: int
    configuration_source: str
    configuration_installed_path: str
    configuration_sha256: str


@dataclass(frozen=True)
class ImportResult:
    target: Path
    backup_cleanup_pending: Optional[Path]


class _RuntimeLease:
    """A Windows share-deny lease compatible with FileStream FileShare.None."""

    def __init__(self, path: Path) -> None:
        self.path = path
        self._handle: Optional[int] = None
        self._fallback_fd: Optional[int] = None

    def __enter__(self) -> "_RuntimeLease":
        if os.name == "nt":
            from ctypes import wintypes

            kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
            create_file = kernel32.CreateFileW
            create_file.argtypes = (
                wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, wintypes.LPVOID,
                wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE,
            )
            create_file.restype = wintypes.HANDLE
            desired_access = 0x80000000 | 0x40000000 | 0x00010000
            open_always = 4
            temporary_delete_on_close = 0x00000100 | 0x04000000
            handle = create_file(
                str(self.path), desired_access, 0, None, open_always,
                temporary_delete_on_close, None,
            )
            invalid_handle = ctypes.c_void_p(-1).value
            if handle == invalid_handle:
                error = ctypes.get_last_error()
                raise SupplyError(
                    f"PHP runtime is in use or another import holds the lease: "
                    f"{self.path} (Windows error {error})")
            self._handle = handle
        else:  # Deterministic fixture support; production supply is Windows-only.
            try:
                self._fallback_fd = os.open(self.path, os.O_CREAT | os.O_EXCL | os.O_RDWR, 0o600)
            except FileExistsError as exc:
                raise SupplyError(
                    f"PHP runtime is in use or another import holds the lease: {self.path}") from exc
        return self

    def __exit__(self, exc_type, exc_value, traceback) -> None:
        if self._handle is not None:
            kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
            kernel32.CloseHandle.argtypes = (ctypes.c_void_p,)
            kernel32.CloseHandle.restype = ctypes.c_int
            kernel32.CloseHandle(self._handle)
            self._handle = None
        if self._fallback_fd is not None:
            os.close(self._fallback_fd)
            self._fallback_fd = None
            try:
                self.path.unlink()
            except FileNotFoundError:
                pass


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise SupplyError(message)


def _parse_nonnegative_int(value: Optional[str], label: str) -> int:
    _require(value is not None and re.fullmatch(r"0|[1-9][0-9]*", value) is not None,
             f"manifest {label} must be a canonical non-negative integer")
    return int(value)


def _sha256_stream(stream: BinaryIO) -> str:
    digest = hashlib.sha256()
    while True:
        chunk = stream.read(1024 * 1024)
        if not chunk:
            break
        digest.update(chunk)
    return digest.hexdigest()


def _sha256_file(path: Path) -> str:
    with path.open("rb") as stream:
        return _sha256_stream(stream)


def _is_reparse(stat_result: os.stat_result) -> bool:
    return bool(getattr(stat_result, "st_file_attributes", 0) & WINDOWS_REPARSE_ATTRIBUTE)


def _reject_reparse_path(path: Path, label: str, include_leaf: bool = True) -> None:
    absolute = Path(os.path.abspath(str(path)))
    candidates: List[Path] = []
    current = absolute if include_leaf else absolute.parent
    while True:
        candidates.append(current)
        if current.parent == current:
            break
        current = current.parent
    for candidate in reversed(candidates):
        try:
            info = candidate.lstat()
        except FileNotFoundError:
            continue
        _require(not stat.S_ISLNK(info.st_mode) and not _is_reparse(info),
                 f"{label} contains a symbolic link or reparse point: {candidate}")


def _local_path(value: os.PathLike[str] | str, label: str, must_exist: bool) -> Path:
    raw = os.fspath(value)
    _require(bool(raw) and "\x00" not in raw, f"{label} path is empty or invalid")
    _require(not raw.startswith(("\\\\", "//", "\\\\?\\", "\\\\.\\")),
             f"{label} must not be a UNC or device path")
    _require(re.match(r"^[A-Za-z][A-Za-z0-9+.-]*://", raw) is None,
             f"{label} must be a local filesystem path")
    path = Path(raw)
    if not path.is_absolute():
        path = Path.cwd() / path
    path = Path(os.path.abspath(str(path)))
    if must_exist:
        _require(path.exists(), f"{label} does not exist: {path}")
    _reject_reparse_path(path, label, include_leaf=must_exist)
    return path


def _validate_member_path(path: str, is_directory: bool, label: str) -> str:
    _require(bool(path), f"{label} has an empty path")
    _require(len(path) <= MAX_PATH_LENGTH, f"{label} path is too long: {path!r}")
    _require(path == unicodedata.normalize("NFC", path),
             f"{label} path is not NFC-normalized: {path!r}")
    _require("\\" not in path, f"{label} path uses a backslash: {path!r}")
    _require("%" not in path, f"{label} path uses encoded or ambiguous syntax: {path!r}")
    _require(":" not in path, f"{label} path uses a drive or stream separator: {path!r}")
    _require(not path.startswith("/") and not path.startswith("//"),
             f"{label} path is absolute: {path!r}")
    _require(all(ord(character) >= 32 and ord(character) != 127 for character in path),
             f"{label} path contains a control character: {path!r}")
    _require(path.endswith("/") == is_directory,
             f"{label} directory marker does not match entry kind: {path!r}")
    normalized = path[:-1] if is_directory else path
    segments = normalized.split("/")
    _require(all(segment not in ("", ".", "..") for segment in segments),
             f"{label} path contains an empty or traversal segment: {path!r}")
    for segment in segments:
        _require(not segment.endswith((" ", ".")),
                 f"{label} path has a Windows-ambiguous trailing character: {path!r}")
        basename = segment.split(".", 1)[0].upper()
        _require(basename not in WINDOWS_RESERVED_NAMES,
                 f"{label} path uses a reserved Windows name: {path!r}")
    return normalized


def _check_path_collisions(entries: Iterable[Tuple[str, bool]], label: str) -> None:
    canonical: Dict[str, Tuple[str, bool]] = {}
    for path, is_directory in entries:
        normalized = _validate_member_path(path, is_directory, label)
        key = normalized.casefold()
        _require(key not in canonical,
                 f"{label} has a duplicate or case-colliding path: {path!r}")
        canonical[key] = (normalized, is_directory)
    for normalized, _ in canonical.values():
        segments = normalized.split("/")
        for index in range(1, len(segments)):
            parent = "/".join(segments[:index])
            collision = canonical.get(parent.casefold())
            _require(collision is None or collision[1],
                     f"{label} has a file/directory collision at {parent!r}")


def _read_xml_without_entities(path: Path) -> ElementTree.Element:
    data = path.read_bytes()
    lowered = data.lower()
    _require(b"<!doctype" not in lowered and b"<!entity" not in lowered,
             "manifest declarations and entities are forbidden")
    try:
        return ElementTree.fromstring(data)
    except ElementTree.ParseError as exc:
        raise SupplyError(f"manifest XML is invalid: {exc}") from exc


def load_manifest(
    path: os.PathLike[str] | str = DEFAULT_MANIFEST,
    *,
    enforce_approved_authority: bool = True,
) -> PhpRuntimeManifest:
    manifest_path = _local_path(path, "manifest", must_exist=True)
    _require(manifest_path.is_file(), f"manifest is not a regular file: {manifest_path}")
    if enforce_approved_authority:
        _require(_sha256_file(manifest_path) == APPROVED_MANIFEST_SHA256,
                 "manifest SHA-256 does not match the approved authority")
    root = _read_xml_without_entities(manifest_path)
    required_root_attributes = {
        "SchemaVersion", "Id", "Authority", "OfficialUrl", "Version",
        "Architecture", "ThreadSafety", "Toolchain", "ArchiveFilename",
        "ArchiveSize", "ArchiveSha256", "ArchiveRoot", "FileCount",
        "DirectoryCount", "TotalUncompressedBytes",
    }
    _require(root.tag == "PhpRuntimeManifest", "manifest root must be PhpRuntimeManifest")
    _require(set(root.attrib) == required_root_attributes,
             "manifest root attributes do not match schema version 1")
    _require(root.attrib["SchemaVersion"] == "1", "unsupported manifest schema version")
    version = root.attrib["Version"]
    _require(VERSION_PATTERN.fullmatch(version) is not None, "manifest version is invalid")
    expected_filename = f"php-{version}-nts-Win32-vs17-x64.zip"
    _require(root.attrib["Id"] == f"php-{version}-nts-win32-vs17-x64",
             "manifest identity does not match version/build")
    _require(root.attrib["Authority"] == "The PHP Group official PHP for Windows archive",
             "manifest authority is not the approved official publisher")
    _require(root.attrib["Architecture"] == "x64", "only the pinned x64 runtime is allowed")
    _require(root.attrib["ThreadSafety"] == "NTS", "only the pinned NTS runtime is allowed")
    _require(root.attrib["Toolchain"] == "VS17", "only the pinned VS17 runtime is allowed")
    _require(root.attrib["ArchiveRoot"] == "flat", "PHP archive must have a flat root")
    _require(root.attrib["ArchiveFilename"] == expected_filename,
             "manifest archive filename is not the exact immutable release filename")
    expected_url = f"https://downloads.php.net/~windows/releases/archives/{expected_filename}"
    _require(root.attrib["OfficialUrl"] == expected_url,
             "manifest provenance URL does not match the official archive identity")
    archive_size = _parse_nonnegative_int(root.attrib.get("ArchiveSize"), "ArchiveSize")
    _require(0 < archive_size <= MAX_ARCHIVE_BYTES, "manifest archive size exceeds bounds")
    archive_sha256 = root.attrib["ArchiveSha256"]
    _require(HEX_64.fullmatch(archive_sha256) is not None,
             "manifest archive SHA-256 is invalid")
    if enforce_approved_authority:
        _require(version == APPROVED_VERSION, "manifest version is not the approved release")
        _require(root.attrib["ArchiveFilename"] == APPROVED_ARCHIVE_FILENAME,
                 "manifest archive filename is not approved")
        _require(archive_size == APPROVED_ARCHIVE_SIZE,
                 "manifest archive size is not approved")
        _require(archive_sha256 == APPROVED_ARCHIVE_SHA256,
                 "manifest archive SHA-256 is not approved")

    children = list(root)
    _require(bool(children) and children[0].tag == "Configuration",
             "manifest must begin with one Configuration element")
    configuration = children[0]
    _require(set(configuration.attrib) == {"Source", "InstalledPath", "Sha256"},
             "manifest Configuration attributes do not match schema")
    _require(configuration.attrib["Source"] == "WebEngine.php.ini",
             "manifest configuration source is not approved")
    _require(configuration.attrib["InstalledPath"] == "php.ini",
             "manifest configuration install path is not approved")
    _require(HEX_64.fullmatch(configuration.attrib["Sha256"]) is not None,
             "manifest configuration SHA-256 is invalid")

    files: List[FileRecord] = []
    directories: List[str] = []
    ordered_paths: List[str] = []
    for child in children[1:]:
        if child.tag == "File":
            _require(set(child.attrib) == {"Path", "Size", "Sha256"},
                     "manifest File attributes do not match schema")
            file_path = child.attrib["Path"]
            size = _parse_nonnegative_int(child.attrib.get("Size"), f"File[{file_path}].Size")
            _require(size <= MAX_FILE_BYTES, f"manifest file exceeds size bound: {file_path}")
            digest = child.attrib["Sha256"]
            _require(HEX_64.fullmatch(digest) is not None,
                     f"manifest file SHA-256 is invalid: {file_path}")
            files.append(FileRecord(file_path, size, digest))
            ordered_paths.append(file_path)
        elif child.tag == "Directory":
            _require(set(child.attrib) == {"Path"},
                     "manifest Directory attributes do not match schema")
            directories.append(child.attrib["Path"])
            ordered_paths.append(child.attrib["Path"])
        else:
            raise SupplyError(f"unexpected manifest element: {child.tag}")
        _require(child.text is None or not child.text.strip(),
                 f"manifest element {child.tag} must not contain text")
        _require(len(list(child)) == 0, f"manifest element {child.tag} must be empty")

    _require(ordered_paths == sorted(ordered_paths), "manifest inventory is not ordinally sorted")
    _require(len(ordered_paths) <= MAX_ENTRIES, "manifest entry count exceeds bounds")
    _check_path_collisions(
        [(record.path, False) for record in files] + [(path, True) for path in directories],
        "manifest",
    )
    file_count = _parse_nonnegative_int(root.attrib.get("FileCount"), "FileCount")
    directory_count = _parse_nonnegative_int(root.attrib.get("DirectoryCount"), "DirectoryCount")
    total_bytes = _parse_nonnegative_int(
        root.attrib.get("TotalUncompressedBytes"), "TotalUncompressedBytes")
    _require(file_count == len(files), "manifest FileCount does not match inventory")
    _require(directory_count == len(directories),
             "manifest DirectoryCount does not match inventory")
    _require(total_bytes == sum(record.size for record in files),
             "manifest total byte count does not match inventory")
    _require(total_bytes <= MAX_TOTAL_BYTES, "manifest total byte count exceeds bounds")
    file_paths = {record.path.casefold() for record in files}
    for required in ("php-cgi.exe", "php8.dll", "ext/php_pdo_mysql.dll"):
        _require(required.casefold() in file_paths,
                 f"manifest is missing required runtime file: {required}")
    _require("php8ts.dll" not in file_paths, "manifest contains a thread-safe PHP core DLL")
    _require(configuration.attrib["InstalledPath"].casefold() not in file_paths,
             "configuration install path collides with archive inventory")
    return PhpRuntimeManifest(
        version=version,
        architecture=root.attrib["Architecture"],
        thread_safety=root.attrib["ThreadSafety"],
        toolchain=root.attrib["Toolchain"],
        archive_filename=root.attrib["ArchiveFilename"],
        archive_size=archive_size,
        archive_sha256=archive_sha256,
        files=tuple(files),
        directories=tuple(directories),
        total_uncompressed_bytes=total_bytes,
        configuration_source=configuration.attrib["Source"],
        configuration_installed_path=configuration.attrib["InstalledPath"],
        configuration_sha256=configuration.attrib["Sha256"],
    )


def _parse_ini(path: Path) -> Tuple[Dict[str, str], List[str]]:
    values: Dict[str, str] = {}
    extensions: List[str] = []
    try:
        lines = path.read_text(encoding="utf-8").splitlines()
    except UnicodeDecodeError as exc:
        raise SupplyError("PHP configuration must be UTF-8 text") from exc
    for raw_line in lines:
        line = raw_line.strip()
        if not line or line.startswith((";", "#", "[")):
            continue
        _require("=" in line, f"PHP configuration contains an invalid directive: {line!r}")
        key, value = (part.strip() for part in line.split("=", 1))
        key = key.casefold()
        value = value.strip().strip('"').strip("'")
        if key == "extension":
            extensions.append(value.casefold())
        else:
            _require(key not in values, f"PHP configuration repeats directive: {key}")
            values[key] = value.casefold()
    return values, extensions


def validate_configuration(path: os.PathLike[str] | str, manifest: PhpRuntimeManifest) -> Path:
    ini_path = _local_path(path, "PHP configuration", must_exist=True)
    _require(ini_path.is_file(), f"PHP configuration is not a regular file: {ini_path}")
    _require(ini_path.name == manifest.configuration_source,
             "PHP configuration filename does not match manifest")
    _require(_sha256_file(ini_path) == manifest.configuration_sha256,
             "PHP configuration SHA-256 does not match manifest")
    values, extensions = _parse_ini(ini_path)
    required_values = {
        "engine": "on",
        "short_open_tag": "off",
        "expose_php": "off",
        "enable_dl": "off",
        "disable_functions": "exec,passthru,shell_exec,system,proc_open,popen",
        "open_basedir": "${aorebirth_webcore_root};${aorebirth_php_state_dir}",
        "user_ini.filename": "",
        "default_charset": "iso-8859-1",
        "date.timezone": "utc",
        "display_errors": "off",
        "display_startup_errors": "off",
        "log_errors": "on",
        "allow_url_fopen": "off",
        "allow_url_include": "off",
        "file_uploads": "off",
        "extension_dir": "ext",
        "cgi.force_redirect": "on",
        "cgi.fix_pathinfo": "off",
        "cgi.discard_path": "on",
        "fastcgi.impersonate": "off",
        "session.use_strict_mode": "on",
        "session.use_cookies": "on",
        "session.use_only_cookies": "on",
        "session.use_trans_sid": "off",
        "session.cookie_httponly": "on",
        "session.cookie_samesite": "lax",
        "session.cookie_secure": "off",
    }
    for key, expected in required_values.items():
        _require(values.get(key) == expected,
                 f"PHP configuration directive {key} must be {expected}")
    _require(extensions == ["php_pdo_mysql.dll"],
             "PHP configuration must enable only php_pdo_mysql.dll")
    for key in ("error_log", "upload_tmp_dir", "sys_temp_dir", "session.save_path"):
        value = values.get(key, "")
        _require(value.startswith("${aorebirth_php_state_dir}/")
                 and ":" not in value and "\\" not in value,
                 f"PHP configuration {key} must use AOREBIRTH_PHP_STATE_DIR")
    return ini_path


def _validate_zip_entry_type(info: zipfile.ZipInfo) -> None:
    _require(not (info.flag_bits & 0x1), f"encrypted ZIP entry is forbidden: {info.filename!r}")
    _require(not (info.external_attr & WINDOWS_REPARSE_ATTRIBUTE),
             f"ZIP reparse-point entry is forbidden: {info.filename!r}")
    if info.create_system == 3:
        file_type = stat.S_IFMT(info.external_attr >> 16)
        allowed_type = stat.S_IFDIR if info.is_dir() else stat.S_IFREG
        _require(file_type in (0, allowed_type),
                 f"ZIP link or special entry is forbidden: {info.filename!r}")


def _pe_machine(stream: BinaryIO, label: str) -> int:
    header = stream.read(64)
    _require(len(header) >= 64 and header[:2] == b"MZ", f"{label} is not a PE executable")
    pe_offset = struct.unpack_from("<I", header, 0x3C)[0]
    _require(64 <= pe_offset <= MAX_FILE_BYTES - 6, f"{label} has an invalid PE header offset")
    stream.seek(pe_offset)
    pe_header = stream.read(6)
    _require(len(pe_header) == 6 and pe_header[:4] == b"PE\x00\x00",
             f"{label} has an invalid PE signature")
    return struct.unpack_from("<H", pe_header, 4)[0]


def _validate_x64_pe_file(path: Path, label: str) -> None:
    with path.open("rb") as stream:
        _require(_pe_machine(stream, label) == X64_PE_MACHINE,
                 f"{label} is not an x64 PE image")


def validate_archive(
    archive: os.PathLike[str] | str,
    requested_version: str,
    manifest: PhpRuntimeManifest,
) -> Path:
    archive_path = _local_path(archive, "PHP archive", must_exist=True)
    _require(archive_path.is_file(), f"PHP archive is not a regular file: {archive_path}")
    _require(requested_version == manifest.version,
             f"requested PHP version must be exactly {manifest.version}")
    _require(archive_path.name == manifest.archive_filename,
             "PHP archive filename does not match the pinned immutable release filename")
    archive_stat = archive_path.stat()
    _require(archive_stat.st_size == manifest.archive_size,
             "PHP archive size does not match manifest")
    _require(archive_stat.st_size <= MAX_ARCHIVE_BYTES, "PHP archive exceeds size bound")

    expected_files = {record.path: record for record in manifest.files}
    expected_directories = set(manifest.directories)
    with archive_path.open("rb") as archive_stream:
        _require(_sha256_stream(archive_stream) == manifest.archive_sha256,
                 "PHP archive SHA-256 does not match manifest")
        archive_stream.seek(0)
        try:
            with zipfile.ZipFile(archive_stream, "r") as zip_file:
                infos = zip_file.infolist()
                _require(len(infos) <= MAX_ENTRIES, "PHP archive entry count exceeds bounds")
                entries: List[Tuple[str, bool]] = []
                actual_names: Set[str] = set()
                total_bytes = 0
                for info in infos:
                    is_directory = info.is_dir()
                    _validate_zip_entry_type(info)
                    _validate_member_path(info.filename, is_directory, "PHP archive")
                    entries.append((info.filename, is_directory))
                    actual_names.add(info.filename)
                    if is_directory:
                        _require(info.file_size == 0, f"ZIP directory is not empty: {info.filename}")
                        _require(info.filename in expected_directories,
                                 f"unexpected PHP archive directory: {info.filename}")
                        continue
                    record = expected_files.get(info.filename)
                    _require(record is not None, f"unexpected PHP archive file: {info.filename}")
                    _require(info.file_size == record.size,
                             f"PHP archive file size mismatch: {info.filename}")
                    _require(info.file_size <= MAX_FILE_BYTES,
                             f"PHP archive file exceeds size bound: {info.filename}")
                    if info.file_size > 1024 * 1024:
                        _require(info.compress_size > 0 and
                                 info.file_size <= info.compress_size * MAX_COMPRESSION_RATIO,
                                 f"PHP archive entry exceeds compression-ratio bound: {info.filename}")
                    total_bytes += info.file_size
                    _require(total_bytes <= MAX_TOTAL_BYTES,
                             "PHP archive total expanded size exceeds bounds")
                _check_path_collisions(entries, "PHP archive")
                expected_names = set(expected_files) | expected_directories
                _require(actual_names == expected_names,
                         "PHP archive inventory is missing one or more manifest entries")
                _require(total_bytes == manifest.total_uncompressed_bytes,
                         "PHP archive expanded byte count does not match manifest")
                for path, record in expected_files.items():
                    with zip_file.open(path, "r") as entry_stream:
                        _require(_sha256_stream(entry_stream) == record.sha256,
                                 f"PHP archive file SHA-256 mismatch: {path}")
                for pe_path in ("php-cgi.exe", "php8.dll"):
                    with zip_file.open(pe_path, "r") as pe_stream:
                        _require(_pe_machine(pe_stream, pe_path) == X64_PE_MACHINE,
                                 f"{pe_path} is not an x64 PE image")
        except (zipfile.BadZipFile, EOFError, OSError) as exc:
            raise SupplyError(f"PHP archive is invalid or corrupt: {exc}") from exc
    return archive_path


def _expected_installed_directories(manifest: PhpRuntimeManifest) -> Set[str]:
    directories = {path[:-1] for path in manifest.directories}
    for record in manifest.files:
        parts = record.path.split("/")[:-1]
        for index in range(1, len(parts) + 1):
            directories.add("/".join(parts[:index]))
    return directories


def _walk_installed_tree(root: Path) -> Tuple[Dict[str, Path], Set[str]]:
    files: Dict[str, Path] = {}
    directories: Set[str] = set()

    def visit(directory: Path, relative: str) -> None:
        with os.scandir(directory) as entries:
            for entry in entries:
                entry_path = Path(entry.path)
                info = entry.stat(follow_symlinks=False)
                is_directory = entry.is_dir(follow_symlinks=False)
                _require(not entry.is_symlink() and not _is_reparse(info),
                         f"installed runtime contains a link or reparse point: {entry_path}")
                child_relative = f"{relative}/{entry.name}" if relative else entry.name
                normalized = child_relative.replace(os.sep, "/")
                validation_path = f"{normalized}/" if is_directory else normalized
                _validate_member_path(validation_path, is_directory, "installed runtime")
                if is_directory:
                    directories.add(normalized)
                    visit(entry_path, normalized)
                elif entry.is_file(follow_symlinks=False):
                    files[normalized] = entry_path
                else:
                    raise SupplyError(
                        f"installed runtime contains a special file: {entry_path}")

    visit(root, "")
    _check_path_collisions(
        [(path, False) for path in files] + [(f"{path}/", True) for path in directories],
        "installed runtime",
    )
    return files, directories


def validate_installed_tree(
    target: os.PathLike[str] | str,
    manifest: PhpRuntimeManifest,
    ini_path: os.PathLike[str] | str = DEFAULT_INI,
) -> Path:
    target_path = _local_path(target, "PHP runtime target", must_exist=True)
    _require(target_path.is_dir(), f"PHP runtime target is not a directory: {target_path}")
    validate_configuration(ini_path, manifest)
    files, directories = _walk_installed_tree(target_path)
    expected_files = {record.path: record for record in manifest.files}
    expected_files[manifest.configuration_installed_path] = FileRecord(
        manifest.configuration_installed_path, -1, manifest.configuration_sha256)
    _require(set(files) == set(expected_files),
             "installed PHP runtime file inventory does not match manifest plus configuration")
    _require(directories == _expected_installed_directories(manifest),
             "installed PHP runtime directory inventory does not match manifest")
    for path, record in expected_files.items():
        installed = files[path]
        if record.size >= 0:
            _require(installed.stat().st_size == record.size,
                     f"installed PHP runtime file size mismatch: {path}")
        _require(_sha256_file(installed) == record.sha256,
                 f"installed PHP runtime file SHA-256 mismatch: {path}")
    _validate_x64_pe_file(files["php-cgi.exe"], "installed php-cgi.exe")
    _validate_x64_pe_file(files["php8.dll"], "installed php8.dll")
    return target_path


def _safe_remove_tree(path: Path, parent: Path, prefix: str) -> None:
    _require(path.parent == parent and path.name.startswith(prefix),
             f"refusing to remove unexpected temporary path: {path}")
    if path.exists():
        _reject_reparse_path(path, "temporary PHP runtime tree", include_leaf=True)
        shutil.rmtree(path)


def _extract_validated_archive(archive: Path, staging: Path, manifest: PhpRuntimeManifest) -> None:
    expected_files = {record.path: record for record in manifest.files}
    with archive.open("rb") as archive_stream:
        with zipfile.ZipFile(archive_stream, "r") as zip_file:
            for info in zip_file.infolist():
                relative = info.filename[:-1] if info.is_dir() else info.filename
                destination = staging.joinpath(*relative.split("/"))
                _require(os.path.commonpath((str(staging), str(destination))) == str(staging),
                         f"PHP archive entry escapes staging tree: {info.filename}")
                if info.is_dir():
                    destination.mkdir(parents=True, exist_ok=True)
                    continue
                destination.parent.mkdir(parents=True, exist_ok=True)
                record = expected_files[info.filename]
                digest = hashlib.sha256()
                written = 0
                with zip_file.open(info, "r") as source, destination.open("xb") as output:
                    while True:
                        chunk = source.read(1024 * 1024)
                        if not chunk:
                            break
                        written += len(chunk)
                        _require(written <= record.size,
                                 f"PHP archive expanded beyond declared size: {info.filename}")
                        digest.update(chunk)
                        output.write(chunk)
                _require(written == record.size and digest.hexdigest() == record.sha256,
                         f"PHP archive changed during extraction: {info.filename}")


def import_runtime(
    archive: os.PathLike[str] | str,
    requested_version: str,
    target: os.PathLike[str] | str,
    manifest_path: os.PathLike[str] | str = DEFAULT_MANIFEST,
    ini_path: os.PathLike[str] | str = DEFAULT_INI,
) -> ImportResult:
    target_path = _local_path(target, "PHP runtime target", must_exist=False)
    _require(target_path.parent != target_path and bool(target_path.name),
             "PHP runtime target must not be a filesystem root")
    _require(target_path.parent.exists() and target_path.parent.is_dir(),
             f"PHP runtime target parent must already exist: {target_path.parent}")
    _reject_reparse_path(target_path.parent, "PHP runtime target parent", include_leaf=True)
    with _RuntimeLease(target_path.parent / RUNTIME_LOCK_FILENAME):
        return _import_runtime_locked(
            archive, requested_version, target_path, manifest_path, ini_path)


def _import_runtime_locked(
    archive: os.PathLike[str] | str,
    requested_version: str,
    target_path: Path,
    manifest_path: os.PathLike[str] | str,
    ini_path: os.PathLike[str] | str,
) -> ImportResult:
    manifest = load_manifest(manifest_path)
    source_ini = validate_configuration(ini_path, manifest)
    archive_path = validate_archive(archive, requested_version, manifest)
    _reject_reparse_path(target_path.parent, "PHP runtime target parent", include_leaf=True)
    if target_path.exists():
        _require(target_path.is_dir(), "existing PHP runtime target is not a directory")
        _reject_reparse_path(target_path, "existing PHP runtime target", include_leaf=True)

    token = uuid.uuid4().hex
    staging = target_path.parent / f"{target_path.name}.staging-{token}"
    backup = target_path.parent / f"{target_path.name}.backup-{token}"
    backup_cleanup_pending: Optional[Path] = None
    moved_old = False
    activated = False
    try:
        staging.mkdir()
        _extract_validated_archive(archive_path, staging, manifest)
        installed_ini = staging / manifest.configuration_installed_path
        with source_ini.open("rb") as source, installed_ini.open("xb") as destination:
            shutil.copyfileobj(source, destination, length=1024 * 1024)
        _require(_sha256_file(installed_ini) == manifest.configuration_sha256,
                 "PHP configuration changed while copying into staging")
        validate_installed_tree(staging, manifest, source_ini)
        if target_path.exists():
            os.replace(target_path, backup)
            moved_old = True
        os.replace(staging, target_path)
        activated = True
        validate_installed_tree(target_path, manifest, source_ini)
    except Exception as exc:
        rollback_error: Optional[Exception] = None
        if activated:
            failed = target_path.parent / f"{target_path.name}.failed-{token}"
            try:
                os.replace(target_path, failed)
                if moved_old:
                    os.replace(backup, target_path)
                _safe_remove_tree(failed, target_path.parent, f"{target_path.name}.failed-")
            except Exception as rollback_exc:  # pragma: no cover - catastrophic OS failure
                rollback_error = rollback_exc
        elif moved_old and not target_path.exists():
            try:
                os.replace(backup, target_path)
            except Exception as rollback_exc:  # pragma: no cover - catastrophic OS failure
                rollback_error = rollback_exc
        if staging.exists():
            _safe_remove_tree(staging, target_path.parent, f"{target_path.name}.staging-")
        if rollback_error is not None:
            raise SupplyError(
                f"PHP runtime import failed ({exc}); rollback also failed ({rollback_error}); "
                f"preserved backup: {backup}"
            ) from exc
        if isinstance(exc, SupplyError):
            raise
        raise SupplyError(f"PHP runtime import failed: {exc}") from exc

    if moved_old and backup.exists():
        try:
            _safe_remove_tree(backup, target_path.parent, f"{target_path.name}.backup-")
        except OSError:
            backup_cleanup_pending = backup
    return ImportResult(target_path, backup_cleanup_pending)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", default=str(DEFAULT_MANIFEST))
    parser.add_argument("--ini", default=str(DEFAULT_INI))
    subparsers = parser.add_subparsers(dest="command", required=True)

    subparsers.add_parser("validate-manifest")

    archive_parser = subparsers.add_parser("validate-archive")
    archive_parser.add_argument("--archive", required=True)
    archive_parser.add_argument("--version", required=True)

    import_parser = subparsers.add_parser("import")
    import_parser.add_argument("--archive", required=True)
    import_parser.add_argument("--version", required=True)
    import_parser.add_argument("--target", required=True)

    installed_parser = subparsers.add_parser("validate-installed")
    installed_parser.add_argument("--target", required=True)
    return parser


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = _build_parser().parse_args(argv)
    try:
        manifest = load_manifest(args.manifest)
        if args.command == "validate-manifest":
            validate_configuration(args.ini, manifest)
            print(f"PASS PHP runtime manifest {manifest.version} {manifest.thread_safety} {manifest.architecture}")
        elif args.command == "validate-archive":
            validate_configuration(args.ini, manifest)
            validate_archive(args.archive, args.version, manifest)
            print(f"PASS PHP runtime archive {manifest.archive_filename}")
        elif args.command == "import":
            result = import_runtime(
                args.archive, args.version, args.target, args.manifest, args.ini)
            suffix = (f" backup-cleanup=pending:{result.backup_cleanup_pending}"
                      if result.backup_cleanup_pending else "")
            print(f"PASS PHP runtime import {result.target}{suffix}")
        elif args.command == "validate-installed":
            validate_installed_tree(args.target, manifest, args.ini)
            print(f"PASS PHP runtime installed tree {Path(args.target)}")
        else:  # pragma: no cover - argparse owns command validation
            raise SupplyError(f"unsupported command: {args.command}")
        return 0
    except SupplyError as exc:
        print(f"FAIL PHP runtime supply: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
