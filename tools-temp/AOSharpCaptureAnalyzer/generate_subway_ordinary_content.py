from __future__ import annotations

import csv
import math
import re
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
CAPTURE_ROOT = REPO / "tools-temp" / "AOSharpLiveCapture" / "bin" / "Debug" / "captures"
CAPTURES = (
    "20260709-205921",
    "20260709-210452",
    "20260709-212115",
    "20260709-212336",
    "20260709-220439",
    "20260709-222339",
    "20260710-202132",
)
SPAWN_CAPTURES = ("20260709-212336", "20260709-222339", "20260710-202132")
CAPTURE_ARCHETYPE_FILTERS = {
    "20260710-202132": frozenset(("Looter", "Stim Fiend", "Deranged Shopper")),
}
ARCHETYPE_CAPTURE_FILTERS = {
    "Deranged Shopper": frozenset(("20260710-202132",)),
}
OUTPUT = (
    REPO
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "Core"
    / "Playfields"
    / "CapturedSubwayOrdinaryContentProvider.cs"
)

ARCHETYPES = {
    "Shadow": ("shadow", "shadow"),
    "Stim Fiend": ("stim_fiend", "stim_fiend"),
    "Workman Striker": ("workman_striker", "striker"),
    "Architect Striker": ("architect_striker", "striker"),
    "Infected Attendant": ("infected_attendant", "infected_attendant"),
    "Slum Runner": ("slum_runner", "slum_runner"),
    "Looter": ("looter", "looter"),
    "Infector": ("infector", "infector"),
    "Lost Thought": ("lost_thought", "lost_thought"),
    "Neural Burnout": ("neural_burnout", "neural_burnout"),
    "Deranged Shopper": ("deranged_shopper", "deranged_shopper"),
}

