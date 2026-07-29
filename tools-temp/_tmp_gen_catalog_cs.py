import re, subprocess, os

PASSAGE_DEST_PF = {
    "Passage to Old Frontier": 4310, "Passage to Frontier Bridge": 4310, "Passage to Frontier Outskirts": 4310,
    "Passage to Misty Dreams Border": 4312, "Passage to The Core": 4312, "Passage to Mil": 4311,
    "Passage to Brawl": 4311, "Passage to Frontier Border": 4311, "Passage to Nascence Wilds": 4311,
    "Passage to The Wetlands": 4311, "Passage to Silence": 4311, "Passage to Steppe of Dispair": 4312,
    "Passage to Two Mountains": 4312,
    "Passage to The Scoop": 4542, "Passage to Barter": 4543, "Passage to Enig": 4541,
    "Passage to Cape Callous": 4544, "Passage to The Fallen Forest": 4540, "Passage to The Sink": 4541,
    "Passage to Archbile": 4543, "Passage to Utopolis": 4542, "Passage to Enclave": 4542,
    "Passage to Chronos Canyon": 4540, "Passage to Corona": 4544, "Passage to Domeview": 4544,
    "Passage to Eastfang": 4541, "Passage to Tinker Tower": 4543, "Passage to The Divide": 4540,
    "Passage to Shell Beach": 4540, "Passage to Remnans": 4540, "Passage to Ripwell": 4542,
    "Passage to Whispervale": 4540, "Passage to The Outer Isles": 4544, "Passage to Acme": 4544,
    "Passage to Cold Rock": 4544, "Passage to Nero": 4542, "Passage to Sabre's Cradle": 4543,
    "Passage to Shunpike": 4543, "Passage to Spade": 4541, "Passage to Stormshelter": 4542,
    "Passage to Time's Tide": 4540, "Passage to The Jagged Coast": 4544, "Passage to Godstrand Cliffs": 4541,
    "Passage to Monopolis": 4543, "Passage to Shattered Heartlands": 4541,
    "Passage to Cutching Light": 4880, "Passage to Giant's Hoof": 4880, "Passage to Mirador": 4880,
    "Passage to The Court": 4880, "Passage to The Highlands": 4881, "Passage to Halls of Scheol": 4881,
    "Passage to Eastern Brink": 4881, "Passage to Marble Orchards": 4881, "Passage to The Temple Bog": 4881,
    "Passage to The Twilight Basin": 4881, "Passage to The Approach": 4881, "Passage to Necropolis": 4881,
    "Passage to The Bastion": 4881,
    "Passage to City South": 4872, "Passage to City North": 4872, "Passage to Lament Lagoon": 4872,
    "Passage to The Outmost Yard": 4872, "Passage to Watcher's Ocular": 4872, "Passage to The Pool": 4872,
    "Passage to Coral Raft": 4873, "Passage to Dead Ends": 4873, "Passage to Piercing Tundra": 4873,
    "Passage to Blue Mist": 4320, "Passage to Yutto Wasteland": 4320, "Passage to The Ravine": 4320,
    "Passage to Penumbra Fortress": 4321, "Passage to The Pipe": 4321, "Passage to Glacier Hill": 4321,
    "Passage to Path to Fire": 4322, "Passage to Purity": 4321, "Passage to White Citadel": 4321,
    "Passage to Path to fire": 4322, "Passage to Misty Marshes": 4322, "Passage to Dark Hill": 4322,
    "Passage to Razor's Lair": 4322,
    "Passage to Inferno Frontier": 4328,
    "Passage to Sorrow": 4605, "Passage to Yutto Marshes": 4605, "Passage to Dark Marshes": 4605,
    "Passage to Inferno Barracks": 4605, "Passage to Sorrow Outlook": 4605, "Passage to Oasis": 4605,
    "Passage to Xark's Lair": 4605,
}

