namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

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

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;

    #endregion

    public static class SurveillanceUplinkQuestRuntime
    {
        private sealed class BillTradeSession
        {
            public Identity NpcIdentity;

            public Identity StagedContainer;
        }

        public const string BillTradeOfferNodeId = "bill_105157_001";

        public const string BillTradeHoldNodeId = "bill_105157_trade";

        public const string BillKneecappingOfferNodeId = "bill_105157_002";

        public const int RebuiltHc12SecTecMonitorItemId = 295800;

        public const int RcPAudioRecordingDeviceItemId = 295801;

        private const int BillInstance = 2028010598;

        private const int PrizedHouseplantInstance = 1463912423;

        private const int PrizedHouseplantTemplateId = 295738;

        private const int AreteLandingPlayfieldId = 6553;

        private const int BillTurnInXpReward = 2229;

        private const int BillTurnInCreditReward = 1160;

        private const string UplinkFeedback = "~&!!!\":!!!)<s\u001dHC-12 SecTec: Camera feed activated.";

        // Capture 20260720-105157 FormatFeedback wire.
        private const string BillTurnInRewardFeedback = "~&!!!\":$'O\"ui!!!;4i!!!.X~";

        private const string DroidBeepChat = "Surveillance Droid: Beep Beep Beep!";

        private const string RcPGrantFlag = "rcp-audio-granted";

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private const int UniqueItemFlagBit = 0x08000000;

        private static readonly object TradeSyncRoot = new object();

        private static readonly Dictionary<int, BillTradeSession> TradeSessionsByCharacter =
            new Dictionary<int, BillTradeSession>();

        private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

        public static bool TryHandleSecTecUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || message == null || message.Action != GenericCmdAction.Use)
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (!IsValidPlayerInArete(character) || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            IItem item = ResolveInventoryItem(character, target);
            if (!IsSecTecMonitor(item))
            {
                return false;
            }

            ICharacter droid = ResolveTargetedSurveillanceDroid(character);
            if (droid == null || !IsCaptureSurveillanceDroid(droid))
            {
                return false;
            }

            if (!IsSelectedTarget(character, droid.Identity))
            {
                return false;
            }

            bool uplinkActive = IsSurveillanceUplinkActive(character);
            bool plantBugActive = IsPlantBugActive(character);
            bool hasRcP = HasRcPDevice(character);

            // Recovery: player has HC-12 + tip/items from Alex turn-in but Mission:5514B19D never activated.
            if (!uplinkActive && !plantBugActive && !hasRcP && HasSecTecMonitor(character))
            {
                EnsureSurveillanceUplinkActive(character);
                uplinkActive = IsSurveillanceUplinkActive(character);
            }

            if (!uplinkActive && plantBugActive && !hasRcP)
            {
                GenericCmdMessageHandler.Default.Acknowledge(character, message);
                try
                {
                    SendUplinkFeedback(character);
                    SendDroidBeep(character);
                    if (!TryGrantRcPDevice(character))
                    {
                        Log(
                            "sectec-use recovery grant RC-P failed character="
                            + character.Identity.ToString(true));
                    }
                }
                catch (Exception ex)
                {
                    Log("sectec-use recovery EXCEPTION: " + ex);
                }

                return true;
            }

            if (!uplinkActive)
            {
                return false;
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            try
            {
                SendUplinkFeedback(character);
                SendDroidBeep(character);
                CompleteUplinkAndOfferPlantBug(character);
                if (!TryGrantRcPDevice(character))
                {
                    Log("sectec-use grant RC-P failed character=" + character.Identity.ToString(true));
                }
            }
            catch (Exception ex)
            {
                Log("sectec-use EXCEPTION: " + ex);
            }

            return true;
        }

        public static bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
        {
            if (client == null || message == null || message.Target == null || message.Target.Length < 2)
            {
                return false;
            }

            if (UseItemOnItemInteractionRules.ResolveRouteMode(message.Action)
                != UseItemOnItemInteractionRouteMode.UseItemOnItem)
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (!IsValidPlayerInArete(character) || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            Identity itemIdentity = message.Target[0];
            IItem item = ResolveInventoryItem(character, itemIdentity);
            if (item == null || !IsRcPDevice(item))
            {
                return false;
            }

            if (!IsPlantBugActive(character))
            {
                return false;
            }

            if (!IsPrizedHouseplantTarget(character, message.Target[1]))
            {
                return false;
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            TryConsumeInventoryItem(character, itemIdentity, RcPAudioRecordingDeviceItemId);
            CompletePlantBugAndOfferDeliverBill(character);
            return true;
        }

        public static bool TryBeginBillTrade(ICharacter source, Identity billIdentity)
        {
            if (source == null)
            {
                return false;
            }

            EnsureBillDeliverAvailable(source);
            if (billIdentity.Type != IdentityType.CanbeAffected || billIdentity.Instance == 0)
            {
                billIdentity = new Identity
                               {
                                   Type = IdentityType.CanbeAffected,
                                   Instance = BillInstance
                               };
            }

            BeginBillTrade(source, billIdentity);
            KnuBotStartTradeMessageHandler.Default.Send(
                source,
                billIdentity,
                "Drag and drop the item(s) you want to give to ICC Immigration Officer Bill into one of the slots available and press \"accept\"",
                1);
            Log(
                "bill-trade-opened character="
                + source.Identity.ToString(true)
                + " hasHc12="
                + HasSecTecMonitor(source));
            return true;
        }

        public static bool TryStageBillTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (source == null || message == null || !IsBillNpc(source, message.Target))
            {
                return false;
            }

            if (!HasSecTecMonitor(source) && !IsBillDeliverActive(source) && GetTradeSession(source) == null)
            {
                return false;
            }

            EnsureBillTradeSession(source, message.Target);
            BillTradeSession session = GetTradeSession(source);
            if (session == null)
            {
                return true;
            }

            session.NpcIdentity = message.Target;
            if (message.Container.Type != IdentityType.None && message.Container.Instance > 0)
            {
                session.StagedContainer = message.Container;
                Log(
                    "bill-trade-staged character="
                    + source.Identity.ToString(true)
                    + " container="
                    + message.Container.ToString(true));
            }

            return true;
        }

        public static bool ShouldSuppressGenericBillTradeRemove(ICharacter source, Identity target)
        {
            if (source == null || !IsBillNpc(source, target))
            {
                return false;
            }

            return HasSecTecMonitor(source) || IsBillDeliverActive(source) || GetTradeSession(source) != null;
        }

        public static bool TryFinishBillTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null)
            {
                return false;
            }

            bool isBill = IsBillNpc(source, message.Target);
            BillTradeSession session = GetTradeSession(source);
            // Only claim Bill's own FinishTrade. Tip/HC-12 inventory flags must not steal
            // Accept from other NPCs (ZoneEngineLog 2026-07-21 01:33:42 Stan → bill-turnin ABORTED).
            if (!isBill && session == null)
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            if (session == null)
            {
                EnsureBillTradeSession(source, message.Target);
                session = GetTradeSession(source);
            }

            Identity stagedContainer = session != null ? session.StagedContainer : Identity.None;
            ApplyBillTradeTurnIn(source, message.Target, stagedContainer);
            return true;
        }

        private static void ApplyBillTradeTurnIn(ICharacter source, Identity billTarget, Identity stagedContainer)
        {
            int instance = source.Identity.Instance;
            lock (TradeSyncRoot)
            {
                if (!TurnInInFlightByCharacter.Add(instance))
                {
                    return;
                }
            }

            try
            {
                if (!TryConsumeInventoryItem(source, stagedContainer, RebuiltHc12SecTecMonitorItemId))
                {
                    Log(
                        "bill-turnin ABORTED — HC-12 not consumed character="
                        + source.Identity.ToString(true)
                        + " staged="
                        + stagedContainer.ToString(true)
                        + " hasItem="
                        + HasSecTecMonitor(source));
                    Identity reopenTarget = billTarget;
                    if (reopenTarget.Type != IdentityType.CanbeAffected || reopenTarget.Instance == 0)
                    {
                        reopenTarget = new Identity
                                       {
                                           Type = IdentityType.CanbeAffected,
                                           Instance = BillInstance
                                       };
                    }

                    BeginBillTrade(source, reopenTarget);
                    KnuBotStartTradeMessageHandler.Default.Send(
                        source,
                        reopenTarget,
                        "Drag and drop the item(s) you want to give to ICC Immigration Officer Bill into one of the slots available and press \"accept\"",
                        1);
                }
                else
                {
                    try
                    {
                        KnuBotRejectedItemsMessageHandler.Default.Send(source, billTarget, new Item[0], 0);
                    }
                    catch (Exception ex)
                    {
                        Log("bill-rejecteditems failed: " + ex.Message);
                    }

                    ApplyBillTurnInXpCredits(source);
                    TrySendBillTurnInRewardFeedback(source);
                    CompleteDeliverBillAndClearTips(source);
                    ForgetTradeSession(source);
                    try
                    {
                        ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, billTarget);
                    }
                    catch (Exception ex)
                    {
                        Log("bill-resume-dialogue failed: " + ex.Message);
                    }

                    Log("bill-turnin done character=" + source.Identity.ToString(true));
                }
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(instance);
                }
            }
        }

        private static void CompleteUplinkAndOfferPlantBug(ICharacter source)
        {
            int instance = source.Identity.Instance;
            MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                instance,
                "Mission:5514B19D",
                "Mission:5514B19E");
            if (result.Status != MissionOperationStatus.Applied
                && result.Status != MissionOperationStatus.AlreadyApplied)
            {
                MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19D");
                MissionRuntime.Service.OfferMission(instance, "Mission:5514B19E");
                MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19E");
            }

            SafeQuestFullUpdateSender.TrySendUplinkToPlantBugHandoff(source);
            TryGrantRcPDevice(source);
        }

        private static void CompletePlantBugAndOfferDeliverBill(ICharacter source)
        {
            int instance = source.Identity.Instance;
            MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                instance,
                "Mission:5514B19E",
                "Mission:5514B19F");
            if (result.Status != MissionOperationStatus.Applied
                && result.Status != MissionOperationStatus.AlreadyApplied)
            {
                MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19E");
                MissionRuntime.Service.OfferMission(instance, "Mission:5514B19F");
                MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19F");
            }

            SafeQuestFullUpdateSender.TrySendPlantBugToDeliverBillHandoff(source);
        }

        private static void CompleteDeliverBillAndClearTips(ICharacter source)
        {
            int instance = source.Identity.Instance;
            MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                instance,
                "Mission:5514B19F",
                "Mission:5514B1A0");
            if (result.Status != MissionOperationStatus.Applied
                && result.Status != MissionOperationStatus.AlreadyApplied)
            {
                MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19F");
                MissionRuntime.Service.OfferMission(instance, "Mission:5514B1A0");
                MissionRuntime.Service.AcceptMission(instance, "Mission:5514B1A0");
            }

            MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19E");
            MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19D");
            SafeQuestFullUpdateSender.TrySendDeliverBillToKneecappingHandoff(source);
            Log("bill-deliver-to-kneecapping character=" + source.Identity.ToString(true));
        }

        public static bool TryHandleBillDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null || answerIndex != 0)
            {
                return false;
            }

            bool kneecappingOffer = string.Equals(
                previousNodeId,
                BillKneecappingOfferNodeId,
                StringComparison.OrdinalIgnoreCase);
            bool tradeHold = string.Equals(
                previousNodeId,
                BillTradeHoldNodeId,
                StringComparison.OrdinalIgnoreCase);
            if (!kneecappingOffer && !tradeHold)
            {
                return false;
            }

            OfferKneecappingMission(source);
            return true;
        }

        private static void OfferKneecappingMission(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (HasSecTecMonitor(source)
                && !TryConsumeInventoryItem(source, Identity.None, RebuiltHc12SecTecMonitorItemId))
            {
                Log("bill-kneecapping consume-retry failed character=" + source.Identity.ToString(true));
            }

            if (!IsKneecappingActive(source))
            {
                MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                    instance,
                    "Mission:5514B19F",
                    "Mission:5514B1A0");
                if (result.Status != MissionOperationStatus.Applied
                    && result.Status != MissionOperationStatus.AlreadyApplied)
                {
                    MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19F");
                    MissionRuntime.Service.OfferMission(instance, "Mission:5514B1A0");
                    MissionRuntime.Service.AcceptMission(instance, "Mission:5514B1A0");
                }
            }

            MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19E");
            MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19D");
            RexQuestPreviewEmissionResult tip =
                SafeQuestFullUpdateSender.TrySendDeliverBillToKneecappingHandoff(source);
            Log(
                "bill-kneecapping-offered character="
                + source.Identity.ToString(true)
                + " tipEmitted="
                + (tip != null && tip.Emitted)
                + " tipMsg="
                + (tip == null || tip.Message == null ? string.Empty : tip.Message));
        }

        private static bool HasRcPDevice(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       RcPAudioRecordingDeviceItemId);
        }

        private static bool TryGrantRcPDevice(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (HasRcPDevice(source))
            {
                return true;
            }

            EnsureRcPTemplateAllowsGrant();
            if (!GrantSingleRewardItem(source, RcPAudioRecordingDeviceItemId))
            {
                return false;
            }

            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                "Mission:5514B19D",
                RcPGrantFlag,
                "item:" + RcPAudioRecordingDeviceItemId);
            Log("granted RC-P " + RcPAudioRecordingDeviceItemId + " character=" + source.Identity.ToString(true));
            return true;
        }

        private static void EnsureRcPTemplateAllowsGrant()
        {
            ItemTemplate template;
            if (!ItemLoader.ItemList.TryGetValue(RcPAudioRecordingDeviceItemId, out template)
                || template == null
                || template.Stats == null
                || !template.Stats.ContainsKey(0))
            {
                return;
            }

            int flags = template.Stats[0];
            if ((flags & UniqueItemFlagBit) != 0)
            {
                template.Stats[0] = flags & ~UniqueItemFlagBit;
                Log("cleared Unique flag on template " + RcPAudioRecordingDeviceItemId + " for quest handout");
            }
        }

        private static bool GrantSingleRewardItem(ICharacter source, int itemId)
        {
            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null)
            {
                Log("grant skipped item=" + itemId + " reason=no-inventory-or-client");
                return false;
            }

            if (!ItemLoader.ItemList.ContainsKey(itemId))
            {
                Log("grant skipped item=" + itemId + " reason=missing-ItemLoader-template");
                return false;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
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
                Log("grant create failed item=" + itemId + " err=" + ex.Message);
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log(
                    "grant failed item="
                    + itemId
                    + " status="
                    + grant.Status
                    + " invErr="
                    + grant.InventoryError
                    + " ex="
                    + grant.ExceptionMessage);
                return false;
            }

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

        private static void ApplyBillTurnInXpCredits(ICharacter source)
        {
            MissionRewardDefinition definition = new MissionRewardDefinition
                                                {
                                                    RewardKey = "captured-bill-hc12-turnin-xp-credits",
                                                    RewardType = "character-stats",
                                                    IsResolved = true,
                                                    StatMutations =
                                                        new[]
                                                        {
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.cash,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = BillTurnInCreditReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.xp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = BillTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.unsavedxp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = BillTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.lastxp,
                                                                Kind = MissionStatMutationKind.Set,
                                                                Value = BillTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            }
                                                        }
                                                };
            MissionRewardExecutionResult result = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                "Mission:5514B19F",
                definition,
                "capture:20260720-105157:bill-turnin-xp-credits");
            if (!result.Succeeded || result.StatValues == null)
            {
                return;
            }

            foreach (MissionCharacterStatValue statValue in result.StatValues)
            {
                uint value = statValue.Value <= 0
                                 ? 0
                                 : (uint)Math.Min(statValue.Value, uint.MaxValue);
                source.Stats[(StatIds)statValue.StatId].Set(value);
            }

            StatMessageHandler.Default.SendChanged(source);
        }

        private static void SendUplinkFeedback(ICharacter source)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return;
            }

            source.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = source.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = UplinkFeedback,
                    Unknown2 = 0
                });
        }

        private static void TrySendBillTurnInRewardFeedback(ICharacter source)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return;
            }

            source.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = source.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = BillTurnInRewardFeedback,
                    Unknown2 = 0
                });
        }

        private static void SendDroidBeep(ICharacter source)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return;
            }

            source.Controller.Client.SendCompressed(
                new ChatTextMessage
                {
                    Identity = source.Identity,
                    Text = DroidBeepChat
                });
        }

        /// <summary>
        /// Login / tip journal sync for the Flint→Bill→Alex chain.
        /// Clears prior tips (Action59 Int16 + Quest/Delete) when progress has moved on, then
        /// re-emits only the current tip. Fixes stuck "Surveillance Uplink" Remain 00:00.
        /// </summary>
        public static bool TrySyncTipsForLogin(ICharacter source)
        {
            if (source == null
                || source.Controller == null
                || source.Controller.Client == null
                || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            bool pastUplink = HasReachedOrPassed(characterId, "Mission:5514B19E")
                              || HasReachedOrPassed(characterId, "Mission:5514B19F")
                              || HasReachedOrPassed(characterId, "Mission:5514B1A0")
                              || HasReachedOrPassed(characterId, "Mission:555B4365")
                              || HasReachedOrPassed(characterId, "Mission:555B4366")
                              || HasReachedOrPassed(characterId, "Mission:555B4367");
            bool pastPlant = HasReachedOrPassed(characterId, "Mission:5514B19F")
                             || HasReachedOrPassed(characterId, "Mission:5514B1A0")
                             || HasReachedOrPassed(characterId, "Mission:555B4365")
                             || HasReachedOrPassed(characterId, "Mission:555B4366")
                             || HasReachedOrPassed(characterId, "Mission:555B4367");
            bool pastDeliver = HasReachedOrPassed(characterId, "Mission:5514B1A0")
                               || HasReachedOrPassed(characterId, "Mission:555B4365")
                               || HasReachedOrPassed(characterId, "Mission:555B4366")
                               || HasReachedOrPassed(characterId, "Mission:555B4367");
            bool pastKneecapping = HasReachedOrPassed(characterId, "Mission:555B4365")
                                   || HasReachedOrPassed(characterId, "Mission:555B4366")
                                   || HasReachedOrPassed(characterId, "Mission:555B4367");
            bool pastReport = HasReachedOrPassed(characterId, "Mission:555B4366")
                              || HasReachedOrPassed(characterId, "Mission:555B4367");

            if (pastUplink)
            {
                ClearTipPair(source, unchecked((int)0x555A4A49), unchecked((int)0x5514B19D));
                MissionRuntime.Service.CompleteMission(characterId, "Mission:5514B19D");
            }

            if (pastPlant)
            {
                ClearTipPair(source, unchecked((int)0x555A4E3B), unchecked((int)0x5514B19E));
                MissionRuntime.Service.CompleteMission(characterId, "Mission:5514B19E");
            }

            if (pastDeliver)
            {
                ClearTipPair(source, unchecked((int)0x555A4E3C), unchecked((int)0x5514B19F));
                MissionRuntime.Service.CompleteMission(characterId, "Mission:5514B19F");
            }

            if (pastKneecapping)
            {
                ClearTipPair(source, unchecked((int)0x555A4E3D), unchecked((int)0x5514B1A0));
                MissionRuntime.Service.CompleteMission(characterId, "Mission:5514B1A0");
            }

            if (pastReport)
            {
                ClearTipPair(source, unchecked((int)0x555B4365), unchecked((int)0x555B4365));
                MissionRuntime.Service.CompleteMission(characterId, "Mission:555B4365");
            }

            // Current tip only (highest progress first).
            if (VernonGodfrayQuestRuntime.TrySyncTipsForLogin(source))
            {
                return true;
            }

            if (DoctorMasonQuestRuntime.TrySyncTipsForLogin(source))
            {
                return true;
            }

            if (LoreleiQuestRuntime.TrySyncTipsForLogin(source))
            {
                return true;
            }

            if (SarahGreeneQuestRuntime.TrySyncTipsForLogin(source))
            {
                return true;
            }

            if (StanGoodmanQuestRuntime.TrySyncTipsForLogin(source))
            {
                return true;
            }

            if (IsMissionActiveOrOffered(characterId, "Mission:555B4367"))
            {
                RexQuestPreviewEmissionResult tradeskill =
                    SafeQuestFullUpdateSender.TrySendTradeskillNanoSensorTip(source);
                return tradeskill != null && tradeskill.Emitted;
            }

            if (IsMissionActiveOrOffered(characterId, "Mission:555B4366"))
            {
                RexQuestPreviewEmissionResult stan =
                    SafeQuestFullUpdateSender.TrySendReportAlexToTalkStanHandoff(source);
                return stan != null && stan.Emitted;
            }

            if (IsMissionActiveOrOffered(characterId, "Mission:555B4365"))
            {
                RexQuestPreviewEmissionResult report =
                    SafeQuestFullUpdateSender.TrySendKneecappingToReportAlexHandoff(source);
                return report != null && report.Emitted;
            }

            if (IsMissionActiveOrOffered(characterId, "Mission:5514B1A0"))
            {
                RexQuestPreviewEmissionResult kneecap =
                    SafeQuestFullUpdateSender.TrySendDeliverBillToKneecappingHandoff(source);
                return kneecap != null && kneecap.Emitted;
            }

            if (IsMissionActiveOrOffered(characterId, "Mission:5514B19F"))
            {
                RexQuestPreviewEmissionResult deliverBill =
                    SafeQuestFullUpdateSender.TrySendPlantBugToDeliverBillHandoff(source);
                return deliverBill != null && deliverBill.Emitted;
            }

            if (IsMissionActiveOrOffered(characterId, "Mission:5514B19E"))
            {
                RexQuestPreviewEmissionResult plant =
                    SafeQuestFullUpdateSender.TrySendUplinkToPlantBugHandoff(source);
                return plant != null && plant.Emitted;
            }

            if (IsMissionActiveOrOffered(characterId, "Mission:5514B19D"))
            {
                ReanchorGameTimeForTipJournal(source);
                RexQuestPreviewEmissionResult uplink =
                    SafeQuestFullUpdateSender.TrySendSurveillanceUplinkPreview(source);
                return uplink != null && uplink.Emitted;
            }

            // Even with no Active mission, wipe a journal ghost if DB already completed Uplink+.
            if (IsMissionCompleted(characterId, "Mission:5514B19D")
                || IsMissionCompleted(characterId, "Mission:5514B19E")
                || IsMissionCompleted(characterId, "Mission:5514B19F")
                || IsMissionCompleted(characterId, "Mission:5514B1A0"))
            {
                ClearTipPair(source, unchecked((int)0x555A4A49), unchecked((int)0x5514B19D));
            }

            return false;
        }

        private static void ClearTipPair(ICharacter source, int wireInstance, int legacyInstance)
        {
            FlintKneecappingTipWire.TryDeleteTip(source, wireInstance);
            if (legacyInstance != 0 && legacyInstance != wireInstance)
            {
                FlintKneecappingTipWire.TryDeleteTip(source, legacyInstance);
            }
        }

        private static void ReanchorGameTimeForTipJournal(ICharacter source)
        {
            ZoneClient client = source != null && source.Controller != null
                                    ? source.Controller.Client as ZoneClient
                                    : null;
            if (client == null || source == null)
            {
                return;
            }

            client.SendCompressed(
                new GameTimeMessage
                {
                    Identity =
                        new Identity
                        {
                            Type = IdentityType.CanbeAffected,
                            Instance = source.Identity.Instance
                        },
                    Unknown1 = 30024.0f,
                    Unknown3 = 185408,
                    Unknown4 = 80183.3125f
                });
            client.LastGameTimeSyncUtc = DateTime.UtcNow;
        }

        private static bool HasReachedOrPassed(int characterId, string questId)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, questId);
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered
                       || mission.State == MissionLifecycleState.Completed);
        }

        private static bool IsMissionActiveOrOffered(int characterId, string questId)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, questId);
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static bool IsMissionCompleted(int characterId, string questId)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, questId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        private static bool IsSurveillanceUplinkActive(ICharacter source)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, "Mission:5514B19D");
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static void EnsureSurveillanceUplinkActive(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized || IsSurveillanceUplinkActive(source))
            {
                return;
            }

            int instance = source.Identity.Instance;
            if (IsMissionCompleted(instance, "Mission:5514B19D"))
            {
                return;
            }

            MissionRuntime.Service.OfferMission(instance, "Mission:5514B19D");
            MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19D");
            Log("recovered Surveillance Uplink active character=" + source.Identity.ToString(true));
        }

        private static bool IsPlantBugActive(ICharacter source)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, "Mission:5514B19E");
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static bool IsBillDeliverActive(ICharacter source)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, "Mission:5514B19F");
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static void EnsureBillDeliverAvailable(ICharacter source)
        {
            if (source == null
                || !MissionRuntime.IsInitialized
                || IsBillDeliverActive(source)
                || IsBillDeliverCompleted(source))
            {
                return;
            }

            MissionRuntime.Service.OfferMission(source.Identity.Instance, "Mission:5514B19F");
            MissionRuntime.Service.AcceptMission(source.Identity.Instance, "Mission:5514B19F");
            Log("bill-deliver-ensured-active character=" + source.Identity.ToString(true));
        }

        private static bool IsBillDeliverCompleted(ICharacter source)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, "Mission:5514B19F");
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        private static bool IsKneecappingActive(ICharacter source)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, "Mission:5514B1A0");
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static bool HasSecTecMonitor(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       RebuiltHc12SecTecMonitorItemId);
        }

        private static bool IsPrizedHouseplantTarget(ICharacter source, Identity target)
        {
            if (target.Type != IdentityType.Terminal || source == null || source.Playfield == null)
            {
                return false;
            }

            if (target.Instance == PrizedHouseplantInstance)
            {
                return true;
            }

            StaticDynel plant = Pool.Instance.GetObject<StaticDynel>(source.Playfield.Identity, target);
            if (plant == null)
            {
                return false;
            }

            if (plant.Template != null && plant.Template.ID == PrizedHouseplantTemplateId)
            {
                return true;
            }

            int templateId;
            if (plant.Stats != null
                && (plant.Stats.TryGetValue((int)StatIds.acgitemtemplateid, out templateId)
                    || plant.Stats.TryGetValue((int)StatIds.staticinstance, out templateId)))
            {
                return templateId == PrizedHouseplantTemplateId;
            }

            return false;
        }

        private static ICharacter ResolveTargetedSurveillanceDroid(ICharacter source)
        {
            Identity selected = source.SelectedTarget;
            if (selected.Type == IdentityType.None)
            {
                selected = source.FightingTarget;
            }

            if (selected.Type != IdentityType.CanbeAffected || selected.Instance == 0)
            {
                return null;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, selected);
            if (!IsCaptureSurveillanceDroid(npc))
            {
                return null;
            }

            return npc;
        }

        private static bool IsSelectedTarget(ICharacter source, Identity expected)
        {
            if (source == null || expected.Type == IdentityType.None)
            {
                return false;
            }

            Identity selected = source.SelectedTarget;
            if (selected.Type == IdentityType.None)
            {
                selected = source.FightingTarget;
            }

            return selected.Type == expected.Type && selected.Instance == expected.Instance;
        }

        private static bool IsCaptureSurveillanceDroid(ICharacter npc)
        {
            return npc != null
                   && npc.Stats[StatIds.health].Value > 0
                   && IsSurveillanceDroidName(npc.Name)
                   && npc.Stats[StatIds.monsterdata].Value == 210238;
        }

        private static bool IsBillNpc(ICharacter source, Identity target)
        {
            if (target.Type == IdentityType.CanbeAffected && target.Instance == BillInstance)
            {
                return true;
            }

            if (source == null || source.Playfield == null || target.Instance == 0)
            {
                return false;
            }

            ICharacter named = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, target);
            if (named != null
                && string.Equals(named.Name, "ICC Immigration Officer Bill", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(source.Playfield.Identity))
            {
                if (candidate != null
                    && candidate.Identity.Instance == target.Instance
                    && string.Equals(
                        candidate.Name,
                        "ICC Immigration Officer Bill",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSurveillanceDroidName(string name)
        {
            return string.Equals(name, "Surveillance Droid", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSecTecMonitor(IItem item)
        {
            return item != null
                   && (item.LowID == RebuiltHc12SecTecMonitorItemId
                       || item.HighID == RebuiltHc12SecTecMonitorItemId);
        }

        private static bool IsRcPDevice(IItem item)
        {
            return item != null
                   && (item.LowID == RcPAudioRecordingDeviceItemId
                       || item.HighID == RcPAudioRecordingDeviceItemId);
        }

        private static IItem ResolveInventoryItem(ICharacter character, Identity itemIdentity)
        {
            if (character == null || itemIdentity.Type != IdentityType.Inventory)
            {
                return null;
            }

            if (character.BaseInventory == null)
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

        private static bool TryConsumeInventoryItem(ICharacter source, Identity stagedContainer, int itemId)
        {
            if (source == null || source.BaseInventory == null || itemId <= 0)
            {
                return false;
            }

            if (stagedContainer.Type != IdentityType.None && stagedContainer.Instance > 0)
            {
                IInventoryPage stagedPage;
                if (source.BaseInventory.Pages.TryGetValue((int)stagedContainer.Type, out stagedPage)
                    && stagedPage != null)
                {
                    IItem staged = stagedPage[stagedContainer.Instance];
                    if (staged != null
                        && (staged.LowID == itemId || staged.HighID == itemId)
                        && TryRemoveInventorySlot(
                            source,
                            stagedPage,
                            (int)stagedContainer.Type,
                            stagedContainer.Instance,
                            staged))
                    {
                        return true;
                    }
                }
            }

            int[] preferredPages = { (int)IdentityType.Inventory, (int)IdentityType.OverflowWindow };
            for (int i = 0; i < preferredPages.Length; i++)
            {
                IInventoryPage page;
                if (source.BaseInventory.Pages.TryGetValue(preferredPages[i], out page)
                    && page != null
                    && TryConsumeFromPage(source, preferredPages[i], page, itemId))
                {
                    return true;
                }
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in source.BaseInventory.Pages)
            {
                if (pageEntry.Key == (int)IdentityType.Inventory
                    || pageEntry.Key == (int)IdentityType.OverflowWindow)
                {
                    continue;
                }

                if (TryConsumeFromPage(source, pageEntry.Key, pageEntry.Value, itemId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryConsumeFromPage(ICharacter source, int pageType, IInventoryPage page, int itemId)
        {
            if (page == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IItem> slot in page.List())
            {
                IItem item = slot.Value;
                if (item != null
                    && (item.LowID == itemId || item.HighID == itemId)
                    && TryRemoveInventorySlot(source, page, pageType, slot.Key, item))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryRemoveInventorySlot(
            ICharacter source,
            IInventoryPage page,
            int pageType,
            int slot,
            IItem item)
        {
            page.Remove(slot);
            try
            {
                if (source.BaseInventory.Write())
                {
                    CharacterActionMessageHandler.Default.SendDeleteItem(source, pageType, slot);
                    Log(
                        "bill-consumed item slot page="
                        + pageType.ToString("X")
                        + " slot="
                        + slot
                        + " character="
                        + source.Identity.ToString(true));
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log("bill-consume write failed: " + ex.Message);
            }

            page.Add(slot, item);
            return false;
        }

        private static bool IsValidPlayerInArete(ICharacter source)
        {
            return source != null
                   && source.Controller is PlayerController
                   && source.Playfield != null
                   && source.Playfield.Identity.Instance == AreteLandingPlayfieldId;
        }

        private static void BeginBillTrade(ICharacter source, Identity billIdentity)
        {
            EnsureBillTradeSession(source, billIdentity);
        }

        private static void EnsureBillTradeSession(ICharacter source, Identity billIdentity)
        {
            lock (TradeSyncRoot)
            {
                BillTradeSession existing;
                if (TradeSessionsByCharacter.TryGetValue(source.Identity.Instance, out existing)
                    && existing != null)
                {
                    existing.NpcIdentity = billIdentity;
                    return;
                }

                TradeSessionsByCharacter[source.Identity.Instance] = new BillTradeSession
                                                                     {
                                                                         NpcIdentity = billIdentity
                                                                     };
            }
        }

        private static BillTradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (TradeSyncRoot)
            {
                BillTradeSession session;
                TradeSessionsByCharacter.TryGetValue(source.Identity.Instance, out session);
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
                TradeSessionsByCharacter.Remove(source.Identity.Instance);
            }
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Error, "ARETE_SURVEILLANCE_UPLINK " + message);
        }
    }
}
