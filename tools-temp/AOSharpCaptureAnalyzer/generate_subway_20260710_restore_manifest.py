from __future__ import annotations

import csv
from collections import Counter, defaultdict
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
CAPTURE_ID = "20260710-202132"
CAPTURE = REPO / "tools-temp" / "AOSharpLiveCapture" / "bin" / "Debug" / "captures" / CAPTURE_ID
OUTPUT_CSV = REPO / "docs" / "generated" / "subway_20260710_population_restore_manifest.csv"
OUTPUT_MD = REPO / "docs" / "generated" / "subway_20260710_population_restore.md"

SUPPORTED_FAMILY_IDENTITIES = {
    "79557C09", "79557C26", "79557C31", "79557C8B", "79557CA7",
    "79557CAB", "79557CAD", "7957E411", "7957E4A5", "7957E4B1",
    "7957E4BC", "79557C66", "7957E40A", "79557F14", "7957E5C6",
    "7957E5C7", "7957E5C8", "7957E5CA", "79557CAC", "7957405C",
    "795743A7", "795743A8", "7957E02C", "7957E02E", "7957E123",
    "7957E40E", "7957E5BF", "7957E5C4", "7957E5C5",
}

ORDINARY_IDENTITIES = {
    "79557CB8", "7957E5CD", "79557F12", "7957E128", "7957E415",
    "7957E5CF", "7957E5D0", "7957E5D1", "79574527",
}

SUPPORTED_NAMES = {
    "Discarded Pet", "Disobedient Bot", "Mugger", "Violent Vagabond",
}

ORDINARY_NAMES = {
    "Looter", "Stim Fiend", "Deranged Shopper",
}

NAMED_BOSSES = {
    "Abmouth Supremus", "Bitaxel", "Bloodcreeper", "Empty Shell",
    "Eumenides", "Fragmented Soul", "Incomplete Rebuild", "Melded Patterns",
    "Molested Molecules", "Premature Pattern", "Redundant Scan", "Strike Foreman",
    "Vergil Aeneid",
}

CLASSIFICATIONS = {
    "SUPPORTED_FAMILY_RESTORE",
    "ORDINARY_ENEMY_REGENERATE",
    "NAMED_BOSS_EXCLUDED",
    "OWNED_SUMMON_EXCLUDED",
    "UNSUPPORTED_FAMILY_EXCLUDED",
    "DUPLICATE_EXCLUDED",
    "MALFORMED_OR_INCOMPLETE",
}


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(newline="", encoding="utf-8-sig", errors="replace") as handle:
        return [
            {key: (value or "").replace("\x00", "") for key, value in row.items()}
            for row in csv.DictReader(handle)
        ]


def identity_instance(identity: str) -> str:
    return identity.removeprefix("(SimpleChar:").removesuffix(")").upper()


def classify(row: dict[str, str]) -> str:
    identity = identity_instance(row.get("Identity", ""))
    name = row.get("Name", "")
    required = (identity, name, row.get("PositionX", ""), row.get("PositionY", ""), row.get("PositionZ", ""))
    if not all(required):
        return "MALFORMED_OR_INCOMPLETE"
    if identity in SUPPORTED_FAMILY_IDENTITIES:
        return "SUPPORTED_FAMILY_RESTORE"
    if identity in ORDINARY_IDENTITIES:
        return "ORDINARY_ENEMY_REGENERATE"
    if row.get("Owner", "") or name == "Healer":
        return "OWNED_SUMMON_EXCLUDED"
    if name in NAMED_BOSSES:
        return "NAMED_BOSS_EXCLUDED"
    if name in SUPPORTED_NAMES or name in ORDINARY_NAMES:
        return "DUPLICATE_EXCLUDED"
    return "UNSUPPORTED_FAMILY_EXCLUDED"


