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
    /// Dr. Rosenblatt KnuBot trade adapter for Hiathlin body-part turn-in (capture 20260822-070136).
    /// </summary>
    internal static class RosenblattHiathlinTradeAdapter
    {
        private static readonly Dictionary<int, RosenblattTradeSession> SessionsByCharacter =
            new Dictionary<int, RosenblattTradeSession>();

        private static readonly object SyncRoot = new object();

        internal static void BeginTrade(ICharacter source, Identity npcIdentity)
        {
            EnsureTradeSession(source, npcIdentity);
        }

        internal static void EnsureTradeSession(ICharacter source, Identity npcIdentity)
        {
            if (source == null || source.Identity.Instance <= 0 || npcIdentity == Identity.None)
            {
                return;
            }

            if (GetSession(source) != null)
            {
                return;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[source.Identity.Instance] = new RosenblattTradeSession
                                                               {
                                                                   NpcIdentity = npcIdentity
                                                               };
            }
        }

        internal static Identity ResolveLiveNpcIdentity(ICharacter source, Identity messageTarget, Identity fallback)
        {
            ICharacter resolved = TryResolveRosenblattNpc(source, messageTarget);
            if (resolved != null)
            {
                return resolved.Identity;
            }

            if (messageTarget.Type == IdentityType.CanbeAffected && messageTarget.Instance > 0)
            {
                return messageTarget;
            }

            return fallback;
        }

        internal static bool HasActiveSession(ICharacter source)
        {
            return GetSession(source) != null;
        }

        internal static bool IsRosenblattTradeNpc(ICharacter source, Identity npcIdentity)
        {
            return TryResolveRosenblattNpc(source, npcIdentity) != null;
        }

        internal static ICharacter TryResolveRosenblattNpc(ICharacter source, Identity npcIdentity)
        {
            if (npcIdentity.Type == IdentityType.None || npcIdentity.Instance <= 0)
            {
                return null;
            }

            if (RosenblattHiathlinInteractionRules.IsRosenblatt(npcIdentity))
            {
                try
                {
                    ICharacter byStatic = Pool.Instance.GetObject<ICharacter>(npcIdentity);
                    if (byStatic != null
                        && RosenblattHiathlinInteractionRules.IsRosenblattName(byStatic.Name))
                    {
                        return byStatic;
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

                    if (RosenblattHiathlinInteractionRules.IsRosenblattName(character.Name))
                    {
                        return character;
                    }
                }
            }

            try
            {
                ICharacter direct = Pool.Instance.GetObject<ICharacter>(npcIdentity);
                if (direct != null
                    && RosenblattHiathlinInteractionRules.IsRosenblattName(direct.Name))
                {
                    return direct;
                }
            }
            catch (Exception)
            {
            }

            foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected))
            {
                if (character == null || character.Identity.Instance != npcIdentity.Instance)
                {
                    continue;
                }

                if (RosenblattHiathlinInteractionRules.IsRosenblattName(character.Name))
                {
                    return character;
                }
            }

            return null;
        }

        internal static bool ShouldClaimTradeMessage(ICharacter source, KnuBotTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            // Compact Message Datadisc trades belong to Papagena/Cascading adapters.
            // Do not claim generic Rosenblatt dialogue sessions — that steals disc turn-ins.
            if (RosenblattPapagenaTradeAdapter.HasActiveSession(source)
                || RosenblattCascadingSpiritTradeAdapter.HasActiveSession(source)
                || RosenblattSpinetoothTradeAdapter.HasActiveSession(source)
                || RosenblattDemonicTradeAdapter.HasActiveSession(source))
            {
                return false;
            }

            return GetSession(source) != null
                   || ContentDrivenNpcDialogueRouter.IsRosenblattHiathlinTurnInTradeActive(source)
                   || (IsRosenblattTradeNpc(source, message.Target)
                       && RosenblattHiathlinQuestRuntime.CanTurnIn(source));
        }

        internal static bool TryStageTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            if (!ShouldClaimTradeMessage(source, message))
            {
                return false;
            }

            RosenblattTradeSession session = GetSession(source);
            if (session == null)
            {
                Identity tradeTarget = ResolveLiveNpcIdentity(source, message.Target, message.Target);
                BeginTrade(source, tradeTarget);
                session = GetSession(source);
            }

            if (session == null)
            {
                return true;
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
                if (itemId == RosenblattHiathlinInteractionRules.HiathlinThighItemId)
                {
                    session.RegularBodyParts++;
                }
                else if (itemId == RosenblattHiathlinInteractionRules.HiathlinPrimeThighItemId)
                {
                    session.PrimeBodyParts++;
                }

                session.StagedContainers.Add(message.Container);
            }

            return true;
        }

        internal static bool TryFinishTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            if (RosenblattPapagenaTradeAdapter.HasActiveSession(source)
                || RosenblattCascadingSpiritTradeAdapter.HasActiveSession(source)
                || RosenblattSpinetoothTradeAdapter.HasActiveSession(source)
                || RosenblattDemonicTradeAdapter.HasActiveSession(source))
            {
                return false;
            }

            RosenblattTradeSession existingSession = GetSession(source);
            ICharacter rosenblattNpc = TryResolveRosenblattNpc(source, message.Target);
            bool isRosenblattTarget = rosenblattNpc != null;
            bool dialogueTurnInActive =
                ContentDrivenNpcDialogueRouter.IsRosenblattHiathlinTurnInTradeActive(source);
            bool canTurnIn = RosenblattHiathlinQuestRuntime.CanTurnIn(source);
            if (existingSession == null
                && !dialogueTurnInActive
                && !(isRosenblattTarget && canTurnIn))
            {
                return false;
            }

            Identity tradeNpcIdentity = rosenblattNpc != null ? rosenblattNpc.Identity : message.Target;

            // Spawned Rosenblatt has no BaseKnuBot — always claim FinishTrade once routed here.

            if (message.Decline != 0)
            {
                SendRejectedItems(source, tradeNpcIdentity);
                ClearSession(source);
                return true;
            }

            RosenblattTradeSession session = GetSession(source);
            if (session == null)
            {
                EnsureTradeSession(source, tradeNpcIdentity);
                session = GetSession(source);
            }

            if (session == null)
            {
                SendRejectedItems(source, tradeNpcIdentity);
                return true;
            }

            session.NpcIdentity = tradeNpcIdentity;

            bool hasInventoryParts = RosenblattHiathlinQuestRuntime.HasRequiredBodyParts(source);
            bool hasStagedParts =
                session.RegularBodyParts >= RosenblattHiathlinInteractionRules.RequiredRegularBodyParts
                && session.PrimeBodyParts >= RosenblattHiathlinInteractionRules.RequiredPrimeBodyParts;
            bool ok = canTurnIn && (hasInventoryParts || hasStagedParts);

            SendRejectedItems(source, session.NpcIdentity);

            if (ok)
            {
                bool bodyPartsAlreadyConsumed = false;
                if (hasInventoryParts)
                {
                    if (!RosenblattHiathlinQuestRuntime.TryConsumeBodyParts(source))
                    {
                        ChatTextMessageHandler.Default.Send(
                            source,
                            "Dr. Rosenblatt could not accept those items (turn-in failed).");
                        LogUtil.Debug(
                            DebugInfoDetail.Engine,
                            "RosenblattHiathlin trade failed consume by=" + source.Identity.ToString(true));
                        ClearSession(source);
                        return true;
                    }

                    bodyPartsAlreadyConsumed = true;
                }
                else if (hasStagedParts)
                {
                    RosenblattHiathlinQuestRuntime.SyncTurnInProgressFromStagedParts(source);
                    bodyPartsAlreadyConsumed = true;
                }

                if (RosenblattHiathlinQuestRuntime.CompleteTurnIn(source, bodyPartsAlreadyConsumed))
                {
                    ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, session.NpcIdentity);
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "RosenblattHiathlin trade complete by=" + source.Identity.ToString(true)
                        + " stagedRegular=" + session.RegularBodyParts
                        + " stagedPrime=" + session.PrimeBodyParts);
                }
                else
                {
                    ChatTextMessageHandler.Default.Send(
                        source,
                        "Dr. Rosenblatt could not accept those items (turn-in failed).");
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "RosenblattHiathlin trade failed after consume by=" + source.Identity.ToString(true));
                }
            }
            else
            {
                string reason = !canTurnIn
                                    ? "quest not ready for turn-in"
                                    : !hasInventoryParts && !hasStagedParts
                                        ? "missing Hiathlin body parts"
                                        : "turn-in failed";
                ChatTextMessageHandler.Default.Send(
                    source,
                    "Dr. Rosenblatt could not accept those items (" + reason + ").");
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "RosenblattHiathlin trade rejected by=" + source.Identity.ToString(true)
                    + " canTurnIn=" + canTurnIn
                    + " hasInventoryParts=" + hasInventoryParts
                    + " hasStagedParts=" + hasStagedParts
                    + " stagedRegular=" + session.RegularBodyParts
                    + " stagedPrime=" + session.PrimeBodyParts);
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

            if (RosenblattHiathlinInteractionRules.IsHiathlinThighItem(item.LowID, item.HighID))
            {
                return RosenblattHiathlinInteractionRules.HiathlinThighItemId;
            }

            if (RosenblattHiathlinInteractionRules.IsHiathlinPrimeThighItem(item.LowID, item.HighID))
            {
                return RosenblattHiathlinInteractionRules.HiathlinPrimeThighItemId;
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

        private static RosenblattTradeSession GetSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                RosenblattTradeSession session;
                return SessionsByCharacter.TryGetValue(source.Identity.Instance, out session)
                           ? session
                           : null;
            }
        }

        internal static void ClearTradeSession(ICharacter source)
        {
            ClearSession(source);
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

        private sealed class RosenblattTradeSession
        {
            public RosenblattTradeSession()
            {
                this.StagedContainers = new List<Identity>();
            }

            public Identity NpcIdentity;
            public int RegularBodyParts;
            public int PrimeBodyParts;
            public List<Identity> StagedContainers;
        }
    }
}
