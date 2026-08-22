namespace ZoneEngine.Core.Doja
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture-backed Nascense DOJA QuestFullUpdate packets (20260821-222107).
    /// Patches recipient SimpleChar instance and mission expiry, then enqueues raw bytes.
    /// </summary>
    internal static class DojaChipPacketSender
    {
        private const int CapturedCharacterInstance = unchecked((int)0x78D84040);
        private const int MissionIdentityType = 0x0000DAC3;

        /// <summary>Same GameTimeMessage epoch as MissionAcceptService / PerkResetMissionSender.</summary>
        private const long ClientClockBaseSeconds = 1_201_445_827L;

        /// <summary>Default turn-in duration when a 6A8x expiry band is present (12 hours).</summary>
        private const int TurnInMissionDurationSeconds = 12 * 60 * 60;

        /// <summary>Capture cooldown duration (18 hours).</summary>
        private const int CooldownMissionDurationSeconds = 18 * 60 * 60;

        /// <summary>
        /// Raw byte offset of QuestActions[0].UnknownHash1 AbsoluteTime in capture IN #95 (448 bytes).
        /// </summary>
        private const int CooldownExpiryOffset = 323;

        private const string TurnInQuestFullUpdateHex =
            "00D6000A000102A100000DB478D84040465A40610000C35078D8404001000007E20000DAC355AA24210000000F000000000000000000000002596F7527766520666F756E64206120444F4A412D436869700000000102596F7527766520666F756E64206120444F4A412D436869703C42523E596F75206861766520666F756E642061207069656365206F662068617264776172652075736564206279204A4F4245206669656C6420736369656E746973747320746F2074616720616E6420747261636B2063726561747572657320696E2074686520536861646F776C616E64732E20596F75276C6C20626520726577617264656420666F722072657475726E696E6720746865206368697020746F2074686520776F6D616E2077686F7365207369676E617475726520697320696D7072696E746564206F6E2074686520636869702C2022536361726C6574742044616C7175697374222E000000C35078D8404000000006000000000000000000000000000003F1000003F1000007E200045BAC00045BAC00000001000000005847434B0000000000000000335452380000003C000000000000000000000000000000000000C35078D840400003BC520000000000000000000007E20000001800000000000000000000000000000000000111D3000196BA0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D861E3C00009C5000000FA1000186A0000186A0446940000000000044630000000007E20000C35078D840400000000105861E3C000000000000000000000006000007E20000C35078D8404000000000000196BA000000000000000000000000000000000000000000000007000003F101";

        /// <summary>Capture IN #95 QuestFullUpdate Mission:55AA2803 (448 bytes).</summary>
        private const string CooldownQuestFullUpdateHex =
            "003E000A000101C000000DB878D84040465A40610000C35078D8404001000007E20000DAC355AA28030000000F000000000000000000000402596F752063616E206F6E6C79207475726E20696E206F6E6520444F4A2E2E2E000000002A596F752063616E206F6E6C79207475726E20696E206F6E6520444F4A41206368697020746F6461792E000000C35078D8404000000006000000000000000000000000000003F1000003F1000003F13654464200000000000000000000000000000000000000000000000000000000000000000000C35078D840400003BC520000043800000438000007E20000001800000000000000000000000000000000000111D3000196D4000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A89B128000000000000D2F14D87A81500000000000000000000000000000000000000000000000000000000000007E20000C35078D84040000000010587A815000000000000000000000006000007E20000C35078D8404000000000000196D4000000000000000000000000000000000000000000000007000003F101";

        /// <summary>
        /// Capture Action59 for Mission:55AA2421 (character 78D84040). Mission instance bytes patched per quest.
        /// Wire Action is Int32=0x3B. Capture packets.hex.log IN #93.
        /// </summary>
        private const string CapturedAction59DeleteHex =
            "003C000A0001003700000DB878D840405E4777700000C35078D84040000000003B000000000000DAC355AA24210000DAC355AA24210000";

        /// <summary>Capture Quest/Delete for Mission:55AA2421. Capture packets.hex.log IN #94.</summary>
        private const string CapturedQuestDeleteHex =
            "003D000A0001003500000DB878D84040212C487A0000C35078D840400000000001000000000000DAC355AA24210000000000000000";

        private const int CapturedDeleteMissionInstance = unchecked((int)0x55AA2421);

        internal static bool TrySendQuestFullUpdate(ICharacter character, string questId)
        {
            return TrySendQuestFullUpdate(character, questId, ResolveDurationSeconds(questId));
        }

        /// <summary>
        /// Send capture QFU. For cooldown, <paramref name="remainingSeconds"/> drives Remain
        /// (login/zone resync uses elapsed cooldown, not a fresh 18h).
        /// </summary>
        internal static bool TrySendQuestFullUpdate(ICharacter character, string questId, int remainingSeconds)
        {
            string hex = ResolveHex(questId);
            if (string.IsNullOrEmpty(hex))
            {
                return false;
            }

            if (remainingSeconds <= 0)
            {
                remainingSeconds = ResolveDurationSeconds(questId);
            }

            bool isCooldown = string.Equals(
                questId,
                DojaChipInteractionRules.QuestCooldown,
                StringComparison.OrdinalIgnoreCase);
            if (isCooldown && remainingSeconds > CooldownMissionDurationSeconds)
            {
                remainingSeconds = CooldownMissionDurationSeconds;
            }

            return TrySendRaw(character, hex, remainingSeconds, isCooldown);
        }

        /// <summary>
        /// Capture-backed journal delete: Action59 then Quest/Delete hex only
        /// (capture 20260821-222107 IN #93–#94). Do not also send typed QuestMessage —
        /// that triple-delete crashed the client after Scarlett trade.
        /// </summary>
        internal static bool TrySendQuestDelete(ICharacter character, string questId)
        {
            int instance;
            if (!TryResolveMissionInstance(questId, out instance) || !CanSend(character))
            {
                return false;
            }

            try
            {
                var client = (ZoneClient)character.Controller.Client;

                byte[] action59 = HexToBytes(CapturedAction59DeleteHex);
                ReplaceInstance(action59, CapturedCharacterInstance, character.Identity.Instance);
                ReplaceInstance(action59, CapturedDeleteMissionInstance, instance);
                byte[] questDelete = HexToBytes(CapturedQuestDeleteHex);
                ReplaceInstance(questDelete, CapturedCharacterInstance, character.Identity.Instance);
                ReplaceInstance(questDelete, CapturedDeleteMissionInstance, instance);
                client.EnqueueOutboundCompressedBuffer(action59);
                client.EnqueueOutboundCompressedBuffer(questDelete);

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "DOJA_NASCENSE QuestDelete quest=" + questId
                    + " char=" + character.Identity.Instance.ToString("X8"));
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "DOJA_NASCENSE QuestDelete failed: " + exception.Message);
                return false;
            }
        }

        private static string ResolveHex(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return null;
            }

            if (string.Equals(questId, DojaChipInteractionRules.QuestTurnIn, StringComparison.OrdinalIgnoreCase))
            {
                return TurnInQuestFullUpdateHex;
            }

            if (string.Equals(questId, DojaChipInteractionRules.QuestCooldown, StringComparison.OrdinalIgnoreCase))
            {
                return CooldownQuestFullUpdateHex;
            }

            return null;
        }

        private static int ResolveDurationSeconds(string questId)
        {
            if (string.Equals(questId, DojaChipInteractionRules.QuestCooldown, StringComparison.OrdinalIgnoreCase))
            {
                return CooldownMissionDurationSeconds;
            }

            return TurnInMissionDurationSeconds;
        }

        private static bool TryResolveMissionInstance(string questId, out int instance)
        {
            instance = 0;
            if (string.IsNullOrWhiteSpace(questId))
            {
                return false;
            }

            string normalized = questId.Trim();
            int colon = normalized.LastIndexOf(':');
            string hex = colon >= 0 ? normalized.Substring(colon + 1) : normalized;
            return int.TryParse(
                hex,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out instance);
        }

        private static bool TrySendRaw(ICharacter character, string hex, int durationSeconds, bool isCooldown)
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
                if (isCooldown)
                {
                    PatchCooldownExpiry(packet, client, durationSeconds);
                }
                else
                {
                    PatchMissionExpiryBand(packet, client, durationSeconds);
                }

                client.EnqueueOutboundCompressedBuffer(packet);
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "DOJA_NASCENSE QuestFullUpdate failed: " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// PerkReset-style fixed-offset AbsoluteTime patch so Remain shows exactly 18h.
        /// </summary>
        private static void PatchCooldownExpiry(byte[] packet, ZoneClient client, int durationSeconds)
        {
            if (packet == null
                || client == null
                || durationSeconds <= 0
                || packet.Length < CooldownExpiryOffset + 4)
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
            WriteInt32BigEndian(packet, CooldownExpiryOffset, (int)expiry);
        }

        /// <summary>
        /// Rewrite AbsoluteTime expiry in the 0x6A80xxxx..0x6A8Fxxxx band when present.
        /// Turn-in QFU has no AbsoluteTime band — skip silently.
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
                if (band < unchecked((int)0x6A800000) || band > unchecked((int)0x6A8F0000))
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
