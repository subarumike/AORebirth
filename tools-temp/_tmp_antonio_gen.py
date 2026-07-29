# Generate Antonio Stacklund dialogue, tip sender, combine rules, vendor files.
from __future__ import print_function
import json
import re
import struct
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-Antonio-Stacklund")
root = Path(r"AORebirth/Server/ZoneEngine")
hexlog = (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines()

PLAYER = 0x7996C028
NPC = 0x78E0FC7C

# tip id -> label for comments
TIPS = [
    (0x5569CDBF, "AssaultRifle", "upgrade_assault"),
    (0x5569CDC4, "WailingBat", "upgrade_bat"),
    (0x5569CDC5, "GripBlade", "upgrade_blade"),
    (0x5569CDCC, "ShaolinBow", "upgrade_bow"),
    (0x5569CDCD, "ElectricalPistol", "upgrade_pistol"),
    (0x5569CDCE, "InjectorDagger", "upgrade_dagger"),
    (0x5569CDD9, "WavePlasmaGun", "upgrade_energy"),
    (0x5569CDE4, "NiznoBombThrower", "upgrade_grenade"),
    (0x5569CDF1, "WarHammer", "upgrade_hammer"),
    (0x5569CDF7, "CersetRifle", "upgrade_rifle"),
    (0x5569CDF8, "PolishedEliminator", "upgrade_shotgun"),
    (0x5569CDF9, "SilentSpitter", "upgrade_smg"),
    (0x5569CDFF, "SpineSword", "upgrade_sword"),
    (0x5569CE00, "StrongOakBo", "upgrade_oakbo"),
    (0x5569CE11, "SurgeBat", "upgrade_bat_energy"),
    (0x5569CE17, "HandStaffNaja", "upgrade_naja"),
    (0x5569CDC1, "PoisonBracer", "craft_bracer"),
    (0x5569CDC2, "LeatherVest", "craft_vest"),
    (0x5569CDC3, "RangeMeter", "craft_hud"),
]

# Extract first QFU hex per mission id
tip_hex = {}
for line in hexlog:
    if "n3=QuestFullUpdate" not in line:
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", line)
    if not m:
        continue
    raw = bytes.fromhex(m.group(1))
    for mid, name, node in TIPS:
        if mid in tip_hex:
            continue
        if struct.pack(">I", mid) in raw:
            tip_hex[mid] = m.group(1).upper()

missing = [hex(m) for m, _, _ in TIPS if m not in tip_hex]
if missing:
    raise SystemExit("missing tips: " + ",".join(missing))

# Find a sample expiry value used in first tip for ReplaceInt32Be
sample = bytes.fromhex(tip_hex[0x5569CDBF])
# Find D2F14D then expiry
idx = sample.find(bytes.fromhex("D2F14D"))
expiry = struct.unpack_from(">I", sample, idx + 3)[0] if idx >= 0 else 0x5FA0E000
print("sample expiry", hex(expiry), "player", hex(PLAYER))

# --- dialogue JSON ---
upgrade_options = [
    ("Assault Rifle", "upgrade_assault", 0),
    ("Bat", "upgrade_bat", 1),
    ("Blade", "upgrade_blade", 2),
    ("Bow", "upgrade_bow", 3),
    ("Pistol", "upgrade_pistol", 4),
    ("Dagger", "upgrade_dagger", 5),
    ("Energy Gun", "upgrade_energy", 6),
    ("Grenade Launcher", "upgrade_grenade", 7),
    ("Hammer", "upgrade_hammer", 8),
    ("Rifle", "upgrade_rifle", 9),
    ("Shotgun", "upgrade_shotgun", 10),
    ("Submachine Gun", "upgrade_smg", 11),
    ("Sword", "upgrade_sword", 12),
    ("Oak Bo", "upgrade_oakbo", 13),
]
# special wording
upgrade_texts = {
    0: "I would like to upgrade my Assault Rifle.",
    1: "I would like to upgrade my Bat.",
    2: "I would like to upgrade my Blade.",
    3: "I would like to upgrade my Bow.",
    4: "I would like to upgrade my Pistol.",
    5: "I would like to upgrade my Dagger.",
    6: "I would like to upgrade my Energy Gun.",
    7: "I would like to upgrade my Grenade Launcher.",
    8: "I would like to upgrade my Hammer.",
    9: "I would like to upgrade my Rifle.",
    10: "I would like to upgrade my Shotgun.",
    11: "I would like to upgrade my Submachine Gun.",
    12: "I would like to upgrade my Sword.",
    13: "I would like to upgrade my Oak Bo.",
    14: "I would like to turn my Bat in to a melee energy weapon.",
    15: "I would like to upgrade my Damaged Staff of Naja.",
}

def opt(oid, index, text, next_id, actions=None):
    return {
        "Id": oid,
        "Index": index,
        "Text": text,
        "TextEvidence": "KnuBotAnswerList 20260726-Antonio-Stacklund",
        "NextNodeId": next_id,
        "Actions": actions or [],
    }

nodes = []
nodes.append({
    "Id": "antonio_001",
    "PromptText": "Hello there, welcome to Rubi-Ka! Looking for an upgrade for your weapon? With my greatest creation, the Adaptation Factory, you can improve your old, worn weapon!",
    "PromptTextConfidence": "KnubotAppendText 20260726-Antonio-Stacklund",
    "Options": [
        opt("antonio_001_0", 0, "As a matter of fact I would like to upgrade my weapon...", "antonio_upgrade_menu"),
        opt("antonio_001_1", 1, "Do you have any weapons to sell?", "antonio_sale"),
        opt("antonio_001_2", 2, "Do you only have weapons and weapon upgrades?", "antonio_other"),
        opt("antonio_001_3", 3, "Goodbye", "antonio_goodbye", [{"Type": "EndDialogue"}]),
    ],
    "EnterActions": [],
})
nodes.append({
    "Id": "antonio_sale",
    "PromptText": "Of course! Take a look.",
    "PromptTextConfidence": "KnubotAppendText 20260726-Antonio-Stacklund sale",
    "Options": [
        opt("antonio_sale_0", 0, "Goodbye", "antonio_goodbye", [{"Type": "EndDialogue"}]),
    ],
    "EnterActions": [],
})
nodes.append({
    "Id": "antonio_other",
    "PromptText": "Actually, you can use the adaption factory to make other things. How about a bracer? A leather vest? Or maybe a hud device is what you are looking for?",
    "PromptTextConfidence": "KnubotAppendText 20260726-Antonio-Stacklund other",
    "Options": [
        opt("antonio_other_0", 0, "Teach me how to make a bracer.", "antonio_craft_bracer"),
        opt("antonio_other_1", 1, "Teach me how to make a leather vest.", "antonio_craft_vest"),
        opt("antonio_other_2", 2, "Teach me how to make a hud device.", "antonio_craft_hud"),
        opt("antonio_other_3", 3, "Goodbye", "antonio_goodbye", [{"Type": "EndDialogue"}]),
    ],
    "EnterActions": [],
})

upgrade_opts = []
for i in range(16):
    node_id = [
        "antonio_upgrade_assault","antonio_upgrade_bat","antonio_upgrade_blade","antonio_upgrade_bow",
        "antonio_upgrade_pistol","antonio_upgrade_dagger","antonio_upgrade_energy","antonio_upgrade_grenade",
        "antonio_upgrade_hammer","antonio_upgrade_rifle","antonio_upgrade_shotgun","antonio_upgrade_smg",
        "antonio_upgrade_sword","antonio_upgrade_oakbo","antonio_upgrade_bat_energy","antonio_upgrade_naja",
    ][i]
    upgrade_opts.append(opt("antonio_upgrade_menu_%d" % i, i, upgrade_texts[i], node_id))
upgrade_opts.append(opt("antonio_upgrade_menu_16", 16, "Goodbye", "antonio_goodbye", [{"Type": "EndDialogue"}]))

nodes.append({
    "Id": "antonio_upgrade_menu",
    "PromptText": "Good! What type of weapon would you like to upgrade?",
    "PromptTextConfidence": "KnubotAppendText 20260726-Antonio-Stacklund upgrade-menu",
    "Options": upgrade_opts,
    "EnterActions": [],
})

recipe_nodes = [
    ("antonio_upgrade_assault", "AssaultRifle"),
    ("antonio_upgrade_bat", "WailingBat"),
    ("antonio_upgrade_blade", "GripBlade"),
    ("antonio_upgrade_bow", "ShaolinBow"),
    ("antonio_upgrade_pistol", "ElectricalPistol"),
    ("antonio_upgrade_dagger", "InjectorDagger"),
    ("antonio_upgrade_energy", "WavePlasmaGun"),
    ("antonio_upgrade_grenade", "NiznoBombThrower"),
    ("antonio_upgrade_hammer", "WarHammer"),
    ("antonio_upgrade_rifle", "CersetRifle"),
    ("antonio_upgrade_shotgun", "PolishedEliminator"),
    ("antonio_upgrade_smg", "SilentSpitter"),
    ("antonio_upgrade_sword", "SpineSword"),
    ("antonio_upgrade_oakbo", "StrongOakBo"),
    ("antonio_upgrade_bat_energy", "SurgeBat"),
    ("antonio_upgrade_naja", "HandStaffNaja"),
    ("antonio_craft_bracer", "PoisonBracer"),
    ("antonio_craft_vest", "LeatherVest"),
    ("antonio_craft_hud", "RangeMeter"),
]
for nid, tip_name in recipe_nodes:
    nodes.append({
        "Id": nid,
        "PromptText": "I will upload the recipe to your NCU.",
        "PromptTextConfidence": "KnubotAppendText 20260726-Antonio-Stacklund recipe",
        "Options": [
            opt(nid + "_0", 0, "Goodbye", "antonio_goodbye", [{"Type": "EndDialogue"}]),
        ],
        "EnterActions": [],
    })

nodes.append({
    "Id": "antonio_goodbye",
    "PromptText": "Off you go!",
    "PromptTextConfidence": "KnubotAppendText 20260726-Antonio-Stacklund goodbye",
    "Options": [],
    "EnterActions": [{"Type": "EndDialogue"}],
})

dialogue = {
    "Identity": {
        "Id": "arete-antonio-stacklund-dialogue-20260726",
        "Version": "captured-antonio-20260726-Antonio-Stacklund",
        "Source": "tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-Antonio-Stacklund",
    },
    "SourceCaptures": ["20260726-Antonio-Stacklund"],
    "Npcs": [{
        "Id": "antonio-stacklund",
        "NpcIdentity": "SimpleChar:78E0FC7C",
        "Name": "Antonio Stacklund",
        "RootNodeId": "antonio_001",
        "Aliases": ["(SimpleChar:78E0FC7C)"],
        "Nodes": nodes,
    }],
}

dlg_path = root / "Content/Arete/flint-novak/dialogue/antonio-stacklund.dialogue.json"
dlg_path.write_text(json.dumps(dialogue, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("wrote", dlg_path)

# --- TipSender ---
# Map enter node -> tip method
node_to_tip = {nid: tip for nid, tip in recipe_nodes}

lines = []
lines.append("namespace ZoneEngine.Core.Arete.Quests")
lines.append("{")
lines.append("    using System;")
lines.append("")
lines.append("    using AORebirth.Core.Entities;")
lines.append("    using AORebirth.Core.Network;")
lines.append("")
lines.append("    using ZoneEngine.Core.Controllers;")
lines.append("")
lines.append("    /// <summary>")
lines.append("    /// Capture 20260726-Antonio-Stacklund QuestFullUpdate recipe tips.")
lines.append("    /// </summary>")
lines.append("    internal static class AntonioStacklundTipSender")
lines.append("    {")
lines.append("        private const int CapturedPlayerInstance = unchecked((int)0x7996C028);")
lines.append("        private const int CapturedExpiryValue = unchecked((int)0x%08X);" % expiry)
lines.append("        private const long TipClientClockBaseSeconds = 1_201_445_827L;")
lines.append("        private const int TipMissionDurationSeconds = 48 * 60 * 60;")
lines.append("")

for mid, name, _ in TIPS:
    lines.append("        public const int %sInstance = unchecked((int)0x%08X);" % (name, mid))
lines.append("")

for mid, name, _ in TIPS:
    hx = tip_hex[mid]
    # wrap hex as const string - chunk for readability
    lines.append("        private const string %sHex =" % name)
    # split into ~120 char chunks
    chunks = [hx[i:i+120] for i in range(0, len(hx), 120)]
    for i, c in enumerate(chunks):
        if i < len(chunks) - 1:
            lines.append('            "%s" +' % c)
        else:
            lines.append('            "%s";' % c)
    lines.append("")

lines.append("        public static bool TrySendTipForNode(ICharacter source, string nodeId)")
lines.append("        {")
lines.append("            if (string.IsNullOrEmpty(nodeId))")
lines.append("            {")
lines.append("                return false;")
lines.append("            }")
lines.append("")
# switch-like if chain
for nid, tip in recipe_nodes:
    lines.append('            if (string.Equals(nodeId, "%s", StringComparison.OrdinalIgnoreCase))' % nid)
    lines.append("            {")
    lines.append("                return TrySend%s(source);" % tip)
    lines.append("            }")
    lines.append("")
lines.append("            return false;")
lines.append("        }")
lines.append("")

for mid, name, _ in TIPS:
    lines.append("        public static bool TrySend%s(ICharacter source)" % name)
    lines.append("        {")
    lines.append("            return TrySendWire(source, %sHex, %sInstance);" % (name, name))
    lines.append("        }")
    lines.append("")

# helper methods from ShinySword
lines.append("        private static bool TrySendWire(ICharacter source, string tipHex, int tipInstance)")
lines.append("        {")
lines.append("            ZoneClient client = source?.Controller?.Client as ZoneClient;")
lines.append("            if (client == null || source.Identity.Instance == 0)")
lines.append("            {")
lines.append("                return false;")
lines.append("            }")
lines.append("")
lines.append("            try")
lines.append("            {")
lines.append("                byte[] packet = HexToBytes(tipHex);")
lines.append("                ReplaceInt32Be(packet, CapturedPlayerInstance, source.Identity.Instance);")
lines.append("                int liveExpiry = ComputeLiveTipExpiry(client);")
lines.append("                ReplaceInt32Be(packet, CapturedExpiryValue, liveExpiry);")
lines.append("                client.EnqueueOutboundCompressedBuffer(packet);")
lines.append("                return true;")
lines.append("            }")
lines.append("            catch (Exception)")
lines.append("            {")
lines.append("                return false;")
lines.append("            }")
lines.append("        }")
lines.append("")
lines.append("        private static int ComputeLiveTipExpiry(ZoneClient client)")
lines.append("        {")
lines.append("            double secondsSinceSync = (DateTime.UtcNow - client.LastGameTimeSyncUtc).TotalSeconds;")
lines.append("            if (secondsSinceSync < 0)")
lines.append("            {")
lines.append("                secondsSinceSync = 0;")
lines.append("            }")
lines.append("")
lines.append("            return unchecked(")
lines.append("                (int)(TipClientClockBaseSeconds + (long)secondsSinceSync + TipMissionDurationSeconds));")
lines.append("        }")
lines.append("")
lines.append("        private static byte[] HexToBytes(string hex)")
lines.append("        {")
lines.append("            byte[] bytes = new byte[hex.Length / 2];")
lines.append("            for (int i = 0; i < bytes.Length; i++)")
lines.append("            {")
lines.append("                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);")
lines.append("            }")
lines.append("")
lines.append("            return bytes;")
lines.append("        }")
lines.append("")
lines.append("        private static void WriteInt32Be(byte[] packet, int offset, int value)")
lines.append("        {")
lines.append("            packet[offset] = (byte)(value >> 24);")
lines.append("            packet[offset + 1] = (byte)(value >> 16);")
lines.append("            packet[offset + 2] = (byte)(value >> 8);")
lines.append("            packet[offset + 3] = (byte)value;")
lines.append("        }")
lines.append("")
lines.append("        private static void ReplaceInt32Be(byte[] packet, int oldValue, int newValue)")
lines.append("        {")
lines.append("            for (int i = 0; i + 4 <= packet.Length; i++)")
lines.append("            {")
lines.append("                int v = (packet[i] << 24) | (packet[i + 1] << 16) | (packet[i + 2] << 8) | packet[i + 3];")
lines.append("                if (v == oldValue)")
lines.append("                {")
lines.append("                    WriteInt32Be(packet, i, newValue);")
lines.append("                }")
lines.append("            }")
lines.append("        }")
lines.append("    }")
lines.append("}")

tip_path = root / "Core/Arete/Quests/AntonioStacklundTipSender.cs"
tip_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
print("wrote", tip_path, "bytes", tip_path.stat().st_size)

# --- QuestRuntime ---
rt = r'''namespace ZoneEngine.Core.Arete.Quests
{
    using System;

    using AORebirth.Core.Entities;

    using Utility;

    /// <summary>
    /// Capture 20260726-Antonio-Stacklund: recipe tip upload on recipe dialogue nodes.
    /// </summary>
    internal static class AntonioStacklundQuestRuntime
    {
        public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null)
            {
                return false;
            }

            // Tips fire when entering recipe result nodes (after selecting a recipe option).
            // ContentDrivenNpcDialogueRouter calls this with previousNodeId = node before answer.
            return false;
        }

        public static void OnEnteredNode(ICharacter source, string nodeId)
        {
            if (source == null || string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            if (AntonioStacklundTipSender.TrySendTipForNode(source, nodeId))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Antonio Stacklund recipe tip sent node=" + nodeId + " character=" + source.Identity);
            }
        }
    }
}
'''
(root / "Core/Arete/Quests/AntonioStacklundQuestRuntime.cs").write_text(rt, encoding="utf-8")
print("wrote runtime")

# --- Combine rules from tip itemrefs ---
# pairs: (a, b, result) - both orders accepted
combines = [
    # Assault
    (248306, 248315, 248316),  # factory + fluid sample -> chemical tempering
    (248316, 121569, 248347),  # fluid + worn AR -> BO-18
    # Bracer (3-step partial from truncated tip - need full decode)
    (248306, 248322, 248321),  # factory + venom -> cartridge
    (248306, 248334, 248335),  # factory + compression -> pneumatic
    # Leather vest
    (248306, 248333, 248332),  # factory + intestines -> gut string
    (248332, 248325, 248373),  # gut + leather hide -> vest
    # Range meter - truncated; known from wiki/AOU
    (248306, 248318, 248317),  # factory + optical -> scope
    (248306, 248307, 248308),  # factory + power -> adapted power
    # Bat
    (248306, 248328, 248327),
    (248327, 121564, 248341),
    # Blade
    (248306, 248325, 248326),
    (248326, 218403, 248352),
    # Bow
    (248332, 248339, 248354),
    # Pistol
    (248308, 121567, 248343),
    # Dagger
    (248321, 218395, 248350),
    # Energy
    (248306, 248319, 248320),
    (248320, 248340, 248349),
    # Grenade
    (248335, 248338, 248346),
    # Hammer
    (248306, 248330, 248331),
    (248331, 218406, 248353),
    # Rifle
    (248317, 121568, 248348),
    # Shotgun (screwdriver, no factory)
    (150922, 121570, 248345),
    # SMG
    (248306, 248310, 248312),
    (248312, 121571, 248344),
    # Sword
    (248306, 248323, 248324),
    (248324, 218404, 248351),
    # Oak Bo
    (248326, 121565, 301071),
    # Surge bat
    (248308, 121564, 248355),
    # Naja
    (248321, 302163, 302602),
]

# Complete bracer and range meter from AOU/wiki (capture tip truncated but itemrefs present)
# Poison Injector Bracelet tip truncated at step 3 - fetch from ascii in hex
def full_tip_text(mid):
    raw = bytes.fromhex(tip_hex[mid])
    runs = re.findall(rb"[\x20-\x7e]{20,}", raw)
    return max((r.decode("ascii") for r in runs), key=len)

for mid in (0x5569CDC1, 0x5569CDC3):
    t = full_tip_text(mid)
    print("--- tip", hex(mid), "len", len(t))
    print(t[:1200])
    # extract itemref ids
    ids = [int(x) for x in re.findall(r"itemref://(\d+)/", t)]
    print("ids", ids)

print("combines count", len(combines))

# Write combine rules
cl = []
cl.append("namespace ZoneEngine.Core.Arete.Quests")
cl.append("{")
cl.append("    using System.Collections.Generic;")
cl.append("")
cl.append("    using AORebirth.Core.Items;")
cl.append("")
cl.append("    using ZoneEngine.Core;")
cl.append("")
cl.append("    /// <summary>")
cl.append("    /// Capture 20260726-Antonio-Stacklund Adaptation Factory weapon/gadget recipes.")
cl.append("    /// </summary>")
cl.append("    internal static class AntonioStacklundCombineRules")
cl.append("    {")
cl.append("        private static readonly int[][] Recipes =")
cl.append("            {")
for a, b, r in combines:
    cl.append("                new[] { %d, %d, %d }," % (a, b, r))
cl.append("            };")
cl.append("")
cl.append("        internal static TradeSkillEntry TryMatch(int sourceHighId, int targetHighId)")
cl.append("        {")
cl.append("            foreach (int[] recipe in Recipes)")
cl.append("            {")
cl.append("                int left = recipe[0];")
cl.append("                int right = recipe[1];")
cl.append("                int result = recipe[2];")
cl.append("                if ((sourceHighId == left && targetHighId == right)")
cl.append("                    || (sourceHighId == right && targetHighId == left))")
cl.append("                {")
cl.append("                    return CreateEntry(sourceHighId, targetHighId, result);")
cl.append("                }")
cl.append("            }")
cl.append("")
cl.append("            return null;")
cl.append("        }")
cl.append("")
cl.append("        internal static int SourceProcessBonus(int itemHighId)")
cl.append("        {")
cl.append("            foreach (int[] recipe in Recipes)")
cl.append("            {")
cl.append("                if (itemHighId == recipe[0] || itemHighId == recipe[1])")
cl.append("                {")
cl.append("                    return 1;")
cl.append("                }")
cl.append("            }")
cl.append("")
cl.append("            return 0;")
cl.append("        }")
cl.append("")
cl.append("        internal static int TargetProcessBonus(int itemHighId)")
cl.append("        {")
cl.append("            return SourceProcessBonus(itemHighId);")
cl.append("        }")
cl.append("")
cl.append("        private static TradeSkillEntry CreateEntry(int id1, int id2, int resultId)")
cl.append("        {")
cl.append("            int resolved = resultId;")
cl.append("            if (ItemLoader.ItemList != null && !ItemLoader.ItemList.ContainsKey(resolved))")
cl.append("            {")
cl.append("                // keep captured id even if missing; TradeSkillReceiver will fail soft")
cl.append("            }")
cl.append("")
cl.append("            return new TradeSkillEntry")
cl.append("                   {")
cl.append("                       ID1 = id1,")
cl.append("                       ID2 = id2,")
cl.append("                       DeleteFlag = 3,")
cl.append("                       IsImplant = false,")
cl.append("                       MaxBump = 0,")
cl.append("                       MaxXP = 0,")
cl.append("                       MinTargetQL = 0,")
cl.append("                       MinXP = 0,")
cl.append("                       QLRangePercent = 0,")
cl.append("                       ResultLowId = resolved,")
cl.append("                       ResultHighId = resolved,")
cl.append("                       Skills = new List<TradeSkillSkill>()")
cl.append("                   };")
cl.append("        }")
cl.append("    }")
cl.append("}")
(root / "Core/Arete/Quests/AntonioStacklundCombineRules.cs").write_text("\n".join(cl) + "\n", encoding="utf-8")
print("wrote combine rules")

# --- Vendor files from Sarah pattern ---
sarah_dir = root / "Core/Playfields"
# Write content provider
cp = r'''namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System.Collections.ObjectModel;

    #endregion

    /// <summary>
    /// Capture 20260726-Antonio-Stacklund: Use Antonio (shop cart) → ShopUpdate
    /// VendingMachine:12E7720D (owner 78E0FC7C), template StaticInstance=248368.
    /// </summary>
    internal static class CapturedAreteAntonioStacklundVendorContentProvider
    {
        internal const int AreteLandingPlayfieldId = 6553;

        internal const int SourceNpcInstance = unchecked((int)0x78E0FC7C);

        internal const int SourceVendorInstance = unchecked((int)0x12E7720D);

        internal const int CaptureVendorTemplateId = 248368;

        internal const int RuntimeVendorTemplateFallbackId = 99634;

        internal const string DisplayName = "Antonio Stacklund";

        internal const string Evidence = "AOSharpLiveCapture/20260726-Antonio-Stacklund";

        // Capture shop-updates.csv terminal 12E7720D slots 0..15.
        private static readonly CapturedAreteAlexAreaVendorStockDefinition[] CapturedStock =
            {
                new CapturedAreteAlexAreaVendorStockDefinition(0, 248306, 248306, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(1, 150922, 150922, 10),
                new CapturedAreteAlexAreaVendorStockDefinition(2, 121569, 121569, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(3, 248340, 248340, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(4, 248338, 248338, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(5, 121567, 121567, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(6, 121568, 121568, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(7, 121570, 121570, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(8, 121571, 121571, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(9, 121564, 121564, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(10, 218403, 218403, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(11, 248339, 248339, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(12, 218395, 218395, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(13, 218406, 218406, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(14, 121565, 121565, 1),
                new CapturedAreteAlexAreaVendorStockDefinition(15, 218404, 218404, 1)
            };

        internal static ReadOnlyCollection<CapturedAreteAlexAreaVendorStockDefinition> Stock
        {
            get
            {
                return new ReadOnlyCollection<CapturedAreteAlexAreaVendorStockDefinition>(CapturedStock);
            }
        }
    }
}
'''
(sarah_dir / "CapturedAreteAntonioStacklundVendorContentProvider.cs").write_text(cp, encoding="utf-8")

# Clone Sarah registry/runtime/handler with replacements via reading files
replacements = [
    ("CapturedAreteSarahGreene", "CapturedAreteAntonioStacklund"),
    ("Sarah Greene", "Antonio Stacklund"),
    ("sarah", "antonio"),
    ("Sarah", "Antonio"),
]
for src_name, dst_name in [
    ("CapturedAreteSarahGreeneVendorRuntimeRegistry.cs", "CapturedAreteAntonioStacklundVendorRuntimeRegistry.cs"),
    ("CapturedAreteSarahGreeneVendorRuntimeService.cs", "CapturedAreteAntonioStacklundVendorRuntimeService.cs"),
]:
    text = (sarah_dir / src_name).read_text(encoding="utf-8")
    for a, b in replacements:
        text = text.replace(a, b)
    text = text.replace("20260726-sara-greene-vendor", "20260726-Antonio-Stacklund")
    text = text.replace("armor shop", "general store / weapons shop")
    text = text.replace("armor vendor", "Antonio vendor")
    (sarah_dir / dst_name).write_text(text, encoding="utf-8")
    print("wrote", dst_name)

handler_src = root / "Core/MessageHandlers/CapturedAreteSarahGreeneVendorInteractionHandler.cs"
handler_dst = root / "Core/MessageHandlers/CapturedAreteAntonioStacklundVendorInteractionHandler.cs"
ht = handler_src.read_text(encoding="utf-8")
for a, b in replacements:
    ht = ht.replace(a, b)
ht = ht.replace("20260726-sara-greene-vendor", "20260726-Antonio-Stacklund")
handler_dst.write_text(ht, encoding="utf-8")
print("wrote handler")

# Complete bracer/hud recipes from full tip text
bracer = full_tip_text(0x5569CDC1)
hud = full_tip_text(0x5569CDC3)
Path(r"tools-temp/_tmp_antonio_bracer_hud.txt").write_text(bracer + "\n\n" + hud, encoding="utf-8")
print("done")
