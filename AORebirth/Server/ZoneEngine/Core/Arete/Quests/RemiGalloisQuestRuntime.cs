namespace ZoneEngine.Core.Arete.Quests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

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
    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Capture 20260801-SANDSTORM: Remi Gallois Hellfyre field test on Arete Landing.
    /// Accept → Mission:5576B777 + Experimental Hellfyre Rocket Launcher (295757) to overflow
    /// (player equips via normal inventory right-click / ClientMove to RH slot 6).
    /// Kill 3 SANDSTORM Marauders → Mission:5576B780 Return to Remi.
    /// Finish → 2581 XP, 1160 credits + nano crystals ql25; launcher self-destructs after 30s.
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

        public const string QuellQuestId = "Mission:5576B777";

        public const string ReturnQuestId = "Mission:5576B780";

        private const string LegacyQuellQuestId = "Mission:556B5E53";

        private const string LegacyReturnQuestId = "Mission:556B5E59";

        public const int HellfyreLauncherItemId = 295757;

        /// <summary>Capture 20260801-SANDSTORM Hellfyre HealthDamage Amount=-500 FireAC per rocket.</summary>
        public const int HellfyreCapturedDamage = 500;

        /// <summary>Capture CastNanoSpell nano id for rocket detonation.</summary>
        public const int HellfyreCapturedNanoId = 295887;

        private const int AreteLandingPlayfieldId = 6553;

        private const int RequiredKillCount = 3;

        private const int FinishXpReward = 2581;

        private const int FinishCreditReward = 1160;

        // Mike: Experimental Hellfyre self-destructs 30s after Remi finish rewards.
        private const int HellfyreSelfDestructDelayMilliseconds = 30 * 1000;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        // Capture FormatFeedback "Received reward: 2581 XP, 1160 credits."
        private const string FinishRewardFeedback = "~&!!!\":$'O\"ui!!!?@i!!!.X~";

        private const string RewardsGrantedFlag = "remi-gallois-rewards-granted";

        private const string ReturnArmedFlag = "remi-gallois-return-armed";

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

        // Tip wire can succeed while MissionRuntime Offer/Accept fails — still track kills.
        private static readonly HashSet<int> QuellArmedByCharacter = new HashSet<int>();

        // After 3 kills: prefer remi_return even while Hellfyre is still equipped.
        private static readonly HashSet<int> ReturnArmedByCharacter = new HashSet<int>();

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

            // Capture 20260801-SANDSTORM: after kill handoff tip Mission:5576B780 → remi_return
            // ("Your field test is complete!"). Must beat Hellfyre/Quell "doing" branch.
            if (IsReturnPending(source))
            {
                ArmReturn(source);
                RemiGalloisTipSender.TrySendReturnTipOnly(source);
                return ReturnNodeId;
            }

            if (IsQuellPending(source))
            {
                ArmQuell(source);
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
                if (IsReturnPending(source) || HasRewardsGranted(source))
                {
                    return true;
                }

                StartQuellQuest(source);
                return true;
            }

            // Re-assert tip when closing accept dialogue (mission window may miss mid-knubot QFU).
            if (string.Equals(previousNodeId, AcceptNodeId, StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                if (IsReturnPending(source))
                {
                    RemiGalloisTipSender.TrySendReturnTipOnly(source);
                    return true;
                }

                RemiGalloisTipSender.TrySendQuellTipOnly(source);
                return true;
            }

            if (string.Equals(previousNodeId, DoingNodeId, StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                if (IsReturnPending(source))
                {
                    return true;
                }

                TryGrantHellfyreLauncher(source);
                return true;
            }

            if (string.Equals(previousNodeId, ReturnNodeId, StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                CompleteReturnRewards(source);
                return true;
            }

            if (string.Equals(previousNodeId, FinishNodeId, StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                // Capture close after remi_finish AppendText; rewards already granted on remi_return.
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
                || IsReturnPending(attacker)
                || HasRewardsGranted(attacker)
                || (!IsQuellArmed(attacker)
                    && !HasHellfyreLauncher(attacker)
                    && !IsMissionActive(attacker, QuellQuestId)
                    && !IsMissionActive(attacker, LegacyQuellQuestId))
                || IsMissionCompleted(attacker, QuellQuestId))
            {
                return false;
            }

            if (HasHellfyreLauncher(attacker) && !IsReturnPending(attacker))
            {
                ArmQuell(attacker);
            }

            string observationKey = target.Identity.ToString(true);
            int characterId = attacker.Identity.Instance;
            int progress = AdvanceLocalKillProgress(characterId, observationKey);
            if (progress <= 0)
            {
                return false;
            }

            Log(
                "quell kill progress="
                + progress
                + "/"
                + RequiredKillCount
                + " character="
                + attacker.Identity.ToString(true)
                + " target="
                + observationKey);
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
            if (source == null || HasRewardsGranted(source) || IsReturnPending(source))
            {
                return;
            }

            ArmQuell(source);
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
            if (source == null || HasRewardsGranted(source) || IsReturnPending(source))
            {
                return;
            }

            ArmQuell(source);
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

            DisarmQuell(source);
            ArmReturn(source);
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteTip(source.Identity.Instance, QuellQuestId, "mission_5576B777_kill_marauders");
            }

            EnsureQuestActive(source, ReturnQuestId);
            RexQuestPreviewEmissionResult tip = RemiGalloisTipSender.TrySendQuellToReturnHandoff(source);
            Log("quell complete → return tip result=" + tip.Message);
        }

        /// <summary>
        /// Capture 20260801-SANDSTORM: Hellfyre RH (295757) vs SANDSTORM Marauder → 500 FireAC.
        /// </summary>
        public static bool TryGetHellfyreRocketDamage(
            ICharacter attacker,
            ICharacter target,
            int weaponLowId,
            int weaponHighId,
            out int damage)
        {
            damage = 0;
            if (attacker == null
                || target == null
                || !IsInAreteLanding(attacker)
                || !IsSandstormMarauder(target))
            {
                return false;
            }

            if (weaponLowId != HellfyreLauncherItemId && weaponHighId != HellfyreLauncherItemId)
            {
                return false;
            }

            damage = HellfyreCapturedDamage;
            return true;
        }

        public static void AnnounceHellfyreRocketHit(
            ICharacter attacker,
            ICharacter target,
            int damage,
            int targetHpAfter,
            bool killingHit)
        {
            if (attacker?.Playfield == null || target == null || damage <= 0)
            {
                return;
            }

            try
            {
                // Capture: CastNanoSpell NanoId=295887 Unknown1=1 Caster=player Target=marauder.
                attacker.Playfield.Announce(
                    new CastNanoSpellMessage
                    {
                        Identity = attacker.Identity,
                        Unknown = 0,
                        NanoId = HellfyreCapturedNanoId,
                        Unknown1 = 1,
                        Target = target.Identity,
                        Caster = attacker.Identity
                    });
                TryAnnounceHellfyreSpellList(attacker, target);

                // Capture HealthDamage: Identity=marauder Amount=-500 Stat=FireAC TargetHp=... Target=player.
                attacker.Playfield.Announce(
                    new HealthDamageMessage
                    {
                        Identity = target.Identity,
                        Unknown = 0,
                        Unknown1 = -damage,
                        Unknown2 = (int)StatIds.fireac,
                        Unknown3 = targetHpAfter,
                        Unknown4 = killingHit ? 5 : 0,
                        Target = attacker.Identity,
                        Unknown5 = 0
                    });
            }
            catch (Exception ex)
            {
                Log("hellfyre rocket chrome failed err=" + ex.Message);
            }
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

            DisarmReturn(source);
            DisarmQuell(source);
            MarkRewardsGranted(source);
            if (MissionRuntime.IsInitialized)
            {
                EnsureQuestActive(source, ReturnQuestId);
                ForceCompleteTip(source.Identity.Instance, ReturnQuestId, "mission_5576B780_return_remi");
                MissionRuntime.Service.SetFlag(
                    source.Identity.Instance,
                    ReturnQuestId,
                    RewardsGrantedFlag,
                    "1");
            }

            ScheduleHellfyreSelfDestruct(source);
            Log("return complete rewards character=" + source.Identity.ToString(true));
        }

        /// <summary>
        /// Capture 20260801-SANDSTORM: TemplateAction + ContainerAdd Overflow only.
        /// Player equips with normal inventory right-click / ClientMove → RH slot 6.
        /// </summary>
        private static void TryGrantHellfyreLauncher(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            if (HasHellfyreLauncher(source))
            {
                Log("hellfyre grant skipped reason=already-owned character=" + source.Identity.ToString(true));
                return;
            }

            TryGrantOverflowItem(
                source,
                HellfyreLauncherItemId,
                HellfyreLauncherItemId,
                1,
                "hellfyre-overflow");
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

            AreteQuestRewardGrants.GrantCreditsAndXpOnce(
                source,
                ReturnQuestId,
                "arete-credits-awarded-remi-return",
                FinishCreditReward,
                "arete-xp-awarded-remi-return",
                FinishXpReward,
                "remi-gallois-2581xp");
        }

        private static void ScheduleHellfyreSelfDestruct(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            ICharacter captured = source;
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    try
                    {
                        Thread.Sleep(HellfyreSelfDestructDelayMilliseconds);
                        DestroyHellfyreLauncher(captured);
                    }
                    catch (Exception ex)
                    {
                        Log("hellfyre self-destruct schedule failed err=" + ex.Message);
                    }
                });
            Log(
                "hellfyre self-destruct armed delayMs="
                + HellfyreSelfDestructDelayMilliseconds
                + " character="
                + source.Identity.ToString(true));
        }

        private static void DestroyHellfyreLauncher(ICharacter source)
        {
            if (source == null
                || source.Controller?.Client == null
                || !InventoryContainerRuntimeService.Default.HasCharacterInventory(source))
            {
                return;
            }

            int removed = 0;
            foreach (KeyValuePair<int, IInventoryPage> pageEntry in source.BaseInventory.Pages)
            {
                if (pageEntry.Value == null)
                {
                    continue;
                }

                List<int> slots = new List<int>();
                foreach (KeyValuePair<int, IItem> slot in pageEntry.Value.List())
                {
                    IItem item = slot.Value;
                    if (item != null
                        && (item.LowID == HellfyreLauncherItemId || item.HighID == HellfyreLauncherItemId))
                    {
                        slots.Add(slot.Key);
                    }
                }

                for (int i = 0; i < slots.Count; i++)
                {
                    int slot = slots[i];
                    try
                    {
                        source.BaseInventory.RemoveItem(pageEntry.Key, slot);
                        CharacterActionMessageHandler.Default.SendDeleteItem(source, pageEntry.Key, slot);
                        removed++;
                    }
                    catch (Exception ex)
                    {
                        Log("hellfyre remove failed page=" + pageEntry.Key + " slot=" + slot + " err=" + ex.Message);
                    }
                }
            }

            if (removed > 0)
            {
                try
                {
                    source.BaseInventory.Write();
                    source.CalculateSkills();
                    InventoryContainerRuntimeService.Default.EnsureWeaponVisualMeshes(source, true);
                }
                catch (Exception ex)
                {
                    Log("hellfyre self-destruct finalize failed err=" + ex.Message);
                }

                ChatTextMessageHandler.Default.Send(
                    source,
                    "The Experimental Hellfyre Rocket Launcher self-destructs.");
            }

            Log(
                "hellfyre self-destruct removed="
                + removed
                + " character="
                + source.Identity.ToString(true));
        }

        private static bool HasHellfyreLauncher(ICharacter source)
        {
            if (source?.BaseInventory?.Pages == null)
            {
                return false;
            }

            foreach (IInventoryPage page in source.BaseInventory.Pages.Values)
            {
                if (page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> slot in page.List())
                {
                    IItem item = slot.Value;
                    if (item != null
                        && (item.LowID == HellfyreLauncherItemId || item.HighID == HellfyreLauncherItemId))
                    {
                        return true;
                    }
                }
            }

            return false;
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
            if (AreteSandstormMarauderRuntime.IsRegisteredMarauder(target))
            {
                return true;
            }

            // Fallback: capture name (level can be overwritten by Prepare after ApplyMarauderStats).
            return target != null
                   && string.Equals(target.Name, "SANDSTORM Marauder", StringComparison.OrdinalIgnoreCase);
        }

        private static void ArmQuell(ICharacter source)
        {
            if (source != null)
            {
                QuellArmedByCharacter.Add(source.Identity.Instance);
            }
        }

        private static void DisarmQuell(ICharacter source)
        {
            if (source != null)
            {
                QuellArmedByCharacter.Remove(source.Identity.Instance);
            }
        }

        private static bool IsQuellArmed(ICharacter source)
        {
            return source != null && QuellArmedByCharacter.Contains(source.Identity.Instance);
        }

        private static bool IsQuellPending(ICharacter source)
        {
            if (source == null || HasRewardsGranted(source) || IsReturnPending(source))
            {
                return false;
            }

            if (IsMissionCompleted(source, QuellQuestId))
            {
                return false;
            }

            return IsQuellArmed(source)
                   || HasHellfyreLauncher(source)
                   || IsMissionActive(source, QuellQuestId)
                   || IsMissionActive(source, LegacyQuellQuestId);
        }

        private static void ArmReturn(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            ReturnArmedByCharacter.Add(source.Identity.Instance);
            if (MissionRuntime.IsInitialized)
            {
                try
                {
                    MissionRuntime.Service.SetFlag(
                        source.Identity.Instance,
                        ReturnQuestId,
                        ReturnArmedFlag,
                        "1");
                }
                catch (Exception ex)
                {
                    Log("ArmReturn SetFlag failed err=" + ex.Message);
                }
            }
        }

        private static void DisarmReturn(ICharacter source)
        {
            if (source != null)
            {
                ReturnArmedByCharacter.Remove(source.Identity.Instance);
            }
        }

        private static bool IsReturnArmed(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (ReturnArmedByCharacter.Contains(source.Identity.Instance))
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
                       ReturnArmedFlag) != null;
        }

        private static bool IsReturnPending(ICharacter source)
        {
            if (source == null || HasRewardsGranted(source))
            {
                return false;
            }

            if (IsReturnArmed(source))
            {
                return true;
            }

            if ((IsMissionActive(source, ReturnQuestId) || IsMissionActive(source, LegacyReturnQuestId))
                && !IsMissionCompleted(source, ReturnQuestId))
            {
                return true;
            }

            // Quell tip completed / kill objective done → return even if Return MissionRuntime missed.
            return IsMissionCompleted(source, QuellQuestId)
                   && !IsMissionCompleted(source, ReturnQuestId);
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
