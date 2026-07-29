# Generate NascenceLifeSpawn.cs from Nascence Life captures (4310–4312).
import csv
import os
from collections import Counter

out = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Playfields\NascenceLifeSpawn.cs"

# (capture_dir, capture_folder, allowed_playfield_ids)
captures = [
    (
        r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-170408",
        "20260718-170408",
        {4310},
    ),
    (
        r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-173204",
        "20260718-173204",
        {4311},
    ),
    (
        r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-174130",
        "20260718-174130",
        {4311},
    ),
    (
        r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-180726",
        "20260718-180726",
        {4312},
    ),
]

rows = []
seen = set()
skipped_hecklers_4312 = 0
for cap_dir, cap_folder, allowed_pfs in captures:
    src = os.path.join(cap_dir, "scfu-appearance.csv")
    with open(src, newline="", encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            pf = int(row.get("PlayfieldId") or "0")
            if pf not in allowed_pfs:
                continue
            name = (row.get("Name") or "").strip()
            if not name or name == "Cratonera":
                continue
            # PF 4312 Hecklers stay in NascenceCoreHecklerSpawnOrchestrator.
            if pf == 4312 and name.startswith("Heckler of"):
                skipped_hecklers_4312 += 1
                continue
            md = int(row.get("MonsterData") or "0")
            if md <= 0:
                continue
            ident = row.get("Identity") or ""
            if ident in seen:
                continue
            seen.add(ident)
            row["_CaptureFolder"] = cap_folder
            row["_PlayfieldId"] = pf
            rows.append(row)

rows.sort(key=lambda r: (r["_PlayfieldId"], r.get("Name") or "", r.get("Identity") or ""))


def fnum(s, d="0"):
    return s if s not in (None, "") else d


def parse_tex(s):
    out = []
    for part in (s or "").split("|"):
        if not part:
            continue
        f = part.split(":")
        place, tex = int(f[0]), int(f[1])
        if tex > 0:
            out.append((place, tex))
    return out


def parse_mesh(s):
    out = []
    for part in (s or "").split("|"):
        if not part:
            continue
        f = part.split(":")
        out.append((int(f[0]), int(f[1]), int(f[2]), int(f[3])))
    return out


def csharp_tex(texs):
    if not texs:
        return "null"
    parts = ", ".join("new[] {{ {0}, {1} }}".format(a, b) for a, b in texs)
    return "new[] {{ {0} }}".format(parts)


def csharp_mesh(meshes):
    if not meshes:
        return "null"
    parts = ", ".join(
        "new[] {{ {0}, {1}, {2}, {3} }}".format(a, b, c, d) for a, b, c, d in meshes
    )
    return "new[] {{ {0} }}".format(parts)


blocks = []
for row in rows:
    name = row["Name"].replace("\\", "\\\\").replace('"', '\\"')
    texs = parse_tex(row.get("Textures"))
    meshes = parse_mesh(row.get("Meshes"))
    head = int(fnum(row.get("HeadMesh"), "0"))
    pf = row["_PlayfieldId"]
    cap = row["_CaptureFolder"]
    blocks.append(
        """            new LifeNpc
            {{
                PlayfieldId = {pf},
                Name = "{name}",
                Level = {level}, Health = {health}, MonsterData = {md}, Scale = {scale}, VisualFlags = {vf}, HeadMesh = {head},
                X = {x}f, Y = {y}f, Z = {z}f,
                Hx = {hx}f, Hy = {hy}f, Hz = {hz}f, Hw = {hw}f,
                Textures = {tex},
                Meshes = {mesh},
                CaptureFolder = "{cap}",
            }}""".format(
            pf=pf,
            name=name,
            level=int(fnum(row.get("Level"), "1")),
            health=int(fnum(row.get("Health"), "1")),
            md=int(fnum(row.get("MonsterData"), "0")),
            scale=int(fnum(row.get("MonsterScale"), "100")),
            vf=int(fnum(row.get("VisualFlags"), "31")),
            head=head,
            x=fnum(row.get("PositionX")),
            y=fnum(row.get("PositionY")),
            z=fnum(row.get("PositionZ")),
            hx=fnum(row.get("HeadingX")),
            hy=fnum(row.get("HeadingY")),
            hz=fnum(row.get("HeadingZ")),
            hw=fnum(row.get("HeadingW"), "1"),
            tex=csharp_tex(texs),
            mesh=csharp_mesh(meshes),
            cap=cap,
        )
    )

body = ",\n".join(blocks)

by_pf = Counter(r["_PlayfieldId"] for r in rows)
counts_4311 = Counter(r["Name"] for r in rows if r["_PlayfieldId"] == 4311)
counts_4312 = Counter(r["Name"] for r in rows if r["_PlayfieldId"] == 4312)
by_cap = Counter(r["_CaptureFolder"] for r in rows)

header = """namespace AORebirth.Core.Playfields
{{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields.Content;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture-backed Nascence Life outdoor mob/NPC population (PF 4310–4313).
    /// Captures: 20260718-170408 (4310 Frontier), 20260718-173204 (4311 Crippler cave),
    /// 20260718-174130 (4311 Two Mountains), 20260718-180726 (4312 East / Core; Hecklers excluded).
    /// Total {count} NPCs (4310={n4310}, 4311={n4311}, 4312={n4312}).
    /// PF 4312 Hecklers remain in NascenceCoreHecklerSpawnOrchestrator.
    /// </summary>
    internal static class NascenceLifeSpawn
    {{
        private const string TemplateHash = "BART";

        private sealed class LifeNpc
        {{
            public int PlayfieldId;
            public string Name;
            public int Level;
            public int Health;
            public int MonsterData;
            public int Scale;
            public int VisualFlags;
            public int HeadMesh;
            public float X;
            public float Y;
            public float Z;
            public float Hx;
            public float Hy;
            public float Hz;
            public float Hw;
            public int[][] Textures;
            public int[][] Meshes;
            public string CaptureFolder;
        }}

        private static readonly LifeNpc[] Npcs =
        {{
{body}
        }};

        public static void SpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {{
            if (playfield == null || activateNpc == null)
            {{
                return;
            }}

            int pf = playfieldIdentity.Instance;
            if (pf != NascenceLifeContentModule.FrontierPlayfieldId
                && pf != NascenceLifeContentModule.WildsPlayfieldId
                && pf != NascenceLifeContentModule.CorePlayfieldId
                && pf != NascenceLifeContentModule.Nascence4313PlayfieldId)
            {{
                return;
            }}

            int spawned = 0;
            for (int i = 0; i < Npcs.Length; i++)
            {{
                LifeNpc def = Npcs[i];
                if (def.PlayfieldId != pf)
                {{
                    continue;
                }}

                if (SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                {{
                    spawned++;
                }}
            }}

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceLifeSpawn pf=" + pf + " spawned=" + spawned + "/" + Npcs.Length);
        }}

        private static bool SpawnOne(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            LifeNpc def)
        {{
            var npcController = new NPCController();
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                TemplateHash,
                playfieldIdentity,
                new Coordinate {{ x = def.X, y = def.Y, z = def.Z }},
                new Quaternion(def.Hx, def.Hy, def.Hz, def.Hw),
                npcController,
                def.Level);

            if (mob == null)
            {{
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NascenceLifeSpawn FAILED template=" + TemplateHash + " npc=" + def.Name);
                return false;
            }}

            mob.Name = def.Name;
            mob.Playfield = playfield;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)def.MonsterData);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)def.Level);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, (uint)def.VisualFlags);
            if (def.Scale > 0)
            {{
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)def.Scale);
            }}

            if (def.HeadMesh > 0)
            {{
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, (uint)def.HeadMesh);
            }}

            if (def.Textures != null && def.Textures.Length > 0)
            {{
                mob.Textures.Clear();
                foreach (int[] t in def.Textures)
                {{
                    if (t == null || t.Length < 2 || t[1] <= 0)
                    {{
                        continue;
                    }}

                    mob.Textures.Add(new AOTextures(t[0], t[1]));
                }}
            }}

            if (def.Meshes != null && def.Meshes.Length > 0)
            {{
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                foreach (int[] m in def.Meshes)
                {{
                    if (m == null || m.Length < 4 || m[1] <= 0)
                    {{
                        continue;
                    }}

                    mob.MeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                    mob.SocialMeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                }}
            }}

            activateNpc(mob);
            return true;
        }}
    }}
}}
""".format(
    count=len(rows),
    n4310=by_pf.get(4310, 0),
    n4311=by_pf.get(4311, 0),
    n4312=by_pf.get(4312, 0),
    body=body,
)

with open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write(header)

print(
    "wrote",
    out,
    "total",
    len(rows),
    "by_pf",
    dict(by_pf),
    "by_cap",
    dict(by_cap),
    "skipped_hecklers_4312",
    skipped_hecklers_4312,
)
print("4312 species:")
for name, n in sorted(counts_4312.items()):
    print(" {:3d}  {}".format(n, name))
