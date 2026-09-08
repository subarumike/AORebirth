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
    internal static partial class DojaChipPacketSender
    {

        /// <summary>Same GameTimeMessage epoch as MissionAcceptService / PerkResetMissionSender.</summary>

        /// <summary>Default turn-in duration when a 6A8x expiry band is present (12 hours).</summary>

        /// <summary>Capture cooldown duration (18 hours).</summary>

        /// <summary>
        /// Raw byte offset of QuestActions[0].UnknownHash1 AbsoluteTime in capture IN #95 (448 bytes).
        /// </summary>


        /// <summary>Capture IN #95 QuestFullUpdate Mission:55AA2803 (448 bytes).</summary>

        /// <summary>
        /// Capture Action59 for Mission:55AA2421 (character 78D84040). Mission instance bytes patched per quest.
        /// Wire Action is Int32=0x3B. Capture packets.hex.log IN #93.
        /// </summary>

        /// <summary>Capture Quest/Delete for Mission:55AA2421. Capture packets.hex.log IN #94.</summary>


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


        private static bool CanSend(ICharacter character)
        {
            return character != null
                   && character.Controller != null
                   && character.Controller.Client is ZoneClient;
        }


    }
}
