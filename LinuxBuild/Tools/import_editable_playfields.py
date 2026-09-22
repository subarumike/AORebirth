"""Import an operator-owned editable Playfields archive into a clean acceptance tree."""
import hashlib
import json
from pathlib import Path, PurePosixPath
import stat
import sys
import zipfile

def import_archive(archive, root):
    if archive.is_symlink() or not archive.is_file():
        raise ValueError("Archive must be a regular file")
    if root.exists() and (root.is_symlink() or not root.is_dir() or any(root.iterdir())):
        raise ValueError("Editable import destination must be absent or empty")
    if any(parent.is_symlink() for parent in root.parents):
        raise ValueError("Editable import destination cannot traverse symlinks")
    with zipfile.ZipFile(archive) as source:
        records = []
        names = set()
        for member in source.infolist():
            raw = member.filename
            path = PurePosixPath(raw)
            if "\\" in raw or path.is_absolute() or any(part in ("", ".", "..") or ":" in part for part in raw.rstrip("/").split("/")):
                raise ValueError("Unsafe archive path")
            if any(ord(c) < 32 for c in raw):
                raise ValueError("Unsafe archive path")
            mode = member.external_attr >> 16
            if stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR):
                raise ValueError("Archive special file rejected")
            if member.flag_bits & 1:
                raise ValueError("Encrypted archive rejected")
            name = path.as_posix().casefold()
            if name in names:
                raise ValueError("Duplicate archive destination")
            names.add(name)
            records.append((member, path))
        file_paths = {path.as_posix().casefold() for member, path in records if not member.is_dir()}
        for _, path in records:
            if any(parent.as_posix().casefold() in file_paths for parent in path.parents):
                raise ValueError("Archive file/directory collision")
        if source.testzip() is not None:
            raise ValueError("Archive integrity check failed")
        root.mkdir(parents=True, exist_ok=True)
        count = total = 0
        for member, relative in records:
            target = root.joinpath(*relative.parts)
            if member.is_dir():
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            data = source.read(member)
            with target.open("xb") as stream:
                stream.write(data)
            count += 1
            total += len(data)
    return {"archiveSha256": hashlib.sha256(archive.read_bytes()).hexdigest(), "fileCount": count, "totalBytes": total}

if __name__ == "__main__":
    try:
        receipt = import_archive(Path(sys.argv[1]), Path(sys.argv[2]))
        print(json.dumps(receipt, sort_keys=True))
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        print("EDITABLE_PLAYFIELD_IMPORT=FAIL " + str(error), file=sys.stderr)
        sys.exit(1)
