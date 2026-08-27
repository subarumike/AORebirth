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
    /// Capture-backed Dr. Rosenblatt Spinetooth datadisc quest runtime (20260822-083846).
    /// </summary>
    internal static class RosenblattSpinetoothQuestRuntime
    {
        private static readonly HashSet<int> PredatorDiscTradedCharacters = new HashSet<int>();

        internal static bool CanOfferDiscTrade(ICharacter source)
        {
            if (source == null || !HasDatadisc(source))
            {
                return false;
            }

            // Allow trade even if mission runtime is briefly unavailable; AcceptQuest still
            // gates quest grant. Only block when Spinetooth is already active.
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
                RosenblattSpinetoothInteractionRules.QuestId);
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
                RosenblattSpinetoothInteractionRules.QuestId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        internal static MissionOperationResult AcceptQuest(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "rosenblatt-spinetooth-runtime-unavailable"
                       };
            }

            string questId = RosenblattSpinetoothInteractionRules.QuestId;
            int characterId = source.Identity.Instance;

            if (IsMissionActive(source))
            {
                RosenblattSpinetoothPacketSender.TrySendQuestFullUpdate(source);
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-spinetooth-already-active"
                       };
            }

            if (IsMissionCompleted(source) || IsRewardGranted(source))
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-spinetooth-already-completed"
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
                RosenblattSpinetoothPacketSender.TrySendQuestFullUpdate(source);
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
                    RosenblattSpinetoothPacketSender.TrySendQuestDelete(attacker);
                    MissionRuntime.Service.AbandonMission(
                        attacker.Identity.Instance,
                        RosenblattSpinetoothInteractionRules.QuestId);
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

            if (!RosenblattSpinetoothInteractionRules.IsSpinetoothHatchlingName(target.Name))
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
                RosenblattSpinetoothPacketSender.TrySendQuestDelete(source);
                MissionRuntime.Service.AbandonMission(
                    source.Identity.Instance,
                    RosenblattSpinetoothInteractionRules.QuestId);
                return false;
            }

            if (!IsMissionActive(source))
            {
                return false;
            }

            return RosenblattSpinetoothPacketSender.TrySendQuestFullUpdate(source);
        }

        internal static void MarkPredatorDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (PredatorDiscTradedCharacters)
            {
                PredatorDiscTradedCharacters.Add(source.Identity.Instance);
            }
        }

        internal static bool HasPredatorDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            lock (PredatorDiscTradedCharacters)
            {
                return PredatorDiscTradedCharacters.Contains(source.Identity.Instance);
            }
        }

        internal static void ClearPredatorDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (PredatorDiscTradedCharacters)
            {
                PredatorDiscTradedCharacters.Remove(source.Identity.Instance);
            }
        }

        internal static bool HasDatadisc(ICharacter source)
        {
            return HasCompactMessageDatadisc(
                source,
                RosenblattSpinetoothInteractionRules.PredatorDatadiscItemId);
        }

        internal static bool HasCompactMessageDatadisc(ICharacter source, int itemId)
        {
            return CountItem(source, itemId) > 0;
        }

        internal static int CountDatadisc(ICharacter source)
        {
            return CountItem(
                source,
                RosenblattSpinetoothInteractionRules.PredatorDatadiscItemId);
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

            int itemId = RosenblattSpinetoothInteractionRules.PredatorDatadiscItemId;
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
            string questId = RosenblattSpinetoothInteractionRules.QuestId;
            int creditReward = RosenblattSpinetoothInteractionRules.CreditReward;
            int xpReward = RosenblattSpinetoothInteractionRules.XpReward;

            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = characterId,
                    QuestId = questId,
                    ObjectiveId = "mission_55AA38B6_spinetooth_kill",
                    ObservationKey = "rosenblatt-spinetooth-kill",
                    Amount = 1,
                    EventType = "RosenblattSpinetooth:Kill",
                    SourceIdentity = source.Identity.ToString(true),
                    TargetIdentity = target != null
                                         ? target.Identity.ToString(true)
                                         : RosenblattSpinetoothInteractionRules.SpinetoothHatchlingName
                });

            MissionCompleteService.GrantCredits(source, creditReward);
            CombatXpRuntimeService.AwardDirectXp(source, xpReward, "rosenblatt-spinetooth-complete");
            MissionCompleteService.SendRewardFeedback(source, xpReward, creditReward);
            MissionCompleteService.SendMissionAccomplishedFeedback(source);
            RosenblattSpinetoothPacketSender.TrySendQuestDelete(source);

            MissionOperationResult completed = MissionRuntime.Service.CompleteMission(characterId, questId);
            if (!IsClientEmitSuccess(completed) && IsMissionActive(source))
            {
                if (IsPersistenceFailure(completed))
                {
                    MissionRuntime.Service.AbandonMission(characterId, questId);
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "RosenblattSpinetooth complete failed by=" + source.Identity.ToString(true)
                    + " status=" + completed.Status
                    + " msg=" + completed.Message);
                return;
            }

            MissionRuntime.Service.SetFlag(
                characterId,
                questId,
                RosenblattSpinetoothInteractionRules.RewardGrantedFlag,
                "credits:" + creditReward.ToString(CultureInfo.InvariantCulture));

            ClearPredatorDiscTraded(source);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "RosenblattSpinetooth kill complete by=" + source.Identity.ToString(true));
        }

        private static bool IsRewardGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       RosenblattSpinetoothInteractionRules.QuestId,
                       RosenblattSpinetoothInteractionRules.RewardGrantedFlag) != null;
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
