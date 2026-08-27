namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Thrak.Quests;

    #endregion

    /// <summary>
    /// KnuBot trade adapter for Ecclesiast Aban Fala (capture 20260822-224319).
    /// Ancient Device inspect on Journey?; Insignia of Aban turn-in before garden quest.
    /// </summary>
    internal static class NascenceAbanFalaTradeAdapter
    {
        private enum TradeKind
        {
            None = 0,
            AncientDevice = 1,
            Insignia = 2,
            LuxWeiAncientDevice = 3,
            LuxWeiActivatedArtifact = 4,
            Silvertail = 5
        }

        private static readonly Dictionary<int, FalaTradeSession> SessionsByCharacter =
            new Dictionary<int, FalaTradeSession>();

        private static readonly object SyncRoot = new object();

        internal static void BeginDeviceTrade(ICharacter source, Identity npcIdentity, string dialogueSourceNodeId = null)
        {
            BeginTrade(source, npcIdentity, TradeKind.AncientDevice, dialogueSourceNodeId);
        }

        internal static void BeginInsigniaTrade(ICharacter source, Identity npcIdentity)
        {
            BeginTrade(source, npcIdentity, TradeKind.Insignia);
        }

        internal static void BeginLuxWeiDeviceTrade(ICharacter source, Identity npcIdentity)
        {
            BeginTrade(source, npcIdentity, TradeKind.LuxWeiAncientDevice);
        }

        internal static void BeginLuxWeiActivatedArtifactTrade(ICharacter source, Identity npcIdentity)
        {
            BeginTrade(source, npcIdentity, TradeKind.LuxWeiActivatedArtifact);
        }

        internal static void BeginSilvertailTrade(ICharacter source, Identity npcIdentity)
        {
            BeginTrade(source, npcIdentity, TradeKind.Silvertail);
        }

        internal static bool IsAbanChainTradeNpc(ICharacter source, Identity npcIdentity)
        {
            return IsFalaTradeNpc(source, npcIdentity)
                   || IsLuxWeiTradeNpc(source, npcIdentity)
                   || IsAbanSilvertailTradeNpc(source, npcIdentity);
        }

        internal static bool IsAbanSilvertailTradeNpc(ICharacter source, Identity npcIdentity)
        {
            if (!IsDreamingSilvertail(source, npcIdentity))
            {
                return false;
            }

            FalaTradeSession session = GetSession(source);
            if (session != null && session.Kind == TradeKind.Silvertail)
            {
                return true;
            }

            return NascenceAbanFalaQuestRuntime.CanUseSilvertailSoulTrade(source);
        }

        private static bool IsDreamingSilvertail(ICharacter source, Identity npcIdentity)
        {
            ICharacter npc = ResolveNpcByName(source, npcIdentity, NascenceAbanFalaInteractionRules.DreamingSilvertailName);
            return npc != null;
        }

        private static void BeginTrade(ICharacter source, Identity npcIdentity, TradeKind kind, string dialogueSourceNodeId = null)
        {
            if (source == null || source.Identity.Instance <= 0 || npcIdentity == Identity.None || kind == TradeKind.None)
            {
                return;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[source.Identity.Instance] = new FalaTradeSession
                                                                {
                                                                    NpcIdentity = npcIdentity,
                                                                    Kind = kind,
                                                                    DialogueSourceNodeId = dialogueSourceNodeId
                                                                };
            }
        }

        internal static string ResolveDeviceTradePostNodeId(ICharacter source)
        {
            FalaTradeSession session = GetSession(source);
            return NascenceAbanFalaQuestRuntime.ResolveDeviceTradePostNodeId(
                source,
                session == null ? null : session.DialogueSourceNodeId);
        }

        internal static bool IsFalaTradeNpc(ICharacter source, Identity npcIdentity)
        {
            return NascenceAbanFalaInteractionRules.IsFala(npcIdentity)
                   || ResolveFala(source, npcIdentity) != null;
        }

        internal static bool IsLuxWeiTradeNpc(ICharacter source, Identity npcIdentity)
        {
            return NascenceAbanFalaInteractionRules.IsLuxWei(npcIdentity)
                   || ResolveLuxWei(source, npcIdentity) != null;
        }

        /// <summary>
        /// Dreaming Silvertail is shared with the Thrak garden-key chain; Aban must claim staging first.
        /// </summary>
        internal static bool ShouldClaimTradeBeforeThrak(ICharacter source, KnuBotTradeMessage message)
        {
            if (source == null || message == null)
            {
                return false;
            }

            if (GetSession(source) != null)
            {
                return true;
            }

            if (IsFalaTradeNpc(source, message.Target)
                || IsLuxWeiTradeNpc(source, message.Target))
            {
                return true;
            }

            return IsAbanSilvertailTradeNpc(source, message.Target);
        }

        internal static bool TryStageTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            FalaTradeSession session = GetSession(source);
            if (session == null)
            {
                if (IsLuxWeiTradeNpc(source, message.Target))
                {
                    TradeKind luxKind = ResolveLuxWeiTradeKind(source);
                    session = new FalaTradeSession
                                {
                                    NpcIdentity = message.Target,
                                    Kind = luxKind
                                };
                    lock (SyncRoot)
                    {
                        SessionsByCharacter[source.Identity.Instance] = session;
                    }
                }
                else if (IsAbanSilvertailTradeNpc(source, message.Target))
                {
                    session = new FalaTradeSession
                                {
                                    NpcIdentity = message.Target,
                                    Kind = TradeKind.Silvertail
                                };
                    lock (SyncRoot)
                    {
                        SessionsByCharacter[source.Identity.Instance] = session;
                    }
                }
                else if (IsFalaTradeNpc(source, message.Target))
                {
                    session = new FalaTradeSession
                                {
                                    NpcIdentity = message.Target,
                                    Kind = TradeKind.AncientDevice
                                };
                    lock (SyncRoot)
                    {
                        SessionsByCharacter[source.Identity.Instance] = session;
                    }
                }
                else
                {
                    return false;
                }
            }

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
                return true;
            }

            int itemId = ResolveItemId(item);
            lock (SyncRoot)
            {
                session.StagedItemId = itemId;
                session.StagedContainer = message.Container;
                session.StagedQuality = item != null && item.Quality > 0 ? item.Quality : 1;
                RefineKindFromItem(source, session, itemId);
            }

            return true;
        }

        internal static bool TryFinishTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            if (!IsAbanChainTradeNpc(source, message.Target))
            {
                FalaTradeSession existing = GetSession(source);
                if (existing == null || !IdentitiesEqual(existing.NpcIdentity, message.Target))
                {
                    return false;
                }
            }

            if (message.Decline != 0)
            {
                SendRejectedItems(source, message.Target, 0, 1, 1);
                ClearSession(source);
                return true;
            }

            FalaTradeSession session = GetSession(source);
            if (session == null)
            {
                TradeKind inferredKind = InferTradeKind(source, message.Target);
                BeginTrade(source, message.Target, inferredKind);
                session = GetSession(source);
            }

            if (session == null)
            {
                SendRejectedItems(source, message.Target, 0, 1, 1);
                return true;
            }

            session.NpcIdentity = message.Target;

            int itemId = session.StagedItemId;
            if (itemId == 0)
            {
                itemId = FindExpectedItem(source, session.Kind);
            }

            RefineKindFromItem(source, session, itemId);

            bool ok = ApplyTrade(source, session, itemId);
            int quality = session.StagedQuality > 0 ? session.StagedQuality : 1;
            bool consumeTrade = ok && session.Kind == TradeKind.LuxWeiActivatedArtifact;
            bool luxWeiInspect = ok && session.Kind == TradeKind.LuxWeiAncientDevice;
            bool falaDeviceInspect = ok && session.Kind == TradeKind.AncientDevice;
            bool falaInsigniaTurnIn = ok && session.Kind == TradeKind.Insignia;
            bool silvertailTrade = ok && session.Kind == TradeKind.Silvertail;

            int rejectedUnknown2;
            int rejectedItemId;
            if (consumeTrade)
            {
                rejectedUnknown2 = 0;
                rejectedItemId = 0;
            }
            else if (luxWeiInspect || falaDeviceInspect || falaInsigniaTurnIn)
            {
                // Capture Fala/Lux-Wei/Prophet inspect: Unknown2=1 returns offered item to inventory UI.
                rejectedUnknown2 = 1;
                rejectedItemId = itemId;
            }
            else if (silvertailTrade)
            {
                // Capture: close trade UI; device return is via TemplateAction (avoids duplicate RejectedItems grant).
                rejectedUnknown2 = 0;
                rejectedItemId = 0;
            }
            else
            {
                rejectedUnknown2 = 1;
                rejectedItemId = ok ? itemId : 0;
            }

            SendRejectedItems(source, session.NpcIdentity, rejectedItemId, quality, rejectedUnknown2);

            if (ok)
            {
                if (luxWeiInspect || falaDeviceInspect)
                {
                    NascenceAbanFalaQuestRuntime.TryRestoreAncientDeviceIfMissing(source);
                }

                if (falaInsigniaTurnIn)
                {
                    NascenceAbanFalaQuestRuntime.TryRestoreInsigniaIfMissing(source);
                }

                if (session.Kind == TradeKind.LuxWeiActivatedArtifact)
                {
                    NascenceAbanFalaQuestRuntime.CompleteLuxWeiKeyReturn(source);
                }

                if (session.Kind == TradeKind.Silvertail)
                {
                    int count = NascenceAbanFalaQuestRuntime.IncrementSoulCount(source);
                    if (count >= 3)
                    {
                        NascenceAbanFalaQuestRuntime.TryForceReturnInspectedAnalyzer(source);
                    }
                    else
                    {
                        NascenceAbanFalaQuestRuntime.TryForceReturnFavoredAnalyzer(source);
                    }
                }

                if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, session.NpcIdentity))
                {
                    string forcedNodeId = ResolvePostTradeDialogueNodeId(source, session.Kind);
                    if (!string.IsNullOrWhiteSpace(forcedNodeId))
                    {
                        ContentDrivenNpcDialogueRouter.TryForceResumeAbanPostTradeDialogue(
                            source,
                            session.NpcIdentity,
                            forcedNodeId);
                    }
                }

                NascenceAbanFalaQuestRuntime.TrySyncClientJournal(source);
            }

            ClearSession(source);
            return true;
        }

        private static string ResolvePostTradeDialogueNodeId(ICharacter source, TradeKind kind)
        {
            switch (kind)
            {
                case TradeKind.AncientDevice:
                    return NascenceAbanFalaTradeAdapter.ResolveDeviceTradePostNodeId(source);
                case TradeKind.Insignia:
                    return NascenceAbanFalaInteractionRules.InsigniaTurnInNodeId;
                case TradeKind.LuxWeiAncientDevice:
                    return NascenceAbanFalaInteractionRules.LuxWeiActivationNodeId;
                case TradeKind.LuxWeiActivatedArtifact:
                    return NascenceAbanFalaInteractionRules.LuxWeiFarewellNodeId;
                default:
                    return null;
            }
        }

        private static bool ApplyTrade(ICharacter source, FalaTradeSession session, int itemId)
        {
            switch (session.Kind)
            {
                case TradeKind.AncientDevice:
                    if (itemId != NascenceAbanFalaInteractionRules.AncientDeviceItemId)
                    {
                        return false;
                    }

                    bool fromArtifactOffer = string.Equals(
                        session.DialogueSourceNodeId,
                        NascenceAbanFalaInteractionRules.ArtifactOfferNodeId,
                        StringComparison.OrdinalIgnoreCase);
                    if (fromArtifactOffer)
                    {
                        NascenceAbanFalaQuestRuntime.AcceptRedemptionQuests(source);
                        NascenceAbanFalaQuestRuntime.MarkDeviceInspected(source);
                    }
                    else if (NascenceAbanFalaQuestRuntime.IsMissionActive(
                                 source,
                                 NascenceAbanFalaInteractionRules.QuestDeviceInfo)
                             || NascenceAbanFalaQuestRuntime.IsMissionActive(
                                 source,
                                 NascenceAbanFalaInteractionRules.QuestInsigniaTask))
                    {
                        NascenceAbanFalaQuestRuntime.MarkDeviceInspected(source);
                    }

                    return true;

                case TradeKind.Insignia:
                    if (itemId != NascenceAbanFalaInteractionRules.InsigniaOfAbanItemId)
                    {
                        return false;
                    }

                    NascenceAbanFalaQuestRuntime.CompleteInsigniaTurnIn(source);
                    return true;

                case TradeKind.LuxWeiAncientDevice:
                    if (itemId != NascenceAbanFalaInteractionRules.AncientDeviceItemId)
                    {
                        return false;
                    }

                    NascenceAbanFalaQuestRuntime.MarkLuxWeiDeviceShown(source);
                    return true;

                case TradeKind.LuxWeiActivatedArtifact:
                    if (!NascenceAbanFalaQuestRuntime.IsLuxWeiKeyReturnReady(source))
                    {
                        return false;
                    }

                    if (itemId != NascenceAbanFalaInteractionRules.InspectedAncientPatternAnalyzerItemId
                        && itemId != NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId
                        && itemId != NascenceAbanFalaInteractionRules.AncientDeviceItemId)
                    {
                        return false;
                    }

                    TryConsumeItem(source, itemId, session.StagedContainer);
                    return true;

                case TradeKind.Silvertail:
                    if (itemId != NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId
                        && itemId != NascenceAbanFalaInteractionRules.InspectedAncientPatternAnalyzerItemId
                        && itemId != NascenceAbanFalaInteractionRules.AncientDeviceItemId)
                    {
                        return false;
                    }

                    Identity silvertailIdentity = session.NpcIdentity;
                    ICharacter dreaming = ResolveNpcByName(
                        source,
                        silvertailIdentity,
                        NascenceAbanFalaInteractionRules.DreamingSilvertailName);
                    if (dreaming != null)
                    {
                        silvertailIdentity = dreaming.Identity;
                    }

                    return ThrakGardenKeySilvertailTransform.TryCurseAndAggro(source, silvertailIdentity);

                default:
                    return false;
            }
        }

        private static void RefineKindFromItem(ICharacter source, FalaTradeSession session, int itemId)
        {
            if (session == null || itemId <= 0)
            {
                return;
            }

            if (itemId == NascenceAbanFalaInteractionRules.InsigniaOfAbanItemId)
            {
                session.Kind = TradeKind.Insignia;
                return;
            }

            if (itemId == NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId
                || itemId == NascenceAbanFalaInteractionRules.InspectedAncientPatternAnalyzerItemId)
            {
                if (session.Kind == TradeKind.Silvertail)
                {
                    return;
                }

                if (source != null
                    && NascenceAbanFalaQuestRuntime.CanUseSilvertailSoulTrade(source)
                    && IsDreamingSilvertail(source, session.NpcIdentity))
                {
                    session.Kind = TradeKind.Silvertail;
                    return;
                }

                if (IsLuxWeiTradeNpc(source, session.NpcIdentity))
                {
                    session.Kind = NascenceAbanFalaQuestRuntime.IsLuxWeiKeyReturnReady(source)
                                       ? TradeKind.LuxWeiActivatedArtifact
                                       : TradeKind.LuxWeiAncientDevice;
                    return;
                }

                if (session.Kind == TradeKind.LuxWeiActivatedArtifact
                    && NascenceAbanFalaQuestRuntime.IsLuxWeiKeyReturnReady(source))
                {
                    return;
                }

                if (session.Kind == TradeKind.None
                    && NascenceAbanFalaQuestRuntime.IsLuxWeiKeyReturnReady(source)
                    && IsLuxWeiTradeNpc(source, session.NpcIdentity))
                {
                    session.Kind = TradeKind.LuxWeiActivatedArtifact;
                }

                return;
            }

            if (itemId == NascenceAbanFalaInteractionRules.AncientDeviceItemId)
            {
                if (session.Kind == TradeKind.LuxWeiAncientDevice)
                {
                    return;
                }

                if (IsLuxWeiTradeNpc(source, session.NpcIdentity))
                {
                    session.Kind = NascenceAbanFalaQuestRuntime.IsLuxWeiKeyReturnReady(source)
                                       ? TradeKind.LuxWeiActivatedArtifact
                                       : TradeKind.LuxWeiAncientDevice;
                    return;
                }

                session.Kind = session.Kind == TradeKind.None
                                   ? TradeKind.AncientDevice
                                   : session.Kind;
            }
        }

        private static TradeKind InferTradeKind(ICharacter source, Identity npcIdentity)
        {
            if (IsAbanSilvertailTradeNpc(source, npcIdentity)
                && NascenceAbanFalaQuestRuntime.CanUseSilvertailSoulTrade(source))
            {
                return TradeKind.Silvertail;
            }

            if (IsLuxWeiTradeNpc(source, npcIdentity))
            {
                return ResolveLuxWeiTradeKind(source);
            }

            if (IsFalaTradeNpc(source, npcIdentity))
            {
                return TradeKind.AncientDevice;
            }

            return TradeKind.AncientDevice;
        }

        private static TradeKind ResolveLuxWeiTradeKind(ICharacter source)
        {
            if (NascenceAbanFalaQuestRuntime.IsLuxWeiKeyReturnReady(source))
            {
                return TradeKind.LuxWeiActivatedArtifact;
            }

            return TradeKind.LuxWeiAncientDevice;
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

        private static bool TryFindItemInInventory(ICharacter source, int itemId, out int foundItemId)
        {
            foundItemId = 0;
            if (source == null || source.BaseInventory == null || itemId <= 0)
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

                foreach (KeyValuePair<int, IItem> entry in page.List())
                {
                    IItem item = entry.Value;
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.LowID == itemId || item.HighID == itemId)
                    {
                        foundItemId = itemId;
                        return true;
                    }
                }
            }

            return false;
        }

        private static int FindExpectedItem(ICharacter source, TradeKind kind)
        {
            if (source == null || source.BaseInventory == null)
            {
                return 0;
            }

            int expected;
            switch (kind)
            {
                case TradeKind.Insignia:
                    expected = NascenceAbanFalaInteractionRules.InsigniaOfAbanItemId;
                    break;
                case TradeKind.LuxWeiActivatedArtifact:
                    if (TryFindItemInInventory(
                            source,
                            NascenceAbanFalaInteractionRules.InspectedAncientPatternAnalyzerItemId,
                            out expected))
                    {
                        return expected;
                    }

                    return TryFindItemInInventory(
                               source,
                               NascenceAbanFalaInteractionRules.AncientDeviceItemId,
                               out expected)
                               ? expected
                               : 0;
                case TradeKind.Silvertail:
                    if (TryFindItemInInventory(
                            source,
                            NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId,
                            out expected))
                    {
                        return expected;
                    }

                    return 0;
                default:
                    expected = NascenceAbanFalaInteractionRules.AncientDeviceItemId;
                    break;
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

                    if (item.LowID == expected || item.HighID == expected)
                    {
                        return expected;
                    }
                }
            }

            return 0;
        }

        private static int ResolveItemId(IItem item)
        {
            if (item == null)
            {
                return 0;
            }

            int low = item.LowID;
            int high = item.HighID;
            if (low == NascenceAbanFalaInteractionRules.AncientDeviceItemId
                || high == NascenceAbanFalaInteractionRules.AncientDeviceItemId)
            {
                return NascenceAbanFalaInteractionRules.AncientDeviceItemId;
            }

            if (low == NascenceAbanFalaInteractionRules.InsigniaOfAbanItemId
                || high == NascenceAbanFalaInteractionRules.InsigniaOfAbanItemId)
            {
                return NascenceAbanFalaInteractionRules.InsigniaOfAbanItemId;
            }

            if (low == NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId
                || high == NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId)
            {
                return NascenceAbanFalaInteractionRules.FavoredAncientPatternAnalyzerItemId;
            }

            if (low == NascenceAbanFalaInteractionRules.InspectedAncientPatternAnalyzerItemId
                || high == NascenceAbanFalaInteractionRules.InspectedAncientPatternAnalyzerItemId)
            {
                return NascenceAbanFalaInteractionRules.InspectedAncientPatternAnalyzerItemId;
            }

            return low > 0 ? low : high;
        }

        private static void SendRejectedItems(ICharacter source, Identity npcIdentity, int itemId, int quality, int unknown2)
        {
            Item[] items = new Item[0];
            if (itemId > 0)
            {
                int ql = quality > 0 ? quality : 1;
                items = new[] { new Item(ql, itemId, itemId) };
            }

            KnuBotRejectedItemsMessageHandler.Default.Send(source, npcIdentity, items, unknown2);
        }

        private static ICharacter ResolveNpcByName(ICharacter source, Identity npcIdentity, string expectedName)
        {
            if (source != null && source.Playfield != null)
            {
                foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>(source.Playfield.Identity))
                {
                    if (character == null || character.Identity.Instance != npcIdentity.Instance)
                    {
                        continue;
                    }

                    if (string.Equals(character.Name, expectedName, StringComparison.OrdinalIgnoreCase))
                    {
                        return character;
                    }
                }
            }

            try
            {
                ICharacter direct = Pool.Instance.GetObject<ICharacter>(npcIdentity);
                if (direct != null
                    && string.Equals(direct.Name, expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return direct;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static ICharacter ResolveFala(ICharacter source, Identity npcIdentity)
        {
            if (NascenceAbanFalaInteractionRules.IsFala(npcIdentity))
            {
                try
                {
                    ICharacter direct = Pool.Instance.GetObject<ICharacter>(npcIdentity);
                    if (direct != null && NascenceAbanFalaInteractionRules.IsFalaName(direct.Name))
                    {
                        return direct;
                    }
                }
                catch (Exception)
                {
                }
            }

            if (source != null && source.Playfield != null)
            {
                foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>(source.Playfield.Identity))
                {
                    if (character == null || character.Identity.Instance != npcIdentity.Instance)
                    {
                        continue;
                    }

                    if (NascenceAbanFalaInteractionRules.IsFalaName(character.Name))
                    {
                        return character;
                    }
                }
            }

            return null;
        }

        private static ICharacter ResolveLuxWei(ICharacter source, Identity npcIdentity)
        {
            if (NascenceAbanFalaInteractionRules.IsLuxWei(npcIdentity))
            {
                try
                {
                    ICharacter direct = Pool.Instance.GetObject<ICharacter>(npcIdentity);
                    if (direct != null && NascenceAbanFalaInteractionRules.IsLuxWeiName(direct.Name))
                    {
                        return direct;
                    }
                }
                catch (Exception)
                {
                }
            }

            if (source != null && source.Playfield != null)
            {
                foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>(source.Playfield.Identity))
                {
                    if (character == null || character.Identity.Instance != npcIdentity.Instance)
                    {
                        continue;
                    }

                    if (NascenceAbanFalaInteractionRules.IsLuxWeiName(character.Name))
                    {
                        return character;
                    }
                }
            }

            return null;
        }

        private static FalaTradeSession GetSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                FalaTradeSession session;
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

        internal static void ClearTradeSession(ICharacter source)
        {
            ClearSession(source);
        }

        private static bool IdentitiesEqual(Identity a, Identity b)
        {
            return a.Type == b.Type && a.Instance == b.Instance;
        }

        private sealed class FalaTradeSession
        {
            public Identity NpcIdentity;
            public TradeKind Kind;
            public string DialogueSourceNodeId;
            public int StagedItemId;
            public int StagedQuality;
            public Identity StagedContainer;
        }
    }
}
