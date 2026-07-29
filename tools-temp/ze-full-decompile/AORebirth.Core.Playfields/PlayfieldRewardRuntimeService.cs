using System;
using AORebirth.Core.Entities;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.Thrak.Quests;

namespace AORebirth.Core.Playfields;

internal sealed class PlayfieldRewardRuntimeService
{
	internal void RunNpcDeathRewardHooks(ICharacter attacker, ICharacter target, Action<ICharacter, ICharacter> awardCombatXp)
	{
		if (attacker != null)
		{
			awardCombatXp?.Invoke(attacker, target);
			RexB18CObjectiveProgressTracker.TryObserveNpcDeath(attacker, target);
			FlintBioComQuestRuntime.TryObserveNpcDeath(attacker, target);
			KneecappingQuestRuntime.TryObserveNpcDeath(attacker, target);
			ThrakGardenKeySilvertailTransform.TryObserveCursedDeath(attacker, target);
		}
	}
}
