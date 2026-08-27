namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Capture-backed Joshua Falker silvertail / chimera kill quest runtime (20260822-221109).
    /// </summary>
    internal static class NascenceLifeJoshuaFalkerQuestRuntime
    {
        private const int OverflowRewardSlot = 0x6F;

        internal static MissionOperationResult AcceptBothKillQuests(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "nascence-life-falker-runtime-unavailable"
                       };
            }

            MissionOperationResult silvertail = AcceptKillQuest(
                source,
                NascenceLifeJoshuaFalkerInteractionRules.SilvertailQuestId,
                (character, killsDone) =>
                    NascenceLifeJoshuaFalkerPacketSender.TrySendSilvertailQuestFullUpdate(character, killsDone));
            MissionOperationResult chimera = AcceptKillQuest(
                source,
                NascenceLifeJoshuaFalkerInteractionRules.ChimeraQuestId,
                (character, killsDone) =>
                    NascenceLifeJoshuaFalkerPacketSender.TrySendChimeraQuestFullUpdate(character, killsDone));

            if (IsPersistenceFailure(silvertail) || IsPersistenceFailure(chimera))
            {
                return IsPersistenceFailure(silvertail) ? silvertail : chimera;
            }

            return silvertail.Status == MissionOperationStatus.Applied
                       || chimera.Status == MissionOperationStatus.Applied
                       ? new MissionOperationResult { Status = MissionOperationStatus.Applied }
                       : new MissionOperationResult { Status = MissionOperationStatus.AlreadyApplied };
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

            int playfieldId = attacker.Playfield != null ? attacker.Playfield.Identity.Instance : 0;
            if (!NascenceLifeJoshuaFalkerInteractionRules.IsQuestPlayfield(playfieldId))
            {
                return false;
            }

            bool handled = false;
            if (NascenceLifeJoshuaFalkerInteractionRules.IsSwiftSilvertailName(target.Name))
            {
                handled = TryAdvanceKill(
                    attacker,
                    target,
                    NascenceLifeJoshuaFalkerInteractionRules.SilvertailQuestId,
                    NascenceLifeJoshuaFalkerInteractionRules.SilvertailKillCountFlag,
                    NascenceLifeJoshuaFalkerInteractionRules.SilvertailRewardGrantedFlag,
                    "mission_55ABAD28_silvertail_kill",
                    "NascenceLifeFalker:SilvertailKill",
                    NascenceLifeJoshuaFalkerInteractionRules.SilvertailRewardLowItemId,
                    NascenceLifeJoshuaFalkerInteractionRules.SilvertailRewardHighItemId,
                    NascenceLifeJoshuaFalkerInteractionRules.SilvertailRewardQuality,
                    NascenceLifeJoshuaFalkerPacketSender.TrySendSilvertailQuestDelete);
            }

            if (NascenceLifeJoshuaFalkerInteractionRules.IsBarkingChimeraName(target.Name))
            {
                handled = TryAdvanceKill(
                               attacker,
                               target,
                               NascenceLifeJoshuaFalkerInteractionRules.ChimeraQuestId,
                               NascenceLifeJoshuaFalkerInteractionRules.ChimeraKillCountFlag,
                               NascenceLifeJoshuaFalkerInteractionRules.ChimeraRewardGrantedFlag,
                               "mission_55ABAD29_chimera_kill",
                               "NascenceLifeFalker:ChimeraKill",
                               NascenceLifeJoshuaFalkerInteractionRules.ChimeraRewardLowItemId,
                               NascenceLifeJoshuaFalkerInteractionRules.ChimeraRewardHighItemId,
                               NascenceLifeJoshuaFalkerInteractionRules.ChimeraRewardQuality,
                               NascenceLifeJoshuaFalkerPacketSender.TrySendChimeraQuestDelete)
                           || handled;
            }

            return handled;
        }

        internal static bool TryResendActiveMissionsForLogin(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            bool sent = false;
            if (IsMissionActive(source, NascenceLifeJoshuaFalkerInteractionRules.SilvertailQuestId))
            {
                sent = NascenceLifeJoshuaFalkerPacketSender.TrySendSilvertailQuestFullUpdate(
                    source,
                    GetKillCount(
                        source,
                        NascenceLifeJoshuaFalkerInteractionRules.SilvertailQuestId,
                        NascenceLifeJoshuaFalkerInteractionRules.SilvertailKillCountFlag));
            }

            if (IsMissionActive(source, NascenceLifeJoshuaFalkerInteractionRules.ChimeraQuestId))
            {
                sent = NascenceLifeJoshuaFalkerPacketSender.TrySendChimeraQuestFullUpdate(
                           source,
                           GetKillCount(
                               source,
                               NascenceLifeJoshuaFalkerInteractionRules.ChimeraQuestId,
                               NascenceLifeJoshuaFalkerInteractionRules.ChimeraKillCountFlag))
                       || sent;
            }

            return sent;
        }

        private static bool TryAdvanceKill(
            ICharacter attacker,
            ICharacter target,
            string questId,
            string killCountFlag,
            string rewardGrantedFlag,
            string objectiveId,
            string eventType,
            int rewardLowId,
            int rewardHighId,
            int rewardQuality,
            Func<ICharacter, bool> sendQuestDelete)
        {
            if (IsRewardGranted(attacker, questId, rewardGrantedFlag))
            {
                if (IsMissionActive(attacker, questId))
                {
                    sendQuestDelete(attacker);
                    MissionRuntime.Service.AbandonMission(attacker.Identity.Instance, questId);
                }

                return false;
            }

            if (!IsMissionActive(attacker, questId))
            {
                return false;
            }

            int killCount = GetKillCount(attacker, questId, killCountFlag);
            if (killCount >= NascenceLifeJoshuaFalkerInteractionRules.RequiredKills)
            {
                return false;
            }

            killCount++;
            SetKillCount(attacker, questId, killCountFlag, killCount);
            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = attacker.Identity.Instance,
                    QuestId = questId,
                    ObjectiveId = objectiveId,
                    ObservationKey = eventType + ":" + killCount.ToString(CultureInfo.InvariantCulture),
                    Amount = 1,
                    EventType = eventType,
                    SourceIdentity = attacker.Identity.ToString(true),
                    TargetIdentity = target.Identity.ToString(true)
                });

            if (killCount < NascenceLifeJoshuaFalkerInteractionRules.RequiredKills)
            {
                ResendKillQuestProgress(attacker, questId, killCount);
                return true;
            }

            CompleteKillQuest(
                attacker,
                questId,
                rewardGrantedFlag,
                objectiveId,
                eventType,
                rewardLowId,
                rewardHighId,
                rewardQuality,
                sendQuestDelete);
            return true;
        }

        private static void CompleteKillQuest(
            ICharacter source,
            string questId,
            string rewardGrantedFlag,
            string objectiveId,
            string eventType,
            int rewardLowId,
            int rewardHighId,
            int rewardQuality,
            Func<ICharacter, bool> sendQuestDelete)
        {
            if (!TryGrantKillRewardItem(source, rewardLowId, rewardHighId, rewardQuality))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_FALKER reward grant failed quest=" + questId
                    + " char=" + source.Identity.ToString(true));
                return;
            }

            CombatXpRuntimeService.AwardDirectXp(
                source,
                NascenceLifeJoshuaFalkerInteractionRules.XpReward,
                "nascence-life-falker-complete");
            MissionCompleteService.SendRewardFeedback(
                source,
                NascenceLifeJoshuaFalkerInteractionRules.XpReward,
                0);
            MissionCompleteService.SendMissionAccomplishedFeedback(source);
            sendQuestDelete(source);

            int characterId = source.Identity.Instance;
            MissionOperationResult completed = MissionRuntime.Service.CompleteMission(characterId, questId);
            if (!IsClientEmitSuccess(completed) && IsMissionActive(source, questId))
            {
                if (IsPersistenceFailure(completed))
                {
                    MissionRuntime.Service.AbandonMission(characterId, questId);
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_FALKER complete failed quest=" + questId
                    + " status=" + completed.Status
                    + " msg=" + completed.Message);
                return;
            }

            MissionRuntime.Service.SetFlag(
                characterId,
                questId,
                rewardGrantedFlag,
                "item:" + rewardLowId.ToString(CultureInfo.InvariantCulture));
        }

        private static bool TryGrantKillRewardItem(
            ICharacter source,
            int lowId,
            int highId,
            int quality)
        {
            if (source == null
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(lowId))
            {
                return false;
            }

            Item item;
            try
            {
                item = new Item(quality, lowId, highId);
            }
            catch (Exception)
            {
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                return false;
            }

            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = item.LowID,
                    ItemHighId = item.HighID,
                    Quality = item.Quality,
                    Unknown1 = 1,
                    Unknown2 = 87,
                    Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Unknown3 = 0,
                    Unknown4 = 0
                });
            source.Send(
                new ContainerAddItemMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity
                             {
                                 Type = IdentityType.OverflowWindow,
                                 Instance = source.Identity.Instance
                             },
                    TargetPlacement = OverflowRewardSlot
                });
            return true;
        }

        private static void ResendKillQuestProgress(ICharacter source, string questId, int killsDone)
        {
            if (string.Equals(
                questId,
                NascenceLifeJoshuaFalkerInteractionRules.SilvertailQuestId,
                StringComparison.OrdinalIgnoreCase))
            {
                NascenceLifeJoshuaFalkerPacketSender.TrySendSilvertailQuestFullUpdate(source, killsDone);
                return;
            }

            if (string.Equals(
                questId,
                NascenceLifeJoshuaFalkerInteractionRules.ChimeraQuestId,
                StringComparison.OrdinalIgnoreCase))
            {
                NascenceLifeJoshuaFalkerPacketSender.TrySendChimeraQuestFullUpdate(source, killsDone);
            }
        }

        private static MissionOperationResult AcceptKillQuest(
            ICharacter source,
            string questId,
            Func<ICharacter, int, bool> sendQuestFullUpdate)
        {
            int characterId = source.Identity.Instance;

            if (IsMissionActive(source, questId))
            {
                sendQuestFullUpdate(
                    source,
                    GetKillCount(source, questId, GetKillCountFlagForQuest(questId)));
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "nascence-life-falker-already-active"
                       };
            }

            if (IsMissionCompleted(source, questId)
                || IsRewardGranted(source, questId, GetRewardFlagForQuest(questId)))
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "nascence-life-falker-already-completed"
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
                SetKillCount(source, questId, GetKillCountFlagForQuest(questId), 0);
                sendQuestFullUpdate(source, 0);
            }

            return accepted;
        }

        private static string GetKillCountFlagForQuest(string questId)
        {
            return string.Equals(
                questId,
                NascenceLifeJoshuaFalkerInteractionRules.SilvertailQuestId,
                StringComparison.OrdinalIgnoreCase)
                       ? NascenceLifeJoshuaFalkerInteractionRules.SilvertailKillCountFlag
                       : NascenceLifeJoshuaFalkerInteractionRules.ChimeraKillCountFlag;
        }

        private static string GetRewardFlagForQuest(string questId)
        {
            return string.Equals(
                questId,
                NascenceLifeJoshuaFalkerInteractionRules.SilvertailQuestId,
                StringComparison.OrdinalIgnoreCase)
                       ? NascenceLifeJoshuaFalkerInteractionRules.SilvertailRewardGrantedFlag
                       : NascenceLifeJoshuaFalkerInteractionRules.ChimeraRewardGrantedFlag;
        }

        private static bool IsMissionActive(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Active;
        }

        private static bool IsMissionCompleted(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        private static int GetKillCount(ICharacter source, string questId, string killCountFlag)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return 0;
            }

            MissionFlagRecord flag = MissionRuntime.Service.GetFlag(
                source.Identity.Instance,
                questId,
                killCountFlag);
            if (flag == null || string.IsNullOrWhiteSpace(flag.Value))
            {
                return 0;
            }

            int count;
            return int.TryParse(flag.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
                       ? Math.Max(0, count)
                       : 0;
        }

        private static void SetKillCount(ICharacter source, string questId, string killCountFlag, int count)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                questId,
                killCountFlag,
                count.ToString(CultureInfo.InvariantCulture));
        }

        private static bool IsRewardGranted(ICharacter source, string questId, string rewardGrantedFlag)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       questId,
                       rewardGrantedFlag) != null;
        }

        private static bool IsClientEmitSuccess(MissionOperationResult result)
        {
            return result != null
                   && (result.Status == MissionOperationStatus.Applied
                       || result.Status == MissionOperationStatus.AlreadyApplied);
        }

        private static bool IsPersistenceFailure(MissionOperationResult result)
        {
            return result != null
                   && result.Status != MissionOperationStatus.Applied
                   && result.Status != MissionOperationStatus.AlreadyApplied
                   && result.Status != MissionOperationStatus.Unresolved;
        }

        internal static bool TryHandleJournalDelete(ICharacter source, Identity missionIdentity)
        {
            if (source == null || missionIdentity == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            string questId;
            switch (missionIdentity.Instance)
            {
                case unchecked((int)0x55ABAD28):
                    questId = NascenceLifeJoshuaFalkerInteractionRules.SilvertailQuestId;
                    break;
                case unchecked((int)0x55ABAD29):
                    questId = NascenceLifeJoshuaFalkerInteractionRules.ChimeraQuestId;
                    break;
                default:
                    return false;
            }

            int characterId = source.Identity.Instance;
            MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, questId);
            if (mission == null || mission.State != MissionLifecycleState.Active)
            {
                return false;
            }

            if (string.Equals(questId, NascenceLifeJoshuaFalkerInteractionRules.SilvertailQuestId, StringComparison.OrdinalIgnoreCase))
            {
                NascenceLifeJoshuaFalkerPacketSender.TrySendSilvertailQuestDelete(source);
            }
            else
            {
                NascenceLifeJoshuaFalkerPacketSender.TrySendChimeraQuestDelete(source);
            }

            MissionRuntime.Service.AbandonMission(characterId, questId);
            return true;
        }
    }
}