PASSAGE_ARRIVAL = {
    "Passage to Old Frontier": (4310, 858.0, 31.52, 1479.0),
    "Passage to Frontier Bridge": (4310, 792.0, 31.81, 1149.0),
    "Passage to Frontier Outskirts": (4310, 684.0, 29.41, 1898.0),
    "Passage to Misty Dreams Border": (4312, 1544.0, 52.83, 680.0),
    "Passage to The Core": (4312, 1630.0, 43.96, 1469.0),
    "Passage to Mil": (4311, 274.0, 77.716, 1665.0),
    "Passage to Brawl": (4311, 242.0, 105.01, 1035.0),
    "Passage to Frontier Border": (4311, 608.0, 13.81, 556.0),
    "Passage to Inferno Frontier": (4328, 239.474, 38.445, 58.7122),
    "Passage to Path to fire": (4322, 1070.83, 83.1564, 307.288),
    "Passage to Path to Fire": (4322, 1070.83, 83.1564, 307.288),
    "Passage to Misty Marshes": (4322, 1070.83, 83.1564, 307.288),
    "Passage to Dark Hill": (4322, 1070.83, 83.1564, 307.288),
    "Passage to Razor's Lair": (4322, 1070.83, 83.1564, 307.288),
}

RETURN_STATUE_TEMPLATES = {
    4310: 222955, 4311: 222955, 4312: 222955, 4313: 222955,
    4540: 223577, 4541: 223577, 4542: 223577, 4543: 223577, 4544: 223577,
    4880: 223578, 4881: 223578, 4872: 223589, 4873: 223589,
    4320: 224017, 4321: 224017, 4322: 223982, 4605: 223981, 4328: 227466,
}

GARDEN_RETURN = {
    4676: (345.121, 119.989, 381.021), 4677: (456.680, 40.152, 424.161),
    4678: (347.838, 116.446, 387.168), 4679: (347.609, 119.202, 397.899),
    4680: (464.280, 40.070, 409.273), 4681: (461.489, 40.143, 413.082),
    4682: (376.287, 119.190, 395.672), 4683: (491.036, 40.065, 410.499),
    4684: (367.756, 119.844, 424.454), 4685: (370.495, 119.841, 416.171),
    4686: (494.782, 40.100, 411.140), 4687: (494.910, 40.067, 411.759),
    4688: (372.971, 119.513, 391.241), 4689: (373.276, 119.572, 392.195),
    4690: (494.736, 40.058, 411.072), 4691: (496.894, 40.070, 410.004),
    4692: (372.929, 119.577, 392.849), 4693: (369.599, 119.404, 394.313),
    4694: (495.774, 40.053, 410.609), 4695: (496.284, 40.064, 411.012),
    4696: (379.017, 119.837, 396.285), 4697: (495.768, 40.069, 409.867),
    4698: (379.017, 119.837, 396.285), 4699: (495.768, 40.069, 409.867),
}

def query(sql):
    proc = subprocess.run(
        [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "cellao_codex_test", "-N", "-B", "-e", sql],
        capture_output=True, text=True, check=True)
    return [line.split("\t") for line in proc.stdout.splitlines() if line.strip()]

def decode_template(hexstats):
    for m in re.findall(r"CE00(0[0-9A-F]{5})", hexstats.upper()):
        val = int(m, 16)
        if val >= 200000:
            return val
    return None

zone_pfs = sorted(set(PASSAGE_DEST_PF.values()) | set(RETURN_STATUE_TEMPLATES.keys()))
zone_statues = {}
for pf, x, y, z, hexstats in query(
    f"SELECT Playfield, X, Y, Z, HEX(stats) FROM staticdynels WHERE Playfield IN ({','.join(map(str, zone_pfs))})"
):
    t = decode_template(hexstats)
    if t:
        zone_statues.setdefault(int(pf), []).append((t, float(x), float(y), float(z)))

routes = []
missing = []
for name, dest_pf in sorted(PASSAGE_DEST_PF.items()):
    if name in PASSAGE_ARRIVAL:
        pf, x, y, z = PASSAGE_ARRIVAL[name]
    else:
        preferred = RETURN_STATUE_TEMPLATES.get(dest_pf)
        pick = None
        for t, x, y, z in zone_statues.get(dest_pf, []):
            if preferred and t == preferred:
                pick = (dest_pf, x, y, z)
                break
        if not pick and zone_statues.get(dest_pf):
            t, x, y, z = zone_statues[dest_pf][0]
            pick = (dest_pf, x, y, z)
        if not pick:
            missing.append(name)
            continue
        pf, x, y, z = pick
    routes.append((name, pf, x, y, z))

