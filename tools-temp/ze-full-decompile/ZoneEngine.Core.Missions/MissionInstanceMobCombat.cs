using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core.Missions;

internal static class MissionInstanceMobCombat
{
	private const float AggroRadius = 2f;

	private static readonly object Gate = new object();

	private static readonly HashSet<int> AggressiveMobs = new HashSet<int>();

	private static readonly HashSet<int> FindItemHosts = new HashSet<int>();

	public static void RegisterAggressive(Identity identity)
	{
		if (((Identity)(ref identity)).Instance == 0)
		{
			return;
		}
		lock (Gate)
		{
			AggressiveMobs.Add(((Identity)(ref identity)).Instance);
		}
	}

	public static void RegisterFindItemHost(Identity identity)
	{
		if (((Identity)(ref identity)).Instance == 0)
		{
			return;
		}
		lock (Gate)
		{
			FindItemHosts.Add(((Identity)(ref identity)).Instance);
		}
	}

	public static bool IsFindItemHost(Identity identity)
	{
		lock (Gate)
		{
			return FindItemHosts.Contains(((Identity)(ref identity)).Instance);
		}
	}

	public static void ClearPlayfield(int playfieldInstance)
	{
	}

	public static bool TryPrepareCombat(Character mob, NPCController controller, int level)
	{
		if (mob == null || controller == null)
		{
			return false;
		}
		int minDamage = 40 + level / 2;
		int maxDamage = 80 + level;
		CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight("mission-instance-auto-aggro", minDamage, maxDamage, 2.0, 0, 0, 1279874865);
		string failure;
		return CapturedEnemyCombatRuntime.Prepare(mob, controller, contract, out failure);
	}

	public static ICharacter FindAutomaticAggroTarget(ICharacter npc)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		if (npc == null || ((IInstancedEntity)npc).Playfield == null)
		{
			return null;
		}
		Identity val;
		lock (Gate)
		{
			HashSet<int> aggressiveMobs = AggressiveMobs;
			val = ((IEntity)npc).Identity;
			if (!aggressiveMobs.Contains(((Identity)(ref val)).Instance))
			{
				return null;
			}
		}
		val = ((ITargetingEntity)npc).FightingTarget;
		if (((Identity)(ref val)).Instance != 0 || ((IStats)npc).Stats[(StatIds)27].Value <= 0)
		{
			return null;
		}
		if (!(((IInstancedEntity)npc).Playfield is Playfield playfield))
		{
			return null;
		}
		Coordinate val2 = ((IDynel)npc).Coordinates();
		ICharacter result = null;
		double num = 2.0;
		List<ICharacter> list = playfield.FindCharacterInRange((IDynel)(object)npc, 2f);
		for (int i = 0; i < list.Count; i++)
		{
			ICharacter val3 = list[i];
			if (val3 == null)
			{
				continue;
			}
			val = ((IEntity)val3).Identity;
			int instance = ((Identity)(ref val)).Instance;
			val = ((IEntity)npc).Identity;
			if (instance != ((Identity)(ref val)).Instance && ((IDynel)val3).Controller is PlayerController && ((IStats)val3).Stats[(StatIds)27].Value > 0)
			{
				double num2 = ((IDynel)val3).Coordinates().coordinate.Distance2D(val2.coordinate);
				if (num2 < num)
				{
					num = num2;
					result = val3;
				}
			}
		}
		return result;
	}
}
