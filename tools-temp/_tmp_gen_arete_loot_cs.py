# -*- coding: utf-8 -*-
"""Generate CapturedAreteLandingLootDefinitions.cs from arete part 1/2 corpse opens."""
import csv
import pathlib
import re
import sys
from collections import defaultdict

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth")
CAPS = [
    ROOT / r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\arete part 1",
    ROOT / r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\arete part 2",
]
OUT = ROOT / r"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedAreteLandingLootDefinitions.cs"
EVIDENCE = "AOSharpLiveCapture arete part 1 + arete part 2 corpse-loot-observations InitialSnapshot"

def slug(name):
    s = re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-")
    return s or "unnamed"


def parse_items(items_field):
    out = []
    if not items_field:
        return out
    for part in items_field.split(";"):
        part = part.strip()
        if not part:
            continue
        bits = part.split(":")
        if len(bits) < 4:
            continue
        out.append((int(bits[0]), int(bits[1]), int(bits[2]), int(bits[3])))
    return out


# name -> list of (credits, items, corpse_id, source_folder)
by_name = defaultdict(list)
md_by_name = {}

for cap in CAPS:
    folder = cap.name
    with (cap / "corpse-loot-observations.csv").open(encoding="utf-8-sig", newline="") as fh:
        for row in csv.DictReader(fh):
            if str(row.get("InitialSnapshot", "")).lower() != "true":
                continue
            name = (row.get("EnemyName") or "").strip()
            if not name or name == "(unnamed)":
                continue
            md = int(row.get("MonsterData") or "0")
            credits = int(row.get("CorpseCredits") or "0")
            items = parse_items(row.get("Items") or "")
            corpse = (row.get("CorpseIdentity") or "unknown").replace("(", "").replace(")", "").replace(":", "")
            by_name[name].append((credits, items, corpse, folder))
            md_by_name[name] = md

lines = []
a = lines.append
a("namespace AORebirth.Core.Playfields")
a("{")
a("    using System;")
a("    using System.Collections.Generic;")
a("")
a("    /// <summary>")
a("    /// Capture-backed Arete Landing corpse loot from")
a("    /// tools-temp/AOSharpLiveCapture/.../captures/arete part 1|2.")
a("    /// One observed snapshot per InitialSnapshot corpse open (items + credits).")
a("    /// Match by exact enemy name on playfield 6553.")
a("    /// </summary>")
a("    internal static class CapturedAreteLandingLootDefinitions")
a("    {")
a("        internal const int AreteLandingPlayfieldId = 6553;")
a("")
a("        private const string Evidence =")
a('            "' + EVIDENCE + '";')
a("")
a("        private sealed class MobLootDefinition")
a("        {")
a("            public string ExactName;")
a("            public string ProfileKey;")
a("            public int MonsterData;")
a("            public ObservedCorpseSnapshotDefinition[] Snapshots;")
a("        }")
a("")
a("        private static readonly MobLootDefinition[] Mobs =")
a("        {")

for name in sorted(by_name.keys()):
    snaps = by_name[name]
    md = md_by_name[name]
    profile = "captured.arete." + slug(name)
    a("            new MobLootDefinition")
    a("            {")
    a('                ExactName = "' + name.replace('\\', '\\\\').replace('"', '\\"') + '",')
    a('                ProfileKey = "' + profile + '",')
    a("                MonsterData = " + str(md) + ",")
    a("                Snapshots =")
    a("                    new[]")
    a("                    {")
    for i, (credits, items, corpse, folder) in enumerate(snaps):
        snap_key = "arete." + slug(name) + "." + slug(folder) + "." + corpse.lower() + "." + str(i)
        if items:
            a("                        Snapshot(")
            a('                            "' + snap_key + '",')
            a("                            " + str(credits) + ",")
            for j, (low, high, ql, qty) in enumerate(items):
                comma = "," if j < len(items) - 1 else ""
                a(
                    "                            Entry(\""
                    + snap_key
                    + "\", "
                    + str(low)
                    + ", "
                    + str(high)
                    + ", "
                    + str(ql)
                    + ", "
                    + str(qty)
                    + ")"
                    + comma
                )
            a("                            ),")
        else:
            a(
                '                        Snapshot("'
                + snap_key
                + '", '
                + str(credits)
                + "),"
            )
    a("                    }")
    a("            },")

