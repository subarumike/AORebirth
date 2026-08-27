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
    /// Capture-backed Rodriguez Donna Red QuestFullUpdate + Bracer of Jobe grant
    /// (20260822-221109 QFU; 20260825-155929 TemplateAction/ContainerAdd Overflow).
    /// </summary>
    internal static class NascenceLifeRodriguezPacketSender
    {
        private const int CapturedCharacterInstance = unchecked((int)0x7A1ADE69);
        private const int CapturedMissionInstance = unchecked((int)0x55ABF001);

        private const int MissionDurationSeconds = 0x1A4;

        private const long ClientClockBaseSeconds = 1_201_445_827L;

        private const float GameTimeUnknown1 = 30024.0f;
        private const int GameTimeUnknown3 = 185408;
        private const float GameTimeUnknown4 = 80183.3125f;

        private const string AcceptQuestFullUpdateHex =
            "00BF000A0001025700000DB67A1ADE69465A40610000C3507A1ADE6901000007E20000DAC355ABF0010000000F00000000000000000000000254616C6B20746F20446F6E6E612052656400000000CF54616C6B20746F20446F6E6E61205265643C42523E3C42523E4472616B6520746F6C6420796F7520746F2074616C6B20746F20446F6E6E61205265642E205368652077696C6C20646F20736F6D652072656164696E67732066726F6D20796F75722042726163657220746F206576616C7561746520796F757220706572666F726D616E636520696E20746865204E617363656E636520656E7669726F6E6D656E742E3C62723E3C62723E506572686170732073686520616C736F206861732061207461736B20666F7220796F753F000000C3507A1E3C2400000006000000000000000000000000000003F1000003F1000003F13459344A00000000000000000000000000000000000000000000000000000000000000000000C3507A1ADE690003BC52000001A4000001A4000007E20000001700000000000000000000000000000000000111D3534E4441000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A8A6588000000000000D2F14D8958A100009C50000010D6000186A0000186A04476C0000000000044DBE000000007E20000C3507A1ADE6900000001058958A1000000000000000000000006000007E20000C3507A1ADE690000000000019B02000000000000000000000000000000000000000000000007000003F101";

        private const string CapturedAction59DeleteHex =
            "05A7000A0001003700000DBD78D840405E4777700000C35078D84040000000003B000000000000DAC355ABF0010000DAC355ABF0010000";

        private const string CapturedQuestDeleteHex =
            "05A8000A0001003500000DBD78D84040212C487A0000C35078D840400000000001000000000000DAC355ABF0010000000000000000";

        internal static bool TrySendQuestFullUpdate(ICharacter character)
        {
            return TrySendRaw(character, AcceptQuestFullUpdateHex, MissionDurationSeconds);
        }

        /// <summary>
        /// Capture 20260825-155929 after [MARK] Bracer: TemplateAction 223762 + ContainerAdd Overflow slot 111.
        /// Server inventory must already hold the item (TryGrantQuestRewardItem).
        /// </summary>
        internal static bool TrySendBracerGrant(ICharacter character)
        {
            if (!CanSend(character))
            {
                return false;
            }

            try
            {
                int itemId = NascenceLifeRodriguezInteractionRules.BracerItemId;
                int quality = NascenceLifeRodriguezInteractionRules.BracerQuality;
                character.Send(
                    new TemplateActionMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        ItemLowId = itemId,
                        ItemHighId = itemId,
                        Quality = quality,
                        Unknown1 = NascenceLifeRodriguezInteractionRules.BracerTemplateActionUnknown1,
                        Unknown2 = NascenceLifeRodriguezInteractionRules.BracerTemplateActionUnknown2,
                        Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                        Unknown3 = 0,
                        Unknown4 = 0
                    });
                character.Send(
                    new ContainerAddItemMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                        Target = new Identity
                                 {
                                     Type = IdentityType.OverflowWindow,
                                     Instance = character.Identity.Instance
                                 },
                        TargetPlacement = NascenceLifeRodriguezInteractionRules.BracerOverflowSlot
                    });
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "NASCENCE_LIFE_RODRIGUEZ bracer grant failed: " + exception.Message);
                return false;
            }
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
                ReplaceInstance(action59, CapturedMissionInstance, CapturedMissionInstance);
                byte[] questDelete = HexToBytes(CapturedQuestDeleteHex);
                ReplaceInstance(questDelete, CapturedCharacterInstance, character.Identity.Instance);
                ReplaceInstance(questDelete, CapturedMissionInstance, CapturedMissionInstance);
                client.EnqueueOutboundCompressedBuffer(action59);
                client.EnqueueOutboundCompressedBuffer(questDelete);

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_RODRIGUEZ QuestDelete char=" + character.Identity.Instance.ToString("X8"));
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "NASCENCE_LIFE_RODRIGUEZ QuestDelete failed: " + exception.Message);
                return false;
            }
        }

        private static bool TrySendRaw(ICharacter character, string hex, int durationSeconds)
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
                PatchMissionExpiryBand(packet, client, durationSeconds);
                client.EnqueueOutboundCompressedBuffer(packet);
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "NASCENCE_LIFE_RODRIGUEZ QuestFullUpdate failed: " + exception.Message);
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
