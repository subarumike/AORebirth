namespace ZoneEngine.Core.Perks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;
    using AORebirth.Stats;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// Capture-backed TrainPerk / UsePerk / AddPerkAction runtime (20260715-194155).
    /// UsePerk executes the action item OnUse script (CastNano/LockPerk/Hit/SystemText/...).
    /// Attack perk actions (Quick Bash): OnUser cast/lock, OnTarget SpecialHit — wire Target is often self.
    /// </summary>
    public sealed class PerkRuntimeService
    {
        public static readonly PerkRuntimeService Default = new PerkRuntimeService();

        private const int QueuePerkParameter1 = 2;

        private const int QueuePerkParameter2 = 100;

        /// <summary>
        /// Fallback cooldown when action item has no LockPerk (capture Channel Rage ~750ms).
        /// </summary>
        private const int FallbackPerkAvailableDelayMilliseconds = 750;

        /// <summary>
        /// Free full reset cooldown (live Perk-Reset Service Provider / AO-Universe: 2 days).
        /// </summary>
        public const int FullPerkResetCooldownSeconds = 2 * 24 * 60 * 60;

        /// <summary>
        /// Early reset fee while cooldown mission/timer is active. Capture 20260716-Reset-perks FinishTrade Amount.
        /// </summary>
        public const int EarlyFullPerkResetCreditCost = 20000000;

        public bool IsFullPerkResetFree(Character character)
        {
            if (character == null)
            {
                return false;
            }

            int lastResetUnix = (int)character.Stats[StatIds.lastperkresettime].BaseValue;
            if (lastResetUnix <= 0)
            {
                return true;
            }

            long nowUnix = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            return nowUnix - lastResetUnix >= FullPerkResetCooldownSeconds;
        }

        /// <summary>
        /// Seconds left on the free-reset cooldown (0 when free / never reset). Drives the mission timer
        /// and lets the cooldown mission be re-sent on login/zone from the persisted lastperkresettime.
        /// </summary>
        public int GetFullPerkResetCooldownRemainingSeconds(Character character)
        {
            if (character == null)
            {
                return 0;
            }

            int lastResetUnix = (int)character.Stats[StatIds.lastperkresettime].BaseValue;
            if (lastResetUnix <= 0)
            {
                return 0;
            }

            long nowUnix = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            long remaining = FullPerkResetCooldownSeconds - (nowUnix - lastResetUnix);
            if (remaining <= 0)
            {
                return 0;
            }

            return remaining > int.MaxValue ? int.MaxValue : (int)remaining;
        }

        /// <summary>
        /// Clears all trained perks (DB + client RemovePerkAction / ClearAllPerks). Capture 20260716-Reset-perks.
        /// </summary>
        public bool TryResetAllPerks(Character character, bool chargeEarlyFee)
        {
            if (character == null || character.Controller == null || character.Controller.Client == null)
            {
                return false;
            }

            character.EnsureTrainedPerks();
            bool free = this.IsFullPerkResetFree(character);
            if (!free && !chargeEarlyFee)
            {
                return false;
            }

            if (!free && chargeEarlyFee)
            {
                int cashBefore = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
                if (cashBefore < EarlyFullPerkResetCreditCost)
                {
                    return false;
                }

                int cashAfter = CashStatRules.Clamp((long)cashBefore - EarlyFullPerkResetCreditCost);
                character.Stats[StatIds.cash].Set((uint)cashAfter);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)cashAfter);
            }

            List<int> trained = character.TrainedPerkPacketIds.ToList();
            foreach (int packetId in trained)
            {
                PerkDefinition def;
                if (PerkCatalog.TryGet(packetId, out def) && def != null && def.GrantsPerkAction)
                {
                    this.SendRemovePerkAction(character, def);
                }
            }

            character.TrainedPerkPacketIds.Clear();
            if (character.LockedPerkPacketIdsUntilUtc != null)
            {
                character.LockedPerkPacketIdsUntilUtc.Clear();
            }

            try
            {
                CharacterPerksDao.Instance.DeleteAllPerks(character.Identity.Instance);
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(ex);
            }

            this.SendClearAllPerks(character);

            int nowUnix =
                (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            character.Stats[StatIds.lastperkresettime].Set((uint)nowUnix);
            StatMessageHandler.Default.SendSingle(
                character,
                (int)StatIds.lastperkresettime,
                (uint)nowUnix);

            // Persist immediately so the cooldown (and its mission timer) survive an instant relog.
            try
            {
                character.Stats.Write();
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(ex);
            }

            PerkResetMissionSender.SendResetCooldownMission(character, FullPerkResetCooldownSeconds);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "PERK_RESET char=" + character.Identity.Instance + " removed=" + trained.Count
                + " paid=" + (!free && chargeEarlyFee));
            return true;
        }

        public bool TryHandleTrainPerk(IZoneClient client, CharacterActionMessage message)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return false;
            }

            Character character = client.Controller.Character as Character;
            if (character == null)
            {
                return false;
            }

            int packetId = message.Parameter2;
            character.EnsureTrainedPerks();
            character.TrainedPerkPacketIds.Add(packetId);
            try
            {
                CharacterPerksDao.Instance.WritePerk(character.Identity.Instance, packetId);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "PERK_PERSIST write char=" + character.Identity.Instance + " packetId=" + packetId);
            }
            catch (System.Exception ex)
            {
                LogUtil.ErrorException(ex);
            }

            PerkDefinition def;
            PerkCatalog.TryGet(packetId, out def);

            if (def != null && def.GrantsPerkAction)
            {
                this.SendAddPerkAction(character, def);
            }

            this.SendTrainPerkAck(character, packetId);
            return true;
        }

        public bool TryHandleUsePerk(IZoneClient client, CharacterActionMessage message)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return false;
            }

            Character character = client.Controller.Character as Character;
            if (character == null)
            {
                return false;
            }

            PerkDefinition def = ResolveUseDefinition(message);
            int lockPacketId = def != null ? def.PacketId : ResolvePacketIdFromUse(message);

            character.EnsureTrainedPerks();
            if (lockPacketId > 0 && !character.TrainedPerkPacketIds.Contains(lockPacketId))
            {
                character.TrainedPerkPacketIds.Add(lockPacketId);
            }

            if (lockPacketId > 0 && character.IsPerkLocked(lockPacketId))
            {
                this.SendPerkUnavailable(character, lockPacketId);
                return true;
            }

            this.SendQueuePerk(character);

            bool ranLockPerk = false;
            if (def != null && def.GrantsPerkAction && def.ActionTemplateId.HasValue)
            {
                ranLockPerk = this.ExecuteActionOnUse(character, def.ActionTemplateId.Value);
            }
            else
            {
                string perkName = def != null && !string.IsNullOrEmpty(def.Name) ? StripTierSuffix(def.Name) : "Perk";
                this.SendPerformFeedback(character, perkName);
            }

            if (!ranLockPerk && lockPacketId > 0)
            {
                character.LockPerkPacket(lockPacketId, 1);
                this.SendPerkUnavailable(character, lockPacketId);
                this.SchedulePerkAvailable(character, lockPacketId, FallbackPerkAvailableDelayMilliseconds);
            }

            return true;
        }

        public void ResendPerkActions(Character character)
        {
            if (character == null)
            {
                return;
            }

            character.EnsureTrainedPerks();
            foreach (int packetId in character.TrainedPerkPacketIds)
            {
                // Capture TrainPerk server echo teaches the client which perks are trained.
                this.SendTrainPerkAck(character, packetId);

                PerkDefinition def;
                if (PerkCatalog.TryGet(packetId, out def) && def.GrantsPerkAction)
                {
                    this.SendAddPerkAction(character, def);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "PERK_PERSIST resync char=" + character.Identity.Instance + " count="
                + character.TrainedPerkPacketIds.Count);

            // Re-send the reset-cooldown mission so its Remain timer survives relog/zone.
            int cooldownRemaining = this.GetFullPerkResetCooldownRemainingSeconds(character);
            if (cooldownRemaining > 0)
            {
                PerkResetMissionSender.SendResetCooldownMission(character, cooldownRemaining);
            }
        }

        /// <summary>
        /// Runs action-item OnUse functions (requirements gated). Returns true if LockPerk was present.
        /// </summary>
        private bool ExecuteActionOnUse(Character character, int actionTemplateId)
        {
            ItemTemplate actionItem;
            if (!ItemLoader.ItemList.TryGetValue(actionTemplateId, out actionItem) || actionItem.Events == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.GameFunctions,
                    "Perk action template missing id=" + actionTemplateId);
                return false;
            }

            // Capture: UsePerk.Target is frequently the caster. Keep selected/fighting mob for OnTarget hits.
            PrepareCombatTarget(character);

            bool lockPerkSeen = false;
            foreach (Event ev in actionItem.Events)
            {
                if (ev.EventType != EventType.OnUse)
                {
                    continue;
                }

                foreach (var function in ev.Functions)
                {
                    if (function.FunctionType == (int)FunctionType.LockPerk)
                    {
                        lockPerkSeen = true;
                    }
                }

                // Requirements + CallFunction via existing Event.Perform path.
                ev.Perform(character, character);
            }

            return lockPerkSeen;
        }

        /// <summary>
        /// Prefer selected/fighting NPC for ItemTarget.Target SpecialHit/Hit (Quick Bash On Target damage).
        /// </summary>
        private static void PrepareCombatTarget(Character character)
        {
            if (character == null)
            {
                return;
            }

            Identity preferred = Identity.None;
            if (character.SelectedTarget.Instance != 0
                && character.SelectedTarget.Instance != character.Identity.Instance)
            {
                preferred = character.SelectedTarget;
            }
            else if (character.FightingTarget.Instance != 0
                     && character.FightingTarget.Instance != character.Identity.Instance)
            {
                preferred = character.FightingTarget;
            }

            if (preferred.Instance != 0)
            {
                character.SetTarget(preferred);
            }
        }

        /// <summary>
        /// Capture UsePerk: Parameter1=slot (often 10000+PacketID), Parameter2=action hash.
        /// Prefer hash so mismatched slots (Blunt Mastery 2 slot 10320) still resolve correctly.
        /// </summary>
        private static PerkDefinition ResolveUseDefinition(CharacterActionMessage message)
        {
            PerkDefinition def;
            if (message.Parameter2 != 0 && PerkCatalog.TryGetByActionHash(message.Parameter2, out def))
            {
                return def;
            }

            int packetId = ResolvePacketIdFromUse(message);
            if (packetId > 0 && PerkCatalog.TryGet(packetId, out def))
            {
                return def;
            }

            return null;
        }

        private static int ResolvePacketIdFromUse(CharacterActionMessage message)
        {
            if (message.Parameter1 >= 10000)
            {
                return message.Parameter1 - 10000;
            }

            return message.Parameter1;
        }

        private static string StripTierSuffix(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            int lastSpace = name.LastIndexOf(' ');
            if (lastSpace <= 0)
            {
                return name;
            }

            string tail = name.Substring(lastSpace + 1);
            int n;
            if (int.TryParse(tail, out n))
            {
                return name.Substring(0, lastSpace);
            }

            return name;
        }

        private void SendAddPerkAction(Character character, PerkDefinition def)
        {
            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.AddPerkAction,
                    Unknown1 = 0,
                    Target = new Identity { Type = IdentityType.None, Instance = def.ActionTemplateId.Value },
                    Parameter1 = def.ActionSlotId,
                    Parameter2 = def.ActionHash.Value,
                    Unknown2 = 0
                });
        }

        private void SendRemovePerkAction(Character character, PerkDefinition def)
        {
            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.RemovePerkAction,
                    Unknown1 = 0,
                    Target = new Identity { Type = IdentityType.None, Instance = def.ActionTemplateId.Value },
                    Parameter1 = def.ActionSlotId,
                    Parameter2 = def.ActionHash.Value,
                    Unknown2 = 0
                });
        }

        private void SendClearAllPerks(Character character)
        {
            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.ClearAllPerks,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = 0,
                    Unknown2 = 0
                });
        }

        private void SendTrainPerkAck(Character character, int packetId)
        {
            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.TrainPerk,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = packetId,
                    Unknown2 = 0
                });
        }

        private void SendQueuePerk(Character character)
        {
            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.QueuePerk,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = QueuePerkParameter1,
                    Parameter2 = QueuePerkParameter2,
                    Unknown2 = 0
                });
        }

        private void SendPerkUnavailable(Character character, int packetId)
        {
            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.PerkUnavailable,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = packetId,
                    Parameter2 = 1,
                    Unknown2 = 0
                });
        }

        private void SendPerkAvailable(Character character, int packetId)
        {
            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.PerkAvailable,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = packetId,
                    Unknown2 = 0
                });
        }

        private void SendPerformFeedback(Character character, string perkActionName)
        {
            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = "~&!!!\":!!!)<s'You successfully perform " + perkActionName + ".",
                    Unknown2 = 0
                });
        }

        private void SchedulePerkAvailable(Character character, int packetId, int delayMs)
        {
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(delayMs);
                    if (character == null || character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    this.SendPerkAvailable(character, packetId);
                });
        }
    }
}
