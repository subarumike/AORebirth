using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;

namespace AORebirth.Core.Playfields;

internal static class MarcusPadAmbientCombat
{
	private const int AreteLandingPlayfieldId = 6553;

	private const string MarcusName = "Marcus Stone";

	private const string BurningRobotName = "Burning Cleaning Robot";

	private const float RobotX = 3636.5132f;

	private const float RobotY = 40.984997f;

	private const float RobotZ = 832.7695f;

	private const int RobotHealth = 58;

	private const int RobotLevel = 5;

	private const int RobotCharacterFlags = 269226497;

	private const int RobotScale = 200;

	private const double FlamethrowerRange = 15.0;

	private const double FlamethrowerRechargeSeconds = 6.0;

	private const int FlamethrowerMinDamage = 7;

	private const int FlamethrowerMaxDamage = 17;

	private const int RobotMinDamage = 1;

	private const int RobotMaxDamage = 3;

	private const double RobotRechargeSeconds = 4.0;

	private const double RobotRespawnSeconds = 20.0;

	private const double FlamethrowerAnimRefreshSeconds = 6.0;

	private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

	private static readonly Dictionary<int, DateTime> NextRobotRespawnUtc = new Dictionary<int, DateTime>();

	private static readonly Dictionary<int, DateTime> NextFlamethrowerAnimUtc = new Dictionary<int, DateTime>();

	public static void ClearPlayfield(int playfieldInstance)
	{
		LinkedPlayfields.Remove(playfieldInstance);
		NextRobotRespawnUtc.Remove(playfieldInstance);
		NextFlamethrowerAnimUtc.Remove(playfieldInstance);
	}

