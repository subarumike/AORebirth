namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using Utility;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture-backed Cascading Spirit QuestFullUpdate / delete packets (20260822-083345).
    /// </summary>
    internal static class RosenblattCascadingSpiritPacketSender
    {
        private const int CapturedCharacterInstance = unchecked((int)0x78D84040);
        private const int CapturedMissionInstance = unchecked((int)0x55AA38B5);

        private const int MissionDurationSeconds = 0x2760;
        private const long ClientClockBaseSeconds = 1_201_445_827L;

        // Capture IN #366 QuestFullUpdate len=694.
        private const string AcceptQuestFullUpdateHex =
            "07B3000A000102B600000DBD78D84040465A40610000C35078D8404001000007E20000DAC355AA38B50000000F000000000000000000000002436F6C6C65637420457373656E6365206F6620746865206861756E7465642E0000000120436F6C6C65637420457373656E6365206F6620746865206861756E7465642E3C42523E3C42523E44722E20526F73656E626C6174742061736B656420796F7520746F20636F6C6C65637420457373656E6365206F6620746865206861756E7465642C20736F6D657468696E672062656C6F6E67696E6720746F2074686520436173636164696E6720537069726974732E3C42523E3C42523E3C666F6E7420636F6C6F723D2223393039303930223E4B696C6C206120436173636164696E67205370697269742E3C2F666F6E743E3C42523E3C666F6E7420636F6C6F723D2223464646464646223E44656C6976657220457373656E6365206F6620746865204861756E74656420746F2044722E20526F73656E626C6174742E3C2F666F6E743E000000C3507A18D41900000006000003E800000000000003E8000003F1000003F1000003F14749594C00000000000000004D37314E0000003C000000000000000000000000000000000000C35078D8404000026ADD0000276000002760000007E200000006000111D345454F480000000000000000000111D344525253000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A927E3D000000000000D2F14D879A5600000000000000000000000000000000000000000000000000000000000007E20000C35078D840400000000105879A56000000000000000000000006000007E20000C35078D840400000000000018B34000000000000000000000000000000000000000000000007000003F101";

        private const string CapturedAction59DeleteHex =
            "0859000A0001003700000DBD78D840405E4777700000C35078D84040000000003B000000000000DAC355AA38B50000DAC355AA38B50000";

        private const string CapturedQuestDeleteHex =
            "085A000A0001003500000DBD78D84040212C487A0000C35078D840400000000001000000000000DAC355AA38B50000000000000000";

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

                // Capture 20260822-083345 IN #418/#419 — exact Action59 + QuestDelete for Mission:55AA38B5.
                byte[] action59 = HexToBytes(CapturedAction59DeleteHex);
                ReplaceInstance(action59, CapturedCharacterInstance, character.Identity.Instance);
                byte[] questDelete = HexToBytes(CapturedQuestDeleteHex);
                ReplaceInstance(questDelete, CapturedCharacterInstance, character.Identity.Instance);
                client.EnqueueOutboundCompressedBuffer(action59);
                client.EnqueueOutboundCompressedBuffer(questDelete);

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ROSENBLATT_CASCADING QuestDelete char=" + character.Identity.Instance.ToString("X8"));
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ROSENBLATT_CASCADING QuestDelete failed: " + exception.Message);
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
                LogUtil.Debug(DebugInfoDetail.Error, "ROSENBLATT_CASCADING QuestFullUpdate failed: " + exception.Message);
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