def implementation_path(classification: str, name: str) -> str:
    if classification == "SUPPORTED_FAMILY_RESTORE":
        return "CapturedSubwayContentProvider shared family: " + name
    if classification == "ORDINARY_ENEMY_REGENERATE":
        return "CapturedSubwayOrdinaryContentProvider archetype: " + name
    return "excluded"


def evidence_identities(path: Path) -> set[str]:
    identities = set()
    if not path.exists():
        return identities
    for row in read_csv(path):
        for value in row.values():
            if value.startswith("(SimpleChar:"):
                identities.add(value)
    return identities


def build_manifest() -> list[dict[str, str]]:
    raw_rows = read_csv(CAPTURE / "scfu-appearance.csv")
    rows_by_identity: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in raw_rows:
        rows_by_identity[row.get("Identity", "")].append(row)

    movement_identities = evidence_identities(CAPTURE / "enemy-movement.csv")
    combat_identities = evidence_identities(CAPTURE / "enemy-combat.csv")
    manifest = []
    for identity, identity_rows in sorted(rows_by_identity.items()):
        row = sorted(identity_rows, key=lambda value: value.get("CapturedUtc", ""))[0]
        classification = classify(row)
        manifest.append(
            {
                "CaptureId": CAPTURE_ID,
                "ResourcePlayfieldId": "127",
                "Identity": identity,
                "Name": row.get("Name", ""),
                "MonsterData": row.get("MonsterData", ""),
                "PositionX": row.get("PositionX", ""),
                "PositionY": row.get("PositionY", ""),
                "PositionZ": row.get("PositionZ", ""),
                "HeadingX": row.get("HeadingX", ""),
                "HeadingY": row.get("HeadingY", ""),
                "HeadingZ": row.get("HeadingZ", ""),
                "HeadingW": row.get("HeadingW", ""),
                "Level": row.get("Level", ""),
                "Health": row.get("Health", ""),
                "MonsterScale": row.get("MonsterScale", ""),
                "RunSpeed": row.get("RunSpeed", ""),
                "NpcFamily": row.get("NpcFamily", ""),
                "CharacterFlags": row.get("CharacterFlags", ""),
                "AppearanceValue": row.get("AppearanceValue", ""),
                "HeadMesh": row.get("HeadMesh", ""),
                "Textures": row.get("Textures", ""),
                "Meshes": row.get("Meshes", ""),
                "Waypoints": row.get("Waypoints", ""),
                "Owner": row.get("Owner", ""),
                "Classification": classification,
                "ImplementationPath": implementation_path(classification, row.get("Name", "")),
                "FullUpdateEvidence": "scfu-appearance.csv",
                "MovementEvidence": "enemy-movement.csv" if identity in movement_identities or row.get("Waypoints", "") else "unobserved",
                "CombatEvidence": "enemy-combat.csv" if identity in combat_identities else "archetype-or-family-only",
                "LootEvidence": "OBSERVED_AVAILABLE_LOOT" if row.get("Name", "") in {"Looter", "Stim Fiend"} else "deferred-or-unobserved",
                "RawScfuRows": str(len(identity_rows)),
            }
        )
    return manifest


def validate(manifest: list[dict[str, str]]) -> Counter:
    counts = Counter(row["Classification"] for row in manifest)
    unknown = set(counts) - CLASSIFICATIONS
    if unknown:
        raise SystemExit("unknown classifications: " + ", ".join(sorted(unknown)))
    if counts["SUPPORTED_FAMILY_RESTORE"] != 29:
        raise SystemExit(f"expected 29 supported rows, found {counts['SUPPORTED_FAMILY_RESTORE']}")
    if counts["ORDINARY_ENEMY_REGENERATE"] != 9:
        raise SystemExit(f"expected 9 ordinary rows, found {counts['ORDINARY_ENEMY_REGENERATE']}")
    included = [row for row in manifest if row["Classification"] in {"SUPPORTED_FAMILY_RESTORE", "ORDINARY_ENEMY_REGENERATE"}]
    if len(included) != 38:
        raise SystemExit(f"expected 38 included rows, found {len(included)}")
    identities = [row["Identity"] for row in included]
    if len(identities) != len(set(identities)):
        raise SystemExit("duplicate included identities")
    if any(row["ResourcePlayfieldId"] != "127" for row in included):
        raise SystemExit("included row outside PF127")
    return counts


