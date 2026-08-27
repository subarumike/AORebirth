namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using Utility;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture-backed Papagena QuestFullUpdate / delete packets (20260822-082554).
    /// </summary>
    internal static class RosenblattPapagenaPacketSender
    {
        private const int CapturedCharacterInstance = unchecked((int)0x78D84040);
        private const int CapturedMissionInstance = unchecked((int)0x55AA38B0);

        private const int MissionDurationSeconds = 0x2760;
        private const long ClientClockBaseSeconds = 1_201_445_827L;

        // Capture IN #1139 QuestFullUpdate len=466.
        private const string AcceptQuestFullUpdateHex =
            "04BB000A000101D200000DBD78D84040465A40610000C35078D8404001000007E20000DAC355AA38B00000000F00000000000000000000000252656D6F7665205061706167656E61000000004C52656D6F7665205061706167656E613C42523E3C42523E44722E20526F73656E626C6174742061736B656420796F7520746F2072656D6F7665205061706167656E6120666F722068696D2E000000C3507A18D41900000006000003E800000000000003E8000003F1000003F1000003F154575538000000000000000036524F390000003C000000000000000000000000000000000000C35078D8404000002C420000276000002760000007E20000000100000000000000000000000000000000000111D350415041000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A927D1F000000000000D2FC1C80C14200000000000000000000000000000000000000000000000000000000000007E20000C35078D84040000000010480C142000000000000000000000006000007E20000C35078D840400000000000018A7D000000000000000000000000000000000000000000000007000003F101";

        private const string CapturedAction59DeleteHex =
            "05A7000A0001003700000DBD78D840405E4777700000C35078D84040000000003B000000000000DAC355AA38B00000DAC355AA38B0000";

        private const string CapturedQuestDeleteHex =
            "05A8000A0001003500000DBD78D84040212C487A0000C35078D840400000000001000000000000DAC355AA38B0000000000000000";

        internal static bool TrySendQuestFullUpdate(ICharacter character)
        {
            return TrySendRaw(character, AcceptQuestFullUpdateHex);
        }

        internal static bool TrySendQuestDelete(ICharacter character)
        {
            // Reuse proven Hiathlin Action59/QuestDelete wire; substitute Mission:55AA38B0.
            return RosenblattHiathlinPacketSender.TrySendQuestDelete(
                character,
                RosenblattPapagenaInteractionRules.QuestId);
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
                LogUtil.Debug(DebugInfoDetail.Error, "ROSENBLATT_PAPAGENA QuestFullUpdate failed: " + exception.Message);
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
