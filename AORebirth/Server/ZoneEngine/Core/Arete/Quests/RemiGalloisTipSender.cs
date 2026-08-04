namespace ZoneEngine.Core.Arete.Quests
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using Utility;

    using ZoneEngine.Core.Controllers;

    /// <summary>
    /// Capture 20260801-SANDSTORM QuestFullUpdate wire tips for Remi Gallois.
    /// Mission:5576B777 Quelling a SANDSTORM → Mission:5576B780 Return to Remi.
    /// </summary>
    internal static class RemiGalloisTipSender
    {
        public const int QuellTipInstance = unchecked((int)0x5576B777);

        public const int ReturnTipInstance = unchecked((int)0x5576B780);

        // Legacy tip instances from 20260727-204902 (clear stuck tips on rewrite).
        private const int LegacyQuellTipInstance = unchecked((int)0x556B5E53);

        private const int LegacyReturnTipInstance = unchecked((int)0x556B5E59);

        private const int CapturedPlayerInstance = unchecked((int)0x79B0C81A);

        // QuestFullUpdate 2026-08-01T17:42:56.7966948Z Mission:5576B777
        private const string QuellTipHex =
            "4C64000A000102E100000DC179B0C81A465A40610000C35079B0C81A01000007E20000DAC35576B7770000000F0000000000000000000000025175656C6C696E6720612053414E4453544F524D00000001565175656C6C696E6720612053414E4453544F524D3C42523E3C42523E52656D692047616C6C6F6973206861732070726F766964656420796F75207769746820616E206578706572696D656E74616C20726F636B6574206C61756E636865722070726F746F747970652C20616E642061206D697373696F6E20746F2074657374206974206F7574206F6E20736F6D6520686F7374696C65206D656368616E697A656420746872656174732E3C42523E3C42523E3C6120687265663D276974656D7265663A2F2F3239353735372F3239353735372F31273E3C696D67207372633D227264623A2F2F323634373937223E3C2F613E3C42523E3C666F6E7420636F6C6F723D2223464646464646223E456C696D696E61746520332053414E4453544F524D204D6172617564657273207573696E672074686520526F636B6574204C61756E636865722E3C2F666F6E743E3C42523E3C42523E000000C35078E0FC7500000006000000000000000000000000000003F1000003F1000003F15558495200000000000000000000000000000000000000000000000000000000000000000000C35079B0C81A00002C420000000000000000000007E200000014000000000000000000000000000000000000000000000000000019994F323333000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2FC1C70E5A600009C5000001999000186A0000186A0457C10000000000044280000000007E20000C35079B0C81A000000010470E5A6000000000000000000000006000007E20000C35079B0C81A00000003000199E2000000000000000000000000000000000000000000000007000003F101";

        // QuestFullUpdate 2026-08-01T17:46:34.4974287Z Mission:5576B780
        private const string ReturnTipHex =
            "5109000A0001026100000DC179B0C81A465A40610000C35079B0C81A01000007E20000DAC35576B7800000000F00000000000000000000000252657475726E20746F2052656D69000000009C52657475726E20746F2052656D693C42523E3C42523E596F75206465666561746564207468652053414E4453544F524D204D6172617564657273213C42523E3C62723E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A3C42523E52657475726E20746F2052656D6920746F20636F6C6C6563742074686520626F756E74792E3C2F666F6E743E000000C35078E0FC7500000006000004880000000000000A15000003F1000003F1000013B50003687500036875000000190000000000036881000368810000001900000000000348E1000348E1000000190000000000036885000368850000001900000000555831450000000000000000334E444400000004000000000000000000000000000000000000C35079B0C81A0003BC520000000000000000000007E20000001800000000000000000000000000000000000111D3000199E30000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D67B2FC00009C5000001999000186A0000186A0455770000000000044544000000007E20000C35079B0C81A000000010567B2FC000000000000000000000006000007E20000C35079B0C81A00000000000199E3000000000000000000000000000000000000000000000007000003F101";

        public static RexQuestPreviewEmissionResult TrySendQuellTipOnly(ICharacter source)
        {
            return TipOnly(source, QuellTipHex, "RemiQuell", "Mission:5576B777");
        }

        public static RexQuestPreviewEmissionResult TrySendQuellToReturnHandoff(ICharacter source)
        {
            return Handoff(source, QuellTipInstance, ReturnTipHex, "RemiQuell→Return", "Mission:5576B780");
        }

        public static RexQuestPreviewEmissionResult TrySendReturnTipOnly(ICharacter source)
        {
            return TipOnly(source, ReturnTipHex, "RemiReturn", "Mission:5576B780");
        }

        public static void DeleteReturnTip(ICharacter source)
        {
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, ReturnTipInstance);
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, LegacyReturnTipInstance);
        }

        public static void DeleteQuellTip(ICharacter source)
        {
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, QuellTipInstance);
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, LegacyQuellTipInstance);
        }

        private static RexQuestPreviewEmissionResult Handoff(
            ICharacter source,
            int deleteMissionInstance,
            string nextHex,
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
                SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, LegacyQuellTipInstance);
                if (!TrySendWire(source, nextHex))
                {
                    return RexQuestPreviewEmissionResult.Failed(label + " tip wire failed after delete.");
                }

                return RexQuestPreviewEmissionResult.Sent(
                    label + " tip. mission=" + questId + " source=20260801-SANDSTORM");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(label + " failed: " + e.Message);
            }
        }

        private static RexQuestPreviewEmissionResult TipOnly(
            ICharacter source,
            string tipHex,
            string label,
            string questId)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(label + " tip skipped: client missing.");
            }

            try
            {
                if (!TrySendWire(source, tipHex))
                {
                    return RexQuestPreviewEmissionResult.Failed(
                        label + " tip wire failed (ZoneClient/enqueue). mission=" + questId);
                }

                return RexQuestPreviewEmissionResult.Sent(
                    label + " tip-only. mission=" + questId + " source=20260801-SANDSTORM");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(label + " tip-only failed: " + e.Message);
            }
        }

        private static bool TrySendWire(ICharacter source, string hex)
        {
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client == null || source.Identity.Instance == 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "RemiGalloisTipSender wire skipped: ZoneClient cast failed or identity=0");
                return false;
            }

            byte[] packet = HexToBytes(hex);
            ReplaceInt32Be(packet, CapturedPlayerInstance, source.Identity.Instance);
            client.EnqueueOutboundCompressedBuffer(packet);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "RemiGalloisTipSender wire enqueued bytes="
                + packet.Length
                + " character="
                + source.Identity.ToString(true));
            return true;
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
