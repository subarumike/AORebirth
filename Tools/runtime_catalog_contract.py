"""Check production catalog dependencies without exposing their contents."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET


GAME_DATA_PATHS = "AORebirth/Libraries/Source/AORebirth.Core/GameData/GameDataPaths.cs"
PORK_GAME_DATA = "Tools/PorkForAor/src/GameData.php"
PRODUCTION_READERS = (
    "AORebirth/Server/ZoneEngine_New", "AORebirth/Server/ChatEngine",
    "AORebirth/Server/LoginEngine", "AORebirth/Server/WorldEngine", "AORebirth/Libraries/Source",
)
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
ZONE_CATALOG_TESTS = {
    PVP_TITLES: "StagedLegacyPvpCatalogLoadsEveryAuthoredBand",
    ROOT_TEXT: "StagedLegacyPvpCatalogLoadsEveryAuthoredBand",
    "MonsterCombatRules.json": "StagedEncounterCatalogsLoadAndResolveReferences",
    "NpcPetSummons.json": "StagedEncounterCatalogsLoadAndResolveReferences",
}
ZONE_TEST_SOURCE = "AORebirth/Server/ZoneEngine_New.Tests/RuntimeCatalogTests.cs"
ZONE_TEST_PROJECT = "AORebirth/Server/ZoneEngine_New.Tests/ZoneEngine_New.Tests.csproj"
SCHEMA_FOLDER = "AORebirth/Schemas"
VALIDATION_FILES = {
    "Tools/runtime_catalog_contract.py",
    "Tools/PorkForAor/verify-catalogs.php",
    "tools/windows_release_contract.py",
    ".github/workflows/runtime-data-architecture.yml",
    PORK_GAME_DATA,
    GAME_DATA_PATHS,
    ZONE_TEST_SOURCE,
    ZONE_TEST_PROJECT,
}

# Fixed root readers use ResolveFile(root, path). Dynamic per-playfield readers use separate APIs.
CS_SOURCE_TOKENS = re.compile(r'"(?:\\.|[^"\\])*"|//[^\n]*|/\*[\s\S]*?\*/')
CS_FILE_CONSTANT = re.compile(r'\bconst\s+string\s+(\w+)\s*=\s*("[^"\r\n]+"|GameDataPaths\.\w+)\s*;')
CS_RESOLVE_FILE = re.compile(r'\bGameDataPaths\.ResolveFile\s*\(\s*[^,;]+?,\s*([^;)]+)\s*\)')
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


def csharp_catalog_reads(path, source, shared):
    source = without_comments(source)
    local = csharp_constants(source)
    for expression in CS_RESOLVE_FILE.findall(source):
        selector = expression.strip().removeprefix("relativePath:").strip()
        selector = local.get(selector, selector)
        if selector.startswith("GameDataPaths."):
            selector = shared.get(selector.removeprefix("GameDataPaths."), selector)
        if not re.fullmatch(r'"[A-Za-z0-9_.-]+\.(?:json|dat)"', selector):
            fail(f"Declare a fixed GameData reader path for {path}")
        yield selector[1:-1]


def csharp_catalog_paths(root):
    shared = csharp_constants(without_comments(production_source(root, GAME_DATA_PATHS)))
    for folder in PRODUCTION_READERS:
        for directory, subdirectories, files in os.walk(root / folder):
            subdirectories[:] = sorted(name for name in subdirectories if name not in {"bin", "obj"})
            for name in sorted(files):
                if not name.endswith(".cs"):
                    continue
                path = Path(directory) / name
                raw = path.read_bytes()
                if b"GameDataPaths.ResolveFile" not in raw:
                    continue
                for name in csharp_catalog_reads(path.relative_to(root).as_posix(), raw.decode("utf-8-sig"), shared):
                    yield path, name


def pork_catalog_paths(root):
    return pork_contract_paths(production_source(root, PORK_GAME_DATA))


def pork_contract_paths(source, *, require_coverage=True):
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
    if require_coverage and uncovered:
        fail("PORK GameData catalog has no structural validation: " + ", ".join(sorted(uncovered)))
    return paths


def catalog_dependencies(root):
    csharp_reads = tuple(csharp_catalog_paths(root))
    required = {name for source, name in csharp_reads if source.name in REQUIRED_ZONE_READERS or name in ZONE_CATALOG_TESTS}
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


def git(root, *arguments, no_matches=False):
    result = subprocess.run(["git", "--no-replace-objects", "-C", str(root), *arguments],
                            capture_output=True, timeout=30, check=False)
    if result.returncode != 0 and not (no_matches and result.returncode == 1):
        fail("Cannot read the exact proposed catalog dependency changes")
    return result.stdout.decode("utf-8-sig")


def baseline_reads(root, base):
    shared = csharp_constants(without_comments(git(root, "show", f"{base}:{GAME_DATA_PATHS}")))
    matches = git(root, "grep", "-l", "-z", "-F", "GameDataPaths.ResolveFile", base, "--",
                  *PRODUCTION_READERS, no_matches=True)
    for entry in filter(None, matches.split("\0")):
        path = entry.removeprefix(f"{base}:")
        if path.endswith(".cs"):
            for name in csharp_catalog_reads(path, git(root, "show", entry), shared):
                yield path, name
    for name in pork_contract_paths(git(root, "show", f"{base}:{PORK_GAME_DATA}"), require_coverage=False):
        yield PORK_GAME_DATA, name


def proposed_catalog_changes(root, base):
    if not re.fullmatch(r"[0-9a-f]{40}", base):
        fail("An exact catalog validation base commit is required")
    git(root, "merge-base", "--is-ancestor", base, "HEAD")
    changed = set(git(root, "diff", "--name-only", "-z", "--no-renames", base, "HEAD", "--").split("\0"))
    required, csharp = catalog_dependencies(root)
    reads = {(source.relative_to(root).as_posix(), name) for source, name in csharp}
    reads.update((PORK_GAME_DATA, name) for name in pork_catalog_paths(root))
    old = set(baseline_reads(root, base))
    names = {name for _, name in reads}
    old_names = {name for _, name in old}
    new = names - old_names
    impacted = {name for _, name in reads ^ old}
    impacted.update(name for reader, name in reads | old if reader in changed
                    or "AORebirth/GameData/" + name in changed or schema_path(name) in changed)
    if changed & VALIDATION_FILES:
        impacted.update(required)
    return {"added_dependencies": sorted(new), "removed_dependencies": sorted(old_names - names),
            "affected_catalogs": sorted(impacted)}


def schema_path(name):
    return f"{SCHEMA_FOLDER}/{Path(name).stem}.schema.json"


def require_new_catalog_coverage(root, paths):
    for name in paths:
        if name in PORK_SCHEMAS:
            continue
        method = ZONE_CATALOG_TESTS.get(name)
        if not method or not re.search(rf"\[TestMethod\]\s*public void {re.escape(method)}\s*\(",
                                      production_source(root, ZONE_TEST_SOURCE)):
            fail(f"New runtime catalog requires a focused production reader fixture: {name}")
        if name.endswith(".json"):
            schema = read_json(root / schema_path(name))
            if not isinstance(schema, dict) or not isinstance(schema.get("$schema"), str):
                fail(f"New runtime catalog requires a declared JSON schema: {name}")


def reconciliation_work(required, rows, upstream, published, checkpoint, prior_rows):
    installed = {row["path"]: row for row in rows}
    missing_authority, needs_reconciliation = [], []
    for name in required:
        authorities = [snapshot.get(name) for snapshot in (upstream, published)]
        if authorities == [None, None]:
            missing_authority.append(name)
            continue
        row = installed.get(name)
        if row is None:
            needs_reconciliation.append(name)
            continue
        previous = [checkpoint[authority]["AFTER"].get(name)
                    for authority in ("PRIVATE_CONTENT_UPSTREAM", "PUBLIC_PRODUCT_DATA")]
        retained = authorities == previous and prior_rows.get(name) == row
        current = (upstream.get(name) or published[name])["blob"] == row["blob"]
        if not (retained or current):
            needs_reconciliation.append(name)
    return {"missing_authority": missing_authority, "requires_reconciliation": needs_reconciliation}


def exact_candidate(source, expected):
    if not re.fullmatch(r"[0-9a-f]{40}", expected) or git(source, "rev-parse", "HEAD").strip() != expected:
        fail("The proposed catalog source checkout differs from the requested head")
    if git(source, "status", "--porcelain", "--untracked-files=all"):
        fail("Private catalog validation requires a clean exact proposed source checkout")


def run_private_readers(source, canonical, receipt_root, required):
    commands = {name: shutil.which(name) for name in ("php", "dotnet")}
    if not all(commands.values()):
        fail("Private catalog validation requires the existing PHP and .NET runtimes")
    environment = dict(os.environ, AO_REBIRTH_RUNTIME_CATALOG_VALIDATION_ROOT=str(canonical))
    environment.pop("AO_REBIRTH_GAMEDATA_OVERRIDE_PATH", None)
    environment.pop("GH_TOKEN", None)
    environment.pop("GITHUB_TOKEN", None)
    methods = {ZONE_CATALOG_TESTS[name] for name in required if name in ZONE_CATALOG_TESTS}
    test_filter = "|".join(f"FullyQualifiedName=ZoneEngine_New.Tests.RuntimeCatalogTests.{name}"
                           for name in sorted(methods))
    receipt = receipt_root / "zone-catalog-readers.trx"
    checks = {
        "pork": [commands["php"], "Tools/PorkForAor/verify-catalogs.php", str(canonical)],
        "zone": [commands["dotnet"], "test", ZONE_TEST_PROJECT, "-m:1", "-p:RuntimeCatalogFocusedTests=true",
                 "--nologo", "--filter", test_filter, "--results-directory", str(receipt_root),
                 "--logger", f"trx;LogFileName={receipt.name}"],
    }
    for name, command in checks.items():
        with (receipt_root / f"{name}-catalog-readers.log").open("xb") as log:
            result = subprocess.run(command, cwd=source, env=environment, stdout=log, stderr=subprocess.STDOUT,
                                    check=False, timeout=600)
        if result.returncode:
            fail(f"The private {name} production reader check failed. Review its retained private log.")
    if not any(line.startswith("PORK_CATALOG_LOADER=PASS ")
               for line in (receipt_root / "pork-catalog-readers.log").read_text(encoding="utf-8").splitlines()):
        fail("The private PORK production reader check did not report success")
    try:
        results = ET.parse(receipt).findall(".//{*}UnitTestResult")
    except (OSError, ET.ParseError) as error:
        raise ValueError("The private Zone reader test receipt is unavailable") from error
    if len(results) != len(methods) or {row.get("testName") for row in results} != methods or any(
            row.get("outcome") != "Passed" for row in results):
        fail("Every required private production reader fixture must execute and pass")


def verify_private_premerge(source, owner, expected, base, selected, changes, release):
    exact_candidate(source, expected)
    profile = release.profile_for(owner)
    if release.fresh_public(owner, profile) != base:
        fail("The proposal base differs from freshly fetched public master")
    private = release.gh(profile, "commits/" + profile["PRIVATE_CONTENT_BRANCH"])
    content_sha = private.get("sha", "")
    if not re.fullmatch(r"[0-9a-f]{40}", content_sha):
        fail("Fresh private GameData commit is unknown")
    upstream = release.tree_files(owner, profile, "PRIVATE_CONTENT_UPSTREAM", content_sha)
    published = release.tree_files(source, profile, "PUBLIC_PRODUCT_DATA", expected)

    state = Path(profile["STATE_ROOT"])
    baseline = release.read_json(release.checked(state, "canonical-state.json"))
    prior = Path(baseline["APPLIED_RECONCILIATION_BUNDLE"])
    plan_file = release.load_descriptor(state, {
        "PATH": (prior / "reconciliation.json").relative_to(state).as_posix(),
        "SHA256": baseline["APPLIED_RECONCILIATION_SHA256"],
    })
    plan = release.read_json(plan_file)
    prior_rows = release.read_json(release.load_descriptor(prior, plan["CANONICAL_MANIFEST"]))
    if release.validate_manifest(prior_rows) != baseline["WINDOWS_GAMEDATA_MANIFEST"]:
        fail("Retained canonical checkpoint does not match its applied manifest")

    canonical = Path(profile["CANONICAL_GAMEDATA_ROOT"])
    if selected is not None:
        selected = selected.absolute()
        if not selected.is_relative_to(state):
            fail("Private staging must stay inside the ignored release state root")
        canonical = release.checked(state, selected.relative_to(state).as_posix())
    rows, original = release.inventory(canonical)
    manifest = release.validate_manifest(rows)
    if selected is None and manifest != baseline["WINDOWS_GAMEDATA_MANIFEST"]:
        fail("Selected canonical GameData differs from the applied reconciliation manifest")
    prior_by_path = {row["path"]: row for row in prior_rows}
    if selected is not None:
        staged_by_path = {row["path"]: row for row in rows}
        if prior_by_path.keys() - staged_by_path.keys():
            fail("Private catalog staging cannot silently remove installed GameData")
        for name, row in staged_by_path.items():
            if row == prior_by_path.get(name):
                continue
            authority = upstream.get(name) or published.get(name)
            if (not release.runtime_content_path(name) or authority is None
                    or any(authority[key] != row[key] for key in ("blob", "size"))):
                fail(f"Private staging includes an unverified changed catalog path: {name}")
    required = required_catalog_paths(source)
    work = reconciliation_work(required, rows, upstream, published, plan["SOURCE_SNAPSHOTS"],
                               prior_by_path)
    if any(work.values()):
        fail("Canonical catalog work required: " + json.dumps(work, sort_keys=True))
    if verify_runtime_catalogs(source, canonical, release.runtime_content_path) != required:
        fail("Private production catalog requirements changed during validation")

    receipt_root = Path(tempfile.mkdtemp(prefix="premerge-catalog-", dir=state))
    run_private_readers(source, canonical, receipt_root, required)
    if release.content_states(canonical)[0] != original:
        fail("Canonical catalog tree changed during private production reader checks")
    exact_candidate(source, expected)
    release.write_json(receipt_root / "receipt.json", {
        "PUBLIC_SOURCE_SHA": expected, "PUBLIC_BASE_SHA": base, "PRIVATE_CONTENT_SHA": content_sha,
        "CATALOG_MANIFEST": manifest, "DEPENDENCIES": changes, "CATALOGS": list(required),
        "PRODUCTION_READERS": "PASS",
        "VERIFIED_AT_UTC": release.now(),
    }, exclusive=True)
    return receipt_root / "receipt.json"


def main():
    owning_root = Path(__file__).resolve().parents[1]
    sys.path.insert(0, str(owning_root / "tools"))
    import windows_release_contract as release

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, default=owning_root)
    parser.add_argument("--catalog-root", type=Path)
    parser.add_argument("--base", help="exact public base for changed catalog dependencies")
    parser.add_argument("--authority-root", type=Path, help="owning product checkout for private premerge validation")
    parser.add_argument("--expected-source", help="exact proposed head for private premerge validation")
    args = parser.parse_args()
    try:
        source = args.source_root.resolve(strict=True)
        if args.authority_root or args.expected_source:
            if not all((args.authority_root, args.expected_source, args.base)):
                fail("Private premerge requires the exact base, proposed head, and owning checkout")
            if source != owning_root:
                fail("Run the private premerge check from the reviewed proposal's own catalog contract")
            exact_candidate(source, args.expected_source)
        paths = verify_runtime_catalogs(source, None if args.authority_root else args.catalog_root,
                                        release.runtime_content_path)
        if args.base:
            changes = proposed_catalog_changes(source, args.base)
            require_new_catalog_coverage(source, changes["added_dependencies"])
            print("RUNTIME_CATALOG_CHANGES=" + json.dumps(changes, sort_keys=True))
            if args.authority_root:
                receipt = verify_private_premerge(source, args.authority_root.resolve(strict=True), args.expected_source,
                                                  args.base, args.catalog_root, changes, release)
                print(f"RUNTIME_CATALOG_PRIVATE_RECEIPT={receipt}")
            elif changes["affected_catalogs"]:
                print("RUNTIME_CATALOG_PRIVATE_REVIEW=REQUIRED_BEFORE_MERGE")
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        print(f"RUNTIME_CATALOG_CONTRACT=FAIL {error}", file=sys.stderr)
        return 1
    print(f"RUNTIME_CATALOG_PUBLIC_STRUCTURE=PASS required_paths={len(paths)}")
    private = "PRODUCTION_READERS_PASSED" if args.authority_root else "CHECKED" if args.catalog_root else "NOT_SUPPLIED"
    print(f"RUNTIME_CATALOG_PRIVATE_DATA={private}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
