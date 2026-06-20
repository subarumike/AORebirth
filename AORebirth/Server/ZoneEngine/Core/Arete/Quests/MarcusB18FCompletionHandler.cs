namespace ZoneEngine.Core.Arete.Quests
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

    using ZoneEngine.Core.Controllers;

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

        private static readonly Dictionary<string, MarcusB18FCompletionState> StateByCharacter =
            new Dictionary<string, MarcusB18FCompletionState>(StringComparer.OrdinalIgnoreCase);

        private static readonly object SyncRoot = new object();

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

            RexMissionChainState chainState = RexMissionChainStateStore.GetState(source);
            if (chainState < RexMissionChainState.B18FPreviewed)
            {
                return MarcusB18FCompletionResult.Skipped(
                    "Marcus B18F completion skipped because Rex chain state is "
                    + chainState
                    + ". requiredState=B18FPreviewed noQuestDelete=true noB194=true");
            }

            MarcusB18FCompletionState completionState;
            lock (SyncRoot)
            {
                completionState = GetOrCreateState(source.Identity);
                if (completionState.TransitionCompleted)
                {
                    return MarcusB18FCompletionResult.Skipped(
                        "Marcus B18F completion skipped because B194 was already previewed. "
                        + "duplicateBlocked=true noDuplicateQuestDelete=true noDuplicateB194=true");
                }

                if (completionState.TransitionInProgress)
                {
                    return MarcusB18FCompletionResult.Skipped(
                        "Marcus B18F completion skipped because the transition is already in progress. "
                        + "duplicateBlocked=true noDuplicateQuestDelete=true noDuplicateB194=true");
                }

                completionState.TransitionInProgress = true;
            }

            RexQuestPreviewEmissionResult deleteResult = null;
            RexQuestPreviewEmissionResult b194Result = null;
            MarcusItemHandoutResult itemHandoutResult = null;
            try
            {
                if (!completionState.Item296780HandoutSatisfied)
                {
                    itemHandoutResult = TryGrantCompactFireSuppressant(source);
                    if (!itemHandoutResult.Completed)
                    {
                        return MarcusB18FCompletionResult.Failed(
                            "Marcus B18F completion failed before B18F Quest Delete because item 296780 handout failed. "
                            + "message=\""
                            + itemHandoutResult.Message
                            + "\" noQuestDelete=true noB194=true noRewards=true noTrade=true noDbMissionPersistence=true");
                    }

                    lock (SyncRoot)
                    {
                        completionState.Item296780HandoutSatisfied = true;
                    }
                }
                else
                {
                    itemHandoutResult = MarcusItemHandoutResult.Succeeded(
                        "item296780GrantSkipped=true reason=process-local-state");
                }

                if (!completionState.B18FDeleteSent)
                {
                    deleteResult = SafeQuestFullUpdateSender.TrySendB18FQuestDelete(source);
                    if (!deleteResult.Emitted)
                    {
                        return MarcusB18FCompletionResult.Failed(
                            "Marcus B18F completion failed before B194 because B18F Quest Delete was not emitted. "
                            + "message=\"" + deleteResult.Message + "\" noB194=true item296780Handout=\""
                            + itemHandoutResult.Message
                            + "\" noRewards=true noTrade=true noDbMissionPersistence=true");
                    }

                    lock (SyncRoot)
                    {
                        completionState.B18FDeleteSent = true;
                    }
                }

                if (!completionState.B194QuestFullUpdateSent)
                {
                    b194Result = SafeQuestFullUpdateSender.TrySendB194Preview(source);
                    if (!b194Result.Emitted)
                    {
                        return MarcusB18FCompletionResult.Failed(
                            "Marcus B18F completion partially applied but B194 QuestFullUpdate was not emitted. "
                            + "b18fDeleteSent=true message=\"" + b194Result.Message + "\" "
                            + "item296780Handout=\"" + itemHandoutResult.Message + "\" "
                            + "noRewards=true noTrade=true noDbMissionPersistence=true");
                    }

                    lock (SyncRoot)
                    {
                        completionState.B194QuestFullUpdateSent = true;
                        completionState.TransitionCompleted = true;
                    }
                }

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "ARETE_MARCUS_B18F_COMPLETION transition applied character="
                    + source.Identity.ToString(true)
                    + " node=" + previousNodeId
                    + " answer=" + answerIndex
                    + " missionDelete=Mission:5514B18F nextQuestFullUpdate=Mission:5514B194 "
                    + "source=20260614-195107/events.log:1645-1646,packets.hex.log:1407 "
                    + "rawReplay=false duplicateGuard=processLocal item296780Handout=\""
                    + itemHandoutResult.Message
                    + "\" "
                    + "noRewards=true noTrade=true noDbMissionPersistence=true");

                return MarcusB18FCompletionResult.Succeeded(
                    "Marcus B18F completion applied b18fDeleteSent="
                    + completionState.B18FDeleteSent
                    + " b194QuestFullUpdateSent="
                    + completionState.B194QuestFullUpdateSent
                    + " deleteMessage=\""
                    + (deleteResult == null ? "already-sent" : deleteResult.Message)
                    + "\" b194Message=\""
                    + (b194Result == null ? "already-sent" : b194Result.Message)
                    + "\" item296780Handout=\""
                    + itemHandoutResult.Message
                    + "\" noRewards=true noTrade=true noDbMissionPersistence=true");
            }
            finally
            {
                lock (SyncRoot)
                {
                    completionState.TransitionInProgress = false;
                }
            }
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
            if (source == null || source.BaseInventory == null)
            {
                return MarcusItemHandoutResult.Failed("sourceInventoryAvailable=false");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return MarcusItemHandoutResult.Failed("sourceClientAvailable=false");
            }

            if (CharacterHasItemInCarriedInventory(source, CompactFireSuppressantItemId))
            {
                return MarcusItemHandoutResult.Succeeded(
                    "item296780GrantSkipped=true reason=already-in-inventory");
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

            InventoryError inventoryError = source.BaseInventory.TryAdd(item);
            if (inventoryError != InventoryError.OK)
            {
                return MarcusItemHandoutResult.Failed(
                    "item296780InventoryAddFailed=true error=" + inventoryError);
            }

            bool persisted;
            try
            {
                persisted = source.BaseInventory.Write();
            }
            catch (Exception e)
            {
                return MarcusItemHandoutResult.Failed(
                    "item296780InventoryPersistFailed=true error=\"" + e.Message + "\"");
            }

            if (!persisted)
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

        private static bool CharacterHasItemInCarriedInventory(ICharacter source, int itemId)
        {
            IInventoryPage page;
            if (source.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out page)
                && InventoryPageHasItem(page, itemId))
            {
                return true;
            }

            return source.BaseInventory.Pages.TryGetValue((int)IdentityType.OverflowWindow, out page)
                   && InventoryPageHasItem(page, itemId);
        }

        private static bool InventoryPageHasItem(IInventoryPage page, int itemId)
        {
            foreach (KeyValuePair<int, IItem> itemEntry in page.List())
            {
                IItem item = itemEntry.Value;
                if (item != null && (item.LowID == itemId || item.HighID == itemId))
                {
                    return true;
                }
            }

            return false;
        }

        private static MarcusB18FCompletionState GetOrCreateState(Identity identity)
        {
            string key = MakeCharacterKey(identity);
            MarcusB18FCompletionState state;
            if (!StateByCharacter.TryGetValue(key, out state))
            {
                state = new MarcusB18FCompletionState();
                StateByCharacter[key] = state;
            }

            return state;
        }

        private static string MakeCharacterKey(Identity identity)
        {
            return ((int)identity.Type).ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + identity.Instance.ToString("X8", CultureInfo.InvariantCulture);
        }

        private sealed class MarcusB18FCompletionState
        {
            public bool TransitionInProgress { get; set; }

            public bool B18FDeleteSent { get; set; }

            public bool B194QuestFullUpdateSent { get; set; }

            public bool Item296780HandoutSatisfied { get; set; }

            public bool TransitionCompleted { get; set; }
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
