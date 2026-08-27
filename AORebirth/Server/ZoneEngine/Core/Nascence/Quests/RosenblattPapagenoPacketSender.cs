namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using Utility;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture-backed Papageno QuestFullUpdate / delete packets (20260825-204815).
    /// </summary>
    internal static class RosenblattPapagenoPacketSender
    {
        private const int CapturedCharacterInstance = unchecked((int)0x7A1ADE69);

        private const int MissionDurationSeconds = 0x2760;
        private const long ClientClockBaseSeconds = 1_201_445_827L;

        // Capture 20260825-204815 IN #129 QuestFullUpdate Mission:55B0B8A3 Remove Papageno.
        private const string AcceptQuestFullUpdateHex =
            "1018000A000101D200000DBA7A1ADE69465A40610000C3507A1ADE6901000007E20000DAC355B0B8A30000000F00000000000000000000000252656D6F7665205061706167656E6F000000004C52656D6F7665205061706167656E6F3C42523E3C42523E44722E20526F73656E626C6174742061736B656420796F7520746F2072656D6F7665205061706167656E6F20666F722068696D2E000000C3507A2ED6F800000006000003E800000000000003E8000003F1000003F1000003F1545737560000000000000000575555300000000F000000000000000000000000000000000000C3507A1ADE6900002C420000276000002760000007E20000000100000000000000000000000000000000000111D341504147000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A971ECA000000000000D2FC1C84430700000000000000000000000000000000000000000000000000000000000007E20000C3507A1ADE690000000104844307000000000000000000000006000007E20000C3507A1ADE690000000000018A7C000000000000000000000000000000000000000000000007000003F101";

        private const string CapturedAction59DeleteHex =
            "11FD000A0001003700000DBA7A1ADE695E4777700000C3507A1ADE69000000003B000000000000DAC355B0B8A30000DAC355B0B8A30000";

        private const string CapturedQuestDeleteHex =
            "11FE000A0001003500000DBA7A1ADE69212C487A0000C3507A1ADE690000000001000000000000DAC355B0B8A30000000000000000";

        internal static bool TrySendQuestFullUpdate(ICharacter character)
        {
            return TrySendRaw(character, AcceptQuestFullUpdateHex);
        }

        internal static bool TrySendQuestDelete(ICharacter character)
        {
            if (!CanSend(character))
            {
                return false;
            }

            try
            {
                var client = (ZoneClient)character.Controller.Client;
                byte[] action59 = HexToBytes(CapturedAction59DeleteHex);
                ReplaceInstance(action59, CapturedCharacterInstance, character.Identity.Instance);
                byte[] questDelete = HexToBytes(CapturedQuestDeleteHex);
                ReplaceInstance(questDelete, CapturedCharacterInstance, character.Identity.Instance);
                client.EnqueueOutboundCompressedBuffer(action59);
                client.EnqueueOutboundCompressedBuffer(questDelete);
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ROSENBLATT_PAPAGENO QuestDelete failed: " + exception.Message);
                return false;
            }
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
                LogUtil.Debug(DebugInfoDetail.Error, "ROSENBLATT_PAPAGENO QuestFullUpdate failed: " + exception.Message);
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
