"""Cross-process leases, immutable input snapshots, and recoverable publication.

The layer is deliberately byte-agnostic: generators retain ownership of discovery,
rendering, and semantic validation.  This module only makes a prepared byte cohort
safe to check or publish.  Repository-supported readers must take a read lease;
writers take a write lease.  A commit marker, when supplied, is replaced last.
"""

from __future__ import annotations

import ctypes
import hashlib
import json
import os
import re
import secrets
import shutil
import stat
import time
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Iterable, Mapping, Optional, Sequence


MAX_LEASE_WAIT_SECONDS = 600.0
_CHUNK = 1024 * 1024
_HEX64 = re.compile(r"^[0-9a-f]{64}$")
_ID = re.compile(r"^[0-9a-f]{32}$")
_WINDOWS_REPLACE_RETRY_SECONDS = 0.5
_WINDOWS_TRANSIENT_REPLACE_ERRORS = frozenset({5, 32, 33})


class GeneratedArtifactError(RuntimeError):
    pass


class ArtifactLeaseBusy(GeneratedArtifactError):
    pass


class DelegationError(GeneratedArtifactError):
    pass


class InputChangedError(GeneratedArtifactError):
    def __init__(self, changes: Sequence[str]) -> None:
        self.changes = tuple(changes)
        super().__init__("generated-artifact inputs changed: " + "; ".join(self.changes))


class PendingRecoveryError(GeneratedArtifactError):
    pass


class ArtifactTransactionError(GeneratedArtifactError):
    pass


class SimulatedCrash(BaseException):
    """Fault-hook sentinel which intentionally bypasses caught-failure rollback."""


def _sha_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha_file(path: Path) -> tuple[str, int]:
    digest = hashlib.sha256()
    length = 0
    with path.open("rb") as handle:
        while True:
            block = handle.read(_CHUNK)
            if not block:
                break
            digest.update(block)
            length += len(block)
    return digest.hexdigest(), length


