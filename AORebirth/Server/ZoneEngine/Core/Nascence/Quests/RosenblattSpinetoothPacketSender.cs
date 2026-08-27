namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using Utility;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture-backed Spinetooth QuestFullUpdate / delete packets (20260822-083846).
    /// </summary>
    internal static class RosenblattSpinetoothPacketSender
    {
        private const int CapturedCharacterInstance = unchecked((int)0x78D84040);
        private const int CapturedMissionInstance = unchecked((int)0x55AA38B6);

        private const int MissionDurationSeconds = 0x2760;
        private const long ClientClockBaseSeconds = 1_201_445_827L;

        // Capture QuestFullUpdate (Spinetooth Hatchling).
        private const string AcceptQuestFullUpdateHex =
            "1224000A000101EB00000DBD78D84040465A40610000C35078D8404001000007E20000DAC355AA38B60000000F0000000000000000000000025370696E65746F6F74682074726F75626C6500000000625370696E65746F6F74682074726F75626C653C42523E3C42523E44722E20526F73656E626C6174742061736B656420796F7520746F2067657420726964206F662061205370696E65746F6F74682048617463686C696E6720666F722068696D2E20000000C3507A18D41900000006000007D000000000000007D0000003F1000003F1000003F144425839000000000000000045364F4C0000003C000000000000000000000000000000000000C35078D8404000002C420000276000002760000007E20000000100000000000000000000000000000000000111D353504854000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A92813C000000000000D2FC1C80C15C00000000000000000000000000000000000000000000000000000000000007E20000C35078D84040000000010480C15C000000000000000000000006000007E20000C35078D840400000000000018A82000000000000000000000000000000000000000000000007000003F101";

        internal static bool TrySendQuestFullUpdate(ICharacter character)
        {
            return TrySendRaw(character, AcceptQuestFullUpdateHex);
        }

        internal static bool TrySendQuestDelete(ICharacter character)
        {
            // Reuse proven Hiathlin Action59/QuestDelete wire; substitute Mission:55AA38B6.
            return RosenblattHiathlinPacketSender.TrySendQuestDelete(
                character,
                RosenblattSpinetoothInteractionRules.QuestId);
        }

        private static bool TrySendRaw(ICharacter character, string hex)
        {
            if (!CanSend(character) || string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            try
            {
                var client = character.Controller.Client as ZoneClient;
                byte[] packet = HexToBytes(hex);
                ReplaceInstance(packet, CapturedCharacterInstance, character.Identity.Instance);
                PatchMissionExpiryBand(packet, client);
                client.EnqueueOutboundCompressedBuffer(packet);
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "ROSENBLATT_SPINETOOTH QuestFullUpdate failed: " + exception.Message);
                return false;
            }
        }

        private static void PatchMissionExpiryBand(byte[] packet, ZoneClient client)
        {
            if (packet == null || client == null)
            {
                return;
            }

            double secondsSinceSync = (DateTime.UtcNow - client.LastGameTimeSyncUtc).TotalSeconds;
            if (secondsSinceSync < 0)
            {
                secondsSinceSync = 0;
            }

            if (secondsSinceSync > MissionDurationSeconds)
            {
                secondsSinceSync = 0;
                client.LastGameTimeSyncUtc = DateTime.UtcNow;
            }

            long clientClockNow = ClientClockBaseSeconds + (long)secondsSinceSync;
            long expiry = clientClockNow + MissionDurationSeconds;

            for (int i = 0; i + 4 <= packet.Length; i++)
            {
                int value = (packet[i] << 24)
                            | (packet[i + 1] << 16)
                            | (packet[i + 2] << 8)
                            | packet[i + 3];
                int band = value & unchecked((int)0xFFFF0000);
                if (band < unchecked((int)0x6A800000) || band > unchecked((int)0x6AC00000))
                {
                    continue;
                }

                WriteInt32BigEndian(packet, i, (int)expiry);
                return;
            }
        }

        private static void WriteInt32BigEndian(byte[] packet, int offset, int value)
        {
            packet[offset] = (byte)(value >> 24);
            packet[offset + 1] = (byte)(value >> 16);
            packet[offset + 2] = (byte)(value >> 8);
            packet[offset + 3] = (byte)value;
        }

        private static bool CanSend(ICharacter character)
        {
            return character != null
                   && character.Controller != null
                   && character.Controller.Client is ZoneClient;
        }

        private static void ReplaceInstance(byte[] packet, int from, int to)
        {
            byte f0 = (byte)(from >> 24);
            byte f1 = (byte)(from >> 16);
            byte f2 = (byte)(from >> 8);
            byte f3 = (byte)from;

            for (int i = 0; i + 4 <= packet.Length; i++)
            {
                if (packet[i] == f0 && packet[i + 1] == f1 && packet[i + 2] == f2 && packet[i + 3] == f3)
                {
                    packet[i] = (byte)(to >> 24);
                    packet[i + 1] = (byte)(to >> 16);
                    packet[i + 2] = (byte)(to >> 8);
                    packet[i + 3] = (byte)to;
                    i += 3;
                }
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return new byte[0];
            }

            int length = hex.Length / 2;
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
