"""Package integrity for editable NewEngine data; no gameplay or provenance permission policy."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import sys

MANIFEST = "CONTENT_MANIFEST.json"
PLAYFIELD_CONTENT_ROOT = "GameData/PlayfieldContent/"

def digest(data):
    return hashlib.sha256(data).hexdigest()

def pairs(items):
    result = {}
    for key, value in items:
        if key in result:
            raise ValueError("Duplicate JSON property")
        result[key] = value
    return result

def read_json(data):
    return json.loads(data.decode("utf-8-sig"), object_pairs_hook=pairs, parse_constant=lambda value: (_ for _ in ()).throw(ValueError("Non-finite JSON value")))

def inventory(root):
    if root.is_symlink() or not root.is_dir():
        raise ValueError("Content package root must be a real directory")
    rows = []
    for name in ("GameData", "Content"):
        base = root / name
        if not base.exists() and not base.is_symlink():
            if name == "Content":
                continue
            raise ValueError("GameData directory missing")
        if base.is_symlink() or not base.is_dir():
            raise ValueError("Content directory must not be a symlink")
        for directory, subdirs, files in os.walk(base, followlinks=False):
            for child in subdirs:
                if (Path(directory) / child).is_symlink():
                    raise ValueError("Content directory symlink rejected")
            for name in files:
                path = Path(directory) / name
                if not stat.S_ISREG(path.lstat().st_mode):
                    raise ValueError("Content special file rejected")
                data = path.read_bytes()
                relative = path.relative_to(root).as_posix()
                if any(ord(c) < 32 for c in relative):
                    raise ValueError("Content path has control characters")
                if path.suffix.lower() == ".json":
                    try:
                        read_json(data)
                    except (ValueError, UnicodeError) as error:
                        raise ValueError("Content JSON is invalid: " + relative) from error
                rows.append({"path": relative, "size": len(data), "sha256": digest(data)})
    rows.sort(key=lambda row: row["path"])
    paths = {row["path"] for row in rows}
    if not any(path.startswith(PLAYFIELD_CONTENT_ROOT) and path.endswith("/Npcs.json") for path in paths):
        raise ValueError("Required editable NewEngine playfield NPC content missing")
    return rows

def execute(mode, root, sha, platform, expected=None):
    if not re.fullmatch(r"[0-9a-f]{40}", sha) or platform not in ("linux", "windows", "windows-hosted-linux-publish"):
        raise ValueError("Invalid content source identity")
    actual = {"schemaVersion": 1, "sourceSha": sha, "buildPlatform": platform, "files": inventory(root)}
    path = root / MANIFEST
    if path.is_symlink():
        raise ValueError("Content manifest symlink rejected")
    if mode == "write":
        path.write_text(json.dumps(actual, indent=2) + "\n", encoding="utf-8", newline="\n")
    if not path.is_file():
        raise ValueError("Content manifest missing")
    data = path.read_bytes()
    recorded = read_json(data)
    if recorded != actual:
        raise ValueError("Editable content inventory, hash, or source identity mismatch")
    sha256 = digest(data)
    if expected and (not re.fullmatch(r"[0-9a-f]{64}", expected) or expected != sha256):
        raise ValueError("Content manifest digest mismatch")
    return {
        "CONTENT_PROVENANCE_VERSION": "1",
        "CONTENT_MANIFEST_SHA256": sha256,
        "CONTENT_FILE_COUNT": str(len(actual["files"])),
        "CONTENT_TOTAL_BYTES": str(sum(row["size"] for row in actual["files"]))
    }

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("write", "check"))
    parser.add_argument("root", type=Path)
    parser.add_argument("sha")
    parser.add_argument("platform")
    parser.add_argument("expected", nargs="?")
    args = parser.parse_args()
    try:
        values = execute(args.mode, args.root, args.sha, args.platform, args.expected)
        for key, value in values.items():
            print(key + "=" + value)
    except (OSError, ValueError) as error:
        print("CONTENT_PROVENANCE=FAIL " + str(error), file=sys.stderr)
        return 1
    return 0

if __name__ == "__main__":
    sys.exit(main())
