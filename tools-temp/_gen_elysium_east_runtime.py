# Generate ElysiumEastMobRuntime.cs from Elysium captures (PF 4540 South + 4543 East).
# Captures: 182451, 190145 (Heckler densify), 193914, 201436 (more South densify).
import binascii
import csv
import os
import re
import struct
from collections import OrderedDict

caps = [
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-182451",
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-190145",
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-193914",
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-201436",
]
allowed_pfs = {"4540", "4543"}
# Player pets / engineer automata that show up in SCFU without a reliable Owner field.
skip_name_substrings = (
    "Slayerdroid",
    "Slaydroid",
)
out_cs = (
    r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine"
    r"\Core\Playfields\ElysiumEastMobRuntime.cs"
)

by_id = OrderedDict()
for cap in caps:
    with open(os.path.join(cap, "scfu-appearance.csv"), encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            if row.get("CharacterInfoType") != "NPCInfo":
                continue
            pf = str(row.get("PlayfieldId") or "")
            if pf not in allowed_pfs:
                continue
            # Real pets have Owner; ExtTex wildlife often carries IsPet wire flag.
            owner = (row.get("Owner") or "").strip()
            if owner and owner not in ("0", "(None)", "None"):
                continue
            name = (row.get("Name") or "").strip()
            if not name:
                continue
            if any(s.lower() in name.lower() for s in skip_name_substrings):
                continue
            by_id[row["Identity"]] = row


def parse_overrides(texov):
    if not texov:
        return []
    out = []
    for p in texov.split("|"):
        m = re.search(r"^(.*):(\d+):(\d+):(\d+)$", p)
        if not m:
            continue
        name = m.group(1).split("\x00")[0].replace("\x00", "")
        out.append((name, int(m.group(2)), int(m.group(3)), int(m.group(4))))
    return out


def extract_exttex_from_raw(r):
    raw = r.get("RawBodyHex") or ""
    unk1_hex = r.get("ScfuUnknown1Hex") or ""
    if not raw or not unk1_hex:
        return None
    body = binascii.unhexlify(raw)
    unk1 = binascii.unhexlify(unk1_hex)
    ui = body.find(unk1)
    if ui < 0:
        return None
    after = body[ui + len(unk1) :]
    try:
        run = int(float(r.get("RunSpeedBase") or 0))
    except Exception:
        run = 0
    if run > 255:
        if len(after) >= 2 and struct.unpack(">H", after[:2])[0] == run:
            after = after[2:]
        elif len(after) >= 2 and struct.unpack("<H", after[:2])[0] == run:
            after = after[2:]
    elif after and after[0] == run:
        after = after[1:]

    marker_07e2 = bytes([0, 0, 7, 0xE2])
    marker_0bd3 = bytes([0, 0, 0x0B, 0xD3])
    marker_0fc4 = bytes([0, 0, 0x0F, 0xC4])
    if after.startswith(marker_07e2):
        p = 0
        while p + 48 <= len(after) and after[p : p + 4] == marker_07e2:
            p += 48
        return after[:p]
    if after.startswith(marker_0bd3) or after.startswith(marker_0fc4):
        # 0BD3/0FC4 header once, then name32 + tex4 + unk4 + flag4 entries.
        p = 4
        while p + 44 <= len(after):
            nxt = after[p : p + 4]
            if nxt in (
                marker_0bd3,
                marker_0fc4,
                marker_07e2,
                bytes([0, 0, 3, 0xF1]),
                bytes([0, 0, 0x17, 0xA6]),
            ):
                break
            if after[p] == 0:
                break
            p += 44
        return after[:p]
    return None


def parse_mesh_list(s):
    if not s:
        return []
    out = []
    for p in s.split("|"):
        bits = p.split(":")
        if len(bits) != 4:
            continue
        out.append(tuple(int(x) for x in bits))
    return out


def parse_tex_list(s):
    if not s:
        return []
    out = []
    for p in s.split("|"):
        bits = p.split(":")
        if len(bits) != 3:
            continue
        out.append(tuple(int(x) for x in bits))
    if out and all(t[1] == 0 for t in out):
        return []
    return out


def sid(name):
    s = re.sub(r"[^A-Za-z0-9]+", "_", name).strip("_")
    if not s or s[0].isdigit():
        s = "M_" + s
    return s


def map_side(side_str):
    s = (side_str or "").strip()
    if s in ("Omni", "OmniTek"):
        return 2  # Side.Omni
    if s == "Clan":
        return 1
    if s == "Neutral":
        return 0
    return 3  # Monster


slots = []
by_name_ex = {}
for ident, r in by_id.items():
    name = (r.get("Name") or "").strip()
    if not name:
        continue
    try:
        pf = int(float(r.get("PlayfieldId") or 0))
        x = float(r["PositionX"])
        y = float(r["PositionY"])
        z = float(r["PositionZ"])
        level = int(float(r.get("Level") or 1))
        md = int(float(r.get("MonsterData") or 0))
        scale = int(float(r.get("MonsterScale") or 100))
        health = int(float(r.get("Health") or 100))
        flags = int(float(r.get("CharacterFlags") or 268964353))
        family = int(float(r.get("NpcFamily") or 0))
        run = int(float(r.get("RunSpeedBase") or 0))
        vf = int(float(r.get("VisualFlags") or 31))
        hy = float(r.get("HeadingY") or 0)
        hw = float(r.get("HeadingW") or 1)
        side = map_side(r.get("Side"))
    except Exception:
        continue

    flags_str = r.get("Flags") or ""
    has_ext = "HasExtendedTextures" in flags_str
    is_pet = "IsPet" in flags_str
    has_flag7 = "UnknownFlag7" in flags_str
    entries = parse_overrides(r.get("TextureOverrides") or "")
    meshes = parse_mesh_list(r.get("Meshes") or "")
    textures = parse_tex_list(r.get("Textures") or "")
    head = 0
    try:
        head = int(float(r.get("HeadMesh") or 0))
    except Exception:
        pass

    slot = dict(
        name=name,
        pf=pf,
        side=side,
        x=x,
        y=y,
        z=z,
        level=level,
        md=md,
        scale=scale,
        health=health,
        flags=flags,
        family=family,
        run=run,
        vf=vf,
        hy=hy,
        hw=hw,
        meshes=meshes,
        textures=textures,
        has_ext=has_ext,
        is_pet=is_pet,
        has_flag7=has_flag7,
        entries=entries,
        head=head,
        raw_row=r,
    )
    slots.append(slot)
    if name not in by_name_ex or (has_ext and not by_name_ex[name]["has_ext"]):
        by_name_ex[name] = slot

ext_map = {}
pet_names = set()
flag7_names = set()
for name, s in by_name_ex.items():
    # ExtTex wildlife needs pet-style SCFU flags even when not a real pet.
    if s.get("is_pet") or s.get("has_ext"):
        pet_names.add(name)
    if s.get("has_flag7") or s.get("has_ext"):
        flag7_names.add(name)
    if not s["has_ext"]:
        continue
    blk = extract_exttex_from_raw(s["raw_row"])
    if blk:
        ext_map[name] = blk
        print("ext", name, len(blk), blk[:4].hex())
    else:
        print("FAILED ext", name)

print("slots", len(slots), "names", len(by_name_ex), "ext", len(ext_map))
from collections import Counter
print("pf", Counter(s["pf"] for s in slots))
print("side", Counter(s["side"] for s in slots))

lines = []
a = lines.append
a("namespace ZoneEngine.Core.Playfields")
a("{")
a("    #region Usings ...")
a("")
a("    using System;")
a("    using System.Collections.Generic;")
a("")
a("    using AORebirth.Core.Entities;")
a("    using AORebirth.Core.NPCHandler;")
a("    using AORebirth.Core.Playfields;")
a("    using AORebirth.Core.Textures;")
a("    using AORebirth.Core.Vector;")
a("    using AORebirth.Enums;")
a("    using AORebirth.ObjectManager;")
a("")
a("    using SmokeLounge.AOtomation.Messaging.GameData;")
a("")
a("    using Utility;")
a("")
a("    using ZoneEngine.Core;")
a("    using ZoneEngine.Core.Controllers;")
a("")
a("    using Quaternion = AORebirth.Core.Vector.Quaternion;")
a("")
a("    #endregion")
a("")
a("    /// <summary>")
a("    /// Elysium wildlife PF 4543 East + PF 4540 South.")
a("    /// Captures 20260727-182451 / 190145 / 193914 / 201436: appearance, ExtTex, Side.")
a("    /// Aggressive AOS 8m; Omni/Clan skip same-side and Neutral players. Heckler fight from 190145.")
a("    /// </summary>")
a("    internal static class ElysiumEastMobRuntime")
a("    {")
a("        private sealed class MobSlot")
a("        {")
a("            public string Name;")
a("            public int PlayfieldId;")
a("            public int Side;")
a("            public int MonsterData;")
a("            public int Level;")
a("            public int Health;")
a("            public int NpcFamily;")
a("            public int Scale;")
a("            public int RunSpeed;")
a("            public int CharacterFlags;")
a("            public int VisualFlags;")
a("            public int HeadMesh;")
a("            public float X;")
a("            public float Y;")
a("            public float Z;")
a("            public float HeadingY;")
a("            public float HeadingW;")
a("            public int[][] Textures;")
a("            public int[][] Meshes;")
a("        }")
a("")
a("        private const int ElysiumEastPlayfieldId = 4543;")
a("        private const int ElysiumSouthPlayfieldId = 4540;")
a("        private const double RespawnSeconds = 60.0;")
a("        private const float AggroRadiusMeters = 8.0f;")
a("")
a("        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();")
a("")
a("        private static readonly Dictionary<int, DateTime[]> NextRespawnUtcBySlot =")
a("            new Dictionary<int, DateTime[]>();")
a("")
a("        private static readonly Dictionary<int, float> AggroRadiusByNpcInstance =")
a("            new Dictionary<int, float>();")
a("")
a("        private static readonly object AggroGate = new object();")
a("")

for name, blk in sorted(ext_map.items()):
    ident = sid(name)
    a("        // Capture ExtTex: " + name)
    a("        private static readonly byte[] ExtTex_%s =" % ident)
    a("            {")
    chunk = []
    for bt in blk:
        chunk.append("0x%02X" % bt)
        if len(chunk) == 12:
            a("                " + ", ".join(chunk) + ",")
            chunk = []
    if chunk:
        a("                " + ", ".join(chunk))
    a("            };")
    a("")

a("        private static readonly byte[] DefaultScfuUnknown1 =")
a("            {")
a("                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,")
a("                0x02, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,")
a("                0x00, 0x02, 0x00, 0x00")
a("            };")
a("")
a("        private static readonly byte[] ExtTexScfuUnknown1 =")
a("            {")
a("                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,")
a("                0x03, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,")
a("                0x00, 0x03, 0x00, 0x00")
a("            };")
a("")
a("        private static readonly MobSlot[] Slots =")
a("            {")

for s in slots:
    if s["textures"]:
        tex_cs = "new[] { %s }" % ", ".join(
            "new[] { %d, %d, %d }" % t for t in s["textures"]
        )
    else:
        tex_cs = "null"
    if s["meshes"]:
        mesh_cs = "new[] { %s }" % ", ".join(
            "new[] { %d, %d, %d, %d }" % m for m in s["meshes"]
        )
    else:
        mesh_cs = "null"
    safe_name = s["name"].replace("\\", "\\\\").replace('"', '\\"')
    a(
        "                new MobSlot { Name = \"%s\", PlayfieldId = %d, Side = %d, "
        "MonsterData = %d, Level = %d, Health = %d, NpcFamily = %d, Scale = %d, "
        "RunSpeed = %d, CharacterFlags = %d, VisualFlags = %d, HeadMesh = %d, "
        "X = %.3ff, Y = %.3ff, Z = %.3ff, HeadingY = %.6ff, HeadingW = %.6ff, "
        "Textures = %s, Meshes = %s },"
        % (
            safe_name,
            s["pf"],
            s["side"],
            s["md"],
            s["level"],
            s["health"],
            s["family"],
            s["scale"],
            s["run"],
            s["flags"],
            s["vf"],
            s["head"],
            s["x"],
            s["y"],
            s["z"],
            s["hy"],
            s["hw"],
            tex_cs,
            mesh_cs,
        )
    )

a("            };")
a("")
a("        private static bool SupportsPlayfield(int playfieldInstance)")
a("        {")
a("            return playfieldInstance == ElysiumEastPlayfieldId")
a("                   || playfieldInstance == ElysiumSouthPlayfieldId;")
a("        }")
a("")
a("        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)")
a("        {")
for name in sorted(ext_map.keys()):
    safe = name.replace('"', '\\"')
    a('            if (string.Equals(name, "%s", StringComparison.OrdinalIgnoreCase))' % safe)
    a("            {")
    a("                data = (byte[])ExtTex_%s.Clone();" % sid(name))
    a("                return true;")
    a("            }")
a("            data = null;")
a("            return false;")
a("        }")
a("")
a("        internal static bool UsesPetScfuFlags(string name)")
a("        {")
for name in sorted(pet_names):
    safe = name.replace('"', '\\"')
    a('            if (string.Equals(name, "%s", StringComparison.OrdinalIgnoreCase))' % safe)
    a("            {")
    a("                return true;")
    a("            }")
a("            return false;")
a("        }")
a("")
a("        internal static bool UsesUnknownFlag7(string name)")
a("        {")
for name in sorted(flag7_names):
    safe = name.replace('"', '\\"')
    a('            if (string.Equals(name, "%s", StringComparison.OrdinalIgnoreCase))' % safe)
    a("            {")
    a("                return true;")
    a("            }")
a("            return false;")
a("        }")
a("")
a("        internal static bool TryGetCapturedScfuUnknown1(string name, out byte[] data)")
a("        {")
a("            if (string.IsNullOrEmpty(name))")
a("            {")
a("                data = null;")
a("                return false;")
a("            }")
a("")
a("            byte[] unused;")
a("            if (TryGetExtendedTextureOverride(name, out unused))")
a("            {")
a("                data = (byte[])ExtTexScfuUnknown1.Clone();")
a("                return true;")
a("            }")
a("")
for name in sorted(by_name_ex.keys()):
    safe = name.replace('"', '\\"')
    a('            if (string.Equals(name, "%s", StringComparison.OrdinalIgnoreCase))' % safe)
    a("            {")
    a("                data = (byte[])DefaultScfuUnknown1.Clone();")
    a("                return true;")
    a("            }")
a("            data = null;")
a("            return false;")
a("        }")
a("")
a("        public static ICharacter FindAutomaticAggroTarget(ICharacter npc)")
a("        {")
a("            if (npc == null || npc.Playfield == null || npc.Stats[StatIds.health].Value <= 0)")
a("            {")
a("                return null;")
a("            }")
a("")
a("            if (npc.FightingTarget.Instance != 0)")
a("            {")
a("                return null;")
a("            }")
a("")
a("            float radius;")
a("            lock (AggroGate)")
a("            {")
a("                if (!AggroRadiusByNpcInstance.TryGetValue(npc.Identity.Instance, out radius)")
a("                    || radius <= 0f)")
a("                {")
a("                    return null;")
a("                }")
a("            }")
a("")
a("            Playfield playfield = npc.Playfield as Playfield;")
a("            if (playfield == null || npc.RawCoordinates == null)")
a("            {")
a("                return null;")
a("            }")
a("")
a("            int npcSide = npc.Stats[StatIds.side].Value;")
a("            Coordinate npcCoord = npc.Coordinates();")
a("            ICharacter best = null;")
a("            double bestDistance = radius;")
a("            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, radius);")
a("            for (int i = 0; i < inRange.Count; i++)")
a("            {")
a("                ICharacter candidate = inRange[i];")
a("                if (candidate == null")
a("                    || candidate.Identity.Instance == npc.Identity.Instance")
a("                    || !(candidate.Controller is PlayerController)")
a("                    || candidate.Stats[StatIds.health].Value <= 0")
a("                    || candidate.RawCoordinates == null)")
a("                {")
a("                    continue;")
a("                }")
a("")
a("                int playerSide = candidate.Stats[StatIds.side].Value;")
a("                // Omni/Clan: skip same-side and Neutral players (only aggro opposing side).")
a("                if (npcSide == (int)Side.Omni || npcSide == (int)Side.Clan)")
a("                {")
a("                    if (playerSide == (int)Side.Neutral || playerSide == npcSide)")
a("                    {")
a("                        continue;")
a("                    }")
a("                }")
a("")
a("                double distance = candidate.Coordinates().coordinate.Distance2D(npcCoord.coordinate);")
a("                if (distance < bestDistance)")
a("                {")
a("                    bestDistance = distance;")
a("                    best = candidate;")
a("                }")
a("            }")
a("")
a("            return best;")
a("        }")
a("")
a("        public static void StartForPlayfield(")
a("            Playfield playfield,")
a("            Identity playfieldIdentity,")
a("            Action<ICharacter> activateNpc)")
a("        {")
a("            if (playfield == null")
a("                || activateNpc == null")
a("                || !SupportsPlayfield(playfieldIdentity.Instance)")
a("                || !LinkedPlayfields.Add(playfieldIdentity.Instance))")
a("            {")
a("                return;")
a("            }")
a("")
a("            NextRespawnUtcBySlot[playfieldIdentity.Instance] = new DateTime[Slots.Length];")
a("            int spawned = 0;")
a("            for (int i = 0; i < Slots.Length; i++)")
a("            {")
a("                if (Slots[i].PlayfieldId != playfieldIdentity.Instance)")
a("                {")
a("                    continue;")
a("                }")
a("")
a("                if (SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)")
a("                {")
a("                    spawned++;")
a("                }")
a("            }")
a("")
a("            LogUtil.Debug(")
a("                DebugInfoDetail.Engine,")
a('                "ElysiumEastMobRuntime started pf="')
a("                + playfieldIdentity.Instance")
a('                + " spawned="')
a("                + spawned")
a('                + "/"')
a("                + Slots.Length")
a('                + " source=182451+190145+193914+201436");')
a("        }")
a("")
a("        public static void ClearPlayfield(int playfieldInstance)")
a("        {")
a("            LinkedPlayfields.Remove(playfieldInstance);")
a("            NextRespawnUtcBySlot.Remove(playfieldInstance);")
a("        }")
a("")
a("        public static void TickRespawn(")
a("            Playfield playfield,")
a("            Identity playfieldIdentity,")
a("            Action<ICharacter> activateNpc)")
a("        {")
a("            if (playfield == null")
a("                || activateNpc == null")
a("                || !SupportsPlayfield(playfieldIdentity.Instance)")
a("                || !LinkedPlayfields.Contains(playfieldIdentity.Instance))")
a("            {")
a("                return;")
a("            }")
a("")
a("            DateTime[] next;")
a("            if (!NextRespawnUtcBySlot.TryGetValue(playfieldIdentity.Instance, out next)")
a("                || next == null")
a("                || next.Length != Slots.Length)")
a("            {")
a("                next = new DateTime[Slots.Length];")
a("                NextRespawnUtcBySlot[playfieldIdentity.Instance] = next;")
a("            }")
a("")
a("            for (int i = 0; i < Slots.Length; i++)")
a("            {")
a("                if (Slots[i].PlayfieldId != playfieldIdentity.Instance)")
a("                {")
a("                    continue;")
a("                }")
a("")
a("                Character living = FindLivingSlotMob(playfield, i);")
a("                if (living != null)")
a("                {")
a("                    next[i] = DateTime.MinValue;")
a("                    RegisterAggro(living.Identity.Instance);")
a("                    continue;")
a("                }")
a("")
a("                if (next[i] == DateTime.MinValue)")
a("                {")
a("                    next[i] = DateTime.UtcNow + TimeSpan.FromSeconds(RespawnSeconds);")
a("                    continue;")
a("                }")
a("")
a("                if (next[i] > DateTime.UtcNow)")
a("                {")
a("                    continue;")
a("                }")
a("")
a("                if (SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)")
a("                {")
a("                    next[i] = DateTime.MinValue;")
a("                }")
a("            }")
a("        }")
a("")
a("        private static Character FindLivingSlotMob(Playfield playfield, int slotIndex)")
a("        {")
a("            MobSlot slot = Slots[slotIndex];")
a("            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))")
a("            {")
a("                if (candidate == null")
a("                    || candidate.Controller == null")
a("                    || candidate.Controller is PlayerController")
a("                    || !string.Equals(candidate.Name, slot.Name, StringComparison.OrdinalIgnoreCase))")
a("                {")
a("                    continue;")
a("                }")
a("")
a("                Character mob = candidate as Character;")
a("                if (mob == null || mob.Stats[StatIds.health].Value <= 0)")
a("                {")
a("                    continue;")
a("                }")
a("")
a("                float dx = mob.Coordinates().x - slot.X;")
a("                float dz = mob.Coordinates().z - slot.Z;")
a("                if ((dx * dx) + (dz * dz) <= 25.0f)")
a("                {")
a("                    return mob;")
a("                }")
a("            }")
a("")
a("            return null;")
a("        }")
a("")
a("        private static Character SpawnSlot(")
a("            Playfield playfield,")
a("            Identity playfieldIdentity,")
a("            Action<ICharacter> activateNpc,")
a("            int slotIndex)")
a("        {")
a("            MobSlot slot = Slots[slotIndex];")
a("            if (slot.PlayfieldId != playfieldIdentity.Instance)")
a("            {")
a("                return null;")
a("            }")
a("")
a("            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Aggressive };")
a("            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(")
a('                "A004",')
a("                playfieldIdentity,")
a("                new Coordinate { x = slot.X, y = slot.Y, z = slot.Z },")
a("                new Quaternion(0.0, slot.HeadingY, 0.0, slot.HeadingW),")
a("                controller,")
a("                slot.Level);")
a("            if (mob == null)")
a("            {")
a("                return null;")
a("            }")
a("")
a("            mob.Name = slot.Name;")
a("            mob.Playfield = playfield;")
a("            ApplyCaptureStats(mob, slot);")
a("            PrepareCombat(mob, controller, slot);")
a("            mob.Coordinates(new Coordinate { x = slot.X, y = slot.Y, z = slot.Z });")
a("            mob.DoNotDoTimers = false;")
a("            RegisterAggro(mob.Identity.Instance);")
a("            activateNpc(mob);")
a("            RegisterAggro(mob.Identity.Instance);")
a("            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);")
a("            return mob;")
a("        }")
a("")
a("        private static void PrepareCombat(Character mob, NPCController controller, MobSlot slot)")
a("        {")
a("            CapturedEnemyCombatContract contract;")
a("            if (IsElysiumHeckler(slot.Name))")
a("            {")
a("                SetStat(mob, StatIds.mindamage, NpcCombatAttackRules.CapturedElysiumHecklerMinDamage);")
a("                SetStat(mob, StatIds.maxdamage, NpcCombatAttackRules.CapturedElysiumHecklerMaxDamage);")
a("                contract = CapturedEnemyCombatContract.ElysiumHecklerAttack(")
a('                    "elysium-heckler-20260727-190145",')
a("                    mob.Identity.Instance);")
a("            }")
a("            else")
a("            {")
a("                int minDamage = Math.Max(1, slot.Level);")
a("                int maxDamage = Math.Max(minDamage + 1, slot.Level + (slot.Level / 2));")
a("                SetStat(mob, StatIds.mindamage, minDamage);")
a("                SetStat(mob, StatIds.maxdamage, maxDamage);")
a("                contract = CapturedEnemyCombatContract.FixedAttackOnSight(")
a('                    "elysium-aos-20260727-193914",')
a("                    minDamage,")
a("                    maxDamage,")
a("                    2.0,")
a("                    0,")
a("                    0,")
a("                    0,")
a("                    0,")
a("                    0,")
a("                    0,")
a("                    0,")
a("                    0,")
a("                    0);")
a("            }")
a("")
a("            string unused;")
a("            CapturedEnemyCombatRuntime.Prepare(mob, controller, contract, out unused);")
a("            controller.AiProfile = NpcAiProfile.Aggressive;")
a("        }")
a("")
a("        private static bool IsElysiumHeckler(string name)")
a("        {")
a("            return !string.IsNullOrEmpty(name)")
a('                   && name.StartsWith("Heckler of ", StringComparison.OrdinalIgnoreCase);')
a("        }")
a("")
a("        private static void RegisterAggro(int npcInstance)")
a("        {")
a("            lock (AggroGate)")
a("            {")
a("                AggroRadiusByNpcInstance[npcInstance] = AggroRadiusMeters;")
a("            }")
a("        }")
a("")
a("        private static void ApplyCaptureStats(Character mob, MobSlot slot)")
a("        {")
a("            SetStat(mob, StatIds.monsterdata, slot.MonsterData);")
a("            SetStat(mob, StatIds.level, slot.Level);")
a("            SetStat(mob, StatIds.life, slot.Health);")
a("            SetStat(mob, StatIds.health, slot.Health);")
a("            SetStat(mob, StatIds.npcfamily, slot.NpcFamily);")
a("            SetStat(mob, StatIds.monsterscale, slot.Scale);")
a("            SetStat(mob, StatIds.runspeed, slot.RunSpeed);")
a("            SetStat(mob, StatIds.flags, slot.CharacterFlags);")
a("            SetStat(mob, StatIds.visualflags, slot.VisualFlags);")
a("            SetStat(mob, StatIds.side, slot.Side);")
a("            if (slot.HeadMesh > 0)")
a("            {")
a("                SetStat(mob, StatIds.headmesh, slot.HeadMesh);")
a("            }")
a("")
a("            mob.Textures.Clear();")
a("            if (slot.Textures != null)")
a("            {")
a("                for (int i = 0; i < slot.Textures.Length; i++)")
a("                {")
a("                    int[] t = slot.Textures[i];")
a("                    mob.Textures.Add(new AOTextures(t[0], t[1]));")
a("                }")
a("            }")
a("")
a("            mob.MeshLayer.Clear();")
a("            mob.SocialMeshLayer.Clear();")
a("            if (slot.Meshes != null)")
a("            {")
a("                for (int i = 0; i < slot.Meshes.Length; i++)")
a("                {")
a("                    int[] m = slot.Meshes[i];")
a("                    mob.MeshLayer.AddMesh(m[0], m[1], m[2], m[3]);")
a("                    mob.SocialMeshLayer.AddMesh(m[0], m[1], m[2], m[3]);")
a("                }")
a("            }")
a("        }")
a("")
a("        private static void SetStat(Character mob, StatIds stat, int value)")
a("        {")
a("            mob.Stats.SetBaseValueWithoutTriggering((int)stat, (uint)value);")
a("            mob.Stats[stat].Value = value;")
a("        }")
a("    }")
a("}")

text = "\n".join(lines) + "\n"
with open(out_cs, "w", encoding="utf-8", newline="\n") as f:
    f.write(text)
print("wrote", out_cs, "chars", len(text), "slots", len(slots))