	public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 6553 || !LinkedPlayfields.Add(((Identity)(ref playfieldIdentity)).Instance))
		{
			return;
		}
		Character val = FindNamedNpc(playfield, "Marcus Stone");
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "MarcusPadAmbientCombat: Marcus Stone not found pf=" + ((Identity)(ref playfieldIdentity)).Instance);
			return;
		}
		Character val2 = SpawnBurningRobot(playfield, playfieldIdentity, activateNpc);
		if (val2 != null)
		{
			LinkFight(playfield, val, val2);
			string[] obj = new string[5] { "MarcusPadAmbientCombat linked Marcus=", null, null, null, null };
			Identity identity = ((PooledObject)val).Identity;
			obj[1] = ((Identity)(ref identity)).ToString(true);
			obj[2] = " robot=";
			identity = ((PooledObject)val2).Identity;
			obj[3] = ((Identity)(ref identity)).ToString(true);
			obj[4] = " source=20260720-064523";
			LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
		}
	}

	public static void TickRespawn(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Expected O, but got Unknown
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 6553 || !LinkedPlayfields.Contains(((Identity)(ref playfieldIdentity)).Instance))
		{
			return;
		}
		Character val = FindNamedNpc(playfield, "Burning Cleaning Robot");
		Character val2;
		Identity val3;
		if (val != null && ((Dynel)val).Stats[(StatIds)27].Value > 0)
		{
			NextRobotRespawnUtc.Remove(((Identity)(ref playfieldIdentity)).Instance);
			val2 = FindNamedNpc(playfield, "Marcus Stone");
			if (val2 != null && ((Dynel)val2).Stats[(StatIds)27].Value > 0)
			{
				val3 = val2.FightingTarget;
				if (((Identity)(ref val3)).Instance != 0)
				{
					val3 = val2.FightingTarget;
					int instance = ((Identity)(ref val3)).Instance;
					val3 = ((PooledObject)val).Identity;
					if (instance == ((Identity)(ref val3)).Instance)
					{
						goto IL_00f5;
					}
				}
				LinkFight(playfield, val2, val);
				return;
			}
			goto IL_00f5;
		}
		if (!NextRobotRespawnUtc.TryGetValue(((Identity)(ref playfieldIdentity)).Instance, out var value))
		{
			NextRobotRespawnUtc[((Identity)(ref playfieldIdentity)).Instance] = DateTime.UtcNow + TimeSpan.FromSeconds(20.0);
		}
		else
		{
			if (value > DateTime.UtcNow)
			{
				return;
			}
			Character val4 = FindNamedNpc(playfield, "Marcus Stone");
			if (val4 != null)
			{
				Character val5 = SpawnBurningRobot(playfield, playfieldIdentity, activateNpc);
				if (val5 != null)
				{
					LinkFight(playfield, val4, val5);
					NextRobotRespawnUtc.Remove(((Identity)(ref playfieldIdentity)).Instance);
					val3 = ((PooledObject)val5).Identity;
					LogUtil.Debug((DebugInfoDetail)128, "MarcusPadAmbientCombat respawned robot=" + ((Identity)(ref val3)).ToString(true) + " source=20260720-064523");
				}
			}
		}
		return;
		IL_00f5:
		if (val2 != null && (!NextFlamethrowerAnimUtc.TryGetValue(((Identity)(ref playfieldIdentity)).Instance, out var value2) || value2 <= DateTime.UtcNow))
		{
			playfield.Announce((MessageBody)new AttackInfoMessage
			{
				Identity = ((PooledObject)val2).Identity,
				Unknown = 0,
				Target = ((PooledObject)val).Identity,
				Unknown1 = 7,
				Unknown2 = 0,
				Unknown3 = 6,
				Unknown4 = 0,
				Unknown5 = 3,
				Unknown6 = 0
			});
			NextFlamethrowerAnimUtc[((Identity)(ref playfieldIdentity)).Instance] = DateTime.UtcNow + TimeSpan.FromSeconds(6.0);
		}
	}

	private static void LinkFight(Playfield playfield, Character marcus, Character robot)
	{
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Expected O, but got Unknown
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Expected O, but got Unknown
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Expected O, but got Unknown
		CapturedEnemyCombatRuntime.Prepare(marcus, ((Dynel)marcus).Controller as NPCController, CreateMarcusFlamethrowerContract(), out var failure);
		if (!string.IsNullOrEmpty(failure))
		{
			LogUtil.Debug((DebugInfoDetail)512, "MarcusPadAmbientCombat Marcus combat prepare: " + failure);
		}
		CapturedEnemyCombatRuntime.Prepare(robot, ((Dynel)robot).Controller as NPCController, CreateBurningRobotContract(), out failure);
		if (!string.IsNullOrEmpty(failure))
		{
			LogUtil.Debug((DebugInfoDetail)512, "MarcusPadAmbientCombat robot combat prepare: " + failure);
		}
		marcus.SetFightingTarget(((PooledObject)robot).Identity);
		robot.SetFightingTarget(((PooledObject)marcus).Identity);
		playfield.ResetCombatTick(((PooledObject)marcus).Identity);
		playfield.ResetCombatTick(((PooledObject)robot).Identity);
		playfield.Announce((MessageBody)new AttackMessage
		{
			Identity = ((PooledObject)marcus).Identity,
			Unknown = 0,
			Target = ((PooledObject)robot).Identity,
			Action = 0
		});
		playfield.Announce((MessageBody)new AttackInfoMessage
		{
			Identity = ((PooledObject)marcus).Identity,
			Unknown = 0,
			Target = ((PooledObject)robot).Identity,
			Unknown1 = 7,
			Unknown2 = 0,
			Unknown3 = 6,
			Unknown4 = 0,
			Unknown5 = 3,
			Unknown6 = 0
		});
		playfield.Announce((MessageBody)new AttackMessage
		{
			Identity = ((PooledObject)robot).Identity,
			Unknown = 0,
			Target = ((PooledObject)marcus).Identity,
			Action = 0
		});
	}

	private static CapturedEnemyCombatContract CreateMarcusFlamethrowerContract()
	{
		CapturedEnemyCombatAttackDefinition repeatingAttack = new CapturedEnemyCombatAttackDefinition(7, 17, 0, 15.0, 6.0, usesEquippedWeapon: false, 0, 6, 0, 3, 0, sendAttackInfo: true);
		return CapturedEnemyCombatContract.CapturedSpecialSequence("20260720-064523 Marcus flamethrower AttackInfo WeaponSlot=6 vs Burning Cleaning Robot", new CapturedEnemySpecialAttackSequenceDefinition(0.5, null, repeatingAttack, new CapturedEnemySpecialAttackDefinition[0], 0, 0, 0, 0, 0));
	}

	private static CapturedEnemyCombatContract CreateBurningRobotContract()
	{
		CapturedEnemyCombatAttackDefinition repeatingAttack = new CapturedEnemyCombatAttackDefinition(1, 3, 0, 15.0, 4.0, usesEquippedWeapon: false, -1, 0, 0, 3, 0, sendAttackInfo: false);
		return CapturedEnemyCombatContract.CapturedSpecialSequence("20260720-064523 Burning Cleaning Robot SpecialAttackWeapon 43/43/43/3/0 + Attack Marcus", new CapturedEnemySpecialAttackSequenceDefinition(0.2, null, repeatingAttack, new CapturedEnemySpecialAttackDefinition[1]
		{
			new CapturedEnemySpecialAttackDefinition(43, 43, 43, string.Empty)
		}, 43, 43, 43, 3, 0));
	}

	private static Character SpawnBurningRobot(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected O, but got Unknown
		//IL_006e: Expected O, but got Unknown
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Expected O, but got Unknown
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		NPCController nPCController = new NPCController
		{
			AiProfile = NpcAiProfile.Passive
		};
		Character val = NonPlayerCharacterHandler.SpawnMobFromTemplate("A004", playfieldIdentity, new Coordinate
		{
			x = 3636.5132f,
			y = 40.984997f,
			z = 832.7695f
		}, new Quaternion(0.0, 0.9414477, 0.0, 0.3371589), (IController)(object)nPCController, 5);
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "MarcusPadAmbientCombat: Burning Cleaning Robot spawn failed");
			return null;
		}
		((Dynel)val).Name = "Burning Cleaning Robot";
		((Dynel)val).Playfield = (IPlayfield)(object)playfield;
		CombatTestMobArchetype.Prepare((ICharacter)(object)val, CombatTestMobArchetype.MalfunctioningCleaningRobot);
		((Dynel)val).Name = "Burning Cleaning Robot";
		((Dynel)val).Stats[(StatIds)1].Value = 58;
		((Dynel)val).Stats[(StatIds)1].BaseValue = 58u;
		((Dynel)val).Stats[(StatIds)27].Value = 58;
		((Dynel)val).Stats[(StatIds)27].BaseValue = 58u;
		((Dynel)val).Stats[(StatIds)54].Value = 5;
		((Dynel)val).Stats[(StatIds)54].BaseValue = 5u;
		((Dynel)val).Stats[(StatIds)360].Value = 200;
		((Dynel)val).Stats[(StatIds)360].BaseValue = 200u;
		((Dynel)val).Stats[(StatIds)0].Value = 269226497;
		((Dynel)val).Stats[(StatIds)0].BaseValue = 269226497u;
		((Dynel)val).Stats[(StatIds)673].Value = 31;
		((Dynel)val).Stats[(StatIds)673].BaseValue = 31u;
		((Dynel)val).Coordinates(new Coordinate
		{
			x = 3636.5132f,
			y = 40.984997f,
			z = 832.7695f
		});
		((Dynel)val).DoNotDoTimers = false;
		activateNpc((ICharacter)(object)val);
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
		return val;
	}

	private static Character FindNamedNpc(Playfield playfield, string name)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || string.IsNullOrEmpty(name))
		{
			return null;
		}
		foreach (ICharacter item in Pool.Instance.GetAll<ICharacter>(((PooledObject)playfield).Identity))
		{
			if (item == null || ((IDynel)item).Controller == null || ((IDynel)item).Controller is PlayerController || !string.Equals(((INamedEntity)item).Name, name, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			return (Character)(object)((item is Character) ? item : null);
		}
		return null;
	}
}