a("        };")
a("")
a("        private static readonly Dictionary<string, MobLootDefinition> ByExactName =")
a("            BuildByExactName();")
a("")
a("        private static Dictionary<string, MobLootDefinition> BuildByExactName()")
a("        {")
a("            Dictionary<string, MobLootDefinition> map =")
a("                new Dictionary<string, MobLootDefinition>(StringComparer.OrdinalIgnoreCase);")
a("            for (int i = 0; i < Mobs.Length; i++)")
a("            {")
a("                map[Mobs[i].ExactName] = Mobs[i];")
a("            }")
a("")
a("            return map;")
a("        }")
a("")
a("        internal static bool TryRegister(")
a("            LootTableRegistry registry,")
a("            string enemyName,")
a("            out string profileKey)")
a("        {")
a("            profileKey = null;")
a("            if (registry == null || string.IsNullOrWhiteSpace(enemyName))")
a("            {")
a("                return false;")
a("            }")
a("")
a("            MobLootDefinition mob;")
a("            if (!ByExactName.TryGetValue(enemyName.Trim(), out mob) || mob == null)")
a("            {")
a("                return false;")
a("            }")
a("")
a("            profileKey = mob.ProfileKey;")
a("            string tableKey = \"captured.\" + mob.ProfileKey;")
a("            if (registry.ContainsTable(tableKey))")
a("            {")
a("                return true;")
a("            }")
a("")
a("            registry.RegisterTable(")
a("                new LootTableDefinition")
a("                {")
a("                    LootTableKey = tableKey,")
a("                    DisplayName = mob.ExactName + \" Arete captured corpse\",")
a("                    TableType = LootTableType.EnemyType,")
a("                    RollGroups = new LootGroupDefinition[0],")
a("                    ObservedCorpseSnapshots = mob.Snapshots,")
a("                    CreditsPolicy = new CreditsPolicyDefinition")
a("                    {")
a("                        Mode = CreditsPolicyMode.Unresolved,")
a("                        Evidence = LootEvidenceConfidence.Unresolved")
a("                    },")
a("                    QualityPolicy = \"captured-observed-corpse-snapshots\",")
a("                    Evidence = Evidence,")
a("                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,")
a("                    ItemPoolUnresolved = true,")
a("                    Enabled = true")
a("                });")
a("            registry.RegisterAssignment(")
a("                new LootAssignmentDefinition")
a("                {")
a("                    AssignmentKey = tableKey,")
a("                    TargetType = LootAssignmentTargetType.EnemyType,")
a("                    TargetKey = mob.ProfileKey,")
a("                    LootTableKey = tableKey,")
a("                    Priority = 0,")
a("                    Conditions = new string[0],")
a("                    Evidence = Evidence,")
a("                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,")
a("                    Enabled = true")
a("                });")
a("            return true;")
a("        }")
a("")
a("        internal static bool TryGetTypicalCredits(string enemyName, out int credits)")
a("        {")
a("            credits = 0;")
a("            if (string.IsNullOrWhiteSpace(enemyName))")
a("            {")
a("                return false;")
a("            }")
a("")
a("            MobLootDefinition mob;")
a("            if (!ByExactName.TryGetValue(enemyName.Trim(), out mob) || mob == null")
a("                || mob.Snapshots == null || mob.Snapshots.Length == 0)")
a("            {")
a("                return false;")
a("            }")
a("")
a("            // Prefer a non-zero observed credit sample for empty-corpse guard.")
a("            for (int i = 0; i < mob.Snapshots.Length; i++)")
a("            {")
a("                if (mob.Snapshots[i].Credits > 0)")
a("                {")
a("                    credits = mob.Snapshots[i].Credits;")
a("                    return true;")
a("                }")
a("            }")
a("")
a("            credits = mob.Snapshots[0].Credits;")
a("            return true;")
a("        }")
a("")
a("        private static ObservedCorpseSnapshotDefinition Snapshot(")
a("            string key,")
a("            int credits,")
a("            params LootEntryDefinition[] entries)")
a("        {")
a("            return new ObservedCorpseSnapshotDefinition")
a("            {")
a("                SnapshotKey = key,")
a("                Credits = credits,")
a("                Entries = entries ?? new LootEntryDefinition[0],")
a("                Evidence = LootEvidenceConfidence.ProvenCapture,")
a("                SelectionProbabilityEvidence = LootEvidenceConfidence.Unresolved,")
a("                EvidenceReference = Evidence + \"; \" + key")
a("            };")
a("        }")
a("")
a("        private static LootEntryDefinition Entry(")
a("            string snapshotKey,")
a("            int lowItemId,")
a("            int highItemId,")
a("            int quality,")
a("            int quantity)")
a("        {")
a("            return new LootEntryDefinition")
a("            {")
a("                SelectionKey = snapshotKey,")
a("                ItemTemplateId = lowItemId,")
a("                HighItemTemplateId = highItemId,")
a("                FixedQuality = quality,")
a("                MinimumQuality = quality,")
a("                MaximumQuality = quality,")
a("                MinimumQuantity = quantity,")
a("                MaximumQuantity = quantity,")
a("                Weight = 0,")
a("                DropChanceBasisPoints = 0,")
a("                UniquePerCorpse = true,")
a("                Semantics = LootSemantics.ObservedAvailable,")
a("                Evidence = LootEvidenceConfidence.ObservedAvailableLoot,")
a("                EvidenceReference = Evidence + \"; \" + snapshotKey,")
a("                ProbabilityEvidence = \"unresolved\"")
a("            };")
a("        }")
a("    }")
a("}")
a("")

OUT.write_text("\n".join(lines), encoding="utf-8")
print("Wrote", OUT)
print("mobs", len(by_name))
for n, snaps in sorted(by_name.items(), key=lambda x: -len(x[1])):
    empties = sum(1 for s in snaps if not s[1])
    print(" ", n, "n=", len(snaps), "empty=", empties, "md=", md_by_name[n])
