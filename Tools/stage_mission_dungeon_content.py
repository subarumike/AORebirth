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


def rooted(path, *, must_exist=True):
    if ".." in path.parts:
        raise ValueError(f"Traversal rejected in dungeon content root: {path}")
    path = path.absolute()
    for component in (*reversed(path.parents), path):
        try:
            check_path(component)
        except FileNotFoundError:
            if must_exist:
                raise
            break
    return path.resolve(strict=must_exist)


def checked(root, relative):
    relative = Path(relative)
    if relative.anchor or ".." in relative.parts:
        raise ValueError(f"Traversal rejected in dungeon content member: {relative}")
    path = root
    check_path(path)
    for part in relative.parts:
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


def exact_members(directory, expected):
    if {entry.name for entry in directory.iterdir()} != set(expected):
        raise ValueError(f"Unexpected or missing retained dungeon members: {directory}")


def gather(source, style):
    exact_members(source, (*COMMON, NPC_LINK, "Playfields"))
    playfields = checked(source, "Playfields")
    exact_members(playfields, (str(style),))
    exact_members(checked(playfields, str(style)), STYLE_FILES)
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
        expected = set(files)
        expected.update(parent.as_posix() for path in files for parent in Path(path).parents if parent != Path("."))
        entries = set()
        for member in package.rglob("*"):
            check_path(member)
            entries.add(member.relative_to(package).as_posix())
        if entries != expected or any(read(package, path) != data for path, data in files.items()):
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
    current_path = directory / "Current.json"
    current_styles = {}
    if current_path.exists() or current_path.is_symlink():
        current = json.loads(read(directory, "Current.json"), object_pairs_hook=json_object)
        if (set(current) != {"SchemaVersion", "Styles"} or current["SchemaVersion"] != 1
                or not isinstance(current["Styles"], dict) or not current["Styles"]):
            raise ValueError("Invalid prior retained dungeon current index")
        current_styles = current["Styles"]
    archived_styles, npc_references = {}, set()
    for package in sorted(directory.iterdir()):
        if package.name in ("Current.json", NPC_FOLDER):
            continue
        if not DIGEST.fullmatch(package.name):
            raise ValueError(f"Unexpected retained dungeon archive owner: {package.name}")
        playfields = checked(directory, Path(package.name) / "Playfields")
        styles = tuple(playfields.iterdir())
        if len(styles) != 1 or not re.fullmatch(r"[1-9][0-9]*", styles[0].name):
            raise ValueError(f"Retained dungeon revision {package.name} does not describe one original style.")
        style = styles[0].name
        files = gather(package, int(style))
        if identity(files) != package.name:
            raise ValueError(f"Prior retained dungeon digest mismatch: {package.name}")
        reference = json.loads(files[NPC_LINK], object_pairs_hook=json_object)
        revision = reference.get("Sha256")
        if (set(reference) != {"SchemaVersion", "Sha256"} or reference["SchemaVersion"] != 1
                or not isinstance(revision, str) or not DIGEST.fullmatch(revision)):
            raise ValueError(f"Invalid retained NPC catalog reference for {package.name}.")
        retain(destination, package.name, files)
        archived_styles[package.name] = style
        npc_references.add(revision)

    for style, revision in current_styles.items():
        if not isinstance(revision, str) or archived_styles.get(revision) != style:
            raise ValueError(f"Missing or conflicting prior retained dungeon archive for style {style}.")

    npc_root = checked(directory, NPC_FOLDER)
    npc_archives = set()
    for package in sorted(npc_root.iterdir()):
        if not DIGEST.fullmatch(package.name):
            raise ValueError(f"Unexpected retained NPC catalog owner: {package.name}")
        checked(npc_root, package.name)
        exact_members(package, NPC_FILES)
        npc_files = {path: read(package, path) for path in NPC_FILES}
        if identity(npc_files, NPC_HEADER) != package.name:
            raise ValueError(f"Prior retained NPC catalog digest mismatch: {package.name}")
        retain(destination / NPC_FOLDER, package.name, npc_files)
        npc_archives.add(package.name)
    if not npc_references <= npc_archives:
        raise ValueError("Missing prior retained NPC catalog reference.")


def json_object(entries):
    result = {}
    for name, value in entries:
        if name in result:
            raise ValueError(f"Duplicate dungeon archive key: {name}")
        result[name] = value
    return result

def stage(source, output, retained_from=None):
    source = rooted(source)
    output = rooted(output, must_exist=False)
    if output == source or output.is_relative_to(source) or source.is_relative_to(output):
        raise ValueError("Stage into a distinct candidate GameData tree outside the active input tree.")
    if retained_from is not None:
        retained_from = rooted(retained_from)
        if output == retained_from or output.is_relative_to(retained_from) or retained_from.is_relative_to(output):
            raise ValueError("Stage into a distinct candidate tree outside the previous retained source.")

    common = {path: read(source, path) for path in COMMON}
    policy = json.loads(common["DungeonGeneration.json"], object_pairs_hook=json_object)
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
