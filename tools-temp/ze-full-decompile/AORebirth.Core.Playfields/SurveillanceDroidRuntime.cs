using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;

namespace AORebirth.Core.Playfields;

internal static class SurveillanceDroidRuntime
{
	internal const string NpcName = "Surveillance Droid";

	internal const uint CapturedScfuFlags = 170543699u;

	internal const int CaptureInstance = 2028010634;

	private const int AreteLandingPlayfieldId = 6553;

	private const int MonsterDataId = 210238;

	private const float SpawnX = 3567.518f;

	private const float SpawnY = 5.1100006f;

	private const float SpawnZ = 820.3735f;

	private const float Hy = 0.5793964f;

	private const float Hw = 0.8150459f;

	internal static readonly byte[] CapturedUnknown1 = new byte[28]
	{
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 3, 1, 0, 1, 0, 1, 0, 1,
		0, 1, 0, 0, 0, 3, 0, 0
	};

	private static readonly byte[] ExtendedTextureOverrideData = new byte[137]
	{
		20, 0, 0, 15, 196, 99, 97, 109, 101, 114,
		97, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 3, 53,
		27, 0, 0, 0, 0, 0, 0, 0, 0, 99,
		97, 109, 101, 114, 97, 32, 103, 108, 111, 119,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 3, 168, 166, 0, 0, 0, 0, 0,
		0, 0, 0, 99, 97, 109, 101, 114, 97, 32,
		108, 101, 110, 115, 101, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 3, 168, 168, 0,
		0, 0, 0, 0, 0, 0, 0
	};

	private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

	internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
	{
		if (string.Equals(name, "Surveillance Droid", StringComparison.Ordinal))
		{
			data = (byte[])ExtendedTextureOverrideData.Clone();
			return true;
		}
		data = null;
		return false;
	}

