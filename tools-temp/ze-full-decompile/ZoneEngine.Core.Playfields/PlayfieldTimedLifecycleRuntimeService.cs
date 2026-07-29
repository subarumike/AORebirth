using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldTimedLifecycleRuntimeService
{
	internal void ProcessHeartbeatLifecycle(Identity playfieldIdentity, Func<IEnumerable<ICharacter>> characters, Func<Identity, bool> hasPendingDeadNpcDespawn, Action processPendingCorpseSpawns, Action processCorpseDespawns, Action processPendingCorpseCreditAwards, Func<ICharacter, bool> processDeadNpcDespawn, Action<ICharacter> processRegeneration, Action<ICharacter> processCombatTick, Action<ICharacter> processNpcPatrolTick, Action<ICharacter> processFollow, Action<ICharacter> processPlayerCollision)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		Require(characters, "characters");
		Require(hasPendingDeadNpcDespawn, "hasPendingDeadNpcDespawn");
		Require(processPendingCorpseSpawns, "processPendingCorpseSpawns");
		Require(processCorpseDespawns, "processCorpseDespawns");
		Require(processPendingCorpseCreditAwards, "processPendingCorpseCreditAwards");
		Require(processDeadNpcDespawn, "processDeadNpcDespawn");
		Require(processRegeneration, "processRegeneration");
		Require(processCombatTick, "processCombatTick");
		Require(processNpcPatrolTick, "processNpcPatrolTick");
		Require(processFollow, "processFollow");
		Require(processPlayerCollision, "processPlayerCollision");
		processPendingCorpseSpawns();
		processCorpseDespawns();
		processPendingCorpseCreditAwards();
		IEnumerable<ICharacter> enumerable = (from xx in characters()
			where ((IDynel)xx).InPlayfield(playfieldIdentity) && (!((IInstancedEntity)xx).DoNotDoTimers || hasPendingDeadNpcDespawn(((IEntity)xx).Identity))
			select xx).ToList();
		foreach (ICharacter item in enumerable)
		{
			if (item != null && !((IInstancedEntity)item).Starting && !processDeadNpcDespawn(item) && !((IInstancedEntity)item).DoNotDoTimers)
			{
				processCombatTick(item);
				processRegeneration(item);
				if (((IDynel)item).Controller is NPCController)
				{
					processNpcPatrolTick(item);
				}
				else
				{
					processFollow(item);
				}
				if (((IDynel)item).Controller is PlayerController)
				{
					processPlayerCollision(item);
				}
			}
		}
	}

	private static void Require(Delegate callback, string name)
	{
		if ((object)callback == null)
		{
			throw new ArgumentNullException(name);
		}
	}
}
