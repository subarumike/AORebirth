namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture-backed Falker kill QuestFullUpdate packets (20260822-221109, raw-packets ordinals 357/358).
    /// </summary>
    internal static class NascenceLifeJoshuaFalkerPacketSender
    {
        private const int CapturedCharacterInstance = unchecked((int)0x7A1ADE69);
        private const int CapturedSilvertailMissionInstance = unchecked((int)0x55ABAD28);
        private const int CapturedChimeraMissionInstance = unchecked((int)0x55ABAD29);

        private const int MissionDurationSeconds = 0x2760;

        private const long ClientClockBaseSeconds = 1_201_445_827L;

        private const float GameTimeUnknown1 = 30024.0f;
        private const int GameTimeUnknown3 = 185408;
        private const float GameTimeUnknown4 = 80183.3125f;

        private const string SilvertailQuestFullUpdateHex =
            "008D000A0001026700000DBD7A1ADE69465A40610000C3507A1ADE6901000007E20000DAC355ABAD280000000F000000000000000000000002526564756365206E756D626572206F662073696C7665727461696C732E00000000C3526564756365206E756D626572206F662073696C7665727461696C732E3C42523E3C42523E536369656E74697374204A6F736875612046616C6B65722077616E747320796F7520746F206173736973742068696D207265647563696E6720746865206E756D626572206F662053776966742053696C7665727461696C7320696E2074686520617265612C20616E64206861732061736B656420796F7520746F206B696C6C2031302053776966742053696C7665727461696C7320666F722068696D2E000000C3507A18D42400000006000000000000000000000000000003F1000003F1000007E2000354EF000354F00000000700000000583249500000000000000000424C425100000007000000000000000000000000000000000000C3507A1ADE6900002C420000276000002760000007E200000014000000000000000000000000000000000000000000000000000000005357534900000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A933DD2000000000000D2FC1C80C3C300000000000000000000000000000000000000000000000000000000000007E20000C3507A1ADE69000000010480C3C3000000000000000000000008000007E20000C3507A1ADE690000000A00018A7A000000000000000000000000000000000000000000000007000003F101";

        private const string ChimeraQuestFullUpdateHex =
            "008E000A0001024400000DBD7A1ADE69465A40610000C3507A1ADE6901000007E20000DAC355ABAD290000000F000000000000000000000002526564756365206E756D626572206F66204368696D657261732E00000000A3526564756365206E756D626572206F66204368696D657261732E3C42523E3C42523E536369656E74697374204A6F736875612046616C6B65722068617320726571756573746564207468617420796F752068656C702068696D2072656475636520746865206E756D626572206F66204261726B696E67204368696D6572617320696E204E617363656E7365206279206B696C6C696E67203130206F66207468656D2E000000C3507A18D42400000006000000000000000000000000000003F1000003F1000007E2000356C6000356C7000000070000000038324233000000000000000036314C4700000007000000000000000000000000000000000000C3507A1ADE6900002C420000276000002760000007E200000014000000000000000000000000000000000000000000000000000000004C4C424500000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A933DD2000000000000D2FC1C80C3C400000000000000000000000000000000000000000000000000000000000007E20000C3507A1ADE69000000010480C3C4000000000000000000000008000007E20000C3507A1ADE690000000A00018A7B000000000000000000000000000000000000000000000007000003F101";

        private const string CapturedAction59DeleteHex =
            "05A7000A0001003700000DBD78D840405E4777700000C35078D84040000000003B000000000000DAC355ABAD280000DAC355ABAD280000";

        private const string CapturedQuestDeleteHex =
            "05A8000A0001003500000DBD78D84040212C487A0000C35078D840400000000001000000000000DAC355ABAD2800000000000000";

        internal static bool TrySendSilvertailQuestFullUpdate(ICharacter character, int killsDone = 0)
        {
            return TrySendRaw(character, SilvertailQuestFullUpdateHex, killsDone);
        }

        internal static bool TrySendChimeraQuestFullUpdate(ICharacter character, int killsDone = 0)
        {
            return TrySendRaw(character, ChimeraQuestFullUpdateHex, killsDone);
        }

        internal static bool TrySendSilvertailQuestDelete(ICharacter character)
        {
            return TrySendQuestDelete(character, CapturedSilvertailMissionInstance);
        }

        internal static bool TrySendChimeraQuestDelete(ICharacter character)
        {
            return TrySendQuestDelete(character, CapturedChimeraMissionInstance);
        }

        private static bool TrySendQuestDelete(ICharacter character, int missionInstance)
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
                ReplaceInstance(action59, CapturedSilvertailMissionInstance, missionInstance);
                byte[] questDelete = HexToBytes(CapturedQuestDeleteHex);
                ReplaceInstance(questDelete, CapturedCharacterInstance, character.Identity.Instance);
                ReplaceInstance(questDelete, CapturedSilvertailMissionInstance, missionInstance);
                client.EnqueueOutboundCompressedBuffer(action59);
                client.EnqueueOutboundCompressedBuffer(questDelete);

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_FALKER QuestDelete mission=" + missionInstance.ToString("X8")
                    + " char=" + character.Identity.Instance.ToString("X8"));
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "NASCENCE_LIFE_FALKER QuestDelete failed: " + exception.Message);
                return false;
            }
        }

        private static bool TrySendRaw(ICharacter character, string hex, int killsDone)
        {
            if (!CanSend(character) || string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            try
            {
                var client = character.Controller.Client as ZoneClient;
                ReanchorGameTime(character, client);
                byte[] packet = HexToBytes(hex);
                ReplaceInstance(packet, CapturedCharacterInstance, character.Identity.Instance);
                PatchKillQuestRemain(packet, killsDone);
                PatchMissionExpiryBand(packet, client);
                client.EnqueueOutboundCompressedBuffer(packet);
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "NASCENCE_LIFE_FALKER QuestFullUpdate failed: " + exception.Message);
                return false;
            }
        }

        private static void ReanchorGameTime(ICharacter character, ZoneClient client)
        {
            if (character == null || client == null)
            {
                return;
            }

            client.SendCompressed(
                new GameTimeMessage
                {
                    Identity =
                        new Identity
                        {
                            Type = IdentityType.CanbeAffected,
                            Instance = character.Identity.Instance
                        },
                    Unknown1 = GameTimeUnknown1,
                    Unknown3 = GameTimeUnknown3,
                    Unknown4 = GameTimeUnknown4
                });
            client.LastGameTimeSyncUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Capture QFU tail Unknown23 holds required kill count; patch to remaining kills for journal Remain column.
        /// </summary>
        private static void PatchKillQuestRemain(byte[] packet, int killsDone)
        {
            if (packet == null)
            {
                return;
            }

            int required = NascenceLifeJoshuaFalkerInteractionRules.RequiredKills;
            int remaining = Math.Max(0, required - Math.Max(0, killsDone));

            for (int i = 0; i + 8 <= packet.Length; i++)
            {
                if (packet[i] != 0x00
                    || packet[i + 1] != 0x00
                    || packet[i + 2] != 0x00
                    || packet[i + 3] != 0x0A
                    || packet[i + 4] != 0x00
                    || packet[i + 5] != 0x01
                    || packet[i + 6] != 0x8A
                    || packet[i + 7] != 0x7A && packet[i + 7] != 0x7B)
                {
                    continue;
                }

                WriteInt32BigEndian(packet, i, remaining);
                return;
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
                // Capture stamps include 0x6A93xxxx (Falker) and 0x6AB1xxxx (Donna); old 0x6A92 cap missed them.
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
            int length = hex.Length / 2;
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
