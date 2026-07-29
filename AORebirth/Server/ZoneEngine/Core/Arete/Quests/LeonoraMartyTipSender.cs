namespace ZoneEngine.Core.Arete.Quests
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using ZoneEngine.Core.Controllers;

    /// <summary>Capture 20260725-credit-card QuestFullUpdate tips (live AbsoluteTime; capture Abs=0).</summary>
    internal static class LeonoraMartyTipSender
    {
        public const int DeliverTipInstance = unchecked((int)0x5565CD8F);

        public const int StealTipInstance = unchecked((int)0x5565CD8E);

        private const int CapturedPlayerInstance = unchecked((int)0x7995EF26);

        private const int DeliverExpiryWriteOffset = 464;

        private const int StealExpiryWriteOffset = 373;

        private const long TipClientClockBaseSeconds = 1_201_445_827L;

        private const int TipMissionDurationSeconds = 48 * 60 * 60;

        private const string DeliverTipHex =
            "97F5000A0001024D00000DC17995EF26465A40610000C3507995EF2601000007E20000DAC35565CD8F0000000F00000000000000000000000244656C6976657220746865204C6F737420437265646974204361726400000000AA44656C6976657220746865204C6F73742043726564697420436172643C42523E3C42523E596F75206861766520666F756E6420612063726564697420636172642E20546865206E616D65207072696E746564206F6E207468652063617264206973204C656F6E6F7261204D617274792E20596F7520776F6E64657220696620796F752073686F756C642064656C697665722074686520637265646974206361726420746F206865722E000000C3507995EF26000000060000000000000000000009CB000003F1000003F1000007E20004885E0004885E00000001000000004D354D5400000000000000004E5459520000000A000000000000000000000000000000000000C3507995EF2600026ADD0000000000000000000007E200000006000111D342424F4B0000000000000000000111D34C454D590000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D5DF30800000000000000000000000000000000000000000000000000000000000007E20000C3507995EF2600000001055DF308000000000000000000000006000007E20000C3507995EF260000000000019A95000000000000000000000000000000000000000000000007000003F101";

        private const string StealTipHex =
            "97F4000A000101F200000DC17995EF26465A40610000C3507995EF2601000007E20000DAC35565CD8E0000000F000000000000000000000002537465616C2074686520637265646974732E2E2E0000000067537465616C2074686520637265646974732E2E2E3C42523E3C42523E596F75206861766520666F756E64206120637265646974206361726420616E6420776F6E64657220696620796F752073686F756C6420737465616C2074686520637265646974732E2E2E000000C3507995EF260000000600003A980000000000000000000003F1000003F1000003F14857585300000000000000004137325800000007000000000000000000000000000000000000C3507995EF260003BC520000000000000000000007E20000001800000000000000000000000000000000000111D300019A960000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D5DF30700000000000000000000000000000000000000000000000000000000000007E20000C3507995EF2600000001055DF307000000000000000000000006000007E20000C3507995EF260000000000019A96000000000000000000000000000000000000000000000007000003F101";

        public static RexQuestPreviewEmissionResult TrySendBothTips(ICharacter source)
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

        /// <summary>
        /// Capture 20260726-073341 steal Use: Deliver tip Delete first, then after cash Steal tip.
        /// </summary>
        public static void DeleteDeliverTipOnly(ICharacter source)
        {
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, DeliverTipInstance);
        }

        public static void DeleteStealTipOnly(ICharacter source)
        {
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
