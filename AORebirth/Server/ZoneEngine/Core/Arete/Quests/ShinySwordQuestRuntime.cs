namespace ZoneEngine.Core.Arete.Quests
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Capture 20260725-shiny-sword-nano:
    /// Use Shiny Sword (297289) → tip Mission:5565CD87 → trade to Greedy Desert Reet →
    /// 1280 credits + 2507 XP + nano 223381 ql25.
    /// </summary>
    public static class ShinySwordQuestRuntime
    {
        public const string DemandNodeId = "greedy_demand";

        public const string TradeNodeId = "greedy_trade";

        public const int ShinySwordItemId = 297289;

        public const int NanoRewardItemId = 223381;

        public const int NanoRewardQuality = 25;

        public const string QuestId = "Mission:5565CD87";

        private const int FinishXpReward = 2507;

        private const int FinishCreditReward = 1280;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedUseTemplateActionUnknown2 = 3;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        // Capture FormatFeedback body (length prefix stripped).
        private const string FinishRewardFeedback = "~&!!!\":$'O\"ui!!!>Ki!!!0&~";

        private const string TradePrompt =
            "Drag and drop the item(s) you want to give to Greedy Desert Reet into one of the slots available and press \"accept\"";

        private const string RewardsGrantedFlag = "shiny-sword-rewards-granted";

        private static readonly object TradeSyncRoot = new object();

        private static readonly Dictionary<int, ShinySwordTradeSession> TradeByCharacter =
            new Dictionary<int, ShinySwordTradeSession>();

        private sealed class ShinySwordTradeSession
        {
            public Identity NpcIdentity;

            public Identity StagedContainer;
        }

        public static bool HasShinySword(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       ShinySwordItemId);
        }

        public static bool IsGreedyDesertReet(ICharacter source, Identity npcIdentity)
        {
            if (source?.Playfield == null
                || npcIdentity.Type != IdentityType.CanbeAffected
                || npcIdentity.Instance == 0)
            {
                return false;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, npcIdentity);
            // Capture 20260725-shiny-sword-nano: match identity, not oasis spawn registry.
            // StartTrade used dialogue registration (by name) while FinishTrade required
            // OasisReetInstances — trade opened then turn-in/nano grant never ran.
            return LoreleiOasisMobRuntime.MatchesGreedyDesertReetIdentity(npc);
        }

        /// <summary>
        /// Capture: Use Inventory sword → QuestFullUpdate tip + TemplateAction Unknown2=3 (sword kept).
        /// </summary>
        public static bool TryHandleShinySwordUse(
            ICharacter character,
            Identity itemPosition,
            Item item)
        {
            if (character == null
                || item == null
                || (item.LowID != ShinySwordItemId && item.HighID != ShinySwordItemId))
            {
                return false;
            }

            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(character)
                || character.Controller == null
                || character.Controller.Client == null)
            {
                Log("sword-use skipped: inventory/client missing");
                return false;
            }

            // Capture order: QuestFullUpdate tip, then TemplateAction Unknown2=3 (sword kept).
            if (!HasRewardsGranted(character))
            {
                EnsureQuestActive(character, QuestId);
                RexQuestPreviewEmissionResult tipResult = ShinySwordTipSender.TrySendTip(character);
                Log(
                    "shiny-sword use tip character="
                    + character.Identity.ToString(true)
                    + " slot="
                    + itemPosition
                    + " emitted="
                    + tipResult.Emitted
                    + " "
                    + tipResult.Message);
            }
            else
            {
                Log(
                    "shiny-sword use skipped tip (rewards already granted) character="
                    + character.Identity.ToString(true)
                    + " slot="
                    + itemPosition);
            }

            character.Send(
                new TemplateActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    ItemLowId = ShinySwordItemId,
                    ItemHighId = ShinySwordItemId,
                    Quality = item.Quality > 0 ? item.Quality : 1,
                    Unknown1 = CapturedTemplateActionUnknown1,
                    Unknown2 = CapturedUseTemplateActionUnknown2,
                    Placement = itemPosition,
                    Unknown3 = 0,
                    Unknown4 = 0
                });

            return true;
        }

        public static bool TryBeginSwordTrade(ICharacter source, Identity reetIdentity)
        {
            if (source == null)
            {
                return false;
            }

            if (reetIdentity.Type != IdentityType.CanbeAffected || reetIdentity.Instance == 0)
            {
                return false;
            }

            BeginTrade(source, reetIdentity);
            KnuBotStartTradeMessageHandler.Default.Send(source, reetIdentity, TradePrompt, 1);
            return true;
        }

        public static bool TryStageSwordTradeItem(ICharacter character, KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsGreedyDesertReet(character, message.Target))
            {
                return false;
            }

            if (!HasShinySword(character) && GetTradeSession(character) == null)
            {
                return false;
            }

            BeginTrade(character, message.Target);
            ShinySwordTradeSession session = GetTradeSession(character);
            if (session == null)
            {
                return true;
            }

            session.NpcIdentity = message.Target;
            if (message.Container.Type != IdentityType.None && message.Container.Instance >= 0)
            {
                session.StagedContainer = message.Container;
            }

            return true;
        }

        public static bool ShouldSuppressGenericSwordTradeRemove(
            ICharacter character,
            KnuBotTradeMessage message)
        {
            return character != null
                   && message != null
                   && IsGreedyDesertReet(character, message.Target)
                   && (HasShinySword(character) || GetTradeSession(character) != null);
        }

        public static bool TryFinishSwordTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null || !IsGreedyDesertReet(source, message.Target))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            ShinySwordTradeSession session = GetTradeSession(source);
            Identity staged = session != null ? session.StagedContainer : Identity.None;
            ApplySwordTurnIn(source, message.Target, staged);
            return true;
        }

        private static void ApplySwordTurnIn(ICharacter source, Identity reetTarget, Identity staged)
        {
            if (!TryConsumeInventoryItem(source, staged, ShinySwordItemId)
                && !TryConsumeInventoryItem(source, Identity.None, ShinySwordItemId))
            {
                KnuBotRejectedItemsMessageHandler.Default.Send(source, reetTarget, new Item[0], 0);
                ForgetTradeSession(source);
                return;
            }

            GrantTurnInRewards(source);
            ShinySwordTipSender.DeleteTip(source);
            CompleteQuest(source, QuestId);
            KnuBotRejectedItemsMessageHandler.Default.Send(source, reetTarget, new Item[0], 0);
            ForgetTradeSession(source);

            if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, reetTarget))
            {
                KnuBotCloseChatWindowMessageHandler.Default.Send(source, reetTarget);
            }
        }

        private static void GrantTurnInRewards(ICharacter source)
        {
            if (source == null || HasRewardsGranted(source))
            {
                return;
            }

            // Capture 20260725-shiny-sword-nano #26–#33: FormatFeedback → Cash/XP →
            // TemplateAction nano 223381 QL25 → tip Action59/Delete.
            SendFinishFeedback(source);
            ApplyCredits(source);
            CombatXpRuntimeService.AwardDirectXp(source, FinishXpReward, "shiny-sword-2507xp");
            TryGrantNanoReward(source);
            MarkRewardsGranted(source);
        }

        private static void TryGrantNanoReward(ICharacter source)
        {
            if (source == null
                || !InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller?.Client == null)
            {
                return;
            }

            if (!ItemLoader.ItemList.ContainsKey(NanoRewardItemId))
            {
                Log("nano grant skipped: ItemLoader missing id=" + NanoRewardItemId);
                return;
            }

            Item item;
            try
            {
                item = new Item(NanoRewardQuality, NanoRewardItemId, NanoRewardItemId);
            }
            catch (Exception ex)
            {
                Log("nano create failed: " + ex.Message);
                return;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log(
                    "nano grant failed status="
                    + grant.Status
                    + " invErr="
                    + grant.InventoryError);
                return;
            }

            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = NanoRewardItemId,
                    ItemHighId = NanoRewardItemId,
                    Quality = NanoRewardQuality,
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
                    Target = new Identity
                             {
                                 Type = IdentityType.OverflowWindow,
                                 Instance = source.Identity.Instance
                             },
                    TargetPlacement = CapturedOverflowNextFreeSlot
                });
        }

        private static void ApplyCredits(ICharacter source)
        {
            if (source?.Stats == null)
            {
                return;
            }

            AreteQuestRewardGrants.GrantCredits(source, FinishCreditReward);
        }

        private static void SendFinishFeedback(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return;
            }

            source.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = source.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = FinishRewardFeedback,
                    Unknown2 = 0
                });
        }

        private static bool HasRewardsGranted(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (!MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       QuestId,
                       RewardsGrantedFlag) != null;
        }

        private static void MarkRewardsGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            EnsureQuestActive(source, QuestId);
            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                QuestId,
                RewardsGrantedFlag,
                "1");
        }

        private static void BeginTrade(ICharacter source, Identity npcIdentity)
        {
            if (source == null)
            {
                return;
            }

            lock (TradeSyncRoot)
            {
                TradeByCharacter[source.Identity.Instance] =
                    new ShinySwordTradeSession
                    {
                        NpcIdentity = npcIdentity,
                        StagedContainer = Identity.None
                    };
            }
        }

        private static ShinySwordTradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (TradeSyncRoot)
            {
                ShinySwordTradeSession session;
                return TradeByCharacter.TryGetValue(source.Identity.Instance, out session)
                           ? session
                           : null;
            }
        }

        private static void ForgetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (TradeSyncRoot)
            {
                TradeByCharacter.Remove(source.Identity.Instance);
            }
        }

        private static bool TryConsumeInventoryItem(ICharacter source, Identity stagedContainer, int itemId)
        {
            if (source == null || source.BaseInventory == null || itemId <= 0)
            {
                return false;
            }

            if (stagedContainer.Type != IdentityType.None && stagedContainer.Instance >= 0)
            {
                IInventoryPage stagedPage;
                if (source.BaseInventory.Pages.TryGetValue((int)stagedContainer.Type, out stagedPage)
                    && stagedPage != null)
                {
                    IItem staged = stagedPage[stagedContainer.Instance];
                    if (staged != null && (staged.LowID == itemId || staged.HighID == itemId))
                    {
                        source.BaseInventory.RemoveItem((int)stagedContainer.Type, stagedContainer.Instance);
                        CharacterActionMessageHandler.Default.SendDeleteItem(
                            source,
                            (int)stagedContainer.Type,
                            stagedContainer.Instance);
                        return true;
                    }
                }
            }

            Identity found;
            if (!TryFindItemContainer(source, itemId, out found))
            {
                return false;
            }

            source.BaseInventory.RemoveItem((int)found.Type, found.Instance);
            CharacterActionMessageHandler.Default.SendDeleteItem(source, (int)found.Type, found.Instance);
            return true;
        }

        private static bool TryFindItemContainer(ICharacter source, int itemId, out Identity found)
        {
            found = Identity.None;
            if (source?.BaseInventory?.Pages == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in source.BaseInventory.Pages)
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> slot in page.List())
                {
                    IItem item = slot.Value;
                    if (item != null && (item.LowID == itemId || item.HighID == itemId))
                    {
                        found = new Identity { Type = (IdentityType)pageEntry.Key, Instance = slot.Key };
                        return true;
                    }
                }
            }

            return false;
        }

        private static void EnsureQuestActive(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))
            {
                return;
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, questId);
            if (mission != null && mission.State == MissionLifecycleState.Active)
            {
                return;
            }

            MissionRuntime.Service.OfferMission(characterId, questId);
            MissionRuntime.Service.AcceptMission(characterId, questId);
        }

        private static void CompleteQuest(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))
            {
                return;
            }

            MissionRuntime.Service.CompleteMission(source.Identity.Instance, questId);
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "ShinySwordQuestRuntime " + message);
        }
    }
}
