namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;
    using ZoneEngine.Core;

    #endregion

    internal sealed class PlayfieldCorpseAccessRuntimeService
    {
        internal bool TryUseCorpse<TCorpseState>(
            ICharacter looter,
            Identity corpseIdentity,
            IDictionary<int, TCorpseState> corpses,
            TimeSpan itemLootLifetime,
            TimeSpan emptyCleanupDelay,
            Func<TCorpseState, Identity> deadNpcIdentity,
            Func<TCorpseState, DateTime> expiresAtUtc,
            Func<TCorpseState, bool> hasUnlootedItems,
            Action<TCorpseState, bool> setOpened,
            Func<TCorpseState, object> lootClass,
            Action<int> despawnCorpse,
            Action<TCorpseState, TimeSpan, string> extendCorpseLifetime,
            Action<ICharacter, TCorpseState> sendCorpseInventoryUpdate,
            Action<ICharacter, TCorpseState> scheduleCorpseCreditAward,
            Action<TCorpseState, TimeSpan, string> scheduleCorpseDespawn)
            where TCorpseState : class
        {
            if (looter == null || corpseIdentity.Type != IdentityType.Corpse)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseUse reject invalid looter={0} corpse={1}",
                        looter == null ? Identity.None : looter.Identity,
                        corpseIdentity));
                return false;
            }

            TCorpseState corpse;
            if (!corpses.TryGetValue(corpseIdentity.Instance, out corpse))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseUse reject unknown corpse={0} looter={1} registeredCount={2}",
                        corpseIdentity,
                        looter.Identity,
                        corpses.Count));
                return false;
            }

            if (expiresAtUtc(corpse) <= DateTime.UtcNow)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format("CorpseUse reject expired corpse={0} looter={1}", corpseIdentity, looter.Identity));
                despawnCorpse(corpseIdentity.Instance);
                return false;
            }

            setOpened(corpse, true);

            if (hasUnlootedItems(corpse))
            {
                extendCorpseLifetime(corpse, itemLootLifetime, "corpse-use");
                this.SendCorpseInventoryUpdateAndCredits(
                    looter,
                    corpse,
                    sendCorpseInventoryUpdate,
                    scheduleCorpseCreditAward);
            }
            else
            {
                this.SendCorpseInventoryUpdateAndCredits(
                    looter,
                    corpse,
                    sendCorpseInventoryUpdate,
                    scheduleCorpseCreditAward);
            }

            if (!hasUnlootedItems(corpse))
            {
                scheduleCorpseDespawn(corpse, emptyCleanupDelay, "opened-empty");
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "CorpseUse accepted corpse={0} deadNpc={1} looter={2} opened={3} lootClass={4}",
                    corpseIdentity,
                    deadNpcIdentity(corpse),
                    looter.Identity,
                    true,
                    lootClass(corpse)));

            return true;
        }

        internal bool TryUseDeadNpcCorpse<TCorpseState>(
            ICharacter looter,
            Identity deadNpcIdentity,
            IEnumerable<TCorpseState> corpses,
            Func<TCorpseState, Identity> corpseIdentity,
            Func<TCorpseState, Identity> corpseDeadNpcIdentity,
            Func<TCorpseState, DateTime> createdAtUtc,
            Func<ICharacter, Identity, bool> tryUseCorpse,
            out Identity routedCorpseIdentity)
            where TCorpseState : class
        {
            routedCorpseIdentity = Identity.None;

            if (looter == null || deadNpcIdentity.Type != IdentityType.CanbeAffected)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "DeadNpcCorpseUse reject invalid looter={0} deadNpc={1}",
                        looter == null ? Identity.None : looter.Identity,
                        deadNpcIdentity));
                return false;
            }

            TCorpseState corpse = corpses
                .Where(
                    x => corpseDeadNpcIdentity(x).Type == deadNpcIdentity.Type
                         && corpseDeadNpcIdentity(x).Instance == deadNpcIdentity.Instance)
                .OrderByDescending(createdAtUtc)
                .ThenByDescending(x => corpseIdentity(x).Instance)
                .FirstOrDefault();

            if (corpse == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "DeadNpcCorpseUse reject unknown deadNpc={0} looter={1} registeredCount={2}",
                        deadNpcIdentity,
                        looter.Identity,
                        corpses.Count()));
                return false;
            }

            routedCorpseIdentity = corpseIdentity(corpse);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "DeadNpcCorpseUse route deadNpc={0} corpse={1} looter={2} created={3:o}",
                    deadNpcIdentity,
                    routedCorpseIdentity,
                    looter.Identity,
                    createdAtUtc(corpse)));
            return tryUseCorpse(looter, routedCorpseIdentity);
        }

        internal bool TryLootCorpseItem<TCorpseState, TCorpseLootItem>(
            ICharacter looter,
            Identity sourceContainer,
            Identity target,
            int targetPlacement,
            IEnumerable<TCorpseState> corpses,
            Func<TCorpseState, int> corpseInventoryHandle,
            Func<TCorpseState, Identity> corpseIdentity,
            Func<TCorpseState, DateTime> expiresAtUtc,
            Func<TCorpseState, bool> hasUnlootedItems,
            Func<TCorpseState, int> remainingUnlootedItems,
            Func<TCorpseState, TCorpseLootItem> findCorpseLootItem,
            Func<TCorpseLootItem, Item> lootItem,
            Func<TCorpseLootItem, int> lootItemSlot,
            Action<TCorpseLootItem, bool> setLooted,
            Action<TCorpseState, bool> setOpened,
            Func<ICharacter, Item, bool> characterHasUniqueItemAlready,
            Action<ICharacter, string> sendChatText,
            Action<ICharacter> sendUseActionFinished,
            Func<ICharacter, Item, int, CorpseLootInventoryTransferResult> tryAddCorpseLootItem,
            Action<ICharacter, Identity, int> sendCorpseContainerAddItem,
            Action<TCorpseState, TimeSpan, string> scheduleCorpseDespawn,
            Action<TCorpseState, TimeSpan, string> extendCorpseLifetime,
            Action<int> despawnCorpse,
            TimeSpan itemLootLifetime,
            TimeSpan emptyCleanupDelay)
            where TCorpseState : class
            where TCorpseLootItem : class
        {
            if (looter == null || sourceContainer.Type != IdentityType.Backpack)
            {
                return false;
            }

            int corpseInventoryHandleValue = (sourceContainer.Instance >> 16) & 0xffff;
            TCorpseState corpse = corpses.FirstOrDefault(x => corpseInventoryHandle(x) == corpseInventoryHandleValue);

            if (corpse == null)
            {
                return false;
            }

            if (expiresAtUtc(corpse) <= DateTime.UtcNow)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format("CorpseLoot reject expired corpse={0} looter={1}", corpseIdentity(corpse), looter.Identity));
                despawnCorpse(corpseIdentity(corpse).Instance);
                return true;
            }

            if (target != looter.Identity)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseLoot reject target mismatch source={0} target={1} looter={2}",
                        sourceContainer,
                        target,
                        looter.Identity));
                sendUseActionFinished(looter);
                return true;
            }

            int requestedLootSlot = sourceContainer.Instance & 0xffff;
            TCorpseLootItem corpseLootItem = findCorpseLootItem(corpse);
            if (corpseLootItem == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseLoot reject missing item corpse={0} source={1} requestedSlot={2}",
                        corpseIdentity(corpse),
                        sourceContainer,
                        requestedLootSlot));
                sendUseActionFinished(looter);
                return true;
            }

            Item item = lootItem(corpseLootItem);
            if (characterHasUniqueItemAlready(looter, item))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseLoot reject duplicate unique corpse={0} looter={1} source={2} item={3}/{4}",
                        corpseIdentity(corpse),
                        looter.Identity,
                        sourceContainer,
                        item.LowID,
                        item.HighID));
                sendChatText(looter, "You already have this unique item.");
                sendUseActionFinished(looter);
                return true;
            }

            CorpseLootInventoryTransferResult transferResult = tryAddCorpseLootItem(looter, item, targetPlacement);
            if (transferResult.Status == CorpseLootInventoryTransferStatus.NoFreeSlot)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseLoot reject no free inventory slot corpse={0} looter={1}",
                        corpseIdentity(corpse),
                        looter.Identity));
                sendUseActionFinished(looter);
                return true;
            }

            if (transferResult.Status == CorpseLootInventoryTransferStatus.AddFailed)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "CorpseLoot inventory add failed corpse={0} looter={1} targetSlot={2} error={3}",
                        corpseIdentity(corpse),
                        looter.Identity,
                        transferResult.TargetSlot,
                        transferResult.ExceptionMessage));
                sendUseActionFinished(looter);
                return true;
            }

            if (transferResult.Status == CorpseLootInventoryTransferStatus.AddRejected)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseLoot inventory add rejected corpse={0} looter={1} targetPage={2} targetSlot={3} error={4}",
                        corpseIdentity(corpse),
                        looter.Identity,
                        transferResult.TargetPageNumber,
                        transferResult.TargetSlot,
                        transferResult.InventoryError));

                if (transferResult.InventoryError == InventoryError.HaveUniqueAlready)
                {
                    sendChatText(looter, "You already have this unique item.");
                }

                sendUseActionFinished(looter);
                return true;
            }

            setLooted(corpseLootItem, true);
            setOpened(corpse, true);
            sendCorpseContainerAddItem(looter, sourceContainer, targetPlacement);

            if (!hasUnlootedItems(corpse))
            {
                scheduleCorpseDespawn(corpse, emptyCleanupDelay, "looted-empty");
            }
            else
            {
                extendCorpseLifetime(corpse, itemLootLifetime, "loot-remaining");
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "CorpseLoot accepted corpse={0} looter={1} source={2} lootSlot={3} targetSlot={4} ackPlacement={5} cashResync={6} remaining={7}",
                    corpseIdentity(corpse),
                    looter.Identity,
                    sourceContainer,
                    lootItemSlot(corpseLootItem),
                    transferResult.TargetSlot,
                    transferResult.TargetSlot,
                    looter.Stats[StatIds.cash].BaseValue,
                    remainingUnlootedItems(corpse)));

            return true;
        }

        internal void ProcessPendingCorpseCreditAwards<TAward, TCorpseState>(
            IDictionary<int, TAward> pendingCorpseCreditAwards,
            IDictionary<int, TCorpseState> corpses,
            Func<TAward, DateTime> dueAtUtc,
            Func<TAward, int> corpseInstance,
            Func<TAward, Identity> looterIdentity,
            Func<TCorpseState, Identity> corpseIdentity,
            Func<Identity, ICharacter> findLooter,
            Func<ICharacter, bool> looterInPlayfield,
            Action<ICharacter, TCorpseState> awardCorpseCredits)
            where TAward : class
            where TCorpseState : class
        {
            List<TAward> dueAwards = pendingCorpseCreditAwards.Values.Where(x => dueAtUtc(x) <= DateTime.UtcNow).ToList();

            foreach (TAward award in dueAwards)
            {
                int corpseInstanceValue = corpseInstance(award);
                pendingCorpseCreditAwards.Remove(corpseInstanceValue);

                TCorpseState corpse;
                if (!corpses.TryGetValue(corpseInstanceValue, out corpse))
                {
                    continue;
                }

                ICharacter looter = findLooter(looterIdentity(award));
                if (looter == null || !looterInPlayfield(looter))
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        string.Format(
                            "Corpse credits skipped; looter missing corpse={0} looter={1}",
                            corpseIdentity(corpse),
                            looterIdentity(award)));
                    continue;
                }

                awardCorpseCredits(looter, corpse);
            }
        }

        private void SendCorpseInventoryUpdateAndCredits<TCorpseState>(
            ICharacter looter,
            TCorpseState corpse,
            Action<ICharacter, TCorpseState> sendCorpseInventoryUpdate,
            Action<ICharacter, TCorpseState> scheduleCorpseCreditAward)
            where TCorpseState : class
        {
            sendCorpseInventoryUpdate(looter, corpse);
            scheduleCorpseCreditAward(looter, corpse);
        }
    }
}