FLAG_VALUES = {
    "IsNpc": 0x00000001,
    "UnknownFlag": 0x00000002,
    "UnknownFlag6": 0x00000008,
    "HasExtendedTextures": 0x00000010,
    "HasFightingTarget": 0x00000020,
    "HasPlayfieldId": 0x00000040,
    "HasHeadMesh": 0x00000080,
    "HasNoWeaponPairs": 0x00000100,
    "HasHeading": 0x00000200,
    "IsUnderAttack": 0x00000400,
    "HasSmallHealth": 0x00000800,
    "HasExtendedLevel": 0x00001000,
    "HasExtendedRunSpeed": 0x00002000,
    "HasSmallHealthDamage": 0x00004000,
    "HasWaypoints": 0x00010000,
    "HasSmallNpcFamily": 0x00020000,
    "HasSmallNpcLosHeight": 0x00080000,
    "UnknownFlag7": 0x00200000,
    "UnknownFlag2": 0x00200000,
    "IsImmune": 0x00800000,
    "UnknownFlag3": 0x01000000,
    "UnknownDataFlag": 0x02000000,
    "HasOrgName": 0x04000000,
    "IsPet": 0x08000000,
    "UnknownFlag5": 0x10000000,
    "UnknownFlag4": 0x20000000,
}


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def parse_time(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def identity_hex(identity: str) -> str:
    return identity.removeprefix("(SimpleChar:").removesuffix(")")


def parse_flags(value: str) -> int:
    result = 0
    for name in (part.strip() for part in value.split(",")):
        if name:
            result |= FLAG_VALUES.get(name, 0)
    return result


def parse_triplets(value: str) -> list[tuple[str, str, str]]:
    result = []
    for part in value.split("|") if value else []:
        fields = part.split(":")
        if len(fields) == 3:
            result.append((fields[0], fields[1], fields[2]))
    return result


def parse_textures(value: str) -> list[tuple[int, int, int]]:
    return [(int(a), int(b), int(c)) for a, b, c in parse_triplets(value)]


def parse_meshes(value: str) -> list[tuple[int, int, int, int]]:
    result = []
    for part in value.split("|") if value else []:
        fields = part.split(":")
        if len(fields) == 4:
            result.append(tuple(int(field) for field in fields))
    return result


def canonical_position(row: dict[str, str]) -> tuple[float, float, float]:
    waypoints = parse_triplets(row.get("Waypoints", ""))
    if waypoints:
        return tuple(float(value) for value in waypoints[0])
    return (float(row["PositionX"]), float(row["PositionY"]), float(row["PositionZ"]))


def capture_allows_archetype(capture: str, name: str) -> bool:
    allowed_names = CAPTURE_ARCHETYPE_FILTERS.get(capture)
    if allowed_names is not None and name not in allowed_names:
        return False
    allowed_captures = ARCHETYPE_CAPTURE_FILTERS.get(name)
    return allowed_captures is None or capture in allowed_captures


def load_scfu_rows(captures: tuple[str, ...]) -> list[dict[str, str]]:
    rows = []
    for capture in captures:
        for row in read_csv(CAPTURE_ROOT / capture / "scfu-appearance.csv"):
            if row["Name"] not in ARCHETYPES:
                continue
            if not capture_allows_archetype(capture, row["Name"]):
                continue
            row = dict(row)
            row["EvidenceCapture"] = capture
            rows.append(row)
    return rows


def first_rows_by_identity(rows: list[dict[str, str]]) -> list[dict[str, str]]:
    result = {}
    for row in sorted(rows, key=lambda value: parse_time(value["CapturedUtc"])):
        result.setdefault(row["Identity"], row)
    return list(result.values())


def select_spawns() -> list[dict[str, str]]:
    rows = first_rows_by_identity(load_scfu_rows(SPAWN_CAPTURES))
    rows = [row for row in rows if not row.get("Owner")]
    selected: list[dict[str, str]] = []
    for row in sorted(rows, key=lambda value: parse_time(value["CapturedUtc"])):
        position = canonical_position(row)
        duplicate = False
        for prior in selected:
            if prior["Name"] != row["Name"]:
                continue
            prior_position = canonical_position(prior)
            distance = math.sqrt(sum((a - b) ** 2 for a, b in zip(position, prior_position)))
            if distance <= 1.5:
                duplicate = True
                break
        if not duplicate:
            selected.append(row)
    return sorted(selected, key=lambda value: (value["Name"], value["Identity"]))


def visual_profile(row: dict[str, str]) -> tuple:
    return (
        row["AppearanceValue"],
        row["Side"],
        row["Fatness"],
        row["Breed"],
        row["Gender"],
        row["Race"],
        row["MonsterData"],
        row["NpcFamily"],
        row["NpcLosHeight"],
        row["CharacterFlags"],
        row["AccountFlags"],
        row["Expansions"],
        row["VisualFlags"],
        row["VisibleTitle"],
        row["HeadMesh"],
        row["Textures"],
        row["Meshes"],
        row["TextureOverrides"],
    )


def select_archetype_profiles(spawns: list[dict[str, str]]) -> dict[str, dict[str, str]]:
    profiles = {}
    for name in ARCHETYPES:
        candidates = [row for row in spawns if row["Name"] == name]
        counts = Counter(visual_profile(row) for row in candidates)
        selected_profile, _ = counts.most_common(1)[0]
        profiles[name] = next(row for row in candidates if visual_profile(row) == selected_profile)
    return profiles


def combat_profiles() -> dict[str, dict[str, object]]:
    name_by_identity = {}
    for row in load_scfu_rows(CAPTURES):
        name_by_identity[row["Identity"]] = row["Name"]

    attacks = defaultdict(list)
    detail_pattern = re.compile(
        r"WeaponSlot=(?P<slot>-?\d+).*Unk1=(?P<unknown>-?\d+).*WeaponInstance=(?P<instance>-?\d+)"
    )
    for capture in CAPTURES:
        for row in read_csv(CAPTURE_ROOT / capture / "enemy-combat.csv"):
            if row["MessageType"] != "AttackInfo" or row["SourceIdentity"] not in name_by_identity:
                continue
            if not row["Amount"].isdigit() or int(row["Amount"]) <= 0:
                continue
            match = detail_pattern.search(row["Detail"])
            if not match:
                continue
            attacks[name_by_identity[row["SourceIdentity"]]].append(
                {
                    "identity": row["SourceIdentity"],
                    "time": parse_time(row["CapturedUtc"]),
                    "amount": int(row["Amount"]),
                    "slot": int(match.group("slot")),
                    "unknown": int(match.group("unknown")),
                    "instance": int(match.group("instance")),
                }
            )

    result = {}
    for name in ARCHETYPES:
        rows = attacks[name]
        intervals = []
        by_identity = defaultdict(list)
        for row in rows:
            by_identity[row["identity"]].append(row)
        for identity_rows in by_identity.values():
            times = sorted(row["time"] for row in identity_rows)
            for previous, current in zip(times, times[1:]):
                seconds = (current - previous).total_seconds()
                if 0.5 <= seconds <= 10.0:
                    intervals.append(seconds)
        intervals.sort()
        profile = Counter((row["slot"], row["unknown"], row["instance"]) for row in rows)
        slot, unknown, instance = profile.most_common(1)[0][0] if profile else (0, 0, 0)
        result[name] = {
            "observed": bool(rows),
            "min": min((row["amount"] for row in rows), default=0),
            "max": max((row["amount"] for row in rows), default=0),
            "recharge": intervals[(len(intervals) - 1) // 2] if intervals else 0.0,
            "slot": slot,
            "unknown": unknown,
            "instance": instance,
            "rows": len(rows),
        }
    return result


def loot_profiles() -> dict[str, list[dict[str, int]]]:
    mapped = defaultdict(list)
    opened_by_name = defaultdict(list)
    corpse_pattern = re.compile(
        r"^(?P<time>\S+) \[CORPSE-SEEN\] identity=\((?P<identity>Corpse:[0-9A-F]+)\) "
        r"name=Remains of (?P<name>.+?) pos="
    )
    for capture in CAPTURES:
        corpses = []
        with (CAPTURE_ROOT / capture / "events.log").open(encoding="utf-8-sig") as handle:
            for line in handle:
                match = corpse_pattern.search(line)
                if (
                    match
                    and match.group("name") in ARCHETYPES
                    and capture_allows_archetype(capture, match.group("name"))
                ):
                    corpses.append(
                        {
                            "time": parse_time(match.group("time")),
                            "identity": f"({match.group('identity')})",
                            "name": match.group("name"),
                        }
                    )
        corpse_loot = defaultdict(list)
        for row in read_csv(CAPTURE_ROOT / capture / "inventory-updates.csv"):
            if not row["InventoryIdentity"].startswith("(Corpse:"):
                continue
            timestamp = parse_time(row["CapturedUtc"])
            candidates = [
                corpse
                for corpse in corpses
                if corpse["identity"] == row["InventoryIdentity"] and corpse["time"] <= timestamp
            ]
            if not candidates:
                continue
            corpse = max(candidates, key=lambda value: value["time"])
            key = (capture, corpse["identity"], corpse["time"].isoformat(), corpse["name"])
            corpse_loot[key].append((int(row["LowId"]), int(row["HighId"]), int(row["Quality"])))
        for key, items in corpse_loot.items():
            opened_by_name[key[3]].append(items)
    for name, corpse_items in opened_by_name.items():
        opened_corpses = len(corpse_items)
        counts = Counter(item for items in corpse_items for item in items)
        for (low, high, quality), count in sorted(counts.items()):
            mapped[name].append(
                {
                    "low": low,
                    "high": high,
                    "quality": quality,
                    "count": count,
                    "corpses": opened_corpses,
                    "basis": min(10000, int(round(count * 10000.0 / opened_corpses))),
                }
            )
    return mapped


def cs_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def cs_float(value: str | float) -> str:
    text = str(value)
    if "e" in text.lower():
        text = format(float(text), ".9f").rstrip("0").rstrip(".")
    if "." not in text:
        text += ".0"
    return text + "f"


def emit_array(items: list[str], indent: str) -> str:
    if not items:
        return "new " + ("CapturedSubwayTextureDefinition[0]" if "Texture" in indent else "object[0]")
    return "\n".join(items)


def generate() -> str:
    spawns = select_spawns()
    profiles = select_archetype_profiles(spawns)
    combat = combat_profiles()
    loot = loot_profiles()
    evidence_captures = defaultdict(set)
    for evidence_row in load_scfu_rows(CAPTURES):
        evidence_captures[evidence_row["Name"]].add(evidence_row["EvidenceCapture"])
    lines = [
        "// <auto-generated>",
        "// Generated only from completed AOSharp Subway captures by generate_subway_ordinary_content.py.",
        "// Do not hand-edit captured values; regenerate from the checked capture evidence.",
        "// </auto-generated>",
        "namespace AORebirth.Core.Playfields",
        "{",
        "    using System;",
        "    using System.Collections.Generic;",
        "    using System.Linq;",
        "",
        "    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;",
        "",
        "    using ZoneEngine.Core;",
        "    using ZoneEngine.Core.Playfields;",
        "",
        "    internal sealed class CapturedSubwayOrdinaryContentProvider",
        "    {",
        "        public const int SubwayPlayfieldInstance = 127;",
        "",
        "        private static readonly CapturedSubwayOrdinaryArchetypeDefinition[] Archetypes =",
        "        {",
    ]

    for name, (key, family_key) in ARCHETYPES.items():
        row = profiles[name]
        combat_row = combat[name]
        texture_lines = [
            f"new CapturedSubwayTextureDefinition({place}, {texture_id}, {unknown})"
            for place, texture_id, unknown in parse_textures(row["Textures"])
        ]
        mesh_lines = [
            f"new CapturedSubwayMeshDefinition({position}, {mesh_id}u, {override}, {layer})"
            for position, mesh_id, override, layer in parse_meshes(row["Meshes"])
        ]
        loot_lines = [
            "new CapturedSubwayLootEvidenceDefinition("
            f"{item['low']}, {item['high']}, {item['quality']}, {item['count']}, {item['corpses']}, {item['basis']})"
            for item in loot.get(name, [])
        ]
        lines.extend(
            [
                "            new CapturedSubwayOrdinaryArchetypeDefinition(",
                f"                {cs_string(key)},",
                f"                {cs_string(family_key)},",
                f"                {cs_string(name)},",
                f"                {int(row['MonsterData'])},",
                f"                {int(row['NpcFamily'] or 0)},",
                f"                {int(row['NpcLosHeight'] or 0)},",
                f"                {int(row['CharacterFlags'])},",
                f"                {int(row['AccountFlags'])},",
                f"                {int(row['Expansions'])},",
                f"                {int(row['VisualFlags'])},",
                f"                {int(row['VisibleTitle'])},",
                f"                {int(row['AppearanceValue'])}u,",
                f"                {int(row['HeadMesh'] or 0)},",
                "                new CapturedSubwayTextureDefinition[]",
                "                {",
            ]
        )
        for index, item in enumerate(texture_lines):
            lines.append("                    " + item + ("," if index < len(texture_lines) - 1 else ""))
        lines.extend(["                },", "                new CapturedSubwayMeshDefinition[]", "                {"])
        for index, item in enumerate(mesh_lines):
            lines.append("                    " + item + ("," if index < len(mesh_lines) - 1 else ""))
        lines.extend(
            [
                "                },",
                "                new CapturedSubwayCombatEvidenceDefinition(",
                f"                    {str(combat_row['observed']).lower()},",
                f"                    {combat_row['min']},",
                f"                    {combat_row['max']},",
                f"                    {combat_row['recharge']:.6f},",
                f"                    {combat_row['slot']},",
                f"                    {combat_row['unknown']},",
                f"                    {combat_row['instance']},",
                f"                    {combat_row['rows']}),",
                "                new CapturedSubwayLootEvidenceDefinition[]",
                "                {",
            ]
        )
        for index, item in enumerate(loot_lines):
            lines.append("                    " + item + ("," if index < len(loot_lines) - 1 else ""))
        lines.extend(
            [
                "                },",
                "                new string[]",
                "                {",
                *[
                    "                    " + cs_string(capture)
                    + ("," if i < len(evidence_captures[name]) - 1 else "")
                    for i, capture in enumerate(sorted(evidence_captures[name]))
                ],
                "                }),",
            ]
        )

    lines.extend(["        };", "", "        private static readonly CapturedSubwayOrdinarySpawnDefinition[] Spawns =", "        {"])
    for row in spawns:
        key, _ = ARCHETYPES[row["Name"]]
        x, y, z = canonical_position(row)
        waypoints = parse_triplets(row.get("Waypoints", ""))
        lines.extend(
            [
                "            new CapturedSubwayOrdinarySpawnDefinition(",
                f"                0x{identity_hex(row['Identity'])},",
                f"                {cs_string(key)},",
                f"                {int(row['Level'])},",
                f"                {int(row['Health'])},",
                f"                {int(row['HealthDamage'])},",
                f"                {int(row['MonsterScale'])},",
                f"                {int(row['RunSpeed'])},",
                f"                {cs_float(x)}, {cs_float(y)}, {cs_float(z)},",
                f"                {cs_float(row['HeadingX'])}, {cs_float(row['HeadingY'])}, {cs_float(row['HeadingZ'])}, {cs_float(row['HeadingW'])},",
                f"                (SimpleCharFullUpdateFlags)0x{parse_flags(row['ScfuFlags']):08X},",
                f"                {int(row['ScfuFlags2'].replace('HasOwner', '4') or 0) if row['ScfuFlags2'].isdigit() else 0},",
                f"                {cs_string(row['ScfuUnknown1Hex'])},",
                f"                {int(row['ScfuUnknown2'])},",
                "                new CapturedSubwayWaypointDefinition[]",
                "                {",
            ]
        )
        for index, waypoint in enumerate(waypoints):
            suffix = "," if index < len(waypoints) - 1 else ""
            lines.append(
                "                    new CapturedSubwayWaypointDefinition("
                f"{cs_float(waypoint[0])}, {cs_float(waypoint[1])}, {cs_float(waypoint[2])}){suffix}"
            )
        lines.extend(
            [
                "                },",
                f"                {cs_string(row['Owner'])},",
                f"                {cs_string(row['EvidenceCapture'])},",
                f"                {cs_string(row['CapturedUtc'])}),",
            ]
        )

    lines.extend(
        [
            "        };",
            "",
            "        private static readonly Dictionary<string, CapturedSubwayOrdinaryArchetypeDefinition> ArchetypesByKey =",
            "            Archetypes.ToDictionary(value => value.Key, StringComparer.Ordinal);",
            "",
            "        public CapturedSubwayOrdinaryArchetypeDefinition[] GetArchetypes()",
            "        {",
            "            return Archetypes.ToArray();",
            "        }",
            "",
            "        public CapturedSubwayOrdinarySpawnDefinition[] GetSpawns()",
            "        {",
            "            return Spawns",
            "                .Where(",
            "                    spawn => !string.Equals(spawn.EvidenceCapture, \"20260710-202132\", StringComparison.Ordinal)",
            "                             || SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(spawn.SourceInstance))",
            "                .ToArray();",
            "        }",
            "",
            "        public bool TryGetArchetype(string key, out CapturedSubwayOrdinaryArchetypeDefinition archetype)",
            "        {",
            "            return ArchetypesByKey.TryGetValue(key, out archetype);",
            "        }",
            "",
            "        public CombatLootTableEntry[] BuildCapturedLootEntries()",
            "        {",
            "            var entries = new List<CombatLootTableEntry>();",
            "            foreach (CapturedSubwayOrdinaryArchetypeDefinition archetype in Archetypes)",
            "            {",
            "                int slot = 0;",
            "                foreach (CapturedSubwayLootEvidenceDefinition loot in archetype.LootEvidence)",
            "                {",
            "                    entries.Add(",
            "                        new CombatLootTableEntry",
            "                        {",
            "                            ExactName = archetype.Name,",
            "                            MonsterData = archetype.MonsterData,",
            "                            NpcFamily = archetype.NpcFamily,",
            "                            Slot = slot++,",
            "                            DropChanceBasisPoints = loot.ObservedBasisPoints,",
            "                            ItemTemplates =",
            "                                new[]",
            "                                {",
            "                                    new CombatLootItemTemplate",
            "                                    {",
            "                                        LowId = loot.LowId,",
            "                                        HighId = loot.HighId,",
            "                                        MinQuality = loot.Quality,",
            "                                        MaxQuality = loot.Quality,",
            "                                        RangeCheck = 0,",
            "                                        DropGroupHash = \"captured-subway-ordinary\"",
            "                                    }",
            "                                }",
            "                        });",
            "                }",
            "            }",
            "",
            "            return entries.ToArray();",
            "        }",
            "    }",
            "",
            "    internal sealed class CapturedSubwayOrdinaryArchetypeDefinition",
            "    {",
            "        public CapturedSubwayOrdinaryArchetypeDefinition(string key, string familyKey, string name, int monsterData, int npcFamily, int npcLosHeight, int characterFlags, int accountFlags, int expansions, int visualFlags, int visibleTitle, uint appearanceValue, int headMesh, CapturedSubwayTextureDefinition[] textures, CapturedSubwayMeshDefinition[] meshes, CapturedSubwayCombatEvidenceDefinition combat, CapturedSubwayLootEvidenceDefinition[] lootEvidence, string[] evidenceCaptures)",
            "        {",
            "            this.Key = key; this.FamilyKey = familyKey; this.Name = name; this.MonsterData = monsterData; this.NpcFamily = npcFamily; this.NpcLosHeight = npcLosHeight; this.CharacterFlags = characterFlags; this.AccountFlags = accountFlags; this.Expansions = expansions; this.VisualFlags = visualFlags; this.VisibleTitle = visibleTitle; this.AppearanceValue = appearanceValue; this.HeadMesh = headMesh; this.Textures = textures ?? new CapturedSubwayTextureDefinition[0]; this.Meshes = meshes ?? new CapturedSubwayMeshDefinition[0]; this.Combat = combat; this.LootEvidence = lootEvidence ?? new CapturedSubwayLootEvidenceDefinition[0]; this.EvidenceCaptures = evidenceCaptures ?? new string[0];",
            "        }",
            "        public string Key { get; private set; } public string FamilyKey { get; private set; } public string Name { get; private set; } public int MonsterData { get; private set; } public int NpcFamily { get; private set; } public int NpcLosHeight { get; private set; } public int CharacterFlags { get; private set; } public int AccountFlags { get; private set; } public int Expansions { get; private set; } public int VisualFlags { get; private set; } public int VisibleTitle { get; private set; } public uint AppearanceValue { get; private set; } public int HeadMesh { get; private set; } public CapturedSubwayTextureDefinition[] Textures { get; private set; } public CapturedSubwayMeshDefinition[] Meshes { get; private set; } public CapturedSubwayCombatEvidenceDefinition Combat { get; private set; } public CapturedSubwayLootEvidenceDefinition[] LootEvidence { get; private set; } public string[] EvidenceCaptures { get; private set; }",
            "    }",
            "",
            "    internal sealed class CapturedSubwayOrdinarySpawnDefinition",
            "    {",
            "        public CapturedSubwayOrdinarySpawnDefinition(int sourceInstance, string archetypeKey, int level, int health, int healthDamage, int monsterScale, int runSpeed, float x, float y, float z, float headingX, float headingY, float headingZ, float headingW, SimpleCharFullUpdateFlags capturedFlags, int capturedFlags2, string unknown1Hex, int unknown2, CapturedSubwayWaypointDefinition[] waypoints, string sourceOwnerIdentity, string evidenceCapture, string evidenceTimestamp)",
            "        {",
            "            this.SourceInstance = sourceInstance; this.ArchetypeKey = archetypeKey; this.Level = level; this.Health = health; this.HealthDamage = healthDamage; this.MonsterScale = monsterScale; this.RunSpeed = runSpeed; this.X = x; this.Y = y; this.Z = z; this.HeadingX = headingX; this.HeadingY = headingY; this.HeadingZ = headingZ; this.HeadingW = headingW; this.CapturedFlags = capturedFlags; this.CapturedFlags2 = capturedFlags2; this.Unknown1 = HexToBytes(unknown1Hex); this.Unknown2 = unknown2; this.Waypoints = waypoints ?? new CapturedSubwayWaypointDefinition[0]; this.SourceOwnerIdentity = sourceOwnerIdentity; this.EvidenceCapture = evidenceCapture; this.EvidenceTimestamp = evidenceTimestamp;",
            "        }",
            "        public int SourceInstance { get; private set; } public string ArchetypeKey { get; private set; } public int Level { get; private set; } public int Health { get; private set; } public int HealthDamage { get; private set; } public int MonsterScale { get; private set; } public int RunSpeed { get; private set; } public float X { get; private set; } public float Y { get; private set; } public float Z { get; private set; } public float HeadingX { get; private set; } public float HeadingY { get; private set; } public float HeadingZ { get; private set; } public float HeadingW { get; private set; } public SimpleCharFullUpdateFlags CapturedFlags { get; private set; } public int CapturedFlags2 { get; private set; } public byte[] Unknown1 { get; private set; } public int Unknown2 { get; private set; } public CapturedSubwayWaypointDefinition[] Waypoints { get; private set; } public string SourceOwnerIdentity { get; private set; } public string EvidenceCapture { get; private set; } public string EvidenceTimestamp { get; private set; }",
            "        private static byte[] HexToBytes(string value) { if (string.IsNullOrEmpty(value)) return new byte[0]; var result = new byte[value.Length / 2]; for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(value.Substring(i * 2, 2), 16); return result; }",
            "    }",
            "",
            "    internal sealed class CapturedSubwayTextureDefinition { public CapturedSubwayTextureDefinition(int place, int id, int unknown) { this.Place = place; this.Id = id; this.Unknown = unknown; } public int Place { get; private set; } public int Id { get; private set; } public int Unknown { get; private set; } }",
            "    internal sealed class CapturedSubwayMeshDefinition { public CapturedSubwayMeshDefinition(int position, uint id, int overrideTextureId, int layer) { this.Position = position; this.Id = id; this.OverrideTextureId = overrideTextureId; this.Layer = layer; } public int Position { get; private set; } public uint Id { get; private set; } public int OverrideTextureId { get; private set; } public int Layer { get; private set; } }",
            "    internal sealed class CapturedSubwayWaypointDefinition { public CapturedSubwayWaypointDefinition(float x, float y, float z) { this.X = x; this.Y = y; this.Z = z; } public float X { get; private set; } public float Y { get; private set; } public float Z { get; private set; } }",
            "    internal sealed class CapturedSubwayCombatEvidenceDefinition { public CapturedSubwayCombatEvidenceDefinition(bool observed, int minDamage, int maxDamage, double rechargeSeconds, int weaponSlot, int attackInfoUnknown, int weaponInstance, int observedRows) { this.Observed = observed; this.MinDamage = minDamage; this.MaxDamage = maxDamage; this.RechargeSeconds = rechargeSeconds; this.WeaponSlot = weaponSlot; this.AttackInfoUnknown = attackInfoUnknown; this.WeaponInstance = weaponInstance; this.ObservedRows = observedRows; } public bool Observed { get; private set; } public int MinDamage { get; private set; } public int MaxDamage { get; private set; } public double RechargeSeconds { get; private set; } public int WeaponSlot { get; private set; } public int AttackInfoUnknown { get; private set; } public int WeaponInstance { get; private set; } public int ObservedRows { get; private set; } }",
            "    internal sealed class CapturedSubwayLootEvidenceDefinition { public CapturedSubwayLootEvidenceDefinition(int lowId, int highId, int quality, int observedCount, int observedCorpses, int observedBasisPoints) { this.LowId = lowId; this.HighId = highId; this.Quality = quality; this.ObservedCount = observedCount; this.ObservedCorpses = observedCorpses; this.ObservedBasisPoints = observedBasisPoints; } public int LowId { get; private set; } public int HighId { get; private set; } public int Quality { get; private set; } public int ObservedCount { get; private set; } public int ObservedCorpses { get; private set; } public int ObservedBasisPoints { get; private set; } }",
            "}",
            "",
        ]
    )
    return "\n".join(lines)


if __name__ == "__main__":
    OUTPUT.write_text(generate(), encoding="utf-8")
    print(f"generated {OUTPUT}")
