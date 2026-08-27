namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Capture-backed Dr. Rosenblatt Hiathlin quest runtime (20260822-070136).
    /// </summary>
    internal static class RosenblattHiathlinQuestRuntime
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

        internal static bool IsMissionCompleted(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        internal static bool HasActiveQuest(ICharacter source)
        {
            return IsMissionActive(source, RosenblattHiathlinInteractionRules.QuestAccept);
        }

        internal static bool CanTurnIn(ICharacter source)
        {
            if (source == null || !HasActiveQuest(source))
            {
                return false;
            }

            if (!HasRequiredBodyParts(source))
            {
                return false;
            }

            EnsureProgressFromBodyParts(source);
            return true;
        }

        internal static string ResolveStartNodeId(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return null;
            }

            if (IsRewardGranted(source)
                || IsMissionCompleted(source, RosenblattHiathlinInteractionRules.QuestAccept))
            {
                return null;
            }

            if (HasActiveQuest(source))
            {
                return RosenblattHiathlinInteractionRules.ReturnRootNodeId;
            }

            return null;
        }

        internal static MissionOperationResult AcceptQuest(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "rosenblatt-quest-runtime-unavailable"
                       };
            }

            string questId = RosenblattHiathlinInteractionRules.QuestAccept;
            int characterId = source.Identity.Instance;

            if (HasActiveQuest(source))
            {
                SyncClientJournal(source);
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-quest-already-active"
                       };
            }

            if (IsMissionCompleted(source, questId) || IsRewardGranted(source))
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "rosenblatt-quest-already-completed"
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
                SetRegularKillCount(source, 0);
                SetPrimeKilled(source, false);
                RosenblattHiathlinPacketSender.TrySendQuestFullUpdate(
                    source,
                    RosenblattHiathlinInteractionRules.QuestAccept);
            }

            return accepted;
        }

        /// <summary>
        /// Capture 20260822-070136: OverflowWindow ContainerAdd slot=0x6F on kill, then QFU stage advance.
        /// </summary>
        internal static bool TryObserveNpcDeath(ICharacter attacker, ICharacter target)
        {
            if (attacker == null || target == null || !(attacker.Controller is PlayerController))
            {
                return false;
            }

            if (!MissionRuntime.IsInitialized || !HasActiveQuest(attacker))
            {
                return false;
            }

            int playfieldId = attacker.Playfield != null ? attacker.Playfield.Identity.Instance : 0;
            if (!RosenblattHiathlinInteractionRules.IsQuestPlayfield(playfieldId))
            {
                return false;
            }

            bool isRegular = RosenblattHiathlinInteractionRules.IsRegularHiathlinName(target.Name);
            bool isPrime = RosenblattHiathlinInteractionRules.IsHiathlinPrimeName(target.Name);
            if (!isRegular && !isPrime)
            {
                return false;
            }

            if (isRegular
                && GetRegularKillCount(attacker) >= RosenblattHiathlinInteractionRules.RequiredRegularKills)
            {
                return false;
            }

            if (isPrime && IsPrimeKilled(attacker))
            {
                return false;
            }

            int itemId = isRegular
                ? RosenblattHiathlinInteractionRules.HiathlinThighItemId
                : RosenblattHiathlinInteractionRules.HiathlinPrimeThighItemId;
            if (!TryGrantBodyPartItem(attacker, itemId))
            {
                return false;
            }

            string previousClientQuestId = ResolveClientQuestId(attacker);
            if (!TryAdvanceBodyPartProgress(attacker, isRegular, isPrime))
            {
                return false;
            }

            return SyncJournalAfterProgress(attacker, previousClientQuestId);
        }

        internal static bool TryObserveBodyPartLoot(ICharacter looter, Item item)
        {
            if (looter == null || item == null || !(looter.Controller is PlayerController))
            {
                return false;
            }

            if (!MissionRuntime.IsInitialized || !HasActiveQuest(looter))
            {
                return false;
            }

            int playfieldId = looter.Playfield != null ? looter.Playfield.Identity.Instance : 0;
            if (!RosenblattHiathlinInteractionRules.IsQuestPlayfield(playfieldId))
            {
                return false;
            }

            bool isRegular = RosenblattHiathlinInteractionRules.IsHiathlinThighItem(item.LowID, item.HighID);
            bool isPrime = RosenblattHiathlinInteractionRules.IsHiathlinPrimeThighItem(item.LowID, item.HighID);
            if (!isRegular && !isPrime)
            {
                return false;
            }

            string previousClientQuestId = ResolveClientQuestId(looter);
            if (!TryAdvanceBodyPartProgress(looter, isRegular, isPrime))
            {
                return false;
            }

            return SyncJournalAfterProgress(looter, previousClientQuestId);
        }

        internal static bool TryResendActiveMissionsForLogin(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (IsRewardGranted(source) && HasActiveQuest(source))
            {
                ClearClientJournal(source);
                MissionRuntime.Service.AbandonMission(
                    source.Identity.Instance,
                    RosenblattHiathlinInteractionRules.QuestAccept);
                return false;
            }

            if (!HasActiveQuest(source))
            {
                return false;
            }

            return SyncClientJournal(source);
        }

        internal static bool CompleteTurnIn(ICharacter source, bool bodyPartsAlreadyConsumed = false)
        {
            if (source == null || !MissionRuntime.IsInitialized || !HasActiveQuest(source))
            {
                return false;
            }

            if (!bodyPartsAlreadyConsumed && !HasRequiredBodyParts(source))
            {
                return false;
            }

            if (bodyPartsAlreadyConsumed)
            {
                SyncTurnInProgressFromStagedParts(source);
            }
            else
            {
                EnsureProgressFromBodyParts(source);
            }

            if (!TryGrantLightBarReward(source))
            {
                return false;
            }

            ClearClientJournal(source);
            MissionCompleteService.SendMissionAccomplishedFeedback(source);
            MissionOperationResult completed = MissionRuntime.Service.CompleteMission(
                source.Identity.Instance,
                RosenblattHiathlinInteractionRules.QuestAccept);
            if (IsPersistenceFailure(completed) && HasActiveQuest(source))
            {
                MissionRuntime.Service.AbandonMission(
                    source.Identity.Instance,
                    RosenblattHiathlinInteractionRules.QuestAccept);
            }

            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                RosenblattHiathlinInteractionRules.QuestAccept,
                RosenblattHiathlinInteractionRules.RewardGrantedFlag,
                "item:" + RosenblattHiathlinInteractionRules.LightBarRewardItemId.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        internal static int CountBodyPart(ICharacter source, int itemId)
        {
            if (source == null || source.BaseInventory == null || itemId <= 0)
            {
                return 0;
            }

            int count = 0;
            foreach (var pageEntry in source.BaseInventory.Pages)
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (var entry in page.List())
                {
                    IItem item = entry.Value;
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.LowID == itemId || item.HighID == itemId)
                    {
                        int stack = item.MultipleCount > 0 ? item.MultipleCount : 1;
                        count += stack;
                    }
                }
            }

            if (count > 0)
            {
                return count;
            }

            return InventoryContainerRuntimeService.Default.CountCharacterItemInCarriedInventory(
                source,
                itemId);
        }

        internal static bool HasRequiredBodyParts(ICharacter source)
        {
            return source != null
                   && CountBodyPart(source, RosenblattHiathlinInteractionRules.HiathlinThighItemId)
                      >= RosenblattHiathlinInteractionRules.RequiredRegularBodyParts
                   && CountBodyPart(source, RosenblattHiathlinInteractionRules.HiathlinPrimeThighItemId)
                      >= RosenblattHiathlinInteractionRules.RequiredPrimeBodyParts;
        }

        internal static bool TryConsumeBodyParts(ICharacter source)
        {
            if (source == null || !HasRequiredBodyParts(source))
            {
                return false;
            }

            int regularRemaining = RosenblattHiathlinInteractionRules.RequiredRegularBodyParts;
            int primeRemaining = RosenblattHiathlinInteractionRules.RequiredPrimeBodyParts;
            foreach (KeyValuePair<int, IInventoryPage> pageEntry in source.BaseInventory.Pages.ToList())
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> entry in page.List().ToList())
                {
                    IItem item = entry.Value;
                    if (item == null)
                    {
                        continue;
                    }

                    bool isRegular = RosenblattHiathlinInteractionRules.IsHiathlinThighItem(item.LowID, item.HighID);
                    bool isPrime = RosenblattHiathlinInteractionRules.IsHiathlinPrimeThighItem(item.LowID, item.HighID);
                    if (!isRegular && !isPrime)
                    {
                        continue;
                    }

                    int stack = item.MultipleCount > 0 ? item.MultipleCount : 1;
                    if (isRegular && regularRemaining > 0)
                    {
                        int take = Math.Min(stack, regularRemaining);
                        regularRemaining -= take;
                        stack -= take;
                    }
                    else if (isPrime && primeRemaining > 0)
                    {
                        int take = Math.Min(stack, primeRemaining);
                        primeRemaining -= take;
                        stack -= take;
                    }
                    else
                    {
                        continue;
                    }

                    if (stack <= 0)
                    {
                        page.Remove(entry.Key);
                        NotifyItemRemoved(source, pageEntry.Key, entry.Key);
                    }
                    else
                    {
                        item.MultipleCount = stack;
                    }

                    if (regularRemaining <= 0 && primeRemaining <= 0)
                    {
                        break;
                    }
                }

                if (regularRemaining <= 0 && primeRemaining <= 0)
                {
                    break;
                }
            }

            if (regularRemaining > 0 || primeRemaining > 0)
            {
                return false;
            }

            try
            {
                source.BaseInventory.Write();
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private static bool TryAdvanceBodyPartProgress(ICharacter source, bool isRegular, bool isPrime)
        {
            if (isRegular)
            {
                int lootedRegular = GetRegularKillCount(source);
                if (lootedRegular >= RosenblattHiathlinInteractionRules.RequiredRegularKills)
                {
                    return false;
                }

                lootedRegular++;
                SetRegularKillCount(source, lootedRegular);
                MissionRuntime.Service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = source.Identity.Instance,
                        QuestId = RosenblattHiathlinInteractionRules.QuestAccept,
                        ObjectiveId = "mission_55AA388F_hiathlin_kill",
                        ObservationKey = "rosenblatt-hiathlin-regular:" + lootedRegular.ToString(CultureInfo.InvariantCulture),
                        Amount = 1,
                        EventType = "RosenblattHiathlin:RegularKill",
                        SourceIdentity = source.Identity.ToString(true),
                        TargetIdentity = RosenblattHiathlinInteractionRules.HiathlinThighItemId.ToString(CultureInfo.InvariantCulture)
                    });
                return true;
            }

            if (!isPrime || IsPrimeKilled(source))
            {
                return false;
            }

            SetPrimeKilled(source, true);
            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = source.Identity.Instance,
                    QuestId = RosenblattHiathlinInteractionRules.QuestAccept,
                    ObjectiveId = "mission_55AA388F_hiathlin_prime_kill",
                    ObservationKey = "rosenblatt-hiathlin-prime",
                    Amount = 1,
                    EventType = "RosenblattHiathlin:PrimeKill",
                    SourceIdentity = source.Identity.ToString(true),
                    TargetIdentity = RosenblattHiathlinInteractionRules.HiathlinPrimeThighItemId.ToString(CultureInfo.InvariantCulture)
                });
            return true;
        }

        private static bool TryGrantBodyPartItem(ICharacter source, int itemId)
        {
            if (source == null
                || itemId <= 0
                || !MissionRuntime.IsInitialized
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(itemId))
            {
                return false;
            }

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

            SendBodyPartItemNotifications(source, item);
            return true;
        }

        private static void SendBodyPartItemNotifications(ICharacter source, Item item)
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

        private static bool TryGrantLightBarReward(ICharacter source)
        {
            if (source == null
                || !MissionRuntime.IsInitialized
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(RosenblattHiathlinInteractionRules.LightBarRewardItemId))
            {
                return false;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    RosenblattHiathlinInteractionRules.LightBarRewardItemId))
            {
                return true;
            }

            Item item;
            try
            {
                item = new Item(
                    1,
                    RosenblattHiathlinInteractionRules.LightBarRewardItemId,
                    RosenblattHiathlinInteractionRules.LightBarRewardItemId);
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
            return true;
        }

        private static bool SyncJournalAfterProgress(ICharacter source, string previousClientQuestId)
        {
            string nextClientQuestId = ResolveClientQuestId(source);
            if (string.Equals(previousClientQuestId, nextClientQuestId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(previousClientQuestId))
            {
                RosenblattHiathlinPacketSender.TrySendQuestDelete(source, previousClientQuestId);
            }

            RosenblattHiathlinPacketSender.TrySendQuestFullUpdate(source, nextClientQuestId);
            return true;
        }

        private static bool IsRewardGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       RosenblattHiathlinInteractionRules.QuestAccept,
                       RosenblattHiathlinInteractionRules.RewardGrantedFlag) != null;
        }

        private static bool SyncClientJournal(ICharacter source)
        {
            ClearClientJournal(source);
            string questId = ResolveClientQuestId(source);
            return RosenblattHiathlinPacketSender.TrySendQuestFullUpdate(source, questId);
        }

        private static void ClearClientJournal(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < RosenblattHiathlinInteractionRules.ProgressiveClientQuestIds.Length; i++)
            {
                RosenblattHiathlinPacketSender.TrySendQuestDelete(
                    source,
                    RosenblattHiathlinInteractionRules.ProgressiveClientQuestIds[i]);
            }
        }

        private static string ResolveClientQuestId(ICharacter source)
        {
            return RosenblattHiathlinInteractionRules.ResolveClientQuestId(
                GetRegularKillCount(source),
                IsPrimeKilled(source));
        }

        internal static void SyncTurnInProgressFromStagedParts(ICharacter source)
        {
            SetRegularKillCount(source, RosenblattHiathlinInteractionRules.RequiredRegularKills);
            SetPrimeKilled(source, true);
        }

        internal static bool TryGmResetQuest(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            RosenblattHiathlinTradeAdapter.ClearTradeSession(source);
            ClearClientJournal(source);
            MissionRuntime.Service.AbandonMission(
                source.Identity.Instance,
                RosenblattHiathlinInteractionRules.QuestAccept);
            return true;
        }

        private static void EnsureProgressFromBodyParts(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int regularParts = CountBodyPart(source, RosenblattHiathlinInteractionRules.HiathlinThighItemId);
            if (regularParts >= RosenblattHiathlinInteractionRules.RequiredRegularBodyParts
                && GetRegularKillCount(source) < RosenblattHiathlinInteractionRules.RequiredRegularKills)
            {
                SetRegularKillCount(source, RosenblattHiathlinInteractionRules.RequiredRegularKills);
            }

            int primeParts = CountBodyPart(source, RosenblattHiathlinInteractionRules.HiathlinPrimeThighItemId);
            if (primeParts >= RosenblattHiathlinInteractionRules.RequiredPrimeBodyParts && !IsPrimeKilled(source))
            {
                SetPrimeKilled(source, true);
            }
        }

        private static int GetRegularKillCount(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return 0;
            }

            MissionFlagRecord flag = MissionRuntime.Service.GetFlag(
                source.Identity.Instance,
                RosenblattHiathlinInteractionRules.QuestAccept,
                RosenblattHiathlinInteractionRules.RegularKillCountFlag);
            if (flag == null || string.IsNullOrWhiteSpace(flag.Value))
            {
                return 0;
            }

            int count;
            return int.TryParse(flag.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
                       ? Math.Max(0, count)
                       : 0;
        }

        private static void SetRegularKillCount(ICharacter source, int count)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                RosenblattHiathlinInteractionRules.QuestAccept,
                RosenblattHiathlinInteractionRules.RegularKillCountFlag,
                count.ToString(CultureInfo.InvariantCulture));
        }

        private static bool IsPrimeKilled(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       RosenblattHiathlinInteractionRules.QuestAccept,
                       RosenblattHiathlinInteractionRules.PrimeKilledFlag) != null;
        }

        private static void SetPrimeKilled(ICharacter source, bool killed)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            if (!killed)
            {
                return;
            }

            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                RosenblattHiathlinInteractionRules.QuestAccept,
                RosenblattHiathlinInteractionRules.PrimeKilledFlag,
                "1");
        }

        private static void NotifyItemRemoved(ICharacter source, int pageType, int slot)
        {
            try
            {
                ZoneEngine.Core.MessageHandlers.CharacterActionMessageHandler.Default.SendDeleteItem(
                    source,
                    pageType,
                    slot);
            }
            catch (Exception)
            {
            }
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
