namespace ZoneEngine.Core.Arete.Quests
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using ZoneEngine.Core.Controllers;

    /// <summary>
    /// Capture 20260725-shiny-sword-nano QuestFullUpdate tip for Mission:5565CD87.
    /// </summary>
    internal static class ShinySwordTipSender
    {
        public const int TipInstance = unchecked((int)0x5565CD87);

        private const int CapturedPlayerInstance = unchecked((int)0x7995EF26);

        private const int CapturedExpiryValue = unchecked((int)0x5DF2C300);

        private const int ExpiryWriteOffsetA = 383;

        private const int ExpiryWriteOffsetB = 431;

        private const long TipClientClockBaseSeconds = 1_201_445_827L;

        private const int TipMissionDurationSeconds = 48 * 60 * 60;

        // packets.hex.log IN #6 QuestFullUpdate (player patched + live AbsoluteTime).
        private const string TipHex =
            "90CA000A000101EF00000DC17995EF26465A40610000C3507995EF2601000007E20000DAC35565CD870000000F000000000000000000000002546865205368696E792053776F72640000000059546865205368696E792053776F72643C42523E3C42523E596F75206861766520666F756E642061207368696E792073776F72642E205065726861707320736F6D656F6E652077616E747320746869732073776F72642E2E2E000000C3507995EF26000000060000050000000000000009CB000003F1000003F1000007E2000368950003689500000019000000003259543000000000000000005058573600000007000000000000000000000000000000000000C3507995EF2600026ADD0000000000000000000007E200000006000111D3534853570000000000000000000111D3475244530000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D5DF2C300000000000000000000000000000000000000000000000000000000000007E20000C3507995EF2600000001055DF2C3000000000000000000000006000007E20000C3507995EF260000000000019A92000000000000000000000000000000000000000000000007000003F101";

        public static RexQuestPreviewEmissionResult TrySendTip(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Shiny Sword tip skipped: client missing.");
            }

            try
            {
                TrySendWire(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "Shiny Sword tip. mission=Mission:5565CD87 source=20260725-shiny-sword-nano");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("Shiny Sword tip failed: " + e.Message);
            }
        }

        public static void DeleteTip(ICharacter source)
        {
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, TipInstance);
        }

        private static void TrySendWire(ICharacter source)
        {
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client == null || source.Identity.Instance == 0)
            {
                return;
            }

            byte[] packet = HexToBytes(TipHex);
            ReplaceInt32Be(packet, CapturedPlayerInstance, source.Identity.Instance);
            int liveExpiry = ComputeLiveTipExpiry(client);
            ReplaceInt32Be(packet, CapturedExpiryValue, liveExpiry);
            if (ExpiryWriteOffsetA + 4 <= packet.Length)
            {
                WriteInt32Be(packet, ExpiryWriteOffsetA, liveExpiry);
            }

            if (ExpiryWriteOffsetB + 4 <= packet.Length)
            {
                WriteInt32Be(packet, ExpiryWriteOffsetB, liveExpiry);
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
