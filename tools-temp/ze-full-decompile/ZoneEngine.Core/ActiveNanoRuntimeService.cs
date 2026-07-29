using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Nanos;
using AORebirth.Core.Network;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Packets;

namespace ZoneEngine.Core;

public sealed class ActiveNanoRuntimeService
{
	private sealed class ZoneTransferStatSnapshot
	{
		public int Health { get; set; }

		public int CurrentNano { get; set; }
	}

	public sealed class ActiveNanoRemovalTarget
	{
		public int NanoId { get; private set; }

		public Identity ClearIdentity { get; private set; }

		public int NanoInstance { get; private set; }

		public int DurationParameter1 { get; private set; }

		public ActiveNanoRemovalTarget(int nanoId, Identity clearIdentity, int nanoInstance, int durationParameter1)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			NanoId = nanoId;
			ClearIdentity = clearIdentity;
			NanoInstance = nanoInstance;
			DurationParameter1 = durationParameter1;
		}
	}

	public static readonly ActiveNanoRuntimeService Default = new ActiveNanoRuntimeService();

	private const int MaxDurationCentiseconds = 36000000;

	private static readonly object Sync = new object();

	private static readonly Dictionary<int, Dictionary<int, Timer>> ExpiryTimersByCharacter = new Dictionary<int, Dictionary<int, Timer>>();

	private static readonly Dictionary<int, int> NextNanoInstanceByCharacter = new Dictionary<int, int>();

	private static readonly Dictionary<int, List<DBCharacterActiveNano>> ZoneTransferStashByCharacter = new Dictionary<int, List<DBCharacterActiveNano>>();

	private static readonly Dictionary<int, ZoneTransferStatSnapshot> ZoneTransferStatStashByCharacter = new Dictionary<int, ZoneTransferStatSnapshot>();

	private ActiveNanoRuntimeService()
	{
	}

	public bool ApplyActiveNano(ICharacter character, int nanoId, int durationCentiseconds, Identity durationPacketIdentity = default(Identity), int activeStrain = 0)
	{
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Expected O, but got Unknown
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || !NanoLoader.NanoList.ContainsKey(nanoId))
		{
			return false;
		}
		bool flag = nanoId == 157490;
		if (!flag && !CanActivateNano(character, nanoId))
		{
			return false;
		}
		NanoFormula val = NanoLoader.NanoList[nanoId];
		int num = ((activeStrain > 0) ? activeStrain : ResolveNanoStrain(character, nanoId));
		DateTime expiresAtUtc = ((durationCentiseconds > 0) ? DateTime.UtcNow.AddMilliseconds((long)durationCentiseconds * 10L) : DateTime.MaxValue);
		Identity val3;
		if (character.ActiveNanos.TryGetValue(num, out var value) && value != null && value.ID == nanoId)
		{
			ActiveNanoState val2 = (ActiveNanoState)(object)((value is ActiveNanoState) ? value : null);
			if (val2 != null)
			{
				val2.TickCounter = durationCentiseconds;
				val2.TickInterval = durationCentiseconds;
				val2.ExpiresAtUtc = expiresAtUtc;
				val2.NcuCost = val.NCUCost();
				val3 = default(Identity);
				if (!((object)(Identity)(ref durationPacketIdentity)).Equals((object)val3))
				{
					val2.DurationPacketIdentity = durationPacketIdentity;
				}
				val3 = ((IEntity)character).Identity;
				CancelExpiryTimer(((Identity)(ref val3)).Instance, num);
				if (durationCentiseconds > 0)
				{
					ScheduleExpiry(character, num, nanoId, durationCentiseconds);
				}
				SyncPersistedStore(character);
				SyncCurrentNcuStat(character);
				return true;
			}
		}
		RemoveActiveNanoByStrain(character, num, notifyClient: true);
		ActiveNanoState val4 = new ActiveNanoState
		{
			ID = nanoId
		};
		val3 = ((IEntity)character).Identity;
		val4.Instance = AllocateNanoInstance(((Identity)(ref val3)).Instance);
		val4.Nanotype = val.getItemAttribute(75);
		val4.TickCounter = durationCentiseconds;
		val4.TickInterval = durationCentiseconds;
		val4.NcuCost = val.NCUCost();
		val4.ExpiresAtUtc = expiresAtUtc;
		val4.PlayfieldBound = flag;
		val4.DurationPacketIdentity = durationPacketIdentity;
		val3 = ((IEntity)character).Identity;
		val4.DurationParameter1 = ((Identity)(ref val3)).Instance;
		ActiveNanoState value2 = val4;
		character.ActiveNanos[num] = (IActiveNano)(object)value2;
		SyncPersistedStore(character);
		SyncCurrentNcuStat(character);
		if (durationCentiseconds > 0)
		{
			ScheduleExpiry(character, num, nanoId, durationCentiseconds);
		}
		return true;
	}

	public bool CanActivateNano(ICharacter character, int nanoId)
	{
		if (character == null || !NanoLoader.NanoList.ContainsKey(nanoId))
		{
			return false;
		}
		if (nanoId == 157490)
		{
			return true;
		}
		NanoFormula val = NanoLoader.NanoList[nanoId];
		int key = ResolveNanoStrain(character, nanoId);
		int num = val.NCUCost();
		int num2 = GetUsedNcu(character);
		if (character.ActiveNanos.TryGetValue(key, out var value))
		{
			num2 -= GetNanoNcuCost(value);
		}
		return num2 + num <= GetMaxNcu(character);
	}

	public int ResolveNanoStrain(ICharacter character, int nanoId)
	{
		if (NanoEventRuntimeService.Default.HasSummonPetOnUse(nanoId))
		{
			string preferredPetHash = PetSummonNanoCatalog.GetPreferredPetHash(nanoId);
			int num = PetSlotClassifier.ResolveStrain(preferredPetHash);
			if (num > 0)
			{
				return num;
			}
		}
		return NanoLoader.NanoList[nanoId].NanoStrain();
	}

	public bool HasActiveNanoInStrain(ICharacter character, int nanoId, int strain)
	{
		if (character == null || strain <= 0)
		{
			return false;
		}
		IActiveNano value;
		return character.ActiveNanos.TryGetValue(strain, out value) && value != null && value.ID == nanoId;
	}

	public int GetUsedNcu(ICharacter character)
	{
		if (character == null)
		{
			return 0;
		}
		int num = 0;
		foreach (KeyValuePair<int, IActiveNano> activeNano in character.ActiveNanos)
		{
			IActiveNano value = activeNano.Value;
			ActiveNanoState val = (ActiveNanoState)(object)((value is ActiveNanoState) ? value : null);
			if (val == null || !val.PlayfieldBound)
			{
				num += GetNanoNcuCost(activeNano.Value);
			}
		}
		return num;
	}

	public int GetMaxNcu(ICharacter character)
	{
		if (character == null)
		{
			return 0;
		}
		return Math.Max(0, ((IStats)character).Stats[(StatIds)181].Value);
	}

	public void SyncCurrentNcuStat(ICharacter character)
	{
		if (character != null)
		{
			((IStats)character).Stats[(StatIds)180].Value = GetUsedNcu(character);
			((IStats)character).Stats[(StatIds)180].Changed = false;
		}
	}

	public void HandleRemoveFriendlyNano(IZoneClient client, CharacterActionMessage message)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || client.Controller == null || client.Controller.Character == null || message == null)
		{
			return;
		}
		ICharacter character = client.Controller.Character;
		List<ActiveNanoRemovalTarget> list = BuildRemovalTargets(character, message);
		if (list.Count == 0)
		{
			((IClient)client).Server.Info((IClient)(object)client, "RemoveFriendlyNano no targets target={0} p1={1} p2={2} active={3}", new object[4]
			{
				message.Target,
				message.Parameter1,
				message.Parameter2,
				character.ActiveNanos.Count
			});
			BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.AcknowledgeRemoveFriendlyNano(character, message, (character.ActiveNanos.Count == 1) ? character.ActiveNanos.Values.First().ID : 0);
			return;
		}
		foreach (ActiveNanoRemovalTarget item in list)
		{
			RemoveActiveNanoByNanoId(character, item.NanoId, notifyClient: false);
		}
		SyncCurrentNcuStat(character);
		((IStats)character).Stats.ClearChangedFlags();
		((IClient)client).Server.Info((IClient)(object)client, "RemoveFriendlyNano clearing nanoIds={0}", new object[1] { string.Join(",", list.Select((ActiveNanoRemovalTarget x) => x.NanoId.ToString())) });
		BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.CompleteFriendlyNanoRemoval(character, message, list);
		SyncPersistedStore(character);
	}

	private List<ActiveNanoRemovalTarget> BuildRemovalTargets(ICharacter character, CharacterActionMessage message)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		List<ActiveNanoRemovalTarget> list = new List<ActiveNanoRemovalTarget>();
		int num = ResolveNanoIdFromRemoveMessage(character, message);
		if (num > 0)
		{
			Identity clearIdentity = ResolveClearIdentity(character, num);
			int nanoInstance = ResolveNanoInstance(character, num);
			int durationParameter = ResolveDurationParameter1(character, num);
			list.Add(new ActiveNanoRemovalTarget(num, clearIdentity, nanoInstance, durationParameter));
			return list;
		}
		foreach (KeyValuePair<int, IActiveNano> item in character.ActiveNanos.ToList())
		{
			list.Add(new ActiveNanoRemovalTarget(item.Value.ID, ResolveClearIdentity(character, item.Value), item.Value.Instance, ResolveDurationParameter1(character, item.Value)));
		}
		return list;
	}

	private Identity ResolveClearIdentity(ICharacter character, int nanoId)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		foreach (IActiveNano value in character.ActiveNanos.Values)
		{
			if (value.ID != nanoId)
			{
				continue;
			}
			return ResolveClearIdentity(character, value);
		}
		return ((IEntity)character).Identity;
	}

	private Identity ResolveClearIdentity(ICharacter character, IActiveNano activeNano)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		ActiveNanoState val = (ActiveNanoState)(object)((activeNano is ActiveNanoState) ? activeNano : null);
		if (val != null)
		{
			Identity durationPacketIdentity = val.DurationPacketIdentity;
			if (((Identity)(ref durationPacketIdentity)).Instance != 0)
			{
				return val.DurationPacketIdentity;
			}
		}
		return ((IEntity)character).Identity;
	}

	private int ResolveNanoInstance(ICharacter character, int nanoId)
	{
		foreach (IActiveNano value in character.ActiveNanos.Values)
		{
			if (value.ID == nanoId)
			{
				return value.Instance;
			}
		}
		return 0;
	}

	private int ResolveDurationParameter1(ICharacter character, int nanoId)
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		foreach (IActiveNano value in character.ActiveNanos.Values)
		{
			if (value.ID != nanoId)
			{
				continue;
			}
			return ResolveDurationParameter1(character, value);
		}
		Identity identity = ((IEntity)character).Identity;
		return ((Identity)(ref identity)).Instance;
	}

	private int ResolveDurationParameter1(ICharacter character, IActiveNano activeNano)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		ActiveNanoState val = (ActiveNanoState)(object)((activeNano is ActiveNanoState) ? activeNano : null);
		if (val != null && val.DurationParameter1 > 0)
		{
			return val.DurationParameter1;
		}
		Identity identity = ((IEntity)character).Identity;
		return ((Identity)(ref identity)).Instance;
	}

	public bool TryHandleRemoveFriendlyNano(IZoneClient client, CharacterActionMessage message)
	{
		HandleRemoveFriendlyNano(client, message);
		return true;
	}

	public void ForceFriendlyNanoRemoval(IZoneClient client, CharacterActionMessage message)
	{
		HandleRemoveFriendlyNano(client, message);
	}

	public void PrepareCharacterForLogin(ICharacter character)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (character != null && character.ActiveNanos.Count != 0)
		{
			Identity identity = ((IEntity)character).Identity;
			CancelAllExpiryTimersForCharacter(((Identity)(ref identity)).Instance);
			character.ActiveNanos.Clear();
			SyncCurrentNcuStat(character);
		}
	}

	public void PersistCharacterActiveNanos(ICharacter character)
	{
		if (character != null)
		{
			SyncPersistedStore(character);
		}
	}

	public void ClearPlayfieldBoundActiveNanos(ICharacter character)
	{
		if (character != null)
		{
			int[] array = (from entry in character.ActiveNanos
				where entry.Value is ActiveNanoState && ((ActiveNanoState)entry.Value).PlayfieldBound
				select entry.Key).ToArray();
			int[] array2 = array;
			foreach (int strain in array2)
			{
				RemoveActiveNanoByStrain(character, strain, notifyClient: true);
			}
		}
	}

	public void RemoveActiveNanoInStrain(ICharacter character, int strain, bool notifyClient)
	{
		RemoveActiveNanoByStrain(character, strain, notifyClient);
	}

	public void HandlePlayfieldLeave(ICharacter character)
	{
		if (character != null)
		{
			StashZoneTransferNanos(character);
			StashZoneTransferStats(character);
			PetRuntimeService.Default.StashPetForZoneTransfer(character);
			ClearPlayfieldBoundActiveNanos(character);
			RevokeImplantAccessOnPlayfieldLeave(character);
			PersistCharacterActiveNanos(character);
		}
	}

	public void RevokeImplantAccessOnPlayfieldLeave(ICharacter character)
	{
		Character val = (Character)(object)((character is Character) ? character : null);
		if (val != null)
		{
			val.GrantImplantAccess(-1);
		}
	}

	public void RestoreCharacterActiveNanos(ICharacter character, bool notifyClient)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return;
		}
		Identity identity = ((IEntity)character).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		DateTime utcNow = DateTime.UtcNow;
		List<DBCharacterActiveNano> list = TakeZoneTransferStash(instance);
		bool flag = list != null && list.Count > 0;
		if (!flag)
		{
			Dao<DBCharacterActiveNano, CharacterActiveNanosDao>.Instance.DeleteExpiredActiveNanos(instance, utcNow);
			list = Dao<DBCharacterActiveNano, CharacterActiveNanosDao>.Instance.ReadActiveNanos(instance);
		}
		if (list == null || list.Count == 0)
		{
			return;
		}
		List<DBCharacterActiveNano> list2 = new List<DBCharacterActiveNano>();
		bool flag2 = false;
		foreach (DBCharacterActiveNano item in list)
		{
			if (!NanoLoader.NanoList.ContainsKey(item.NanoId))
			{
				continue;
			}
			DateTime dateTime = ((item.ExpiresAtUtcTicks > 0) ? new DateTime(item.ExpiresAtUtcTicks, DateTimeKind.Utc) : DateTime.MaxValue);
			int num = GetRemainingDurationCentiseconds(dateTime, utcNow, item.DurationCentiseconds);
			bool flag3 = dateTime == DateTime.MaxValue && NanoEventRuntimeService.Default.HasSummonPetOnUse(item.NanoId);
			if (num <= 0 && !flag3)
			{
				continue;
			}
			if (num <= 0 && flag3)
			{
				if (!flag && !PetRuntimeService.Default.HasPendingRestoreForStrain(instance, item.Strain))
				{
					continue;
				}
				num = 0;
			}
			list2.Add(item);
			RestoreActiveNano(character, item, dateTime, num);
			flag2 = true;
		}
		Dao<DBCharacterActiveNano, CharacterActiveNanosDao>.Instance.ReplaceActiveNanos(instance, (IEnumerable<DBCharacterActiveNano>)list2);
		if (!PetRuntimeService.Default.HasPendingRestore(instance))
		{
			CleanupOrphanSummonPetNanos(character, notifyClient);
		}
		if (notifyClient && flag2)
		{
			NotifyClientActiveNanosRestored(character);
		}
		foreach (KeyValuePair<int, IActiveNano> item2 in character.ActiveNanos.ToList())
		{
			if (!NanoEventRuntimeService.Default.HasSummonPetOnUse(item2.Value.ID))
			{
				PetShellItemService.Default.GiveShellAfterNanoRestore(character, item2.Value.ID);
			}
		}
		SyncCurrentNcuStat(character);
	}

	public void CleanupOrphanSummonPetNanosAfterPetRestore(ICharacter character)
	{
		CleanupOrphanSummonPetNanos(character, notifyClient: true);
	}

	public void PurgeOrphanSummonNanoInStrain(ICharacter character, int strain, bool notifyClient)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		if (character != null && character.ActiveNanos.TryGetValue(strain, out var value) && value != null && NanoEventRuntimeService.Default.HasSummonPetOnUse(value.ID) && !PetRuntimeService.Default.HasActivePetInStrain(character, strain))
		{
			PetRuntimeService @default = PetRuntimeService.Default;
			Identity identity = ((IEntity)character).Identity;
			if (!@default.HasPendingRestoreForStrain(((Identity)(ref identity)).Instance, strain))
			{
				RemoveActiveNanoByStrain(character, strain, notifyClient);
			}
		}
	}

	private void CleanupOrphanSummonPetNanos(ICharacter character, bool notifyClient)
	{
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return;
		}
		foreach (KeyValuePair<int, IActiveNano> item in character.ActiveNanos.ToList())
		{
			if (NanoEventRuntimeService.Default.HasSummonPetOnUse(item.Value.ID) && !PetRuntimeService.Default.HasActivePetInStrain(character, item.Key))
			{
				PetRuntimeService @default = PetRuntimeService.Default;
				Identity identity = ((IEntity)character).Identity;
				if (!@default.HasPendingRestoreForStrain(((Identity)(ref identity)).Instance, item.Key))
				{
					RemoveActiveNanoByStrain(character, item.Key, notifyClient);
				}
			}
		}
	}

	public void SchedulePostLoginNanoRestore(IZoneClient client)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || client.Controller == null || client.Controller.Character == null)
		{
			return;
		}
		Identity identity = ((IEntity)client.Controller.Character).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		bool flag = HasZoneTransferStash(instance);
		bool flag2 = Dao<DBCharacterActiveNano, CharacterActiveNanosDao>.Instance.HasActiveNanos(instance);
		bool flag3 = PetRuntimeService.Default.HasPendingRestore(instance);
		if (!flag && !flag2)
		{
			if (flag3)
			{
				PetRuntimeService.Default.ClearPendingRestoreForOwner(instance);
				LogUtil.Debug((DebugInfoDetail)256, "Cleared stale pet pending restore on login char=" + instance);
			}
			return;
		}
		int restoreDelayMilliseconds = (flag ? 250 : 750);
		ThreadPool.QueueUserWorkItem(delegate
		{
			Thread.Sleep(restoreDelayMilliseconds);
			ICharacter val2 = ((client.Controller != null) ? client.Controller.Character : null);
			if (val2 != null && ((IDynel)val2).Controller != null && ((IDynel)val2).Controller.Client != null)
			{
				RestoreCharacterActiveNanos(val2, notifyClient: true);
			}
		});
		if (!(flag && flag3))
		{
			return;
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			Thread.Sleep(restoreDelayMilliseconds + 500);
			ICharacter val = ((client.Controller != null) ? client.Controller.Character : null);
			if (val != null && ((IInstancedEntity)val).Playfield != null)
			{
				PetRuntimeService.Default.TryRestorePetAfterZoneIn(val);
			}
		});
	}

	private void NotifyClientActiveNanosRestored(ICharacter character)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IDynel)character).Controller == null || ((IDynel)character).Controller.Client == null)
		{
			return;
		}
		foreach (KeyValuePair<int, IActiveNano> item in character.ActiveNanos.ToList())
		{
			IActiveNano value = item.Value;
			BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.NotifyActiveNanoDuration(character, ((IEntity)character).Identity, value.ID, value.TickCounter);
		}
		SimpleCharFullUpdate.SendToOne(character, ((IDynel)character).Controller.Client);
	}

	private void RestoreActiveNano(ICharacter character, DBCharacterActiveNano persisted, DateTime expiresAtUtc, int remainingCentiseconds)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Expected O, but got Unknown
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		NanoFormula val = NanoLoader.NanoList[persisted.NanoId];
		ActiveNanoState val2 = new ActiveNanoState
		{
			ID = persisted.NanoId
		};
		Identity identity;
		int instance;
		if (persisted.NanoInstance <= 0)
		{
			identity = ((IEntity)character).Identity;
			instance = AllocateNanoInstance(((Identity)(ref identity)).Instance);
		}
		else
		{
			instance = persisted.NanoInstance;
		}
		val2.Instance = instance;
		val2.Nanotype = val.getItemAttribute(75);
		val2.TickCounter = remainingCentiseconds;
		val2.TickInterval = ((persisted.DurationCentiseconds > 0) ? persisted.DurationCentiseconds : remainingCentiseconds);
		val2.NcuCost = val.NCUCost();
		val2.ExpiresAtUtc = expiresAtUtc;
		val2.DurationPacketIdentity = ((IEntity)character).Identity;
		ActiveNanoState val3 = val2;
		if (val3.DurationParameter1 <= 0)
		{
			identity = ((IEntity)character).Identity;
			val3.DurationParameter1 = ((Identity)(ref identity)).Instance;
		}
		character.ActiveNanos[persisted.Strain] = (IActiveNano)(object)val3;
		ScheduleExpiry(character, persisted.Strain, persisted.NanoId, remainingCentiseconds);
	}

	private bool RemoveActiveNanoByNanoId(ICharacter character, int nanoId, bool notifyClient)
	{
		if (character == null)
		{
			return false;
		}
		KeyValuePair<int, IActiveNano> keyValuePair = character.ActiveNanos.FirstOrDefault((KeyValuePair<int, IActiveNano> x) => x.Value.ID == nanoId);
		if (keyValuePair.Value == null)
		{
			return false;
		}
		RemoveActiveNanoByStrain(character, keyValuePair.Key, notifyClient);
		return true;
	}

	private void RemoveActiveNanoByStrain(ICharacter character, int strain, bool notifyClient)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		if (character.ActiveNanos.TryGetValue(strain, out var value))
		{
			int iD = value.ID;
			int instance = value.Instance;
			character.ActiveNanos.Remove(strain);
			Identity identity = ((IEntity)character).Identity;
			CancelExpiryTimer(((Identity)(ref identity)).Instance, strain);
			if (NanoEventRuntimeService.Default.HasSummonPetOnUse(iD))
			{
				PetRuntimeService.Default.DismissPetByStrain(character, strain);
			}
			if (notifyClient)
			{
				BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.CompleteFriendlyNanoRemoval(character, iD, ((IEntity)character).Identity, instance);
			}
			SyncPersistedStore(character);
			SyncCurrentNcuStat(character);
			((IStats)character).Stats.ClearChangedFlags();
		}
	}

	private int GetNanoNcuCost(IActiveNano activeNano)
	{
		if (activeNano == null)
		{
			return 0;
		}
		ActiveNanoState val = (ActiveNanoState)(object)((activeNano is ActiveNanoState) ? activeNano : null);
		if (val != null && val.NcuCost > 0)
		{
			return val.NcuCost;
		}
		if (NanoLoader.NanoList.ContainsKey(activeNano.ID))
		{
			return Math.Max(0, NanoLoader.NanoList[activeNano.ID].NCUCost());
		}
		return 0;
	}

	private void ScheduleExpiry(ICharacter character, int strain, int nanoId, int durationCentiseconds)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		int num = ClampDurationCentiseconds(durationCentiseconds);
		if (num <= 0)
		{
			return;
		}
		Identity identity = ((IEntity)character).Identity;
		int characterId = ((Identity)(ref identity)).Instance;
		CancelExpiryTimer(characterId, strain);
		Timer timer = null;
		timer = new Timer(delegate
		{
			try
			{
				if (character.ActiveNanos.TryGetValue(strain, out var value2) && value2.ID == nanoId)
				{
					RemoveActiveNanoByStrain(character, strain, ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null);
				}
			}
			finally
			{
				if (timer != null)
				{
					timer.Dispose();
				}
				RemoveExpiryTimerEntry(characterId, strain);
			}
		}, null, (long)num * 10L, -1L);
		lock (Sync)
		{
			if (!ExpiryTimersByCharacter.TryGetValue(characterId, out var value))
			{
				value = new Dictionary<int, Timer>();
				ExpiryTimersByCharacter[characterId] = value;
			}
			value[strain] = timer;
		}
	}

	private void CancelExpiryTimer(int characterId, int strain)
	{
		lock (Sync)
		{
			if (ExpiryTimersByCharacter.TryGetValue(characterId, out var value) && value.TryGetValue(strain, out var value2))
			{
				value2.Dispose();
				value.Remove(strain);
				if (value.Count == 0)
				{
					ExpiryTimersByCharacter.Remove(characterId);
				}
			}
		}
	}

	private void RemoveExpiryTimerEntry(int characterId, int strain)
	{
		lock (Sync)
		{
			if (ExpiryTimersByCharacter.TryGetValue(characterId, out var value))
			{
				value.Remove(strain);
				if (value.Count == 0)
				{
					ExpiryTimersByCharacter.Remove(characterId);
				}
			}
		}
	}

	private void StashZoneTransferNanos(ICharacter character)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)character).Identity;
		int characterId = ((Identity)(ref identity)).Instance;
		List<DBCharacterActiveNano> list = (from row in ((IEnumerable<KeyValuePair<int, IActiveNano>>)character.ActiveNanos).Select((Func<KeyValuePair<int, IActiveNano>, DBCharacterActiveNano>)delegate(KeyValuePair<int, IActiveNano> entry)
			{
				//IL_0023: Unknown result type (might be due to invalid IL or missing references)
				//IL_0028: Unknown result type (might be due to invalid IL or missing references)
				//IL_0035: Unknown result type (might be due to invalid IL or missing references)
				//IL_0042: Unknown result type (might be due to invalid IL or missing references)
				//IL_0050: Unknown result type (might be due to invalid IL or missing references)
				//IL_005d: Unknown result type (might be due to invalid IL or missing references)
				//IL_007b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0091: Expected O, but got Unknown
				IActiveNano value = entry.Value;
				ActiveNanoState val = (ActiveNanoState)(object)((value is ActiveNanoState) ? value : null);
				return (val == null || val.PlayfieldBound) ? ((DBCharacterActiveNano)null) : new DBCharacterActiveNano
				{
					CharacterId = characterId,
					NanoId = val.ID,
					Strain = entry.Key,
					NanoInstance = val.Instance,
					DurationCentiseconds = ((val.TickInterval > 0) ? val.TickInterval : val.TickCounter),
					ExpiresAtUtcTicks = val.ExpiresAtUtc.Ticks
				};
			})
			where row != null
			select row).ToList();
		lock (Sync)
		{
			if (list.Count == 0)
			{
				ZoneTransferStashByCharacter.Remove(characterId);
			}
			else
			{
				ZoneTransferStashByCharacter[characterId] = list;
			}
		}
	}

	public bool HasZoneTransferStash(int characterId)
	{
		return HasZoneTransferNanoStash(characterId) || HasZoneTransferStatStash(characterId);
	}

	public bool HasZoneTransferNanoStash(int characterId)
	{
		lock (Sync)
		{
			List<DBCharacterActiveNano> value;
			return ZoneTransferStashByCharacter.TryGetValue(characterId, out value) && value != null && value.Count > 0;
		}
	}

	public bool HasZoneTransferStatStash(int characterId)
	{
		lock (Sync)
		{
			return ZoneTransferStatStashByCharacter.ContainsKey(characterId);
		}
	}

	public void TryRestoreZoneTransferStats(ICharacter character)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return;
		}
		Identity identity = ((IEntity)character).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneTransferStatSnapshot value;
		lock (Sync)
		{
			if (!ZoneTransferStatStashByCharacter.TryGetValue(instance, out value) || value == null)
			{
				return;
			}
			ZoneTransferStatStashByCharacter.Remove(instance);
		}
		int num = Math.Max(1, ((IStats)character).Stats[(StatIds)1].Value);
		int num2 = Math.Max(0, Math.Min(value.Health, num));
		int num3 = Math.Max(0, value.CurrentNano);
		((IStats)character).Stats[(StatIds)27].Value = num2;
		((IStats)character).Stats[(StatIds)27].BaseValue = (uint)num2;
		((IStats)character).Stats[(StatIds)214].Value = num3;
		((IStats)character).Stats[(StatIds)214].BaseValue = (uint)num3;
		SendRestoredCombatStatsToClient(character, num2, num3);
		LogUtil.Debug((DebugInfoDetail)256, $"RestoreZoneTransferStats char={((IEntity)character).Identity} hp={num2}/{num} np={num3}");
	}

	private void StashZoneTransferStats(ICharacter character)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		if (character != null)
		{
			Identity identity = ((IEntity)character).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			lock (Sync)
			{
				ZoneTransferStatStashByCharacter[instance] = new ZoneTransferStatSnapshot
				{
					Health = ((IStats)character).Stats[(StatIds)27].Value,
					CurrentNano = ((IStats)character).Stats[(StatIds)214].Value
				};
			}
			PersistCharacterCombatStats(character);
			LogUtil.Debug((DebugInfoDetail)256, $"StashZoneTransferStats char={((IEntity)character).Identity} hp={((IStats)character).Stats[(StatIds)27].Value}/{((IStats)character).Stats[(StatIds)1].Value} np={((IStats)character).Stats[(StatIds)214].Value}");
		}
	}

	private void PersistCharacterCombatStats(ICharacter character)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		if (character != null)
		{
			Identity identity = ((IEntity)character).Identity;
			UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)27, ((IStats)character).Stats[(StatIds)27].Value);
			identity = ((IEntity)character).Identity;
			UpsertCharacterStat(((Identity)(ref identity)).Instance, (StatIds)214, ((IStats)character).Stats[(StatIds)214].Value);
		}
	}

	private void UpsertCharacterStat(int characterId, StatIds statId, int value)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected I4, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected I4, but got Unknown
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		DBStats val = ((Dao<DBStats, StatDao>)(object)Dao<DBStats, StatDao>.Instance).GetAll((object)new
		{
			Type = 50000,
			Instance = characterId,
			StatId = (int)statId
		}).FirstOrDefault();
		if (val == null)
		{
			((Dao<DBStats, StatDao>)(object)Dao<DBStats, StatDao>.Instance).Add(new DBStats
			{
				Type = 50000,
				Instance = characterId,
				StatId = (int)statId,
				StatValue = value
			}, (IDbConnection)null, (IDbTransaction)null, true);
		}
		else
		{
			val.StatValue = value;
			((Dao<DBStats, StatDao>)(object)Dao<DBStats, StatDao>.Instance).Save(val, (object)null, (IDbConnection)null, (IDbTransaction)null);
		}
	}

	private void SendRestoredCombatStatsToClient(ICharacter character, int health, int currentNano)
	{
		if (character != null && ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
		{
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 27, (uint)Math.Max(0, health));
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 214, (uint)Math.Max(0, currentNano));
		}
	}

	private List<DBCharacterActiveNano> TakeZoneTransferStash(int characterId)
	{
		lock (Sync)
		{
			if (!ZoneTransferStashByCharacter.TryGetValue(characterId, out var value) || value == null || value.Count == 0)
			{
				return null;
			}
			ZoneTransferStashByCharacter.Remove(characterId);
			return ((IEnumerable<DBCharacterActiveNano>)value).Select((Func<DBCharacterActiveNano, DBCharacterActiveNano>)((DBCharacterActiveNano row) => new DBCharacterActiveNano
			{
				CharacterId = row.CharacterId,
				NanoId = row.NanoId,
				Strain = row.Strain,
				NanoInstance = row.NanoInstance,
				DurationCentiseconds = row.DurationCentiseconds,
				ExpiresAtUtcTicks = row.ExpiresAtUtcTicks
			})).ToList();
		}
	}

	private void CancelAllExpiryTimersForCharacter(int characterId)
	{
		lock (Sync)
		{
			if (!ExpiryTimersByCharacter.TryGetValue(characterId, out var value))
			{
				return;
			}
			foreach (Timer value2 in value.Values)
			{
				value2.Dispose();
			}
			ExpiryTimersByCharacter.Remove(characterId);
		}
	}

	private void SyncPersistedStore(ICharacter character)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		if (character != null)
		{
			Identity identity = ((IEntity)character).Identity;
			int characterId = ((Identity)(ref identity)).Instance;
			IEnumerable<DBCharacterActiveNano> enumerable = from row in ((IEnumerable<KeyValuePair<int, IActiveNano>>)character.ActiveNanos).Select((Func<KeyValuePair<int, IActiveNano>, DBCharacterActiveNano>)delegate(KeyValuePair<int, IActiveNano> entry)
				{
					//IL_0023: Unknown result type (might be due to invalid IL or missing references)
					//IL_0028: Unknown result type (might be due to invalid IL or missing references)
					//IL_0035: Unknown result type (might be due to invalid IL or missing references)
					//IL_0042: Unknown result type (might be due to invalid IL or missing references)
					//IL_0050: Unknown result type (might be due to invalid IL or missing references)
					//IL_005d: Unknown result type (might be due to invalid IL or missing references)
					//IL_007b: Unknown result type (might be due to invalid IL or missing references)
					//IL_0091: Expected O, but got Unknown
					IActiveNano value = entry.Value;
					ActiveNanoState val = (ActiveNanoState)(object)((value is ActiveNanoState) ? value : null);
					return (val == null || val.PlayfieldBound) ? ((DBCharacterActiveNano)null) : new DBCharacterActiveNano
					{
						CharacterId = characterId,
						NanoId = val.ID,
						Strain = entry.Key,
						NanoInstance = val.Instance,
						DurationCentiseconds = ((val.TickInterval > 0) ? val.TickInterval : val.TickCounter),
						ExpiresAtUtcTicks = val.ExpiresAtUtc.Ticks
					};
				})
				where row != null
				select row;
			Dao<DBCharacterActiveNano, CharacterActiveNanosDao>.Instance.ReplaceActiveNanos(characterId, enumerable);
		}
	}

	private int GetRemainingDurationCentiseconds(DateTime expiresAtUtc, DateTime nowUtc, int originalDurationCentiseconds)
	{
		int num;
		if (expiresAtUtc == DateTime.MaxValue)
		{
			num = originalDurationCentiseconds;
		}
		else
		{
			double totalMilliseconds = (expiresAtUtc - nowUtc).TotalMilliseconds;
			if (totalMilliseconds <= 0.0)
			{
				return 0;
			}
			long num2 = (long)Math.Ceiling(totalMilliseconds / 10.0);
			if (num2 > int.MaxValue)
			{
				num2 = 2147483647L;
			}
			num = (int)num2;
		}
		if (originalDurationCentiseconds > 0)
		{
			num = Math.Min(num, originalDurationCentiseconds);
		}
		return ClampDurationCentiseconds(num);
	}

	private int ClampDurationCentiseconds(int durationCentiseconds)
	{
		if (durationCentiseconds <= 0)
		{
			return 0;
		}
		return Math.Min(durationCentiseconds, 36000000);
	}

	private int TryResolveNanoInstance(ICharacter character, CharacterActionMessage message, int nanoId)
	{
		if (message != null && message.Parameter1 > 0)
		{
			foreach (IActiveNano value in character.ActiveNanos.Values)
			{
				if (value.Instance == message.Parameter1)
				{
					return value.Instance;
				}
			}
		}
		foreach (KeyValuePair<int, IActiveNano> activeNano in character.ActiveNanos)
		{
			if (activeNano.Value.ID == nanoId)
			{
				return activeNano.Value.Instance;
			}
		}
		if (message != null && message.Parameter1 > 0)
		{
			return message.Parameter1;
		}
		return 0;
	}

	private int TryResolveNanoIdByInstance(ICharacter character, CharacterActionMessage message)
	{
		if (message.Parameter1 <= 0)
		{
			return 0;
		}
		foreach (IActiveNano value in character.ActiveNanos.Values)
		{
			if (value.Instance == message.Parameter1)
			{
				return value.ID;
			}
		}
		return 0;
	}

	private int AllocateNanoInstance(int characterId)
	{
		lock (Sync)
		{
			if (!NextNanoInstanceByCharacter.TryGetValue(characterId, out var value))
			{
				value = 1;
			}
			NextNanoInstanceByCharacter[characterId] = value + 1;
			return value;
		}
	}

	private int ResolveNanoIdFromRemoveMessage(ICharacter character, CharacterActionMessage message)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		Identity target = message.Target;
		if ((int)((Identity)(ref target)).Type == 53019)
		{
			target = message.Target;
			if (((Identity)(ref target)).Instance != 0)
			{
				target = message.Target;
				return ((Identity)(ref target)).Instance;
			}
		}
		if (message.Parameter2 > 0)
		{
			return message.Parameter2;
		}
		target = message.Target;
		if (((Identity)(ref target)).Instance > 0)
		{
			Dictionary<int, NanoFormula> nanoList = NanoLoader.NanoList;
			target = message.Target;
			if (nanoList.ContainsKey(((Identity)(ref target)).Instance))
			{
				target = message.Target;
				return ((Identity)(ref target)).Instance;
			}
		}
		int num = TryResolveNanoIdByInstance(character, message);
		if (num > 0)
		{
			return num;
		}
		if (character.ActiveNanos.Count == 1)
		{
			return character.ActiveNanos.Values.First().ID;
		}
		return 0;
	}
}
