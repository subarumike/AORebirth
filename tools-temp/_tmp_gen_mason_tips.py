import os

base = r"C:\Users\nermi\source\repos\AORebirth\tools-temp"
out_path = (
    r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine"
    r"\Core\Arete\Quests\DoctorMasonTipSender.cs"
)
tips = [
    ("AssembleImplant1", "555CF6A7", 0x555BE9FD, "Mission:555BE9FD"),
    ("AssembleImplant2", "555CF6AD", 0x555BE9FE, "Mission:555BE9FE"),
    ("AssembleImplant3", "555CF6AE", 0x555BE9FF, "Mission:555BE9FF"),
    ("ShowDrMasonImplant", "555CF6AF", 0x555BEA00, "Mission:555BEA00"),
    ("InstallTheImplant", "555CF6B1", 0x555BEA01, "Mission:555BEA01"),
    ("TalkToDoctorMasonAfterInstall", "555CF6B5", 0x555BEA02, "Mission:555BEA02"),
    ("TalkToLorelei", "555CF6B6", 0x555BEA03, "Mission:555BEA03"),
]

lines = []
lines.append("namespace ZoneEngine.Core.Arete.Quests")
lines.append("{")
lines.append("    using System;")
lines.append("    using AORebirth.Core.Entities;")
lines.append("    using AORebirth.Core.Network;")
lines.append("    using ZoneEngine.Core.Controllers;")
lines.append("")
lines.append(
    "    /// <summary>Capture 20260721-Mason QuestFullUpdate wire tips "
    "(patched player + fixed mission ids).</summary>"
)
lines.append("    internal static class DoctorMasonTipSender")
lines.append("    {")
for name, live, fixed, qid in tips:
    lines.append(
        f"        private const int {name}Instance = unchecked((int)0x{fixed:08X});"
    )
lines.append("")
lines.append(
    "        public static RexQuestPreviewEmissionResult "
    "TrySendTalkToDoctorMasonToAssemble1Handoff(ICharacter source)"
)
lines.append("        {")
lines.append(
    "            return Handoff(source, unchecked((int)0x555BE9FC), "
    "SendAssembleImplant1Tip, \"TalkMason→Assemble1\", \"Mission:555BE9FD\");"
)
lines.append("        }")
lines.append("")

chain = [
    (
        "TrySendAssemble1ToAssemble2Handoff",
        "AssembleImplant1",
        "SendAssembleImplant2Tip",
        "Assemble1→Assemble2",
        "Mission:555BE9FE",
    ),
    (
        "TrySendAssemble2ToAssemble3Handoff",
        "AssembleImplant2",
        "SendAssembleImplant3Tip",
        "Assemble2→Assemble3",
        "Mission:555BE9FF",
    ),
    (
        "TrySendAssemble3ToShowHandoff",
        "AssembleImplant3",
        "SendShowDrMasonImplantTip",
        "Assemble3→Show",
        "Mission:555BEA00",
    ),
    (
        "TrySendShowToInstallHandoff",
        "ShowDrMasonImplant",
        "SendInstallTheImplantTip",
        "Show→Install",
        "Mission:555BEA01",
    ),
    (
        "TrySendInstallToTalkMasonHandoff",
        "InstallTheImplant",
        "SendTalkToDoctorMasonAfterInstallTip",
        "Install→TalkMason",
        "Mission:555BEA02",
    ),
    (
        "TrySendTalkMasonToLoreleiHandoff",
        "TalkToDoctorMasonAfterInstall",
        "SendTalkToLoreleiTip",
        "TalkMason→Lorelei",
        "Mission:555BEA03",
    ),
]
for method, del_inst, send, label, qid in chain:
    lines.append(f"        public static RexQuestPreviewEmissionResult {method}(ICharacter source)")
    lines.append("        {")
    lines.append(
        f"            return Handoff(source, {del_inst}Instance, {send}, "
        f"\"{label}\", \"{qid}\");"
    )
    lines.append("        }")
    lines.append("")

for name, live, fixed, qid in tips:
    lines.append(
        f"        public static RexQuestPreviewEmissionResult TrySend{name}TipOnly(ICharacter source)"
    )
    lines.append("        {")
    lines.append(
        f"            return TipOnly(source, Send{name}Tip, \"{name}\", \"{qid}\");"
    )
    lines.append("        }")
    lines.append("")

for name, live, fixed, qid in tips:
    hx = open(os.path.join(base, f"_tmp_mason_{live}.hex")).read().strip()
    lines.append(f"        private static void Send{name}Tip(ICharacter source)")
    lines.append("        {")
    lines.append(
        f"            TrySendWire(source, \"{hx}\", "
        f"unchecked((int)0x{int(live, 16):08X}), {name}Instance);"
    )
    lines.append("        }")
    lines.append("")

lines.append(
    """
        private static RexQuestPreviewEmissionResult Handoff(
            ICharacter source,
            int deleteMissionInstance,
            Action<ICharacter> sendTip,
            string label,
            string questId)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(label + " skipped: client missing.");
            }

            try
            {
                SafeQuestFullUpdateSender.SendTipAction59AndDeletePublic(source, deleteMissionInstance);
                sendTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    label + " tip. mission=" + questId + " source=20260721-Mason");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(label + " failed: " + e.Message);
            }
        }

        private static RexQuestPreviewEmissionResult TipOnly(
            ICharacter source,
            Action<ICharacter> sendTip,
            string label,
            string questId)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(label + " tip skipped: client missing.");
            }

            try
            {
                sendTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    label + " tip-only. mission=" + questId + " source=20260721-Mason");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(label + " tip-only failed: " + e.Message);
            }
        }

        private static void TrySendWire(ICharacter source, string hex, int capturedMission, int fixedMission)
        {
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client == null || source.Identity.Instance == 0)
            {
                return;
            }

            const int capturedPlayer = unchecked((int)0x797E306A);
            byte[] packet = HexToBytes(hex);
            ReplaceInt32Be(packet, capturedPlayer, source.Identity.Instance);
            ReplaceInt32Be(packet, capturedMission, fixedMission);
            client.EnqueueOutboundCompressedBuffer(packet);
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private static void ReplaceInt32Be(byte[] packet, int from, int to)
        {
            byte b0 = (byte)(from >> 24);
            byte b1 = (byte)(from >> 16);
            byte b2 = (byte)(from >> 8);
            byte b3 = (byte)from;
            for (int i = 0; i + 4 <= packet.Length; i++)
            {
                if (packet[i] == b0 && packet[i + 1] == b1 && packet[i + 2] == b2 && packet[i + 3] == b3)
                {
                    packet[i] = (byte)(to >> 24);
                    packet[i + 1] = (byte)(to >> 16);
                    packet[i + 2] = (byte)(to >> 8);
                    packet[i + 3] = (byte)to;
                    i += 3;
                }
            }
        }
    }
}
""".rstrip(
        "\n"
    )
)

with open(out_path, "w", encoding="utf-8") as f:
    f.write("\n".join(lines) + "\n")
print("wrote", out_path)
