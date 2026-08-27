namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Clan-side Swift Silvertail datadisc quest: Remove Papageno (20260825-204815).
    /// </summary>
    internal static class RosenblattPapagenoQuestRuntime
    {
        private static readonly HashSet<int> SilvertailDiscTradedCharacters = new HashSet<int>();

        internal static bool CanOfferDiscTrade(ICharacter source)
        {
            if (source == null
                || !RosenblattPapagenoInteractionRules.IsClanPlayer(source)
                || !RosenblattPapagenaQuestRuntime.HasDatadisc(source))
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
                RosenblattPapagenoInteractionRules.QuestId);
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
                RosenblattPapagenoInteractionRules.QuestId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        internal static MissionOperationResult AcceptQuest(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "rosenblatt-papageno-runtime-unavailable"
                       };
            }

            string questId = RosenblattPapagenoInteractionRules.QuestId;
            int characterId = source.Identity.Instance;

            if (IsMissionActive(source))
            {
                RosenblattPapagenoPacketSender.TrySendQuestFullUpdate(source);
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-papageno-already-active"
                       };
            }

            if (IsMissionCompleted(source) || IsRewardGranted(source))
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-papageno-already-completed"
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
                RosenblattPapagenoPacketSender.TrySendQuestFullUpdate(source);
                ClearSilvertailDiscTraded(source);
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
                    RosenblattPapagenoPacketSender.TrySendQuestDelete(attacker);
                    MissionRuntime.Service.AbandonMission(
                        attacker.Identity.Instance,
                        RosenblattPapagenoInteractionRules.QuestId);
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

            if (!RosenblattPapagenoInteractionRules.IsPapagenoName(target.Name))
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
                RosenblattPapagenoPacketSender.TrySendQuestDelete(source);
                MissionRuntime.Service.AbandonMission(
                    source.Identity.Instance,
                    RosenblattPapagenoInteractionRules.QuestId);
                return false;
            }

            if (!IsMissionActive(source))
            {
                return false;
            }

            return RosenblattPapagenoPacketSender.TrySendQuestFullUpdate(source);
        }

        internal static void MarkSilvertailDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (SilvertailDiscTradedCharacters)
            {
                SilvertailDiscTradedCharacters.Add(source.Identity.Instance);
            }
        }

        internal static bool HasSilvertailDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            lock (SilvertailDiscTradedCharacters)
            {
                return SilvertailDiscTradedCharacters.Contains(source.Identity.Instance);
            }
        }

        internal static void ClearSilvertailDiscTraded(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (SilvertailDiscTradedCharacters)
            {
                SilvertailDiscTradedCharacters.Remove(source.Identity.Instance);
            }
        }

        private static void CompleteKill(ICharacter source, ICharacter target)
        {
            int characterId = source.Identity.Instance;
            string questId = RosenblattPapagenoInteractionRules.QuestId;
            int creditReward = RosenblattPapagenoInteractionRules.CreditReward;
            int xpReward = RosenblattPapagenoInteractionRules.XpReward;

            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = characterId,
                    QuestId = questId,
                    ObjectiveId = "mission_55B0B8A3_papageno_kill",
                    ObservationKey = "rosenblatt-papageno-kill",
                    Amount = 1,
                    EventType = "RosenblattPapageno:Kill",
                    SourceIdentity = source.Identity.ToString(true),
                    TargetIdentity = target != null
                                         ? target.Identity.ToString(true)
                                         : RosenblattPapagenoInteractionRules.PapagenoName
                });

            // Capture 20260825-204815: +1000 cash on kill; XP unchanged in session snapshot.
            MissionCompleteService.GrantCredits(source, creditReward);
            CombatXpRuntimeService.AwardDirectXp(source, xpReward, "rosenblatt-papageno-complete");
            MissionCompleteService.SendRewardFeedback(source, xpReward, creditReward);
            MissionCompleteService.SendMissionAccomplishedFeedback(source);

            MissionOperationResult completed = MissionRuntime.Service.CompleteMission(characterId, questId);
            if (!IsClientEmitSuccess(completed) || IsMissionActive(source))
            {
                MissionRuntime.Service.AbandonMission(characterId, questId);
            }

            RosenblattPapagenoPacketSender.TrySendQuestDelete(source);
            RosenblattPapagenoPacketSender.TrySendQuestDelete(source);

            MissionRuntime.Service.SetFlag(
                characterId,
                questId,
                RosenblattPapagenoInteractionRules.RewardGrantedFlag,
                "credits:" + creditReward.ToString(CultureInfo.InvariantCulture));

            ClearSilvertailDiscTraded(source);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "RosenblattPapageno kill complete by=" + source.Identity.ToString(true));
        }

        private static bool IsRewardGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       RosenblattPapagenoInteractionRules.QuestId,
                       RosenblattPapagenoInteractionRules.RewardGrantedFlag) != null;
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
