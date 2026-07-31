namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Completes one accepted RK mission. Capture <c>20260724-141302</c> Kill-Person finish order
    /// (after combat XP / loot lines): TemplateAction token → Feedback(token awarded) →
    /// FormatFeedback "Received reward…" → XP grant → TemplateAction item → Feedback(Mission accomplished)
    /// → CharacterAction(59) → Quest Delete → CharacterAction(0x2F) MissionKey.
    /// </summary>
    internal static class MissionCompleteService
    {
        private const int MissionIdentityType = 0x0000DAC3;

        // Capture finish CharacterAction before Quest Delete (Action printed as 59).
        private const int MissionCompleteAction = 59;

        // Overflow next-free slot used for mission key + token grants in live captures.
        private const int OverflowNextFreeSlot = 111;

        private const int TemplateActionUnknown1 = 1;

        private const int TemplateActionUnknown2 = 87;

        // Feedback category used by mission finish lines (capture 20260724-141302).
        private const int MissionFeedbackCategoryId = 110;

        // "You are awarded a token for your heroic effort."
        private const int TokenAwardedFeedbackMessageId = 175335076;

        // "Mission accomplished." (also used by Arete/Subway finish paths).
        private const int MissionAccomplishedFeedbackMessageId = 108871108;

        // Clan token pair from finish TemplateAction (capture 20260718-062936).
        private const int ClanTokenLowId = 103910;

        private const int ClanTokenHighId = 103911;

        // Adjacent pair in itemrelations.txt (Omni); Clan pair confirmed by capture.
        private const int OmniTokenLowId = 103908;

        private const int OmniTokenHighId = 103909;

        private static readonly object Gate = new object();

        private static readonly HashSet<string> InFlight = new HashSet<string>();

        /// <summary>
        /// Completes the most recently accepted non-expired mission for the character (if any).
        /// </summary>
        public static bool TryCompleteLatest(IZoneClient client, ICharacter character, string reason)
        {
            if (character == null)
            {
                return false;
            }

            List<MissionAcceptedStore.AcceptedMission> all =
                MissionAcceptedStore.GetAll(character.Identity.Instance);
            if (all.Count == 0)
            {
                return false;
            }

            for (int i = all.Count - 1; i >= 0; i--)
            {
                MissionAcceptedStore.AcceptedMission entry = all[i];
                if (!IsGeneratedAcceptedMission(entry))
                {
                    return TryComplete(client, character, entry, reason);
                }
            }

            return false;
        }

        /// <summary>
        /// FindPerson finish must delete the FindPerson journal row, not whatever is "latest"
        /// if the player holds multiple missions.
        /// </summary>
        public static bool TryCompleteFindPerson(IZoneClient client, ICharacter character, string reason)
        {
            if (character == null)
            {
                return false;
            }

            List<MissionAcceptedStore.AcceptedMission> all =
                MissionAcceptedStore.GetAll(character.Identity.Instance);
            if (all == null || all.Count == 0)
            {
                return false;
            }

            MissionAcceptedStore.AcceptedMission findEntry = null;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                MissionAcceptedStore.AcceptedMission candidate = all[i];
                if (candidate != null
                    && !IsGeneratedAcceptedMission(candidate)
                    && MissionTypeCatalog.TypeFromIcon(candidate.MissionIconId) == MissionRollType.FindPerson)
                {
                    findEntry = candidate;
                    break;
                }
            }

            if (findEntry == null)
            {
                return TryCompleteLatest(client, character, reason);
            }

            return TryComplete(client, character, findEntry, reason);
        }

        /// <summary>
        /// Completes when the player is inside a mission instance (LookAt / loot triggers).
        /// </summary>
        public static bool TryCompleteIfInInstance(IZoneClient client, ICharacter character, string reason)
        {
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            if (MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                character.Playfield.Identity.Instance))
            {
                return false;
            }

            if (!MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            return TryCompleteLatest(client, character, reason);
        }

        /// <summary>
        /// Completes when the designated Kill-target NPC dies inside the instance.
        /// </summary>
        public static bool TryCompleteIfMissionTargetKilled(
            ICharacter attacker,
            ICharacter victim,
            string reason)
        {
            if (MissionAcgObjectiveInteractionService.TryHandleTargetDeath(
                attacker,
                victim))
            {
                return true;
            }

            if (IsInClaimedGeneratedPlayfield(attacker)
                || IsInClaimedGeneratedPlayfield(victim)
                || (victim != null
                    && MissionAcgRuntimeInteractionService.ClaimsGeneratedRuntimeIdentity(
                        victim.Identity)))
            {
                return false;
            }

            if (attacker == null || victim == null || !MissionTargetTracker.IsMissionTarget(victim.Identity))
            {
                return false;
            }

            if (victim.Playfield == null
                || !MissionInstanceService.IsMissionInstancePlayfield(victim.Playfield.Identity.Instance))
            {
                return false;
            }

            ICharacter player = attacker;
            if (attacker.Controller == null || !(attacker.Controller is PlayerController))
            {
                return false;
            }

            var client = attacker.Controller.Client as ZoneClient;
            if (client == null)
            {
                return false;
            }

            MissionTargetTracker.Unregister(victim.Identity);

            // Find Person completes via InfoRequest — never via KillTarget (stale Kill tracker on reused PF).
            MissionRollType stamped;
            if (MissionInstanceService.TryGetStampedObjective(victim.Playfield.Identity.Instance, out stamped)
                && stamped != MissionRollType.KillPerson)
            {
                return false;
            }

            List<MissionAcceptedStore.AcceptedMission> accepted =
                MissionAcceptedStore.GetAll(player.Identity.Instance);
            if (accepted != null && accepted.Count > 0)
            {
                for (int i = accepted.Count - 1; i >= 0; i--)
                {
                    MissionAcceptedStore.AcceptedMission latest = accepted[i];
                    if (IsGeneratedAcceptedMission(latest))
                    {
                        continue;
                    }

                    if (latest != null
                        && MissionTypeCatalog.TypeFromIcon(latest.MissionIconId) != MissionRollType.KillPerson)
                    {
                        return false;
                    }

                    break;
                }
            }

            return TryCompleteLatest(client, player, reason ?? "KillTarget");
        }

        public static bool TryComplete(
            IZoneClient client,
            ICharacter character,
            MissionAcceptedStore.AcceptedMission entry,
            string reason)
        {
            if (client == null || character == null || entry == null
                || entry.QuestIdentity == null)
            {
                return false;
            }

            // Generated objectives own their durable completion journal and call it
            // directly. A generated accepted mission reaching this legacy owner is
            // therefore an explicit rejection, including when its runtime state is
            // missing or invalid.
            if (IsGeneratedAcceptedMission(entry))
            {
                return false;
            }

            string flightKey = character.Identity.Instance.ToString("X") + ":"
                               + entry.QuestIdentity.Instance.ToString("X");
            lock (Gate)
            {
                if (!InFlight.Add(flightKey))
                {
                    return false;
                }
            }

            try
            {
                int cashReward = ResolveCashReward(entry);
                int xpReward = ResolveXpReward(entry);

                // Capture 20260724-141302: token grant + "awarded a token" before Received reward.
                bool tokenEligible = MissionTokenProgressTracker.HasFullTokenChance(character.Identity.Instance);
                bool tokenGranted = false;
                if (tokenEligible)
                {
                    int tokenQl = entry.Quality > 0 ? entry.Quality : 1;
                    tokenGranted = TryGrantSideToken(character, tokenQl);
                    if (tokenGranted)
                    {
                        FeedbackMessageHandler.Default.Send(
                            character,
                            MissionFeedbackCategoryId,
                            TokenAwardedFeedbackMessageId);
                    }
                }
                else
                {
                    MissionDiagnostics.Log(
                        "TOKEN-SKIP-PCT char={0} mission={1:X8}",
                        character.Identity.Instance,
                        entry.QuestIdentity.Instance);
                }

                GrantCredits(character, cashReward);
                SendRewardFeedback(character, xpReward, cashReward);
                if (xpReward > 0)
                {
                    CombatXpRuntimeService.AwardDirectXp(
                        character,
                        xpReward,
                        "mission-complete-" + entry.QuestIdentity.Instance.ToString("X8"));
                }

                // Rolled mission ItemRewards always paid on objective complete (independent of token %).
                bool itemGranted = TryGrantOfferItemReward(client, character, entry);
                if (itemGranted)
                {
                    SendYellowFeedback(character, "You've received an item as mission reward!");
                }

                SendMissionAccomplishedFeedback(character);

                SendMissionCompleteAction(character, entry.QuestIdentity);
                SendQuestDelete(character, entry.QuestIdentity);

                int keyInstance;
                bool keyRemoved = false;
                // Prefer mission-keyed take; fall back to latest, then any template in bag.
                if (MissionKeyStore.TryTakeExactNonGenerated(
                        character.Identity.Instance,
                        entry.QuestIdentity,
                        MissionAcgBindingRuntime.IsGeneratedMissionKeyInstance,
                        out keyInstance)
                    || MissionKeyStore.TryTakeLatestNonGenerated(
                        character.Identity.Instance,
                        MissionAcgBindingRuntime.IsGeneratedMissionKeyInstance,
                        out keyInstance))
                {
                    keyRemoved = MissionKeyGrantService.TryRemoveMissionKey(client, character, keyInstance);
                }

                if (!keyRemoved)
                {
                    keyRemoved = MissionKeyGrantService.TryRemoveAnyMissionKey(client, character);
                }

                bool storeRemoved = MissionAcceptedStore.Remove(
                    character.Identity.Instance,
                    entry.QuestIdentity);

                MissionTokenProgressTracker.ClearCharacter(character.Identity.Instance);
                MissionFindItemService.ClearCharacter(character.Identity.Instance);

                MissionDiagnostics.Log(
                    "COMPLETE char={0} mission={1:X8} reason={2} cash={3} xp={4} item={5} token={6} keyRemoved={7} storeRemoved={8} pf={9}",
                    character.Identity.Instance,
                    entry.QuestIdentity.Instance,
                    reason ?? string.Empty,
                    cashReward,
                    xpReward,
                    itemGranted,
                    tokenGranted,
                    keyRemoved,
                    storeRemoved,
                    character.Playfield != null ? character.Playfield.Identity.Instance : 0);

                return true;
            }
            catch (Exception ex)
            {
                MissionDiagnostics.Log(
                    "COMPLETE-FAIL char={0} err={1}",
                    character.Identity.Instance,
                    ex.Message);
                return false;
            }
            finally
            {
                lock (Gate)
                {
                    InFlight.Remove(flightKey);
                }
            }
        }

        internal static bool IsGeneratedAcceptedMission(
            MissionAcceptedStore.AcceptedMission entry)
        {
            if (entry == null || entry.QuestIdentity == null)
            {
                return false;
            }

            if (MissionAcgAllocationService.IsGeneratedAcceptedQuestIdentity(
                (int)entry.QuestIdentity.Type,
                entry.QuestIdentity.Instance))
            {
                return true;
            }

            MissionAcgBindingRecord ignored;
            return MissionAcgBindingRuntime.TryGetByAcceptedQuest(
                entry.QuestIdentity.Instance,
                out ignored);
        }

        private static bool IsInClaimedGeneratedPlayfield(ICharacter character)
        {
            return character != null
                   && character.Playfield != null
                   && MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                       character.Playfield.Identity.Instance);
        }

        internal static int ResolveCashReward(MissionAcceptedStore.AcceptedMission entry)
        {
            if (entry == null)
            {
                return 0;
            }

            int ql = entry.Quality > 0 ? entry.Quality : 1;
            // Hard ceiling: capture-shell leftovers were ~106k on QL18; never pay that again.
            const int AbsoluteMaxCash = 150000;
            int cash = 0;

            if (entry.CashReward > 0)
            {
                cash = entry.CashReward;
            }
            else if (entry.Offer != null && entry.Offer.CashReward > 0)
            {
                cash = entry.Offer.CashReward;
            }
            else
            {
                cash = MissionRollService.BaseCashForMissionQl(ql);
            }

            int maxCash = MissionRollService.BaseCashForMissionQl(ql) * 2;
            if (maxCash > AbsoluteMaxCash)
            {
                maxCash = AbsoluteMaxCash;
            }

            if (cash > maxCash || cash > AbsoluteMaxCash)
            {
                cash = MissionRollService.BaseCashForMissionQl(ql);
            }

            return cash;
        }

        internal static int ResolveXpReward(MissionAcceptedStore.AcceptedMission entry)
        {
            if (entry == null)
            {
                return 0;
            }

            int ql = entry.Quality > 0 ? entry.Quality : 1;
            // Hard ceiling: capture-shell leftovers were ~20M XP and leveled 25→45 in one mish.
            const int AbsoluteMaxXp = 2500000;
            int xp;

            if (entry.ExperienceReward > 0 || entry.CashReward > 0)
            {
                xp = entry.ExperienceReward;
            }
            else if (entry.Offer != null)
            {
                xp = entry.Offer.ExperienceReward;
            }
            else
            {
                return 0;
            }

            int maxXp = MissionRollService.BaseXpForMissionQl(ql) * 2;
            if (maxXp > AbsoluteMaxXp)
            {
                maxXp = AbsoluteMaxXp;
            }

            if (xp > maxXp || xp > AbsoluteMaxXp)
            {
                // Recompute balanced mid-slider XP for stamped QL (ignore shell).
                xp = MissionRollService.BaseXpForMissionQl(ql);
            }

            return xp;
        }

        /// <summary>
        /// Always grants the rolled offer ItemRewards on complete (0 kills still pays).
        /// Independent of token % progress.
        /// </summary>
        internal static bool TryGrantOfferItemReward(
            IZoneClient client,
            ICharacter character,
            MissionAcceptedStore.AcceptedMission entry)
        {
            int ignored;
            return TryGrantOfferItemReward(
                client,
                character,
                entry,
                0,
                out ignored);
        }

        internal static bool TryGrantOfferItemReward(
            IZoneClient client,
            ICharacter character,
            MissionAcceptedStore.AcceptedMission entry,
            int reservedItemInstance,
            out int grantedItemInstance)
        {
            grantedItemInstance = 0;
            if (entry == null || entry.Offer == null || entry.Offer.ItemRewards == null
                || entry.Offer.ItemRewards.Length == 0)
            {
                return false;
            }

            QuestItemShort reward = entry.Offer.ItemRewards[0];
            if (reward == null || reward.LowId <= 0)
            {
                return false;
            }

            int highId = reward.HighId > 0 ? reward.HighId : reward.LowId;
            int ql = reward.Quality > 0 ? reward.Quality : (entry.Quality > 0 ? entry.Quality : 1);
            string name = "Mission Reward";
            int itemInstance;
            InventoryError error;
            bool ok =
                reservedItemInstance == 0
                    ? MissionKeyGrantService.TryGrantNamedItem(
                        client,
                        character,
                        reward.LowId,
                        highId,
                        ql,
                        name,
                        out itemInstance,
                        out error)
                    : MissionKeyGrantService.TryGrantReservedNamedItem(
                        client,
                        character,
                        reward.LowId,
                        highId,
                        ql,
                        name,
                        reservedItemInstance,
                        out itemInstance,
                        out error);
            MissionDiagnostics.Log(
                "COMPLETE-ITEM char={0} ok={1} low={2} high={3} ql={4} err={5}",
                character.Identity.Instance,
                ok,
                reward.LowId,
                highId,
                ql,
                error);
            grantedItemInstance = ok ? itemInstance : 0;
            return ok;
        }

        internal static void GrantCredits(ICharacter character, int cashReward)
        {
            if (cashReward <= 0 || character == null)
            {
                return;
            }

            long before = character.Stats[StatIds.cash].BaseValue;
            if (before < 0)
            {
                before = 0;
            }

            long after = before + cashReward;
            if (after > int.MaxValue)
            {
                after = int.MaxValue;
            }

            character.Stats[StatIds.cash].Set((uint)after);
            // Gold finish: cash/XP/feedback land immediately inside the instance — not on zone.
            if (character.Controller != null && character.Controller.Client != null)
            {
                character.Controller.Client.SendCompressed(
                    new StatMessage
                    {
                        Identity = character.Identity,
                        Stats = new[]
                                {
                                    new GameTuple<CharacterStat, uint>
                                    {
                                        Value1 = (CharacterStat)StatIds.cash,
                                        Value2 = (uint)after
                                    }
                                }
                    });
            }
            else
            {
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)after);
            }
        }

        internal static void SendRewardFeedback(ICharacter character, int xp, int cash)
        {
            SendYellowFeedback(
                character,
                string.Format("Received reward: {0} XP, {1} credits.", xp, cash));
        }

        internal static void SendMissionAccomplishedFeedback(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            var message = new FeedbackMessage
                          {
                              Identity = character.Identity,
                              Unknown = 1,
                              Unknown1 = 0,
                              CategoryId = MissionFeedbackCategoryId,
                              MessageId = MissionAccomplishedFeedbackMessageId
                          };
            if (character.Controller != null && character.Controller.Client != null)
            {
                character.Controller.Client.SendCompressed(message);
            }
            else
            {
                FeedbackMessageHandler.Default.Send(
                    character,
                    MissionFeedbackCategoryId,
                    MissionAccomplishedFeedbackMessageId);
            }
        }

        internal static void SendYellowFeedback(ICharacter character, string plainText)
        {
            if (character == null || string.IsNullOrEmpty(plainText))
            {
                return;
            }

            // Must SendCompressed on the zone client — character.Send can sit buffered until zone.
            // Gold finish shows FormatFeedback immediately on InfoRequest inside the instance.
            var message = new FormatFeedbackMessage
                          {
                              Identity = character.Identity,
                              Unknown = 1,
                              Unknown1 = 0,
                              Unknown2 = 0,
                              FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(plainText)
                          };
            if (character.Controller != null && character.Controller.Client != null)
            {
                character.Controller.Client.SendCompressed(message);
            }
            else
            {
                character.Send(message);
            }
        }

        private static bool TryGrantSideToken(ICharacter character, int quality)
        {
            Side side = (Side)character.Stats[StatIds.side].Value;
            int lowId;
            int highId;
            string tokenName;
            if (side == Side.Clan)
            {
                lowId = ClanTokenLowId;
                highId = ClanTokenHighId;
                tokenName = "Clan Token";
            }
            else if (side == Side.Omni)
            {
                lowId = OmniTokenLowId;
                highId = OmniTokenHighId;
                tokenName = "Omni Token";
            }
            else
            {
                MissionDiagnostics.Log(
                    "TOKEN-SKIP char={0} side={1} (neutral/other → no token)",
                    character.Identity.Instance,
                    side);
                return false;
            }

            int count = MissionLevelTable.GetTokenReward(character.Stats[StatIds.level].Value);
            if (count <= 0)
            {
                count = 1;
            }

            // Same finish-wire grant that already delivers mission reward items in-instance.
            IZoneClient client = character.Controller != null
                                     ? character.Controller.Client as IZoneClient
                                     : null;
            if (client == null)
            {
                MissionDiagnostics.Log(
                    "TOKEN-INV-FAIL char={0} side={1} err=no-client",
                    character.Identity.Instance,
                    side);
                return false;
            }

            int tokenQl = 1;
            int itemInstance;
            InventoryError error;
            bool ok = MissionKeyGrantService.TryGrantNamedItem(
                client,
                character,
                lowId,
                highId,
                tokenQl,
                tokenName,
                out itemInstance,
                out error);
            if (ok && count > 1)
            {
                // Stack extras on the granted slot when level table awards >1.
                try
                {
                    foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
                    {
                        foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List())
                        {
                            IItem item = itemEntry.Value;
                            if (item != null && item.Identity != null
                                && item.Identity.Instance == itemInstance)
                            {
                                item.MultipleCount = count;
                                character.BaseInventory.Write();
                                break;
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            MissionDiagnostics.Log(
                "TOKEN-GRANT char={0} side={1} low={2} high={3} ql={4} count={5} invOk={6} err={7}",
                character.Identity.Instance,
                side,
                lowId,
                highId,
                tokenQl,
                count,
                ok,
                error);
            return ok;
        }

        internal static void SendMissionCompleteAction(ICharacter character, Identity mission)
        {
            var missionId = new Identity
                            {
                                Type = mission.Type != 0
                                           ? mission.Type
                                           : (IdentityType)MissionIdentityType,
                                Instance = mission.Instance
                            };

            var message = new CharacterActionMessage
                          {
                              Identity = character.Identity,
                              Unknown = 0,
                              Action = (CharacterActionType)MissionCompleteAction,
                              Unknown1 = 0,
                              Target = missionId,
                              Parameter1 = MissionIdentityType,
                              Parameter2 = unchecked((int)missionId.Instance),
                              Unknown2 = 0
                          };
            if (character.Controller != null && character.Controller.Client != null)
            {
                character.Controller.Client.SendCompressed(message);
            }
            else
            {
                character.Send(message);
            }
        }

        internal static void SendQuestDelete(ICharacter character, Identity mission)
        {
            var message = new QuestMessage
                          {
                              Identity = character.Identity,
                              Unknown = 0,
                              Action = QuestAction.Delete,
                              Unknown1 = 0,
                              Mission = new Identity
                                        {
                                            Type = mission.Type != 0
                                                       ? mission.Type
                                                       : (IdentityType)MissionIdentityType,
                                            Instance = mission.Instance
                                        },
                              Unknown2 = 0,
                              Unknown3 = 0
                          };
            if (character.Controller != null && character.Controller.Client != null)
            {
                character.Controller.Client.SendCompressed(message);
            }
            else
            {
                character.Send(message);
            }
        }
    }
}
