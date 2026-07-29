using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Missions;

namespace AORebirth.Core.Playfields;

internal static class AlexAreaMobRuntime
{
	private enum MobKind
	{
		Docker,
		WasteCollector,
		GarbageFlea,
		CleaningRobot
	}

	private sealed class MobSlot
	{
		public string Name { get; private set; }

		public MobKind Kind { get; private set; }

		public int MonsterData { get; private set; }

		public int Level { get; private set; }

		public int Health { get; private set; }

		public int NpcFamily { get; private set; }

		public int Scale { get; private set; }

		public int RunSpeed { get; private set; }

		public NpcAiProfile AiProfile { get; private set; }

		public float AggroRadiusMeters { get; private set; }

		public float X { get; private set; }

		public float Y { get; private set; }

		public float Z { get; private set; }

		public MobSlot(string name, MobKind kind, int monsterData, int level, int health, int npcFamily, int scale, int runSpeed, NpcAiProfile aiProfile, float aggroRadiusMeters, float x, float y, float z)
		{
			Name = name;
			Kind = kind;
			MonsterData = monsterData;
			Level = level;
			Health = health;
			NpcFamily = npcFamily;
			Scale = scale;
			RunSpeed = runSpeed;
			AiProfile = aiProfile;
			AggroRadiusMeters = aggroRadiusMeters;
			X = x;
			Y = y;
			Z = z;
		}
	}

	private const int AreteLandingPlayfieldId = 6553;

	private const double RespawnSeconds = 41.0;

	private const float FleaAggroRadiusMeters = 2f;

	private const float DefaultAggroRadiusMeters = 6f;

	private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

	private static readonly Dictionary<int, DateTime[]> NextRespawnUtcBySlot = new Dictionary<int, DateTime[]>();

	private static readonly object AggroGate = new object();

	private static readonly Dictionary<int, float> AggroRadiusByNpcInstance = new Dictionary<int, float>();

	private static readonly byte[] GarbageFleaExtendedTextureOverrideData = new byte[48]
	{
		0, 0, 7, 226, 77, 97, 116, 101, 114, 105,
		97, 108, 32, 35, 57, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 1, 118, 139,
		0, 0, 0, 0, 0, 0, 0, 1
	};

	private static readonly byte[] WasteCollectorExtendedTextureOverrideData = new byte[48]
	{
		0, 0, 7, 226, 77, 97, 116, 101, 114, 105,
		97, 108, 32, 35, 50, 50, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 67, 166,
		0, 0, 0, 0, 0, 0, 0, 1
	};

	private static readonly byte[] DockerExtendedTextureOverrideData = new byte[92]
	{
		0, 0, 11, 211, 77, 97, 116, 101, 114, 105,
		97, 108, 32, 35, 49, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 124, 81,
		0, 0, 0, 0, 0, 0, 0, 0, 77, 97,
		116, 101, 114, 105, 97, 108, 32, 35, 51, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 124, 80, 0, 0, 0, 0, 0, 0,
		0, 0
	};