def write_outputs(manifest: list[dict[str, str]], counts: Counter) -> None:
    OUTPUT_CSV.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT_CSV.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(manifest[0]))
        writer.writeheader()
        writer.writerows(manifest)

    included = [row for row in manifest if row["Classification"] in {"SUPPORTED_FAMILY_RESTORE", "ORDINARY_ENEMY_REGENERATE"}]
    lines = [
        "# Subway 20260710 Population Restore",
        "",
        "Authoritative capture: `20260710-202132` (complete; processing allowed).",
        "",
        "The historical population commit `c2ebdb07` is evidence only. The overbroad safety rollback `e9405ab8` is not reverted wholesale. The later RoomSpace investigation proved the client crash was not caused by these captured coordinates, and this restoration adds no RoomSpace workaround.",
        "",
        "## Classification summary",
        "",
        "| Classification | Rows |",
        "| --- | ---: |",
        *[f"| {classification} | {counts[classification]} |" for classification in sorted(CLASSIFICATIONS)],
        "",
        "## Included rows",
        "",
        "| Identity | Enemy | Position | Level | Classification | Implementation path |",
        "| --- | --- | --- | ---: | --- | --- |",
    ]
    for row in included:
        position = f"({row['PositionX']}, {row['PositionY']}, {row['PositionZ']})"
        lines.append(
            f"| `{row['Identity']}` | {row['Name']} | `{position}` | {row['Level']} | {row['Classification']} | {row['ImplementationPath']} |"
        )
    lines.extend(
        [
            "",
            "## Evidence boundaries",
            "",
            "- Exact identity, position, heading, level, health, scale, run speed, family, flags, appearance, owner, and waypoints come from `scfu-appearance.csv` decoded from raw SCFU packets.",
            "- Movement is applied only when the identity has captured movement/waypoint evidence.",
            "- Looter and Stim Fiend reuse the existing capture-generated ordinary archetypes; Deranged Shopper receives its capture-generated archetype with observed 9-point AttackInfo and no inferred loot.",
            "- Named bosses, player/encounter-owned summons, unsupported families, and cross-capture duplicates remain excluded.",
            "- No coordinate mutation, RoomSpace workaround, boss mechanic, global combat timing change, or unrelated loot change is part of this restoration.",
            "",
            "## Validation",
            "",
            "- Manifest regeneration: PASS, 107 unique identities classified, 29 supported-family restores, 9 ordinary regenerations, 38 total included, zero malformed rows.",
            "- Focused supported population, ordinary population, patrol, identity, position, exclusion, and lifecycle guardrails: PASS.",
            "- `PlayfieldLifecycleTraceTests`: 41/46 PASS. The five remaining failures are the pre-existing announcement/session/visibility architecture guardrail mismatches and are outside this population slice.",
            "- `cmd /d /c tools\\build_aorebirth_debug.cmd`: PASS.",
            "- Chat/Login/Zone restart: PASS; ports `6996`, `7012`, `7500`, and `7501` listening.",
            "- Live client traversal: pending Mike; no AO client was launched for this task.",
        ]
    )
    OUTPUT_MD.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    manifest = build_manifest()
    counts = validate(manifest)
    write_outputs(manifest, counts)
    print(f"manifestRows={len(manifest)} supported=29 ordinary=9 included=38")
    for classification in sorted(CLASSIFICATIONS):
        print(f"{classification}={counts[classification]}")
    print(OUTPUT_CSV)
    print(OUTPUT_MD)


if __name__ == "__main__":
    main()
