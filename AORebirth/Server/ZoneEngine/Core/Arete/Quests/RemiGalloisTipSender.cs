namespace ZoneEngine.Core.Arete.Quests
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using Utility;

    using ZoneEngine.Core.Controllers;

    /// <summary>
    /// Capture 20260727-204902 QuestFullUpdate wire tips for Remi Gallois.
    /// Mission:556B5E53 Quelling a SANDSTORM → Mission:556B5E59 Return to Remi.
    /// </summary>
    internal static class RemiGalloisTipSender
    {
        public const int QuellTipInstance = unchecked((int)0x556B5E53);

        public const int ReturnTipInstance = unchecked((int)0x556B5E59);

        private const int CapturedPlayerInstance = unchecked((int)0x7996C028);

        // Return tip uses D2F14D (ShinySword-style) — patch live AbsoluteTime.
        private const int CapturedReturnExpiry = unchecked((int)0x60EBA800);

        private const int ReturnExpiryWriteOffsetA = 497;

        private const int ReturnExpiryWriteOffsetB = 545;

        private const long TipClientClockBaseSeconds = 1_201_445_827L;

        private const int TipMissionDurationSeconds = 48 * 60 * 60;

        // QuestFullUpdate 2026-07-27T18:49:28.7137846Z Mission:556B5E53
        // Uses D2FC1C marker (same family as FlintKneecappingTipWire). Leave capture AbsoluteTime
        // alone — rewriting it with TipClientClockBase hides the tip (Remain past / zero).
        private const string QuellTipHex =
            "01DB000A000102E100000DC17996C028465A40610000C3507996C02801000007E20000DAC3556B5E530000000F0000000000000000000000025175656C6C696E6720612053414E4453544F524D00000001565175656C6C696E6720612053414E4453544F524D3C42523E3C42523E52656D692047616C6C6F6973206861732070726F766964656420796F75207769746820616E206578706572696D656E74616C20726F636B6574206C61756E636865722070726F746F747970652C20616E642061206D697373696F6E20746F2074657374206974206F7574206F6E20736F6D6520686F7374696C65206D656368616E697A656420746872656174732E3C42523E3C42523E3C6120687265663D276974656D7265663A2F2F3239353735372F3239353735372F31273E3C696D67207372633D227264623A2F2F323634373937223E3C2F613E3C42523E3C666F6E7420636F6C6F723D2223464646464646223E456C696D696E61746520332053414E4453544F524D204D6172617564657273207573696E672074686520526F636B6574204C61756E636865722E3C2F666F6E743E3C42523E3C42523E000000C35078E0FC7500000006000000000000000000000000000003F1000003F1000003F15558495200000000000000000000000000000000000000000000000000000000000000000000C3507996C02800002C420000000000000000000007E200000014000000000000000000000000000000000000000000000000000019994F323333000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2FC1C6E697200009C5000001999000186A0000186A0457C10000000000044280000000007E20000C3507996C02800000001046E6972000000000000000000000006000007E20000C3507996C02800000003000199E2000000000000000000000000000000000000000000000007000003F101";

        // QuestFullUpdate 2026-07-27T18:54:03.9138974Z Mission:556B5E59
        private const string ReturnTipHex =
            "090F000A0001026100000DC17996C028465A40610000C3507996C02801000007E20000DAC3556B5E590000000F00000000000000000000000252657475726E20746F2052656D69000000009C52657475726E20746F2052656D693C42523E3C42523E596F75206465666561746564207468652053414E4453544F524D204D6172617564657273213C42523E3C62723E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A3C42523E52657475726E20746F2052656D6920746F20636F6C6C6563742074686520626F756E74792E3C2F666F6E743E000000C35078E0FC7500000006000005A00000000000000820000003F1000003F1000013B50003687500036875000000190000000000036881000368810000001900000000000348E1000348E1000000190000000000036885000368850000001900000000555831450000000000000000334E44440000000B000000000000000000000000000000000000C3507996C0280003BC520000000000000000000007E20000001800000000000000000000000000000000000111D3000199E30000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D60EBA800009C5000001999000186A0000186A0455770000000000044544000000007E20000C3507996C028000000010560EBA8000000000000000000000006000007E20000C3507996C02800000000000199E3000000000000000000000000000000000000000000000007000003F101";

        public static RexQuestPreviewEmissionResult TrySendQuellTipOnly(ICharacter source)
        {
            return TipOnly(source, QuellTipHex, patchAbsoluteTime: false, "RemiQuell", "Mission:556B5E53");
        }

        public static RexQuestPreviewEmissionResult TrySendQuellToReturnHandoff(ICharacter source)
        {
            return Handoff(source, QuellTipInstance, "RemiQuell→Return", "Mission:556B5E59");
        }

        public static RexQuestPreviewEmissionResult TrySendReturnTipOnly(ICharacter source)
        {
            // Leave capture AbsoluteTime (same as Quell D2FC1C / working Remi tip).
            return TipOnly(source, ReturnTipHex, patchAbsoluteTime: false, "RemiReturn", "Mission:556B5E59");
        }

        public static void DeleteReturnTip(ICharacter source)
        {
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, ReturnTipInstance);
        }

        private static RexQuestPreviewEmissionResult Handoff(
            ICharacter source,
            int deleteMissionInstance,
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
                // Return tip AbsoluteTime left as capture (patching TipClientClockBase hid Remain).
                if (!TrySendWire(source, ReturnTipHex, patchAbsoluteTime: false))
                {
                    return RexQuestPreviewEmissionResult.Failed(label + " tip wire failed after delete.");
                }

                return RexQuestPreviewEmissionResult.Sent(
                    label + " tip. mission=" + questId + " source=20260727-204902");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(label + " failed: " + e.Message);
            }
        }

        private static RexQuestPreviewEmissionResult TipOnly(
            ICharacter source,
            string tipHex,
            bool patchAbsoluteTime,
            string label,
            string questId)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(label + " tip skipped: client missing.");
            }

            try
            {
                if (!TrySendWire(source, tipHex, patchAbsoluteTime))
                {
                    return RexQuestPreviewEmissionResult.Failed(
                        label + " tip wire failed (ZoneClient/enqueue). mission=" + questId);
                }

                return RexQuestPreviewEmissionResult.Sent(
                    label + " tip-only. mission=" + questId + " source=20260727-204902");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(label + " tip-only failed: " + e.Message);
            }
        }

        private static bool TrySendWire(ICharacter source, string hex, bool patchAbsoluteTime)
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

            if (patchAbsoluteTime)
            {
                // Return tip is D2F14D / ShinySword-style — needs live AbsoluteTime.
                SafeQuestFullUpdateSender.ReanchorGameTimeForWireTip(source);
                int liveExpiry = ComputeLiveTipExpiry(client);
                ReplaceInt32Be(packet, CapturedReturnExpiry, liveExpiry);
                WriteInt32Be(packet, ReturnExpiryWriteOffsetA, liveExpiry);
                WriteInt32Be(packet, ReturnExpiryWriteOffsetB, liveExpiry);
            }

            client.EnqueueOutboundCompressedBuffer(packet);
            LogUtil.Debug(
                DebugInfoDetail.Error,
                "RemiGalloisTipSender wire enqueued bytes="
                + packet.Length
                + " character="
                + source.Identity.ToString(true)
                + " patchAbsTime="
                + patchAbsoluteTime);
            return true;
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
            if (packet == null || offset < 0 || offset + 4 > packet.Length)
            {
                return;
            }

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