def _write_durable(path: Path, payload: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("xb") as handle:
        handle.write(payload)
        handle.flush()
        os.fsync(handle.fileno())


def _error_detail(error: BaseException, maximum: int = 2000) -> str:
    detail = " ".join(str(error).split())
    return (detail or type(error).__name__)[:maximum]


def _replace_with_retry(source: Path, destination: Path, operation: str) -> None:
    deadline = time.monotonic() + _WINDOWS_REPLACE_RETRY_SECONDS
    attempts = 0
    while True:
        attempts += 1
        try:
            os.replace(source, destination)
            return
        except PermissionError as error:
            winerror = getattr(error, "winerror", None)
            if (
                os.name != "nt"
                or winerror not in _WINDOWS_TRANSIENT_REPLACE_ERRORS
                or time.monotonic() >= deadline
            ):
                raise ArtifactTransactionError(
                    "atomic replace failed "
                    f"operation={operation} source={source} destination={destination} "
                    f"winerror={winerror} attempts={attempts}: {_error_detail(error)}"
                ) from error
            time.sleep(min(0.025, max(0.0, deadline - time.monotonic())))


def _replace_json(path: Path, value: Mapping[str, Any]) -> None:
    payload = (json.dumps(value, ensure_ascii=True, separators=(",", ":"), sort_keys=True) + "\n").encode("utf-8")
    temporary = path.with_name(path.name + ".next-" + uuid.uuid4().hex)
    try:
        _write_durable(temporary, payload)
        _replace_with_retry(temporary, path, "replace-journal")
    finally:
        temporary.unlink(missing_ok=True)


def _repo_root(value: os.PathLike[str] | str) -> Path:
    root = Path(value).resolve(strict=True)
    if not root.is_dir():
        raise GeneratedArtifactError("repository root is not a directory")
    return root


def _control_root(root: Path) -> Path:
    dot_git = root / ".git"
    if dot_git.is_dir():
        control = dot_git / "aorebirth-generated-artifacts"
    elif dot_git.is_file():
        text = dot_git.read_text(encoding="utf-8").strip()
        if not text.lower().startswith("gitdir:"):
            raise GeneratedArtifactError("repository .git pointer is invalid")
        git_dir = Path(text.split(":", 1)[1].strip())
        if not git_dir.is_absolute():
            git_dir = (root / git_dir).resolve(strict=True)
        control = git_dir / "aorebirth-generated-artifacts"
    else:
        raise GeneratedArtifactError("repository .git directory is missing")
    control.parent.mkdir(parents=True, exist_ok=True)
    if os.name == "nt":
        if os.path.splitdrive(str(control))[0].casefold() != os.path.splitdrive(str(root))[0].casefold():
            raise GeneratedArtifactError("generated-artifact state must stay on the repository volume")
    elif control.parent.stat().st_dev != root.stat().st_dev:
        raise GeneratedArtifactError("generated-artifact state must stay on the repository volume")
    control.mkdir(parents=True, exist_ok=True)
    return control


def _domain_key(domain: str) -> str:
    if not isinstance(domain, str) or not domain.strip() or len(domain) > 200:
        raise GeneratedArtifactError("lease domain is invalid")
    slug = re.sub(r"[^A-Za-z0-9_.-]+", "-", domain.strip()).strip("-.")[:40] or "domain"
    return slug + "-" + hashlib.sha256(domain.encode("utf-8")).hexdigest()[:16]


def _is_reparse_status(status: os.stat_result) -> bool:
    reparse_attribute = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return stat.S_ISLNK(status.st_mode) or bool(
        getattr(status, "st_file_attributes", 0) & reparse_attribute
    )


def _contained(root: Path, value: os.PathLike[str] | str) -> tuple[Path, str]:
    candidate = Path(value)
    if any(part == ".." for part in candidate.parts):
        raise GeneratedArtifactError("generated-artifact path contains parent traversal")
    full = Path(
        os.path.abspath(os.fspath(candidate if candidate.is_absolute() else root / candidate))
    )
    try:
        relative = full.relative_to(root).as_posix()
    except ValueError as error:
        raise GeneratedArtifactError("generated-artifact path is outside the repository") from error
    if not relative or relative == "." or any(part in ("", ".", "..") for part in Path(relative).parts):
        raise GeneratedArtifactError("generated-artifact path is invalid")
    current = root
    for part in Path(relative).parts:
        current = current / part
        try:
            status = os.lstat(current)
        except FileNotFoundError:
            continue
        if _is_reparse_status(status):
            raise GeneratedArtifactError(
                "generated-artifact path contains a symlink or reparse point"
            )
    return full, relative


def _pid_alive(pid: int) -> bool:
    if pid <= 0:
        return False
    if os.name == "nt":
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        open_process = kernel32.OpenProcess
        open_process.argtypes = (ctypes.c_uint32, ctypes.c_int, ctypes.c_uint32)
        open_process.restype = ctypes.c_void_p
        handle = open_process(0x1000, 0, pid)
        if not handle:
            return False
        close = kernel32.CloseHandle
        close.argtypes = (ctypes.c_void_p,)
        close.restype = ctypes.c_int
        close(ctypes.c_void_p(handle))
        return True
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


def _windows_open(path: Path, mode: str, delete_on_close: bool = True) -> int:
    from ctypes import wintypes

    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    create = kernel32.CreateFileW
    create.argtypes = (wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, wintypes.LPVOID,
                       wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE)
    create.restype = wintypes.HANDLE
    delete = 0x00010000
    read, write = 0x80000000, 0x40000000
    share_read, share_delete = 0x1, 0x4
    desired = read | delete | (write if mode == "write" else 0)
    sharing = 0 if mode == "write" else share_read | share_delete
    flags = 0x100 | (0x04000000 if delete_on_close else 0)
    handle = create(str(path), desired, sharing, None, 4, flags, None)
    if handle == ctypes.c_void_p(-1).value:
        raise OSError(ctypes.get_last_error(), "CreateFileW lease acquisition failed")
    return int(handle)


def _windows_read_shared_delete(path: Path, maximum_bytes: int) -> bytes:
    """Read a delete-pending lease record without weakening its lifetime binding."""
    from ctypes import wintypes

    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    create = kernel32.CreateFileW
    create.argtypes = (wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, wintypes.LPVOID,
                       wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE)
    create.restype = wintypes.HANDLE
    handle = create(str(path), 0x80000000, 0x1 | 0x2 | 0x4, None, 3, 0x100, None)
    if handle == ctypes.c_void_p(-1).value:
        raise OSError(ctypes.get_last_error(), "CreateFileW owner-record read failed")
    size_query = kernel32.GetFileSizeEx
    size_query.argtypes = (wintypes.HANDLE, ctypes.POINTER(ctypes.c_longlong))
    size_query.restype = wintypes.BOOL
    read_file = kernel32.ReadFile
    read_file.argtypes = (wintypes.HANDLE, wintypes.LPVOID, wintypes.DWORD,
                          ctypes.POINTER(wintypes.DWORD), wintypes.LPVOID)
    read_file.restype = wintypes.BOOL
    close = kernel32.CloseHandle
    close.argtypes = (wintypes.HANDLE,)
    close.restype = wintypes.BOOL
    try:
        size = ctypes.c_longlong()
        if not size_query(wintypes.HANDLE(handle), ctypes.byref(size)):
            raise OSError(ctypes.get_last_error(), "GetFileSizeEx owner-record read failed")
        if size.value < 0 or size.value > maximum_bytes:
            raise ValueError("owner record exceeds maximum size")
        if size.value == 0:
            return b""
        buffer = ctypes.create_string_buffer(size.value)
        received = wintypes.DWORD()
        if not read_file(wintypes.HANDLE(handle), buffer, size.value,
                         ctypes.byref(received), None):
            raise OSError(ctypes.get_last_error(), "ReadFile owner-record read failed")
        if received.value != size.value:
            raise OSError("owner-record read was incomplete")
        return buffer.raw
    finally:
        close(wintypes.HANDLE(handle))


def _close_native(handle: int) -> None:
    if os.name == "nt":
        close = ctypes.WinDLL("kernel32", use_last_error=True).CloseHandle
        close.argtypes = (ctypes.c_void_p,)
        close.restype = ctypes.c_int
        close(ctypes.c_void_p(handle))
    else:
        import fcntl

        fcntl.flock(handle, fcntl.LOCK_UN)
        os.close(handle)


class GeneratedArtifactLease:
    """Bounded multi-reader/single-writer lease for one generator domain."""

    def __init__(self, repo_root: os.PathLike[str] | str, domain: str,
                 mode: str = "write", timeout_seconds: float = 0.0) -> None:
        if mode not in ("read", "write"):
            raise GeneratedArtifactError("lease mode must be read or write")
        if timeout_seconds < 0 or timeout_seconds > MAX_LEASE_WAIT_SECONDS:
            raise GeneratedArtifactError("lease timeout must be between 0 and 600 seconds")
        self.repo_root = _repo_root(repo_root)
        self.domain, self.mode = domain, mode
        self.domain_key = _domain_key(domain)
        self.control_root = _control_root(self.repo_root)
        self.generation_identity = uuid.uuid4().hex
        self._token = secrets.token_hex(32)
        lock_dir = self.control_root / "locks"
        lock_dir.mkdir(parents=True, exist_ok=True)
        self.lock_path = lock_dir / (self.domain_key + ".lock")
        self._handle: Optional[int] = None
        self._record_handle: Optional[int] = None
        self._record_path: Optional[Path] = None
        self.staging_root: Optional[Path] = None
        deadline = time.monotonic() + timeout_seconds
        last_error: Optional[OSError] = None
        while True:
            try:
                if os.name == "nt":
                    self._handle = _windows_open(self.lock_path, mode)
                else:
                    import fcntl

                    fd = os.open(self.lock_path, os.O_CREAT | os.O_RDWR, 0o600)
                    try:
                        fcntl.flock(fd, (fcntl.LOCK_SH if mode == "read" else fcntl.LOCK_EX) | fcntl.LOCK_NB)
                    except OSError:
                        os.close(fd)
                        raise
                    self._handle = fd
                break
            except OSError as error:
                last_error = error
                if time.monotonic() >= deadline:
                    owners = self._owner_diagnostics(lock_dir)
                    waited = int(timeout_seconds * 1000)
                    raise ArtifactLeaseBusy(
                        f"generated-artifact lease BUSY domain={domain!r} mode={mode} "
                        f"waitMs={waited} osError={getattr(error, 'winerror', None) or error.errno} "
                        f"owners={owners}") from error
                time.sleep(min(0.025, max(0.0, deadline - time.monotonic())))
        try:
            staging_base = self.control_root / "staging" / self.domain_key
            staging_base.mkdir(parents=True, exist_ok=True)
            self.staging_root = staging_base / self.generation_identity
            self.staging_root.mkdir(parents=True, exist_ok=False)
            self._write_staging_owner()
            self._record_path = lock_dir / f"{self.domain_key}.{self.generation_identity}.owner.json"
            record = {
                "schemaVersion": 1, "domain": domain, "mode": mode,
                "ownerPid": os.getpid(), "generationIdentity": self.generation_identity,
                "tokenSha256": _sha_bytes(self._token.encode("ascii")),
            }
            _write_durable(self._record_path, (json.dumps(record, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8"))
            self._record_handle = _windows_open(self._record_path, "read") if os.name == "nt" else None
            if self.mode == "write":
                self.cleanup_abandoned_owner_records()
                self.cleanup_abandoned_staging()
        except Exception:
            if self._record_handle is not None:
                try:
                    _close_native(self._record_handle)
                except Exception:
                    pass
                self._record_handle = None
            elif self._record_path is not None:
                self._record_path.unlink(missing_ok=True)
            if self.staging_root is not None and self.staging_root.exists():
                shutil.rmtree(self.staging_root, ignore_errors=True)
            if self._handle is not None:
                _close_native(self._handle)
                self._handle = None
            raise

    def _write_staging_owner(self) -> None:
        value = {
            "schemaVersion": 1,
            "domain": self.domain,
            "ownerPid": os.getpid(),
            "generationIdentity": self.generation_identity,
            "tokenSha256": _sha_bytes(self._token.encode("ascii")),
        }
        _write_durable(
            self.staging_root / "owner.json",
            (json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8"),
        )

    def _owner_diagnostics(self, lock_dir: Path) -> str:
        values = []
        for path in sorted(lock_dir.glob(self.domain_key + ".*.owner.json"))[:8]:
            try:
                payload = (_windows_read_shared_delete(path, 4096) if os.name == "nt"
                           else path.read_bytes())
                value = json.loads(payload.decode("utf-8"))
                values.append(f"{value.get('ownerPid')}:{value.get('mode')}:{value.get('generationIdentity')}")
            except Exception:
                values.append("unreadable")
        return ",".join(values) or "unavailable"

    def __enter__(self) -> "GeneratedArtifactLease":
        return self

    def __exit__(self, exc_type: Any, exc: Any, traceback: Any) -> None:
        self.close()

    def close(self) -> None:
        if self._handle is None:
            return
        cleanup_error: Optional[Exception] = None
        try:
            if self._record_handle is not None:
                _close_native(self._record_handle)
                self._record_handle = None
            elif self._record_path is not None:
                self._record_path.unlink(missing_ok=True)
            if self.staging_root is not None and self.staging_root.exists():
                shutil.rmtree(self.staging_root)
        except Exception as error:
            cleanup_error = error
        finally:
            _close_native(self._handle)
            self._handle = None
        if cleanup_error is not None:
            raise GeneratedArtifactError("lease staging cleanup failed") from cleanup_error

    def new_staging_directory(self, label: str) -> Path:
        if (self._handle is None or self.staging_root is None
                or not re.fullmatch(r"[A-Za-z0-9_.-]{1,40}", label)):
            raise GeneratedArtifactError("staging label or lease state is invalid")
        path = self.staging_root / (label + "-" + uuid.uuid4().hex)
        path.mkdir()
        return path

    def cleanup_abandoned_staging(self) -> Mapping[str, tuple[str, ...]]:
        """Remove only strictly valid dead-owner staging after exclusive acquisition."""
        if self.mode != "write" or self._handle is None:
            raise GeneratedArtifactError("stale staging cleanup requires a live write lease")
        if self.staging_root is None:
            raise GeneratedArtifactError("stale staging cleanup requires initialized staging")
        base = self.staging_root.parent
        removed = []
        retained = []
        expected_keys = {
            "schemaVersion", "domain", "ownerPid", "generationIdentity", "tokenSha256"
        }
        for path in sorted(base.iterdir(), key=lambda value: value.name):
            if path == self.staging_root:
                retained.append(path.name)
                continue
            owner_path = path / "owner.json"
            try:
                if (not path.is_dir() or path.is_symlink() or not _ID.fullmatch(path.name)
                        or not owner_path.is_file() or owner_path.stat().st_size > 4096):
                    raise ValueError("untrusted staging shape")
                owner = json.loads(owner_path.read_text(encoding="utf-8"))
                if (type(owner) is not dict or set(owner) != expected_keys
                        or owner["schemaVersion"] != 1 or owner["domain"] != self.domain
                        or owner["generationIdentity"] != path.name
                        or type(owner["ownerPid"]) is not int
                        or not _HEX64.fullmatch(str(owner["tokenSha256"]))):
                    raise ValueError("untrusted staging owner")
            except Exception:
                retained.append(path.name)
                continue
            if _pid_alive(owner["ownerPid"]):
                retained.append(path.name)
                continue
            shutil.rmtree(path)
            removed.append(path.name)
        return {"removed": tuple(removed), "retained": tuple(retained)}

    def cleanup_abandoned_owner_records(self) -> Mapping[str, tuple[str, ...]]:
        """Remove only valid dead-owner records while holding the exclusive lease."""
        if self.mode != "write" or self._handle is None or self._record_path is None:
            raise GeneratedArtifactError(
                "stale owner-record cleanup requires a live write lease"
            )
        lock_dir = self._record_path.parent
        removed = []
        retained = []
        expected = {
            "schemaVersion",
            "domain",
            "mode",
            "ownerPid",
            "generationIdentity",
            "tokenSha256",
        }
        for path in sorted(lock_dir.glob(self.domain_key + ".*.owner.json")):
            if path == self._record_path:
                retained.append(path.name)
                continue
            try:
                payload = (
                    _windows_read_shared_delete(path, 4096)
                    if os.name == "nt"
                    else path.read_bytes()
                )
                value = json.loads(payload.decode("utf-8"))
                generation = path.name.removeprefix(self.domain_key + ".").removesuffix(
                    ".owner.json"
                )
                if (
                    type(value) is not dict
                    or set(value) != expected
                    or value["schemaVersion"] != 1
                    or value["domain"] != self.domain
                    or value["mode"] not in ("read", "write")
                    or type(value["ownerPid"]) is not int
                    or value["generationIdentity"] != generation
                    or not _ID.fullmatch(generation)
                    or not _HEX64.fullmatch(str(value["tokenSha256"]))
                ):
                    raise ValueError("untrusted owner record")
            except Exception:
                retained.append(path.name)
                continue
            if _pid_alive(value["ownerPid"]):
                retained.append(path.name)
                continue
            try:
                path.unlink()
            except FileNotFoundError:
                pass
            removed.append(path.name)
        return {"removed": tuple(removed), "retained": tuple(retained)}

    def delegation(self) -> dict[str, Any]:
        return {
            "domain": self.domain, "mode": self.mode, "ownerPid": os.getpid(),
            "generationIdentity": self.generation_identity, "token": self._token,
        }

    @staticmethod
    def validate_delegation(repo_root: os.PathLike[str] | str, delegation: Mapping[str, Any],
                            required_mode: Optional[str] = None) -> Mapping[str, Any]:
        required = {"domain", "mode", "ownerPid", "generationIdentity", "token"}
        if type(delegation) is not dict or set(delegation) != required:
            raise DelegationError("delegated lease fields are invalid")
        domain, mode = delegation["domain"], delegation["mode"]
        pid, generation, token = delegation["ownerPid"], delegation["generationIdentity"], delegation["token"]
        if mode not in ("read", "write") or (required_mode is not None and mode != required_mode):
            raise DelegationError("delegated lease mode is invalid")
        if type(pid) is not int or type(generation) is not str or not _ID.fullmatch(generation):
            raise DelegationError("delegated owner identity is invalid")
        if type(token) is not str or len(token) != 64 or not _HEX64.fullmatch(token):
            raise DelegationError("delegated owner token is invalid")
        root = _repo_root(repo_root)
        key = _domain_key(domain)
        record_path = _control_root(root) / "locks" / f"{key}.{generation}.owner.json"
        try:
            record_bytes = (_windows_read_shared_delete(record_path, 4096) if os.name == "nt"
                            else record_path.read_bytes())
            if len(record_bytes) > 4096:
                raise ValueError("owner record exceeds maximum size")
            record = json.loads(record_bytes.decode("utf-8"))
        except Exception as error:
            raise DelegationError("delegated owner record is unavailable or malformed") from error
        expected = {"schemaVersion", "domain", "mode", "ownerPid", "generationIdentity", "tokenSha256"}
        if (type(record) is not dict or set(record) != expected or record["schemaVersion"] != 1
                or record["domain"] != domain or record["mode"] != mode
                or record["ownerPid"] != pid or record["generationIdentity"] != generation
                or record["tokenSha256"] != _sha_bytes(token.encode("ascii")) or not _pid_alive(pid)):
            raise DelegationError("delegated owner PID/token validation failed")
        return dict(record)


@dataclass(frozen=True)
class InputRecord:
    relative_path: str
    size: int
    sha256: str


@dataclass(frozen=True)
class InputSnapshot:
    repo_root: Path
    snapshot_root: Path
    records: tuple[InputRecord, ...]
    identity: str

    @classmethod
    def capture(cls, lease: GeneratedArtifactLease,
                paths: Iterable[os.PathLike[str] | str]) -> "InputSnapshot":
        supplied_paths = list(paths)
        resolved = []
        seen = set()
        for value in supplied_paths:
            path, relative = _contained(lease.repo_root, value)
            folded = relative.casefold()
            if folded in seen:
                raise GeneratedArtifactError("snapshot input paths collide")
            seen.add(folded)
            resolved.append((relative, path))
        resolved.sort(key=lambda item: item[0])
        root = lease.new_staging_directory("input-snapshot")
        records = []
        for relative, source in resolved:
            if not source.is_file() or source.is_symlink():
                raise GeneratedArtifactError("snapshot input is not a regular file: " + relative)
            target = root / "files" / Path(*relative.split("/"))
            target.parent.mkdir(parents=True, exist_ok=True)
            digest = hashlib.sha256()
            size = 0
            with source.open("rb") as reader, target.open("xb") as writer:
                while True:
                    block = reader.read(_CHUNK)
                    if not block:
                        break
                    digest.update(block)
                    size += len(block)
                    writer.write(block)
                writer.flush()
                os.fsync(writer.fileno())
            records.append(InputRecord(relative, size, digest.hexdigest()))
        payload = [{"relativePath": row.relative_path, "size": row.size, "sha256": row.sha256} for row in records]
        identity = _sha_bytes(json.dumps(payload, sort_keys=True, separators=(",", ":")).encode("utf-8"))
        snapshot = cls(lease.repo_root, root / "files", tuple(records), identity)
        snapshot.revalidate(supplied_paths)
        return snapshot

    def path_for(self, relative_path: str) -> Path:
        if relative_path not in {row.relative_path for row in self.records}:
            raise GeneratedArtifactError("path is not in the immutable input snapshot")
        return self.snapshot_root / Path(*relative_path.split("/"))

    def revalidate(self, paths: Optional[Iterable[os.PathLike[str] | str]] = None) -> None:
        expected = {row.relative_path: row for row in self.records}
        current_paths = list(expected) if paths is None else [
            _contained(self.repo_root, value)[1] for value in paths
        ]
        changes = ["added:" + value for value in sorted(set(current_paths) - set(expected))]
        changes.extend("removed:" + value for value in sorted(set(expected) - set(current_paths)))
        for relative in sorted(set(expected) & set(current_paths)):
            path = self.repo_root / Path(*relative.split("/"))
            if not path.is_file() or path.is_symlink():
                changes.append("missing-or-nonregular:" + relative)
            row = expected[relative]
            if path.is_file() and not path.is_symlink():
                actual_hash, actual_size = _sha_file(path)
                if actual_size != row.size or actual_hash != row.sha256:
                    changes.append(
                        f"changed:{relative} expectedSize={row.size} actualSize={actual_size} "
                        f"expectedSha256={row.sha256} actualSha256={actual_hash}")
            frozen = self.snapshot_root / Path(*relative.split("/"))
            if not frozen.is_file() or frozen.is_symlink():
                changes.append("snapshot-missing-or-nonregular:" + relative)
            else:
                frozen_hash, frozen_size = _sha_file(frozen)
                if frozen_size != row.size or frozen_hash != row.sha256:
                    changes.append(
                        f"snapshot-changed:{relative} expectedSize={row.size} actualSize={frozen_size} "
                        f"expectedSha256={row.sha256} actualSha256={frozen_hash}")
        if changes:
            raise InputChangedError(changes)


class ArtifactTransaction:
    """Recoverable multi-file publication under a write lease."""

    @staticmethod
    def _domain_root(lease: GeneratedArtifactLease) -> Path:
        return lease.control_root / "transactions" / lease.domain_key

    @classmethod
    def assert_readable(cls, lease: GeneratedArtifactLease) -> None:
        root = cls._domain_root(lease)
        try:
            os.lstat(root)
        except FileNotFoundError:
            return
        cls._require_plain_recovery_directory(root, "transaction domain root")
        pending = sorted(path.name for path in root.iterdir())
        if pending:
            raise PendingRecoveryError("generated-artifact recovery is pending: " + ", ".join(pending))

    @classmethod
    def publish(
        cls,
        lease: GeneratedArtifactLease,
        outputs: Mapping[os.PathLike[str] | str, bytes],
        *,
        validators: Optional[Mapping[os.PathLike[str] | str, Callable[[bytes], Any]]] = None,
        artifact_order: Optional[Sequence[os.PathLike[str] | str]] = None,
        commit_marker: Optional[os.PathLike[str] | str] = None,
        validation_callback: Optional[Callable[[str], Any]] = None,
        fault_hook: Optional[Callable[[str, Mapping[str, Any]], Any]] = None,
    ) -> str:
        if lease.mode != "write" or lease._handle is None:
            raise ArtifactTransactionError("publication requires a live write lease")
        if validation_callback is not None and not callable(validation_callback):
            raise ArtifactTransactionError("publication validation callback is invalid")
        cls.assert_readable(lease)
        normalized: dict[str, tuple[Path, bytes]] = {}
        for value, payload in outputs.items():
            path, relative = _contained(lease.repo_root, value)
            if relative.casefold() in {key.casefold() for key in normalized}:
                raise ArtifactTransactionError("output paths collide")
            if not isinstance(payload, bytes):
                raise ArtifactTransactionError("generated output must be bytes: " + relative)
            normalized[relative] = (path, payload)
        if not normalized:
            raise ArtifactTransactionError("generated output cohort is empty")
        if artifact_order is None:
            order = sorted(normalized)
        else:
            order = [_contained(lease.repo_root, value)[1] for value in artifact_order]
            if len(order) != len(set(value.casefold() for value in order)) or set(order) != set(normalized):
                raise ArtifactTransactionError("artifact order must be an exact unique output permutation")
        marker = _contained(lease.repo_root, commit_marker)[1] if commit_marker is not None else None
        if marker is not None:
            if marker not in normalized:
                raise ArtifactTransactionError("commit marker must be one of the generated outputs")
            order = [value for value in order if value != marker] + [marker]
        validator_map = {}
        for key, validator in (validators or {}).items():
            relative = _contained(lease.repo_root, key)[1]
            if relative not in normalized or not callable(validator):
                raise ArtifactTransactionError("output validator target is invalid")
            validator_map[relative] = validator
        for relative in order:
            if relative in validator_map:
                try:
                    validator_map[relative](normalized[relative][1])
                except Exception as error:
                    raise ArtifactTransactionError(
                        f"generated output validator rejected {relative}: {type(error).__name__}") from error

        records = []
        for index, relative in enumerate(order):
            destination, payload = normalized[relative]
            if destination.exists() and (not destination.is_file() or destination.is_symlink()):
                raise ArtifactTransactionError("output destination is not a regular file: " + relative)
            before_hash, before_size = _sha_file(destination) if destination.exists() else (None, 0)
            records.append({
                "relativePath": relative, "newName": f"new/{index:04d}.bin",
                "backupName": f"backup/{index:04d}.bin", "beforeExists": destination.exists(),
                "beforeSize": before_size, "beforeSha256": before_hash,
                "afterSize": len(payload), "afterSha256": _sha_bytes(payload),
            })
        transaction_id = uuid.uuid4().hex
        domain_root = cls._domain_root(lease)
        domain_root.mkdir(parents=True, exist_ok=True)
        tx = domain_root / ("tx-" + transaction_id)
        journal = {
            "schemaVersion": 1, "domain": lease.domain, "transactionIdentity": transaction_id,
            "ownerGenerationIdentity": lease.generation_identity, "state": "building",
            "publishedCount": 0, "commitMarker": marker, "artifacts": records,
        }
        initializing: Optional[Path] = None
        try:
            initializing = lease.new_staging_directory("transaction-init")
            (initializing / "new").mkdir()
            (initializing / "backup").mkdir()
            _replace_json(initializing / "journal.json", journal)
            _replace_with_retry(initializing, tx, "initialize-transaction")
        except Exception as error:
            if initializing is not None and initializing.exists():
                shutil.rmtree(initializing, ignore_errors=True)
            cls._remove_empty(domain_root)
            raise ArtifactTransactionError(
                "transaction initialization failed: " + _error_detail(error)
            ) from error
        journal_path = tx / "journal.json"
        try:
            cls._fault(fault_hook, "after_initialized", {"transactionIdentity": transaction_id})
            for prepare_index, record in enumerate(records):
                relative = record["relativePath"]
                destination, payload = normalized[relative]
                destination.parent.mkdir(parents=True, exist_ok=True)
                _write_durable(tx / record["newName"], payload)
                if record["beforeExists"]:
                    with destination.open("rb") as reader:
                        backup = tx / record["backupName"]
                        with backup.open("xb") as writer:
                            shutil.copyfileobj(reader, writer, _CHUNK)
                            writer.flush()
                            os.fsync(writer.fileno())
                    if _sha_file(backup) != (record["beforeSha256"], record["beforeSize"]):
                        raise ArtifactTransactionError("backup verification failed: " + relative)
                cls._fault(fault_hook, "after_prepare_artifact", {
                    "index": prepare_index,
                    "relativePath": relative,
                })
            journal["state"] = "prepared"
            _replace_json(journal_path, journal)
            cls._fault(fault_hook, "after_prepared", {"transactionIdentity": transaction_id})
            for index, record in enumerate(records):
                destination = lease.repo_root / Path(*record["relativePath"].split("/"))
                cls._require_state(destination, record, "before")
                cls._fault(fault_hook, "before_replace", {"index": index, "relativePath": record["relativePath"]})
                if index == 0 and validation_callback is not None:
                    try:
                        validation_callback("before_publish")
                    except Exception as validation_error:
                        raise ArtifactTransactionError(
                            "publication validation failed phase=before_publish: "
                            + _error_detail(validation_error)
                        ) from validation_error
                _replace_with_retry(
                    tx / record["newName"],
                    destination,
                    "publish-" + record["relativePath"],
                )
                journal["state"] = "publishing"
                journal["publishedCount"] = index + 1
                _replace_json(journal_path, journal)
                cls._fault(fault_hook, "after_replace", {"index": index, "relativePath": record["relativePath"]})
            for record in records:
                cls._require_state(lease.repo_root / Path(*record["relativePath"].split("/")), record, "after")
            if validation_callback is not None:
                try:
                    validation_callback("before_commit")
                except Exception as validation_error:
                    raise ArtifactTransactionError(
                        "publication validation failed phase=before_commit: "
                        + _error_detail(validation_error)
                    ) from validation_error
            cls._fault(fault_hook, "before_commit", {"transactionIdentity": transaction_id})
            journal["state"] = "committed"
            _replace_json(journal_path, journal)
            cls._fault(fault_hook, "after_committed", {"transactionIdentity": transaction_id})
            cls._retire_transaction(lease, tx)
            cls._remove_empty(domain_root)
            return transaction_id
        except Exception as error:
            try:
                loaded = cls._load_journal(tx, lease)
                if loaded["state"] == "committed":
                    cls._retire_transaction(lease, tx)
                else:
                    cls._rollback(lease, tx, loaded)
                cls._remove_empty(domain_root)
            except Exception as recovery_error:
                raise ArtifactTransactionError(
                    "publication failed "
                    f"({type(error).__name__}: {_error_detail(error)}); "
                    f"rollback failed ({_error_detail(recovery_error)})") from error
            raise ArtifactTransactionError(
                "publication failed and rolled back: "
                f"{type(error).__name__}: {_error_detail(error)}") from error

    @classmethod
    def recover(cls, lease: GeneratedArtifactLease) -> str:
        if lease.mode != "write" or lease._handle is None:
            raise ArtifactTransactionError("recovery requires a live write lease")
        root = cls._domain_root(lease)
        try:
            os.lstat(root)
        except FileNotFoundError:
            return "clean"
        cls._require_plain_recovery_directory(root, "transaction domain root")
        entries = sorted(root.iterdir(), key=lambda path: path.name)
        if not entries:
            root.rmdir()
            return "clean"
        if len(entries) != 1 or not entries[0].name.startswith("tx-"):
            raise PendingRecoveryError("generated-artifact transaction state is ambiguous")
        tx = entries[0]
        journal = cls._load_journal(tx, lease)
        if journal["state"] == "committed":
            for record in journal["artifacts"]:
                cls._require_state(lease.repo_root / Path(*record["relativePath"].split("/")), record, "after")
            cls._retire_transaction(lease, tx)
            result = "committed-cleanup"
        else:
            cls._rollback(lease, tx, journal)
            result = "rolled-back"
        cls._remove_empty(root)
        return result

    @staticmethod
    def _fault(hook: Optional[Callable[[str, Mapping[str, Any]], Any]], event: str,
               context: Mapping[str, Any]) -> None:
        if hook is not None:
            hook(event, context)

    @staticmethod
    def _remove_empty(path: Path) -> None:
        try:
            path.rmdir()
        except OSError:
            pass

    @staticmethod
    def _retire_transaction(lease: GeneratedArtifactLease, tx: Path) -> None:
        """Atomically remove a resolved journal from the reader-visible pending set."""
        if lease.staging_root is None:
            raise ArtifactTransactionError("lease staging is unavailable")
        retired = lease.staging_root / ("retired-" + tx.name + "-" + uuid.uuid4().hex)
        _replace_with_retry(tx, retired, "retire-transaction")
        try:
            shutil.rmtree(retired)
        except OSError:
            # Lease close or dead-owner staging cleanup owns retirement debris.
            pass

    @staticmethod
    def _actual(path: Path) -> tuple[bool, Optional[str], int]:
        if not path.exists():
            return False, None, 0
        if not path.is_file() or path.is_symlink():
            raise PendingRecoveryError("artifact target is not a regular file: " + str(path))
        digest, size = _sha_file(path)
        return True, digest, size

    @staticmethod
    def _require_plain_recovery_directory(path: Path, label: str) -> None:
        try:
            status = os.lstat(path)
        except FileNotFoundError as error:
            raise PendingRecoveryError(label + " is missing") from error
        if _is_reparse_status(status):
            raise PendingRecoveryError(label + " is a symlink or reparse point")
        if not stat.S_ISDIR(status.st_mode):
            raise PendingRecoveryError(label + " is not a plain directory")

    @staticmethod
    def _require_plain_recovery_file(path: Path, label: str) -> None:
        try:
            status = os.lstat(path)
        except FileNotFoundError as error:
            raise PendingRecoveryError(label + " is missing") from error
        if _is_reparse_status(status):
            raise PendingRecoveryError(label + " is a symlink or reparse point")
        if not stat.S_ISREG(status.st_mode):
            raise PendingRecoveryError(label + " is not a plain file")

    @classmethod
    def _validate_recovery_tree(cls, tx: Path, lease: GeneratedArtifactLease) -> None:
        domain_root = cls._domain_root(lease)
        cls._require_plain_recovery_directory(domain_root, "transaction domain root")
        try:
            if tx.parent != domain_root or tx.name == "":
                raise PendingRecoveryError("transaction directory is not canonical")
        except OSError as error:
            raise PendingRecoveryError("transaction directory is not canonical") from error
        cls._require_plain_recovery_directory(tx, "transaction directory")
        cls._require_plain_recovery_file(tx / "journal.json", "transaction journal")
        for directory_name in ("new", "backup"):
            directory = tx / directory_name
            cls._require_plain_recovery_directory(
                directory, "transaction " + directory_name + " directory"
            )
            for member in directory.iterdir():
                cls._require_plain_recovery_file(
                    member, "transaction " + directory_name + " member"
                )

    @classmethod
    def _require_state(cls, path: Path, record: Mapping[str, Any], state: str) -> None:
        exists, digest, size = cls._actual(path)
        if state == "before":
            expected = (record["beforeExists"], record["beforeSha256"], record["beforeSize"])
        else:
            expected = (True, record["afterSha256"], record["afterSize"])
        if (exists, digest, size) != expected:
            raise PendingRecoveryError(
                f"artifact has unknown {state} state: {record['relativePath']}")

    @classmethod
    def _rollback(cls, lease: GeneratedArtifactLease, tx: Path, journal: Mapping[str, Any]) -> None:
        actions = []
        for record in journal["artifacts"]:
            path = lease.repo_root / Path(*record["relativePath"].split("/"))
            actual = cls._actual(path)
            before = (record["beforeExists"], record["beforeSha256"], record["beforeSize"])
            after = (True, record["afterSha256"], record["afterSize"])
            if actual == before:
                actions.append(("keep", path, record))
            elif actual == after:
                backup = tx / record["backupName"]
                if record["beforeExists"] and (not backup.is_file() or _sha_file(backup) != (record["beforeSha256"], record["beforeSize"])):
                    raise PendingRecoveryError("verified backup is unavailable: " + record["relativePath"])
                actions.append(("restore" if record["beforeExists"] else "remove", path, record))
            else:
                raise PendingRecoveryError("artifact is tampered or mixed: " + record["relativePath"])
        for action, path, record in reversed(actions):
            if action == "restore":
                _replace_with_retry(
                    tx / record["backupName"],
                    path,
                    "rollback-" + record["relativePath"],
                )
            elif action == "remove":
                path.unlink()
        for record in journal["artifacts"]:
            cls._require_state(lease.repo_root / Path(*record["relativePath"].split("/")), record, "before")
        cls._retire_transaction(lease, tx)

    @classmethod
    def _load_journal(cls, tx: Path, lease: Optional[GeneratedArtifactLease] = None) -> Mapping[str, Any]:
        if lease is not None:
            cls._validate_recovery_tree(tx, lease)
        path = tx / "journal.json"
        if not path.is_file() or path.stat().st_size > _CHUNK:
            raise PendingRecoveryError("transaction journal is missing or oversized")
        try:
            value = json.loads(path.read_text(encoding="utf-8"))
        except Exception as error:
            raise PendingRecoveryError("transaction journal is malformed") from error
        keys = {"schemaVersion", "domain", "transactionIdentity", "ownerGenerationIdentity",
                "state", "publishedCount", "commitMarker", "artifacts"}
        if (type(value) is not dict or set(value) != keys
                or type(value["schemaVersion"]) is not int or value["schemaVersion"] != 1
                or type(value["domain"]) is not str
                or type(value["transactionIdentity"]) is not str
                or not _ID.fullmatch(value["transactionIdentity"])
                or tx.name != "tx-" + value["transactionIdentity"]
                or type(value["ownerGenerationIdentity"]) is not str
                or not _ID.fullmatch(value["ownerGenerationIdentity"])
                or value["state"] not in ("building", "prepared", "publishing", "committed")
                or type(value["artifacts"]) is not list or not value["artifacts"]):
            raise PendingRecoveryError("transaction journal identity or schema is invalid")
        if lease is not None and value["domain"] != lease.domain:
            raise PendingRecoveryError("transaction journal lease domain is invalid")
        record_keys = {"relativePath", "newName", "backupName", "beforeExists", "beforeSize",
                       "beforeSha256", "afterSize", "afterSha256"}
        paths = []
        for index, record in enumerate(value["artifacts"]):
            if (type(record) is not dict or set(record) != record_keys
                    or record["newName"] != f"new/{index:04d}.bin"
                    or record["backupName"] != f"backup/{index:04d}.bin"
                    or type(record["relativePath"]) is not str
                    or type(record["beforeExists"]) is not bool
                    or type(record["beforeSize"]) is not int or record["beforeSize"] < 0
                    or type(record["afterSize"]) is not int or record["afterSize"] < 0
                    or type(record["afterSha256"]) is not str
                    or not _HEX64.fullmatch(record["afterSha256"])
                    or (record["beforeExists"] and (
                        type(record["beforeSha256"]) is not str
                        or not _HEX64.fullmatch(record["beforeSha256"])))
                    or (not record["beforeExists"] and record["beforeSha256"] is not None)):
                raise PendingRecoveryError("transaction artifact record is invalid")
            relative = record["relativePath"]
            if (not relative or "\\" in relative or ":" in relative
                    or relative.startswith("/") or any(part in ("", ".", "..") for part in relative.split("/"))
                    or (not record["beforeExists"] and record["beforeSize"] != 0)):
                raise PendingRecoveryError("transaction artifact path or absent-state record is invalid")
            if lease is not None:
                try:
                    canonical = _contained(lease.repo_root, relative)[1]
                except GeneratedArtifactError as error:
                    raise PendingRecoveryError(
                        "transaction artifact path is not canonical") from error
                if canonical != relative:
                    raise PendingRecoveryError("transaction artifact path is not canonical")
            paths.append(record["relativePath"])
        if len(paths) != len(set(path.casefold() for path in paths)):
            raise PendingRecoveryError("transaction artifact paths collide")
        if value["commitMarker"] is not None and value["commitMarker"] != paths[-1]:
            raise PendingRecoveryError("transaction commit marker was not published last")
        if type(value["publishedCount"]) is not int or not 0 <= value["publishedCount"] <= len(paths):
            raise PendingRecoveryError("transaction publication count is invalid")
        count, state = value["publishedCount"], value["state"]
        if ((state in ("building", "prepared") and count != 0)
                or (state == "publishing" and not 1 <= count <= len(paths))
                or (state == "committed" and count != len(paths))):
            raise PendingRecoveryError("transaction state and publication count are inconsistent")
        return value


__all__ = [
    "ArtifactLeaseBusy", "ArtifactTransaction", "ArtifactTransactionError",
    "DelegationError", "GeneratedArtifactError", "GeneratedArtifactLease",
    "InputChangedError", "InputRecord", "InputSnapshot", "MAX_LEASE_WAIT_SECONDS",
    "PendingRecoveryError", "SimulatedCrash",
]
