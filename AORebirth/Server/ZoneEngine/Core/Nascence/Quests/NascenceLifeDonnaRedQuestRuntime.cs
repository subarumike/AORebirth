namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Capture-backed Scientist Donna Red Ancient Device quest (Mission:55ABAD4D, 20260822-224319).
    /// </summary>
    internal static class NascenceLifeDonnaRedQuestRuntime
    {
        private const int OverflowRewardSlot = 0x6F;

        internal static bool IsMissionActive(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(
                source.Identity.Instance,
                NascenceLifeDonnaRedInteractionRules.QuestId);
            return mission != null && mission.State == MissionLifecycleState.Active;
        }

        internal static bool IsMissionCompleted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(
                source.Identity.Instance,
                NascenceLifeDonnaRedInteractionRules.QuestId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        internal static string ResolveStartNodeId(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            // After accept, reopen on lore hub (not the one-shot grant narrative).
            if (IsMissionActive(source) || IsDeviceGranted(source) || IsMissionCompleted(source))
            {
                return "donna_hub";
            }

            return null;
        }

        /// <summary>
        /// Capture grant can miss client UI if TemplateAction/ContainerAdd were incomplete; retry on talk.
        /// </summary>
        internal static bool TryGrantAncientDeviceOnDialogueOpen(ICharacter source)
        {
            return TryGrantAncientDeviceIfNeeded(source);
        }

        internal static MissionOperationResult AcceptQuest(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "nascence-life-donna-runtime-unavailable"
                       };
            }

            string questId = NascenceLifeDonnaRedInteractionRules.QuestId;
            int characterId = source.Identity.Instance;

            if (IsMissionActive(source))
            {
                NascenceLifeDonnaRedPacketSender.TrySendQuestFullUpdate(source);
                TryGrantAncientDeviceIfNeeded(source);
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "nascence-life-donna-already-active"
                       };
            }

            if (IsMissionCompleted(source))
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "nascence-life-donna-already-completed"
                       };
            }

            MissionOperationResult offer = MissionRuntime.Service.OfferMission(characterId, questId);
            if (!IsClientEmitSuccess(offer) && IsPersistenceFailure(offer))
            {
                return offer;
            }

            MissionOperationResult accepted = MissionRuntime.Service.AcceptMission(characterId, questId);
            if (IsClientEmitSuccess(accepted))
            {
                NascenceLifeDonnaRedPacketSender.TrySendQuestFullUpdate(source);
                TryGrantAncientDeviceIfNeeded(source);
            }

            return accepted;
        }

        internal static bool TryResendActiveMissionsForLogin(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (NascenceAbanFalaQuestRuntime.HasAbanChainProgressed(source))
            {
                NascenceAbanFalaQuestRuntime.TryRemoveDonnaQuestFromClient(source);
                return false;
            }

            if (!IsMissionActive(source))
            {
                return false;
            }

            return NascenceLifeDonnaRedPacketSender.TrySendQuestFullUpdate(source);
        }

        private static bool TryGrantAncientDeviceIfNeeded(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            // Already issued once (including after Fala turn-in) — do not re-grant on talk/login/zone.
            if (IsDeviceGranted(source))
            {
                return true;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    NascenceLifeDonnaRedInteractionRules.AncientDeviceItemId))
            {
                MarkDeviceGranted(source);
                return true;
            }

            if (!IsMissionActive(source))
            {
                return false;
            }

            if (!TryGrantAncientDeviceItem(source))
            {
                return false;
            }

            MarkDeviceGranted(source);
            return true;
        }

        private static bool TryGrantAncientDeviceItem(ICharacter source)
        {
            int itemId = NascenceLifeDonnaRedInteractionRules.AncientDeviceItemId;
            if (source == null
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(itemId))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_DONNA_RED device grant blocked itemId="
                    + itemId.ToString(CultureInfo.InvariantCulture)
                    + " inItemList="
                    + (ItemLoader.ItemList != null && ItemLoader.ItemList.ContainsKey(itemId)));
                return false;
            }

            Item item;
            try
            {
                item = new Item(1, itemId, itemId);
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_DONNA_RED device Item ctor failed: " + exception.Message);
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_DONNA_RED device inventory grant failed status="
                    + grant.Status
                    + " char=" + source.Identity.ToString(true));
                return false;
            }

            // Capture order: TemplateAction then ContainerAddItem (both required for client inventory).
            if (!NascenceLifeDonnaRedPacketSender.TrySendAncientDeviceGrant(source))
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
                        Target =
                            new Identity
                            {
                                Type = IdentityType.OverflowWindow,
                                Instance = source.Identity.Instance
                            },
                        TargetPlacement = OverflowRewardSlot
                    });
            }

            return true;
        }

        private static bool IsDeviceGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       NascenceLifeDonnaRedInteractionRules.QuestId,
                       NascenceLifeDonnaRedInteractionRules.DeviceGrantedFlag) != null;
        }

        private static void MarkDeviceGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                NascenceLifeDonnaRedInteractionRules.QuestId,
                NascenceLifeDonnaRedInteractionRules.DeviceGrantedFlag,
                "item:" + NascenceLifeDonnaRedInteractionRules.AncientDeviceItemId
                    .ToString(CultureInfo.InvariantCulture));
        }

        private static bool IsClientEmitSuccess(MissionOperationResult result)
        {
            return result != null
                   && (result.Status == MissionOperationStatus.Applied
                       || result.Status == MissionOperationStatus.AlreadyApplied);
        }

        private static bool IsPersistenceFailure(MissionOperationResult result)
        {
            return result != null
                   && result.Status != MissionOperationStatus.Applied
                   && result.Status != MissionOperationStatus.AlreadyApplied
                   && result.Status != MissionOperationStatus.Unresolved;
        }
    }
}
