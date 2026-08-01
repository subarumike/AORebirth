namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
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

    using Quaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using Vector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    #endregion

    /// <summary>
    /// Optional Marcus side quest (stacks beside Talk to Flint Novak main tip):
    /// Are those wounded workers… → stim 297044 + QFU Use Stim (B199) →
    /// Use stim on Wounded Dockworker → Return to Marcus Stone (B19A) →
    /// FinishTrade Accept only → rechargers 291082x50 + 291043x25
    /// + capture 20260719-224226 rewards: 1281 XP + 1040 credits.
    /// </summary>
    public static class MarcusWoundedWorkersQuestRuntime
    {
        public const string WoundedOfferNodeId = "marcus_wounded_001";

        public const string WoundedAcceptedNodeId = "marcus_wounded_002";

        public const string HealReturnNodeId = "marcus_heal_001";

        public const string HealTradeNodeId = "marcus_heal_trade";

        public const string HealThanksNodeId = "marcus_heal_002";

        private const int AreteLandingPlayfieldId = 6553;

        private const int HealthRegenStimItemId = 297044;

        private const int RechargerItemId = 291082;

        private const int RechargerQuantity = 50;

        private const int NanoRechargerItemId = 291043;

        private const int NanoRechargerQuantity = 25;

        private const int StimReturnXpReward = 1281;

        private const int StimReturnCreditReward = 1040;

        // Capture 20260719-224226 events.log FormatFeedback wire (1281 XP, 1040 credits).
        private const string StimReturnRewardFeedback = "~&!!!\":$'O\"ui!!!0'i!!!-5~";

        private const string StimGrantedFlag = "marcus-wounded-stim-297044";

        private const string StimReturnRewardsFlag = "marcus-wounded-rechargers";

        private const string StimReturnXpCreditsFlag = "marcus-wounded-xp-credits-1281-1040";

        private const string MergedStimReturnXpCreditsFlag = "marcus-wounded-xp-credits-2076-1040";

        private const string WoundedDockworkerName = "Wounded Dockworker";

        private const string DockworkerThankYou = "Wounded Dockworker: Thank you for saving me.";

        private const int HealedRelapseSeconds = 60;

        private const int WoundedCurrentHealth = 12;

        private static readonly object HealRecoverySync = new object();

        private static readonly Dictionary<int, HealRecoveryState> HealRecoveries =
            new Dictionary<int, HealRecoveryState>();

        private sealed class HealRecoveryState
        {
            public float HomeX;
            public float HomeY;
            public float HomeZ;
            public DateTime RelapseUtc;
            public bool WalkedHome;
        }

        public static bool IsHealthRegenStim(IItem item)
        {
            return item != null
                   && (item.LowID == HealthRegenStimItemId || item.HighID == HealthRegenStimItemId);
        }

        public static bool IsStimReturnTip(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (HasCompletedStimReturn(source))
            {
                return false;
            }

            if (RexMarcusChainCoordinator.GetPhase(source) == RexMarcusChainPhase.ReturnMarcusStim)
            {
                return true;
            }

            ZoneEngine.Core.Missions.MissionStateRecord b19a =
                MissionRuntime.Service.GetMission(source.Identity.Instance, MissionRuntime.RexB19AQuestId);
            return b19a != null
                   && (b19a.State == MissionLifecycleState.Active
                       || b19a.State == MissionLifecycleState.Offered);
        }

        /// <summary>
        /// Heal side-quest finished (persistence Completed and/or recharger rewards granted).
        /// </summary>
        public static bool HasCompletedStimReturn(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord b19a =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB19AQuestId);
            if (b19a != null && b19a.State == MissionLifecycleState.Completed)
            {
                return true;
            }

            return MissionRuntime.Service.GetFlag(characterId, MissionRuntime.RexB19AQuestId, StimReturnRewardsFlag)
                   != null
                   || MissionRuntime.Service.GetFlag(
                          characterId,
                          MissionRuntime.RexB19AQuestId,
                          StimReturnXpCreditsFlag) != null
                   || MissionRuntime.Service.GetFlag(
                          characterId,
                          MissionRuntime.RexB19AQuestId,
                          MergedStimReturnXpCreditsFlag) != null;
        }

        public static bool TryHandleDialogueAnswer(
            ICharacter source,
            string previousNodeId,
            int answerIndex)
        {
            if (source == null || string.IsNullOrWhiteSpace(previousNodeId))
            {
                return false;
            }

            if (string.Equals(previousNodeId, WoundedOfferNodeId, StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                if (HasCompletedStimReturn(source))
                {
                    return true;
                }

                AcceptWoundedWorkersBranch(source);
                return true;
            }

            return false;
        }

        public static bool TryBeginStimReturnTrade(ICharacter source, Identity marcusIdentity)
        {
            if (source == null)
            {
                return false;
            }

            if (HasCompletedStimReturn(source))
            {
                Log("stim-return-trade skipped: already completed character=" + source.Identity.ToString(true));
                return false;
            }

            if (marcusIdentity.Type != IdentityType.CanbeAffected || marcusIdentity.Instance == 0)
            {
                marcusIdentity = new Identity
                                {
                                    Type = IdentityType.CanbeAffected,
                                    Instance = unchecked((int)0x782DE567)
                                };
            }

            RexMarcusChainCoordinator.BeginMarcusTradeSession(
                source,
                marcusIdentity,
                RexMarcusChainCoordinator.MarcusTradeKind.Stim);
            KnuBotStartTradeMessageHandler.Default.Send(
                source,
                marcusIdentity,
                "Drag and drop the item(s) you want to give to Marcus Stone into one of the slots available and press \"accept\"",
                1);
            Log(
                "stim-return-trade-opened character="
                + source.Identity.ToString(true)
                + " target="
                + marcusIdentity.ToString(true));
            return true;
        }

        public static bool TryHandleStimUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || message == null || message.Action != GenericCmdAction.Use)
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || !(character.Controller is PlayerController)
                || character.Playfield == null
                || character.Playfield.Identity.Instance != AreteLandingPlayfieldId
                || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (target.Type == IdentityType.None)
            {
                return false;
            }

            IItem stim = ResolveInventoryItem(character, target);
            if (!IsHealthRegenStim(stim))
            {
                return false;
            }

            // Allow stim Use when B199 is Active OR tip already handed off to B19A (retry heal).
            ZoneEngine.Core.Missions.MissionStateRecord b199 =
                MissionRuntime.Service.GetMission(character.Identity.Instance, MissionRuntime.RexB199QuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b19a =
                MissionRuntime.Service.GetMission(character.Identity.Instance, MissionRuntime.RexB19AQuestId);
            bool questOk = b199 != null
                           && (b199.State == MissionLifecycleState.Active
                               || b199.State == MissionLifecycleState.Offered
                               || b199.State == MissionLifecycleState.Completed);
            if (!questOk
                && b19a != null
                && (b19a.State == MissionLifecycleState.Active
                    || b19a.State == MissionLifecycleState.Offered))
            {
                questOk = true;
            }

            if (!questOk)
            {
                return false;
            }

            ICharacter dockworker = ResolveTargetedWoundedDockworker(character);
            // Capture GenericCmd Use Target=Inventory; also accept Use targeting the dockworker directly.
            if (dockworker == null)
            {
                dockworker = ResolveWoundedDockworkerIdentity(character, target);
            }

            if (dockworker == null)
            {
                Log("stim-use rejected: no Wounded Dockworker selected character=" + character.Identity.ToString(true));
                return false;
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            try
            {
                ApplyHealedDockworkerVisual(dockworker);
                CompleteStimUseHandoff(character);
                SendDockworkerThankYou(character);
            }
            catch (Exception e)
            {
                Log("stim-use EXCEPTION: " + e);
            }

            return true;
        }

        public static void CompleteStimReturnTurnIn(
            ICharacter source,
            Identity marcusTarget,
            Identity stagedContainer,
            string trigger)
        {
            if (source == null)
            {
                return;
            }

            if (HasCompletedStimReturn(source))
            {
                Log(
                    "stim-return-turnin skipped: already rewarded character="
                    + source.Identity.ToString(true)
                    + " trigger="
                    + trigger);
                try
                {
                    KnuBotRejectedItemsMessageHandler.Default.Send(source, marcusTarget, new Item[0], 0);
                }
                catch (Exception)
                {
                }

                SafeQuestFullUpdateSender.TrySendB19AQuestDeleteOnly(source);
                return;
            }

            Log(
                "stim-return-turnin begin character="
                + source.Identity.ToString(true)
                + " trigger="
                + trigger);

            if (!ApplyStimReturnRewards(source))
            {
                Log("stim-return turn-in deferred: durable reward was not completed");
                return;
            }

            TryConsumeStim(source, stagedContainer);

            try
            {
                KnuBotRejectedItemsMessageHandler.Default.Send(source, marcusTarget, new Item[0], 0);
            }
            catch (Exception e)
            {
                Log("stim-return rejecteditems failed: " + e.Message);
            }

            SendStimReturnRewardFeedback(source);
            TryGrantRechargerRewards(source);

            if (MissionRuntime.IsInitialized)
            {
                try
                {
                    ForceCompleteMission(source.Identity.Instance, MissionRuntime.RexB19AQuestId);
                }
                catch (Exception e)
                {
                    Log("stim-return persistence failed: " + e.Message);
                }
            }

            // Remove heal side-quest tip only (Return to Marcus Stone). Flint main tip stays.
            // Delete-only mid-dialogue — Action59 here can abort transport before Delete lands.
            SafeQuestFullUpdateSender.TrySendB19ACompletionCleanup(source);
            SafeQuestFullUpdateSender.TrySendB19AQuestDeleteOnly(source);

            try
            {
                ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, marcusTarget);
            }
            catch (Exception e)
            {
                Log("stim-return resume-dialogue failed: " + e.Message);
            }

            // Resume can reopen Marcus cleanup; force one more delete so B19A cannot stick.
            SafeQuestFullUpdateSender.TrySendB19AQuestDeleteOnly(source);

            Log(
                "stim-return-turnin done character="
                + source.Identity.ToString(true)
                + " phaseNow="
                + RexMarcusChainCoordinator.GetPhase(source));
        }

        private static void AcceptWoundedWorkersBranch(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;

            // Side quest: ADD Use Stim (B199) beside Talk to Flint Novak (main). Do not replace Flint.
            // Never leave premature "Return to Marcus Stone" (B19A) stacked beside Use Stim.
            ClearPrematureB19A(source);

            try
            {
                MissionRuntime.Service.OfferMission(characterId, MissionRuntime.RexB199QuestId);
                MissionRuntime.Service.AcceptMission(characterId, MissionRuntime.RexB199QuestId);
            }
            catch (Exception e)
            {
                Log("b199 offer/accept failed: " + e.Message);
            }

            TryGrantHealthRegenStim(source);
            SafeQuestFullUpdateSender.TrySendB199Preview(source);
            SafeQuestFullUpdateSender.TrySendB19AQuestDeleteOnly(source);
            Log("wounded-workers accepted (stacked beside Flint) character=" + source.Identity.ToString(true));
        }

        /// <summary>
        /// B19A is only valid after stim use. Drop Active/Offered leftovers from dirty retries.
        /// </summary>
        private static void ClearPrematureB19A(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord b19a =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB19AQuestId);
            if (b19a == null)
            {
                return;
            }

            if (b19a.State != MissionLifecycleState.Active
                && b19a.State != MissionLifecycleState.Offered)
            {
                return;
            }

            try
            {
                MissionRuntime.Service.AbandonMission(characterId, MissionRuntime.RexB19AQuestId);
                Log("cleared premature B19A character=" + source.Identity.ToString(true));
            }
            catch (Exception e)
            {
                Log("clear premature B19A failed: " + e.Message);
            }
        }

        private static void CompleteStimUseHandoff(ICharacter source)
        {
            int characterId = source.Identity.Instance;
            try
            {
                ForceCompleteMission(characterId, MissionRuntime.RexB199QuestId);
                MissionRuntime.Service.OfferMission(characterId, MissionRuntime.RexB19AQuestId);
                MissionRuntime.Service.AcceptMission(characterId, MissionRuntime.RexB19AQuestId);
            }
            catch (Exception e)
            {
                Log("stim-use persistence failed: " + e.Message);
            }

            SafeQuestFullUpdateSender.TrySendB199ToB19AHandoff(source);
            Log("stim-use handoff B199→B19A character=" + source.Identity.ToString(true));
        }

        private static void TryGrantHealthRegenStim(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    HealthRegenStimItemId))
            {
                MissionRuntime.Service.SetFlag(
                    characterId,
                    MissionRuntime.RexB199QuestId,
                    StimGrantedFlag,
                    "item:" + HealthRegenStimItemId);
                return;
            }

            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(HealthRegenStimItemId))
            {
                Log(
                    "stim grant skipped: inventory-or-itemloader missing item="
                    + HealthRegenStimItemId
                    + " inItemList="
                    + ItemLoader.ItemList.ContainsKey(HealthRegenStimItemId));
                return;
            }

            // Match Marcus B18F suppressant handout: Unique templates block retries.
            try
            {
                ItemTemplate template;
                if (ItemLoader.ItemList.TryGetValue(HealthRegenStimItemId, out template) && template != null)
                {
                    int flags = template.Stats.ContainsKey(0) ? template.Stats[0] : 0;
                    if ((flags & (int)ItemFlags.Unique) != 0)
                    {
                        template.Stats[0] = flags & ~(int)ItemFlags.Unique;
                    }
                }
            }
            catch (Exception e)
            {
                Log("stim unique-clear failed: " + e.Message);
            }

            Item item;
            try
            {
                item = new Item(1, HealthRegenStimItemId, HealthRegenStimItemId);
                if (item.MultipleCount < 1)
                {
                    item.MultipleCount = 1;
                }
            }
            catch (Exception e)
            {
                Log("stim create failed: " + e.Message);
                return;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log("stim grant failed status=" + grant.Status);
                return;
            }

            try
            {
                SendOverflowTemplateAction(source, HealthRegenStimItemId, 1);
                FeedbackMessageHandler.Default.Send(source, 110, 108871108);
            }
            catch (Exception e)
            {
                Log("stim notify failed: " + e.Message);
            }

            MissionRuntime.Service.SetFlag(
                characterId,
                MissionRuntime.RexB199QuestId,
                StimGrantedFlag,
                "item:" + HealthRegenStimItemId);
        }

        private static void TryGrantRechargerRewards(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            if (MissionRuntime.Service.GetFlag(
                    characterId,
                    MissionRuntime.RexB19AQuestId,
                    StimReturnRewardsFlag) != null)
            {
                return;
            }

            GrantStackedRewardItem(source, RechargerItemId, RechargerQuantity);
            GrantStackedRewardItem(source, NanoRechargerItemId, NanoRechargerQuantity);
            MissionRuntime.Service.SetFlag(
                characterId,
                MissionRuntime.RexB19AQuestId,
                StimReturnRewardsFlag,
                "items:" + RechargerItemId + "x" + RechargerQuantity + "+" + NanoRechargerItemId + "x"
                + NanoRechargerQuantity);
        }

        private static void GrantStackedRewardItem(ICharacter source, int itemId, int quantity)
        {
            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(itemId))
            {
                Log("reward grant skipped item=" + itemId);
                return;
            }

            Item item;
            try
            {
                item = new Item(1, itemId, itemId);
                item.MultipleCount = quantity;
            }
            catch (Exception e)
            {
                Log("reward create failed item=" + itemId + " err=" + e.Message);
                return;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log("reward grant failed item=" + itemId + " status=" + grant.Status);
                return;
            }

            SendOverflowTemplateAction(source, itemId, quantity);
            FeedbackMessageHandler.Default.Send(source, 110, 108871108);
        }

        private static void SendOverflowTemplateAction(ICharacter source, int itemId, int unknown1Quantity)
        {
            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = itemId,
                    ItemHighId = itemId,
                    Quality = 1,
                    Unknown1 = unknown1Quantity,
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
                    Target = new Identity
                             {
                                 Type = IdentityType.OverflowWindow,
                                 Instance = source.Identity.Instance
                             },
                    TargetPlacement = 0x6F
                });
        }

        private static bool ApplyStimReturnRewards(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (MissionRuntime.IsInitialized
                && (MissionRuntime.Service.GetFlag(
                        source.Identity.Instance,
                        MissionRuntime.RexB19AQuestId,
                        StimReturnXpCreditsFlag) != null
                    || MissionRuntime.Service.GetFlag(
                           source.Identity.Instance,
                           MissionRuntime.RexB19AQuestId,
                           MergedStimReturnXpCreditsFlag) != null))
            {
                Log("stim-return xp/credits skipped: flag already set character=" + source.Identity.ToString(true));
                return true;
            }

            if (!MissionRuntime.IsInitialized || MissionRuntime.Rewards == null)
            {
                Log("stim-return rewards deferred: mission reward runtime unavailable");
                return false;
            }

            try
            {
                var definition = new MissionRewardDefinition
                                 {
                                     RewardKey = "captured-marcus-stim-return-xp-credits",
                                     LegacyRewardKeys = new[]
                                                        {
                                                            "captured-marcus-stim-return-xp-credits-2076-1040"
                                                        },
                                     RewardType = "character-stats",
                                     IsResolved = true,
                                     StatMutations = new[]
                                                     {
                                                             new MissionCharacterStatMutation
                                                             {
                                                                 StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                 StatId = (int)StatIds.cash,
                                                                 Kind = MissionStatMutationKind.AddClamped,
                                                                 Value = StimReturnCreditReward,
                                                                 MinimumValue = 0,
                                                                 MaximumValue = uint.MaxValue
                                                             },
                                                             new MissionCharacterStatMutation
                                                             {
                                                                 StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                 StatId = (int)StatIds.xp,
                                                                 Kind = MissionStatMutationKind.AddClamped,
                                                                 Value = StimReturnXpReward,
                                                                 MinimumValue = 0,
                                                                 MaximumValue = uint.MaxValue
                                                             },
                                                             new MissionCharacterStatMutation
                                                             {
                                                                 StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                 StatId = (int)StatIds.unsavedxp,
                                                                 Kind = MissionStatMutationKind.AddClamped,
                                                                 Value = StimReturnXpReward,
                                                                 MinimumValue = 0,
                                                                 MaximumValue = uint.MaxValue
                                                             },
                                                             new MissionCharacterStatMutation
                                                             {
                                                                 StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                 StatId = (int)StatIds.lastxp,
                                                                 Kind = MissionStatMutationKind.Set,
                                                                 Value = StimReturnXpReward,
                                                                 MinimumValue = 0,
                                                                 MaximumValue = uint.MaxValue
                                                             }
                                                     }
                                 };
                MissionRewardExecutionResult result = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                    source.Identity.Instance,
                    MissionRuntime.RexB19AQuestId,
                    definition,
                    "capture:20260719-224226:marcus-b19a-xp-credits");
                if (result == null || !result.Succeeded)
                {
                    Log(
                        "stim-return rewards ledger status="
                        + (result == null ? "null" : result.Status.ToString())
                        + " msg="
                        + (result == null ? string.Empty : result.Message));
                    return false;
                }

                if (result.StatValues != null)
                {
                    foreach (MissionCharacterStatValue statValue in result.StatValues)
                    {
                        uint value = statValue.Value <= 0
                                         ? 0
                                         : (uint)Math.Min(statValue.Value, uint.MaxValue);
                        source.Stats[(StatIds)statValue.StatId].Set(value);
                    }

                    StatMessageHandler.Default.SendChanged(source);
                }

                MissionRuntime.Service.SetFlag(
                    source.Identity.Instance,
                    MissionRuntime.RexB19AQuestId,
                    StimReturnXpCreditsFlag,
                    "xp:" + StimReturnXpReward + "+credits:" + StimReturnCreditReward);
                return true;
            }
            catch (Exception e)
            {
                Log("stim-return rewards deferred: " + e.Message);
                return false;
            }
        }

        private static void SendStimReturnRewardFeedback(ICharacter source)
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
                        FormattedMessage = StimReturnRewardFeedback,
                        Unknown2 = 0
                    });
            }
            catch (Exception e)
            {
                Log("stim reward feedback failed: " + e.Message);
            }
        }

        private static void TryConsumeStim(ICharacter source, Identity stagedContainer)
        {
            if (source == null || source.BaseInventory == null)
            {
                return;
            }

            if (stagedContainer.Type != IdentityType.None && stagedContainer.Instance > 0)
            {
                IInventoryPage stagedPage;
                if (source.BaseInventory.Pages.TryGetValue((int)stagedContainer.Type, out stagedPage)
                    && stagedPage != null)
                {
                    IItem staged = stagedPage[stagedContainer.Instance];
                    if (IsHealthRegenStim(staged))
                    {
                        stagedPage.Remove(stagedContainer.Instance);
                        try
                        {
                            if (source.BaseInventory.Write())
                            {
                                CharacterActionMessageHandler.Default.SendDeleteItem(
                                    source,
                                    (int)stagedContainer.Type,
                                    stagedContainer.Instance);
                                return;
                            }
                        }
                        catch (Exception)
                        {
                        }

                        stagedPage.Add(stagedContainer.Instance, staged);
                    }
                }
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in source.BaseInventory.Pages)
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> entry in page.List())
                {
                    if (!IsHealthRegenStim(entry.Value))
                    {
                        continue;
                    }

                    page.Remove(entry.Key);
                    try
                    {
                        if (source.BaseInventory.Write())
                        {
                            CharacterActionMessageHandler.Default.SendDeleteItem(
                                source,
                                pageEntry.Key,
                                entry.Key);
                            return;
                        }
                    }
                    catch (Exception)
                    {
                    }

                    page.Add(entry.Key, entry.Value);
                }
            }
        }

        private static ICharacter ResolveTargetedWoundedDockworker(ICharacter source)
        {
            Identity selected = source.SelectedTarget;
            if (selected.Type == IdentityType.None || selected.Instance == 0)
            {
                selected = source.FightingTarget;
            }

            return ResolveWoundedDockworkerIdentity(source, selected);
        }

        private static ICharacter ResolveWoundedDockworkerIdentity(ICharacter source, Identity selected)
        {
            if (selected.Type == IdentityType.None || selected.Instance == 0 || source.Playfield == null)
            {
                return null;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, selected);
            if (npc == null || string.IsNullOrWhiteSpace(npc.Name))
            {
                return null;
            }

            return string.Equals(npc.Name.Trim(), WoundedDockworkerName, StringComparison.OrdinalIgnoreCase)
                       ? npc
                       : null;
        }

        private static IItem ResolveInventoryItem(ICharacter character, Identity itemIdentity)
        {
            if (character == null || character.BaseInventory == null)
            {
                return null;
            }

            IInventoryPage page;
            if (!character.BaseInventory.Pages.TryGetValue((int)itemIdentity.Type, out page) || page == null)
            {
                return null;
            }

            return page[itemIdentity.Instance];
        }

        private static void SendDockworkerThankYou(ICharacter source)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return;
            }

            try
            {
                source.Controller.Client.SendCompressed(
                    new ChatTextMessage { Identity = source.Identity, Text = DockworkerThankYou });
            }
            catch (Exception e)
            {
                Log("dockworker thank-you failed: " + e.Message);
            }
        }

        /// <summary>
        /// After heal: stand anim, walk home, then after 60s sit + wounded HP again.
        /// Call from Arete patrol tick.
        /// </summary>
        public static void TickHealRecoveries(Playfield playfield)
        {
            if (playfield == null || playfield.Identity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            List<int> due = null;
            DateTime now = DateTime.UtcNow;
            lock (HealRecoverySync)
            {
                foreach (KeyValuePair<int, HealRecoveryState> entry in HealRecoveries)
                {
                    if (entry.Value.RelapseUtc <= now)
                    {
                        if (due == null)
                        {
                            due = new List<int>();
                        }

                        due.Add(entry.Key);
                    }
                    else if (!entry.Value.WalkedHome)
                    {
                        ICharacter npc = Pool.Instance.GetObject<ICharacter>(
                            playfield.Identity,
                            new Identity { Type = IdentityType.CanbeAffected, Instance = entry.Key });
                        if (npc == null)
                        {
                            continue;
                        }

                        NPCController controller = npc.Controller as NPCController;
                        if (controller == null)
                        {
                            continue;
                        }

                        float dx = npc.RawCoordinates.X - entry.Value.HomeX;
                        float dz = npc.RawCoordinates.Z - entry.Value.HomeZ;
                        if ((dx * dx) + (dz * dz) < 0.25f)
                        {
                            entry.Value.WalkedHome = true;
                            continue;
                        }

                        controller.MoveTo(
                            new Vector3
                            {
                                X = entry.Value.HomeX,
                                Y = entry.Value.HomeY,
                                Z = entry.Value.HomeZ
                            });
                        entry.Value.WalkedHome = true;
                    }
                }
            }

            if (due == null)
            {
                return;
            }

            for (int i = 0; i < due.Count; i++)
            {
                int instance = due[i];
                HealRecoveryState state;
                lock (HealRecoverySync)
                {
                    if (!HealRecoveries.TryGetValue(instance, out state))
                    {
                        continue;
                    }

                    HealRecoveries.Remove(instance);
                }

                ICharacter npc = Pool.Instance.GetObject<ICharacter>(
                    playfield.Identity,
                    new Identity { Type = IdentityType.CanbeAffected, Instance = instance });
                if (npc == null)
                {
                    continue;
                }

                ApplyWoundedDockworkerRelapse(npc, state);
            }
        }

        /// <summary>
        /// Capture 20260720-064523 after stim Use on Wounded Dockworker 78E0FC6F:
        /// SpellList ×2 → CharacterAction StandUp → thank-you chat →
        /// HealthDamage Amount=20 TargetHp=32 Stat=Flags → SpellList.
        /// Then return home; after 60s sit + HP 12 again.
        /// </summary>
        private static void ApplyHealedDockworkerVisual(ICharacter dockworker)
        {
            if (dockworker == null || dockworker.Playfield == null)
            {
                return;
            }

            float homeX = dockworker.RawCoordinates.X;
            float homeY = dockworker.RawCoordinates.Y;
            float homeZ = dockworker.RawCoordinates.Z;

            AnnounceEmptySpellList(dockworker);
            AnnounceEmptySpellList(dockworker);

            Character asCharacter = dockworker as Character;
            if (asCharacter != null)
            {
                asCharacter.UpdateMoveType(37);
                asCharacter.MoveMode = MoveModes.Run;
            }

            dockworker.Stats[StatIds.currentmovementmode].Value = (int)MoveModes.Run;
            dockworker.Stats[StatIds.currentmovementmode].BaseValue = (uint)MoveModes.Run;
            dockworker.Stats[StatIds.prevmovementmode].Value = (int)MoveModes.Run;
            dockworker.Stats[StatIds.prevmovementmode].BaseValue = (uint)MoveModes.Run;

            dockworker.Playfield.Announce(
                new CharacterActionMessage
                {
                    Identity = dockworker.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.StandUp,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = 0,
                    Unknown2 = 0
                });

            dockworker.Playfield.Announce(
                new CharDCMoveMessage
                {
                    Identity = dockworker.Identity,
                    Unknown = 0,
                    MoveType = 37,
                    Heading =
                        new Quaternion
                        {
                            X = dockworker.Heading.xf,
                            Y = dockworker.Heading.yf,
                            Z = dockworker.Heading.zf,
                            W = dockworker.Heading.wf
                        },
                    Coordinates =
                        new Vector3
                        {
                            X = dockworker.RawCoordinates.X,
                            Y = dockworker.RawCoordinates.Y,
                            Z = dockworker.RawCoordinates.Z
                        },
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });

            int maxHp = dockworker.Stats[StatIds.life].Value;
            if (maxHp <= 0)
            {
                maxHp = 32;
            }

            int currentHp = dockworker.Stats[StatIds.health].Value;
            int healAmount = maxHp - currentHp;
            if (healAmount <= 0)
            {
                healAmount = 20;
            }

            dockworker.Stats[StatIds.health].Value = maxHp;
            dockworker.Stats[StatIds.health].BaseValue = (uint)maxHp;

            dockworker.Playfield.Announce(
                new HealthDamageMessage
                {
                    Identity = dockworker.Identity,
                    Unknown = 0,
                    Unknown1 = healAmount,
                    Unknown2 = (int)StatIds.flags,
                    Unknown3 = maxHp,
                    Unknown4 = 0,
                    Target = dockworker.Identity,
                    Unknown5 = 0
                });

            AnnounceEmptySpellList(dockworker);
            dockworker.SendChangedStats();

            Playfield playfield = dockworker.Playfield as Playfield;
            if (playfield != null)
            {
                playfield.AnnounceSpawnedCharacterVisibility(dockworker, Identity.None);
            }

            NPCController npcController = dockworker.Controller as NPCController;
            if (npcController != null)
            {
                npcController.MoveTo(
                    new Vector3 { X = homeX, Y = homeY, Z = homeZ });
            }

            lock (HealRecoverySync)
            {
                HealRecoveries[dockworker.Identity.Instance] = new HealRecoveryState
                {
                    HomeX = homeX,
                    HomeY = homeY,
                    HomeZ = homeZ,
                    RelapseUtc = DateTime.UtcNow.AddSeconds(HealedRelapseSeconds),
                    WalkedHome = true
                };
            }

            Log(
                "healed-visual StandUp+HP dockworker="
                + dockworker.Identity.ToString(true)
                + " hp="
                + maxHp
                + " relapseSec="
                + HealedRelapseSeconds
                + " source=20260720-064523");
        }

        private static void ApplyWoundedDockworkerRelapse(ICharacter dockworker, HealRecoveryState state)
        {
            if (dockworker == null || dockworker.Playfield == null || state == null)
            {
                return;
            }

            NPCController controller = dockworker.Controller as NPCController;
            if (controller != null)
            {
                controller.MoveTo(
                    new Vector3 { X = state.HomeX, Y = state.HomeY, Z = state.HomeZ });
            }

            dockworker.RawCoordinates = new AORebirth.Core.Vector.Vector3(
                state.HomeX,
                state.HomeY,
                state.HomeZ);

            Character asCharacter = dockworker as Character;
            if (asCharacter != null)
            {
                asCharacter.UpdateMoveType(30);
                asCharacter.MoveMode = MoveModes.Sit;
            }

            dockworker.Stats[StatIds.currentmovementmode].Value = (int)MoveModes.Sit;
            dockworker.Stats[StatIds.currentmovementmode].BaseValue = (uint)MoveModes.Sit;
            dockworker.Stats[StatIds.prevmovementmode].Value = (int)MoveModes.Run;
            dockworker.Stats[StatIds.prevmovementmode].BaseValue = (uint)MoveModes.Run;
            dockworker.Stats[StatIds.health].Value = WoundedCurrentHealth;
            dockworker.Stats[StatIds.health].BaseValue = (uint)WoundedCurrentHealth;

            dockworker.Playfield.Announce(
                new CharacterActionMessage
                {
                    Identity = dockworker.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.SitDown,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = 0,
                    Unknown2 = 0
                });

            dockworker.Playfield.Announce(
                new CharDCMoveMessage
                {
                    Identity = dockworker.Identity,
                    Unknown = 0,
                    MoveType = 30,
                    Heading =
                        new Quaternion
                        {
                            X = dockworker.Heading.xf,
                            Y = dockworker.Heading.yf,
                            Z = dockworker.Heading.zf,
                            W = dockworker.Heading.wf
                        },
                    Coordinates =
                        new Vector3 { X = state.HomeX, Y = state.HomeY, Z = state.HomeZ },
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });

            dockworker.SendChangedStats();
            Playfield playfield = dockworker.Playfield as Playfield;
            if (playfield != null)
            {
                playfield.AnnounceSpawnedCharacterVisibility(dockworker, Identity.None);
            }

            Log("healed-relapse Sit+HP12 dockworker=" + dockworker.Identity.ToString(true));
        }

        private static void AnnounceEmptySpellList(ICharacter dockworker)
        {
            try
            {
                dockworker.Playfield.Announce(
                    new SpellListMessage
                    {
                        Identity = dockworker.Identity,
                        Character = dockworker.Identity,
                        NanoEffects = new NanoEffect[0]
                    });
            }
            catch (Exception e)
            {
                Log("spelllist announce failed: " + e.Message);
            }
        }

        private static void ForceCompleteMission(int characterId, string questId)
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
                    ObservationKey = "marcus-wounded-workers",
                    Amount = 1,
                    EventType = "MarcusWoundedWorkers",
                    SourceIdentity = string.Empty,
                    TargetIdentity = string.Empty
                });
            MissionRuntime.Service.CompleteMission(characterId, questId);
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Error, "ARETE_MARCUS_WOUNDED_WORKERS " + message);
        }
    }
}
