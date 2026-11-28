"""Stage retained original mission dungeon resources for review in private GameData."""

import argparse
from hashlib import sha256
import json
import os
from pathlib import Path
import re
import stat
import tempfile


FOLDER = Path("Missions/DungeonContent")
HEADER = b"AORebirth dungeon content v1\n"
NPC_HEADER = b"AORebirth dungeon NPC content v1\n"
NPC_FILES = ("NpcTemplates.json", "MonsterCombatRules.json")
NPC_LINK = "NpcCatalog.json"
NPC_FOLDER = "NpcCatalog"
COMMON = ("DungeonGeneration.json", "DungeonDoorStyles.json", "DungeonEntrances.json", "DynelCollision.json")
STYLE_FILES = ("metadata.json", "Rooms.json", "Districts.json", "Surfaces.dat", "DCGA.png", "GNDA.png")
DIGEST = re.compile(r"[0-9a-f]{64}")


def check_path(path):
    attributes = path.lstat()
    if path.is_symlink() or getattr(attributes, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
        raise ValueError(f"Link or junction rejected in dungeon content: {path}")
    return attributes


def checked(root, relative):
    path = root
    check_path(path)
    for part in Path(relative).parts:
        path /= part
        check_path(path)
    return path


def read(root, relative):
    path = checked(root, relative)
    before = check_path(path)
    if not stat.S_ISREG(before.st_mode) or before.st_size <= 0:
        raise ValueError(f"Expected nonempty dungeon resource: {relative}")
    data = path.read_bytes()
    after = check_path(path)
    if (before.st_size, before.st_mtime_ns, before.st_ino) != (after.st_size, after.st_mtime_ns, after.st_ino):
        raise ValueError(f"Dungeon resource changed during staging: {relative}")
    return data


def relative_style_files(style):
    if not isinstance(style, int) or isinstance(style, bool) or not 0 < style < 2**31:
        raise ValueError(f"Invalid dungeon style identifier: {style}")
    return tuple(f"Playfields/{style}/{name}" for name in STYLE_FILES)


def gather(source, style):
    paths = (*COMMON, NPC_LINK, *relative_style_files(style))
    return {path: read(source, path) for path in paths}


def identity(files, header=HEADER):
    return sha256(header + b"".join(
        f"{path} {sha256(files[path]).hexdigest()}\n".encode()
        for path in sorted(files)
    )).hexdigest()


def retain(destination, revision, files):
    package = destination / revision
    if package.exists() or package.is_symlink():
        checked(destination, revision)
        if any(read(package, path) != data for path, data in files.items()):
            raise ValueError(f"Existing retained dungeon revision failed byte verification: {revision}")
        return

    with tempfile.TemporaryDirectory(prefix=".stage-", dir=destination) as temp:
        staged = Path(temp) / "bundle"
        for path, data in files.items():
            output = staged / path
            output.parent.mkdir(parents=True, exist_ok=True)
            output.write_bytes(data)
        staged.rename(package)


def retain_existing(retained, destination):
    directory = checked(retained, FOLDER)
    npc_revisions = set()
    for package in sorted(directory.iterdir()):
        if not DIGEST.fullmatch(package.name):
            continue
        playfields = checked(directory, Path(package.name) / "Playfields")
        styles = tuple(entry for entry in playfields.iterdir() if entry.is_dir())
        if len(styles) != 1 or not re.fullmatch(r"[1-9][0-9]*", styles[0].name):
            raise ValueError(f"Retained dungeon revision {package.name} does not describe one original style.")
        files = gather(package, int(styles[0].name))
        if identity(files) != package.name:
            raise ValueError(f"Prior retained dungeon digest mismatch: {package.name}")
        retain(destination, package.name, files)
        reference = json.loads(files[NPC_LINK])
        revision = reference["Sha256"]
        if reference["SchemaVersion"] != 1 or not DIGEST.fullmatch(revision):
            raise ValueError(f"Invalid retained NPC catalog reference for {package.name}.")
        if revision not in npc_revisions:
            npc_directory = checked(directory, Path(NPC_FOLDER) / revision)
            npc_files = {path: read(npc_directory, path) for path in NPC_FILES}
            if identity(npc_files, NPC_HEADER) != revision:
                raise ValueError(f"Prior retained NPC catalog digest mismatch: {revision}")
            retain(destination / NPC_FOLDER, revision, npc_files)
            npc_revisions.add(revision)


def stage(source, output, retained_from=None):
    source = source.resolve(strict=True)
    output = output.resolve()
    if output == source or output.is_relative_to(source) or source.is_relative_to(output):
        raise ValueError("Stage into a distinct candidate GameData tree outside the active input tree.")
    if retained_from is not None:
        retained_from = retained_from.resolve(strict=True)
        if output == retained_from or output.is_relative_to(retained_from) or retained_from.is_relative_to(output):
            raise ValueError("Stage into a distinct candidate tree outside the previous retained source.")

    common = {path: read(source, path) for path in COMMON}
    policy = json.loads(common["DungeonGeneration.json"])
    styles = {style for profile in policy["Profiles"] for style in profile["Styles"]}
    if not styles:
        raise ValueError("Generation policy defines no dungeon styles.")
    destination = output / FOLDER
    cursor = output
    cursor.mkdir(parents=True, exist_ok=True)
    for name in FOLDER.parts:
        cursor /= name
        cursor.mkdir(exist_ok=True)
        check_path(cursor)
    npc_destination = destination / NPC_FOLDER
    npc_destination.mkdir(exist_ok=True)
    check_path(npc_destination)
    if retained_from is not None:
        retain_existing(retained_from, destination)

    npc_files = {path: read(source, path) for path in NPC_FILES}
    npc_digest = identity(npc_files, NPC_HEADER)
    retain(npc_destination, npc_digest, npc_files)
    link = json.dumps({"SchemaVersion": 1, "Sha256": npc_digest}, separators=(",", ":")).encode()

    revisions = {}
    for style in sorted(styles):
        files = dict(common)
        files[NPC_LINK] = link
        files.update((path, read(source, path)) for path in relative_style_files(style))
        revisions[style] = identity(files)
        retain(destination, revisions[style], files)

    current = destination / "Current.json"
    if current.exists() or current.is_symlink():
        check_path(current)
    with tempfile.TemporaryDirectory(prefix=".stage-", dir=destination) as temp:
        index = Path(temp) / "Current.json"
        index.write_text(json.dumps({"SchemaVersion": 1, "Styles": revisions}, indent=2) + "\n", encoding="utf-8")
        os.replace(index, current)
    return revisions


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-gamedata", required=True, type=Path)
    parser.add_argument("--output-gamedata", required=True, type=Path)
    parser.add_argument("--retain-from-gamedata", type=Path,
                        help="Carry forward previously approved content-addressed revisions from this private tree.")
    args = parser.parse_args()
    try:
        revisions = stage(args.source_gamedata, args.output_gamedata, args.retain_from_gamedata)
    except (KeyError, OSError, TypeError, ValueError) as error:
        parser.exit(1, f"Dungeon content staging failed: {error}\n")
    for style, revision in revisions.items():
        print(f"{style} {revision}")


if __name__ == "__main__":
    main()
