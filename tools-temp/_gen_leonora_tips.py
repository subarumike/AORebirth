# -*- coding: utf-8 -*-
deliver = open(r"tools-temp/_tip_deliver.hex", encoding="utf-8").read().strip()
steal = open(r"tools-temp/_tip_steal.hex", encoding="utf-8").read().strip()

parts = []
parts.append("namespace ZoneEngine.Core.Arete.Quests\n{\n")
parts.append("    using System;\n\n")
parts.append("    using AORebirth.Core.Entities;\n")
parts.append("    using AORebirth.Core.Network;\n\n")
parts.append("    using ZoneEngine.Core.Controllers;\n\n")
parts.append("    /// <summary>Capture 20260725-credit-card QuestFullUpdate tips (live AbsoluteTime; capture Abs=0).</summary>\n")
parts.append("    internal static class LeonoraMartyTipSender\n    {\n")
parts.append("        public const int DeliverTipInstance = unchecked((int)0x5565CD8F);\n\n")
parts.append("        public const int StealTipInstance = unchecked((int)0x5565CD8E);\n\n")
parts.append("        private const int CapturedPlayerInstance = unchecked((int)0x7995EF26);\n\n")
parts.append("        private const int DeliverExpiryWriteOffset = 464;\n\n")
parts.append("        private const int StealExpiryWriteOffset = 373;\n\n")
parts.append("        private const long TipClientClockBaseSeconds = 1_201_445_827L;\n\n")
parts.append("        private const int TipMissionDurationSeconds = 48 * 60 * 60;\n\n")
parts.append('        private const string DeliverTipHex =\n            "' + deliver + '";\n\n')
parts.append('        private const string StealTipHex =\n            "' + steal + '";\n\n')
parts.append("""        public static RexQuestPreviewEmissionResult TrySendBothTips(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Leonora tips skipped: client missing.");
            }

            try
            {
                TrySendWire(source, StealTipHex, StealExpiryWriteOffset);
                TrySendWire(source, DeliverTipHex, DeliverExpiryWriteOffset);
                return RexQuestPreviewEmissionResult.Sent(
                    "Leonora deliver+steal tips. mission=Mission:5565CD8F/5565CD8E source=20260725-credit-card");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("Leonora tips failed: " + e.Message);
            }
        }

        public static void DeleteBothTips(ICharacter source)
        {
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, DeliverTipInstance);
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, StealTipInstance);
        }

        private static void TrySendWire(ICharacter source, string hex, int expiryOffset)
        {
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client == null || source.Identity.Instance == 0)
            {
                return;
            }

            byte[] packet = HexToBytes(hex);
            ReplaceInt32Be(packet, CapturedPlayerInstance, source.Identity.Instance);
            if (expiryOffset >= 0 && expiryOffset + 4 <= packet.Length)
            {
                WriteInt32Be(packet, expiryOffset, ComputeLiveTipExpiry(client));
            }

            client.EnqueueOutboundCompressedBuffer(packet);
        }

        private static int ComputeLiveTipExpiry(ZoneClient client)
        {
            double secondsSinceSync = (DateTime.UtcNow - client.LastGameTimeSyncUtc).TotalSeconds;
            if (secondsSinceSync < 0)
            {
                secondsSinceSync = 0;
            }

            return unchecked(
                (int)(TipClientClockBaseSeconds + (long)secondsSinceSync + TipMissionDurationSeconds));
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

        private static void WriteInt32Be(byte[] packet, int offset, int value)
        {
            packet[offset] = (byte)(value >> 24);
            packet[offset + 1] = (byte)(value >> 16);
            packet[offset + 2] = (byte)(value >> 8);
            packet[offset + 3] = (byte)value;
        }

        private static void ReplaceInt32Be(byte[] packet, int oldValue, int newValue)
        {
            for (int i = 0; i + 4 <= packet.Length; i++)
            {
                int v = (packet[i] << 24) | (packet[i + 1] << 16) | (packet[i + 2] << 8) | packet[i + 3];
                if (v == oldValue)
                {
                    WriteInt32Be(packet, i, newValue);
                }
            }
        }
    }
}
""")

out = "AORebirth/Server/ZoneEngine/Core/Arete/Quests/LeonoraMartyTipSender.cs"
open(out, "w", encoding="utf-8").write("".join(parts))
print("wrote", out)
