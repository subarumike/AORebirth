namespace ZoneEngine.Core.Thrak.Quests
{
    #region Usings ...

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

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Multi-NPC KnuBot trade adapter for Thrak garden key chain (capture 20260718-185306).
    /// Hyp shows Ancient Device (214998): return the same device (Unknown2=1). Player then combines
    /// Insignia + Ancient Device → favored Ancient Pattern Analyzer (214785).
    /// </summary>
    internal static class ThrakGardenKeyTradeAdapter
    {
        private enum TradeKind
        {
            None = 0,
            ProphetDevice = 1,
            ProphetInsignia = 2,
            HypAnalyzer = 3,
            HypReturn = 4,
            Silvertail = 5
        }

        private static readonly Dictionary<int, ThrakTradeSession> SessionsByCharacter =
            new Dictionary<int, ThrakTradeSession>();

        private static readonly object SyncRoot = new object();

        internal static void BeginTrade(ICharacter source, Identity npcIdentity, string kind)
        {
            if (source == null || source.Identity.Instance <= 0 || npcIdentity == Identity.None)
            {
                return;
            }

            TradeKind parsed = ParseKind(kind);
            if (parsed == TradeKind.None)
            {
                return;
            }

            if ((parsed == TradeKind.HypAnalyzer || parsed == TradeKind.HypReturn)
                && !ThrakGardenKeyQuestRuntime.CanTalkToHypnagogic(source))
            {
                return;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[source.Identity.Instance] = new ThrakTradeSession
                                                               {
                                                                   NpcIdentity = npcIdentity,
                                                                   Kind = parsed
                                                               };
            }
        }

        internal static bool IsThrakTradeNpc(ICharacter source, Identity npcIdentity)
        {
            return ResolveNpcName(source, npcIdentity) != null
                   || ThrakGardenKeyInteractionRules.IsProphet(npcIdentity)
                   || ThrakGardenKeyInteractionRules.IsHypnagogic(npcIdentity)
                   || ThrakGardenKeyInteractionRules.IsDreamingSilvertail(npcIdentity);
        }

        internal static bool TryStageTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            ThrakTradeSession session = GetSession(source);
            if (session == null)
            {
                if (!IsThrakTradeNpc(source, message.Target))
                {
                    return false;
                }

                BeginTrade(source, message.Target, InferKindFromTarget(source, message.Target));
                session = GetSession(source);
            }

            if (session == null)
            {
                return IsThrakTradeNpc(source, message.Target);
            }

            // Live outdoor/garden spawns rematch by identity after BindRegistration; accept either.
            if (!IdentitiesEqual(session.NpcIdentity, message.Target))
            {
                session.NpcIdentity = message.Target;
            }

            IItem item;
            try
            {
                item = InventoryContainerRuntimeService.Default.GetKnuBotTradeItem(
                    source,
                    message.Container.Type,
                    message.Container.Instance);
            }
            catch (Exception)
            {
                // Still claim the trade so HandleKnuBotTradeItemRemove cannot delete the item.
                return true;
            }

            int itemId = ResolveItemId(item);
            lock (SyncRoot)
            {
                session.StagedItemId = itemId;
                session.StagedContainer = message.Container;
                session.StagedQuality = item != null && item.Quality > 0 ? item.Quality : 1;
                RefineKindFromItem(session, itemId);
            }

            return true;
        }

        internal static bool TryFinishTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            if (!IsThrakTradeNpc(source, message.Target))
            {
                ThrakTradeSession existing = GetSession(source);
                if (existing == null || !IdentitiesEqual(existing.NpcIdentity, message.Target))
                {
                    return false;
                }
            }

            if (message.Decline != 0)
            {
                SendRejectedItems(source, message.Target);
                ClearSession(source);
                return true;
            }

            ThrakTradeSession session = GetSession(source);
            if (session == null)
            {
                BeginTrade(source, message.Target, InferKindFromTarget(source, message.Target));
                session = GetSession(source);
            }

            if (session == null)
            {
                SendRejectedItems(source, message.Target);
                return true;
            }

            session.NpcIdentity = message.Target;

            int itemId = session.StagedItemId;
            if (itemId == 0)
            {
                itemId = FindExpectedItemInInventory(source, session.Kind);
            }

            RefineKindFromItem(session, itemId);

            bool ok = ApplyTrade(source, session, itemId);
            int soulsBeforeSilvertail = session.Kind == TradeKind.Silvertail
                                            ? ThrakGardenKeyQuestRuntime.GetSoulCount(source)
                                            : 0;
            if (ok && IsInspectionKeepItemTrade(session.Kind))
            {
                // Keep offered Ancient Device / Insignia in inventory; repair if Remove already ate it.
                EnsureKeptItemPresent(source, session, itemId);
            }

            SendRejectedItems(source, session.NpcIdentity, session.Kind, soulsBeforeSilvertail, itemId, session.StagedQuality);
            if (ok)
            {
                if (session.Kind == TradeKind.HypReturn)
                {
                    // Capture 20260821-225658: RejectedItems Unknown2=0, then TemplateAction
                    // 226994 (garden key), then 214785 (full/favored analyzer).
                    ThrakGardenKeyQuestRuntime.TryGrantGardenKey(source);
                    ThrakGardenKeyQuestRuntime.TryGrantFavoredAnalyzer(source);
                }

                if (session.Kind == TradeKind.Silvertail)
                {
                    // Capture 20260821-225658: souls 1–2 keep favored via RejectedItems Unknown2=1;
                    // 3rd soul Unknown2=0 then TemplateAction 214783 (empty/inspected analyzer).
                    int count = ThrakGardenKeyQuestRuntime.IncrementSoulCount(source);
                    if (count >= 3)
                    {
                        ThrakGardenKeyQuestRuntime.TryForceReturnInspectedAnalyzer(source);
                    }
                    else
                    {
                        ThrakGardenKeyQuestRuntime.TryForceReturnFavoredAnalyzer(source);
                    }

                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "ThrakGardenKey Silvertail trade souls=" + count
                        + " by=" + source.Identity.ToString(true));
                }

                if (session.Kind == TradeKind.ProphetDevice)
                {
                    // Capture RejectedItems returns device; force TemplateAction if client still missing it.
                    ThrakGardenKeyQuestRuntime.TryRestoreAncientDeviceIfMissing(source);
                }

                // Capture: RejectedItems then next KnubotAnswerList (advance past trade-hold node).
                ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, session.NpcIdentity);
                if (session.Kind == TradeKind.HypAnalyzer)
                {
                    // Capture @16:59:06: Insignia of Thrak (214789).
                    // Always re-grant Ancient Device with TemplateAction so client inventory shows it
                    // for Insignia + Device → favored analyzer (214785) combine.
                    ThrakGardenKeyQuestRuntime.TryGrantInsignia(source);
                    ThrakGardenKeyQuestRuntime.TryForceReturnAncientDevice(source);
                }
            }

            ClearSession(source);
            return true;
        }

        private static bool IsInspectionKeepItemTrade(TradeKind kind)
        {
            // Return the offered item to the player (Hyp Ancient Device, Prophet inspect, Silvertail).
            return kind == TradeKind.ProphetDevice
                   || kind == TradeKind.ProphetInsignia
                   || kind == TradeKind.HypAnalyzer
                   || kind == TradeKind.Silvertail;
        }

        private static void EnsureKeptItemPresent(
            ICharacter source,
            ThrakTradeSession session,
            int itemId)
        {
            if (source == null || itemId <= 0)
            {
                return;
            }

            if (itemId == ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId
                || itemId == ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId
                || itemId == ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId)
            {
                if (ThrakGardenKeyQuestRuntime.HasAnalyzer(source))
                {
                    return;
                }

                ThrakGardenKeyQuestRuntime.TryRestoreItem(
                    source,
                    itemId,
                    session != null && session.StagedQuality > 0 ? session.StagedQuality : 1);
                return;
            }

            if (itemId == ThrakGardenKeyInteractionRules.InsigniaOfThrakItemId)
            {
                if (ThrakGardenKeyQuestRuntime.HasInsignia(source))
                {
                    return;
                }

                ThrakGardenKeyQuestRuntime.TryRestoreItem(
                    source,
                    itemId,
                    session != null && session.StagedQuality > 0 ? session.StagedQuality : 1);
            }
        }

        private static void RefineKindFromItem(ThrakTradeSession session, int itemId)
        {
            if (session == null || itemId <= 0)
            {
                return;
            }

            if (session.Kind == TradeKind.ProphetDevice || session.Kind == TradeKind.ProphetInsignia)
            {
                if (itemId == ThrakGardenKeyInteractionRules.InsigniaOfThrakItemId)
                {
                    session.Kind = TradeKind.ProphetInsignia;
                }
                else if (itemId == ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId
                         || itemId == ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId
                         || itemId == ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId)
                {
                    session.Kind = TradeKind.ProphetDevice;
                }
            }
        }

        private static bool ApplyTrade(ICharacter source, ThrakTradeSession session, int itemId)
        {
            switch (session.Kind)
            {
                case TradeKind.ProphetDevice:
                    if (itemId != ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId
                        && itemId != ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId
                        && itemId != ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId)
                    {
                        return false;
                    }

                    // Inspection only — item stays with player (capture RejectedItems empty + continue dialogue).
                    ThrakGardenKeyQuestRuntime.MarkProphetDeviceInspected(source);
                    return true;

                case TradeKind.ProphetInsignia:
                    if (itemId != ThrakGardenKeyInteractionRules.InsigniaOfThrakItemId)
                    {
                        return false;
                    }

                    // Capture RejectedItems Unknown2=1 empty: insignia stays with player for statue use.
                    // Hyp later grants another 214789 for the combine step if needed.
                    ThrakGardenKeyQuestRuntime.CompleteQuest(
                        source,
                        ThrakGardenKeyInteractionRules.QuestInsignia);
                    ThrakGardenKeyQuestRuntime.AcceptQuest(
                        source,
                        ThrakGardenKeyInteractionRules.QuestGarden);
                    return true;

                case TradeKind.HypAnalyzer:
                    if (itemId != ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId
                        && itemId != ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId
                        && itemId != ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId)
                    {
                        return false;
                    }

                    // Inspection only — return the same Ancient Device. Player combines it with
                    // Insignia of Thrak to craft favored Ancient Pattern Analyzer (214785).
                    ThrakGardenKeyQuestRuntime.CompleteQuest(
                        source,
                        ThrakGardenKeyInteractionRules.QuestGarden);
                    ThrakGardenKeyQuestRuntime.AcceptQuest(
                        source,
                        ThrakGardenKeyInteractionRules.QuestSouls);
                    return true;

                case TradeKind.HypReturn:
                    if (itemId != ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId
                        && itemId != ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId
                        && itemId != ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId)
                    {
                        return false;
                    }

                    TryConsumeItem(source, itemId, session.StagedContainer);
                    ThrakGardenKeyQuestRuntime.CompleteQuest(
                        source,
                        ThrakGardenKeyInteractionRules.QuestReturn);
                    // Belt-and-suspenders: wipe VeronicaUpdated / Garden leftovers from the journal.
                    ThrakGardenKeyQuestRuntime.ClearFinishedThrakChainJournal(source);
                    // Key + favored analyzer TemplateActions are sent after RejectedItems
                    // (capture 20260821-225658: 226994 then 214785).
                    return true;

                case TradeKind.Silvertail:
                    if (itemId != ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId
                        && itemId != ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId
                        && itemId != ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId)
                    {
                        return false;
                    }

                    // Device is not consumed. Capture: despawn Dreaming, spawn Cursed, aggro.
                    // Soul quest advances after RejectedItems in TryFinishTrade.
                    return ThrakGardenKeySilvertailTransform.TryCurseAndAggro(source, session.NpcIdentity);

                default:
                    return false;
            }
        }

        private static bool TryConsumeItem(ICharacter source, int itemId, Identity stagedContainer)
        {
            if (source == null || itemId <= 0 || source.BaseInventory == null)
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
                        && (staged.LowID == itemId || staged.HighID == itemId))
                    {
                        stagedPage.Remove(stagedContainer.Instance);
                        try
                        {
                            if (source.BaseInventory.Write())
                            {
                                NotifyItemRemoved(source, stagedContainer);
                                return true;
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
                            NotifyItemRemoved(
                                source,
                                new Identity { Type = (IdentityType)pageEntry.Key, Instance = entry.Key });
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                    }

                    page.Add(entry.Key, item);
                    return false;
                }
            }

            return false;
        }

        private static void NotifyItemRemoved(ICharacter source, Identity location)
        {
            try
            {
                CharacterActionMessageHandler.Default.SendDeleteItem(
                    source,
                    (int)location.Type,
                    location.Instance);
            }
            catch (Exception)
            {
            }
        }

        private static string ResolveNpcName(ICharacter source, Identity identity)
        {
            try
            {
                if (source != null && source.Playfield != null)
                {
                    ICharacter onPf = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, identity);
                    if (onPf != null && ThrakGardenKeyInteractionRules.IsThrakQuestNpcName(onPf.Name))
                    {
                        return onPf.Name;
                    }

                    foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>(source.Playfield.Identity))
                    {
                        if (character == null || character.Identity.Instance != identity.Instance)
                        {
                            continue;
                        }

                        if (ThrakGardenKeyInteractionRules.IsThrakQuestNpcName(character.Name))
                        {
                            return character.Name;
                        }
                    }
                }

                foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected))
                {
                    if (character == null || character.Identity.Instance != identity.Instance)
                    {
                        continue;
                    }

                    if (ThrakGardenKeyInteractionRules.IsThrakQuestNpcName(character.Name))
                    {
                        return character.Name;
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static string InferKindFromTarget(ICharacter source, Identity target)
        {
            string name = ResolveNpcName(source, target);
            if (string.Equals(name, ThrakGardenKeyInteractionRules.ProphetName, StringComparison.OrdinalIgnoreCase)
                || ThrakGardenKeyInteractionRules.IsProphet(target))
            {
                // Insignia trade only after device inspect + QuestInsignia Active.
                // HasInsignia alone must not infer insignia trade — device inspect comes first.
                return ThrakGardenKeyQuestRuntime.IsMissionActive(
                           source,
                           ThrakGardenKeyInteractionRules.QuestInsignia)
                       && ThrakGardenKeyQuestRuntime.HasProphetDeviceInspected(source)
                           ? "ProphetInsignia"
                           : "ProphetDevice";
            }

            if (string.Equals(name, ThrakGardenKeyInteractionRules.HypnagogicName, StringComparison.OrdinalIgnoreCase)
                || ThrakGardenKeyInteractionRules.IsHypnagogic(target))
            {
                if (!ThrakGardenKeyQuestRuntime.CanTalkToHypnagogic(source))
                {
                    return string.Empty;
                }

                return ThrakGardenKeyQuestRuntime.GetSoulCount(source) >= 3
                       || ThrakGardenKeyQuestRuntime.IsMissionActive(
                           source,
                           ThrakGardenKeyInteractionRules.QuestReturn)
                           ? "HypReturn"
                           : "HypAnalyzer";
            }

            if (string.Equals(
                    name,
                    ThrakGardenKeyInteractionRules.DreamingSilvertailName,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    name,
                    ThrakGardenKeyInteractionRules.CursedSilvertailName,
                    StringComparison.OrdinalIgnoreCase)
                || ThrakGardenKeyInteractionRules.IsDreamingSilvertail(target))
            {
                return "Silvertail";
            }

            return string.Empty;
        }

        private static int FindExpectedItemInInventory(ICharacter source, TradeKind kind)
        {
            switch (kind)
            {
                case TradeKind.ProphetDevice:
                case TradeKind.HypAnalyzer:
                    if (ThrakGardenKeyQuestRuntime.HasAnalyzer(source))
                    {
                        return ThrakGardenKeyQuestRuntime.HasFavoredAnalyzer(source)
                                   ? ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId
                                   : ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId;
                    }

                    break;
                case TradeKind.ProphetInsignia:
                    if (ThrakGardenKeyQuestRuntime.HasInsignia(source))
                    {
                        return ThrakGardenKeyInteractionRules.InsigniaOfThrakItemId;
                    }

                    break;
                case TradeKind.HypReturn:
                case TradeKind.Silvertail:
                    if (ThrakGardenKeyQuestRuntime.HasFavoredAnalyzer(source))
                    {
                        return ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId;
                    }

                    if (ThrakGardenKeyQuestRuntime.HasAnalyzer(source))
                    {
                        return ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId;
                    }

                    break;
            }

            return 0;
        }

        private static void SendRejectedItems(
            ICharacter source,
            Identity npcIdentity,
            TradeKind kind,
            int soulsBeforeSilvertail = 0,
            int returnedItemId = 0,
            int returnedQuality = 1)
        {
            // Unknown2=1 returns offered items to the player inventory UI (Prophet + Hyp Ancient Device).
            // Hyp return trade uses Unknown2=0 then re-grants favored analyzer (214785).
            // Silvertail: Unknown2=1 for souls 1–2; Unknown2=0 on the 3rd claim (capture @17:00:29).
            // Capture Prophet/Silvertail Unknown2=1 packets INCLUDE the item (not empty Items[]).
            int unknown2 = 1;
            if (kind == TradeKind.HypReturn)
            {
                unknown2 = 0;
            }
            else if (kind == TradeKind.Silvertail && soulsBeforeSilvertail >= 2)
            {
                unknown2 = 0;
            }
            else if (kind == TradeKind.HypAnalyzer)
            {
                unknown2 = 0;
            }

            Item[] items = new Item[0];
            if (unknown2 == 1 && returnedItemId > 0)
            {
                int ql = returnedQuality > 0 ? returnedQuality : 1;
                items = new[] { new Item(ql, returnedItemId, returnedItemId) };
            }

            KnuBotRejectedItemsMessageHandler.Default.Send(source, npcIdentity, items, unknown2);
        }

        private static void SendRejectedItems(ICharacter source, Identity npcIdentity)
        {
            SendRejectedItems(source, npcIdentity, TradeKind.None, 0, 0, 1);
        }

        private static TradeKind ParseKind(string kind)
        {
            if (string.Equals(kind, "ProphetDevice", StringComparison.OrdinalIgnoreCase))
            {
                return TradeKind.ProphetDevice;
            }

            if (string.Equals(kind, "ProphetInsignia", StringComparison.OrdinalIgnoreCase))
            {
                return TradeKind.ProphetInsignia;
            }

            if (string.Equals(kind, "HypAnalyzer", StringComparison.OrdinalIgnoreCase))
            {
                return TradeKind.HypAnalyzer;
            }

            if (string.Equals(kind, "HypReturn", StringComparison.OrdinalIgnoreCase))
            {
                return TradeKind.HypReturn;
            }

            if (string.Equals(kind, "Silvertail", StringComparison.OrdinalIgnoreCase))
            {
                return TradeKind.Silvertail;
            }

            return TradeKind.None;
        }

        private static int ResolveItemId(IItem item)
        {
            if (item == null)
            {
                return 0;
            }

            int low = item.LowID;
            int high = item.HighID;
            if (low == ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId
                || high == ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId)
            {
                return ThrakGardenKeyInteractionRules.AncientPatternAnalyzerItemId;
            }

            if (low == ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId
                || high == ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId)
            {
                return ThrakGardenKeyInteractionRules.InspectedAncientPatternAnalyzerItemId;
            }

            if (low == ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId
                || high == ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId)
            {
                return ThrakGardenKeyInteractionRules.FavoredAncientPatternAnalyzerItemId;
            }

            if (low == ThrakGardenKeyInteractionRules.InsigniaOfThrakItemId
                || high == ThrakGardenKeyInteractionRules.InsigniaOfThrakItemId)
            {
                return ThrakGardenKeyInteractionRules.InsigniaOfThrakItemId;
            }

            return low > 0 ? low : high;
        }

        private static ThrakTradeSession GetSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                ThrakTradeSession session;
                return SessionsByCharacter.TryGetValue(source.Identity.Instance, out session)
                           ? session
                           : null;
            }
        }

        private static void ClearSession(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter.Remove(source.Identity.Instance);
            }
        }

        private static bool IdentitiesEqual(Identity a, Identity b)
        {
            return a.Type == b.Type && a.Instance == b.Instance;
        }

        private sealed class ThrakTradeSession
        {
            public Identity NpcIdentity;
            public TradeKind Kind;
            public int StagedItemId;
            public int StagedQuality;
            public Identity StagedContainer;
        }
    }
}
