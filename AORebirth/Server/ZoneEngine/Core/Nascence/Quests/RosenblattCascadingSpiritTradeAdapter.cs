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

    #endregion

    /// <summary>
    /// Dr. Rosenblatt trade adapter for Barking Chimera datadisc and Essence turn-in (20260822-083345).
    /// </summary>
    internal static class RosenblattCascadingSpiritTradeAdapter
    {
        private enum TradeMode
        {
            ChimeraDisc,
            EssenceTurnIn
        }

        private static readonly Dictionary<int, CascadingTradeSession> SessionsByCharacter =
            new Dictionary<int, CascadingTradeSession>();

        private static readonly object SyncRoot = new object();

        internal static void BeginDiscTrade(ICharacter source, Identity npcIdentity)
        {
            BeginSession(source, npcIdentity, TradeMode.ChimeraDisc);
        }

        internal static void BeginEssenceTrade(ICharacter source, Identity npcIdentity)
        {
            BeginSession(source, npcIdentity, TradeMode.EssenceTurnIn);
        }

        internal static bool HasActiveSession(ICharacter source)
        {
            return GetSession(source) != null;
        }

        internal static bool IsRosenblattTradeNpc(ICharacter source, Identity npcIdentity)
        {
            return RosenblattHiathlinTradeAdapter.TryResolveRosenblattNpc(source, npcIdentity) != null;
        }

        internal static bool TryStageTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            if (RosenblattHiathlinTradeAdapter.HasActiveSession(source))
            {
                return false;
            }

            CascadingTradeSession session = GetSession(source);
            if (session == null)
            {
                if (!IsRosenblattTradeNpc(source, message.Target))
                {
                    return false;
                }

                if ((RosenblattPapagenaQuestRuntime.HasDatadisc(source)
                     && RosenblattPapagenaQuestRuntime.CanOfferDiscTrade(source))
                    || (RosenblattPapagenaQuestRuntime.HasDatadisc(source)
                        && RosenblattPapagenoQuestRuntime.CanOfferDiscTrade(source)))
                {
                    return false;
                }

                if (RosenblattCascadingSpiritQuestRuntime.CanOfferDiscTrade(source))
                {
                    BeginDiscTrade(source, message.Target);
                }
                else if (RosenblattCascadingSpiritQuestRuntime.CanTurnIn(source))
                {
                    BeginEssenceTrade(source, message.Target);
                }
                else
                {
                    return false;
                }

                session = GetSession(source);
                if (session == null)
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
                if (session.Mode == TradeMode.ChimeraDisc
                    && RosenblattCascadingSpiritInteractionRules.IsBarkingChimeraDatadisc(itemId, itemId))
                {
                    session.StagedItem = true;
                    session.StagedContainers.Add(message.Container);
                }
                else if (session.Mode == TradeMode.EssenceTurnIn
                         && RosenblattCascadingSpiritInteractionRules.IsEssenceOfTheHaunted(itemId, itemId))
                {
                    session.StagedItem = true;
                    session.StagedContainers.Add(message.Container);
                }
            }

            return true;
        }

        internal static bool TryFinishTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            if (RosenblattHiathlinTradeAdapter.HasActiveSession(source))
            {
                return false;
            }

            CascadingTradeSession activeSession = GetSession(source);
            if (activeSession == null)
            {
                return false;
            }

            if (!IdentitiesEqual(activeSession.NpcIdentity, message.Target)
                && !IsRosenblattTradeNpc(source, message.Target))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                SendRejectedItems(source, message.Target);
                ClearSession(source);
                return true;
            }

            CascadingTradeSession session = activeSession;
            session.NpcIdentity = message.Target;

            Identity staged = session.StagedContainers.Count > 0
                                  ? session.StagedContainers[session.StagedContainers.Count - 1]
                                  : Identity.None;

            if (session.Mode == TradeMode.ChimeraDisc)
            {
                bool ok = RosenblattCascadingSpiritQuestRuntime.CanOfferDiscTrade(source)
                          && session.StagedItem
                          && RosenblattCascadingSpiritQuestRuntime.HasChimeraDatadisc(source);

                SendRejectedItems(source, session.NpcIdentity);

                if (ok && RosenblattCascadingSpiritQuestRuntime.TryConsumeChimeraDatadisc(source, staged))
                {
                    RosenblattCascadingSpiritQuestRuntime.MarkChimeraDiscTraded(source);
                    ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, session.NpcIdentity);
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "RosenblattCascading disc trade complete by=" + source.Identity.ToString(true));
                }
            }
            else
            {
                bool ok = RosenblattCascadingSpiritQuestRuntime.CanTurnIn(source)
                          && session.StagedItem
                          && RosenblattCascadingSpiritQuestRuntime.HasEssence(source);

                SendRejectedItems(source, session.NpcIdentity);

                if (ok && RosenblattCascadingSpiritQuestRuntime.TryConsumeEssence(source, staged))
                {
                    if (RosenblattCascadingSpiritQuestRuntime.CompleteTurnIn(source, true))
                    {
                        // Do not resume into Hiathlin turn-in_done — that left Mission:55AA38B5
                        // on the client after rewards. Close + unlock Attack.
                        ClearSession(source);
                        KnuBotCloseChatWindowMessageHandler.Default.Send(source, session.NpcIdentity);
                        RosenblattClientActionLock.Clear(source);
                        LogUtil.Debug(
                            DebugInfoDetail.Engine,
                            "RosenblattCascading essence trade complete by=" + source.Identity.ToString(true));
                        return true;
                    }
                }
                else if (!ok)
                {
                    ChatTextMessageHandler.Default.Send(
                        source,
                        "You need Essence of the Haunted from a Cascading Spirit corpse.");
                }
            }

            ClearSession(source);
            return true;
        }

        private static void BeginSession(ICharacter source, Identity npcIdentity, TradeMode mode)
        {
            if (source == null || source.Identity.Instance <= 0 || npcIdentity == Identity.None)
            {
                return;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[source.Identity.Instance] = new CascadingTradeSession
                                                                    {
                                                                        NpcIdentity = npcIdentity,
                                                                        Mode = mode
                                                                    };
            }
        }

        private static int ResolveItemId(IItem item)
        {
            if (item == null)
            {
                return 0;
            }

            if (RosenblattCascadingSpiritInteractionRules.IsBarkingChimeraDatadisc(item.LowID, item.HighID))
            {
                return RosenblattCascadingSpiritInteractionRules.BarkingChimeraDatadiscItemId;
            }

            if (RosenblattCascadingSpiritInteractionRules.IsEssenceOfTheHaunted(item.LowID, item.HighID))
            {
                return RosenblattCascadingSpiritInteractionRules.EssenceOfTheHauntedItemId;
            }

            return item.LowID > 0 ? item.LowID : item.HighID;
        }

        private static void SendRejectedItems(ICharacter source, Identity npcIdentity)
        {
            KnuBotRejectedItemsMessageHandler.Default.Send(source, npcIdentity, new Item[0], 0);
        }

        private static CascadingTradeSession GetSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                CascadingTradeSession session;
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

        private sealed class CascadingTradeSession
        {
            public CascadingTradeSession()
            {
                this.StagedContainers = new List<Identity>();
            }

            public Identity NpcIdentity;
            public TradeMode Mode;
            public bool StagedItem;
            public List<Identity> StagedContainers;
        }
    }
}
