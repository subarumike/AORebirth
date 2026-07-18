namespace ZoneEngine.Core.Arete.Quests
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

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    public static class MarcusB18FCompletionHandler
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const int MarcusStoneInstance = unchecked((int)0x782DE567);

        private const string B18FCompletionSourceNodeId = "marcus_195107_b18f_002";

        private const int B18FCompletionAnswerIndex = 0;

        private const string B18FCompletionOptionText =
            "So, let me guess... You need some help with the fire?";

        private const int CompactFireSuppressantItemId = 296780;

        private const int CompactFireSuppressantQuality = 1;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private const string MissionId = "Mission:5514B18F";

        private const string ItemRewardKey = "compact-fire-suppressant-296780";

        private const string ItemRewardBaselineFlag = "compact-fire-suppressant-baseline-count";

        public static MarcusB18FCompletionResult TryCompleteFromDialogue(
            ICharacter source,
            Identity npcIdentity,
            string previousNodeId,
            int answerIndex,
            string optionText,
            bool dialogueGateEnabled)
        {
            if (!IsB18FCompletionOption(previousNodeId, answerIndex))
            {
                return MarcusB18FCompletionResult.NotApplicable();
            }

            if (!string.Equals(optionText, B18FCompletionOptionText, StringComparison.Ordinal))
            {
                return MarcusB18FCompletionResult.Failed(
                    "Marcus B18F completion blocked because captured option text did not match. "
                    + "node=" + previousNodeId
                    + " answer=" + answerIndex
                    + " noQuestDelete=true noB194=true noItem296780=true");
            }

            if (!dialogueGateEnabled)
            {
                return MarcusB18FCompletionResult.Skipped(
                    "Marcus B18F completion skipped because dialogue routing gate is disabled. "
                    + "attempted=false noQuestDelete=true noB194=true noItem296780=true");
            }

            if (!IsMarcusStone(npcIdentity))
            {
                return MarcusB18FCompletionResult.Failed(
                    "Marcus B18F completion failed: target is not Marcus Stone. noQuestDelete=true noB194=true");
            }

            if (!IsValidPlayerInArete(source))
            {
                return MarcusB18FCompletionResult.Failed(
                    "Marcus B18F completion failed: source is missing, not a player, or not in Arete Landing 6553. "
                    + "noQuestDelete=true noB194=true");
            }

            if (!MissionRuntime.IsInitialized)
            {
                return MarcusB18FCompletionResult.Failed(
                    "Marcus B18F completion failed: persistent mission runtime is not initialized.");
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, MissionId);
            if (mission == null
                || (mission.State != MissionLifecycleState.Active
                    && mission.State != MissionLifecycleState.Completed))
            {
                return MarcusB18FCompletionResult.Skipped(
                    "Marcus B18F completion skipped because the persistent mission is not active.");
            }

            if (mission.State == MissionLifecycleState.Active)
            {
                MissionOperationResult completion = MissionRuntime.Service.CompleteMission(characterId, MissionId);
                if (completion.Status != MissionOperationStatus.Applied
                    && completion.Status != MissionOperationStatus.AlreadyApplied)
                {
                    return MarcusB18FCompletionResult.Failed(
                        "Marcus B18F persistence failed: " + completion.Message);
                }
            }

            var rewardDefinition = new MissionRewardDefinition
                                   {
                                       RewardKey = ItemRewardKey,
                                       RewardType = "inventory-item",
                                       IsResolved = true
                                   };
            MissionRewardExecutionResult itemReward = MissionRuntime.Rewards.ExecuteExternal(
                characterId,
                MissionId,
                rewardDefinition,
                new CompactFireSuppressantRewardEffect(source));
            if (!itemReward.Succeeded)
            {
                return MarcusB18FCompletionResult.Failed(
                    "Marcus B18F item reward remains retryable: " + itemReward.Message);
            }

            MissionOperationResult b194Transition = MissionRuntime.Service.CompleteAndActivateNextMission(
                characterId,
                MissionId,
                MissionRuntime.RexB194QuestId);
            if (IsPersistenceFailure(b194Transition))
            {
                return MarcusB18FCompletionResult.Failed(
                    "B194 handoff persistence failed: " + b194Transition.Message);
            }

            bool deleteProjected = EnsureQuestProjection(
                source,
                "b18f-delete-projected",
                () => SafeQuestFullUpdateSender.TrySendB18FQuestDelete(source));
            bool b194Projected = EnsureQuestProjection(
                source,
                "b194-preview-projected",
                () => SafeQuestFullUpdateSender.TrySendB194Preview(source));
            if (!deleteProjected || !b194Projected)
            {
                return MarcusB18FCompletionResult.Failed(
                    "Marcus B18F state and item reward are durable, but a client projection remains retryable.");
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ARETE_MARCUS_B18F_COMPLETION transition applied character="
                + source.Identity.ToString(true)
                + " node=" + previousNodeId
                + " answer=" + answerIndex
                + " missionDelete=" + MissionId
                + " nextQuestFullUpdate=" + MissionRuntime.RexB194QuestId
                + " rewardStatus=" + itemReward.Status
                + " persistent=true");

            return MarcusB18FCompletionResult.Succeeded(
                "Marcus B18F completion applied persistently b18fDeleteProjected="
                + deleteProjected
                + " b194QuestFullUpdateProjected="
                + b194Projected
                + " item296780Reward="
                + itemReward.Status);
        }

        private static bool EnsureQuestProjection(
            ICharacter source,
            string flagKey,
            Func<RexQuestPreviewEmissionResult> sender)
        {
            int characterId = source.Identity.Instance;
            if (MissionRuntime.Service.GetFlag(characterId, MissionId, flagKey) != null)
            {
                return true;
            }

            RexQuestPreviewEmissionResult result = sender();
            if (result == null || !result.Emitted)
            {
                return false;
            }

            MissionOperationResult flag = MissionRuntime.Service.SetFlag(
                characterId,
                MissionId,
                flagKey,
                "true");
            return flag.Status == MissionOperationStatus.Applied
                   || flag.Status == MissionOperationStatus.AlreadyApplied;
        }

        private static bool IsPersistenceFailure(MissionOperationResult result)
        {
            return result == null
                   || result.Status == MissionOperationStatus.Rejected
                   || result.Status == MissionOperationStatus.NotFound
                   || result.Status == MissionOperationStatus.Unresolved;
        }

        private static bool IsB18FCompletionOption(string previousNodeId, int answerIndex)
        {
            return string.Equals(previousNodeId, B18FCompletionSourceNodeId, StringComparison.OrdinalIgnoreCase)
                   && answerIndex == B18FCompletionAnswerIndex;
        }

        private static bool IsMarcusStone(Identity identity)
        {
            return identity.Type == IdentityType.CanbeAffected
                   && identity.Instance == MarcusStoneInstance;
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

        private static MarcusItemHandoutResult TryGrantCompactFireSuppressant(ICharacter source)
        {
            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source))
            {
                return MarcusItemHandoutResult.Failed("sourceInventoryAvailable=false");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return MarcusItemHandoutResult.Failed("sourceClientAvailable=false");
            }

            if (!ItemLoader.ItemList.ContainsKey(CompactFireSuppressantItemId))
            {
                return MarcusItemHandoutResult.Failed("itemTemplate296780Available=false");
            }

            Item item;
            try
            {
                item = new Item(
                    CompactFireSuppressantQuality,
                    CompactFireSuppressantItemId,
                    CompactFireSuppressantItemId);
            }
            catch (Exception e)
            {
                return MarcusItemHandoutResult.Failed(
                    "item296780CreateFailed=true error=\"" + e.Message + "\"");
            }

            QuestRewardInventoryGrantResult inventoryGrant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (inventoryGrant.Status == QuestRewardInventoryGrantStatus.InventoryAddFailed)
            {
                return MarcusItemHandoutResult.Failed(
                    "item296780InventoryAddFailed=true error=" + inventoryGrant.InventoryError);
            }

            if (inventoryGrant.Status == QuestRewardInventoryGrantStatus.PersistFailed)
            {
                return MarcusItemHandoutResult.Failed(
                    "item296780InventoryPersistFailed=true error=\"" + inventoryGrant.ExceptionMessage + "\"");
            }

            if (inventoryGrant.Status == QuestRewardInventoryGrantStatus.PersistReturnedFalse)
            {
                return MarcusItemHandoutResult.Failed("item296780InventoryPersistFailed=true writeReturnedFalse=true");
            }

            try
            {
                SendCompactFireSuppressantNotifications(source, item);
            }
            catch (Exception e)
            {
                return MarcusItemHandoutResult.Failed(
                    "item296780ClientNotifyFailed=true error=\"" + e.Message + "\"");
            }

            return MarcusItemHandoutResult.Succeeded(
                "item296780Granted=true inventoryPersisted=true notifications=TemplateAction,ContainerAddItem");
        }

        private sealed class CompactFireSuppressantRewardEffect : IMissionRewardEffect
        {
            private readonly ICharacter source;

            public CompactFireSuppressantRewardEffect(ICharacter source)
            {
                this.source = source;
            }

            public MissionRewardEffectResult Apply(MissionRewardExecutionContext context)
            {
                int currentCount = InventoryContainerRuntimeService.Default.CountCharacterItemInCarriedInventory(
                    this.source,
                    CompactFireSuppressantItemId);
                MissionFlagRecord baselineFlag = MissionRuntime.Service.GetFlag(
                    context.CharacterId,
                    MissionId,
                    ItemRewardBaselineFlag);
                int baselineCount;
                if (baselineFlag == null)
                {
                    baselineCount = currentCount;
                    MissionOperationResult persistedBaseline = MissionRuntime.Service.SetFlag(
                        context.CharacterId,
                        MissionId,
                        ItemRewardBaselineFlag,
                        baselineCount.ToString(CultureInfo.InvariantCulture));
                    if (persistedBaseline.Status != MissionOperationStatus.Applied
                        && persistedBaseline.Status != MissionOperationStatus.AlreadyApplied)
                    {
                        return MissionRewardEffectResult.RetryableFailure(
                            "Unable to persist the item reward inventory baseline: "
                            + persistedBaseline.Message);
                    }
                }
                else if (!int.TryParse(
                             baselineFlag.Value,
                             NumberStyles.Integer,
                             CultureInfo.InvariantCulture,
                             out baselineCount)
                         || baselineCount < 0)
                {
                    return MissionRewardEffectResult.RetryableFailure(
                        "The persisted item reward inventory baseline is invalid.");
                }

                if (currentCount > baselineCount)
                {
                    return MissionRewardEffectResult.AlreadyApplied(
                        "inventory-item:296780:character:"
                        + context.CharacterId.ToString(CultureInfo.InvariantCulture));
                }

                MarcusItemHandoutResult result = TryGrantCompactFireSuppressant(this.source);
                if (!result.Completed)
                {
                    return MissionRewardEffectResult.RetryableFailure(result.Message);
                }

                string effectReference = "inventory-item:296780:character:"
                                         + context.CharacterId.ToString(CultureInfo.InvariantCulture);
                return MissionRewardEffectResult.Applied(effectReference);
            }
        }

        private static void SendCompactFireSuppressantNotifications(ICharacter source, Item item)
        {
            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = item.LowID,
                    ItemHighId = item.HighID,
                    Quality = item.Quality,
                    Unknown1 = CapturedTemplateActionUnknown1,
                    Unknown2 = CapturedTemplateActionUnknown2,
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
                    TargetPlacement = CapturedOverflowNextFreeSlot
                });
        }

        private sealed class MarcusItemHandoutResult
        {
            private MarcusItemHandoutResult()
            {
            }

            public bool Completed { get; private set; }

            public string Message { get; private set; }

            public static MarcusItemHandoutResult Succeeded(string message)
            {
                return new MarcusItemHandoutResult
                       {
                           Completed = true,
                           Message = message
                       };
            }

            public static MarcusItemHandoutResult Failed(string message)
            {
                return new MarcusItemHandoutResult
                       {
                           Completed = false,
                           Message = message
                       };
            }
        }
    }

    public sealed class MarcusB18FCompletionResult
    {
        private MarcusB18FCompletionResult()
        {
        }

        public bool IsApplicable { get; private set; }

        public bool Attempted { get; private set; }

        public bool Completed { get; private set; }

        public string Message { get; private set; }

        public static MarcusB18FCompletionResult NotApplicable()
        {
            return new MarcusB18FCompletionResult();
        }

        public static MarcusB18FCompletionResult Skipped(string message)
        {
            return new MarcusB18FCompletionResult
                   {
                       IsApplicable = true,
                       Attempted = false,
                       Completed = false,
                       Message = message
                   };
        }

        public static MarcusB18FCompletionResult Succeeded(string message)
        {
            return new MarcusB18FCompletionResult
                   {
                       IsApplicable = true,
                       Attempted = true,
                       Completed = true,
                       Message = message
                   };
        }

        public static MarcusB18FCompletionResult Failed(string message)
        {
            return new MarcusB18FCompletionResult
                   {
                       IsApplicable = true,
                       Attempted = true,
                       Completed = false,
                       Message = message
                   };
        }
    }
}
