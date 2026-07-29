namespace ZoneEngine.Core.Arete.Quests
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using ZoneEngine.Core.Controllers;

    /// <summary>Capture 20260725-patric QuestFullUpdate wire tips (patched player + live AbsoluteTime).</summary>
    internal static class PatrickSunTipSender
    {
        public const int InsuranceTipInstance = unchecked((int)0x5565C962);

        public const int TalkTipInstance = unchecked((int)0x5565C963);

        public const int YellTipInstance = unchecked((int)0x5565C966);

        private const int CapturedPlayerInstance = unchecked((int)0x7995EF26);

        // Capture AbsoluteTime (Unknown11) values — replaced with live client-clock expiry.
        private const int CapturedInsuranceExpiry = unchecked((int)0x6A651192);

        private const int CapturedTalkExpiry = unchecked((int)0x6A651123);

        // Yell capture AbsoluteTime is 0 (causes Remain 00:00 / garbage). Write at fixed offset.
        private const int YellExpiryWriteOffset = 472;

        private const long TipClientClockBaseSeconds = 1_201_445_827L;

        private const int TipMissionDurationSeconds = 48 * 60 * 60;

        public static RexQuestPreviewEmissionResult TrySendInsuranceTipOnly(ICharacter source)
        {
            return TipOnly(source, SendInsuranceTip, "PatrickInsurance", "Mission:5565C962");
        }

        public static RexQuestPreviewEmissionResult TrySendInsuranceToTalkHandoff(ICharacter source)
        {
            return Handoff(source, InsuranceTipInstance, SendTalkTip, "PatrickInsurance→Talk", "Mission:5565C963");
        }

        public static RexQuestPreviewEmissionResult TrySendTalkToYellHandoff(ICharacter source)
        {
            return Handoff(source, TalkTipInstance, SendYellTip, "PatrickTalk→Yell", "Mission:5565C966");
        }

        public static RexQuestPreviewEmissionResult TrySendTalkTipOnly(ICharacter source)
        {
            return TipOnly(source, SendTalkTip, "PatrickTalk", "Mission:5565C963");
        }

        public static RexQuestPreviewEmissionResult TrySendYellTipOnly(ICharacter source)
        {
            return TipOnly(source, SendYellTip, "PatrickYell", "Mission:5565C966");
        }

        private static void SendInsuranceTip(ICharacter source)
        {
            TrySendWire(
                source,
                "452A000A000102C300000DC17995EF26465A40610000C3507995EF2601000007E20000DAC35565C9620000000F0000000000000000000000025361766520617420616E20496E737572616E6365207465726D696E616C000000012F5361766520617420616E20496E737572616E6365207465726D696E616C3C42523E3C42523E5061747269636B2053756E2061736B656420796F7520746F207363616E20796F75722063656C6C2073747275637475726520616E642067657420697420736176656420617420616E20696E737572616E6365207465726D696E616C2E2E2E204170706172656E746C7920746861742073686F756C64207361766520796F7572206C69666520696620796F75206469652E2E2E2041726520796F75207375726520796F752074727573742068696D3F3C42523E3C42523E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A2055736520746865204943432043656C6C20537472756374757265205363616E6E65722E3C2F666F6E743E000000C35078E0FC7B00000006000000000000000000000000000003F1000003F1000003F151544E5A00000000000000000000000000000000000000000000000000000000000000000000C3507995EF260003BC520000000500000005000007E20000001800000000000000000000000000000000000111D300019B08000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A651192000000000000D2F14D5DF09400009C5000001999000186A0000186A04558800000000000444AC000000007E20000C3507995EF2600000001055DF094000000000000000000000006000007E20000C3507995EF260000000000019B08000000000000000000000000000000000000000000000007000003F101",
                CapturedInsuranceExpiry);
        }

        private static void SendTalkTip(ICharacter source)
        {
            TrySendWire(
                source,
                "4565000A0001023C00000DC17995EF26465A40610000C3507995EF2601000007E20000DAC35565C9630000000F00000000000000000000000254616C6B20746F205061747269636B2053756E00000000B254616C6B20746F205061747269636B2053756E3C42523E3C42523E5061747269636B20746F6C6420796F7520746F20696E666F726D2068696D20616674657220796F752068616420757365642074686520696E737572616E6365207465726D696E616C2E203C42523E3C42523E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A2054616C6B20746F205061747269636B2053756E2E3C2F666F6E743E000000C35078E0FC7B00000006000000000000000000000000000003F1000003F1000003F15231334100000000000000000000000000000000000000000000000000000000000000000000C3507995EF260003BC520000000300000003000007E20000001800000000000000000000000000000000000111D300019B09000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A651123000000000000D2F14D5DF09500009C5000001999000186A0000186A04558600000000000444B0000000007E20000C3507995EF2600000001055DF095000000000000000000000006000007E20000C3507995EF260000000000019B09000000000000000000000000000000000000000000000007000003F101",
                CapturedTalkExpiry);
        }

        private static void SendYellTip(ICharacter source)
        {
            // Capture AbsoluteTime is 0 → client Remain 00:00 / huge garbage. Patch live expiry.
            TrySendWire(
                source,
                "459A000A0001025500000DC17995EF26465A40610000C3507995EF2601000007E20000DAC35565C9660000000F00000000000000000000000259656C6C206174205061747269636B20666F72204B696C6C696E6720796F7500000000AF59656C6C206174205061747269636B20666F72204B696C6C696E6720796F753C42523E3C42523E50617472696B2061637475616C6C79206B696C6C656420796F752E20596F75206E65656420746F2074656163682068696D2061206C6573736F6E213C62723E3C62723E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A2054616C6B20746F205061747269636B2053756E2E3C2F666F6E743E000000C35078E0FC7B000000060000089800000000000008B5000003F1000003F1000007E200008FAE00008FA90000001E00000000424243310000000000000000444F53580000001E000000000000000000000000000000000000C3507995EF260003BC520000000000000000000007E20000001800000000000000000000000000000000000111D300019B0A0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D5DF09800009C5000001999000186A0000186A04558600000000000444B0000000007E20000C3507995EF2600000001055DF098000000000000000000000006000007E20000C3507995EF260000000000019B0A000000000000000000000000000000000000000000000007000003F101",
                0,
                YellExpiryWriteOffset);
        }

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
                SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, deleteMissionInstance);
                sendTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    label + " tip. mission=" + questId + " source=20260725-patric");
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
                    label + " tip-only. mission=" + questId + " source=20260725-patric");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(label + " tip-only failed: " + e.Message);
            }
        }

        private static void TrySendWire(
            ICharacter source,
            string hex,
            int capturedExpiry,
            int yellExpiryOffset = -1)
        {
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client == null || source.Identity.Instance == 0)
            {
                return;
            }

            byte[] packet = HexToBytes(hex);
            ReplaceInt32Be(packet, CapturedPlayerInstance, source.Identity.Instance);

            int liveExpiry = ComputeLiveTipExpiry(client);
            if (yellExpiryOffset >= 0 && yellExpiryOffset + 4 <= packet.Length)
            {
                WriteInt32Be(packet, yellExpiryOffset, liveExpiry);
            }
            else if (capturedExpiry != 0)
            {
                ReplaceInt32Be(packet, capturedExpiry, liveExpiry);
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
