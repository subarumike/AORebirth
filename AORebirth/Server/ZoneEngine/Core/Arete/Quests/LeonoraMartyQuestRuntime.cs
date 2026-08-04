namespace ZoneEngine.Core.Arete.Quests
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    using Playfield = AORebirth.Core.Playfields.Playfield;

    /// <summary>
    /// Capture 20260725-credit-card / 20260730-214622: Bank of Rubi-Ka Credit Card floor pickup + Leonora turn-in.
    /// Reward: 2507 XP + Vacuum Packed Omni-Med Suit (297054). Steal path: 15000 credits.
    /// </summary>
    public static class LeonoraMartyQuestRuntime
    {
        public const string DefaultStartNodeId = "leonora_001";

        public const string WithCardStartNodeId = "leonora_001_with_card";

        public const string HandOverNodeId = "leonora_hand_over";

        public const int CreditCardItemId = 297302;

        public const int CreditCardWorldTemplateId = 297315;

        // Capture 20260730-214622 Terminal:57A9CCBE (live instance rotates; match also by template).
        public const int CreditCardWorldInstance = unchecked((int)0x57A9CCBE);

        public const int SuitRewardItemId = 297054;

        // Capture 20260726-finish leonora and open vacuumpack TemplateAction Overflow grants.
        private static readonly int[] VacuumPackContents =
            {
                27385,
                27385,
                27382,
                27381,
                27383,
                27386
            };

        private const int AreteLandingPlayfieldId = 6553;

        private const int FinishXpReward = 2507;

        // Capture 20260726-073341: Use inventory credit card → "Received reward: 0 XP, 15000 credits."
        private const int StealCreditReward = 15000;

        private const string PickupFeedback = "You pick up the credit card.";

        private const string FinishRewardFeedback = "Received reward: 2507 XP, 0 credits.";

        private const string StealRewardFeedback = "Received reward: 0 XP, 15000 credits.";

        // Capture 20260726-secon try CC: second Use after claim → FormatFeedback wire x2 + Temp1=2.
        // Same body as cargo-without-quest reject (do not invent English).
        private const string CreditCardAlreadyClaimedFeedback = "~&!!!\":!o[Im";

        private const string TradePrompt =
            "Drag and drop the item(s) you want to give to Leonora Marty into one of the slots available and press \"accept\"";

        private const string DeliverQuestId = "mission_5565CD8F";

        private const string StealQuestId = "mission_5565CD8E";

        private const string RewardsGrantedFlag = "leonora-credit-rewards-granted";

        private const string CreditCardClaimedFlag = "leonora-credit-card-claimed";

        private const string CreditCardStolenFlag = "leonora-credit-card-stolen";

        // Match SarahGreene overflow TemplateAction Unknown1/Unknown2 layout.
        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private static readonly object TradeSyncRoot = new object();

        private static readonly object ClaimSyncRoot = new object();

        // Tip quest ids are wire-only; MissionRuntime flags may not persist. HashSets are
        // authoritative (same pattern as Lorelei lolly pickup).
        // WorldClaimed: first successful ground pickup (blocks re-loot).
        // OutcomeResolved: steal credits OR Leonora turn-in finished (blocks re-loot + re-steal).
        private static readonly HashSet<int> CreditCardWorldClaimedByCharacter = new HashSet<int>();

        private static readonly HashSet<int> CreditCardOutcomeResolvedByCharacter = new HashSet<int>();

        private static readonly Dictionary<int, LeonoraTradeSession> TradeByCharacter =
            new Dictionary<int, LeonoraTradeSession>();

        private sealed class LeonoraTradeSession
        {
            public Identity NpcIdentity;

            public Identity StagedContainer;
        }

        public static string ResolveLeonoraStartNodeId(ICharacter source)
        {
            return HasCreditCard(source) ? WithCardStartNodeId : DefaultStartNodeId;
        }

        public static bool HasCreditCard(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       CreditCardItemId);
        }

        public static void PausePatrolForDialogue(ICharacter npc)
        {
            AORebirth.Core.Playfields.Playfield playfield =
                npc == null ? null : npc.Playfield as AORebirth.Core.Playfields.Playfield;
            if (playfield != null)
            {
                playfield.SuspendCapturedAretePatrol(npc);
            }
        }

        public static void ResumePatrolAfterDialogue(ICharacter npc)
        {
            AORebirth.Core.Playfields.Playfield playfield =
                npc == null ? null : npc.Playfield as AORebirth.Core.Playfields.Playfield;
            if (playfield != null)
            {
                playfield.ResumeCapturedAretePatrol(npc);
            }
        }

        public static bool TryHandleCreditCardPickup(
            IZoneClient client,
            GenericCmdMessage message,
            Identity target)
        {
            if (client == null || message == null || target.Type != IdentityType.Terminal)
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || character.Playfield.Identity.Instance != AreteLandingPlayfieldId
                || !(character.Controller is PlayerController))
            {
                return false;
            }

            if (!IsCreditCardWorldProp(character, target))
            {
                return false;
            }

            // Capture 20260726-secon try CC: already claimed → deny (no grant / tips / despawn).
            if (HasWorldClaimedCreditCard(character) || HasCreditCard(character))
            {
                RejectCreditCardPickup(character, message);
                Log(
                    "credit-card pickup denied (already claimed) character="
                    + character.Identity.ToString(true)
                    + " target="
                    + target.ToString(true));
                return true;
            }

            if (!TryGrantItem(character, CreditCardItemId))
            {
                GenericCmdMessageHandler.Default.Acknowledge(character, message);
                Log("pickup grant failed character=" + character.Identity.ToString(true));
                return true;
            }

            // Capture 20260730-214622 order: QFU tips → FormatFeedback → TemplateAction/CAI → GenericCmd ack → Despawn.
            // World claim only — player may still steal OR turn in to Leonora once.
            MarkCreditCardWorldClaimed(character);
            EnsureQuestActive(character, StealQuestId);
            EnsureQuestActive(character, DeliverQuestId);
            LeonoraMartyTipSender.TrySendBothTips(character);
            SendPickupFeedback(character);
            SendOverflowGrantPackets(character, CreditCardItemId);
            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            TryDespawnCreditCard(character, target);
            Log("credit-card pickup character=" + character.Identity.ToString(true));
            return true;
        }

        private static void RejectCreditCardPickup(ICharacter character, GenericCmdMessage message)
        {
            // Capture order: FormatFeedback x2, then GenericCmd Temp1=2.
            SendAlreadyClaimedFeedback(character);
            SendAlreadyClaimedFeedback(character);
            GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
        }

        private static void SendAlreadyClaimedFeedback(ICharacter character)
        {
            if (character?.Controller?.Client == null)
            {
                return;
            }

            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = CreditCardAlreadyClaimedFeedback,
                    Unknown2 = 0
                });
        }

        private static bool HasWorldClaimedCreditCard(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            lock (ClaimSyncRoot)
            {
                if (CreditCardWorldClaimedByCharacter.Contains(characterId)
                    || CreditCardOutcomeResolvedByCharacter.Contains(characterId))
                {
                    return true;
                }
            }

            return HasPersistedOutcome(source);
        }

        private static bool HasOutcomeResolved(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            lock (ClaimSyncRoot)
            {
                if (CreditCardOutcomeResolvedByCharacter.Contains(characterId))
                {
                    return true;
                }
            }

            return HasPersistedOutcome(source);
        }

        private static bool HasPersistedOutcome(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            if (MissionRuntime.Service.GetFlag(characterId, DeliverQuestId, CreditCardStolenFlag) != null
                || MissionRuntime.Service.GetFlag(characterId, DeliverQuestId, RewardsGrantedFlag) != null)
            {
                MarkCreditCardOutcomeResolved(source);
                return true;
            }

            ZoneEngine.Core.Missions.MissionStateRecord deliver =
                MissionRuntime.Service.GetMission(characterId, DeliverQuestId);
            if (deliver != null && deliver.State == MissionLifecycleState.Completed)
            {
                MarkCreditCardOutcomeResolved(source);
                return true;
            }

            ZoneEngine.Core.Missions.MissionStateRecord steal =
                MissionRuntime.Service.GetMission(characterId, StealQuestId);
            if (steal != null && steal.State == MissionLifecycleState.Completed)
            {
                MarkCreditCardOutcomeResolved(source);
                return true;
            }

            return false;
        }

        private static void MarkCreditCardWorldClaimed(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            lock (ClaimSyncRoot)
            {
                CreditCardWorldClaimedByCharacter.Add(characterId);
            }

            TryPersistClaimFlag(source, CreditCardClaimedFlag);
        }

        private static void MarkCreditCardOutcomeResolved(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            lock (ClaimSyncRoot)
            {
                CreditCardWorldClaimedByCharacter.Add(characterId);
                CreditCardOutcomeResolvedByCharacter.Add(characterId);
            }
        }

        private static void TryPersistClaimFlag(ICharacter source, string flagKey)
        {
            if (source == null || !MissionRuntime.IsInitialized || string.IsNullOrEmpty(flagKey))
            {
                return;
            }

            try
            {
                int characterId = source.Identity.Instance;
                MissionRuntime.Service.OfferMission(characterId, DeliverQuestId);
                MissionRuntime.Service.AcceptMission(characterId, DeliverQuestId);
                MissionRuntime.Service.SetFlag(characterId, DeliverQuestId, flagKey, "1");
            }
            catch (Exception ex)
            {
                Log("claim flag persist skipped: " + ex.Message);
            }
        }

        private static bool HasClaimedCreditCard(ICharacter source)
        {
            return HasWorldClaimedCreditCard(source);
        }

        public static bool TryBeginLeonoraTrade(ICharacter source, Identity leonoraIdentity)
        {
            if (source == null)
            {
                return false;
            }

            if (leonoraIdentity.Type != IdentityType.CanbeAffected || leonoraIdentity.Instance == 0)
            {
                leonoraIdentity = new Identity
                                 {
                                     Type = IdentityType.CanbeAffected,
                                     Instance = unchecked((int)0x78E0FC74)
                                 };
            }

            BeginTrade(source, leonoraIdentity);
            KnuBotStartTradeMessageHandler.Default.Send(source, leonoraIdentity, TradePrompt, 1);
            return true;
        }

        public static bool TryStageLeonoraTradeItem(ICharacter character, KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsLeonoraNpc(character, message.Target))
            {
                return false;
            }

            if (!HasCreditCard(character) && GetTradeSession(character) == null)
            {
                return false;
            }

            BeginTrade(character, message.Target);
            LeonoraTradeSession session = GetTradeSession(character);
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

        public static bool ShouldSuppressGenericLeonoraTradeRemove(
            ICharacter character,
            KnuBotTradeMessage message)
        {
            return character != null
                   && message != null
                   && IsLeonoraNpc(character, message.Target)
                   && (HasCreditCard(character) || GetTradeSession(character) != null);
        }

        public static bool TryFinishLeonoraTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null || !IsLeonoraNpc(source, message.Target))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            LeonoraTradeSession session = GetTradeSession(source);
            Identity staged = session != null ? session.StagedContainer : Identity.None;
            Identity npc = message.Target;
            ApplyCreditCardTurnIn(source, npc, staged);
            return true;
        }

        private static void ApplyCreditCardTurnIn(ICharacter source, Identity leonoraTarget, Identity staged)
        {
            if (!TryConsumeInventoryItem(source, staged, CreditCardItemId)
                && !TryConsumeInventoryItem(source, Identity.None, CreditCardItemId))
            {
                KnuBotRejectedItemsMessageHandler.Default.Send(source, leonoraTarget, new Item[0], 0);
                ForgetTradeSession(source);
                return;
            }

            GrantTurnInRewards(source);
            MarkCreditCardOutcomeResolved(source);
            TryPersistClaimFlag(source, RewardsGrantedFlag);
            LeonoraMartyTipSender.DeleteBothTips(source);
            CompleteQuest(source, DeliverQuestId);
            CompleteQuest(source, StealQuestId);
            KnuBotRejectedItemsMessageHandler.Default.Send(source, leonoraTarget, new Item[0], 0);
            ForgetTradeSession(source);

            if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, leonoraTarget))
            {
                KnuBotCloseChatWindowMessageHandler.Default.Send(source, leonoraTarget);
                // Trade closed without dialogue resume — still unfreeze patrol.
                ICharacter leonora = source.Playfield == null
                                         ? null
                                         : Pool.Instance.GetObject<ICharacter>(
                                             source.Playfield.Identity,
                                             leonoraTarget);
                ResumePatrolAfterDialogue(leonora);
            }
        }

        /// <summary>
        /// Capture 20260726-073341: Use Bank of Rubi-Ka Credit Card in inventory
        /// (client confirm "Are you sure you want to steal the money?") →
        /// delete Deliver tip → consume card → 15000 credits → finish Steal tip.
        /// Mutually exclusive with Leonora turn-in (card consumed either way).
        /// </summary>
        public static bool TryHandleCreditCardStealUse(
            ICharacter character,
            Identity itemPosition,
            Item item)
        {
            if (character == null
                || item == null
                || (item.LowID != CreditCardItemId && item.HighID != CreditCardItemId))
            {
                return false;
            }

            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(character)
                || character.Controller == null
                || character.Controller.Client == null)
            {
                Log("credit-steal skipped: inventory/client missing");
                return false;
            }

            // Always consume the card on Use. Credits only once (outcome resolved).
            bool alreadyResolved = HasOutcomeResolved(character);

            LeonoraMartyTipSender.DeleteDeliverTipOnly(character);
            CompleteQuest(character, DeliverQuestId);

            character.BaseInventory.RemoveItem((int)itemPosition.Type, itemPosition.Instance);
            CharacterActionMessageHandler.Default.SendDeleteItem(
                character,
                (int)itemPosition.Type,
                itemPosition.Instance);

            if (alreadyResolved)
            {
                MarkCreditCardOutcomeResolved(character);
                LeonoraMartyTipSender.DeleteStealTipOnly(character);
                CompleteQuest(character, StealQuestId);
                Log(
                    "credit-card steal ignored (already resolved) — consumed card character="
                    + character.Identity.ToString(true));
                return true;
            }

            GrantStealCredits(character);
            MarkCreditCardStolen(character);
            LeonoraMartyTipSender.DeleteStealTipOnly(character);
            CompleteQuest(character, StealQuestId);

            Log(
                "credit-card stolen character="
                + character.Identity.ToString(true)
                + " slot="
                + itemPosition
                + " credits="
                + StealCreditReward);
            return true;
        }

        private static void GrantStealCredits(ICharacter source)
        {
            if (source?.Stats == null)
            {
                return;
            }

            if (source.Controller?.Client != null)
            {
                source.Controller.Client.SendCompressed(
                    new FormatFeedbackMessage
                    {
                        Identity = source.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        FormattedMessage = StealRewardFeedback,
                        Unknown2 = 0
                    });
            }

            AreteQuestRewardGrants.GrantCreditsOnce(
                source,
                StealQuestId,
                "arete-credits-awarded-leonora-steal",
                StealCreditReward);
        }

        private static void MarkCreditCardStolen(ICharacter source)
        {
            MarkCreditCardOutcomeResolved(source);
            TryPersistClaimFlag(source, CreditCardStolenFlag);
        }

        /// <summary>
        /// Capture 20260726-finish leonora and open vacuumpack:
        /// Use Vacuum Packed Omni-Med Suit (297054) → Overflow armor pieces → delete pack.
        /// Contents (order): 27385, 27385, 27382, 27381, 27383, 27386.
        /// </summary>
        public static bool TryHandleVacuumPackedOmniMedSuitUse(
            ICharacter character,
            Identity itemPosition,
            Item item)
        {
            if (character == null
                || item == null
                || (item.LowID != SuitRewardItemId && item.HighID != SuitRewardItemId))
            {
                return false;
            }

            // Always claim Use for 297054 so generic consumable fallthrough cannot delete the pack
            // without granting armor (capture 20260726-finish leonora and open vacuumpack).
            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(character)
                || character.Controller == null
                || character.Controller.Client == null)
            {
                Log("vacuum-pack use claimed but skipped: inventory/client missing");
                return true;
            }

            for (int i = 0; i < VacuumPackContents.Length; i++)
            {
                if (!ItemLoader.ItemList.ContainsKey(VacuumPackContents[i]))
                {
                    Log("vacuum-pack use claimed but skipped: ItemLoader missing id=" + VacuumPackContents[i]);
                    return true;
                }
            }

            int granted = 0;
            bool unpackAborted = false;
            for (int i = 0; i < VacuumPackContents.Length; i++)
            {
                int contentId = VacuumPackContents[i];
                Item grantItem;
                try
                {
                    grantItem = new Item(1, contentId, contentId);
                }
                catch (Exception ex)
                {
                    Log("vacuum-pack content create failed id=" + contentId + " err=" + ex.Message);
                    unpackAborted = true;
                    break;
                }

                QuestRewardInventoryGrantResult grant =
                    InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, grantItem);
                if (grant.Status == QuestRewardInventoryGrantStatus.Success)
                {
                    // Capture: TemplateAction Overflow Unknown2=87 + ContainerAddItem slot 111.
                    SendOverflowGrantPackets(character, contentId);
                    granted++;
                    continue;
                }

                // Unique armor pieces already owned must not abort the open.
                if (grant.Status == QuestRewardInventoryGrantStatus.InventoryAddFailed
                    && grant.InventoryError == InventoryError.HaveUniqueAlready)
                {
                    continue;
                }

                // Inventory full / other add fail: still push capture Overflow packets so the client
                // receives the pieces (same pattern as buy-nano tip overflow-on-fail).
                SendOverflowGrantPackets(character, contentId);
                Log(
                    "vacuum-pack content grant failed id="
                    + contentId
                    + " status="
                    + grant.Status
                    + " invErr="
                    + grant.InventoryError
                    + " (overflow sent)");
                granted++;
            }

            if (!unpackAborted)
            {
                // Capture: TemplateAction pack Unknown2=3 at Inventory placement, then DeleteItem.
                TemplateActionMessageHandler.Default.Send(
                    character,
                    item,
                    (int)itemPosition.Type,
                    itemPosition.Instance);
                character.BaseInventory.RemoveItem((int)itemPosition.Type, itemPosition.Instance);
                CharacterActionMessageHandler.Default.SendDeleteItem(
                    character,
                    (int)itemPosition.Type,
                    itemPosition.Instance);
            }

            Log(
                "vacuum-pack opened character="
                + character.Identity.ToString(true)
                + " slot="
                + itemPosition
                + " granted="
                + granted
                + " aborted="
                + unpackAborted);
            return true;
        }

        private static void GrantTurnInRewards(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            if (MissionRuntime.IsInitialized
                && MissionRuntime.Service.GetFlag(
                    source.Identity.Instance,
                    DeliverQuestId,
                    RewardsGrantedFlag) != null)
            {
                return;
            }

            if (MissionRuntime.IsInitialized)
            {
                MissionRuntime.Service.SetFlag(
                    source.Identity.Instance,
                    DeliverQuestId,
                    RewardsGrantedFlag,
                    "1");
            }

            TryGrantItem(source, SuitRewardItemId);
            SendOverflowGrantPackets(source, SuitRewardItemId);
            CombatXpRuntimeService.AwardDirectXp(source, FinishXpReward, "leonora-credit-2507xp");
            SendFinishFeedback(source);
        }

        private static bool TryGrantItem(ICharacter character, int itemId)
        {
            if (character == null || !ItemLoader.ItemList.ContainsKey(itemId))
            {
                return false;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(character, itemId))
            {
                return true;
            }

            Item item;
            try
            {
                item = new Item(1, itemId, itemId);
            }
            catch (Exception ex)
            {
                Log("item create failed id=" + itemId + " err=" + ex.Message);
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, item);
            return grant.Status == QuestRewardInventoryGrantStatus.Success;
        }

        private static void SendOverflowGrantPackets(ICharacter character, int itemId)
        {
            if (character == null)
            {
                return;
            }

            character.Send(
                new TemplateActionMessage
                {
                    Identity = character.Identity,
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
            character.Send(
                new ContainerAddItemMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity
                             {
                                 Type = IdentityType.OverflowWindow,
                                 Instance = character.Identity.Instance
                             },
                    TargetPlacement = CapturedOverflowNextFreeSlot
                });
        }

        private static void SendPickupFeedback(ICharacter character)
        {
            if (character?.Controller?.Client == null)
            {
                return;
            }

            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = PickupFeedback,
                    Unknown2 = 0
                });
        }

        private static void SendFinishFeedback(ICharacter character)
        {
            if (character?.Controller?.Client == null)
            {
                return;
            }

            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = FinishRewardFeedback,
                    Unknown2 = 0
                });
        }

        private static bool IsCreditCardWorldProp(ICharacter character, Identity target)
        {
            if (target.Type != IdentityType.Terminal)
            {
                return false;
            }

            if (target.Instance == CreditCardWorldInstance)
            {
                return true;
            }

            // Capture 20260730-214622 / 20260726-secon try CC: world prop instance rotates on respawn.
            // Pool.GetObject throws when the Terminal type/instance is not pooled (e.g. Arete
            // insurance Terminal:C00D1999). That aborted GenericCmd Use before Patrick/Insurance.
            if (character.Playfield == null
                || !Pool.Instance.Contains(character.Playfield.Identity, target))
            {
                return false;
            }

            StaticDynel dynel;
            try
            {
                dynel = Pool.Instance.GetObject<StaticDynel>(character.Playfield.Identity, target);
            }
            catch (Exception)
            {
                return false;
            }

            if (dynel == null)
            {
                return false;
            }

            if (dynel.Template != null && dynel.Template.ID == CreditCardWorldTemplateId)
            {
                return true;
            }

            int template;
            if (dynel.Stats != null
                && (dynel.Stats.TryGetValue((int)StatIds.acgitemtemplateid, out template)
                    || dynel.Stats.TryGetValue((int)StatIds.staticinstance, out template)))
            {
                return template == CreditCardWorldTemplateId;
            }

            return false;
        }

        private static void TryDespawnCreditCard(ICharacter character, Identity target)
        {
            Playfield playfield = character.Playfield as Playfield;
            if (playfield == null)
            {
                return;
            }

            try
            {
                playfield.Announce(DespawnMessageHandler.Default.Create(target));
            }
            catch (Exception ex)
            {
                Log("despawn failed: " + ex.Message);
            }
        }

        private static bool IsLeonoraNpc(ICharacter source, Identity target)
        {
            if (source?.Playfield == null
                || source.Playfield.Identity.Instance != AreteLandingPlayfieldId
                || target.Type != IdentityType.CanbeAffected
                || target.Instance == 0)
            {
                return false;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, target);
            return npc != null
                   && string.Equals(npc.Name, "Leonora Marty", StringComparison.OrdinalIgnoreCase);
        }

        private static void BeginTrade(ICharacter source, Identity npcIdentity)
        {
            lock (TradeSyncRoot)
            {
                TradeByCharacter[source.Identity.Instance] = new LeonoraTradeSession
                {
                    NpcIdentity = npcIdentity,
                    StagedContainer = Identity.None
                };
            }
        }

        private static LeonoraTradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (TradeSyncRoot)
            {
                LeonoraTradeSession session;
                TradeByCharacter.TryGetValue(source.Identity.Instance, out session);
                return session;
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
            LogUtil.Debug(DebugInfoDetail.Engine, "LeonoraMartyQuestRuntime " + message);
        }
    }
}
