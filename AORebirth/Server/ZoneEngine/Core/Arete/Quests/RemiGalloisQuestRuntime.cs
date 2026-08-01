namespace ZoneEngine.Core.Arete.Quests
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Capture 20260727-204902: Remi Gallois Hellfyre field test on Arete Landing.
    /// Accept → Mission:556B5E53 + Experimental Hellfyre Rocket Launcher (295757).
    /// Kill 3 SANDSTORM Marauders → Mission:556B5E59 Return to Remi.
    /// Finish → 2080 XP, 1440 credits + TemplateAction 223349/223361/215265/223365 ql25.
    /// </summary>
    public static class RemiGalloisQuestRuntime
    {
        public const string RootNodeId = "remi_001";

        public const string DoingNodeId = "remi_doing";

        public const string ReturnNodeId = "remi_return";

        public const string DoneNodeId = "remi_done";

        public const string OfferNodeId = "remi_offer_001";

        public const string AcceptNodeId = "remi_accept_001";

        public const string RegrantNodeId = "remi_regrant";

        public const string FinishNodeId = "remi_finish";

        public const string QuellQuestId = "Mission:556B5E53";

        public const string ReturnQuestId = "Mission:556B5E59";

        public const int HellfyreLauncherItemId = 295757;

        /// <summary>Capture 20260727-204902 Hellfyre HealthDamage Amount=-500 FireAC per rocket.</summary>
        public const int HellfyreCapturedDamage = 500;

        /// <summary>Capture CastNanoSpell / SpellList nano for EMP Rocket Detonation.</summary>
        public const int HellfyreCapturedNanoId = 296911;

        private const int AreteLandingPlayfieldId = 6553;

        private const int RequiredKillCount = 3;

        private const int FinishXpReward = 2080;

        private const int FinishCreditReward = 1440;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        // Capture FormatFeedback "Received reward: 2080 XP, 1440 credits."
        private const string FinishRewardFeedback = "~&!!!\":$'O\"ui!!!9Ii!!!1q~";

        private const string RewardsGrantedFlag = "remi-gallois-rewards-granted";

        private static readonly int[] FinishRewardItemIds =
            {
                223349,
                223361,
                215265,
                223365
            };

        private const int FinishRewardQuality = 25;

        private static readonly object KillProgressSyncRoot = new object();

        private static readonly Dictionary<int, int> LocalKillProgressByCharacter = new Dictionary<int, int>();

        private static readonly Dictionary<int, HashSet<string>> LocalObservedDeathsByCharacter =
            new Dictionary<int, HashSet<string>>();

        private static readonly HashSet<int> RewardsGrantedByCharacter = new HashSet<int>();

        public static string ResolveRemiStartNodeId(ICharacter source)
        {
            if (source == null || !IsInAreteLanding(source))
            {
                return null;
            }

            if (HasRewardsGranted(source))
            {
                return DoneNodeId;
            }

            if (IsMissionActive(source, ReturnQuestId) && !IsMissionCompleted(source, ReturnQuestId))
            {
                RemiGalloisTipSender.TrySendReturnTipOnly(source);
                return ReturnNodeId;
            }

            if (IsMissionActive(source, QuellQuestId) && !IsMissionCompleted(source, QuellQuestId))
            {
                RemiGalloisTipSender.TrySendQuellTipOnly(source);
                return DoingNodeId;
            }

            return RootNodeId;
        }

        public static bool IsCompleted(ICharacter source)
        {
            return HasRewardsGranted(source);
        }

        public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null || string.IsNullOrEmpty(previousNodeId))
            {
                return false;
            }

            if (string.Equals(previousNodeId, OfferNodeId, StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                StartQuellQuest(source);
                return true;
            }

            // Re-assert tip when closing accept dialogue (mission window may miss mid-knubot QFU).
            if (string.Equals(previousNodeId, AcceptNodeId, StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                RemiGalloisTipSender.TrySendQuellTipOnly(source);
                return true;
            }

            if (string.Equals(previousNodeId, DoingNodeId, StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                TryGrantHellfyreLauncher(source);
                return true;
            }

            if (string.Equals(previousNodeId, ReturnNodeId, StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                CompleteReturnRewards(source);
                return true;
            }

            return false;
        }

        public static bool TryObserveNpcDeath(ICharacter attacker, ICharacter target)
        {
            if (attacker == null
                || target == null
                || !IsInAreteLanding(attacker)
                || !IsSandstormMarauder(target)
                || !IsMissionActive(attacker, QuellQuestId)
                || IsMissionCompleted(attacker, QuellQuestId)
                || HasRewardsGranted(attacker))
            {
                return false;
            }

            string observationKey = target.Identity.ToString(true);
            int characterId = attacker.Identity.Instance;
            int progress = AdvanceLocalKillProgress(characterId, observationKey);
            if (progress <= 0)
            {
                return false;
            }

            TrySendKillFeedback(attacker, progress);
            if (progress >= RequiredKillCount)
            {
                ClearLocalKillProgress(characterId);
                CompleteQuellAndOfferReturn(attacker);
            }

            return true;
        }

        private static void StartQuellQuest(ICharacter source)
        {
            if (source == null || HasRewardsGranted(source))
            {
                return;
            }

            try
            {
                EnsureQuestActive(source, QuellQuestId);
            }
            catch (Exception ex)
            {
                Log("EnsureQuestActive failed err=" + ex.Message);
            }

            // Capture order: AppendText accept → QuestFullUpdate tip → TemplateAction.
            // Tip/grant are flushed after the accept AppendText in the dialogue router.
            Log("quell mission armed character=" + source.Identity.ToString(true));
        }

        /// <summary>
        /// Capture 20260727-204902: after accept AppendText, emit QuestFullUpdate tip then Hellfyre.
        /// The accept Goodbye may reassert the same tip immediately; no uncaptured timer is used.
        /// </summary>
        public static void EmitAcceptTipAndHellfyre(ICharacter source)
        {
            if (source == null || HasRewardsGranted(source))
            {
                return;
            }

            // Capture order: QuestFullUpdate tip → TemplateAction Hellfyre.
            RexQuestPreviewEmissionResult tip = RemiGalloisTipSender.TrySendQuellTipOnly(source);
            Log("quell tip(accept) result=" + tip.Message);
            TryGrantHellfyreLauncher(source);
        }

        private static void CompleteQuellAndOfferReturn(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteTip(source.Identity.Instance, QuellQuestId, "mission_556B5E53_kill_marauders");
            }

            EnsureQuestActive(source, ReturnQuestId);
            RexQuestPreviewEmissionResult tip = RemiGalloisTipSender.TrySendQuellToReturnHandoff(source);
            Log("quell complete → return tip result=" + tip.Message);
        }

        /// <summary>
        /// Capture SpellList after Hellfyre CastNanoSpell: nano name "EMP Rocket Detonation".
        /// </summary>
        public static void TryAnnounceHellfyreSpellList(ICharacter attacker, ICharacter target)
        {
            if (attacker?.Playfield == null || target == null)
            {
                return;
            }

            try
            {
                attacker.Playfield.Announce(
                    new SpellListMessage
                    {
                        Identity = attacker.Identity,
                        Unknown = 0,
                        Character = attacker.Identity,
                        NanoName = "EMP Rocket Detonation",
                        NanoEffects =
                            new[]
                            {
                                new NanoEffect
                                {
                                    Effect =
                                        new Identity
                                        {
                                            Type = (IdentityType)0x0000CF0A,
                                            Instance = HellfyreCapturedNanoId
                                        },
                                    Unknown1 = 4,
                                    CriterionCount = 1,
                                    Hits = 0,
                                    Delay = 0,
                                    Unknown2 = 1,
                                    Unknown3 = 0,
                                    GfxValue = 0,
                                    GfxRed = 0,
                                    GfxGreen = 0,
                                    GfxBlue = 0
                                }
                            }
                    });
            }
            catch (Exception ex)
            {
                Log("hellfyre SpellList failed err=" + ex.Message);
            }
        }

        private static void CompleteReturnRewards(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            if (HasRewardsGranted(source))
            {
                RemiGalloisTipSender.DeleteReturnTip(source);
                return;
            }

            RemiGalloisTipSender.DeleteReturnTip(source);
            ApplyFinishXpCredits(source);
            TryGrantFinishRewardItems(source);
            TrySendFinishRewardFeedback(source);
            FeedbackMessageHandler.Default.Send(source, 110, 108871108);

            MarkRewardsGranted(source);
            if (MissionRuntime.IsInitialized)
            {
                EnsureQuestActive(source, ReturnQuestId);
                ForceCompleteTip(source.Identity.Instance, ReturnQuestId, "mission_556B5E59_return_remi");
                MissionRuntime.Service.SetFlag(
                    source.Identity.Instance,
                    ReturnQuestId,
                    RewardsGrantedFlag,
                    "1");
            }

            Log("return complete rewards character=" + source.Identity.ToString(true));
        }

        private static void TryGrantHellfyreLauncher(ICharacter source)
        {
            TryGrantHellfyreAsRightHandWeapon(source);
        }

        /// <summary>
        /// Capture 20260727-204902: grant TemplateAction Overflow + ContainerAdd Overflow,
        /// then player ClientMove Inventory→Slot 6 (Righthand) + WIFU Unknown2=6.
        /// Equip RH immediately so the launcher is a hand weapon, not HUD1.
        /// </summary>
        private static void TryGrantHellfyreAsRightHandWeapon(ICharacter source)
        {
            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null)
            {
                Log("hellfyre grant skipped reason=no-inventory-or-client");
                return;
            }

            if (!ItemLoader.ItemList.ContainsKey(HellfyreLauncherItemId))
            {
                Log("hellfyre grant skipped reason=missing-ItemLoader-template id=" + HellfyreLauncherItemId);
                return;
            }

            IInventoryPage weaponPage;
            if (!source.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage)
                || weaponPage == null)
            {
                Log("hellfyre grant skipped reason=no-weapon-page");
                return;
            }

            int rightHand = (int)WeaponSlots.Righthand;
            Item item = new Item(1, HellfyreLauncherItemId, HellfyreLauncherItemId);
            // Capture WIFU Flags=205520897 — Item.Flags is otherwise left 0 on fresh grants.
            int templateFlags = item.GetAttribute((int)StatIds.flags);
            if (templateFlags > 0 && templateFlags != 1234567890)
            {
                item.Flags = templateFlags;
            }

            // Hellfyre stores the launcher mesh on attr 209 / mesh(12), not WeaponMeshRight.
            // Seed WeaponMeshRight so EnsureWeaponVisualMeshes applies rocket mesh 264083.
            int hellfyreMesh = item.GetAttribute(209);
            if (hellfyreMesh <= 0 || hellfyreMesh == 1234567890)
            {
                hellfyreMesh = item.GetAttribute((int)StatIds.mesh);
            }

            if (hellfyreMesh > 0 && hellfyreMesh != 1234567890)
            {
                item.SetAttribute((int)StatIds.weaponmeshright, hellfyreMesh);
            }

            // Capture overflow grant chrome first.
            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = HellfyreLauncherItemId,
                    ItemHighId = HellfyreLauncherItemId,
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

            try
            {
                IInventoryPage inventoryPage;
                if (!source.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out inventoryPage)
                    || inventoryPage == null)
                {
                    Log("hellfyre grant skipped reason=no-inventory-page");
                    return;
                }

                IItem existing = weaponPage[rightHand];
                if (existing != null)
                {
                    int freeForUnequip = inventoryPage.FindFreeSlot();
                    if (freeForUnequip >= 0)
                    {
                        IItemSlotHandler slotHandler = weaponPage as IItemSlotHandler;
                        if (slotHandler != null)
                        {
                            slotHandler.Unequip(rightHand, inventoryPage, freeForUnequip);
                            UnEquip.Send(source.Controller.Client, weaponPage, rightHand);
                        }
                    }
                }

                // Clear Hud1 if a prior bad grant parked the launcher there.
                IItem hudItem = weaponPage[(int)WeaponSlots.Hud1];
                if (hudItem != null
                    && (hudItem.LowID == HellfyreLauncherItemId || hudItem.HighID == HellfyreLauncherItemId))
                {
                    weaponPage.Remove((int)WeaponSlots.Hud1);
                }

                // Capture equip path is Inventory→RH. Park in a real bag slot first so
                // ContainerAdd Source=Inventory:<slot> does not steal another item's icon
                // (hardcoded 0x43 previously produced sunglasses-in-RH chrome).
                int inventorySlot = inventoryPage.FindFreeSlot();
                if (inventorySlot < 0)
                {
                    Log("hellfyre grant skipped reason=inventory-full");
                    return;
                }

                InventoryError bagError = source.BaseInventory.AddToPage(
                    (int)IdentityType.Inventory,
                    inventorySlot,
                    item);
                if (bagError != InventoryError.OK)
                {
                    Log("hellfyre inventory AddToPage failed status=" + bagError);
                    return;
                }

                IItemSlotHandler weaponSlots = weaponPage as IItemSlotHandler;
                if (weaponSlots == null)
                {
                    Log("hellfyre grant skipped reason=no-weapon-slot-handler");
                    return;
                }

                weaponSlots.Equip(inventoryPage, inventorySlot, rightHand);

                source.BaseInventory.Write();

                // Capture equip result: ContainerAdd Inventory→SimpleChar Slot=6 + WIFU.
                source.Send(
                    new ContainerAddItemMessage
                    {
                        Identity = source.Identity,
                        Unknown = 0,
                        SourceContainer = new Identity
                                          {
                                              Type = IdentityType.Inventory,
                                              Instance = inventorySlot
                                          },
                        Target = source.Identity,
                        TargetPlacement = rightHand
                    });
                WeaponItemFullUpdate.SendWeaponDefinition(source, item);
                Equip.Send(source.Controller.Client, weaponPage, rightHand);

                // Equip.Send skips TemplateAction for RH/LH; redraw icon on WeaponPage slot 6.
                source.Send(
                    new TemplateActionMessage
                    {
                        Identity = source.Identity,
                        Unknown = 0,
                        ItemLowId = HellfyreLauncherItemId,
                        ItemHighId = HellfyreLauncherItemId,
                        Quality = 1,
                        Unknown1 = CapturedTemplateActionUnknown1,
                        Unknown2 = rightHand,
                        Placement = new Identity
                                    {
                                        Type = IdentityType.WeaponPage,
                                        Instance = rightHand
                                    },
                        Unknown3 = 0,
                        Unknown4 = 0
                    });

                source.CalculateSkills();
                InventoryContainerRuntimeService.Default.EnsureWeaponVisualMeshes(source, true);
                Log(
                    "hellfyre equipped RH character="
                    + source.Identity.ToString(true)
                    + " fromInventorySlot="
                    + inventorySlot
                    + " flags="
                    + item.Flags);
            }
            catch (Exception ex)
            {
                Log("hellfyre RH equip failed err=" + ex.Message);
            }
        }

        private static void TryGrantFinishRewardItems(ICharacter source)
        {
            for (int i = 0; i < FinishRewardItemIds.Length; i++)
            {
                int itemId = FinishRewardItemIds[i];
                TryGrantOverflowItem(source, itemId, itemId, FinishRewardQuality, "reward-" + itemId);
            }
        }

        private static void TryGrantOverflowItem(
            ICharacter source,
            int lowId,
            int highId,
            int quality,
            string label)
        {
            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null)
            {
                Log(label + " grant skipped reason=no-inventory-or-client");
                return;
            }

            if (!ItemLoader.ItemList.ContainsKey(lowId) && !ItemLoader.ItemList.ContainsKey(highId))
            {
                Log(label + " grant skipped reason=missing-ItemLoader-template id=" + lowId);
                return;
            }

            Item item = new Item(quality, lowId, highId);
            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log(label + " TryGrantQuestRewardItem failed status=" + grant.Status + " id=" + lowId);
                return;
            }

            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = lowId,
                    ItemHighId = highId,
                    Quality = quality,
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

        private static void ApplyFinishXpCredits(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            bool cashApplied = false;
            if (MissionRuntime.IsInitialized)
            {
                MissionRewardDefinition cashDefinition = new MissionRewardDefinition
                                                        {
                                                            RewardKey = "captured-remi-gallois-credits",
                                                            RewardType = "character-stats",
                                                            IsResolved = true,
                                                            StatMutations =
                                                                new[]
                                                                {
                                                                    new MissionCharacterStatMutation
                                                                    {
                                                                        StatIdentityType =
                                                                            (int)IdentityType.CanbeAffected,
                                                                        StatId = (int)StatIds.cash,
                                                                        Kind = MissionStatMutationKind.AddClamped,
                                                                        Value = FinishCreditReward,
                                                                        MinimumValue = 0,
                                                                        MaximumValue = uint.MaxValue
                                                                    }
                                                                }
                                                        };
                MissionRewardExecutionResult cashResult = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                    source.Identity.Instance,
                    ReturnQuestId,
                    cashDefinition,
                    "capture:20260727-204902:remi-finish-credits");
                if (cashResult.Succeeded && cashResult.StatValues != null)
                {
                    foreach (MissionCharacterStatValue statValue in cashResult.StatValues)
                    {
                        if (statValue.StatId != (int)StatIds.cash)
                        {
                            continue;
                        }

                        uint value = statValue.Value <= 0
                                         ? 0
                                         : (uint)Math.Min(statValue.Value, uint.MaxValue);
                        source.Stats[StatIds.cash].Set(value);
                        cashApplied = true;
                    }

                    if (cashApplied)
                    {
                        StatMessageHandler.Default.SendChanged(source);
                    }
                }
            }

            if (!cashApplied)
            {
                long cashAfter = (long)source.Stats[StatIds.cash].Value + FinishCreditReward;
                if (cashAfter > uint.MaxValue)
                {
                    cashAfter = uint.MaxValue;
                }

                source.Stats[StatIds.cash].Set((uint)cashAfter);
                StatMessageHandler.Default.SendChanged(source);
            }

            CombatXpRuntimeService.AwardDirectXp(source, FinishXpReward, "remi-gallois-2080xp");
        }

        private static void TrySendFinishRewardFeedback(ICharacter source)
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

        private static void TrySendKillFeedback(ICharacter character, int currentCount)
        {
            if (character?.Controller?.Client == null
                || currentCount <= 0
                || currentCount >= RequiredKillCount)
            {
                return;
            }

            string feedback = GetCapturedRemainingCountFeedback(currentCount);
            if (string.IsNullOrEmpty(feedback))
            {
                return;
            }

            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = feedback,
                    Unknown2 = 0
                });
        }

        private static string GetCapturedRemainingCountFeedback(int currentCount)
        {
            // Capture: remaining = 3-current → '!'+remaining before "SANDSTORM Mechs" (0x10 sep).
            switch (currentCount)
            {
                case 1:
                    return "~&!!!\":$nZiAi!!!!#s\u0010SANDSTORM Mechs";
                case 2:
                    return "~&!!!\":$nZiAi!!!!\"s\u0010SANDSTORM Mechs";
                default:
                    return null;
            }
        }

        private static bool IsSandstormMarauder(ICharacter target)
        {
            return AreteSandstormMarauderRuntime.IsRegisteredMarauder(target);
        }

        private static int AdvanceLocalKillProgress(int characterId, string observationKey)
        {
            lock (KillProgressSyncRoot)
            {
                HashSet<string> seen;
                if (!LocalObservedDeathsByCharacter.TryGetValue(characterId, out seen) || seen == null)
                {
                    seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    LocalObservedDeathsByCharacter[characterId] = seen;
                }

                int progress;
                if (!LocalKillProgressByCharacter.TryGetValue(characterId, out progress))
                {
                    progress = 0;
                }

                if (!seen.Add(observationKey ?? string.Empty))
                {
                    return progress;
                }

                progress = Math.Min(RequiredKillCount, progress + 1);
                LocalKillProgressByCharacter[characterId] = progress;
                return progress;
            }
        }

        private static void ClearLocalKillProgress(int characterId)
        {
            lock (KillProgressSyncRoot)
            {
                LocalKillProgressByCharacter.Remove(characterId);
                LocalObservedDeathsByCharacter.Remove(characterId);
            }
        }

        private static bool HasRewardsGranted(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (RewardsGrantedByCharacter.Contains(source.Identity.Instance))
            {
                return true;
            }

            if (!MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       ReturnQuestId,
                       RewardsGrantedFlag) != null;
        }

        private static void MarkRewardsGranted(ICharacter source)
        {
            if (source != null)
            {
                RewardsGrantedByCharacter.Add(source.Identity.Instance);
            }
        }

        private static bool IsMissionActive(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static bool IsMissionCompleted(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
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

            if (mission == null || mission.State == MissionLifecycleState.Offered)
            {
                MissionRuntime.Service.OfferMission(characterId, questId);
                MissionRuntime.Service.AcceptMission(characterId, questId);
            }
        }

        private static void ForceCompleteTip(int characterId, string questId, string objectiveId)
        {
            if (!MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))
            {
                return;
            }

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

            if (!string.IsNullOrEmpty(objectiveId))
            {
                MissionRuntime.Service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = characterId,
                        QuestId = questId,
                        ObjectiveId = objectiveId,
                        ObservationKey = "remi-force-complete",
                        Amount = 1,
                        EventType = "RemiGalloisQuestRuntime",
                        SourceIdentity = string.Empty,
                        TargetIdentity = string.Empty
                    });
            }

            MissionRuntime.Service.CompleteMission(characterId, questId);
        }

        private static bool IsInAreteLanding(ICharacter source)
        {
            return source?.Playfield != null
                   && source.Playfield.Identity.Instance == AreteLandingPlayfieldId;
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "RemiGalloisQuestRuntime " + message);
        }
    }
}
