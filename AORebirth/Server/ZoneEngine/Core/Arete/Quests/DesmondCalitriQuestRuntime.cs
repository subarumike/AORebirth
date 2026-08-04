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

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Capture 20260801-Desmond Calitri:
    /// burger delivery → two Protesters → return to Desmond → Cedric Harding.
    /// </summary>
    public static class DesmondCalitriQuestRuntime
    {
        public const string DesmondRootNodeId = "desmond_root";
        public const string BurgerDeliverNodeId = "desmond_burger_deliver";
        public const string BurgerTradeHoldNodeId = "desmond_burger_trade_hold";
        public const string HelpNodeId = "desmond_help";
        public const string ProtesterReturnNodeId = "desmond_protester_return";
        public const string WetworkGoodbyeNodeId = "desmond_wetwork_goodbye";
        public const string DoneNodeId = "desmond_done";
        public const string BarryRootNodeId = "barry_root";
        public const string BarryRootQuestNodeId = "barry_root_quest";

        public const string BurgerQuestId = "Mission:5576B750";
        public const string RallyQuestId = "Mission:5576B755";
        public const string ReturnQuestId = "Mission:5576B758";
        public const string WetworkQuestId = "Mission:5576B75C";

        public const int RequiredProtesterKills = 2;

        private const int AreteLandingPlayfieldId = 6553;
        private const int DesmondInstance = unchecked((int)0x78E0FC77);
        private const int BarryInstance = unchecked((int)0x78E0FC7D);
        // itemnames: 130623 = Bronto Burger; 130621 = A Beer Jug (capture shop slot 0).
        private const int BrontoBurgerItemId = 130623;
        private const int RewardXp = 2581;
        private const int RewardCredits = 1160;
        private const int CapturedTemplateActionUnknown1 = 1;
        private const int CapturedTemplateActionUnknown2 = 87;
        // Capture 20260801-Desmond Calitri: OverflowWindow TargetPlacement=111 (0x6F).
        private const int CapturedOverflowNextFreeSlot = 0x6F;
        private const int CapturedTradeSlotCount = 1;

        private const string TradePrompt =
            "Drag and drop the item(s) you want to give to Desmond Calitri into one of the slots available and press \"accept\"";

        private const string KillProgressFeedback = "~&!!!\":$nZiAi!!!!\"s\nProtester";
        private const string RewardFeedback = "~&!!!\":$'O\"ui!!!?@i!!!.X~";
        private const string RewardsGrantedFlag = "desmond-calitri-rewards-granted";
        private const string RewardItemsGrantedFlag = "desmond-calitri-reward-items-granted";

        private static readonly int[] RewardItemIds = { 295698, 295699, 295703 };
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<int, QuestPhase> LocalPhaseByCharacter =
            new Dictionary<int, QuestPhase>();
        private static readonly Dictionary<int, int> ProtesterKillsByCharacter =
            new Dictionary<int, int>();
        private static readonly Dictionary<int, HashSet<string>> ObservedDeathsByCharacter =
            new Dictionary<int, HashSet<string>>();
        private static readonly Dictionary<int, TradeSession> TradeSessionsByCharacter =
            new Dictionary<int, TradeSession>();

        private enum QuestPhase
        {
            None,
            Burger,
            Rally,
            Return,
            Wetwork,
            Done
        }

        private sealed class TradeSession
        {
            public Identity NpcIdentity;
            public Identity StagedContainer;
        }

        public static string ResolveDesmondStartNodeId(ICharacter source)
        {
            QuestPhase phase = ResolvePhase(source);

            // Sarah-style: burger in bags unlocks deliver even if tip state was lost on restart.
            if (phase == QuestPhase.Burger || HasBrontoBurger(source))
            {
                return BurgerDeliverNodeId;
            }

            switch (phase)
            {
                case QuestPhase.Rally:
                    return HelpNodeId;
                case QuestPhase.Return:
                    return ProtesterReturnNodeId;
                case QuestPhase.Wetwork:
                    return WetworkGoodbyeNodeId;
                case QuestPhase.Done:
                    // Recover tip delete / backpack overflow if Cedric kill only applied XP/credits.
                    EnsureWetworkRewardsSettled(source);
                    return DoneNodeId;
                default:
                    return DesmondRootNodeId;
            }
        }

        public static string ResolveBarryStartNodeId(ICharacter source)
        {
            // Capture 20260801-Desmond Calitri: burger tip present → Desmond option + sell + goodbye.
            // Capture 20260801-burger-vendor (no tip): sell + goodbye only.
            if (ResolvePhase(source) == QuestPhase.Burger)
            {
                return BarryRootQuestNodeId;
            }

            return BarryRootNodeId;
        }

        public static bool TryHandleDesmondDialogueAnswer(
            ICharacter source,
            string previousNodeId,
            int answerIndex)
        {
            if (source == null || answerIndex != 0 || string.IsNullOrEmpty(previousNodeId))
            {
                return false;
            }

            if (string.Equals(previousNodeId, DesmondRootNodeId, StringComparison.OrdinalIgnoreCase))
            {
                StartBurgerQuest(source);
                return true;
            }

            if (string.Equals(previousNodeId, "desmond_decision", StringComparison.OrdinalIgnoreCase))
            {
                StartWetworkQuest(source);
                return true;
            }

            return false;
        }

        public static bool TryHandleBarryDialogueAnswer(
            ICharacter source,
            string previousNodeId,
            int answerIndex)
        {
            // Capture 20260801-burger-vendor: Bronto Burger is bought from Barry's shop
            // (GenericCmd Use / shopping cart → ShopUpdate), not granted by dialogue.
            return false;
        }

        public static bool TryBeginDesmondTrade(ICharacter source, Identity npcIdentity)
        {
            if (source == null
                || (ResolvePhase(source) != QuestPhase.Burger && !HasBrontoBurger(source)))
            {
                return false;
            }

            if (npcIdentity.Type != IdentityType.CanbeAffected || npcIdentity.Instance == 0)
            {
                npcIdentity = new Identity
                              {
                                  Type = IdentityType.CanbeAffected,
                                  Instance = DesmondInstance
                              };
            }

            BeginTrade(source, npcIdentity);
            KnuBotStartTradeMessageHandler.Default.Send(
                source,
                npcIdentity,
                TradePrompt,
                CapturedTradeSlotCount);
            return true;
        }

        public static bool TryStageDesmondTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (source == null || message == null || !IsDesmondNpc(source, message.Target))
            {
                return false;
            }

            if (ResolvePhase(source) != QuestPhase.Burger
                && !HasBrontoBurger(source)
                && GetTradeSession(source) == null)
            {
                return false;
            }

            BeginTradeIfMissing(source, message.Target);
            TradeSession session = GetTradeSession(source);
            if (session != null)
            {
                session.NpcIdentity = message.Target;
                if (message.Container.Type != IdentityType.None && message.Container.Instance >= 0)
                {
                    session.StagedContainer = message.Container;
                }
            }

            return true;
        }

        public static bool ShouldSuppressGenericDesmondTradeRemove(
            ICharacter source,
            KnuBotTradeMessage message)
        {
            return source != null
                   && message != null
                   && IsDesmondNpc(source, message.Target)
                   && (ResolvePhase(source) == QuestPhase.Burger
                       || HasBrontoBurger(source)
                       || GetTradeSession(source) != null);
        }

        public static bool TryFinishDesmondTrade(
            ICharacter source,
            KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null || !IsDesmondNpc(source, message.Target))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            TradeSession session = GetTradeSession(source);
            if (session == null)
            {
                BeginTrade(source, message.Target);
                session = GetTradeSession(source);
            }

            Identity staged = session != null ? session.StagedContainer : Identity.None;
            if (!TryConsumeBurger(source, staged))
            {
                int stagedLow = 0;
                int stagedHigh = 0;
                TryReadStagedItemIds(source, staged, out stagedLow, out stagedHigh);
                Log(
                    "burger-turnin rejected character="
                    + source.Identity.ToString(true)
                    + " staged="
                    + staged.ToString(true)
                    + " stagedLow="
                    + stagedLow
                    + " stagedHigh="
                    + stagedHigh
                    + " hasBurger="
                    + HasBrontoBurger(source)
                    + " phase="
                    + ResolvePhase(source)
                    + " — need item "
                    + BrontoBurgerItemId);
                if (stagedLow != 0 || stagedHigh != 0)
                {
                    ChatTextMessageHandler.Default.Send(
                        source,
                        "Desmond wants Bronto Burger (item "
                        + BrontoBurgerItemId
                        + "). You offered item "
                        + stagedLow
                        + ".");
                }

                BeginTrade(source, message.Target);
                KnuBotStartTradeMessageHandler.Default.Send(
                    source,
                    message.Target,
                    TradePrompt,
                    CapturedTradeSlotCount);
                return true;
            }

            Log("burger-turnin accepted character=" + source.Identity.ToString(true));

            try
            {
                KnuBotRejectedItemsMessageHandler.Default.Send(source, message.Target, new Item[0], 0);
            }
            catch (Exception ex)
            {
                Log("RejectedItems failed: " + ex.Message);
            }

            CompleteAndActivate(source, BurgerQuestId, RallyQuestId, "desmond-burger-turnin");
            SetLocalPhase(source, QuestPhase.Rally);
            DesmondCalitriTipSender.TrySendBurgerToRallyHandoff(source);
            ForgetTradeSession(source);
            ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, message.Target);
            return true;
        }

        public static bool TryObserveNpcDeath(ICharacter attacker, ICharacter target)
        {
            if (attacker == null
                || target == null
                || !IsInAreteLanding(attacker)
                || string.IsNullOrEmpty(target.Name))
            {
                return false;
            }

            if (string.Equals(target.Name, "Protester", StringComparison.Ordinal)
                && ResolvePhase(attacker) == QuestPhase.Rally)
            {
                return ObserveProtesterDeath(attacker, target);
            }

            if (string.Equals(target.Name, "Cedric Harding", StringComparison.Ordinal)
                && ShouldCompleteWetworkOnCedricDeath(attacker))
            {
                CompleteWetwork(attacker);
                return true;
            }

            return false;
        }

        private static bool ShouldCompleteWetworkOnCedricDeath(ICharacter attacker)
        {
            QuestPhase phase = ResolvePhase(attacker);
            if (phase == QuestPhase.Wetwork)
            {
                return true;
            }

            // First kill may have granted XP/credits and marked Done while tip/items failed.
            return phase == QuestPhase.Done && !HasRewardItemsGranted(attacker);
        }

        public static bool IsDesmondNpc(ICharacter source, Identity target)
        {
            return IsNpc(source, target, DesmondInstance, "Desmond Calitri");
        }

        public static bool IsBarryNpc(ICharacter source, Identity target)
        {
            return IsNpc(source, target, BarryInstance, "Barry the Food Vendor");
        }

        private static bool ObserveProtesterDeath(ICharacter source, ICharacter target)
        {
            int characterId = source.Identity.Instance;
            int kills;
            lock (SyncRoot)
            {
                HashSet<string> seen;
                if (!ObservedDeathsByCharacter.TryGetValue(characterId, out seen))
                {
                    seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    ObservedDeathsByCharacter[characterId] = seen;
                }

                if (!seen.Add(target.Identity.ToString(true)))
                {
                    return false;
                }

                ProtesterKillsByCharacter.TryGetValue(characterId, out kills);
                kills = Math.Min(RequiredProtesterKills, kills + 1);
                ProtesterKillsByCharacter[characterId] = kills;
            }

            if (kills < RequiredProtesterKills)
            {
                SendFormatFeedback(source, KillProgressFeedback);
                return true;
            }

            CompleteAndActivate(source, RallyQuestId, ReturnQuestId, "desmond-protester-kills");
            SetLocalPhase(source, QuestPhase.Return);
            DesmondCalitriTipSender.TrySendRallyToReturnHandoff(source);
            lock (SyncRoot)
            {
                ProtesterKillsByCharacter.Remove(characterId);
                ObservedDeathsByCharacter.Remove(characterId);
            }

            return true;
        }

        private static void StartBurgerQuest(ICharacter source)
        {
            if (ResolvePhase(source) != QuestPhase.None)
            {
                return;
            }

            EnsureQuestActive(source, BurgerQuestId);
            SetLocalPhase(source, QuestPhase.Burger);
            DesmondCalitriTipSender.TrySendBurgerTipOnly(source);
        }

        private static void StartWetworkQuest(ICharacter source)
        {
            if (ResolvePhase(source) != QuestPhase.Return)
            {
                return;
            }

            CompleteAndActivate(source, ReturnQuestId, WetworkQuestId, "desmond-return-dialogue");
            SetLocalPhase(source, QuestPhase.Wetwork);
            DesmondCalitriTipSender.TrySendReturnToWetworkHandoff(source);
        }

        private static void CompleteWetwork(ICharacter source)
        {
            EnsureWetworkRewardsSettled(source);
        }

        /// <summary>
        /// Capture 20260801-Desmond Calitri on Cedric death:
        /// XP/credits → FormatFeedback → TemplateAction+ContainerAdd ×3 → tip Action59+Delete.
        /// Idempotent so a partial first kill can still clear the tip and grant backpacks.
        /// </summary>
        private static void EnsureWetworkRewardsSettled(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            try
            {
                if (!HasRewardsGranted(source))
                {
                    AreteQuestRewardGrants.GrantCreditsAndXpOnce(
                        source,
                        WetworkQuestId,
                        "arete-credits-awarded-desmond-wetwork",
                        RewardCredits,
                        "arete-xp-awarded-desmond-wetwork",
                        RewardXp,
                        "desmond-wetwork-2581xp");
                    SendFormatFeedback(source, RewardFeedback);
                }

                TryGrantRewardItems(source);
                CompleteMission(source, WetworkQuestId);
                if (MissionRuntime.IsInitialized)
                {
                    MissionRuntime.Service.SetFlag(
                        source.Identity.Instance,
                        WetworkQuestId,
                        RewardsGrantedFlag,
                        "1");
                }
            }
            catch (Exception ex)
            {
                Log("wetwork settle failed: " + ex.Message);
            }
            finally
            {
                // Always clear Shuttleport Wetwork tip even when item grant fails.
                DesmondCalitriTipSender.DeleteWetworkTip(source);
                SetLocalPhase(source, QuestPhase.Done);
            }
        }

        private static void TryGrantRewardItems(ICharacter source)
        {
            if (source == null || HasRewardItemsGranted(source))
            {
                return;
            }

            int granted = 0;
            for (int i = 0; i < RewardItemIds.Length; i++)
            {
                if (TryGrantQuestRewardWithOverflowPackets(source, RewardItemIds[i]))
                {
                    granted++;
                }
            }

            Log(
                "wetwork reward items granted="
                + granted
                + "/"
                + RewardItemIds.Length
                + " character="
                + source.Identity.ToString(true));

            if (granted == RewardItemIds.Length && MissionRuntime.IsInitialized)
            {
                MissionRuntime.Service.SetFlag(
                    source.Identity.Instance,
                    WetworkQuestId,
                    RewardItemsGrantedFlag,
                    "1");
            }
        }

        private static bool TryGrantQuestRewardWithOverflowPackets(ICharacter source, int itemId)
        {
            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller?.Client == null)
            {
                Log("grant skipped item=" + itemId + " reason=no-inventory-or-client");
                return false;
            }

            if (!ItemLoader.ItemList.ContainsKey(itemId))
            {
                Log("grant skipped item=" + itemId + " reason=missing-ItemLoader-template");
                return false;
            }

            Item item;
            try
            {
                item = new Item(1, itemId, itemId);
            }
            catch (Exception ex)
            {
                Log("grant create failed item=" + itemId + " err=" + ex.Message);
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant == null || grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log(
                    "grant failed item="
                    + itemId
                    + " status="
                    + (grant == null ? "null" : grant.Status.ToString()));
                return false;
            }

            // Capture: TemplateAction Overflow → ContainerAdd Overflow slot 111.
            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = itemId,
                    ItemHighId = itemId,
                    Quality = 1,
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
            return true;
        }

        private static void SendFormatFeedback(ICharacter source, string formattedMessage)
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
                    FormattedMessage = formattedMessage,
                    Unknown2 = 0
                });
        }

        private static bool TryConsumeBurger(ICharacter source, Identity staged)
        {
            Identity container = staged;
            if (!IsBurgerAt(source, container) && !TryFindBurger(source, out container))
            {
                return false;
            }

            source.BaseInventory.RemoveItem((int)container.Type, container.Instance);
            CharacterActionMessageHandler.Default.SendDeleteItem(
                source,
                (int)container.Type,
                container.Instance);
            return true;
        }

        private static bool IsBurgerAt(ICharacter source, Identity container)
        {
            int low;
            int high;
            return TryReadStagedItemIds(source, container, out low, out high)
                   && (low == BrontoBurgerItemId || high == BrontoBurgerItemId);
        }

        private static bool TryReadStagedItemIds(
            ICharacter source,
            Identity container,
            out int lowId,
            out int highId)
        {
            lowId = 0;
            highId = 0;
            if (source?.BaseInventory?.Pages == null
                || container.Type == IdentityType.None
                || container.Instance < 0)
            {
                return false;
            }

            IInventoryPage page;
            if (!source.BaseInventory.Pages.TryGetValue((int)container.Type, out page) || page == null)
            {
                return false;
            }

            IItem item = page[container.Instance];
            if (item == null)
            {
                return false;
            }

            lowId = item.LowID;
            highId = item.HighID;
            return true;
        }

        private static bool TryFindBurger(ICharacter source, out Identity container)
        {
            container = Identity.None;
            if (source?.BaseInventory?.Pages == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in source.BaseInventory.Pages)
            {
                if (pageEntry.Value == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> slot in pageEntry.Value.List())
                {
                    IItem item = slot.Value;
                    if (item != null
                        && (item.LowID == BrontoBurgerItemId || item.HighID == BrontoBurgerItemId))
                    {
                        container = new Identity
                                    {
                                        Type = (IdentityType)pageEntry.Key,
                                        Instance = slot.Key
                                    };
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasBrontoBurger(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       BrontoBurgerItemId);
        }

        private static void BeginTrade(ICharacter source, Identity npcIdentity)
        {
            lock (SyncRoot)
            {
                TradeSessionsByCharacter[source.Identity.Instance] =
                    new TradeSession
                    {
                        NpcIdentity = npcIdentity,
                        StagedContainer = Identity.None
                    };
            }
        }

        private static void BeginTradeIfMissing(ICharacter source, Identity npcIdentity)
        {
            if (GetTradeSession(source) == null)
            {
                BeginTrade(source, npcIdentity);
            }
        }

        private static TradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                TradeSession session;
                return TradeSessionsByCharacter.TryGetValue(source.Identity.Instance, out session)
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

            lock (SyncRoot)
            {
                TradeSessionsByCharacter.Remove(source.Identity.Instance);
            }
        }

        private static QuestPhase ResolvePhase(ICharacter source)
        {
            if (source == null)
            {
                return QuestPhase.None;
            }

            if (HasRewardsGranted(source) || IsMissionCompleted(source, WetworkQuestId))
            {
                return QuestPhase.Done;
            }

            if (IsMissionActive(source, WetworkQuestId))
            {
                return QuestPhase.Wetwork;
            }

            if (IsMissionActive(source, ReturnQuestId))
            {
                return QuestPhase.Return;
            }

            if (IsMissionActive(source, RallyQuestId))
            {
                return QuestPhase.Rally;
            }

            if (IsMissionActive(source, BurgerQuestId))
            {
                return QuestPhase.Burger;
            }

            lock (SyncRoot)
            {
                QuestPhase phase;
                return LocalPhaseByCharacter.TryGetValue(source.Identity.Instance, out phase)
                           ? phase
                           : QuestPhase.None;
            }
        }

        private static void SetLocalPhase(ICharacter source, QuestPhase phase)
        {
            lock (SyncRoot)
            {
                LocalPhaseByCharacter[source.Identity.Instance] = phase;
            }
        }

        private static bool HasRewardsGranted(ICharacter source)
        {
            return source != null
                   && MissionRuntime.IsInitialized
                   && MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       WetworkQuestId,
                       RewardsGrantedFlag) != null;
        }

        private static bool HasRewardItemsGranted(ICharacter source)
        {
            return source != null
                   && MissionRuntime.IsInitialized
                   && MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       WetworkQuestId,
                       RewardItemsGrantedFlag) != null;
        }

        private static bool IsMissionActive(ICharacter source, string questId)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission = GetMission(source, questId);
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static bool IsMissionCompleted(ICharacter source, string questId)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission = GetMission(source, questId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        private static ZoneEngine.Core.Missions.MissionStateRecord GetMission(
            ICharacter source,
            string questId)
        {
            return source == null || !MissionRuntime.IsInitialized
                       ? null
                       : MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
        }

        private static void EnsureQuestActive(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            if (mission == null)
            {
                ZoneEngine.Core.Missions.MissionOperationResult offer =
                    MissionRuntime.Service.OfferMission(source.Identity.Instance, questId);
                ZoneEngine.Core.Missions.MissionOperationResult accept =
                    MissionRuntime.Service.AcceptMission(source.Identity.Instance, questId);
                if ((offer != null && offer.Status != MissionOperationStatus.Applied
                                   && offer.Status != MissionOperationStatus.AlreadyApplied)
                    || (accept != null && accept.Status != MissionOperationStatus.Applied
                                      && accept.Status != MissionOperationStatus.AlreadyApplied))
                {
                    Log(
                        "EnsureQuestActive failed quest="
                        + questId
                        + " offer="
                        + (offer == null ? "<null>" : offer.Status + " " + offer.Message)
                        + " accept="
                        + (accept == null ? "<null>" : accept.Status + " " + accept.Message));
                }
            }
            else if (mission.State == MissionLifecycleState.Offered)
            {
                MissionRuntime.Service.AcceptMission(source.Identity.Instance, questId);
            }
        }

        private static void CompleteAndActivate(
            ICharacter source,
            string currentQuestId,
            string nextQuestId,
            string observationKey)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            EnsureQuestActive(source, currentQuestId);
            MissionRuntime.Service.CompleteMission(characterId, currentQuestId);
            EnsureQuestActive(source, nextQuestId);
            Log(
                "handoff "
                + currentQuestId
                + "→"
                + nextQuestId
                + " character="
                + characterId
                + " key="
                + observationKey);
        }

        private static void CompleteMission(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            EnsureQuestActive(source, questId);
            MissionRuntime.Service.CompleteMission(source.Identity.Instance, questId);
        }

        private static bool IsNpc(
            ICharacter source,
            Identity target,
            int capturedInstance,
            string expectedName)
        {
            if (target.Type == IdentityType.CanbeAffected && target.Instance == capturedInstance)
            {
                return true;
            }

            if (source?.Playfield == null || target.Type != IdentityType.CanbeAffected)
            {
                return false;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, target);
            return npc != null && string.Equals(npc.Name, expectedName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInAreteLanding(ICharacter source)
        {
            return source?.Playfield != null
                   && source.Playfield.Identity.Instance == AreteLandingPlayfieldId;
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "DesmondCalitriQuestRuntime " + message);
        }
    }
}
