"""Check production catalog dependencies without exposing their contents."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from urllib.request import Request, urlopen


GAME_DATA_PATHS = "AORebirth/Libraries/Source/AORebirth.Core/GameData/GameDataPaths.cs"
PORK_GAME_DATA = "Tools/PorkForAor/src/GameData.php"
PRODUCTION_READERS = ("AORebirth/Server/ZoneEngine_New", "AORebirth/Server/ChatEngine")
REQUIRED_ZONE_READERS = {"PvpLegacyTitleCatalog.cs", "MonsterCombatRules.cs", "NpcPetSummonProfile.cs"}
PVP_TITLES = "PvpLegacyTitles.json"
ROOT_TEXT = "Text.json"
PORK_NAME_SECTIONS = {
    "Breed.json": "Breeds",
    "Sex.json": "Sex",
    "Side.json": "Sides",
    "Profession.json": "Professions",
    "GoverningForms.json": "Forms",
}
PORK_SCHEMAS = {*PORK_NAME_SECTIONS, PVP_TITLES, ROOT_TEXT}
TRUSTED_CHECK = "dao-canonical-validation"
PUBLIC_CHECK = "content-boundary"
GITHUB_ACTIONS_APP = 15368
VALIDATION_FILES = {
    "Tools/runtime_catalog_contract.py",
    "Tools/PorkForAor/verify-catalogs.php",
    "tools/windows_release_contract.py",
    ".github/workflows/runtime-data-architecture.yml",
    PORK_GAME_DATA,
    GAME_DATA_PATHS,
}

# Fixed root readers use ResolveFile(root, path). Dynamic per-playfield readers use separate APIs.
CS_SOURCE_TOKENS = re.compile(r'"(?:\\.|[^"\\])*"|//[^\n]*|/\*[\s\S]*?\*/')
CS_FILE_CONSTANT = re.compile(r'\bconst\s+string\s+(\w+)\s*=\s*("[^"\r\n]+"|GameDataPaths\.\w+)\s*;')
CS_RESOLVE_FILE = re.compile(r'\bGameDataPaths\.ResolveFile\(\s*[^,;]+?,\s*([^;)]+)\s*\)')
PHP_REQUIRED = re.compile(r'\bpublic\s+const\s+REQUIRED\s*=\s*\[([^\]]*)\]\s*;')
PHP_FILE = re.compile(r"'([0-9A-Za-z_-]+\.json)'")
PHP_LOAD = re.compile(r"\$this->(?:load|name)\(\s*'([0-9A-Za-z_-]+\.json)'")


def fail(message):
    raise ValueError(message)


def production_source(root, path):
    return (root / path).read_text(encoding="utf-8-sig")


def csharp_constants(source):
    return dict(CS_FILE_CONSTANT.findall(source))


def without_comments(source):
    return CS_SOURCE_TOKENS.sub(lambda token: " " if token[0].startswith(("//", "/*")) else token[0], source)


def csharp_catalog_paths(root):
    shared = csharp_constants(without_comments(production_source(root, GAME_DATA_PATHS)))
    for folder in PRODUCTION_READERS:
        for directory, subdirectories, files in os.walk(root / folder):
            subdirectories[:] = sorted(name for name in subdirectories if name not in {"bin", "obj"})
            for name in sorted(files):
                if not name.endswith(".cs"):
                    continue
                path = Path(directory) / name
                source = without_comments(path.read_text(encoding="utf-8-sig"))
                matches = CS_RESOLVE_FILE.findall(source)
                if not matches:
                    continue
                local = csharp_constants(source)
                for expression in matches:
                    selector = expression.strip().removeprefix("relativePath:").strip()
                    selector = local.get(selector, selector)
                    if selector.startswith("GameDataPaths."):
                        selector = shared.get(selector.removeprefix("GameDataPaths."), selector)
                    if not re.fullmatch(r'"[A-Za-z0-9_.-]+\.(?:json|dat)"', selector):
                        fail(f"Declare a fixed GameData reader path for {path.relative_to(root).as_posix()}")
                    yield path, selector[1:-1]


def pork_catalog_paths(root):
    source = production_source(root, PORK_GAME_DATA)
    declarations = PHP_REQUIRED.findall(source)
    if len(declarations) != 1:
        fail("PORK GameData must declare its required catalogs")
    declaration = declarations[0]
    paths = PHP_FILE.findall(declaration)
    if not paths or len(paths) != len(set(paths)) or not re.fullmatch(r"[\s,]*", PHP_FILE.sub("", declaration)):
        fail("PORK GameData required catalogs must be unique literal filenames")
    if set(PHP_LOAD.findall(source)) - set(paths):
        fail("PORK GameData reads an undeclared catalog")
    uncovered = set(paths) - PORK_SCHEMAS
    if uncovered:
        fail("PORK GameData catalog has no structural validation: " + ", ".join(sorted(uncovered)))
    return paths


def catalog_dependencies(root):
    csharp_reads = tuple(csharp_catalog_paths(root))
    required = {name for source, name in csharp_reads if source.name in REQUIRED_ZONE_READERS}
    required.update(pork_catalog_paths(root))
    return tuple(sorted(required)), csharp_reads


def required_catalog_paths(source_root: Path) -> tuple[str, ...]:
    return catalog_dependencies(Path(source_root))[0]


def unique_pairs(pairs):
    row = {}
    for key, value in pairs:
        if key in row:
            fail("duplicate JSON property")
        row[key] = value
    return row


def read_json(path):
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique_pairs,
                          parse_constant=lambda _: fail("invalid JSON number"))
    except (OSError, UnicodeError, ValueError) as error:
        raise ValueError(f"Missing or invalid catalog: {path.name}") from error


def content_path(root, name):
    path = root / name
    if (not path.is_file() or path.is_symlink() or getattr(path, "is_junction", lambda: False)()
            or not path.resolve().is_relative_to(root)):
        fail(f"Required catalog unavailable in selected GameData: {name}")
    return path


def has_name(value):
    return isinstance(value, str) and bool(value.strip())


def validate_names(name, section, document):
    rows = document.get(section) if isinstance(document, dict) else None
    if not isinstance(rows, dict) or not rows:
        fail(f"{name} requires a nonempty {section} object")
    for key, row in rows.items():
        if not (re.fullmatch(r"0|[1-9][0-9]*", key) and isinstance(row, dict) and has_name(row.get("Name"))):
            fail(f"{name} contains an invalid named identity")
        if section == "Forms":
            ranks = row.get("Ranks")
            if not isinstance(ranks, list) or not ranks or not all(map(has_name, ranks)):
                fail(f"{name} contains an invalid organization rank list")


def validate_title_bands(document, texts=None):
    category = document.get("TextCategory") if isinstance(document, dict) else None
    rows = document.get("Bands") if isinstance(document, dict) else None
    if type(category) is not int or category <= 0 or not isinstance(rows, list) or not rows:
        fail(f"{PVP_TITLES} requires a positive text category and nonempty bands")
    ratings = set()
    for row in rows:
        rating = row.get("MinRating") if isinstance(row, dict) else None
        if type(rating) is not int or rating < 0 or rating in ratings or "TextId" not in row:
            fail(f"{PVP_TITLES} contains an invalid or duplicate rating band")
        ratings.add(rating)
        text_id, fallback = row["TextId"], row.get("Fallback", "")
        if not isinstance(fallback, str) or (text_id is None and not has_name(fallback)):
            fail(f"{PVP_TITLES} contains a band with no title binding")
        if text_id is not None and (type(text_id) is not int or text_id < 0):
            fail(f"{PVP_TITLES} contains an invalid text identity")
        if text_id is not None and texts is not None and not has_name(texts.get((category, text_id))):
            fail(f"{PVP_TITLES} has an unresolved {ROOT_TEXT} reference ({category}, {text_id})")


def text_index(document):
    if not isinstance(document, list) or not document:
        fail(f"{ROOT_TEXT} requires nonempty text records")
    texts = {}
    for row in document:
        if (not isinstance(row, dict) or type(row.get("category")) is not int
                or type(row.get("id")) is not int or not isinstance(row.get("text"), str)):
            fail(f"{ROOT_TEXT} contains an invalid text record")
        key = row["category"], row["id"]
        if key in texts:
            fail(f"{ROOT_TEXT} contains a duplicate category and text identity")
        texts[key] = row["text"]
    return texts


def pork_text_categories(source_root):
    source = production_source(source_root, PORK_GAME_DATA)
    for member in ("PROFESSION_TITLE_CATEGORY", "DEFENDER_RANK_CATEGORY"):
        values = re.findall(rf"\bprivate\s+const\s+{member}\s*=\s*([0-9]+)\s*;", source)
        if len(values) != 1:
            fail(f"PORK GameData must declare one {member}")
        yield int(values[0])


def verify_runtime_catalogs(source_root: Path, catalog_root: Path | None, is_admitted) -> tuple[str, ...]:
    source_root = Path(source_root)
    paths, csharp_reads = catalog_dependencies(source_root)
    declared = set(paths) | {name for _, name in csharp_reads}
    denied = sorted(path for path in declared if not is_admitted(path))
    if denied:
        fail("Production readers reference unadmitted GameData: " + ", ".join(denied))

    public_data = source_root / "AORebirth/GameData"
    for name, section in PORK_NAME_SECTIONS.items():
        validate_names(name, section, read_json(public_data / name))
    validate_title_bands(read_json(public_data / PVP_TITLES))
    if catalog_root is None:
        return paths

    canonical = Path(catalog_root).resolve(strict=True)
    missing = [name for name in paths if not (canonical / name).is_file()]
    if missing:
        fail("Required catalogs unavailable in selected GameData: " + ", ".join(missing))
    files = {name: content_path(canonical, name) for name in paths}
    for name, section in PORK_NAME_SECTIONS.items():
        validate_names(name, section, read_json(files[name]))
    texts = text_index(read_json(files[ROOT_TEXT]))
    validate_title_bands(read_json(files[PVP_TITLES]), texts)
    populated_categories = {category for (category, _), text in texts.items() if has_name(text)}
    if any(category not in populated_categories for category in pork_text_categories(source_root)):
        fail(f"{ROOT_TEXT} lacks a required PORK text category")
    return paths


def git(root, *arguments):
    result = subprocess.run(["git", "-C", str(root), *arguments], capture_output=True, timeout=30, check=False)
    if result.returncode != 0:
        fail("Cannot read the exact proposed catalog dependency changes")
    return result.stdout.decode("utf-8")


def affects_catalog_contract(root, base):
    if not re.fullmatch(r"[0-9a-f]{40}", base):
        fail("An exact catalog validation base commit is required")
    git(root, "merge-base", "--is-ancestor", base, "HEAD")
    changed = set(git(root, "diff", "--name-only", "-z", "--no-renames", base, "--").split("\0"))
    if changed & VALIDATION_FILES:
        return True

    required, readers = catalog_dependencies(root)
    paths = set(required) | {name for _, name in readers}
    data_changes = {"AORebirth/GameData/" + name for name in paths}
    data_changes.update(f"AORebirth/Schemas/{Path(name).stem}.schema.json" for name in paths)
    if changed & data_changes or changed & {source.relative_to(root).as_posix() for source, _ in readers}:
        return True

    # A deleted loader or a removed ResolveFile call also changes the dependency contract.
    deleted_scope = sorted(path for path in changed if path.endswith(".cs")
                           and any(path.startswith(folder + "/") for folder in PRODUCTION_READERS))
    if not deleted_scope:
        return False
    baseline_files = set(git(root, "ls-tree", "-r", "--name-only", "-z", base, "--", *deleted_scope).split("\0"))
    return any(CS_RESOLVE_FILE.search(without_comments(git(root, "show", f"{base}:{path}")))
               for path in deleted_scope if path in baseline_files)


def require_trusted_binding(rules):
    if not isinstance(rules, list) or not all(isinstance(rule, dict) for rule in rules):
        fail("Effective master protection is unavailable")
    for rule in rules:
        if rule.get("type") != "required_status_checks":
            continue
        parameters = rule.get("parameters", {})
        checks = parameters.get("required_status_checks", [])
        trusted_ids = [row.get("integration_id") for row in checks if row.get("context") == TRUSTED_CHECK]
        if (parameters.get("strict_required_status_checks_policy") is not True or len(trusted_ids) != 1
                or type(trusted_ids[0]) is not int or trusted_ids[0] <= 0 or trusted_ids[0] == GITHUB_ACTIONS_APP):
            continue
        source = rule.get("ruleset_id")
        siblings = {row.get("type") for row in rules if row.get("ruleset_id") == source}
        public_bound = any(row.get("context") == PUBLIC_CHECK and row.get("integration_id") == GITHUB_ACTIONS_APP
                           for row in checks)
        if type(source) is int and source > 0 and public_bound and {"pull_request", "deletion", "non_fast_forward"} <= siblings:
            return
    fail("Canonical GameData changes require an effective strict master rule bound to the trusted validation App")


def fetch_master_rules(repository):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        fail("Invalid public repository identity")
    headers = {"Accept": "application/vnd.github+json", "User-Agent": "AORebirth-runtime-catalog-validation"}
    if os.environ.get("GH_TOKEN"):
        headers["Authorization"] = "Bearer " + os.environ["GH_TOKEN"]
    request = Request(f"https://api.github.com/repos/{repository}/rules/branches/master", headers=headers)
    try:
        with urlopen(request, timeout=15) as response:
            return json.load(response)
    except (OSError, ValueError) as error:
        raise ValueError("Cannot read effective master protection") from error


def main():
    owning_root = Path(__file__).resolve().parents[1]
    sys.path.insert(0, str(owning_root / "tools"))
    from windows_release_contract import PUBLIC_REPOSITORY, runtime_content_path

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, default=owning_root)
    parser.add_argument("--catalog-root", type=Path)
    parser.add_argument("--require-trusted-premerge-base")
    args = parser.parse_args()
    try:
        paths = verify_runtime_catalogs(args.source_root, args.catalog_root, runtime_content_path)
    except (OSError, ValueError) as error:
        print(f"RUNTIME_CATALOG_CONTRACT=FAIL {error}", file=sys.stderr)
        return 1
    print(f"RUNTIME_CATALOG_PUBLIC_STRUCTURE=PASS required_paths={len(paths)}")
    print(f"RUNTIME_CATALOG_PRIVATE_DATA={'CHECKED' if args.catalog_root is not None else 'NOT_SUPPLIED'}")
    if args.require_trusted_premerge_base:
        try:
            if affects_catalog_contract(args.source_root.resolve(), args.require_trusted_premerge_base):
                require_trusted_binding(fetch_master_rules(PUBLIC_REPOSITORY))
                print("RUNTIME_CATALOG_PRIVATE_CHECK=REQUIRED_BY_MASTER_RULE")
            else:
                print("RUNTIME_CATALOG_PRIVATE_CHECK=NO_CATALOG_CONTRACT_CHANGE")
        except (OSError, ValueError, subprocess.SubprocessError) as error:
            print(f"RUNTIME_CATALOG_PRIVATE_CHECK=BLOCKED {error}", file=sys.stderr)
            return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
