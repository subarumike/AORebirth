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
    /// Capture-backed Donna Red QuestFullUpdate + Ancient Device grant (20260822-224319).
    /// </summary>
    internal static class NascenceLifeDonnaRedPacketSender
    {
        private const int CapturedCharacterInstance = unchecked((int)0x7A1ADE69);

        /// <summary>Capture QFU duration 0xA8C0 = 43200 seconds (12 hours).</summary>
        private const int MissionDurationSeconds = 0xA8C0;

        private const long ClientClockBaseSeconds = 1_201_445_827L;

        private const float GameTimeUnknown1 = 30024.0f;
        private const int GameTimeUnknown3 = 185408;
        private const float GameTimeUnknown4 = 80183.3125f;

        // Capture raw-packets QuestFullUpdate Mission:55ABAD4D.
        private const string AcceptQuestFullUpdateHex =
            "01DD000A0001032000000DBD7A1ADE69465A40610000C3507A1ADE6901000007E20000DAC355ABAD4D0000000F000000000000000000000002596F752061677265656420746F2066696E6420696E666F726D6174692E2E2E000000018A596F752061677265656420746F2066696E6420696E666F726D6174696F6E2061626F75742074686520616E6369656E74206C6F6F6B696E672064657669636520676976656E20746F20796F7520627920446F6E6E61205265642E3C42523E3C42523E536865206D656E74696F6E6564206120736574746C656D656E742065617374206F662068657220706F736974696F6E2E204D6179626520736F6D6520636C7565732063616E20626520666F756E6420696E20686572206B6E6F776C656467652061626F7574204E617363656E6365206F722074686520616E6369656E742073796D626F6C7320736865206973207265736561726368696E672E3C42523E3C42523E3C666F6E7420636F6C6F723D2223443630303030223E4E6F74653A205072657373205020746F206F70656E2074686520776F726C64206D61702E203C42523E4C6F6F6B20666F72206120706C61636520696E20736F7574682D65617374204E617363656E63652063616C6C65642052656465656D65642056696C6C6167652E3C2F666F6E743E000000C3507A18D4B100000006000000000000000000000000000003F1000003F1000003F15642365200000000000000000000000000000000000000000000000000000000000000000000C3507A1ADE690003BC520000A8C00000A8C0000007E20000001800000000000000000000000000000000000111D3000186C7000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006AB19840000000000000D2F14D88C09900009C50000010D8000186A0000186A044ECA000428A0000442CC000000007E20000C3507A1ADE69000000010588C099000000000000000000000008000007E20000C3507A1ADE6900000000000186C7000000000000000000000000000000000000000000000007000003F101";

        // Capture raw-packets #403 TemplateAction Ancient Device 214998 OverflowWindow.
        private const string AncientDeviceTemplateActionHex =
            "01E5000A0001004100000DBD7A1ADE69355056440000C3507A1ADE6900000347D6000347D60000000100000001000000570000006E000000000000000000000000";

        // Capture raw-packets #404 ContainerAddItem OverflowWindow slot 0x6F.
        private const string AncientDeviceContainerAddHex =
            "01E6000A0001003100000DBD7A1ADE6947537A240000C3507A1ADE69000000006E000000000000006E7A1ADE690000006F";

        internal static bool TrySendQuestFullUpdate(ICharacter character)
        {
            if (!CanSend(character))
            {
                return false;
            }

            try
            {
                var client = (ZoneClient)character.Controller.Client;
                ReanchorGameTime(character, client);
                byte[] buffer = HexToBytes(AcceptQuestFullUpdateHex);
                ReplaceInstance(buffer, CapturedCharacterInstance, character.Identity.Instance);
                PatchMissionExpiryBand(buffer, client, MissionDurationSeconds);
                client.EnqueueOutboundCompressedBuffer(buffer);
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_DONNA_RED QuestFullUpdate failed: " + exception.Message);
                return false;
            }
        }

        internal static bool TrySendAncientDeviceGrant(ICharacter character)
        {
            if (!CanSend(character))
            {
                return false;
            }

            try
            {
                var client = (ZoneClient)character.Controller.Client;
                byte[] templateAction = HexToBytes(AncientDeviceTemplateActionHex);
                byte[] containerAdd = HexToBytes(AncientDeviceContainerAddHex);
                ReplaceInstance(templateAction, CapturedCharacterInstance, character.Identity.Instance);
                ReplaceInstance(containerAdd, CapturedCharacterInstance, character.Identity.Instance);
                client.EnqueueOutboundCompressedBuffer(templateAction);
                client.EnqueueOutboundCompressedBuffer(containerAdd);
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_DONNA_RED device grant packets failed: " + exception.Message);
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
        /// Rewrite AbsoluteTime expiry. Capture stamps span 0x6A80xxxx..0x6AB1xxxx (Donna 0x6AB19840).
        /// </summary>
        private static void PatchMissionExpiryBand(byte[] packet, ZoneClient client, int durationSeconds)
        {
            if (packet == null || client == null || durationSeconds <= 0)
            {
                return;
            }

            double secondsSinceSync = (DateTime.UtcNow - client.LastGameTimeSyncUtc).TotalSeconds;
            if (secondsSinceSync < 0)
            {
                secondsSinceSync = 0;
            }

            if (secondsSinceSync > durationSeconds)
            {
                secondsSinceSync = 0;
                client.LastGameTimeSyncUtc = DateTime.UtcNow;
            }

            long clientClockNow = ClientClockBaseSeconds + (long)secondsSinceSync;
            long expiry = clientClockNow + durationSeconds;

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
                   && character.Controller.Client != null
                   && character.Controller.Client is ZoneClient;
        }

        private static void ReplaceInstance(byte[] buffer, int fromInstance, int toInstance)
        {
            if (buffer == null || fromInstance == toInstance)
            {
                return;
            }

            for (int i = 0; i + 3 < buffer.Length; i++)
            {
                int value = (buffer[i] << 24) | (buffer[i + 1] << 16) | (buffer[i + 2] << 8) | buffer[i + 3];
                if (value != fromInstance)
                {
                    continue;
                }

                buffer[i] = (byte)((toInstance >> 24) & 0xFF);
                buffer[i + 1] = (byte)((toInstance >> 16) & 0xFF);
                buffer[i + 2] = (byte)((toInstance >> 8) & 0xFF);
                buffer[i + 3] = (byte)(toInstance & 0xFF);
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || (hex.Length % 2) != 0)
            {
                throw new ArgumentException("hex");
            }

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
