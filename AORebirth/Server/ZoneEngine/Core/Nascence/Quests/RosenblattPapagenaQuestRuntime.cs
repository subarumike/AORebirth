namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;
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
    /// Capture-backed Dr. Rosenblatt Papagena datadisc quest runtime (20260822-082554).
    /// </summary>
    internal static class RosenblattPapagenaQuestRuntime
    {
        private const int XpReward = 1000;

        internal static bool CanOfferDiscTrade(ICharacter source)
        {
            if (source == null || !HasDatadisc(source))
            {
                return false;
            }

            // Omni players: Remove Papagena. Clan players: Remove Papageno (separate runtime).
            if (RosenblattPapagenoInteractionRules.IsClanPlayer(source))
            {
                return false;
            }

            // Allow trade even if mission runtime is briefly unavailable; AcceptQuest still
            // gates quest grant. Only block when Papagena is already active.
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
                RosenblattPapagenaInteractionRules.QuestId);
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
                RosenblattPapagenaInteractionRules.QuestId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        internal static MissionOperationResult AcceptQuest(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "rosenblatt-papagena-runtime-unavailable"
                       };
            }

            string questId = RosenblattPapagenaInteractionRules.QuestId;
            int characterId = source.Identity.Instance;

            if (IsMissionActive(source))
            {
                RosenblattPapagenaPacketSender.TrySendQuestFullUpdate(source);
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-papagena-already-active"
                       };
            }

            if (IsMissionCompleted(source) || IsRewardGranted(source))
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-papagena-already-completed"
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
                RosenblattPapagenaPacketSender.TrySendQuestFullUpdate(source);
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
                    RosenblattPapagenaPacketSender.TrySendQuestDelete(attacker);
                    MissionRuntime.Service.AbandonMission(
                        attacker.Identity.Instance,
                        RosenblattPapagenaInteractionRules.QuestId);
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

            if (!RosenblattPapagenaInteractionRules.IsPapagenaName(target.Name))
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
                RosenblattPapagenaPacketSender.TrySendQuestDelete(source);
                MissionRuntime.Service.AbandonMission(
                    source.Identity.Instance,
                    RosenblattPapagenaInteractionRules.QuestId);
                return false;
            }

            if (!IsMissionActive(source))
            {
                return false;
            }

            return RosenblattPapagenaPacketSender.TrySendQuestFullUpdate(source);
        }

        internal static bool HasDatadisc(ICharacter source)
        {
            return HasCompactMessageDatadisc(
                source,
                RosenblattPapagenaInteractionRules.SwiftSilvertailDatadiscItemId);
        }

        internal static bool HasCompactMessageDatadisc(ICharacter source, int itemId)
        {
            return CountItem(source, itemId) > 0;
        }

        internal static int CountDatadisc(ICharacter source)
        {
            return CountItem(
                source,
                RosenblattPapagenaInteractionRules.SwiftSilvertailDatadiscItemId);
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

            int itemId = RosenblattPapagenaInteractionRules.SwiftSilvertailDatadiscItemId;
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
            string questId = RosenblattPapagenaInteractionRules.QuestId;

            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = characterId,
                    QuestId = questId,
                    ObjectiveId = "mission_55AA38B0_papagena_kill",
                    ObservationKey = "rosenblatt-papagena-kill",
                    Amount = 1,
                    EventType = "RosenblattPapagena:Kill",
                    SourceIdentity = source.Identity.ToString(true),
                    TargetIdentity = target != null ? target.Identity.ToString(true) : RosenblattPapagenaInteractionRules.PapagenaName
                });

            MissionCompleteService.GrantCredits(source, RosenblattPapagenaInteractionRules.CreditReward);
            CombatXpRuntimeService.AwardDirectXp(source, XpReward, "rosenblatt-papagena-complete");
            MissionCompleteService.SendRewardFeedback(source, XpReward, RosenblattPapagenaInteractionRules.CreditReward);
            MissionCompleteService.SendMissionAccomplishedFeedback(source);
            RosenblattPapagenaPacketSender.TrySendQuestDelete(source);

            MissionOperationResult completed = MissionRuntime.Service.CompleteMission(characterId, questId);
            if (!IsClientEmitSuccess(completed) && IsMissionActive(source))
            {
                if (IsPersistenceFailure(completed))
                {
                    MissionRuntime.Service.AbandonMission(characterId, questId);
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "RosenblattPapagena complete failed by=" + source.Identity.ToString(true)
                    + " status=" + completed.Status
                    + " msg=" + completed.Message);
                return;
            }

            MissionRuntime.Service.SetFlag(
                characterId,
                questId,
                RosenblattPapagenaInteractionRules.RewardGrantedFlag,
                "credits:" + RosenblattPapagenaInteractionRules.CreditReward.ToString(CultureInfo.InvariantCulture));

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "RosenblattPapagena kill complete by=" + source.Identity.ToString(true));
        }

        private static bool IsRewardGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       RosenblattPapagenaInteractionRules.QuestId,
                       RosenblattPapagenaInteractionRules.RewardGrantedFlag) != null;
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
