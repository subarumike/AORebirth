namespace ZoneEngine.Core.Subway.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;

    #endregion

    internal static class WindcallerKarrecTradeAdapter
    {
        private const string TradePendingFlag = "trade-consumption-pending";
        private const string TradeConsumedFlag = "trade-items-consumed";

        private static readonly Dictionary<int, KarrecTradeSession> SessionsByCharacter =
            new Dictionary<int, KarrecTradeSession>();

        private static readonly object SyncRoot = new object();

        internal static void BeginTrade(ICharacter source, Identity karrecIdentity)
        {
            if (source == null || source.Identity.Instance <= 0 || karrecIdentity == Identity.None)
            {
                return;
            }

            lock (SyncRoot)
            {
                SessionsByCharacter[source.Identity.Instance] = new KarrecTradeSession
                                                               {
                                                                   KarrecIdentity = karrecIdentity
                                                               };
            }
        }

        internal static bool TryStageTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (message == null)
            {
                return false;
            }

            KarrecTradeSession session = GetSession(source);
            if (!IsHandledKarrecTarget(session, message.Target))
            {
                return false;
            }

            // Trade can arrive before the return-offer answer creates a session; bind now so
            // FinishTrade and staging share the runtime Karrec identity.
            if (session == null && WindcallerKarrecInteractionRules.IsKarrec(message.Target))
            {
                BeginTrade(source, message.Target);
                session = GetSession(source);
            }

            if (!WindcallerKarrecQuestRuntime.IsActive(source))
            {
                return true;
            }

            if (session == null)
            {
                return true;
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
                MarkUnrecognizedOffering(session, message.Container);
                return true;
            }

            int itemId = ResolveOfferingItemId(item);
            lock (SyncRoot)
            {
                string locationKey = MakeLocationKey(message.Container);
                if (!session.StagedLocations.Add(locationKey))
                {
                    return true;
                }

                if (itemId == 0)
                {
                    session.ContainsUnrecognizedItem = true;
                    return true;
                }

                session.ItemLocations[itemId] = message.Container;
            }

            return true;
        }

        internal static bool TryFinishTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            bool isKarrecTarget = WindcallerKarrecInteractionRules.IsKarrec(message.Target);
            KarrecTradeSession session = GetSession(source);
            if (!isKarrecTarget && !IsHandledKarrecTarget(session, message.Target))
            {
                return false;
            }

            // Always claim Karrec FinishTrade so the generic knubot path cannot no-op
            // (spawned Karrec has no BaseKnuBot).
            if (message.Decline != 0)
            {
                ForgetSession(source);
                MissionDiagnostics.Log("karrec-trade decline character={0}", source.Identity.Instance);
                return true;
            }

            if (session == null)
            {
                BeginTrade(source, message.Target);
                session = GetSession(source);
            }

            // Server already finished earlier but client trade UI never closed — replay projection.
            // Still consume burger/card if they remain in inventory from a prior incomplete turn-in.
            if (WindcallerKarrecQuestRuntime.IsCompleted(source)
                && WindcallerKarrecQuestRuntime.HasAccountAccess(source))
            {
                IList<StagedOffering> leftoverOfferings = null;
                if (TryResolveOfferings(source, session, out leftoverOfferings)
                    || TryFindOfferingsInInventory(source, out leftoverOfferings))
                {
                    TryConsumeAndNotifyOfferings(source, leftoverOfferings);
                }

                SendImmediateTradeAcceptanceUi(source, message.Target);
                EnsureProjection(
                    source,
                    "completion-delete-projected",
                    () => WindcallerKarrecPacketSender.TrySendCompletionAndDelete(source));
                ForgetSession(source);
                MissionDiagnostics.Log(
                    "karrec-trade replay-ui character={0} (already completed, leftovers-consumed={1})",
                    source.Identity.Instance,
                    leftoverOfferings == null ? 0 : leftoverOfferings.Count);
                return true;
            }

            IList<StagedOffering> offerings;
            if (!TryResolveOfferings(source, session, out offerings)
                && !TryFindOfferingsInInventory(source, out offerings))
            {
                MissionDiagnostics.Log(
                    "karrec-trade finish rejected character={0} reason=missing-offerings active={1} staged={2}",
                    source.Identity.Instance,
                    WindcallerKarrecQuestRuntime.IsActive(source),
                    session == null ? 0 : session.StagedLocations.Count);
                ForgetSession(source);
                return true;
            }

            if (!WindcallerKarrecQuestRuntime.IsActive(source)
                && !WindcallerKarrecQuestRuntime.IsCompleted(source))
            {
                MissionOperationResult acceptance = WindcallerKarrecQuestRuntime.Accept(source);
                MissionDiagnostics.Log(
                    "karrec-trade re-accept character={0} status={1} message={2}",
                    source.Identity.Instance,
                    acceptance == null ? "null" : acceptance.Status.ToString(),
                    acceptance == null ? string.Empty : acceptance.Message);
            }

            KarrecCompletionResult completion =
                WindcallerKarrecQuestRuntime.CompleteAfterOfferingsConsumed(source);
            if (!completion.Completed)
            {
                MissionDiagnostics.Log(
                    "karrec-trade completion-failed character={0} error={1}",
                    source.Identity.Instance,
                    completion.Error);
                ForgetSession(source);
                return true;
            }

            if (!TryConsumeAndNotifyOfferings(source, offerings))
            {
                MissionDiagnostics.Log(
                    "karrec-trade consume-failed character={0} (passage already granted)",
                    source.Identity.Instance);
            }

            SendCompletionProjection(source, message.Target, completion);
            ForgetSession(source);
            MissionDiagnostics.Log(
                "karrec-trade completed character={0} totw-access=granted",
                source.Identity.Instance);
            return true;
        }

        private static bool TryConsumeAndNotifyOfferings(ICharacter source, IList<StagedOffering> offerings)
        {
            if (!TryConsumeOfferings(source, offerings))
            {
                return false;
            }

            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                WindcallerKarrecQuestRuntime.QuestId,
                TradeConsumedFlag,
                "297042,297043");
            NotifyOfferingsRemoved(source, offerings);
            return true;
        }

        private static void NotifyOfferingsRemoved(ICharacter source, IList<StagedOffering> offerings)
        {
            if (source == null || offerings == null)
            {
                return;
            }

            foreach (StagedOffering offering in offerings)
            {
                try
                {
                    CharacterActionMessageHandler.Default.SendDeleteItem(
                        source,
                        (int)offering.Location.Type,
                        offering.Slot);
                }
                catch (Exception)
                {
                }
            }
        }

        private static bool TryFindOfferingsInInventory(
            ICharacter source,
            out IList<StagedOffering> offerings)
        {
            offerings = new List<StagedOffering>();
            if (source == null || source.BaseInventory == null)
            {
                return false;
            }

            StagedOffering burger;
            StagedOffering card;
            if (!TryFindSingleOfferingInCarriedInventory(
                    source,
                    WindcallerKarrecQuestRuntime.BrontoBurgerItemId,
                    out burger)
                || !TryFindSingleOfferingInCarriedInventory(
                    source,
                    WindcallerKarrecQuestRuntime.MaddyCreditCardItemId,
                    out card))
            {
                return false;
            }

            offerings.Add(burger);
            offerings.Add(card);
            return true;
        }

        private static bool TryFindSingleOfferingInCarriedInventory(
            ICharacter source,
            int itemId,
            out StagedOffering offering)
        {
            offering = null;
            foreach (int pageType in new[]
                                        {
                                            (int)IdentityType.Inventory,
                                            (int)IdentityType.OverflowWindow
                                        })
            {
                IInventoryPage page;
                if (!source.BaseInventory.Pages.TryGetValue(pageType, out page) || page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> entry in page.List())
                {
                    Item item = entry.Value as Item;
                    if (ResolveOfferingItemId(item) != itemId)
                    {
                        continue;
                    }

                    offering = new StagedOffering
                               {
                                   ItemId = itemId,
                                   Page = page,
                                   Location =
                                       new Identity
                                       {
                                           Type = (IdentityType)pageType,
                                           Instance = entry.Key
                                       },
                                   Slot = entry.Key,
                                   Item = item
                               };
                    return true;
                }
            }

            return false;
        }

        internal static bool TryResumeDurableCompletion(ICharacter source, Identity karrecIdentity)
        {
            if (WindcallerKarrecQuestRuntime.IsCompleted(source))
            {
                KarrecCompletionResult completed =
                    WindcallerKarrecQuestRuntime.CompleteAfterOfferingsConsumed(source);
                if (!completed.Completed)
                {
                    return false;
                }

                SendCompletionProjection(source, karrecIdentity, completed);
                return true;
            }

            if (!WindcallerKarrecQuestRuntime.IsActive(source))
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            MissionFlagRecord pending = MissionRuntime.Service.GetFlag(
                characterId,
                WindcallerKarrecQuestRuntime.QuestId,
                TradePendingFlag);
            if (pending == null)
            {
                return false;
            }

            MissionFlagRecord consumed = MissionRuntime.Service.GetFlag(
                characterId,
                WindcallerKarrecQuestRuntime.QuestId,
                TradeConsumedFlag);
            if (consumed == null)
            {
                if (!ArePersistedOfferingSlotsConsumed(source, pending.Value))
                {
                    return false;
                }

                MissionOperationResult recovered = MissionRuntime.Service.SetFlag(
                    characterId,
                    WindcallerKarrecQuestRuntime.QuestId,
                    TradeConsumedFlag,
                    "recovered-after-persisted-trade-pending");
                if (IsPersistenceFailure(recovered))
                {
                    return false;
                }
            }

            KarrecCompletionResult completion =
                WindcallerKarrecQuestRuntime.CompleteAfterOfferingsConsumed(source);
            if (!completion.Completed)
            {
                return false;
            }

            SendCompletionProjection(source, karrecIdentity, completion);
            ForgetSession(source);
            return true;
        }

        private static void SendCompletionProjection(
            ICharacter source,
            Identity karrecIdentity,
            KarrecCompletionResult completion)
        {
            // Always push trade-close + dialogue on this Accept. Durable flags previously skipped
            // the UI after a server-side success, so the give-items window looked stuck until zone.
            SendImmediateTradeAcceptanceUi(source, karrecIdentity);

            EnsureProjection(
                source,
                "personal-research-feedback-projected",
                () => WindcallerKarrecPacketSender.TrySendPersonalResearchFeedback(source));
            EnsureProjection(
                source,
                "side-token-projected",
                () => WindcallerKarrecPacketSender.TrySendSideTokenProjection(
                    source,
                    completion.SideTokenValue));
            EnsureProjection(
                source,
                "completion-delete-projected",
                () => WindcallerKarrecPacketSender.TrySendCompletionAndDelete(source));
        }

        private static void SendImmediateTradeAcceptanceUi(ICharacter source, Identity karrecIdentity)
        {
            try
            {
                KnuBotRejectedItemsMessageHandler.Default.Send(source, karrecIdentity, new Item[0]);
                Thread.Sleep(25);
                KnuBotAppendTextMessageHandler.Default.Send(
                    source,
                    karrecIdentity,
                    "Karrec hands you a note covered with strange words and symbols, none of which make any sense to you. You upload the information to your ncu and throw the paper away.",
                    1);
                Thread.Sleep(25);
                KnuBotAppendTextMessageHandler.Default.Send(
                    source,
                    karrecIdentity,
                    "Your devotion to the Cult of Three Winds gains you passage to the sacred Temple "
                    + (string.IsNullOrWhiteSpace(source.Name) ? string.Empty : source.Name)
                    + ". You may now use the gateway.",
                    0);
                Thread.Sleep(25);
                KnuBotAnswerListMessageHandler.Default.Send(
                    source,
                    karrecIdentity,
                    new[] { "Thank you, Karrec.", "Goodbye" });
            }
            catch (Exception exception)
            {
                MissionDiagnostics.Log(
                    "karrec-trade ui-send-failed character={0} error={1}",
                    source == null ? 0 : source.Identity.Instance,
                    exception.Message);
            }
        }

        private static bool EnsureProjection(ICharacter source, string flagKey, Func<bool> sender)
        {
            int characterId = source.Identity.Instance;
            if (MissionRuntime.Service.GetFlag(
                characterId,
                WindcallerKarrecQuestRuntime.QuestId,
                flagKey) != null)
            {
                return true;
            }

            bool sent;
            try
            {
                sent = sender();
            }
            catch (Exception)
            {
                return false;
            }

            if (!sent)
            {
                return false;
            }

            MissionOperationResult flag = MissionRuntime.Service.SetFlag(
                characterId,
                WindcallerKarrecQuestRuntime.QuestId,
                flagKey,
                "true");
            return !IsPersistenceFailure(flag);
        }

        private static bool TryResolveOfferings(
            ICharacter source,
            KarrecTradeSession session,
            out IList<StagedOffering> offerings)
        {
            offerings = new List<StagedOffering>();
            if (!WindcallerKarrecInteractionRules.HasExactOfferings(
                    session.ItemLocations.Keys,
                    session.StagedLocations.Count,
                    session.ContainsUnrecognizedItem))
            {
                return false;
            }

            foreach (int itemId in new[]
                                       {
                                           WindcallerKarrecQuestRuntime.BrontoBurgerItemId,
                                           WindcallerKarrecQuestRuntime.MaddyCreditCardItemId
                                       })
            {
                Identity location;
                if (!session.ItemLocations.TryGetValue(itemId, out location))
                {
                    return false;
                }

                IInventoryPage page;
                if (!source.BaseInventory.Pages.TryGetValue((int)location.Type, out page))
                {
                    return false;
                }

                Item item = page[location.Instance] as Item;
                if (ResolveOfferingItemId(item) != itemId)
                {
                    return false;
                }

                offerings.Add(
                    new StagedOffering
                    {
                        ItemId = itemId,
                        Page = page,
                        Location = location,
                        Slot = location.Instance,
                        Item = item
                    });
            }

            return offerings.Count == 2;
        }

        private static string SerializePendingOfferings(IEnumerable<StagedOffering> offerings)
        {
            return string.Join(
                ";",
                offerings.Select(
                    value => value.ItemId.ToString(CultureInfo.InvariantCulture)
                             + ":" + ((int)value.Location.Type).ToString(CultureInfo.InvariantCulture)
                             + ":" + value.Location.Instance.ToString(CultureInfo.InvariantCulture))
                    .ToArray());
        }

        private static bool ArePersistedOfferingSlotsConsumed(ICharacter source, string pendingValue)
        {
            if (source == null || string.IsNullOrWhiteSpace(pendingValue))
            {
                return false;
            }

            string[] entries = pendingValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var resolvedItemIds = new HashSet<int>();
            foreach (string entry in entries)
            {
                string[] parts = entry.Split(':');
                int itemId;
                int containerType;
                int slot;
                if (parts.Length != 3
                    || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId)
                    || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out containerType)
                    || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out slot)
                    || (itemId != WindcallerKarrecQuestRuntime.BrontoBurgerItemId
                        && itemId != WindcallerKarrecQuestRuntime.MaddyCreditCardItemId)
                    || !resolvedItemIds.Add(itemId))
                {
                    return false;
                }

                IInventoryPage page;
                if (!source.BaseInventory.Pages.TryGetValue(containerType, out page))
                {
                    continue;
                }

                if (ResolveOfferingItemId(page[slot] as Item) == itemId)
                {
                    return false;
                }
            }

            return resolvedItemIds.SetEquals(
                new[]
                {
                    WindcallerKarrecQuestRuntime.BrontoBurgerItemId,
                    WindcallerKarrecQuestRuntime.MaddyCreditCardItemId
                });
        }

        private static bool TryConsumeOfferings(ICharacter source, IList<StagedOffering> offerings)
        {
            foreach (StagedOffering offering in offerings)
            {
                offering.Page.Remove(offering.Slot);
            }

            try
            {
                if (source.BaseInventory.Write())
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }

            foreach (StagedOffering offering in offerings)
            {
                offering.Page.Add(offering.Slot, offering.Item);
            }

            return false;
        }

        private static int ResolveOfferingItemId(IItem item)
        {
            if (item == null)
            {
                return 0;
            }

            if (item.LowID == WindcallerKarrecQuestRuntime.BrontoBurgerItemId
                || item.HighID == WindcallerKarrecQuestRuntime.BrontoBurgerItemId)
            {
                return WindcallerKarrecQuestRuntime.BrontoBurgerItemId;
            }

            if (item.LowID == WindcallerKarrecQuestRuntime.MaddyCreditCardItemId
                || item.HighID == WindcallerKarrecQuestRuntime.MaddyCreditCardItemId)
            {
                return WindcallerKarrecQuestRuntime.MaddyCreditCardItemId;
            }

            return 0;
        }

        private static bool IsHandledKarrecTarget(KarrecTradeSession session, Identity identity)
        {
            if (WindcallerKarrecInteractionRules.IsKarrec(identity))
            {
                return true;
            }

            return session != null
                   && session.KarrecIdentity.Type == identity.Type
                   && session.KarrecIdentity.Instance == identity.Instance;
        }

        private static void MarkUnrecognizedOffering(KarrecTradeSession session, Identity location)
        {
            if (session == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                session.StagedLocations.Add(MakeLocationKey(location));
                session.ContainsUnrecognizedItem = true;
            }
        }

        private static string MakeLocationKey(Identity location)
        {
            return ((int)location.Type).ToString(CultureInfo.InvariantCulture)
                   + ":" + location.Instance.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsPersistenceFailure(MissionOperationResult result)
        {
            return result == null
                   || result.Status == MissionOperationStatus.Rejected
                   || result.Status == MissionOperationStatus.NotFound
                   || result.Status == MissionOperationStatus.Unresolved;
        }

        private static KarrecTradeSession GetSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                KarrecTradeSession session;
                return SessionsByCharacter.TryGetValue(source.Identity.Instance, out session) ? session : null;
            }
        }

        private static void ForgetSession(ICharacter source)
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

        private sealed class KarrecTradeSession
        {
            public KarrecTradeSession()
            {
                this.ItemLocations = new Dictionary<int, Identity>();
                this.StagedLocations = new HashSet<string>(StringComparer.Ordinal);
            }

            public Identity KarrecIdentity { get; set; }

            public IDictionary<int, Identity> ItemLocations { get; private set; }

            public ISet<string> StagedLocations { get; private set; }

            public bool ContainsUnrecognizedItem { get; set; }
        }

        private sealed class StagedOffering
        {
            public int ItemId { get; set; }

            public IInventoryPage Page { get; set; }

            public Identity Location { get; set; }

            public int Slot { get; set; }

            public Item Item { get; set; }
        }
    }
}
