using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core;

namespace AORebirth.Core.Playfields;

internal sealed class PlayfieldCorpseAccessRuntimeService
{
	internal bool TryUseCorpse<TCorpseState>(ICharacter looter, Identity corpseIdentity, IDictionary<int, TCorpseState> corpses, TimeSpan itemLootLifetime, TimeSpan emptyCleanupDelay, Func<TCorpseState, Identity> deadNpcIdentity, Func<TCorpseState, DateTime> expiresAtUtc, Func<TCorpseState, bool> isEmpty, Func<TCorpseState, bool> opened, Action<TCorpseState, bool> setOpened, Func<TCorpseState, object> lootClass, Action<int> despawnCorpse, Action<TCorpseState, TimeSpan, string> extendCorpseLifetime, Action<TCorpseState> refreshCorpseInventoryHandle, Action<ICharacter, TCorpseState> sendCorpseInventoryUpdate, Action<ICharacter, TCorpseState> sendCorpseCloseAction, Action<ICharacter> sendUseActionFinished, Action<ICharacter, TCorpseState> scheduleCorpseCreditAward, Action<TCorpseState, TimeSpan, string> scheduleCorpseDespawn) where TCorpseState : class
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Invalid comparison between Unknown and I4
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		if (looter == null || (int)((Identity)(ref corpseIdentity)).Type != 51050)
		{
			LogUtil.Debug((DebugInfoDetail)128, $"CorpseUse reject invalid looter={((looter == null) ? Identity.None : ((IEntity)looter).Identity)} corpse={corpseIdentity}");
			return false;
		}
		if (!corpses.TryGetValue(((Identity)(ref corpseIdentity)).Instance, out var value))
		{
			LogUtil.Debug((DebugInfoDetail)128, $"CorpseUse reject unknown corpse={corpseIdentity} looter={((IEntity)looter).Identity} registeredCount={corpses.Count}");
			return false;
		}
		if (expiresAtUtc(value) <= DateTime.UtcNow)
		{
			LogUtil.Debug((DebugInfoDetail)128, $"CorpseUse reject expired corpse={corpseIdentity} looter={((IEntity)looter).Identity}");
			despawnCorpse(((Identity)(ref corpseIdentity)).Instance);
			return false;
		}
		if (opened(value))
		{
			setOpened(value, arg2: false);
			refreshCorpseInventoryHandle(value);
			sendCorpseCloseAction(looter, value);
			sendUseActionFinished(looter);
			LogUtil.Debug((DebugInfoDetail)128, $"CorpseUse accepted close corpse={corpseIdentity} deadNpc={deadNpcIdentity(value)} looter={((IEntity)looter).Identity} opened=False lootClass={lootClass(value)}");
			return true;
		}
		setOpened(value, arg2: true);
		if (!isEmpty(value))
		{
			extendCorpseLifetime(value, itemLootLifetime, "corpse-use");
			SendCorpseInventoryUpdateAndCredits(looter, value, sendCorpseInventoryUpdate, scheduleCorpseCreditAward);
		}
		else
		{
			SendCorpseInventoryUpdateAndCredits(looter, value, sendCorpseInventoryUpdate, scheduleCorpseCreditAward);
		}
		if (isEmpty(value))
		{
			scheduleCorpseDespawn(value, emptyCleanupDelay, "opened-empty");
		}
		LogUtil.Debug((DebugInfoDetail)128, $"CorpseUse accepted corpse={corpseIdentity} deadNpc={deadNpcIdentity(value)} looter={((IEntity)looter).Identity} opened={true} lootClass={lootClass(value)}");
		return true;
	}

	internal bool TryUseDeadNpcCorpse<TCorpseState>(ICharacter looter, Identity deadNpcIdentity, IEnumerable<TCorpseState> corpses, Func<TCorpseState, Identity> corpseIdentity, Func<TCorpseState, Identity> corpseDeadNpcIdentity, Func<TCorpseState, DateTime> createdAtUtc, Func<ICharacter, Identity, bool> tryUseCorpse, out Identity routedCorpseIdentity) where TCorpseState : class
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Invalid comparison between Unknown and I4
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		routedCorpseIdentity = Identity.None;
		if (looter == null || (int)((Identity)(ref deadNpcIdentity)).Type != 50000)
		{
			LogUtil.Debug((DebugInfoDetail)128, $"DeadNpcCorpseUse reject invalid looter={((looter == null) ? Identity.None : ((IEntity)looter).Identity)} deadNpc={deadNpcIdentity}");
			return false;
		}
		TCorpseState val = corpses.Where(delegate(TCorpseState x)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			Identity val3 = corpseDeadNpcIdentity(x);
			int result;
			if (((Identity)(ref val3)).Type == ((Identity)(ref deadNpcIdentity)).Type)
			{
				val3 = corpseDeadNpcIdentity(x);
				result = ((((Identity)(ref val3)).Instance == ((Identity)(ref deadNpcIdentity)).Instance) ? 1 : 0);
			}
			else
			{
				result = 0;
			}
			return (byte)result != 0;
		}).OrderByDescending(createdAtUtc).ThenByDescending(delegate(TCorpseState x)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			Identity val2 = corpseIdentity(x);
			return ((Identity)(ref val2)).Instance;
		})
			.FirstOrDefault();
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)128, $"DeadNpcCorpseUse reject unknown deadNpc={deadNpcIdentity} looter={((IEntity)looter).Identity} registeredCount={corpses.Count()}");
			return false;
		}
		routedCorpseIdentity = corpseIdentity(val);
		LogUtil.Debug((DebugInfoDetail)128, $"DeadNpcCorpseUse route deadNpc={deadNpcIdentity} corpse={routedCorpseIdentity} looter={((IEntity)looter).Identity} created={createdAtUtc(val):o}");
		return tryUseCorpse(looter, routedCorpseIdentity);
	}

	internal bool TryLootCorpseItem<TCorpseState, TCorpseLootItem>(ICharacter looter, Identity sourceContainer, Identity target, int targetPlacement, IEnumerable<TCorpseState> corpses, Func<TCorpseState, int> corpseInventoryHandle, Func<TCorpseState, Identity> corpseIdentity, Func<TCorpseState, DateTime> expiresAtUtc, Func<TCorpseState, bool> isEmpty, Func<TCorpseState, int> remainingUnlootedItems, Func<TCorpseState, TCorpseLootItem> findCorpseLootItem, Func<TCorpseLootItem, Item> lootItem, Func<TCorpseLootItem, int> lootItemSlot, Action<TCorpseLootItem, bool> setLooted, Action<TCorpseState, bool> setOpened, Func<ICharacter, Item, bool> characterHasUniqueItemAlready, Action<ICharacter, string> sendChatText, Action<ICharacter> sendUseActionFinished, Func<ICharacter, Item, int, CorpseLootInventoryTransferResult> tryAddCorpseLootItem, Action<ICharacter, Identity, int> sendCorpseContainerAddItem, Action<TCorpseState, TimeSpan, string> scheduleCorpseDespawn, Action<TCorpseState, TimeSpan, string> extendCorpseLifetime, Action<int> despawnCorpse, TimeSpan itemLootLifetime, TimeSpan emptyCleanupDelay) where TCorpseState : class where TCorpseLootItem : class
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Invalid comparison between Unknown and I4
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0328: Unknown result type (might be due to invalid IL or missing references)
		//IL_0336: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0381: Invalid comparison between Unknown and I4
		//IL_0437: Unknown result type (might be due to invalid IL or missing references)
		//IL_0445: Unknown result type (might be due to invalid IL or missing references)
		//IL_0452: Unknown result type (might be due to invalid IL or missing references)
		if (looter == null || (int)((Identity)(ref sourceContainer)).Type != 107)
		{
			return false;
		}
		int corpseInventoryHandleValue = (((Identity)(ref sourceContainer)).Instance >> 16) & 0xFFFF;
		TCorpseState val = corpses.FirstOrDefault((TCorpseState x) => corpseInventoryHandle(x) == corpseInventoryHandleValue);
		if (val == null)
		{
			return false;
		}
		if (expiresAtUtc(val) <= DateTime.UtcNow)
		{
			LogUtil.Debug((DebugInfoDetail)128, $"CorpseLoot reject expired corpse={corpseIdentity(val)} looter={((IEntity)looter).Identity}");
			Identity val2 = corpseIdentity(val);
			despawnCorpse(((Identity)(ref val2)).Instance);
			return true;
		}
		if (target != ((IEntity)looter).Identity)
		{
			LogUtil.Debug((DebugInfoDetail)128, $"CorpseLoot reject target mismatch source={sourceContainer} target={target} looter={((IEntity)looter).Identity}");
			sendUseActionFinished(looter);
			return true;
		}
		int num = ((Identity)(ref sourceContainer)).Instance & 0xFFFF;
		TCorpseLootItem val3 = findCorpseLootItem(val);
		if (val3 == null)
		{
			LogUtil.Debug((DebugInfoDetail)128, $"CorpseLoot reject missing item corpse={corpseIdentity(val)} source={sourceContainer} requestedSlot={num}");
			sendUseActionFinished(looter);
			return true;
		}
		Item val4 = lootItem(val3);
		if (characterHasUniqueItemAlready(looter, val4))
		{
			LogUtil.Debug((DebugInfoDetail)128, $"CorpseLoot reject duplicate unique corpse={corpseIdentity(val)} looter={((IEntity)looter).Identity} source={sourceContainer} item={val4.LowID}/{val4.HighID}");
			sendChatText(looter, "You already have this unique item.");
			sendUseActionFinished(looter);
			return true;
		}
		CorpseLootInventoryTransferResult corpseLootInventoryTransferResult = tryAddCorpseLootItem(looter, val4, targetPlacement);
		if (corpseLootInventoryTransferResult.Status == CorpseLootInventoryTransferStatus.NoFreeSlot)
		{
			LogUtil.Debug((DebugInfoDetail)128, $"CorpseLoot reject no free inventory slot corpse={corpseIdentity(val)} looter={((IEntity)looter).Identity}");
			sendUseActionFinished(looter);
			return true;
		}
		if (corpseLootInventoryTransferResult.Status == CorpseLootInventoryTransferStatus.AddFailed)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"CorpseLoot inventory add failed corpse={corpseIdentity(val)} looter={((IEntity)looter).Identity} targetSlot={corpseLootInventoryTransferResult.TargetSlot} error={corpseLootInventoryTransferResult.ExceptionMessage}");
			sendUseActionFinished(looter);
			return true;
		}
		if (corpseLootInventoryTransferResult.Status == CorpseLootInventoryTransferStatus.AddRejected)
		{
			LogUtil.Debug((DebugInfoDetail)128, $"CorpseLoot inventory add rejected corpse={corpseIdentity(val)} looter={((IEntity)looter).Identity} targetPage={corpseLootInventoryTransferResult.TargetPageNumber} targetSlot={corpseLootInventoryTransferResult.TargetSlot} error={corpseLootInventoryTransferResult.InventoryError}");
			if ((int)corpseLootInventoryTransferResult.InventoryError == 1)
			{
				sendChatText(looter, "You already have this unique item.");
			}
			sendUseActionFinished(looter);
			return true;
		}
		setLooted(val3, arg2: true);
		setOpened(val, arg2: true);
		sendCorpseContainerAddItem(looter, sourceContainer, corpseLootInventoryTransferResult.TargetSlot);
		sendChatText(looter, string.Format(CultureInfo.InvariantCulture, "You looted {0}.", ResolveLootItemDisplayName(val4)));
		if (isEmpty(val))
		{
			scheduleCorpseDespawn(val, emptyCleanupDelay, "looted-empty");
		}
		else
		{
			extendCorpseLifetime(val, itemLootLifetime, "loot-remaining");
		}
		LogUtil.Debug((DebugInfoDetail)128, $"CorpseLoot accepted corpse={corpseIdentity(val)} looter={((IEntity)looter).Identity} source={sourceContainer} lootSlot={lootItemSlot(val3)} targetSlot={corpseLootInventoryTransferResult.TargetSlot} ackPlacement={corpseLootInventoryTransferResult.TargetSlot} cashResync={((IStats)looter).Stats[(StatIds)61].BaseValue} remaining={remainingUnlootedItems(val)}");
		return true;
	}

	private static string ResolveLootItemDisplayName(Item item)
	{
		if (item == null)
		{
			return "an item";
		}
		DBItemName val = ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).Get(item.LowID);
		if (val != null && !string.IsNullOrWhiteSpace(val.Name))
		{
			return val.Name;
		}
		val = ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).Get(item.HighID);
		if (val != null && !string.IsNullOrWhiteSpace(val.Name))
		{
			return val.Name;
		}
		return string.Format(CultureInfo.InvariantCulture, "item {0}", item.LowID);
	}

	internal void ProcessPendingCorpseCreditAwards<TAward, TCorpseState>(IDictionary<int, TAward> pendingCorpseCreditAwards, IDictionary<int, TCorpseState> corpses, Func<TAward, DateTime> dueAtUtc, Func<TAward, int> corpseInstance, Func<TAward, Identity> looterIdentity, Func<TCorpseState, Identity> corpseIdentity, Func<Identity, ICharacter> findLooter, Func<ICharacter, bool> looterInPlayfield, Action<ICharacter, TCorpseState> awardCorpseCredits) where TAward : class where TCorpseState : class
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		List<TAward> list = pendingCorpseCreditAwards.Values.Where((TAward x) => dueAtUtc(x) <= DateTime.UtcNow).ToList();
		foreach (TAward item in list)
		{
			int key = corpseInstance(item);
			pendingCorpseCreditAwards.Remove(key);
			if (corpses.TryGetValue(key, out var value))
			{
				ICharacter val = findLooter(looterIdentity(item));
				if (val == null || !looterInPlayfield(val))
				{
					LogUtil.Debug((DebugInfoDetail)128, $"Corpse credits skipped; looter missing corpse={corpseIdentity(value)} looter={looterIdentity(item)}");
				}
				else
				{
					awardCorpseCredits(val, value);
				}
			}
		}
	}

	private void SendCorpseInventoryUpdateAndCredits<TCorpseState>(ICharacter looter, TCorpseState corpse, Action<ICharacter, TCorpseState> sendCorpseInventoryUpdate, Action<ICharacter, TCorpseState> scheduleCorpseCreditAward) where TCorpseState : class
	{
		sendCorpseInventoryUpdate(looter, corpse);
		scheduleCorpseCreditAward(looter, corpse);
	}
}
