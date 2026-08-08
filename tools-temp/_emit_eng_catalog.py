# Emit PetEngineerSummonCatalog.cs from capture map.
import re
from pathlib import Path

SHELL_PET_HASH = {
    96196: "PT10", 150789: "PT10", 150790: "PT10",
    150786: "PT11", 150787: "PT11", 150788: "PT11",
    150794: "PT12", 150795: "PT12", 150796: "PT12", 150797: "PT12",
    150791: "PT13", 150792: "PT13", 150793: "PT13", 96228: "PT13",
    150782: "PT14", 150783: "PT14", 150784: "PT14", 150785: "PT14", 96215: "PT14",
    150777: "PT15", 150778: "PT15", 150779: "PT15", 150780: "PT15", 150781: "PT15",
    150775: "PT19", 150776: "PT19",
    96218: "PT20",
}

pat = re.compile(
    r"^(\d+)\s+\|\s+(\d+)/(\d+)\s+QL(\d+)\s+\|\s+(.+?)\s+\|\s+(\d+)\s+\|\s+(\d+)\s+\|\s+(\d+)\s+\|\s+(\d+)\s+\|\s+(\d+)\s*$"
)
rows = []
for line in Path("tools-temp/_eng_shell_pet_map_clean.txt").read_text(encoding="utf-8").splitlines():
    m = pat.match(line.strip())
    if not m:
        continue
    nano, low, high, ql, name, lvl, hp, md, scale, run = m.groups()
    nano, low, high, ql = map(int, (nano, low, high, ql))
    lvl, hp, md, scale, run = map(int, (lvl, hp, md, scale, run))
    pet_hash = SHELL_PET_HASH.get(low) or SHELL_PET_HASH.get(high)
    if not pet_hash:
        raise SystemExit(f"missing hash for {low}/{high}")
    rows.append((nano, pet_hash, lvl, low, high, ql, name, hp, md, scale, run))
rows.sort()

def emit_dict(rows, fmt):
    return "\n".join("                " + fmt(r) for r in rows)

hash_entries = emit_dict(rows, lambda r: f"{{ {r[0]}, \"{r[1]}\" }},")
type_entries = emit_dict(rows, lambda r: f"{{ {r[0]}, {r[2]} }},")
shell_entries = emit_dict(
    rows,
    lambda r: f"{{ {r[0]}, new CapturedBureaucratShellDisplay({r[3]}, {r[4]}, {r[5]}) }},",
)
profile_entries = emit_dict(
    rows,
    lambda r: (
        f"{{ {r[0]}, new CapturedBureaucratPetProfile("
        f"\"{r[6]}\", {r[2]}, {r[7]}, {r[8]}, {r[9]}, {r[10]}, npcFamily: 95) }},"
    ),
)

cs = f'''#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    #endregion

    /// <summary>
    /// Capture 20260808-131854 (Engnera): Engineer pet nanos SpawnItem a shell; shell OnUse SummonPet.
    /// Nanos have no SummonPet function — catalog drives shell grant + pet spawn.
    /// </summary>
    internal static class PetEngineerSummonCatalog
    {{
        private static readonly Dictionary<int, string> PreferredPetHashByNano =
            new Dictionary<int, string>
            {{
{hash_entries}
            }};

        private static readonly Dictionary<int, int> PreferredPetTypeByNano =
            new Dictionary<int, int>
            {{
{type_entries}
            }};

        private static readonly Dictionary<int, CapturedBureaucratShellDisplay> ShellDisplayByNano =
            new Dictionary<int, CapturedBureaucratShellDisplay>
            {{
{shell_entries}
            }};

        private static readonly Dictionary<int, CapturedBureaucratPetProfile> ProfilesByNano =
            new Dictionary<int, CapturedBureaucratPetProfile>
            {{
{profile_entries}
            }};

        private static readonly Dictionary<string, int> NanoByShellDisplay =
            BuildNanoByShellDisplay();

        private static readonly HashSet<int> ShellItemLowIds = BuildShellItemLowIds();

        public static bool IsEngineerSummonNano(int nanoId)
        {{
            return PreferredPetHashByNano.ContainsKey(nanoId);
        }}

        public static bool TryGetPreferredPetHash(int nanoId, out string petHash)
        {{
            return PreferredPetHashByNano.TryGetValue(nanoId, out petHash);
        }}

        public static int ResolvePreferredPetType(int nanoId)
        {{
            int petTypeId;
            return PreferredPetTypeByNano.TryGetValue(nanoId, out petTypeId) ? petTypeId : 1;
        }}

        public static bool TryGetShellDisplay(int nanoId, out CapturedBureaucratShellDisplay shellDisplay)
        {{
            return ShellDisplayByNano.TryGetValue(nanoId, out shellDisplay);
        }}

        public static bool TryGetProfile(int nanoId, out CapturedBureaucratPetProfile profile)
        {{
            return ProfilesByNano.TryGetValue(nanoId, out profile);
        }}

        public static bool IsShellItemLowId(int lowId)
        {{
            return ShellItemLowIds.Contains(lowId);
        }}

        public static bool TryResolveShellNano(
            int shellItemLowId,
            int shellItemHighId,
            int shellQuality,
            out int nanoId)
        {{
            return NanoByShellDisplay.TryGetValue(
                BuildShellDisplayKey(shellItemLowId, shellItemHighId, shellQuality),
                out nanoId);
        }}

        public static bool TryResolveShellSummonParams(int nanoId, out PetSummonParams summonParams)
        {{
            summonParams = null;
            string petHash;
            if (!PreferredPetHashByNano.TryGetValue(nanoId, out petHash))
            {{
                return false;
            }}

            summonParams = new PetSummonParams
            {{
                NanoId = nanoId,
                PetHash = petHash,
                PetTypeId = ResolvePreferredPetType(nanoId),
            }};
            return true;
        }}

        private static Dictionary<string, int> BuildNanoByShellDisplay()
        {{
            var reverseLookup = new Dictionary<string, int>();
            foreach (KeyValuePair<int, CapturedBureaucratShellDisplay> entry in ShellDisplayByNano)
            {{
                reverseLookup[BuildShellDisplayKey(
                    entry.Value.DisplayItemLowId,
                    entry.Value.DisplayItemHighId,
                    entry.Value.DisplayQuality)] = entry.Key;
            }}

            return reverseLookup;
        }}

        private static HashSet<int> BuildShellItemLowIds()
        {{
            var lowIds = new HashSet<int>();
            foreach (CapturedBureaucratShellDisplay shellDisplay in ShellDisplayByNano.Values)
            {{
                lowIds.Add(shellDisplay.DisplayItemLowId);
            }}

            return lowIds;
        }}

        private static string BuildShellDisplayKey(int lowId, int highId, int quality)
        {{
            return string.Format("{{0}}:{{1}}:{{2}}", lowId, highId, quality);
        }}
    }}
}}
'''

out = Path("AORebirth/Server/ZoneEngine/Core/PetEngineerSummonCatalog.cs")
out.write_text(cs, encoding="utf-8")
print(f"wrote {out} ({len(rows)} nanos)")
