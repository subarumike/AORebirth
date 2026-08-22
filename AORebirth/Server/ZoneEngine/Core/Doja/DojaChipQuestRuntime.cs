namespace ZoneEngine.Core.Doja
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Subway.Quests;

    #endregion

    /// <summary>
    /// Capture-backed Nascense DOJA chip quest runtime (20260821-222107).
    /// </summary>
    internal static class DojaChipQuestRuntime
    {
        private const int CooldownDurationSeconds = 18 * 60 * 60;

        internal static bool TryHandleChipUse(ICharacter character, Identity itemPosition, Item item)
        {
            if (character == null || item == null)
            {
                return false;
            }

            DojaChipInteractionRules.DojaChipDefinition chip;
            if (!DojaChipInteractionRules.TryResolveChip(item.LowID, item.HighID, out chip))
            {
                return false;
            }

            int level = character.Stats[StatIds.level].Value;
            if (!DojaChipInteractionRules.IsLevelEligible(chip, level))
            {
                ChatTextMessageHandler.Default.Send(
                    character,
                    "You are not eligible to turn in this DOJA chip.");
                return true;
            }

            if (HasActiveCooldown(character))
            {
                ChatTextMessageHandler.Default.Send(
                    character,
                    "You've already turned in a DOJA chip today.");
                return true;
            }

            // Only Nascense is capture-backed so far (20260821-222107).
            if (!chip.IsImplemented)
            {
                ChatTextMessageHandler.Default.Send(
                    character,
                    "DOJA Chip " + chip.ZoneName + " is not available yet.");
                return true;
            }

            if (IsMissionActive(character, DojaChipInteractionRules.QuestTurnIn))
            {
                return true;
            }

            // Capture: TemplateAction Unknown2=3 at placement; chip is NOT consumed on use.
            TemplateActionMessageHandler.Default.Send(
                character,
                item,
                (int)itemPosition.Type,
                itemPosition.Instance);

            AcceptQuest(character, DojaChipInteractionRules.QuestTurnIn);
            return true;
        }

        internal static bool HasActiveCooldown(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (IsMissionActive(source, DojaChipInteractionRules.QuestCooldown))
            {
                return true;
            }

            DateTime untilUtc;
            if (TryReadCooldownUntilUtc(source, out untilUtc) && untilUtc > DateTime.UtcNow)
            {
                return true;
            }

            return HasAccountCooldownFlag(source);
        }

        internal static bool IsMissionActive(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Active;
        }

        internal static bool IsMissionCompleted(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        internal static MissionOperationResult AcceptQuest(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "doja-quest-runtime-unavailable"
                       };
            }

            int characterId = source.Identity.Instance;

            if (IsMissionActive(source, questId))
            {
                DojaChipPacketSender.TrySendQuestFullUpdate(source, questId);
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "doja-quest-already-active"
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
                DojaChipPacketSender.TrySendQuestFullUpdate(source, questId);
            }

            return accepted;
        }

        internal static MissionOperationResult CompleteQuest(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "doja-quest-runtime-unavailable"
                       };
            }

            return ForceCloseQuest(source, questId, ResolveObjectiveId(questId), "complete-quest");
        }

        internal static bool CanTalkToScarlett(ICharacter source)
        {
            // Capture: Scarlett always opens chat; turn-in option works only with active DOJA quest + chip.
            return source != null;
        }

        internal static bool CanTurnIn(ICharacter source)
        {
            return source != null
                   && IsMissionActive(source, DojaChipInteractionRules.QuestTurnIn)
                   && HasNascenseChip(source);
        }

        internal static bool HasNascenseChip(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       DojaChipInteractionRules.NascenseChipItemId);
        }

        /// <summary>
        /// Client mission journal clears on zone/relog. Re-emit Active turn-in and/or cooldown QFU
        /// (same durability pattern as Thrak / PerkReset).
        /// </summary>
        internal static bool TryResendActiveMissionsForLogin(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            bool sent = false;

            if (IsMissionActive(source, DojaChipInteractionRules.QuestTurnIn))
            {
                sent |= DojaChipPacketSender.TrySendQuestFullUpdate(
                    source,
                    DojaChipInteractionRules.QuestTurnIn);
            }

            DateTime untilUtc;
            bool hasCooldownUntil = TryReadCooldownUntilUtc(source, out untilUtc);
            if (hasCooldownUntil && untilUtc <= DateTime.UtcNow)
            {
                if (IsMissionActive(source, DojaChipInteractionRules.QuestCooldown))
                {
                    ForceCloseQuest(
                        source,
                        DojaChipInteractionRules.QuestCooldown,
                        ResolveObjectiveId(DojaChipInteractionRules.QuestCooldown),
                        "cooldown-expired-login");
                }

                return sent;
            }

            bool cooldownActive = IsMissionActive(source, DojaChipInteractionRules.QuestCooldown);
            if (!cooldownActive && !hasCooldownUntil)
            {
                return sent;
            }

            if (!cooldownActive)
            {
                AcceptQuest(source, DojaChipInteractionRules.QuestCooldown);
            }

            int remainingSeconds = CooldownDurationSeconds;
            if (hasCooldownUntil)
            {
                remainingSeconds = (int)Math.Ceiling((untilUtc - DateTime.UtcNow).TotalSeconds);
                if (remainingSeconds < 1)
                {
                    remainingSeconds = 1;
                }

                if (remainingSeconds > CooldownDurationSeconds)
                {
                    remainingSeconds = CooldownDurationSeconds;
                }
            }

            // AcceptQuest already emits a full-18h QFU; overwrite with remaining so Remain stays correct.
            sent |= DojaChipPacketSender.TrySendQuestFullUpdate(
                source,
                DojaChipInteractionRules.QuestCooldown,
                remainingSeconds);
            return sent;
        }

        internal static bool CompleteTurnIn(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            // Capture order after RejectedItems: complete turn-in → rewards → Action59+QuestDelete → cooldown QFU.
            // PersistComplete must run before ExecuteAtomicCharacterStats (authoritative-mission gate).
            // RejectedItems is sent by the trade adapter before this method.
            PersistCompleteQuest(source, DojaChipInteractionRules.QuestTurnIn);
            TryApplyTurnInRewards(source);
            DojaChipPacketSender.TrySendQuestDelete(source, DojaChipInteractionRules.QuestTurnIn);

            AcceptQuest(source, DojaChipInteractionRules.QuestCooldown);
            SetCooldownUntilUtc(source, DateTime.UtcNow.AddSeconds(CooldownDurationSeconds));

            if (MissionRuntime.IsInitialized)
            {
                MissionRuntime.Service.SetFlag(
                    source.Identity.Instance,
                    DojaChipInteractionRules.QuestCooldown,
                    DojaChipInteractionRules.TurnInGrantedFlag,
                    "1");
            }

            return true;
        }

        internal static bool TryApplyTurnInRewards(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int levelBefore = source.Stats[StatIds.level].Value;
            DailyMissionRewardSnapshot snapshot;
            if (!DailyMissionRewardRules.TryCreateCompletionSnapshot(
                    levelBefore,
                    source.Stats[StatIds.side].Value,
                    out snapshot))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "DOJA_NASCENSE reward snapshot failed level=" + levelBefore
                    + " side=" + source.Stats[StatIds.side].Value);
                return false;
            }

            // Same order as WindcallerKarrec: side tokens → full-level XP → reconcile/project (XP+SK).
            long sideTokenValue = 0;
            if (snapshot.SideTokenReward > 0
                && snapshot.SideTokenStatId != DailyMissionRewardRules.NoSideTokenStatId)
            {
                MissionRewardExecutionResult tokenResult = ApplySideTokenReward(source, snapshot);
                if (!tokenResult.Succeeded)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "DOJA_NASCENSE side-token reward failed: " + tokenResult.Message);
                    return false;
                }

                ApplyPersistedStatValues(source, tokenResult.StatValues);
                sideTokenValue = source.Stats[(StatIds)snapshot.SideTokenStatId].BaseValue;
            }

            MissionRewardExecutionResult xpResult = ApplyXpReward(source, snapshot);
            if (!xpResult.Succeeded)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "DOJA_NASCENSE xp reward failed: " + xpResult.Message);
                return false;
            }

            ApplyPersistedStatValues(source, xpResult.StatValues);
            CombatXpRuntimeService.ReconcilePersistedMissionXpRewardState(source, levelBefore);
            if (!CombatXpRuntimeService.TryProjectPersistedMissionXpReward(source, levelBefore))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "DOJA_NASCENSE xp/sk client projection failed levelBefore=" + levelBefore);
            }

            if (snapshot.SideTokenReward > 0
                && snapshot.SideTokenStatId != DailyMissionRewardRules.NoSideTokenStatId)
            {
                if (!WindcallerKarrecPacketSender.TrySendSideTokenProjection(
                        source,
                        snapshot.SideTokenStatId,
                        snapshot.SideTokenReward,
                        sideTokenValue))
                {
                    SendSideTokenFeedback(source, sideTokenValue);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "DOJA_NASCENSE rewards applied xp=" + snapshot.XpReward
                + " tokens=" + snapshot.SideTokenReward
                + " tokenStat=" + snapshot.SideTokenStatId
                + " levelBefore=" + levelBefore);
            return true;
        }

        private static MissionRewardExecutionResult ApplyXpReward(
            ICharacter source,
            DailyMissionRewardSnapshot snapshot)
        {
            // Match Karrec daily-mission full-level XP mutations (xp + lastxp only).
            // UnsavedXP / level / SK are handled by CombatXpRuntimeService reconcile+project.
            var definition = new MissionRewardDefinition
                             {
                                 RewardKey = "doja-nascense-full-level-xp-v1",
                                 RewardType = "character-stats",
                                 IsResolved = true,
                                 StatMutations = new[]
                                                 {
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = (int)StatIds.xp,
                                                         Kind = MissionStatMutationKind.AddClamped,
                                                         Value = snapshot.XpReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = int.MaxValue
                                                     },
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = (int)StatIds.lastxp,
                                                         Kind = MissionStatMutationKind.Set,
                                                         Value = snapshot.XpReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = int.MaxValue
                                                     }
                                                 }
                             };
            return MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                DojaChipInteractionRules.QuestTurnIn,
                definition,
                DailyMissionRewardRules.CreateFullLevelXpEffectReference(
                    snapshot.LevelBefore,
                    snapshot.XpReward));
        }

        private static MissionRewardExecutionResult ApplySideTokenReward(
            ICharacter source,
            DailyMissionRewardSnapshot snapshot)
        {
            var definition = new MissionRewardDefinition
                             {
                                 RewardKey = "doja-nascense-side-tokens-v1",
                                 RewardType = "character-stats",
                                 IsResolved = true,
                                 StatMutations = new[]
                                                 {
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = snapshot.SideTokenStatId,
                                                         Kind = MissionStatMutationKind.AddClamped,
                                                         Value = snapshot.SideTokenReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = int.MaxValue
                                                     }
                                                 }
                             };
            return MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                DojaChipInteractionRules.QuestTurnIn,
                definition,
                DailyMissionRewardRules.CreateSideTokenEffectReference(
                    snapshot.SideTokenStatId,
                    snapshot.SideTokenReward));
        }

        private static void SendSideTokenFeedback(ICharacter source, long sideTokenValue)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return;
            }

            try
            {
                source.Controller.Client.SendCompressed(
                    new FormatFeedbackMessage
                    {
                        Identity = source.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        FormattedMessage = "Side tokens collected: " + sideTokenValue + ".",
                        Unknown2 = 0
                    });
            }
            catch (Exception)
            {
            }
        }

        private static void ApplyPersistedStatValues(
            ICharacter source,
            System.Collections.Generic.IList<MissionCharacterStatValue> statValues)
        {
            if (source == null || statValues == null)
            {
                return;
            }

            foreach (MissionCharacterStatValue statValue in statValues)
            {
                if (statValue == null
                    || statValue.StatIdentityType != (int)IdentityType.CanbeAffected
                    || statValue.StatId < 0)
                {
                    continue;
                }

                uint value = statValue.Value <= 0
                                 ? 0
                                 : (uint)Math.Min(statValue.Value, uint.MaxValue);
                if (source.Stats[(StatIds)statValue.StatId].BaseValue != value)
                {
                    source.Stats[(StatIds)statValue.StatId].Set(value);
                }
            }
        }

        private static MissionOperationResult ForceCloseQuest(
            ICharacter source,
            string questId,
            string objectiveId,
            string reason)
        {
            MissionOperationResult completed = PersistCompleteQuest(source, questId, objectiveId, reason);
            if (IsClientEmitSuccess(completed) || completed.Status == MissionOperationStatus.AlreadyApplied)
            {
                DojaChipPacketSender.TrySendQuestDelete(source, questId);
            }

            return completed;
        }

        /// <summary>
        /// Complete/abandon mission in MissionRuntime only — no client QuestDelete wire.
        /// </summary>
        private static MissionOperationResult PersistCompleteQuest(
            ICharacter source,
            string questId,
            string objectiveId = null,
            string reason = "persist-complete")
        {
            if (source == null || string.IsNullOrWhiteSpace(questId) || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "doja-force-close-unavailable"
                       };
            }

            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                objectiveId = ResolveObjectiveId(questId);
            }

            int characterId = source.Identity.Instance;
            if (IsMissionActive(source, questId) && !string.IsNullOrWhiteSpace(objectiveId))
            {
                MissionRuntime.Service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = characterId,
                        QuestId = questId,
                        ObjectiveId = objectiveId,
                        ObservationKey = "doja:" + reason + ":" + questId,
                        Amount = 1,
                        EventType = "DojaChip:ForceClose",
                        SourceIdentity = source.Identity.ToString(true),
                        TargetIdentity = questId
                    });
            }

            MissionOperationResult completed = MissionRuntime.Service.CompleteMission(characterId, questId);
            if (IsPersistenceFailure(completed) && IsMissionActive(source, questId))
            {
                completed = MissionRuntime.Service.AbandonMission(characterId, questId);
            }

            return completed;
        }

        private static string ResolveObjectiveId(string questId)
        {
            if (string.Equals(questId, DojaChipInteractionRules.QuestTurnIn, StringComparison.OrdinalIgnoreCase))
            {
                return "mission_55AA2421_turnin";
            }

            if (string.Equals(questId, DojaChipInteractionRules.QuestCooldown, StringComparison.OrdinalIgnoreCase))
            {
                return "mission_55AA2803_cooldown";
            }

            return null;
        }

        private static void SetCooldownUntilUtc(ICharacter source, DateTime untilUtc)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            string serialized = untilUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            MissionRuntime.Service.SetFlag(
                characterId,
                DojaChipInteractionRules.QuestCooldown,
                DojaChipInteractionRules.CooldownFlag,
                serialized);
            MissionRuntime.Service.SetFlag(
                characterId,
                DojaChipInteractionRules.QuestTurnIn,
                DojaChipInteractionRules.CooldownFlag,
                serialized);

            string accountKey = MissionRuntime.ResolveAccountKey(characterId);
            if (string.IsNullOrWhiteSpace(accountKey))
            {
                accountKey = "character:" + characterId.ToString(CultureInfo.InvariantCulture);
            }

            MissionRuntime.Service.SetAccountFlag(
                characterId,
                accountKey,
                DojaChipInteractionRules.QuestCooldown,
                DojaChipInteractionRules.CooldownFlag,
                serialized);
        }

        private static bool TryReadCooldownUntilUtc(ICharacter source, out DateTime untilUtc)
        {
            untilUtc = DateTime.MinValue;
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            MissionFlagRecord flag = MissionRuntime.Service.GetFlag(
                characterId,
                DojaChipInteractionRules.QuestCooldown,
                DojaChipInteractionRules.CooldownFlag);
            if (flag == null || string.IsNullOrWhiteSpace(flag.Value))
            {
                flag = MissionRuntime.Service.GetFlag(
                    characterId,
                    DojaChipInteractionRules.QuestTurnIn,
                    DojaChipInteractionRules.CooldownFlag);
            }

            if (flag == null || string.IsNullOrWhiteSpace(flag.Value))
            {
                return false;
            }

            return DateTime.TryParse(
                       flag.Value,
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.RoundtripKind,
                       out untilUtc);
        }

        private static bool HasAccountCooldownFlag(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            string accountKey = MissionRuntime.ResolveAccountKey(characterId);
            if (string.IsNullOrWhiteSpace(accountKey))
            {
                accountKey = "character:" + characterId.ToString(CultureInfo.InvariantCulture);
            }

            MissionAccountFlagRecord flag = MissionRuntime.Service.GetAccountFlag(
                accountKey,
                DojaChipInteractionRules.CooldownFlag);
            if (flag == null || string.IsNullOrWhiteSpace(flag.Value))
            {
                return false;
            }

            DateTime untilUtc;
            if (!DateTime.TryParse(
                    flag.Value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out untilUtc))
            {
                return flag != null;
            }

            return untilUtc > DateTime.UtcNow;
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
    }
}
