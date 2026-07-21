namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Completes Return to Marcus (B196) when the player talks to Marcus after extinguishing the fire.
    /// Without this, Marcus kept offering the fire option while B196 sat stuck in the mission window.
    /// </summary>
    public static class MarcusB196CompletionHandler
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const int MarcusStoneInstance = unchecked((int)0x782DE567);

        private const string MissionId = "Mission:5514B196";

        private const string ObjectiveId = "mission_5514b196_objective_questfullupdate";

        public static MarcusB196CompletionResult TryCompleteOnReturn(
            ICharacter source,
            Identity npcIdentity,
            bool dialogueGateEnabled)
        {
            if (!IsMarcusStone(npcIdentity) && !IsMarcusStoneNameBound(source, npcIdentity))
            {
                return MarcusB196CompletionResult.NotApplicable();
            }

            if (!dialogueGateEnabled)
            {
                return MarcusB196CompletionResult.Skipped(
                    "Marcus B196 return skipped because dialogue routing gate is disabled.");
            }

            if (!IsValidPlayerInArete(source))
            {
                return MarcusB196CompletionResult.Failed(
                    "Marcus B196 return failed: source is missing, not a player, or not in Arete Landing 6553.");
            }

            if (!MissionRuntime.IsInitialized)
            {
                return MarcusB196CompletionResult.Failed(
                    "Marcus B196 return failed: persistent mission runtime is not initialized.");
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, MissionId);
            if (mission != null && mission.State == MissionLifecycleState.Offered)
            {
                MissionRuntime.Service.AcceptMission(characterId, MissionId);
                mission = MissionRuntime.Service.GetMission(characterId, MissionId);
            }

            if (mission == null
                || (mission.State != MissionLifecycleState.Active
                    && mission.State != MissionLifecycleState.Completed))
            {
                return MarcusB196CompletionResult.NotApplicable();
            }

            if (mission.State == MissionLifecycleState.Active)
            {
                MissionOperationResult objective = MissionRuntime.Service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = characterId,
                        QuestId = MissionId,
                        ObjectiveId = ObjectiveId,
                        ObservationKey = "dialogue-return-marcus:" + npcIdentity.ToString(true),
                        Amount = 1,
                        EventType = "NpcDialogueOpen",
                        SourceIdentity = source.Identity.ToString(true),
                        TargetIdentity = npcIdentity.ToString(true)
                    });
                if (objective.Status != MissionOperationStatus.Applied
                    && objective.Status != MissionOperationStatus.AlreadyApplied
                    && objective.Status != MissionOperationStatus.DuplicateObservation)
                {
                    return MarcusB196CompletionResult.Failed(
                        "Marcus B196 objective persistence failed: " + objective.Message);
                }

                MissionOperationResult completion = MissionRuntime.Service.CompleteMission(characterId, MissionId);
                if (completion.Status != MissionOperationStatus.Applied
                    && completion.Status != MissionOperationStatus.AlreadyApplied)
                {
                    return MarcusB196CompletionResult.Failed(
                        "Marcus B196 completion persistence failed: " + completion.Message);
                }
            }

            // Persist prior chain steps as completed so fire handout cannot restart.
            ForceCompleteIfNeeded(characterId, MissionRuntime.RexB18FQuestId);
            ForceCompleteIfNeeded(characterId, MissionRuntime.RexB194QuestId);
            ForceCompleteIfNeeded(characterId, MissionRuntime.RexB18EQuestId);

            // Clear Return to Marcus AND leftover Talk to Marcus / Extinguish / Return to Rex.
            RexQuestPreviewEmissionResult projected = SafeQuestFullUpdateSender.TrySendB196CompletionCleanup(source);
            bool ok = projected != null && projected.Emitted;
            if (!ok)
            {
                SafeQuestFullUpdateSender.TrySendB196QuestDelete(source);
                SafeQuestFullUpdateSender.TrySendB18FQuestDelete(source);
                SafeQuestFullUpdateSender.TrySendB18EQuestDelete(source);
                ok = true;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ARETE_MARCUS_B196_COMPLETION applied character="
                + source.Identity.ToString(true)
                + " projected=" + ok);

            return MarcusB196CompletionResult.Succeeded(
                "Marcus B196 Return to Marcus completed; removed B196/B18F/B194/B18E from mission window.");
        }

        private static void ForceCompleteIfNeeded(int characterId, string questId)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, questId);
            if (mission == null || mission.State == MissionLifecycleState.Completed)
            {
                return;
            }

            if (mission.State == MissionLifecycleState.Offered)
            {
                MissionRuntime.Service.AcceptMission(characterId, questId);
                mission = MissionRuntime.Service.GetMission(characterId, questId);
            }

            if (mission == null || mission.State != MissionLifecycleState.Active)
            {
                return;
            }

            string objectiveId = "mission_"
                                 + questId.Replace("Mission:", string.Empty).ToLowerInvariant()
                                 + "_objective_questfullupdate";
            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = characterId,
                    QuestId = questId,
                    ObjectiveId = objectiveId,
                    ObservationKey = "marcus-b196-force-complete",
                    Amount = 1,
                    EventType = "NpcDialogueOpen",
                    SourceIdentity = string.Empty,
                    TargetIdentity = string.Empty
                });
            MissionRuntime.Service.CompleteMission(characterId, questId);
        }

        private static bool IsMarcusStone(Identity identity)
        {
            return identity.Type == IdentityType.CanbeAffected
                   && identity.Instance == MarcusStoneInstance;
        }

        private static bool IsMarcusStoneNameBound(ICharacter source, Identity npcIdentity)
        {
            if (source == null || source.Playfield == null
                || npcIdentity.Type != IdentityType.CanbeAffected
                || npcIdentity.Instance == 0)
            {
                return false;
            }

            ICharacter npc = AORebirth.ObjectManager.Pool.Instance.GetObject<ICharacter>(
                source.Playfield.Identity,
                npcIdentity);
            return npc != null
                   && !string.IsNullOrWhiteSpace(npc.Name)
                   && npc.Name.IndexOf("Marcus Stone", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsValidPlayerInArete(ICharacter source)
        {
            return source != null
                   && source.Controller is PlayerController
                   && source.Identity.Type == IdentityType.CanbeAffected
                   && source.Identity.Instance != 0
                   && source.Playfield != null
                   && source.Playfield.Identity.Instance == AreteLandingPlayfieldId;
        }
    }

    public sealed class MarcusB196CompletionResult
    {
        private MarcusB196CompletionResult()
        {
        }

        public bool IsApplicable { get; private set; }

        public bool Attempted { get; private set; }

        public bool Completed { get; private set; }

        public string Message { get; private set; }

        public static MarcusB196CompletionResult NotApplicable()
        {
            return new MarcusB196CompletionResult();
        }

        public static MarcusB196CompletionResult Skipped(string message)
        {
            return new MarcusB196CompletionResult
                   {
                       IsApplicable = true,
                       Attempted = false,
                       Completed = false,
                       Message = message
                   };
        }

        public static MarcusB196CompletionResult Succeeded(string message)
        {
            return new MarcusB196CompletionResult
                   {
                       IsApplicable = true,
                       Attempted = true,
                       Completed = true,
                       Message = message
                   };
        }

        public static MarcusB196CompletionResult Failed(string message)
        {
            return new MarcusB196CompletionResult
                   {
                       IsApplicable = true,
                       Attempted = true,
                       Completed = false,
                       Message = message
                   };
        }
    }
}
