namespace ZoneEngine.Core.Subway.Quests
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;
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
        internal const int SideTokenStatId = 75;
        internal const int SideTokenReward = 2;
        internal const int PersonalResearchXpAllocation = 5000;
        internal const string AccountAccessFlagKey = "totw-wall-access";

        internal const string QuestId = "Mission:55579381";
        private const string ObjectiveId = "mission_55579381_deliver_offerings";
        private const string BurgerGrantFlag = "bronto-burger-granted";
        private const string CardGrantFlag = "maddy-credit-card-granted";
        private const string ResearchAllocationFlag = "personal-research-xp-allocation";
        private const string LevelXpRewardFlag = "one-level-xp-reward";

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

            // TOTW gateway access is the turn-in contract. Side token / research feedback are
            // best-effort so a reward-writer failure cannot strand the player without passage.
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

            TryAwardOneLevelXpReward(source, characterId);

            MissionRewardExecutionResult sideTokens = ApplySideTokenReward(source);
            MissionRewardExecutionStatus researchStatus = MissionRewardExecutionStatus.Unresolved;
            var researchDefinition = new MissionRewardDefinition
                                     {
                                         RewardKey = "personal-research-xp-5000",
                                         RewardType = "personal-research-allocation",
                                         IsResolved = true
                                     };
            MissionRewardExecutionResult research = MissionRuntime.Rewards.ExecuteExternal(
                characterId,
                QuestId,
                researchDefinition,
                new PersonalResearchAllocationEffect(characterId));
            if (research != null && research.Succeeded)
            {
                researchStatus = research.Status;
            }

            long sideTokenValue = source.Stats[(StatIds)SideTokenStatId].BaseValue;
            if (sideTokens != null && sideTokens.Succeeded && sideTokens.StatValues != null)
            {
                foreach (MissionCharacterStatValue statValue in sideTokens.StatValues)
                {
                    if (statValue.StatId != SideTokenStatId)
                    {
                        continue;
                    }

                    sideTokenValue = statValue.Value;
                    source.Stats[(StatIds)SideTokenStatId].Set(
                        statValue.Value <= 0
                            ? 0
                            : (uint)Math.Min(statValue.Value, uint.MaxValue));
                }

                StatMessageHandler.Default.SendChanged(source);
            }

            return KarrecCompletionResult.Succeeded(
                sideTokenValue,
                sideTokens != null && sideTokens.Succeeded
                    ? sideTokens.Status
                    : MissionRewardExecutionStatus.Unresolved,
                researchStatus);
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

        private static void TryAwardOneLevelXpReward(ICharacter source, int characterId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            if (MissionRuntime.Service.GetFlag(characterId, QuestId, LevelXpRewardFlag) != null)
            {
                return;
            }

            int xpNeeded = CombatXpRuntimeService.GetXpNeededForNextLevel(source);
            if (xpNeeded <= 0)
            {
                MissionRuntime.Service.SetFlag(
                    characterId,
                    QuestId,
                    LevelXpRewardFlag,
                    "skipped-max-level");
                return;
            }

            bool awarded = CombatXpRuntimeService.AwardDirectXp(
                source,
                xpNeeded,
                "karrec-quest");
            MissionRuntime.Service.SetFlag(
                characterId,
                QuestId,
                LevelXpRewardFlag,
                awarded
                    ? "awarded:" + xpNeeded.ToString(CultureInfo.InvariantCulture)
                    : "failed:" + xpNeeded.ToString(CultureInfo.InvariantCulture));
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

        private static MissionRewardExecutionResult ApplySideTokenReward(ICharacter source)
        {
            var definition = new MissionRewardDefinition
                             {
                                 RewardKey = "side-tokens-2",
                                 RewardType = "character-stats",
                                 IsResolved = true,
                                 StatMutations = new[]
                                                 {
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = SideTokenStatId,
                                                         Kind = MissionStatMutationKind.AddClamped,
                                                         Value = SideTokenReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = uint.MaxValue
                                                     }
                                                 }
                             };
            return MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                QuestId,
                definition,
                "capture:20260717-223626:stat-75-plus-2");
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

        private sealed class PersonalResearchAllocationEffect : IMissionRewardEffect
        {
            private readonly int characterId;

            public PersonalResearchAllocationEffect(int characterId)
            {
                this.characterId = characterId;
            }

            public MissionRewardEffectResult Apply(MissionRewardExecutionContext context)
            {
                MissionFlagRecord existing = MissionRuntime.Service.GetFlag(
                    this.characterId,
                    QuestId,
                    ResearchAllocationFlag);
                if (existing != null)
                {
                    return MissionRewardEffectResult.AlreadyApplied(
                        "mission-flag:" + ResearchAllocationFlag + ":5000");
                }

                MissionOperationResult flag = MissionRuntime.Service.SetFlag(
                    this.characterId,
                    QuestId,
                    ResearchAllocationFlag,
                    PersonalResearchXpAllocation.ToString());
                return flag.Status == MissionOperationStatus.Applied
                           || flag.Status == MissionOperationStatus.AlreadyApplied
                           ? MissionRewardEffectResult.Applied(
                               "mission-flag:" + ResearchAllocationFlag + ":5000")
                           : MissionRewardEffectResult.RetryableFailure(flag.Message);
            }
        }
    }

    internal sealed class KarrecCompletionResult
    {
        private KarrecCompletionResult()
        {
        }

        internal bool Completed { get; private set; }

        internal long SideTokenValue { get; private set; }

        internal MissionRewardExecutionStatus SideTokenStatus { get; private set; }

        internal MissionRewardExecutionStatus ResearchStatus { get; private set; }

        internal string Error { get; private set; }

        internal static KarrecCompletionResult Succeeded(
            long sideTokenValue,
            MissionRewardExecutionStatus sideTokenStatus,
            MissionRewardExecutionStatus researchStatus)
        {
            return new KarrecCompletionResult
                   {
                       Completed = true,
                       SideTokenValue = sideTokenValue,
                       SideTokenStatus = sideTokenStatus,
                       ResearchStatus = researchStatus
                   };
        }

        internal static KarrecCompletionResult Failed(string error)
        {
            return new KarrecCompletionResult { Error = error };
        }
    }
}
