namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Arete.Dialogue;

    #endregion

    /// <summary>
    /// Capture-backed Ecclesiast Aban Fala quest runtime (20260822-224319).
    /// </summary>
    internal static class NascenceAbanFalaQuestRuntime
    {
        internal static bool IsMissionActive(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Active;
        }

        internal static bool HasDeviceInspected(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       NascenceAbanFalaInteractionRules.QuestDeviceInfo,
                       NascenceAbanFalaInteractionRules.DeviceInspectedFlag) != null
                   || MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       NascenceAbanFalaInteractionRules.QuestInsigniaTask,
                       NascenceAbanFalaInteractionRules.DeviceInspectedFlag) != null;
        }

        internal static void MarkDeviceInspected(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                NascenceAbanFalaInteractionRules.QuestDeviceInfo,
                NascenceAbanFalaInteractionRules.DeviceInspectedFlag,
                "1");
            TrySyncClientJournal(source);
        }

        internal static string ResolveStartNodeId(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return null;
            }

            if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden)
                || IsSoulsQuestActive(source))
            {
                return null;
            }

            if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestInsigniaTask)
                || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestDeviceInfo))
            {
                return NascenceAbanFalaInteractionRules.ReopenHubNodeId;
            }

            if (NascenceLifeDonnaRedQuestRuntime.IsMissionActive(source))
            {
                return NascenceAbanFalaInteractionRules.JourneyNodeId;
            }

            return null;
        }

        /// <summary>
        /// Capture 20260822-224319: Fala only speaks during Donna device quest or insignia/device sub-quests.
        /// </summary>
        internal static bool CanTalkToFala(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden)
                || IsSoulsQuestActive(source))
            {
                return false;
            }

            return NascenceLifeDonnaRedQuestRuntime.IsMissionActive(source)
                   || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestInsigniaTask)
                   || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestDeviceInfo);
        }

        /// <summary>
        /// Capture 20260822-224319: Lux-Wei only during garden passage quest or souls phase.
        /// </summary>
        internal static bool CanTalkToLuxWei(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden)
                   || IsSoulsQuestActive(source);
        }

        internal static void TrySyncClientJournalAfterTrade(ICharacter source, DialogueSession session)
        {
            if (source == null || !MissionRuntime.IsInitialized || session == null)
            {
                return;
            }

            string nodeId = session.CurrentNodeId;
            if (string.Equals(nodeId, "fala_004", StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    nodeId,
                    NascenceAbanFalaInteractionRules.DeviceTradeHoldNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestDeviceInfo))
                {
                    NascenceAbanFalaPacketSender.TrySendDeviceInfoQuestFullUpdate(source);
                }

                if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestInsigniaTask))
                {
                    NascenceAbanFalaPacketSender.TrySendInsigniaTaskQuestFullUpdate(source);
                }
            }

            if (string.Equals(nodeId, NascenceAbanFalaInteractionRules.InsigniaTurnInNodeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    nodeId,
                    NascenceAbanFalaInteractionRules.InsigniaTradeHoldNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden))
                {
                    NascenceAbanFalaPacketSender.TrySendGardenQuestFullUpdate(source);
                }
            }

            if (string.Equals(
                    nodeId,
                    NascenceAbanFalaInteractionRules.LuxWeiActivationNodeId,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    nodeId,
                    NascenceAbanFalaInteractionRules.LuxWeiDeviceTradeHoldNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden))
                {
                    NascenceAbanFalaPacketSender.TrySendGardenQuestFullUpdate(source);
                }
            }

            if (string.Equals(
                    nodeId,
                    NascenceAbanFalaInteractionRules.LuxWeiFarewellNodeId,
                    StringComparison.OrdinalIgnoreCase)
                && IsSoulsQuestActive(source))
            {
                NascenceAbanFalaPacketSender.TrySendSoulsQuestFullUpdate(source);
            }
        }

        internal static void AcceptRedemptionQuests(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden))
            {
                return;
            }

            int characterId = source.Identity.Instance;

            AcceptOne(source, characterId, NascenceAbanFalaInteractionRules.QuestInsigniaTask);
            AcceptOne(source, characterId, NascenceAbanFalaInteractionRules.QuestDeviceInfo);

            // Capture 20260822-224319: QFU 052, delete 4D, QFU 053 — always sync client journal.
            NascenceAbanFalaPacketSender.TrySendInsigniaTaskQuestFullUpdate(source);
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestDonnaDevice);
            NascenceAbanFalaPacketSender.TrySendDeviceInfoQuestFullUpdate(source);
            TrySyncClientJournal(source);
        }

        internal static void CompleteInsigniaTurnIn(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;

            // Capture 20260822-224319 order: delete 053, add 054, delete 052.
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestDeviceInfo);

            AcceptOne(source, characterId, NascenceAbanFalaInteractionRules.QuestGarden);
            NascenceAbanFalaPacketSender.TrySendGardenQuestFullUpdate(source);

            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestInsigniaTask);
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestDonnaDevice);
            TryRestoreInsigniaIfMissing(source);
            TrySyncClientJournal(source);
        }

        /// <summary>
        /// Donna device quest is superseded once Fala insignia/device sub-quests exist.
        /// </summary>
        internal static string ResolveDeviceTradePostNodeId(ICharacter source, string dialogueSourceNodeId = null)
        {
            if (string.Equals(
                    dialogueSourceNodeId,
                    NascenceAbanFalaInteractionRules.ArtifactOfferNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "fala_004";
            }

            if (string.Equals(
                    dialogueSourceNodeId,
                    NascenceAbanFalaInteractionRules.JourneyNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return NascenceAbanFalaInteractionRules.RedemptionNodeId;
            }

            if (source != null
                && (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestInsigniaTask)
                    || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestDeviceInfo)))
            {
                return "fala_004";
            }

            return NascenceAbanFalaInteractionRules.RedemptionNodeId;
        }

        internal static bool HasAbanChainProgressed(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestInsigniaTask)
                   || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestDeviceInfo)
                   || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden)
                   || IsSoulsQuestActive(source)
                   || HasDeviceInspected(source)
                   || NascenceLifeDonnaRedQuestRuntime.IsMissionCompleted(source);
        }

        internal static void TryRemoveDonnaQuestFromClient(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            TryRemoveClientQuest(source, NascenceAbanFalaInteractionRules.QuestDonnaDevice);
        }

        /// <summary>
        /// Re-emit capture QuestFullUpdate packets from current server mission state (zone/relog/dialogue/trade).
        /// </summary>
        internal static bool TrySyncClientJournal(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (IsAbanChainFinishedForClient(source))
            {
                ClearStaleAbanQuestsFromClient(source);
                return true;
            }

            PurgeSupersededClientQuests(source);

            if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSouls)
                || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSoulsOne)
                || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSoulsTwo)
                || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSoulsReturn))
            {
                int souls = GetSoulCount(source);
                if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSoulsReturn))
                {
                    return NascenceAbanFalaPacketSender.TrySendSoulsReturnQuestFullUpdate(source);
                }

                return NascenceAbanFalaPacketSender.TrySendSoulsQuestFullUpdate(source, souls);
            }

            if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden))
            {
                SyncGardenPhaseClientJournal(source);
                return true;
            }

            bool sent = false;
            if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestInsigniaTask))
            {
                sent |= NascenceAbanFalaPacketSender.TrySendInsigniaTaskQuestFullUpdate(source);
            }

            if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestDeviceInfo))
            {
                sent |= NascenceAbanFalaPacketSender.TrySendDeviceInfoQuestFullUpdate(source);
            }

            return sent;
        }

        internal static bool TryResendActiveMissionsForLogin(ICharacter source)
        {
            return TrySyncClientJournal(source);
        }

        internal static bool IsSoulsQuestActive(ICharacter source)
        {
            return IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSouls)
                   || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSoulsOne)
                   || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSoulsTwo)
                   || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSoulsReturn);
        }

        internal static string ResolveLuxWeiStartNodeId(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized || !CanTalkToLuxWei(source))
            {
                return null;
            }

            if (IsSoulsQuestActive(source))
            {
                if (IsLuxWeiKeyReturnReady(source))
                {
                    return NascenceAbanFalaInteractionRules.LuxWeiHubNodeId;
                }

                return NascenceAbanFalaInteractionRules.LuxWeiSoulsInProgressNodeId;
            }

            if (HasLuxWeiDeviceShown(source))
            {
                return NascenceAbanFalaInteractionRules.LuxWeiActivationNodeId;
            }

            return NascenceAbanFalaInteractionRules.LuxWeiRootNodeId;
        }

        internal static void MarkLuxWeiDeviceShown(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                NascenceAbanFalaInteractionRules.QuestGarden,
                NascenceAbanFalaInteractionRules.LuxWeiDeviceShownFlag,
                "1");
        }

        internal static bool HasLuxWeiDeviceShown(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       NascenceAbanFalaInteractionRules.QuestGarden,
                       NascenceAbanFalaInteractionRules.LuxWeiDeviceShownFlag) != null;
        }

        internal static int GetSoulCount(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return 0;
            }

            MissionFlagRecord flag = MissionRuntime.Service.GetFlag(
                source.Identity.Instance,
                NascenceAbanFalaInteractionRules.QuestSouls,
                NascenceAbanFalaInteractionRules.SoulCountFlag);
            if (flag == null || string.IsNullOrWhiteSpace(flag.Value))
            {
                return 0;
            }

            int count;
            return int.TryParse(flag.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
                       ? count
                       : 0;
        }

        internal static bool IsLuxWeiKeyReturnReady(ICharacter source)
        {
            return GetSoulCount(source) >= 3
                   || IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSoulsReturn);
        }

        internal static bool HasAbanGardenKey(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    NascenceAbanFalaInteractionRules.GardenKeyItemId))
            {
                return true;
            }

            AORebirth.Core.Inventory.IInventoryPage weaponPage;
            if (source.BaseInventory == null
                || !source.BaseInventory.Pages.TryGetValue(
                       (int)IdentityType.WeaponPage,
                       out weaponPage)
                || weaponPage == null)
            {
                return false;
            }

            for (int slot = weaponPage.FirstSlotNumber;
                 slot < weaponPage.FirstSlotNumber + weaponPage.MaxSlots;
                 slot++)
            {
                AORebirth.Core.Items.IItem item = weaponPage[slot];
                if (item != null
                    && NascenceAbanFalaInteractionRules.IsGardenKeyItem(item.LowID))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Capture 20260822-224319 @21:01:03: TemplateAction garden key (226824) then favored analyzer.
        /// </summary>
        internal static bool TryGrantAbanGardenKey(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (HasAbanGardenKey(source))
            {
                int characterId = source.Identity.Instance;
                MissionRuntime.Service.SetFlag(
                    characterId,
                    NascenceAbanFalaInteractionRules.QuestSoulsReturn,
                    NascenceAbanFalaInteractionRules.LuxWeiKeyGrantedFlag,
                    "item:" + NascenceAbanFalaInteractionRules.GardenKeyItemId);
                return true;
            }

            if (TryGrantQuestItem(
                    source,
                    NascenceAbanFalaInteractionRules.QuestSoulsReturn,
                    NascenceAbanFalaInteractionRules.GardenKeyItemId,
                    NascenceAbanFalaInteractionRules.LuxWeiKeyGrantedFlag))
            {
                return true;
            }

            if (TryGrantQuestItem(
                    source,
                    NascenceAbanFalaInteractionRules.QuestSouls,
                    NascenceAbanFalaInteractionRules.GardenKeyItemId,
                    NascenceAbanFalaInteractionRules.LuxWeiKeyGrantedFlag))
            {
                return true;
            }

            return TryGrantQuestItem(
                source,
                NascenceAbanFalaInteractionRules.QuestGarden,
                NascenceAbanFalaInteractionRules.GardenKeyItemId,
                NascenceAbanFalaInteractionRules.LuxWeiKeyGrantedFlag);
        }

        internal static bool TryRestoreAbanGardenKeyIfMissing(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (HasAbanGardenKey(source))
            {
                return true;
            }

            int characterId = source.Identity.Instance;
            if (MissionRuntime.Service.GetFlag(
                    characterId,
                    NascenceAbanFalaInteractionRules.QuestSoulsReturn,
                    NascenceAbanFalaInteractionRules.LuxWeiKeyGrantedFlag) == null
                && MissionRuntime.Service.GetFlag(
                       characterId,
                       NascenceAbanFalaInteractionRules.QuestSouls,
                       NascenceAbanFalaInteractionRules.LuxWeiKeyGrantedFlag) == null)
            {
                return false;
            }

            return TryRestoreItem(source, NascenceAbanFalaInteractionRules.GardenKeyItemId, 1);
        }

        internal static void TryEnsureAncientDevicePresent(ICharacter source, int itemId, int quality)
        {
            if (source == null || itemId != NascenceAbanFalaInteractionRules.AncientDeviceItemId)
            {
                return;
            }

            TryRestoreAncientDeviceIfMissing(source);
        }

        /// <summary>
        /// Capture inspect trades: RejectedItems Unknown2=1 returns device; repair only if still missing.
        /// </summary>
        internal static bool TryRestoreAncientDeviceIfMissing(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (HasFavoredAnalyzer(source) || HasInspectedAnalyzer(source))
            {
                return true;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    NascenceAbanFalaInteractionRules.AncientDeviceItemId))
            {
                return true;
            }

            return TryRestoreItem(source, NascenceAbanFalaInteractionRules.AncientDeviceItemId, 1);
        }

        /// <summary>
        /// Capture Fala insignia turn-in: RejectedItems Unknown2=1 returns insignia for garden statue + later combine.
        /// </summary>
        internal static bool TryRestoreInsigniaIfMissing(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    NascenceAbanFalaInteractionRules.InsigniaOfAbanItemId))
            {
                return true;
            }

            if (!IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden)
                && !IsSoulsQuestActive(source))
            {
                return false;
            }

            return TryGrantQuestItem(
                source,
                NascenceAbanFalaInteractionRules.QuestGarden,
                NascenceAbanFalaInteractionRules.InsigniaOfAbanItemId,
                NascenceAbanFalaInteractionRules.LuxWeiActivationGrantsFlag + "-214788-restore");
        }

        internal static void CompleteLuxWeiActivation(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;

            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestGarden);

            AcceptOne(source, characterId, NascenceAbanFalaInteractionRules.QuestSouls);
            NascenceAbanFalaPacketSender.TrySendSoulsQuestFullUpdate(source);

            // Player keeps the inspected Ancient Device and combines it with Insignia via tradeskill.
            // Only re-grant insignia if they spent it entering the garden.
            TryGrantQuestItem(
                source,
                NascenceAbanFalaInteractionRules.QuestSouls,
                NascenceAbanFalaInteractionRules.InsigniaOfAbanItemId,
                NascenceAbanFalaInteractionRules.LuxWeiActivationGrantsFlag + "-214788");
            TrySyncClientJournal(source);
        }

        internal static bool CanUseSilvertailSoulTrade(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (!IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSouls)
                && !IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSoulsOne)
                && !IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestSoulsTwo))
            {
                return false;
            }

            return GetSoulCount(source) < 3 && HasFavoredAnalyzer(source);
        }

        internal static bool HasFavoredAnalyzer(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId);
        }

        internal static bool HasInspectedAnalyzer(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       NascenceAbanFalaInteractionRules.InspectedAncientPatternAnalyzerItemId);
        }

        internal static bool TryForceReturnFavoredAnalyzer(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            TryConsumeCarriedItem(source, NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId);
            return TryRestoreItem(source, NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId, 1);
        }

        internal static bool TryForceReturnInspectedAnalyzer(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            TryConsumeCarriedItem(source, NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId);
            TryConsumeCarriedItem(source, NascenceAbanFalaInteractionRules.InspectedAncientPatternAnalyzerItemId);
            TryConsumeCarriedItem(source, NascenceAbanFalaInteractionRules.AncientDeviceItemId);
            return TryRestoreItem(source, NascenceAbanFalaInteractionRules.InspectedAncientPatternAnalyzerItemId, 1);
        }

        internal static int IncrementSoulCount(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return 0;
            }

            int previous = GetSoulCount(source);
            if (previous >= 3)
            {
                return 3;
            }

            int next = previous + 1;
            int characterId = source.Identity.Instance;
            MissionRuntime.Service.SetFlag(
                characterId,
                NascenceAbanFalaInteractionRules.QuestSouls,
                NascenceAbanFalaInteractionRules.SoulCountFlag,
                next.ToString(CultureInfo.InvariantCulture));

            PurgeDuplicateSoulsClientQuests(source);

            if (next >= 3)
            {
                ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestSouls);
                ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestSoulsOne);
                ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestSoulsTwo);
                AcceptOne(source, characterId, NascenceAbanFalaInteractionRules.QuestSoulsReturn);
                NascenceAbanFalaPacketSender.TrySendSoulsReturnQuestFullUpdate(source);
            }
            else
            {
                AcceptOne(source, characterId, NascenceAbanFalaInteractionRules.QuestSouls);
                NascenceAbanFalaPacketSender.TrySendSoulsQuestFullUpdate(source, next);
            }

            TrySyncClientJournal(source);
            return next;
        }

        private static void PurgeDuplicateSoulsClientQuests(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            NascenceAbanFalaPacketSender.TrySendQuestDelete(source, NascenceAbanFalaInteractionRules.QuestSoulsOne);
            NascenceAbanFalaPacketSender.TrySendQuestDelete(source, NascenceAbanFalaInteractionRules.QuestSoulsTwo);
            NascenceAbanFalaPacketSender.TrySendQuestDelete(source, NascenceAbanFalaInteractionRules.QuestSoulsReturn);
        }

        internal static bool IsAbanChainFinishedForClient(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (HasAbanGardenKey(source))
            {
                return true;
            }

            int characterId = source.Identity.Instance;
            return MissionRuntime.Service.GetFlag(
                       characterId,
                       NascenceAbanFalaInteractionRules.QuestSouls,
                       NascenceAbanFalaInteractionRules.LuxWeiKeyGrantedFlag) != null
                   || MissionRuntime.Service.GetFlag(
                       characterId,
                       NascenceAbanFalaInteractionRules.QuestSoulsReturn,
                       NascenceAbanFalaInteractionRules.LuxWeiKeyGrantedFlag) != null;
        }

        internal static void ClearStaleAbanQuestsFromClient(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            for (int i = 0; i < NascenceAbanFalaInteractionRules.AllClientQuestIds.Length; i++)
            {
                string questId = NascenceAbanFalaInteractionRules.AllClientQuestIds[i];
                NascenceAbanFalaPacketSender.TrySendQuestDelete(source, questId);

                MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, questId);
                if (mission != null && mission.State == MissionLifecycleState.Active)
                {
                    MissionRuntime.Service.CompleteMission(characterId, questId);
                }
            }
        }

        private static bool TryRestoreItem(ICharacter source, int itemId, int quality)
        {
            if (source == null || itemId <= 0 || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
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

            Item item;
            try
            {
                item = new Item(quality > 0 ? quality : 1, itemId, itemId);
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

            SendItemGrantPackets(source, item);
            return true;
        }

        private static void TryConsumeCarriedItem(ICharacter source, int itemId)
        {
            if (source == null || itemId <= 0 || source.BaseInventory == null)
            {
                return;
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
                    IItem item = entry.Value;
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.LowID != itemId && item.HighID != itemId)
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
                        }
                        else
                        {
                            page.Add(entry.Key, item);
                        }
                    }
                    catch (Exception)
                    {
                        page.Add(entry.Key, item);
                    }

                    return;
                }
            }
        }

        internal static void CompleteLuxWeiKeyReturn(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            if (!IsLuxWeiKeyReturnReady(source))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_ABAN_FALA key return blocked souls="
                    + GetSoulCount(source).ToString(CultureInfo.InvariantCulture));
                return;
            }

            int characterId = source.Identity.Instance;

            // Capture 20260822-224319 @21:01:03: garden key then favored analyzer.
            bool keyGranted = TryGrantAbanGardenKey(source);
            bool analyzerGranted = TryForceReturnFavoredAnalyzer(source);
            if (!keyGranted)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_ABAN_FALA garden key grant failed char="
                    + source.Identity.ToString(true)
                    + " souls="
                    + GetSoulCount(source).ToString(CultureInfo.InvariantCulture));
            }

            if (!analyzerGranted)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_ABAN_FALA favored analyzer return failed char="
                    + source.Identity.ToString(true));
            }

            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestSoulsReturn);
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestSoulsTwo);
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestSoulsOne);
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestSouls);
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestGarden);
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestDeviceInfo);
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestInsigniaTask);
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestDonnaDevice);
            ClearStaleAbanQuestsFromClient(source);
        }

        private static bool TryGrantQuestItem(ICharacter source, string questId, int itemId, string flagKey)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            bool alreadyFlagged = MissionRuntime.Service.GetFlag(characterId, questId, flagKey) != null;
            bool hasItem = InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                           && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                               source,
                               itemId);

            if (alreadyFlagged && hasItem)
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

            if (!hasItem)
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

                SendItemGrantPackets(source, item);
            }

            MissionOperationResult flag = MissionRuntime.Service.SetFlag(
                characterId,
                questId,
                flagKey,
                "item:" + itemId);
            return flag.Status == MissionOperationStatus.Applied
                   || flag.Status == MissionOperationStatus.AlreadyApplied;
        }

        private static void SendItemGrantPackets(ICharacter source, Item item)
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
                    Target = new Identity
                             {
                                 Type = IdentityType.OverflowWindow,
                                 Instance = source.Identity.Instance
                             },
                    TargetPlacement = 0x6F
                });
        }

        private static void ForceRetireQuest(ICharacter source, int characterId, string questId)
        {
            TryRemoveClientQuest(source, questId);

            MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, questId);
            if (mission != null && mission.State == MissionLifecycleState.Active)
            {
                MissionRuntime.Service.CompleteMission(characterId, questId);
            }
        }

        private static void PurgeSupersededClientQuests(ICharacter source)
        {
            if (HasAbanChainProgressed(source))
            {
                TryRemoveDonnaQuestFromClient(source);
            }

            if (IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden)
                || IsSoulsQuestActive(source))
            {
                if (!IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestInsigniaTask))
                {
                    TryRemoveClientQuest(source, NascenceAbanFalaInteractionRules.QuestInsigniaTask);
                }

                if (!IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestDeviceInfo))
                {
                    TryRemoveClientQuest(source, NascenceAbanFalaInteractionRules.QuestDeviceInfo);
                }
            }

            if (IsSoulsQuestActive(source)
                && !IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden))
            {
                TryRemoveClientQuest(source, NascenceAbanFalaInteractionRules.QuestGarden);
            }
        }

        private static void TryRemoveClientQuest(ICharacter source, string questId)
        {
            if (source == null || string.IsNullOrWhiteSpace(questId))
            {
                return;
            }

            NascenceAbanFalaPacketSender.TrySendQuestDelete(source, questId);

            int missionInstance = ParseMissionInstance(questId);
            if (missionInstance == 0)
            {
                return;
            }

            MissionAcceptedStore.Remove(
                source.Identity.Instance,
                new Identity
                {
                    Type = (IdentityType)0x0000DAC3,
                    Instance = missionInstance
                });
        }

        private static int ParseMissionInstance(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return 0;
            }

            string normalized = questId.Trim();
            int colon = normalized.LastIndexOf(':');
            string hex = colon >= 0 ? normalized.Substring(colon + 1) : normalized;
            int instance;
            return int.TryParse(
                hex,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out instance)
                       ? instance
                       : 0;
        }

        private static void SyncGardenPhaseClientJournal(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            if (!IsMissionActive(source, NascenceAbanFalaInteractionRules.QuestGarden))
            {
                return;
            }

            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestInsigniaTask);
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestDeviceInfo);
            ForceRetireQuest(source, characterId, NascenceAbanFalaInteractionRules.QuestDonnaDevice);
            NascenceAbanFalaPacketSender.TrySendGardenQuestFullUpdate(source);
        }

        private static void AcceptOne(ICharacter source, int characterId, string questId)
        {
            if (IsMissionActive(source, questId))
            {
                return;
            }

            MissionOperationResult offer = MissionRuntime.Service.OfferMission(characterId, questId);
            if (offer != null
                && offer.Status != MissionOperationStatus.Applied
                && offer.Status != MissionOperationStatus.AlreadyApplied
                && offer.Status != MissionOperationStatus.Unresolved)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_ABAN_FALA offer failed quest="
                    + questId
                    + " status="
                    + offer.Status.ToString());
            }

            MissionRuntime.Service.AcceptMission(characterId, questId);
        }

        internal static bool TryGmResetAbanQuest(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            NascenceAbanFalaTradeAdapter.ClearTradeSession(source);

            for (int i = 0; i < NascenceAbanFalaInteractionRules.AllClientQuestIds.Length; i++)
            {
                string questId = NascenceAbanFalaInteractionRules.AllClientQuestIds[i];
                NascenceAbanFalaPacketSender.TrySendQuestDelete(source, questId);
                MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, questId);
                if (mission != null && mission.State == MissionLifecycleState.Active)
                {
                    MissionRuntime.Service.AbandonMission(characterId, questId);
                }
            }

            MissionRuntime.Service.SetFlag(
                characterId,
                NascenceAbanFalaInteractionRules.QuestSouls,
                NascenceAbanFalaInteractionRules.SoulCountFlag,
                "0");
            MissionRuntime.Service.SetFlag(
                characterId,
                NascenceAbanFalaInteractionRules.QuestGarden,
                NascenceAbanFalaInteractionRules.LuxWeiDeviceShownFlag,
                string.Empty);
            MissionRuntime.Service.SetFlag(
                characterId,
                NascenceAbanFalaInteractionRules.QuestSouls,
                NascenceAbanFalaInteractionRules.LuxWeiKeyGrantedFlag,
                string.Empty);
            MissionRuntime.Service.SetFlag(
                characterId,
                NascenceAbanFalaInteractionRules.QuestDeviceInfo,
                NascenceAbanFalaInteractionRules.DeviceInspectedFlag,
                string.Empty);
            MissionRuntime.Service.SetFlag(
                characterId,
                NascenceAbanFalaInteractionRules.QuestInsigniaTask,
                NascenceAbanFalaInteractionRules.DeviceInspectedFlag,
                string.Empty);

            TryRemoveCarriedItem(source, NascenceAbanFalaInteractionRules.GardenKeyItemId);

            return true;
        }

        internal static bool TryHandleJournalDelete(ICharacter source, Identity missionIdentity)
        {
            if (source == null || missionIdentity == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            string questId;
            if (!NascenceAbanFalaInteractionRules.TryResolveQuestId(missionIdentity.Instance, out questId))
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, questId);
            if (mission == null || mission.State != MissionLifecycleState.Active)
            {
                return false;
            }

            NascenceAbanFalaPacketSender.TrySendQuestDelete(source, questId);
            MissionRuntime.Service.AbandonMission(characterId, questId);

            if (NascenceAbanFalaInteractionRules.IsSoulsQuestId(questId))
            {
                MissionRuntime.Service.SetFlag(
                    characterId,
                    NascenceAbanFalaInteractionRules.QuestSouls,
                    NascenceAbanFalaInteractionRules.SoulCountFlag,
                    "0");
            }

            return true;
        }

        private static void TryRemoveCarriedItem(ICharacter source, int itemId)
        {
            if (source == null || itemId <= 0 || source.BaseInventory == null)
            {
                return;
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
                    IItem item = entry.Value;
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.LowID != itemId && item.HighID != itemId)
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
                        }
                        else
                        {
                            page.Add(entry.Key, item);
                        }
                    }
                    catch (Exception)
                    {
                        page.Add(entry.Key, item);
                    }

                    return;
                }
            }
        }
    }
}
