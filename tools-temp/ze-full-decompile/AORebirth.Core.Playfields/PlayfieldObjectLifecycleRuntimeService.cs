using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace AORebirth.Core.Playfields;

internal sealed class PlayfieldObjectLifecycleRuntimeService
{
	internal void RemoveInstancedEntity(IInstancedEntity entity)
	{
		Pool.Instance.RemoveObject<IInstancedEntity>(entity);
	}

	internal int DespawnCorpses<TCorpseState>(IDictionary<int, TCorpseState> pendingCorpseSpawns, IDictionary<int, TCorpseState> corpses, Func<string, Identity, bool> shouldDespawn, Func<TCorpseState, string> corpseName, Func<TCorpseState, Identity> deadNpcIdentity, Action<int> despawnCorpse)
	{
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		if (shouldDespawn == null)
		{
			return 0;
		}
		int num = 0;
		foreach (TCorpseState item in (from x in pendingCorpseSpawns
			where shouldDespawn(corpseName(x.Value), deadNpcIdentity(x.Value))
			select x.Value).ToList())
		{
			Identity val = deadNpcIdentity(item);
			pendingCorpseSpawns.Remove(((Identity)(ref val)).Instance);
			num++;
		}
		foreach (int item2 in (from x in corpses
			where shouldDespawn(corpseName(x.Value), deadNpcIdentity(x.Value))
			select x.Key).ToList())
		{
			despawnCorpse(item2);
			num++;
		}
		return num;
	}

	internal void DespawnCorpse(int corpseInstance, Action<Identity> sendDespawn, Action<int> clearNpcCorpseDespawn, Action<int> removeCorpseState, Action<int> removePendingCorpseCreditAward)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)51050;
		((Identity)(ref val)).Instance = corpseInstance;
		Identity val2 = val;
		sendDespawn(val2);
		clearNpcCorpseDespawn(corpseInstance);
		removeCorpseState(corpseInstance);
		removePendingCorpseCreditAward(corpseInstance);
		LogUtil.Debug((DebugInfoDetail)128, $"Corpse despawned corpse={val2}");
	}

	internal void ProcessPendingCorpseSpawns<TCorpseState>(IDictionary<int, TCorpseState> pendingCorpseSpawns, Func<TCorpseState, DateTime> spawnsAtUtc, Func<TCorpseState, Identity> corpseIdentity, Func<TCorpseState, Identity> deadNpcIdentity, Func<Identity, ICharacter> findDeadNpc, Action<ICharacter, Identity> registerCorpse, Action<Identity, Identity> traceCorpseFullUpdate, Action<ICharacter, Identity> sendCorpseFullUpdate)
	{
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		foreach (TCorpseState item in (from x in pendingCorpseSpawns
			where spawnsAtUtc(x.Value) <= DateTime.UtcNow
			select x.Value).ToList())
		{
			Identity val = corpseIdentity(item);
			Identity val2 = deadNpcIdentity(item);
			pendingCorpseSpawns.Remove(((Identity)(ref val2)).Instance);
			ICharacter val3 = findDeadNpc(val2);
			if (val3 == null)
			{
				LogUtil.Debug((DebugInfoDetail)4, $"Skipping corpse spawn corpse={val}; dead NPC no longer exists deadNpc={val2}");
				continue;
			}
			registerCorpse(val3, val);
			traceCorpseFullUpdate(val, val2);
			sendCorpseFullUpdate(val3, val);
		}
	}
}
