using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Playfields;

namespace AORebirth.Core.Playfields;

internal sealed class NpcCorpseLifecycleCoordinator
{
	private readonly Dictionary<int, DateTime> deadNpcDespawnTicks = new Dictionary<int, DateTime>();

	private readonly Playfield playfield;

	private readonly Action<Identity> removeNpcHome;

	internal NpcCorpseLifecycleCoordinator(Playfield playfield, Action<Identity> removeNpcHome)
	{
		this.playfield = playfield;
		this.removeNpcHome = removeNpcHome;
	}

	internal bool HasPendingDeadNpcDespawn(Identity identity)
	{
		return deadNpcDespawnTicks.ContainsKey(((Identity)(ref identity)).Instance);
	}

	internal bool TryGetDeadNpcDespawn(Identity identity, out DateTime despawnTick)
	{
		return deadNpcDespawnTicks.TryGetValue(((Identity)(ref identity)).Instance, out despawnTick);
	}

	internal void ScheduleDeadNpcDespawn(ICharacter target)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, DateTime> dictionary = deadNpcDespawnTicks;
		Identity identity = ((IEntity)target).Identity;
		dictionary[((Identity)(ref identity)).Instance] = DateTime.UtcNow + NpcCorpseLifecycleRules.DeadNpcDespawnDelay;
		PlayfieldLifecycleTrace.Record("cleaning-robot-death-corpse-despawn", "dead-npc-despawn-scheduled", "DeadNpcDespawnScheduled", ((IEntity)target).Identity, "delayMs=" + (int)NpcCorpseLifecycleRules.DeadNpcDespawnDelay.TotalMilliseconds);
	}

	internal void FinalizeNpcDespawn(ICharacter target)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		((IInstancedEntity)target).DoNotDoTimers = true;
		playfield.ClearCombatTracking(((IEntity)target).Identity);
		Dictionary<int, DateTime> dictionary = deadNpcDespawnTicks;
		Identity identity = ((IEntity)target).Identity;
		dictionary.Remove(((Identity)(ref identity)).Instance);
		removeNpcHome(((IEntity)target).Identity);
		playfield.Despawn(((IEntity)target).Identity);
		Pool.Instance.RemoveObject<Character>((Character)target);
		LogUtil.Debug((DebugInfoDetail)4, $"NPC despawned target={((IEntity)target).Identity}");
	}
}
