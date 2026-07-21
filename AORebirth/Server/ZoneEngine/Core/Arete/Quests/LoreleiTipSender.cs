namespace ZoneEngine.Core.Arete.Quests
{
    using System;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using ZoneEngine.Core.Controllers;

    /// <summary>Capture 20260721-loralei QuestFullUpdate wire tips (patched player + fixed mission ids).</summary>
    internal static class LoreleiTipSender
    {
        private const int TalkToLoreleiInstance = unchecked((int)0x555BEA03);
        private const int LostPetInstance = unchecked((int)0x555BEA04);
        private const int DeliverInstance = unchecked((int)0x555BEA05);
        private const int TalkToVaughnInstance = unchecked((int)0x555BEA06);

        public static RexQuestPreviewEmissionResult TrySendTalkLoreleiToLostPetHandoff(ICharacter source)
        {
            return Handoff(source, TalkToLoreleiInstance, SendLostPetTip, "TalkLorelei→LostPet", "Mission:555BEA04");
        }

        public static RexQuestPreviewEmissionResult TrySendLostPetToDeliverHandoff(ICharacter source)
        {
            return Handoff(source, LostPetInstance, SendDeliverTip, "LostPet→Deliver", "Mission:555BEA05");
        }

        public static RexQuestPreviewEmissionResult TrySendDeliverToVaughnHandoff(ICharacter source)
        {
            return Handoff(source, DeliverInstance, SendTalkToVaughnTip, "Deliver→Vaughn", "Mission:555BEA06");
        }

        public static RexQuestPreviewEmissionResult TrySendLostPetTipOnly(ICharacter source)
        {
            return TipOnly(source, SendLostPetTip, "LostPet", "Mission:555BEA04");
        }

        public static RexQuestPreviewEmissionResult TrySendDeliverTipOnly(ICharacter source)
        {
            return TipOnly(source, SendDeliverTip, "Deliver", "Mission:555BEA05");
        }

        public static RexQuestPreviewEmissionResult TrySendTalkToVaughnTipOnly(ICharacter source)
        {
            return TipOnly(source, SendTalkToVaughnTip, "TalkToVaughn", "Mission:555BEA06");
        }

        private static void SendLostPetTip(ICharacter source)
        {
            TrySendWire(
                source,
                "753C000A000102EE00000DC1797E306A465A40610000C350797E306A01000007E20000DAC3555D681B0000000F0000000000000000000000024C6F72656C65692773204C6F737420506574000000014D4C6F72656C65692773204C6F7374205065743C42523E3C42523E5468652062617274656E646572204C6F72656C656920686173206C6F7374206865722072656574207065742E205468652062697264206E616D6564204C6F6C6C7920657363617065642066726F6D2069747320636167652E20536865207468696E6B732074686520726565742068617320666F756E64206120667269656E642C2062656361757365206974206E6F726D616C6C7920636F6D657320686F6D652E3C42523E4C6F6361746520746865206573636170652061727469737420616E6420676574206974206261636B20696E20746F2069747320636167652E3C42523E3C42523E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A3C42523E43617074757265204C6F72656C656927732052656574205065742E3C2F666F6E743E000000C35078E0FC6800000006000000000000000000000000000003F1000003F1000003F14F34563000000000000000000000000000000000000000000000000000000000000000000000C350797E306A0003BC520000000000000000000007E20000001800000000000000000000000000000000000111D300019A610000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D57FBD700009C5000001999000186A0000186A0455140000000000044234000000007E20000C350797E306A000000010557FBD7000000000000000000000006000007E20000C350797E306A0000000000019A6100000000000000000000000000000000000000020000C73D579BAC17000000080000C73D579BAC170000000400000007000003F101",
                unchecked((int)0x555D681B),
                LostPetInstance);
        }

        private static void SendDeliverTip(ICharacter source)
        {
            TrySendWire(
                source,
                "77AB000A0001029A00000DC1797E306A465A40610000C350797E306A01000007E20000DAC3555D68AF0000000F00000000000000000000000244656C6976657220746865205265657420746F204C6F72656C65692000000000F744656C6976657220746865205265657420746F204C6F72656C6569203C42523E3C42523E41667465722066696E616C6C79206361746368696E67207468652073696C6C7920626972642C2072657475726E20746F204C6F72656C656920746F2068616E64206974206261636B2E3C42523E3C42523E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A3C42523E47697665204C6F72656C656920746865203C6120687265663D276974656D7265663A2F2F3239373336372F3239373336372F31273E50657420436167652057697468206120526565743C2F613E2E3C2F666F6E743E000000C35078E0FC6800000006000005A00000000000000A15000003F1000003F1000007E2000486F4000486F40000000100000000374B4A350000000000000000385949520000000B000000000000000000000000000000000000C350797E306A00026ADD0000000000000000000007E200000006000111D3504543470000000000000000000111D34C5254480000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D57FBF100009C5000001999000186A0000186A0455280000000000044468000000007E20000C350797E306A000000010557FBF1000000000000000000000006000007E20000C350797E306A0000000000019ACC000000000000000000000000000000000000000000000007000003F101",
                unchecked((int)0x555D68AF),
                DeliverInstance);
        }

        private static void SendTalkToVaughnTip(ICharacter source)
        {
            TrySendWire(
                source,
                "79D7000A0001020100000DC1797E306A465A40610000C350797E306A01000007E20000DAC3555D68D80000000F00000000000000000000000254616C6B20746F2056617567686E2048616D6D6F6E64000000007454616C6B20746F2056617567686E2048616D6D6F6E643C42523E3C42523E596F757220494420636172642069732066696E616C6C7920636F6D706C657465212054616C6B20746F2056617567686E2048616D6D6F6E642061626F7574206C656176696E67204172657465204C616E64696E672E000000C35078E0FC6800000006000004100000000000000A15000003F1000003F1000003F13359364B00000000000000000000000000000000000000000000000000000000000000000000C350797E306A0003BC520000000000000000000007E20000001800000000000000000000000000000000000111D300019A580000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D57FBF700000000000000000000000000000000000000000000000000000000000007E20000C350797E306A000000010557FBF7000000000000000000000006000007E20000C350797E306A0000000000019A58000000000000000000000000000000000000000000000007000003F101",
                unchecked((int)0x555D68D8),
                TalkToVaughnInstance);
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
                    label + " tip. mission=" + questId + " source=20260721-loralei");
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
                    label + " tip-only. mission=" + questId + " source=20260721-loralei");
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
