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

        private const string ObjectiveId = "mission_5514b18f_objective_questfullupdate";

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

            // Node+index is capture-authoritative; option text is best-effort (ellipsis/encoding drift).
            if (!string.IsNullOrWhiteSpace(optionText)
                && !string.Equals(optionText.Trim(), B18FCompletionOptionText, StringComparison.Ordinal))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "ARETE_MARCUS_B18F_COMPLETION option text drift node="
                    + previousNodeId
                    + " answer="
                    + answerIndex
                    + " got=\""
                    + optionText
                    + "\" expected=\""
                    + B18FCompletionOptionText
                    + "\" proceeding=true");
            }

            if (!dialogueGateEnabled)
            {
                return MarcusB18FCompletionResult.Skipped(
                    "Marcus B18F completion skipped because dialogue routing gate is disabled. "
                    + "attempted=false noQuestDelete=true noB194=true noItem296780=true");
            }

            // Router always passes dossier identity; also accept live name-bound Marcus.
            if (!IsMarcusStone(npcIdentity) && !IsMarcusStoneNameBound(source, npcIdentity))
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
            ZoneEngine.Core.Missions.MissionStateRecord b196 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB196QuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b194 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB194QuestId);

            bool fireAlreadyDone = (b196 != null
                                    && (b196.State == MissionLifecycleState.Active
                                        || b196.State == MissionLifecycleState.Completed
                                        || b196.State == MissionLifecycleState.Offered))
                                   || (b194 != null && b194.State == MissionLifecycleState.Completed);

            // Fire already done — never re-handout suppressant or complete B196 on dialogue open/answer.
            // Capture: B196 completes only after suppressant trade (RexMarcusChainCoordinator).
            if (fireAlreadyDone)
            {
                return MarcusB18FCompletionResult.Skipped(
                    "Marcus fire handout blocked: fire chain already finished. noItem296780=true noB194=true");
            }

            bool hasSuppressant =
                InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    CompactFireSuppressantItemId);

            // Capture path is dialogue → item 296780 → B18F delete → B194 QFU.
            bool b18fReady = EnsureB18FActive(characterId);
            if (!b18fReady)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ARETE_MARCUS_B18F_COMPLETION EnsureB18FActive failed — continuing item+B194 client projection");
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, MissionId);
            if (mission != null && mission.State == MissionLifecycleState.Active)
            {
                MissionRuntime.Service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = characterId,
                        QuestId = MissionId,
                        ObjectiveId = ObjectiveId,
                        ObservationKey = "dialogue-fire-option:" + previousNodeId + ":" + answerIndex,
                        Amount = 1,
                        EventType = "NpcDialogueAnswer",
                        SourceIdentity = source.Identity.ToString(true),
                        TargetIdentity = npcIdentity.ToString(true)
                    });

                MissionOperationResult completion = MissionRuntime.Service.CompleteMission(characterId, MissionId);
                if (completion.Status != MissionOperationStatus.Applied
                    && completion.Status != MissionOperationStatus.AlreadyApplied)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "ARETE_MARCUS_B18F_COMPLETION CompleteMission status="
                        + completion.Status
                        + " message=\""
                        + completion.Message
                        + "\" — continuing item+B194 projection");
                }
            }

            // Always attempt grant unless the Unique suppressant is already carried.
            MarcusItemHandoutResult directGrant = hasSuppressant
                                                      ? MarcusItemHandoutResult.Succeeded(
                                                          "item296780AlreadyPresent=true skipGrant=true")
                                                      : TryGrantCompactFireSuppressant(source);
            bool itemOk = directGrant.Completed
                          || InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                              source,
                              CompactFireSuppressantItemId);
            if (!itemOk)
            {
                MissionRewardExecutionResult itemReward = MissionRuntime.Rewards.ExecuteExternal(
                    characterId,
                    MissionId,
                    new MissionRewardDefinition
                    {
                        RewardKey = ItemRewardKey,
                        RewardType = "inventory-item",
                        IsResolved = true
                    },
                    new CompactFireSuppressantRewardEffect(source));
                itemOk = InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    CompactFireSuppressantItemId);
                if (!itemOk)
                {
                    // Ledger AlreadyApplied without inventory is not success — force another direct try
                    // after clearing Unique conflict by treating as hard grant path again.
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "ARETE_MARCUS_B18F_COMPLETION item still missing after ledger direct=\""
                        + directGrant.Message
                        + "\" ledger=\""
                        + itemReward.Message
                        + "\" — retrying direct grant");
                    directGrant = TryGrantCompactFireSuppressant(source);
                    itemOk = directGrant.Completed
                             || InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                                 source,
                                 CompactFireSuppressantItemId);
                }
            }

            MissionOperationResult b194Transition = MissionRuntime.Service.CompleteAndActivateNextMission(
                characterId,
                MissionId,
                MissionRuntime.RexB194QuestId);
            if (IsPersistenceFailure(b194Transition))
            {
                ForceCompleteB18FIfNeeded(characterId);
                MissionRuntime.Service.OfferMission(characterId, MissionRuntime.RexB194QuestId);
                MissionRuntime.Service.AcceptMission(characterId, MissionRuntime.RexB194QuestId);
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ARETE_MARCUS_B18F_COMPLETION B194 handoff status="
                    + (b194Transition == null ? "null" : b194Transition.Status.ToString())
                    + " message=\""
                    + (b194Transition == null ? "" : b194Transition.Message)
                    + "\" — forced offer/accept + client projection");
            }

            // Capture 20260719-Rex-Markus-stone: Action59 + Delete + next QFU on mission swaps.
            RexQuestPreviewEmissionResult handoff = SafeQuestFullUpdateSender.TrySendB18FToB194Handoff(source);
            bool projected = handoff != null && handoff.Emitted;
            if (!projected)
            {
                SafeQuestFullUpdateSender.TrySendB18FQuestDelete(source);
                projected = SafeQuestFullUpdateSender.TrySendB194Preview(source).Emitted;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ARETE_MARCUS_B18F_COMPLETION transition applied character="
                + source.Identity.ToString(true)
                + " node=" + previousNodeId
                + " answer=" + answerIndex
                + " itemGrant=" + directGrant.Message
                + " itemOk=" + itemOk
                + " hadItem=" + hasSuppressant
                + " b18fReady=" + b18fReady
                + " handoff=" + (handoff == null ? "null" : handoff.Message)
                + " projected=" + projected);

            if (!projected)
            {
                return MarcusB18FCompletionResult.Failed(
                    "Marcus B18F→B194 client projection failed. itemOk=" + itemOk);
            }

            if (!itemOk)
            {
                return MarcusB18FCompletionResult.Failed(
                    "Marcus B194 projected but Compact Fire Suppressant 296780 missing. direct=\""
                    + directGrant.Message
                    + "\"");
            }

            return MarcusB18FCompletionResult.Succeeded(
                "Marcus B18F completion applied item296780=true b194QuestFullUpdateProjected=true");
        }

        private static void ForceCompleteB18FIfNeeded(int characterId)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, MissionId);
            if (mission == null || mission.State == MissionLifecycleState.Completed)
            {
                return;
            }

            if (mission.State == MissionLifecycleState.Active)
            {
                MissionRuntime.Service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = characterId,
                        QuestId = MissionId,
                        ObjectiveId = ObjectiveId,
                        ObservationKey = "force-complete-before-b194",
                        Amount = 1,
                        EventType = "NpcDialogueAnswer",
                        SourceIdentity = string.Empty,
                        TargetIdentity = string.Empty
                    });
                MissionRuntime.Service.CompleteMission(characterId, MissionId);
            }
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
                   && string.Equals(npc.Name, "Marcus Stone", StringComparison.OrdinalIgnoreCase);
        }

        private static bool EnsureB18FActive(int characterId)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, MissionId);
            if (mission != null
                && (mission.State == MissionLifecycleState.Active
                    || mission.State == MissionLifecycleState.Completed))
            {
                return true;
            }

            MissionOperationResult offer = MissionRuntime.Service.OfferMission(characterId, MissionId);
            if (IsPersistenceFailure(offer) && offer.Status != MissionOperationStatus.AlreadyApplied)
            {
                return false;
            }

            MissionOperationResult accept = MissionRuntime.Service.AcceptMission(characterId, MissionId);
            return !IsPersistenceFailure(accept) || accept.Status == MissionOperationStatus.AlreadyApplied;
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

            // Quest handout must always be deliverable. Template Unique blocked retries when a
            // prior copy sat in bank/overflow or a stuck persistence row left no carried item.
            EnsureSuppressantTemplateAllowsGrant();

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                source,
                CompactFireSuppressantItemId))
            {
                return MarcusItemHandoutResult.Succeeded(
                    "item296780AlreadyPresent=true carried=true noClientNotify=true");
            }

            Item item;
            try
            {
                item = new Item(
                    CompactFireSuppressantQuality,
                    CompactFireSuppressantItemId,
                    CompactFireSuppressantItemId);
                if (item.MultipleCount < 1)
                {
                    item.MultipleCount = 1;
                }
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
                if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    CompactFireSuppressantItemId))
                {
                    return MarcusItemHandoutResult.Succeeded(
                        "item296780AlreadyPresent=true carried=true afterAddFail=true");
                }

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

        private static void EnsureSuppressantTemplateAllowsGrant()
        {
            ItemTemplate template;
            if (!ItemLoader.ItemList.TryGetValue(CompactFireSuppressantItemId, out template)
                || template == null
                || template.Stats == null
                || !template.Stats.ContainsKey(0))
            {
                return;
            }

            int flags = template.Stats[0];
            if ((flags & (int)ItemFlags.Unique) == 0)
            {
                return;
            }

            template.Stats[0] = flags & ~(int)ItemFlags.Unique;
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ARETE_MARCUS_B18F cleared Unique flag on template 296780 for quest handout");
        }

        private sealed class AlreadyPresentFireSuppressantRewardEffect : IMissionRewardEffect
        {
            public MissionRewardEffectResult Apply(MissionRewardExecutionContext context)
            {
                return MissionRewardEffectResult.AlreadyApplied(
                    "inventory-item:296780:character:"
                    + context.CharacterId.ToString(CultureInfo.InvariantCulture));
            }
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
