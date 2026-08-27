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
    /// Dr. Rosenblatt KnuBot trade adapter for Predator datadisc turn-in (capture 20260822-083846).
    /// </summary>
    internal static class RosenblattSpinetoothTradeAdapter
    {
        private static readonly Dictionary<int, SpinetoothTradeSession> SessionsByCharacter =
            new Dictionary<int, SpinetoothTradeSession>();

        private static readonly object SyncRoot = new object();

        internal static void BeginTrade(ICharacter source, Identity npcIdentity)
        {
            if (source == null || source.Identity.Instance <= 0 || npcIdentity == Identity.None)
            {
                return;
            }

            if (!RosenblattSpinetoothQuestRuntime.CanOfferDiscTrade(source))
            {
                return;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[source.Identity.Instance] = new SpinetoothTradeSession
                                                                  {
                                                                      NpcIdentity = npcIdentity
                                                                  };
            }
        }

        internal static bool IsRosenblattDiscTradeNpc(ICharacter source, Identity npcIdentity)
        {
            if (RosenblattHiathlinInteractionRules.IsRosenblatt(npcIdentity))
            {
                return true;
            }

            return ResolveNpcName(source, npcIdentity) != null;
        }

        internal static bool HasActiveSession(ICharacter source)
        {
            return GetSession(source) != null;
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

            SpinetoothTradeSession session = GetSession(source);
            if (session == null)
            {
                if (!IsRosenblattDiscTradeNpc(source, message.Target)
                    || !RosenblattSpinetoothQuestRuntime.CanOfferDiscTrade(source))
                {
                    return false;
                }

                BeginTrade(source, message.Target);
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
                if (RosenblattSpinetoothInteractionRules.IsPredatorDatadisc(itemId, itemId))
                {
                    session.StagedDatadisc = true;
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

            // Only claim FinishTrade when this adapter opened the disc-trade session.
            // Otherwise Dr. Rosenblatt Hiathlin turn-in is stolen whenever a datadisc is in bag.
            if (RosenblattHiathlinTradeAdapter.HasActiveSession(source))
            {
                return false;
            }

            SpinetoothTradeSession activeSession = GetSession(source);
            if (activeSession == null)
            {
                return false;
            }

            if (!IdentitiesEqual(activeSession.NpcIdentity, message.Target)
                && !IsRosenblattDiscTradeNpc(source, message.Target))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                SendRejectedItems(source, message.Target);
                ClearSession(source);
                return true;
            }

            SpinetoothTradeSession session = activeSession;
            session.NpcIdentity = message.Target;

            bool ok = RosenblattSpinetoothQuestRuntime.CanOfferDiscTrade(source)
                      && session.StagedDatadisc
                      && RosenblattSpinetoothQuestRuntime.HasDatadisc(source);

            SendRejectedItems(source, session.NpcIdentity);

            if (ok)
            {
                Identity staged = session.StagedContainers.Count > 0
                                      ? session.StagedContainers[session.StagedContainers.Count - 1]
                                      : Identity.None;
                if (RosenblattSpinetoothQuestRuntime.TryConsumeDatadisc(source, staged))
                {
                    RosenblattSpinetoothQuestRuntime.MarkPredatorDiscTraded(source);
                    ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, session.NpcIdentity);
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "RosenblattSpinetooth disc trade complete by=" + source.Identity.ToString(true));
                }
            }

            ClearSession(source);
            return true;
        }

        private static int ResolveItemId(IItem item)
        {
            if (item == null)
            {
                return 0;
            }

            if (RosenblattSpinetoothInteractionRules.IsPredatorDatadisc(item.LowID, item.HighID))
            {
                return RosenblattSpinetoothInteractionRules.PredatorDatadiscItemId;
            }

            return item.LowID > 0 ? item.LowID : item.HighID;
        }

        private static string ResolveNpcName(ICharacter source, Identity identity)
        {
            try
            {
                if (source != null && source.Playfield != null)
                {
                    ICharacter onPf = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, identity);
                    if (onPf != null && RosenblattHiathlinInteractionRules.IsRosenblattName(onPf.Name))
                    {
                        return onPf.Name;
                    }

                    foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>(source.Playfield.Identity))
                    {
                        if (character == null || character.Identity.Instance != identity.Instance)
                        {
                            continue;
                        }

                        if (RosenblattHiathlinInteractionRules.IsRosenblattName(character.Name))
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

                    if (RosenblattHiathlinInteractionRules.IsRosenblattName(character.Name))
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

        private static void SendRejectedItems(ICharacter source, Identity npcIdentity)
        {
            KnuBotRejectedItemsMessageHandler.Default.Send(source, npcIdentity, new Item[0], 0);
        }

        private static SpinetoothTradeSession GetSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                SpinetoothTradeSession session;
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

        private sealed class SpinetoothTradeSession
        {
            public SpinetoothTradeSession()
            {
                this.StagedContainers = new List<Identity>();
            }

            public Identity NpcIdentity;
            public bool StagedDatadisc;
            public List<Identity> StagedContainers;
        }
    }
}