route_lines = []
for name, pf, x, y, z in routes:
    esc = name.replace('"', '\\"')
    route_lines.append(
        f'                       {{ "{esc}", new NascenceGardenPassageRoute({pf}, {x}f, {y}f, {z}f, "cellao_codex_test") }},'
    )

garden_lines = []
for pf, (x, y, z) in sorted(GARDEN_RETURN.items()):
    garden_lines.append(f"                {{ {pf}, new GardenReturnPosition({x}f, {y}f, {z}f) }},")

RETURN_PAIRS = [
    (222955, 214789), (222954, 214788),
    (223577, 214782), (244831, 214782),
    (223559, 214781), (244830, 214781),
    (223578, 214855), (245044, 214855), (264069, 214855),
    (223565, 214840), (245043, 214840), (264070, 214840),
    (223589, 214881), (245046, 214881),
    (223574, 214880), (245045, 214880),
    (224017, 224049), (245048, 224049),
    (223982, 224052), (245047, 224052),
    (223981, 224050), (245050, 224050),
    (224018, 224051), (245049, 224051),
    (227466, 231154), (227466, 231155),
]

pair_lines = []
for statue, insignia in RETURN_PAIRS:
    pair_lines.append(f"                new ShadowlandsReturnKeyPair({statue}, {insignia}),")

ZONE_PF_LINES = ", ".join(str(p) for p in zone_pfs)

