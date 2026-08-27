namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Capture-backed Dr. Rosenblatt Cascading Spirit quest runtime (20260822-083345).
    /// </summary>
    internal static class RosenblattCascadingSpiritQuestRuntime
    {
        private static readonly System.Collections.Generic.HashSet<int> ChimeraDiscTradedCharacters =
            new System.Collections.Generic.HashSet<int>();

        internal static bool CanOfferDiscTrade(ICharacter source)
        {
            if (source == null || !HasChimeraDatadisc(source))
            {
                return false;
            }

            if (MissionRuntime.IsInitialized && IsMissionActive(source))
            {
                return false;
            }

            if (MissionRuntime.IsInitialized
                && (IsMissionCompleted(source) || IsRewardGranted(source)))
            {
                return false;
            }

            return true;
        }

        internal static bool IsMissionActive(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(
                source.Identity.Instance,
                RosenblattCascadingSpiritInteractionRules.QuestId);
            return mission != null && mission.State == MissionLifecycleState.Active;
        }

        internal static bool IsMissionCompleted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(
                source.Identity.Instance,
                RosenblattCascadingSpiritInteractionRules.QuestId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        internal static string ResolveStartNodeId(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return null;
            }

            if (IsRewardGranted(source)
                || IsMissionCompleted(source))
            {
                // Reward already paid but client journal may still show Mission:55AA38B5.
                if (IsMissionActive(source))
                {
                    MissionRuntime.Service.AbandonMission(
                        source.Identity.Instance,
                        RosenblattCascadingSpiritInteractionRules.QuestId);
                }

                RosenblattCascadingSpiritPacketSender.TrySendQuestDelete(source);
                ClearChimeraDiscTraded(source);
                return null;
            }

            // Recover stuck journal: rewards/essence already consumed but CompleteMission failed.
            if (IsMissionActive(source)
                && IsSpiritKilled(source)
                && !HasEssence(source))
            {
                MissionRuntime.Service.AbandonMission(
                    source.Identity.Instance,
                    RosenblattCascadingSpiritInteractionRules.QuestId);
                RosenblattCascadingSpiritPacketSender.TrySendQuestDelete(source);
                MissionRuntime.Service.SetFlag(
                    source.Identity.Instance,
                    RosenblattCascadingSpiritInteractionRules.QuestId,
                    RosenblattCascadingSpiritInteractionRules.RewardGrantedFlag,
                    "recovered-stuck-after-essence");
                ClearChimeraDiscTraded(source);
                return null;
            }

            if (IsMissionActive(source))
            {
                return RosenblattHiathlinInteractionRules.ReturnRootNodeId;
            }

            return null;
        }

        internal static MissionOperationResult AcceptQuest(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "rosenblatt-cascading-runtime-unavailable"
                       };
            }

            string questId = RosenblattCascadingSpiritInteractionRules.QuestId;
            int characterId = source.Identity.Instance;

            if (IsMissionActive(source))
            {
                RosenblattCascadingSpiritPacketSender.TrySendQuestFullUpdate(source);
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-cascading-already-active"
                       };
            }

            if (IsMissionCompleted(source) || IsRewardGranted(source))
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-cascading-already-completed"
                       };
            }

            MissionOperationResult offer = MissionRuntime.Service.OfferMission(characterId, questId);
            if (!IsClientEmitSuccess(offer) && IsPersistenceFailure(offer))
            {
                return offer;
            }

            MissionOperationResult accepted = MissionRuntime.Service.AcceptMission(characterId, questId);
            if (IsClientEmitSuccess(accepted))
            {
                RosenblattCascadingSpiritPacketSender.TrySendQuestFullUpdate(source);
                ClearChimeraDiscTraded(source);
            }

            return accepted;
        }

        internal static bool TryObserveNpcDeath(ICharacter attacker, ICharacter target)
        {
            if (attacker == null || target == null || !(attacker.Controller is PlayerController))
            {
                return false;
            }

            if (!MissionRuntime.IsInitialized || !IsMissionActive(attacker))
            {
                return false;
            }

            if (!RosenblattHiathlinInteractionRules.IsQuestPlayfield(
                    attacker.Playfield != null ? attacker.Playfield.Identity.Instance : 0))
            {
                return false;
            }

            if (!RosenblattCascadingSpiritInteractionRules.IsCascadingSpiritName(target.Name))
            {
                return false;
            }

            int characterId = attacker.Identity.Instance;
            string questId = RosenblattCascadingSpiritInteractionRules.QuestId;

            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = characterId,
                    QuestId = questId,
                    ObjectiveId = "mission_55AA38B5_cascading_kill",
                    ObservationKey = "rosenblatt-cascading-spirit-kill",
                    Amount = 1,
                    EventType = "RosenblattCascadingSpirit:Kill",
                    SourceIdentity = attacker.Identity.ToString(true),
                    TargetIdentity = target.Identity.ToString(true)
                });

            MissionRuntime.Service.SetFlag(
                characterId,
                questId,
                RosenblattCascadingSpiritInteractionRules.SpiritKilledFlag,
                "1");

            RosenblattCascadingSpiritPacketSender.TrySendQuestFullUpdate(attacker);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "RosenblattCascading kill observed by=" + attacker.Identity.ToString(true));
            return true;
        }

        internal static bool CanTurnIn(ICharacter source)
        {
            if (source == null || !IsMissionActive(source))
            {
                return false;
            }

            if (!IsSpiritKilled(source))
            {
                return false;
            }

            return HasEssence(source);
        }

        internal static bool CompleteTurnIn(ICharacter source, bool essenceAlreadyConsumed)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (!IsMissionActive(source))
            {
                return false;
            }

            if (!essenceAlreadyConsumed && !TryConsumeEssence(source, Identity.None))
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            string questId = RosenblattCascadingSpiritInteractionRules.QuestId;

            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = characterId,
                    QuestId = questId,
                    ObjectiveId = "mission_55AA38B5_turnin",
                    ObservationKey = "rosenblatt-cascading-essence-turnin",
                    Amount = 1,
                    EventType = "RosenblattCascadingSpirit:TurnIn",
                    SourceIdentity = source.Identity.ToString(true),
                    TargetIdentity = RosenblattHiathlinInteractionRules.RosenblattIdentityText
                });

            MissionCompleteService.GrantCredits(source, RosenblattCascadingSpiritInteractionRules.CreditReward);
            CombatXpRuntimeService.AwardDirectXp(
                source,
                RosenblattCascadingSpiritInteractionRules.XpReward,
                "rosenblatt-cascading-complete");
            MissionCompleteService.SendRewardFeedback(
                source,
                RosenblattCascadingSpiritInteractionRules.XpReward,
                RosenblattCascadingSpiritInteractionRules.CreditReward);
            MissionCompleteService.SendMissionAccomplishedFeedback(source);

            MissionOperationResult completed = MissionRuntime.Service.CompleteMission(characterId, questId);
            if (!IsClientEmitSuccess(completed) || IsMissionActive(source))
            {
                // Rewards already granted — never leave journal stuck (Mike Cascading turn-in).
                MissionRuntime.Service.AbandonMission(characterId, questId);
                if (!IsClientEmitSuccess(completed))
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "RosenblattCascading complete failed; abandoned by=" + source.Identity.ToString(true)
                        + " status=" + completed.Status
                        + " msg=" + completed.Message);
                }
            }

            // Capture 20260822-083345: Action59 then QuestDelete AFTER mission clear.
            RosenblattCascadingSpiritPacketSender.TrySendQuestDelete(source);
            RosenblattCascadingSpiritPacketSender.TrySendQuestDelete(source);

            MissionRuntime.Service.SetFlag(
                characterId,
                questId,
                RosenblattCascadingSpiritInteractionRules.RewardGrantedFlag,
                "credits:" + RosenblattCascadingSpiritInteractionRules.CreditReward.ToString(CultureInfo.InvariantCulture));

            ClearChimeraDiscTraded(source);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "RosenblattCascading turn-in complete by=" + source.Identity.ToString(true));
            return true;
        }

        internal static bool TryResendActiveMissionsForLogin(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (IsRewardGranted(source) && IsMissionActive(source))
            {
                RosenblattCascadingSpiritPacketSender.TrySendQuestDelete(source);
                MissionRuntime.Service.AbandonMission(
                    source.Identity.Instance,
                    RosenblattCascadingSpiritInteractionRules.QuestId);
                return false;
            }

            if (!IsMissionActive(source))
            {
                return false;
            }

            return RosenblattCascadingSpiritPacketSender.TrySendQuestFullUpdate(source);
        }

        internal static void MarkChimeraDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (ChimeraDiscTradedCharacters)
            {
                ChimeraDiscTradedCharacters.Add(source.Identity.Instance);
            }
        }

        internal static bool HasChimeraDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            lock (ChimeraDiscTradedCharacters)
            {
                return ChimeraDiscTradedCharacters.Contains(source.Identity.Instance);
            }
        }

        internal static void ClearChimeraDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (ChimeraDiscTradedCharacters)
            {
                ChimeraDiscTradedCharacters.Remove(source.Identity.Instance);
            }
        }

        internal static bool HasChimeraDatadisc(ICharacter source)
        {
            return CountItem(source, RosenblattCascadingSpiritInteractionRules.BarkingChimeraDatadiscItemId) > 0;
        }

        internal static bool HasEssence(ICharacter source)
        {
            return CountItem(source, RosenblattCascadingSpiritInteractionRules.EssenceOfTheHauntedItemId) > 0;
        }

        internal static bool TryConsumeChimeraDatadisc(ICharacter source, Identity stagedContainer)
        {
            return TryConsumeItem(
                source,
                stagedContainer,
                RosenblattCascadingSpiritInteractionRules.BarkingChimeraDatadiscItemId);
        }

        internal static bool TryConsumeEssence(ICharacter source, Identity stagedContainer)
        {
            return TryConsumeItem(
                source,
                stagedContainer,
                RosenblattCascadingSpiritInteractionRules.EssenceOfTheHauntedItemId);
        }

        private static bool TryConsumeItem(ICharacter source, Identity stagedContainer, int itemId)
        {
            if (source == null || source.BaseInventory == null)
            {
                return false;
            }

            if (stagedContainer.Type != IdentityType.None && stagedContainer.Instance > 0)
            {
                IInventoryPage stagedPage;
                if (source.BaseInventory.Pages.TryGetValue((int)stagedContainer.Type, out stagedPage)
                    && stagedPage != null)
                {
                    IItem staged = stagedPage[stagedContainer.Instance];
                    if (staged != null && (staged.LowID == itemId || staged.HighID == itemId))
                    {
                        stagedPage.Remove(stagedContainer.Instance);
                        try
                        {
                            if (source.BaseInventory.Write())
                            {
                                return true;
                            }
                        }
                        catch (Exception)
                        {
                        }

                        stagedPage.Add(stagedContainer.Instance, staged);
                    }
                }
            }

            foreach (var pageEntry in source.BaseInventory.Pages.ToList())
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (var entry in page.List().ToList())
                {
                    IItem item = entry.Value;
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.LowID != itemId && item.HighID != itemId)
                    {
                        continue;
                    }

                    page.Remove(entry.Key);
                    try
                    {
                        if (source.BaseInventory.Write())
                        {
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                    }

                    page.Add(entry.Key, item);
                    return false;
                }
            }

            return false;
        }

        private static int CountItem(ICharacter source, int itemId)
        {
            if (source == null || source.BaseInventory == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var pageEntry in source.BaseInventory.Pages)
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (var entry in page.List())
                {
                    IItem item = entry.Value;
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.LowID == itemId || item.HighID == itemId)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool IsSpiritKilled(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       RosenblattCascadingSpiritInteractionRules.QuestId,
                       RosenblattCascadingSpiritInteractionRules.SpiritKilledFlag) != null;
        }

        private static bool IsRewardGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       RosenblattCascadingSpiritInteractionRules.QuestId,
                       RosenblattCascadingSpiritInteractionRules.RewardGrantedFlag) != null;
        }

        private static bool IsClientEmitSuccess(MissionOperationResult result)
        {
            return result.Status == MissionOperationStatus.Applied
                   || result.Status == MissionOperationStatus.AlreadyApplied;
        }

        private static bool IsPersistenceFailure(MissionOperationResult result)
        {
            return result.Status == MissionOperationStatus.Unresolved
                   || result.Status == MissionOperationStatus.Rejected;
        }
    }
}