	private static readonly MobSlot[] Slots = new MobSlot[16]
	{
		new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Aggressive, 6f, 3520.2432f, 5.315f, 872.97473f),
		new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Aggressive, 6f, 3502.3074f, 5.1100006f, 857.66364f),
		new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Aggressive, 6f, 3521.3774f, 5.1100006f, 876.4073f),
		new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Aggressive, 6f, 3522.3467f, 5.1100006f, 874.9996f),
		new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Aggressive, 6f, 3523.994f, 5.1100006f, 880.1992f),
		new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Aggressive, 6f, 3495.174f, 5.1100006f, 879.16656f),
		new MobSlot("32-V Docker", MobKind.Docker, 17649, 3, 35, 1019, 110, 11, NpcAiProfile.Aggressive, 6f, 3492.6052f, 5.1100006f, 878.40924f),
		new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 6f, 3513.9517f, 5.1100006f, 865.63983f),
		new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 6f, 3514.402f, 5.1100006f, 866.71875f),
		new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 6f, 3510.8594f, 5.1100006f, 864.0514f),
		new MobSlot("Waste Collector", MobKind.WasteCollector, 17714, 2, 29, 1019, 75, 12, NpcAiProfile.Aggressive, 6f, 3492.674f, 5.1100006f, 866.95435f),
		new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 2f, 3529.97f, 5.1100006f, 894.44257f),
		new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 2f, 3499.842f, 5.1100006f, 898.7892f),
		new MobSlot("Garbage Flea", MobKind.GarbageFlea, 17657, 2, 24, 25, 125, 8, NpcAiProfile.Aggressive, 2f, 3559.97f, 5.1100006f, 865.22f),
		new MobSlot("IIV-X Advanced Docker", MobKind.Docker, 17649, 4, 323, 1019, 110, 15, NpcAiProfile.Aggressive, 6f, 3515.6375f, 5.3050003f, 905.0099f),
		new MobSlot("Cleanmeister Intelligence Robot", MobKind.CleaningRobot, 297023, 2, 180, 1019, 100, 13, NpcAiProfile.Aggressive, 6f, 3541.288f, 5.2202663f, 877.33673f)
	};

	internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
	{
		if (string.Equals(name, "Garbage Flea", StringComparison.Ordinal))
		{
			data = (byte[])GarbageFleaExtendedTextureOverrideData.Clone();
			return true;
		}
		if (string.Equals(name, "Waste Collector", StringComparison.Ordinal))
		{
			data = (byte[])WasteCollectorExtendedTextureOverrideData.Clone();
			return true;
		}
		if (string.Equals(name, "32-V Docker", StringComparison.Ordinal) || string.Equals(name, "IIV-X Advanced Docker", StringComparison.Ordinal))
		{
			data = (byte[])DockerExtendedTextureOverrideData.Clone();
			return true;
		}
		data = null;
		return false;
	}

	public static ICharacter FindAutomaticAggroTarget(ICharacter npc)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		if (npc == null || ((IInstancedEntity)npc).Playfield == null || ((IStats)npc).Stats[(StatIds)27].Value <= 0)
		{
			return null;
		}
		Identity val = ((ITargetingEntity)npc).FightingTarget;
		if (((Identity)(ref val)).Instance != 0)
		{
			return null;
		}
		float value;
		lock (AggroGate)
		{
			Dictionary<int, float> aggroRadiusByNpcInstance = AggroRadiusByNpcInstance;
			val = ((IEntity)npc).Identity;
			if (!aggroRadiusByNpcInstance.TryGetValue(((Identity)(ref val)).Instance, out value) || value <= 0f)
			{
				return null;
			}
		}
		if (!(((IInstancedEntity)npc).Playfield is Playfield playfield))
		{
			return null;
		}
		Coordinate val2 = ((IDynel)npc).Coordinates();
		ICharacter result = null;
		double num = value;
		List<ICharacter> list = playfield.FindCharacterInRange((IDynel)(object)npc, value);
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

	public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 6553 || !LinkedPlayfields.Add(((Identity)(ref playfieldIdentity)).Instance))
		{
			return;
		}
		NextRespawnUtcBySlot[((Identity)(ref playfieldIdentity)).Instance] = new DateTime[Slots.Length];
		int num = 0;
		for (int i = 0; i < Slots.Length; i++)
		{
			if (SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
			{
				num++;
			}
		}
		LogUtil.Debug((DebugInfoDetail)128, "AlexAreaMobRuntime spawned=" + num + "/" + Slots.Length + " pf=" + ((Identity)(ref playfieldIdentity)).Instance + " source=20260720-080123");
		if (num == 0)
		{
			LinkedPlayfields.Remove(((Identity)(ref playfieldIdentity)).Instance);
			NextRespawnUtcBySlot.Remove(((Identity)(ref playfieldIdentity)).Instance);
		}
	}

	public static void ClearPlayfield(int playfieldInstance)
	{
		LinkedPlayfields.Remove(playfieldInstance);
		NextRespawnUtcBySlot.Remove(playfieldInstance);
	}

	public static void TickRespawn(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 6553)
		{
			return;
		}
		LinkedPlayfields.Add(((Identity)(ref playfieldIdentity)).Instance);
		if (!NextRespawnUtcBySlot.TryGetValue(((Identity)(ref playfieldIdentity)).Instance, out var value) || value == null || value.Length != Slots.Length)
		{
			value = new DateTime[Slots.Length];
			NextRespawnUtcBySlot[((Identity)(ref playfieldIdentity)).Instance] = value;
		}
		for (int i = 0; i < Slots.Length; i++)
		{
			if (HasLivingMobNear(playfield, Slots[i]))
			{
				value[i] = DateTime.MaxValue;
			}
			else if (value[i] == DateTime.MaxValue)
			{
				value[i] = DateTime.UtcNow + TimeSpan.FromSeconds(41.0);
			}
			else if (!(value[i] > DateTime.UtcNow) && SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
			{
				value[i] = DateTime.MaxValue;
			}
		}
	}

	private static Character SpawnSlot(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc, int slotIndex)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Expected O, but got Unknown
		//IL_009a: Expected O, but got Unknown
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Expected O, but got Unknown
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		MobSlot mobSlot = Slots[slotIndex];
		NPCController nPCController = new NPCController
		{
			AiProfile = mobSlot.AiProfile
		};
		string text = ((mobSlot.Kind == MobKind.GarbageFlea) ? CombatTestMobArchetype.DuneFlea.TemplateHash : "A004");
		Character val = NonPlayerCharacterHandler.SpawnMobFromTemplate(text, playfieldIdentity, new Coordinate
		{
			x = mobSlot.X,
			y = mobSlot.Y,
			z = mobSlot.Z
		}, new Quaternion(0.0, 0.0, 0.0, 1.0), (IController)(object)nPCController, mobSlot.Level);
		if (val == null)
		{
			return null;
		}
		((Dynel)val).Name = mobSlot.Name;
		((Dynel)val).Playfield = (IPlayfield)(object)playfield;
		if (mobSlot.Kind == MobKind.GarbageFlea)
		{
			CombatTestMobArchetype.Prepare((ICharacter)(object)val, CombatTestMobArchetype.DuneFlea);
		}
		else if (mobSlot.Kind == MobKind.CleaningRobot)
		{
			CombatTestMobArchetype.Prepare((ICharacter)(object)val, CombatTestMobArchetype.MalfunctioningCleaningRobot);
		}
		else
		{
			CombatTestMobArchetype.Prepare((ICharacter)(object)val, CombatTestMobArchetype.DuneFlea);
		}
		((Dynel)val).Name = mobSlot.Name;
		ApplyCaptureStats(val, mobSlot);
		nPCController.AiProfile = mobSlot.AiProfile;
		CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight("alex-area-20260720-080123", Math.Max(1, mobSlot.Level), Math.Max(3, mobSlot.Level + 2), 2.0, 0, 0, 1279874865);
		CapturedEnemyCombatRuntime.Prepare(val, nPCController, contract, out var _);
		((Dynel)val).Coordinates(new Coordinate
		{
			x = mobSlot.X,
			y = mobSlot.Y,
			z = mobSlot.Z
		});
		((Dynel)val).DoNotDoTimers = false;
		activateNpc((ICharacter)(object)val);
		Identity identity = ((PooledObject)val).Identity;
		RegisterAggro(((Identity)(ref identity)).Instance, mobSlot.AggroRadiusMeters);
		MissionInstanceMobCombat.RegisterAggressive(((PooledObject)val).Identity);
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
		return val;
	}

	private static void ApplyCaptureStats(Character mob, MobSlot slot)
	{
		SetStat((ICharacter)(object)mob, (StatIds)359, slot.MonsterData);
		SetStat((ICharacter)(object)mob, (StatIds)1, slot.Health);
		SetStat((ICharacter)(object)mob, (StatIds)27, slot.Health);
		SetStat((ICharacter)(object)mob, (StatIds)54, slot.Level);
		SetStat((ICharacter)(object)mob, (StatIds)455, slot.NpcFamily);
		SetStat((ICharacter)(object)mob, (StatIds)360, slot.Scale);
		SetStat((ICharacter)(object)mob, (StatIds)156, slot.RunSpeed);
		SetStat((ICharacter)(object)mob, (StatIds)0, 268964353);
		SetStat((ICharacter)(object)mob, (StatIds)673, 31);
		SetStat((ICharacter)(object)mob, (StatIds)33, 3);
		SetStat((ICharacter)(object)mob, (StatIds)4, 6);
		SetStat((ICharacter)(object)mob, (StatIds)59, 1);
		SetStat((ICharacter)(object)mob, (StatIds)89, 1);
		SetStat((ICharacter)(object)mob, (StatIds)47, 1);
		if (slot.Kind == MobKind.GarbageFlea)
		{
			SetStat((ICharacter)(object)mob, (StatIds)42, 15231);
			SetStat((ICharacter)(object)mob, (StatIds)404, 15231);
		}
		else if (slot.Kind != MobKind.CleaningRobot)
		{
			SetStat((ICharacter)(object)mob, (StatIds)42, 0);
			SetStat((ICharacter)(object)mob, (StatIds)404, 0);
		}
		if (((Dynel)mob).Textures != null)
		{
			((Dynel)mob).Textures.Clear();
		}
		if (((Dynel)mob).MeshLayer != null)
		{
			((Dynel)mob).MeshLayer.Clear();
		}
		if (mob.SocialMeshLayer != null)
		{
			mob.SocialMeshLayer.Clear();
		}
	}

	private static void SetStat(ICharacter mob, StatIds stat, int value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		((IStats)mob).Stats[stat].Value = value;
		((IStats)mob).Stats[stat].BaseValue = (uint)value;
	}

	private static void RegisterAggro(int npcInstance, float radiusMeters)
	{
		if (npcInstance == 0)
		{
			return;
		}
		lock (AggroGate)
		{
			AggroRadiusByNpcInstance[npcInstance] = radiusMeters;
		}
	}

	private static bool HasLivingMobNear(Playfield playfield, MobSlot slot)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		foreach (ICharacter item in Pool.Instance.GetAll<ICharacter>(((PooledObject)playfield).Identity))
		{
			if (item != null && !(((IDynel)item).Controller is PlayerController) && string.Equals(((INamedEntity)item).Name, slot.Name, StringComparison.OrdinalIgnoreCase) && ((IStats)item).Stats[(StatIds)27].Value > 0)
			{
				float num = ((IDynel)item).Coordinates().x - slot.X;
				float num2 = ((IDynel)item).Coordinates().z - slot.Z;
				if (num * num + num2 * num2 <= 6.25f)
				{
					return true;
				}
			}
		}
		return false;
	}
}