	public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		if (playfield != null && activateNpc != null && ((Identity)(ref playfieldIdentity)).Instance == 6553)
		{
			LinkedPlayfields.Add(((Identity)(ref playfieldIdentity)).Instance);
			Character val = SpawnDroid(playfield, playfieldIdentity, activateNpc);
			if (val != null)
			{
				string[] obj = new string[7]
				{
					"SurveillanceDroidRuntime SPAWNED pf=",
					((Identity)(ref playfieldIdentity)).Instance.ToString(),
					" id=",
					null,
					null,
					null,
					null
				};
				Identity identity = ((PooledObject)val).Identity;
				obj[3] = ((object)(Identity)(ref identity)).ToString();
				obj[4] = " monsterdata=";
				obj[5] = 210238.ToString();
				obj[6] = " template=A004+wire";
				LogUtil.Debug((DebugInfoDetail)512, string.Concat(obj));
			}
			else
			{
				LogUtil.Debug((DebugInfoDetail)512, "SurveillanceDroidRuntime START produced no mob (A004 / already present)");
			}
		}
	}

	public static void ClearPlayfield(int playfieldInstance)
	{
		LinkedPlayfields.Remove(playfieldInstance);
	}

	public static void TickEnsurePresent(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 6553)
		{
			return;
		}
		LinkedPlayfields.Add(((Identity)(ref playfieldIdentity)).Instance);
		try
		{
			ICharacter val = FindLivingDroid(playfield);
			if (val != null)
			{
				if (IsCaptureCorrectDroid(val))
				{
					return;
				}
				string[] obj = new string[6] { "SurveillanceDroidRuntime replacing invalid droid id=", null, null, null, null, null };
				Identity identity = ((IEntity)val).Identity;
				obj[1] = ((object)(Identity)(ref identity)).ToString();
				obj[2] = " monsterdata=";
				obj[3] = ((IStats)val).Stats[(StatIds)359].Value.ToString();
				obj[4] = " breed=";
				obj[5] = ((IStats)val).Stats[(StatIds)4].Value.ToString();
				LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
				playfield.DespawnNpcImmediately(val);
			}
			if (SpawnDroid(playfield, playfieldIdentity, activateNpc) != null)
			{
				LogUtil.Debug((DebugInfoDetail)128, "SurveillanceDroidRuntime respawned pf=" + ((Identity)(ref playfieldIdentity)).Instance);
			}
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "SurveillanceDroidRuntime ensure exception " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	internal static ICharacter FindLivingDroid(Playfield playfield)
	{
		if (playfield == null)
		{
			return null;
		}
		foreach (ICharacter item in playfield.EnumerateActiveCharacters())
		{
			if (item == null || ((IStats)item).Stats[(StatIds)27].Value <= 0 || (!string.Equals(((INamedEntity)item).Name, "Surveillance Droid", StringComparison.OrdinalIgnoreCase) && ((IStats)item).Stats[(StatIds)359].Value != 210238))
			{
				continue;
			}
			return item;
		}
		return null;
	}

	private static bool IsCaptureCorrectDroid(ICharacter npc)
	{
		return npc != null && ((IStats)npc).Stats[(StatIds)359].Value == 210238 && ((IStats)npc).Stats[(StatIds)4].Value == 6;
	}

	private static Character SpawnDroid(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected O, but got Unknown
		//IL_00a3: Expected O, but got Unknown
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Expected O, but got Unknown
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c4: Expected O, but got Unknown
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0312: Unknown result type (might be due to invalid IL or missing references)
		//IL_0317: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = FindLivingDroid(playfield);
		if (val != null && IsCaptureCorrectDroid(val))
		{
			return null;
		}
		if (val != null)
		{
			playfield.DespawnNpcImmediately(val);
		}
		NPCController nPCController = new NPCController
		{
			AiProfile = NpcAiProfile.Social
		};
		Character val2;
		try
		{
			val2 = NonPlayerCharacterHandler.SpawnMobFromTemplate("A004", playfieldIdentity, new Coordinate
			{
				x = 3567.518f,
				y = 5.1100006f,
				z = 820.3735f
			}, new Quaternion(0.0, 0.5793964266777039, 0.0, 0.8150458931922913), (IController)(object)nPCController, 6);
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "SurveillanceDroidRuntime SpawnMobFromTemplate threw " + ex.GetType().Name + ": " + ex.Message);
			return null;
		}
		if (val2 == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "SurveillanceDroidRuntime spawn FAILED template=A004 source=20260720-151642");
			return null;
		}
		CombatTestMobArchetype.Prepare((ICharacter)(object)val2, CombatTestMobArchetype.DuneFlea);
		((Dynel)val2).Name = "Surveillance Droid";
		val2.FirstName = string.Empty;
		val2.LastName = string.Empty;
		((Dynel)val2).Playfield = (IPlayfield)(object)playfield;
		((Dynel)val2).MeshLayer.Clear();
		val2.SocialMeshLayer.Clear();
		((Dynel)val2).Textures.Clear();
		for (int i = 0; i < 5; i++)
		{
			((Dynel)val2).Textures.Add(new AOTextures(i, 0));
		}
		SetStat(val2, (StatIds)359, 210238);
		SetStat(val2, (StatIds)1, 69);
		SetStat(val2, (StatIds)27, 69);
		SetStat(val2, (StatIds)54, 6);
		SetStat(val2, (StatIds)673, 31);
		SetStat(val2, (StatIds)455, 137);
		SetStat(val2, (StatIds)466, 0);
		SetStat(val2, (StatIds)0, 268964353);
		SetStat(val2, (StatIds)33, 0);
		SetStat(val2, (StatIds)4, 6);
		SetStat(val2, (StatIds)59, 1);
		SetStat(val2, (StatIds)89, 1);
		SetStat(val2, (StatIds)47, 1);
		SetStat(val2, (StatIds)64, 0);
		SetStat(val2, (StatIds)360, 110);
		SetStat(val2, (StatIds)156, 20);
		SetStat(val2, (StatIds)173, 3);
		SetStat(val2, (StatIds)174, 3);
		SetStat(val2, (StatIds)660, 0);
		SetStat(val2, (StatIds)389, 0);
		SetStat(val2, (StatIds)60, 0);
		SetStat(val2, (StatIds)368, 0);
		((Dynel)val2).Coordinates(new Coordinate
		{
			x = 3567.518f,
			y = 5.1100006f,
			z = 820.3735f
		});
		((Dynel)val2).DoNotDoTimers = false;
		activateNpc((ICharacter)(object)val2);
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val2, Identity.None);
		string[] obj = new string[10]
		{
			"SurveillanceDroidRuntime SPAWNED name=Surveillance Droid monsterdata=",
			210238.ToString(),
			" id=",
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		Identity identity = ((PooledObject)val2).Identity;
		obj[3] = ((object)(Identity)(ref identity)).ToString();
		obj[4] = " at=";
		obj[5] = 3567.518f.ToString();
		obj[6] = ",";
		obj[7] = 5.1100006f.ToString();
		obj[8] = ",";
		obj[9] = 820.3735f.ToString();
		LogUtil.Debug((DebugInfoDetail)512, string.Concat(obj));
		return val2;
	}

	private static void SetStat(Character mob, StatIds stat, int value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Expected I4, but got Unknown
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		((Dynel)mob).Stats.SetBaseValueWithoutTriggering((int)stat, (uint)value);
		((Dynel)mob).Stats[stat].Value = value;
	}
}
