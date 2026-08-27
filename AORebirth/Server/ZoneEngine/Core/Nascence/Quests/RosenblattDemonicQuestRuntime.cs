namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Capture-backed Dr. Rosenblatt Demonic Subjugator datadisc quest runtime (20260822-084957).
    /// </summary>
    internal static class RosenblattDemonicQuestRuntime
    {
        private static readonly HashSet<int> WeaverDiscTradedCharacters = new HashSet<int>();

        internal static bool CanOfferDiscTrade(ICharacter source)
        {
            if (source == null || !HasDatadisc(source))
            {
                return false;
            }

            // Allow trade even if mission runtime is briefly unavailable; AcceptQuest still
            // gates quest grant. Only block when Demonic is already active.
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
                RosenblattDemonicInteractionRules.QuestId);
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
                RosenblattDemonicInteractionRules.QuestId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        internal static MissionOperationResult AcceptQuest(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "rosenblatt-demonic-runtime-unavailable"
                       };
            }

            string questId = RosenblattDemonicInteractionRules.QuestId;
            int characterId = source.Identity.Instance;

            if (IsMissionActive(source))
            {
                RosenblattDemonicPacketSender.TrySendQuestFullUpdate(source);
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-demonic-already-active"
                       };
            }

            if (IsMissionCompleted(source) || IsRewardGranted(source))
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-demonic-already-completed"
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
                RosenblattDemonicPacketSender.TrySendQuestFullUpdate(source);
            }

            return accepted;
        }

        internal static bool TryObserveNpcDeath(ICharacter attacker, ICharacter target)
        {
            if (attacker == null || target == null || !(attacker.Controller is PlayerController))
            {
                return false;
            }

            if (!MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (IsMissionCompleted(attacker))
            {
                return false;
            }

            if (IsRewardGranted(attacker))
            {
                if (IsMissionActive(attacker))
                {
                    RosenblattDemonicPacketSender.TrySendQuestDelete(attacker);
                    MissionRuntime.Service.AbandonMission(
                        attacker.Identity.Instance,
                        RosenblattDemonicInteractionRules.QuestId);
                }

                return false;
            }

            if (!IsMissionActive(attacker))
            {
                return false;
            }

            if (!RosenblattHiathlinInteractionRules.IsQuestPlayfield(
                    attacker.Playfield != null ? attacker.Playfield.Identity.Instance : 0))
            {
                return false;
            }

            if (!RosenblattDemonicInteractionRules.IsDemonicSubjugator(target))
            {
                return false;
            }

            CompleteKill(attacker, target);
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
                RosenblattDemonicPacketSender.TrySendQuestDelete(source);
                MissionRuntime.Service.AbandonMission(
                    source.Identity.Instance,
                    RosenblattDemonicInteractionRules.QuestId);
                return false;
            }

            if (!IsMissionActive(source))
            {
                return false;
            }

            return RosenblattDemonicPacketSender.TrySendQuestFullUpdate(source);
        }

        internal static void MarkWeaverDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (WeaverDiscTradedCharacters)
            {
                WeaverDiscTradedCharacters.Add(source.Identity.Instance);
            }
        }

        internal static bool HasWeaverDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            lock (WeaverDiscTradedCharacters)
            {
                return WeaverDiscTradedCharacters.Contains(source.Identity.Instance);
            }
        }

        internal static void ClearWeaverDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (WeaverDiscTradedCharacters)
            {
                WeaverDiscTradedCharacters.Remove(source.Identity.Instance);
            }
        }

        internal static bool HasDatadisc(ICharacter source)
        {
            return HasCompactMessageDatadisc(
                source,
                RosenblattDemonicInteractionRules.WeaverDatadiscItemId);
        }

        internal static bool HasCompactMessageDatadisc(ICharacter source, int itemId)
        {
            return CountItem(source, itemId) > 0;
        }

        internal static int CountDatadisc(ICharacter source)
        {
            return CountItem(
                source,
                RosenblattDemonicInteractionRules.WeaverDatadiscItemId);
        }

        private static int CountItem(ICharacter source, int itemId)
        {
            if (source == null || source.BaseInventory == null || itemId <= 0)
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

        internal static bool TryConsumeDatadisc(ICharacter source, Identity stagedContainer)
        {
            if (source == null || source.BaseInventory == null)
            {
                return false;
            }

            int itemId = RosenblattDemonicInteractionRules.WeaverDatadiscItemId;
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

        private static void CompleteKill(ICharacter source, ICharacter target)
        {
            int characterId = source.Identity.Instance;
            string questId = RosenblattDemonicInteractionRules.QuestId;
            int creditReward = RosenblattDemonicInteractionRules.CreditReward;
            int xpReward = RosenblattDemonicInteractionRules.XpReward;

            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = characterId,
                    QuestId = questId,
                    ObjectiveId = "mission_55AA38B7_demonic_kill",
                    ObservationKey = "rosenblatt-demonic-kill",
                    Amount = 1,
                    EventType = "RosenblattDemonic:Kill",
                    SourceIdentity = source.Identity.ToString(true),
                    TargetIdentity = target != null
                                         ? target.Identity.ToString(true)
                                         : RosenblattDemonicInteractionRules.DemonicSubjugatorName
                });

            MissionCompleteService.GrantCredits(source, creditReward);
            CombatXpRuntimeService.AwardDirectXp(source, xpReward, "rosenblatt-demonic-complete");
            MissionCompleteService.SendRewardFeedback(source, xpReward, creditReward);
            MissionCompleteService.SendMissionAccomplishedFeedback(source);

            MissionOperationResult completed = MissionRuntime.Service.CompleteMission(characterId, questId);
            if (!IsClientEmitSuccess(completed) || IsMissionActive(source))
            {
                MissionRuntime.Service.AbandonMission(characterId, questId);
                if (!IsClientEmitSuccess(completed))
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "RosenblattDemonic complete failed; abandoned by=" + source.Identity.ToString(true)
                        + " status=" + completed.Status
                        + " msg=" + completed.Message);
                }
            }

            RosenblattDemonicPacketSender.TrySendQuestDelete(source);
            RosenblattDemonicPacketSender.TrySendQuestDelete(source);

            MissionRuntime.Service.SetFlag(
                characterId,
                questId,
                RosenblattDemonicInteractionRules.RewardGrantedFlag,
                "credits:" + creditReward.ToString(CultureInfo.InvariantCulture));

            ClearWeaverDiscTraded(source);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "RosenblattDemonic kill complete by=" + source.Identity.ToString(true));
        }

        private static bool IsRewardGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       RosenblattDemonicInteractionRules.QuestId,
                       RosenblattDemonicInteractionRules.RewardGrantedFlag) != null;
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
