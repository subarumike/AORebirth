namespace ZoneEngine.Core.Doja
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
    /// Scarlett Dalquist KnuBot trade adapter for Nascense DOJA chip turn-in (capture 20260821-222107).
    /// </summary>
    internal static class DojaChipTradeAdapter
    {
        private static readonly Dictionary<int, DojaTradeSession> SessionsByCharacter =
            new Dictionary<int, DojaTradeSession>();

        private static readonly object SyncRoot = new object();

        internal static void BeginTrade(ICharacter source, Identity npcIdentity)
        {
            if (source == null || source.Identity.Instance <= 0 || npcIdentity == Identity.None)
            {
                return;
            }

            if (!DojaChipQuestRuntime.CanTurnIn(source))
            {
                return;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[source.Identity.Instance] = new DojaTradeSession
                                                               {
                                                                   NpcIdentity = npcIdentity
                                                               };
            }
        }

        internal static bool IsDojaTradeNpc(ICharacter source, Identity npcIdentity)
        {
            if (DojaChipInteractionRules.IsScarlett(npcIdentity))
            {
                return true;
            }

            return ResolveNpcName(source, npcIdentity) != null;
        }

        internal static bool TryStageTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            DojaTradeSession session = GetSession(source);
            if (session == null)
            {
                if (!IsDojaTradeNpc(source, message.Target))
                {
                    return false;
                }

                BeginTrade(source, message.Target);
                session = GetSession(source);
            }

            if (session == null)
            {
                return IsDojaTradeNpc(source, message.Target);
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
            }

            return true;
        }

        internal static bool TryFinishTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            if (!IsDojaTradeNpc(source, message.Target))
            {
                DojaTradeSession existing = GetSession(source);
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

            DojaTradeSession session = GetSession(source);
            if (session == null)
            {
                BeginTrade(source, message.Target);
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
                itemId = DojaChipQuestRuntime.HasNascenseChip(source)
                             ? DojaChipInteractionRules.NascenseChipItemId
                             : 0;
            }

            bool ok = itemId == DojaChipInteractionRules.NascenseChipItemId
                      && DojaChipQuestRuntime.CanTurnIn(source);

            // Capture 20260821-222107: RejectedItems empty Unknown2=0 FIRST, then rewards/quests.
            SendRejectedItems(source, session.NpcIdentity);

            if (ok)
            {
                TryConsumeItem(source, itemId, session.StagedContainer);
                DojaChipQuestRuntime.CompleteTurnIn(source);
                ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, session.NpcIdentity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "DojaChip Scarlett trade complete by=" + source.Identity.ToString(true));
            }

            ClearSession(source);
            return true;
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
                    if (onPf != null && DojaChipInteractionRules.IsScarlettName(onPf.Name))
                    {
                        return onPf.Name;
                    }

                    foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>(source.Playfield.Identity))
                    {
                        if (character == null || character.Identity.Instance != identity.Instance)
                        {
                            continue;
                        }

                        if (DojaChipInteractionRules.IsScarlettName(character.Name))
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

                    if (DojaChipInteractionRules.IsScarlettName(character.Name))
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

        private static int ResolveItemId(IItem item)
        {
            if (item == null)
            {
                return 0;
            }

            if (DojaChipInteractionRules.IsNascenseChip(item.LowID, item.HighID))
            {
                return DojaChipInteractionRules.NascenseChipItemId;
            }

            return item.LowID > 0 ? item.LowID : item.HighID;
        }

        private static DojaTradeSession GetSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                DojaTradeSession session;
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

        private sealed class DojaTradeSession
        {
            public Identity NpcIdentity;
            public int StagedItemId;
            public int StagedQuality;
            public Identity StagedContainer;
        }
    }
}