cs = f'''namespace ZoneEngine.Core.MessageHandlers
{{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    #endregion

    public static class NascenceStatueTeleportCatalog
    {{
        public const int ThrakInsigniaTemplateId = 214789;

        private const int ShadowlandsGardenPlayfieldMin = 4676;

        private const int ShadowlandsGardenPlayfieldMax = 4699;

        private static readonly int[] ShadowlandsZonePlayfieldIds =
            new[]
            {{
                {ZONE_PF_LINES}
            }};

        private static readonly ShadowlandsReturnKeyPair[] ReturnKeyPairs = BuildReturnKeyPairs();

        private static readonly Dictionary<int, GardenReturnPosition> GardenReturnPositionsByPlayfieldId =
            BuildGardenReturnPositionsByPlayfieldId();

        private static readonly Dictionary<string, NascenceGardenPassageRoute> GardenPassageRoutesByName =
            BuildGardenPassageRoutesByName();

        public static bool IsShadowlandsGardenPlayfield(int playfieldId)
        {{
            return playfieldId >= ShadowlandsGardenPlayfieldMin
                   && playfieldId <= ShadowlandsGardenPlayfieldMax;
        }}

        public static bool IsShadowlandsZonePlayfield(int playfieldId)
        {{
            for (int i = 0; i < ShadowlandsZonePlayfieldIds.Length; i++)
            {{
                if (ShadowlandsZonePlayfieldIds[i] == playfieldId)
                {{
                    return true;
                }}
            }}

            return false;
        }}

        public static bool TryGetGardenPassageRouteByName(
            string passageName,
            out NascenceGardenPassageRoute route)
        {{
            route = null;
            if (string.IsNullOrEmpty(passageName))
            {{
                return false;
            }}

            NascenceGardenPassageRoute exactRoute;
            if (GardenPassageRoutesByName.TryGetValue(passageName.Trim(), out exactRoute))
            {{
                route = exactRoute;
                return true;
            }}

            foreach (KeyValuePair<string, NascenceGardenPassageRoute> entry in GardenPassageRoutesByName)
            {{
                if (passageName.IndexOf(entry.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {{
                    route = entry.Value;
                    return true;
                }}
            }}

            return false;
        }}

        public static bool TryMatchReturnKey(int statueTemplateId, int insigniaTemplateId)
        {{
            for (int i = 0; i < ReturnKeyPairs.Length; i++)
            {{
                ShadowlandsReturnKeyPair pair = ReturnKeyPairs[i];
                if (pair.StatueTemplateId == statueTemplateId
                    && pair.InsigniaTemplateId == insigniaTemplateId)
                {{
                    return true;
                }}
            }}

            return false;
        }}

        public static bool IsZoneReturnStatueTemplate(int templateId)
        {{
            for (int i = 0; i < ReturnKeyPairs.Length; i++)
            {{
                if (ReturnKeyPairs[i].StatueTemplateId == templateId)
                {{
                    return true;
                }}
            }}

            return false;
        }}

        public static int ResolveReturnGardenPlayfieldId(int sourceZonePlayfieldId, int otUnredeemedValue)
        {{
            bool unredeemed = otUnredeemedValue != 0;

            if (sourceZonePlayfieldId >= 4310 && sourceZonePlayfieldId <= 4313)
            {{
                return unredeemed ? 4677 : 4676;
            }}

            if (sourceZonePlayfieldId >= 4540 && sourceZonePlayfieldId <= 4544)
            {{
                return unredeemed ? 4680 : 4678;
            }}

            if (sourceZonePlayfieldId == 4880 || sourceZonePlayfieldId == 4881)
            {{
                return unredeemed ? 4683 : 4682;
            }}

            if (sourceZonePlayfieldId == 4872 || sourceZonePlayfieldId == 4873)
            {{
                return unredeemed ? 4686 : 4684;
            }}

            if (sourceZonePlayfieldId >= 4320 && sourceZonePlayfieldId <= 4322)
            {{
                return unredeemed ? 4690 : 4688;
            }}

            if (sourceZonePlayfieldId == 4605)
            {{
                return unredeemed ? 4694 : 4692;
            }}

            if (sourceZonePlayfieldId == 4328)
            {{
                return unredeemed ? 4697 : 4696;
            }}

            return unredeemed ? 4677 : 4676;
        }}

        public static void ResolveReturnGardenPosition(
            int gardenPlayfieldId,
            out float x,
            out float y,
            out float z)
        {{
            GardenReturnPosition position;
            if (GardenReturnPositionsByPlayfieldId.TryGetValue(gardenPlayfieldId, out position))
            {{
                x = position.X;
                y = position.Y;
                z = position.Z;
                return;
            }}

            x = 456.680f;
            y = 40.152f;
            z = 424.161f;
        }}

        private static ShadowlandsReturnKeyPair[] BuildReturnKeyPairs()
        {{
            return new ShadowlandsReturnKeyPair[]
                   {{
{chr(10).join(pair_lines)}
                   }};
        }}

        private static Dictionary<int, GardenReturnPosition> BuildGardenReturnPositionsByPlayfieldId()
        {{
            return new Dictionary<int, GardenReturnPosition>
                   {{
{chr(10).join(garden_lines)}
                   }};
        }}

        private static Dictionary<string, NascenceGardenPassageRoute> BuildGardenPassageRoutesByName()
        {{
            return new Dictionary<string, NascenceGardenPassageRoute>(StringComparer.OrdinalIgnoreCase)
                   {{
{chr(10).join(route_lines)}
                   }};
        }}
    }}

    public sealed class NascenceGardenPassageRoute
    {{
        public NascenceGardenPassageRoute(
            int destinationPlayfieldId,
            float destinationX,
            float destinationY,
            float destinationZ,
            string evidence)
        {{
            this.DestinationPlayfieldId = destinationPlayfieldId;
            this.DestinationX = destinationX;
            this.DestinationY = destinationY;
            this.DestinationZ = destinationZ;
            this.Evidence = evidence;
        }}

        public int DestinationPlayfieldId {{ get; private set; }}

        public float DestinationX {{ get; private set; }}

        public float DestinationY {{ get; private set; }}

        public float DestinationZ {{ get; private set; }}

        public string Evidence {{ get; private set; }}
    }}

    public sealed class GardenReturnPosition
    {{
        public GardenReturnPosition(float x, float y, float z)
        {{
            this.X = x;
            this.Y = y;
            this.Z = z;
        }}

        public float X {{ get; private set; }}

        public float Y {{ get; private set; }}

        public float Z {{ get; private set; }}
    }}

    public sealed class ShadowlandsReturnKeyPair
    {{
        public ShadowlandsReturnKeyPair(int statueTemplateId, int insigniaTemplateId)
        {{
            this.StatueTemplateId = statueTemplateId;
            this.InsigniaTemplateId = insigniaTemplateId;
        }}

        public int StatueTemplateId {{ get; private set; }}

        public int InsigniaTemplateId {{ get; private set; }}
    }}
}}
'''

out_path = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\MessageHandlers\NascenceStatueTeleportCatalog.cs"
with open(out_path, "w", newline="\r\n") as f:
    f.write(cs)

print("routes", len(routes), "missing", missing)
print("wrote", out_path)
