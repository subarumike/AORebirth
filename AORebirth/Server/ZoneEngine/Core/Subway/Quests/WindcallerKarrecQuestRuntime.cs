namespace ZoneEngine.Core.Subway.Quests
{
    #region Usings ...

    using System;
    using System.Globalization;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;

    #endregion

    internal static class WindcallerKarrecQuestRuntime
    {
        internal const int SubwayPlayfieldId = WindcallerKarrecInteractionRules.PlayfieldId;
        internal const int WindcallerKarrecInstance = WindcallerKarrecInteractionRules.KarrecInstance;
        internal const int AnnoyingDudeInstance = unchecked((int)0x796360BD);
        internal const int MaddyCardileInstance = unchecked((int)0x796360BC);
        internal const int BrontoBurgerItemId = WindcallerKarrecInteractionRules.BurgerItemId;
        internal const int MaddyCreditCardItemId = WindcallerKarrecInteractionRules.CreditCardItemId;
        internal const int DailyMissionXpRewardItemId = 285612;
        internal const string AccountAccessFlagKey = "totw-wall-access";

        internal const string QuestId = "Mission:55579381";
        private const string ObjectiveId = "mission_55579381_deliver_offerings";
        private const string BurgerGrantFlag = "bronto-burger-granted";
        private const string CardGrantFlag = "maddy-credit-card-granted";
        private const string CompletionRewardSnapshotFlag = "completion-reward-snapshot-v1";

        internal static bool IsActive(ICharacter source)
        {
            if (!IsPlayerInSubway(source) || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, QuestId);
            return mission != null && mission.State == MissionLifecycleState.Active;
        }

        internal static bool IsCompleted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, QuestId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        internal static MissionOperationResult Accept(ICharacter source)
        {
            if (!IsPlayerInSubway(source) || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "Karrec acceptance requires an initialized mission runtime and a player in Subway 655."
                       };
            }

            int characterId = source.Identity.Instance;
            MissionOperationResult offer = MissionRuntime.Service.OfferMission(characterId, QuestId);
            if (IsPersistenceFailure(offer))
            {
                return offer;
            }

            return MissionRuntime.Service.AcceptMission(characterId, QuestId);
        }

        internal static bool TryGrantBurger(ICharacter source)
        {
            return TryGrantObjectiveItem(source, BrontoBurgerItemId, BurgerGrantFlag);
        }

        internal static bool TryGrantCreditCard(ICharacter source)
        {
            return TryGrantObjectiveItem(source, MaddyCreditCardItemId, CardGrantFlag);
        }

        internal static bool HasBothOfferingItems(ICharacter source)
        {
            return IsActive(source)
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       BrontoBurgerItemId)
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       MaddyCreditCardItemId);
        }

        internal static bool TryPrepareCompletionRewardSnapshot(ICharacter source)
        {
            if (!IsActive(source))
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            MissionFlagRecord existing = MissionRuntime.Service.GetFlag(
                characterId,
                QuestId,
                CompletionRewardSnapshotFlag);
            DailyMissionRewardSnapshot snapshot;
            if (existing != null)
            {
                return DailyMissionRewardRules.TryParseCompletionSnapshot(existing.Value, out snapshot);
            }

            if (!DailyMissionRewardRules.TryCreateCompletionSnapshot(
                source.Stats[StatIds.level].Value,
                source.Stats[StatIds.side].Value,
                out snapshot))
            {
                return false;
            }

            string serialized = DailyMissionRewardRules.SerializeCompletionSnapshot(snapshot);
            MissionOperationResult persisted = MissionRuntime.Service.SetFlag(
                characterId,
                QuestId,
                CompletionRewardSnapshotFlag,
                serialized);
            if (persisted.Status != MissionOperationStatus.Applied
                && persisted.Status != MissionOperationStatus.AlreadyApplied)
            {
                return false;
            }

            MissionFlagRecord readBack = MissionRuntime.Service.GetFlag(
                characterId,
                QuestId,
                CompletionRewardSnapshotFlag);
            return readBack != null
                   && string.Equals(readBack.Value, serialized, StringComparison.Ordinal)
                   && DailyMissionRewardRules.TryParseCompletionSnapshot(readBack.Value, out snapshot);
        }

        internal static KarrecCompletionResult CompleteAfterOfferingsConsumed(ICharacter source)
        {
            if (!IsPlayerInSubway(source) || !MissionRuntime.IsInitialized)
            {
                return KarrecCompletionResult.Failed("invalid-player-playfield-or-mission-runtime");
            }

            int characterId = source.Identity.Instance;
            MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, QuestId);
            if (mission == null
                || (mission.State != MissionLifecycleState.Active
                    && mission.State != MissionLifecycleState.Completed))
            {
                return KarrecCompletionResult.Failed("karrec-mission-not-active");
            }

            DailyMissionRewardSnapshot rewardSnapshot;
            if (!TryReadCompletionRewardSnapshot(characterId, out rewardSnapshot))
            {
                return KarrecCompletionResult.Failed("completion-reward-snapshot-missing-or-invalid");
            }

            string accountKey = MissionRuntime.ResolveAccountKey(characterId);
            if (string.IsNullOrWhiteSpace(accountKey))
            {
                // Prefer username, but never block TOTW passage if the character row is incomplete.
                accountKey = "character:" + characterId.ToString(CultureInfo.InvariantCulture);
            }

            if (mission.State == MissionLifecycleState.Active)
            {
                MissionOperationResult burger = ObserveOffering(
                    source,
                    BrontoBurgerItemId,
                    "trade-offering:297042");
                MissionOperationResult card = ObserveOffering(
                    source,
                    MaddyCreditCardItemId,
                    "trade-offering:297043");
                if (IsPersistenceFailure(burger) || IsPersistenceFailure(card))
                {
                    return KarrecCompletionResult.Failed(
                        "offering-observation-failed:"
                        + (IsPersistenceFailure(burger) ? burger.Message : card.Message));
                }

                MissionOperationResult completion = MissionRuntime.Service.CompleteMission(characterId, QuestId);
                if (completion.Status != MissionOperationStatus.Applied
                    && completion.Status != MissionOperationStatus.AlreadyApplied)
                {
                    return KarrecCompletionResult.Failed("completion-failed:" + completion.Message);
                }
            }

            MissionRewardExecutionResult sideTokens = ApplySideTokenReward(source, rewardSnapshot);
            if (!sideTokens.Succeeded)
            {
                return KarrecCompletionResult.Failed("side-token-reward-failed:" + sideTokens.Message);
            }

            int appliedSideTokenStatId;
            int appliedSideTokenReward;
            if (sideTokens.Stage == null
                || !DailyMissionRewardRules.TryResolveAppliedSideTokenForSnapshot(
                    rewardSnapshot,
                    sideTokens.Stage.EffectReference,
                    out appliedSideTokenStatId,
                    out appliedSideTokenReward))
            {
                return KarrecCompletionResult.Failed("side-token-reward-provenance-unresolved");
            }

            long sideTokenValue = 0;
            if (appliedSideTokenStatId != DailyMissionRewardRules.NoSideTokenStatId)
            {
                if (!TryGetPersistedStatValue(
                    sideTokens.StatValues,
                    appliedSideTokenStatId,
                    out sideTokenValue))
                {
                    sideTokenValue = source.Stats[(StatIds)appliedSideTokenStatId].BaseValue;
                }

                source.Stats[(StatIds)appliedSideTokenStatId].Set(
                    sideTokenValue <= 0
                        ? 0
                        : (uint)Math.Min(sideTokenValue, uint.MaxValue));
                source.Stats[(StatIds)appliedSideTokenStatId].Changed = false;
            }

            MissionRewardExecutionResult xpReward = ApplyFullLevelXpReward(source, rewardSnapshot);
            if (!xpReward.Succeeded)
            {
                return KarrecCompletionResult.Failed("full-level-xp-reward-failed:" + xpReward.Message);
            }

            int appliedXpLevel;
            int appliedXpReward;
            if (xpReward.Stage == null
                || !DailyMissionRewardRules.TryParseFullLevelXpEffectReference(
                    xpReward.Stage.EffectReference,
                    out appliedXpLevel,
                    out appliedXpReward)
                || appliedXpLevel != rewardSnapshot.LevelBefore
                || appliedXpReward != rewardSnapshot.XpReward)
            {
                return KarrecCompletionResult.Failed("full-level-xp-reward-provenance-unresolved");
            }

            if (!ContainsPersistedStatValue(xpReward.StatValues, (int)StatIds.xp)
                || !ContainsPersistedStatValue(xpReward.StatValues, (int)StatIds.lastxp))
            {
                return KarrecCompletionResult.Failed("full-level-xp-reward-values-unresolved");
            }

            ApplyPersistedStatValues(source, xpReward.StatValues);
            if (!CombatXpRuntimeService.ReconcilePersistedMissionXpRewardState(
                source,
                rewardSnapshot.LevelBefore))
            {
                return KarrecCompletionResult.Failed("full-level-xp-state-reconciliation-failed");
            }

            if (MissionRuntime.Service.GetAccountFlag(accountKey, AccountAccessFlagKey) == null)
            {
                MissionOperationResult accessFlag = MissionRuntime.Service.SetAccountFlag(
                    characterId,
                    accountKey,
                    QuestId,
                    AccountAccessFlagKey,
                    "completed:" + QuestId);
                if (accessFlag.Status != MissionOperationStatus.Applied
                    && accessFlag.Status != MissionOperationStatus.AlreadyApplied)
                {
                    return KarrecCompletionResult.Failed("account-flag-persistence-failed:" + accessFlag.Message);
                }
            }

            return KarrecCompletionResult.Succeeded(
                sideTokenValue,
                appliedSideTokenStatId,
                appliedSideTokenReward,
                rewardSnapshot.LevelBefore,
                rewardSnapshot.XpReward,
                sideTokens.Status,
                xpReward.Status,
                CloneStatValues(xpReward.StatValues));
        }

        internal static bool TryProjectXpReward(ICharacter source, KarrecCompletionResult completion)
        {
            if (source == null
                || completion == null
                || !completion.Completed
                || !ContainsPersistedStatValue(completion.XpStatValues, (int)StatIds.xp)
                || !ContainsPersistedStatValue(completion.XpStatValues, (int)StatIds.lastxp))
            {
                return false;
            }

            return CombatXpRuntimeService.TryProjectPersistedMissionXpReward(
                source,
                completion.LevelBefore);
        }

        internal static bool HasAccountAccess(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            string accountKey = MissionRuntime.ResolveAccountKey(characterId);
            if (!string.IsNullOrWhiteSpace(accountKey)
                && MissionRuntime.Service.GetAccountFlag(accountKey, AccountAccessFlagKey) != null)
            {
                return true;
            }

            string fallbackKey = "character:" + characterId.ToString(CultureInfo.InvariantCulture);
            return MissionRuntime.Service.GetAccountFlag(fallbackKey, AccountAccessFlagKey) != null;
        }

        private static MissionOperationResult ObserveOffering(
            ICharacter source,
            int itemId,
            string observationKey)
        {
            return MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = source.Identity.Instance,
                    QuestId = QuestId,
                    ObjectiveId = ObjectiveId,
                    ObservationKey = observationKey,
                    Amount = 1,
                    EventType = "KnuBotTrade:OfferingConsumed",
                    SourceIdentity = source.Identity.ToString(true),
                    TargetIdentity = "Item:" + itemId
                });
        }

        private static MissionRewardExecutionResult ApplySideTokenReward(
            ICharacter source,
            DailyMissionRewardSnapshot snapshot)
        {
            int mutationStatId = snapshot.SideTokenStatId == DailyMissionRewardRules.NoSideTokenStatId
                                     ? DailyMissionRewardRules.OmniSideTokenStatId
                                     : snapshot.SideTokenStatId;
            var definition = new MissionRewardDefinition
                             {
                                 // Retain this legacy key so already-applied completions cannot double-grant.
                                 RewardKey = "side-tokens-2",
                                 RewardType = "character-stats",
                                 IsResolved = true,
                                 StatMutations = new[]
                                                 {
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = mutationStatId,
                                                         Kind = MissionStatMutationKind.AddClamped,
                                                         Value = snapshot.SideTokenReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = int.MaxValue
                                                     }
                                                 }
                             };
            return MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                QuestId,
                definition,
                DailyMissionRewardRules.CreateSideTokenEffectReference(
                    snapshot.SideTokenStatId,
                    snapshot.SideTokenReward));
        }

        private static MissionRewardExecutionResult ApplyFullLevelXpReward(
            ICharacter source,
            DailyMissionRewardSnapshot snapshot)
        {
            var definition = new MissionRewardDefinition
                             {
                                 RewardKey = "daily-mission-full-level-xp-v1",
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
                QuestId,
                definition,
                DailyMissionRewardRules.CreateFullLevelXpEffectReference(
                    snapshot.LevelBefore,
                    snapshot.XpReward));
        }

        private static bool TryReadCompletionRewardSnapshot(
            int characterId,
            out DailyMissionRewardSnapshot snapshot)
        {
            snapshot = null;
            MissionFlagRecord flag = MissionRuntime.Service.GetFlag(
                characterId,
                QuestId,
                CompletionRewardSnapshotFlag);
            return flag != null
                   && DailyMissionRewardRules.TryParseCompletionSnapshot(flag.Value, out snapshot);
        }

        private static void ApplyPersistedStatValues(
            ICharacter source,
            IList<MissionCharacterStatValue> statValues)
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

        private static IList<MissionCharacterStatValue> CloneStatValues(
            IList<MissionCharacterStatValue> statValues)
        {
            var clones = new List<MissionCharacterStatValue>();
            if (statValues == null)
            {
                return clones;
            }

            foreach (MissionCharacterStatValue statValue in statValues)
            {
                if (statValue == null)
                {
                    continue;
                }

                clones.Add(
                    new MissionCharacterStatValue
                    {
                        StatIdentityType = statValue.StatIdentityType,
                        StatId = statValue.StatId,
                        Value = statValue.Value
                    });
            }

            return clones;
        }

        private static bool ContainsPersistedStatValue(
            IList<MissionCharacterStatValue> statValues,
            int statId)
        {
            long ignored;
            return TryGetPersistedStatValue(statValues, statId, out ignored);
        }

        private static bool TryGetPersistedStatValue(
            IList<MissionCharacterStatValue> statValues,
            int statId,
            out long value)
        {
            value = 0;
            if (statValues == null)
            {
                return false;
            }

            foreach (MissionCharacterStatValue statValue in statValues)
            {
                if (statValue != null
                    && statValue.StatIdentityType == (int)IdentityType.CanbeAffected
                    && statValue.StatId == statId)
                {
                    value = statValue.Value;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGrantObjectiveItem(ICharacter source, int itemId, string flagKey)
        {
            if (!IsActive(source))
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            if (MissionRuntime.Service.GetFlag(characterId, QuestId, flagKey) != null)
            {
                return true;
            }

            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(itemId))
            {
                return false;
            }

            if (!InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
            {
                Item item;
                try
                {
                    item = new Item(1, itemId, itemId);
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

                SendObjectiveItemNotifications(source, item);
            }

            MissionOperationResult flag = MissionRuntime.Service.SetFlag(
                characterId,
                QuestId,
                flagKey,
                "item:" + itemId);
            return flag.Status == MissionOperationStatus.Applied
                   || flag.Status == MissionOperationStatus.AlreadyApplied;
        }

        private static void SendObjectiveItemNotifications(ICharacter source, Item item)
        {
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
                    Target = new Identity { Type = IdentityType.OverflowWindow, Instance = source.Identity.Instance },
                    TargetPlacement = 0x6F
                });
        }

        private static bool IsPlayerInSubway(ICharacter source)
        {
            return source != null
                   && source.Identity.Type == IdentityType.CanbeAffected
                   && source.Identity.Instance != 0
                   && source.Playfield != null
                   && source.Playfield.Identity.Instance == SubwayPlayfieldId;
        }

        private static bool IsPersistenceFailure(MissionOperationResult result)
        {
            return result == null
                   || result.Status == MissionOperationStatus.Rejected
                   || result.Status == MissionOperationStatus.NotFound
                   || result.Status == MissionOperationStatus.Unresolved;
        }

    }

    internal sealed class KarrecCompletionResult
    {
        private KarrecCompletionResult()
        {
            this.XpStatValues = new MissionCharacterStatValue[0];
        }

        internal bool Completed { get; private set; }

        internal long SideTokenValue { get; private set; }

        internal int SideTokenStatId { get; private set; }

        internal int SideTokenReward { get; private set; }

        internal int XpReward { get; private set; }

        internal int LevelBefore { get; private set; }

        internal IList<MissionCharacterStatValue> XpStatValues { get; private set; }

        internal MissionRewardExecutionStatus SideTokenStatus { get; private set; }

        internal MissionRewardExecutionStatus XpStatus { get; private set; }

        internal string Error { get; private set; }

        internal static KarrecCompletionResult Succeeded(
            long sideTokenValue,
            int sideTokenStatId,
            int sideTokenReward,
            int levelBefore,
            int xpReward,
            MissionRewardExecutionStatus sideTokenStatus,
            MissionRewardExecutionStatus xpStatus,
            IList<MissionCharacterStatValue> xpStatValues)
        {
            return new KarrecCompletionResult
                   {
                       Completed = true,
                       SideTokenValue = sideTokenValue,
                       SideTokenStatId = sideTokenStatId,
                       SideTokenReward = sideTokenReward,
                       LevelBefore = levelBefore,
                       XpReward = xpReward,
                       SideTokenStatus = sideTokenStatus,
                       XpStatus = xpStatus,
                       XpStatValues = xpStatValues ?? new MissionCharacterStatValue[0]
                   };
        }

        internal static KarrecCompletionResult Failed(string error)
        {
            return new KarrecCompletionResult { Error = error };
        }
    }
}
