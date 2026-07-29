using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;

namespace AORebirth.Core.Playfields;

internal static class JunkyardCleaningRobotRuntime
{
	private const int AreteLandingPlayfieldId = 6553;

	private const string RobotName = "Cleaning Robot";

	private const int RobotLevel = 1;

	private const int RobotHealth = 15;

	private const int RobotScale = 100;

	private const int RobotCharacterFlags = 268964353;

	private const double RespawnSeconds = 90.0;

	private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

	private static readonly Dictionary<int, DateTime[]> NextRespawnUtcBySlot = new Dictionary<int, DateTime[]>();

	private static readonly float[][] SpawnSlots = new float[9][]
	{
		new float[3] { 3589.222f, 5.1100006f, 864.95667f },
		new float[3] { 3583.181f, 5.1100006f, 870.7136f },
		new float[3] { 3587.6567f, 5.1100006f, 881.64233f },
		new float[3] { 3580.9883f, 5.1100006f, 866.9392f },
		new float[3] { 3582.4028f, 5.1100006f, 884.2308f },
		new float[3] { 3585.8594f, 5.1100006f, 869.0397f },
		new float[3] { 3578.9949f, 5.1100006f, 871.8415f },
		new float[3] { 3578.7f, 5.1100006f, 863.3f },
		new float[3] { 3586.5f, 5.1100006f, 862.3f }
	};

	public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 6553 || !LinkedPlayfields.Add(((Identity)(ref playfieldIdentity)).Instance))
		{
			return;
		}
		NextRespawnUtcBySlot[((Identity)(ref playfieldIdentity)).Instance] = new DateTime[SpawnSlots.Length];
		int num = 0;
		for (int i = 0; i < SpawnSlots.Length; i++)
		{
			if (SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
			{
				num++;
			}
		}
		LogUtil.Debug((DebugInfoDetail)128, "JunkyardCleaningRobotRuntime spawned=" + num + "/" + SpawnSlots.Length + " pf=" + ((Identity)(ref playfieldIdentity)).Instance + " source=20260720-072904");
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
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 6553)
		{
			return;
		}
		LinkedPlayfields.Add(((Identity)(ref playfieldIdentity)).Instance);
		if (!NextRespawnUtcBySlot.TryGetValue(((Identity)(ref playfieldIdentity)).Instance, out var value) || value == null || value.Length != SpawnSlots.Length)
		{
			value = new DateTime[SpawnSlots.Length];
			NextRespawnUtcBySlot[((Identity)(ref playfieldIdentity)).Instance] = value;
		}
		int num = CountAliveCleaningRobots(playfield);
		for (int i = 0; i < SpawnSlots.Length; i++)
		{
			if (num >= SpawnSlots.Length)
			{
				break;
			}
			if (HasLivingRobotNear(playfield, SpawnSlots[i]))
			{
				value[i] = DateTime.MaxValue;
			}
			else if (value[i] == DateTime.MaxValue)
			{
				value[i] = DateTime.UtcNow + TimeSpan.FromSeconds(90.0);
			}
			else if (!(value[i] > DateTime.UtcNow) && SpawnSlot(playfield, playfieldIdentity, activateNpc, i) != null)
			{
				value[i] = DateTime.MaxValue;
				num++;
			}
		}
	}

	private static Character SpawnSlot(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc, int slotIndex)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		//IL_0070: Expected O, but got Unknown
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Expected O, but got Unknown
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		float[] array = SpawnSlots[slotIndex];
		NPCController nPCController = new NPCController
		{
			AiProfile = NpcAiProfile.Passive
		};
		Character val = NonPlayerCharacterHandler.SpawnMobFromTemplate("A004", playfieldIdentity, new Coordinate
		{
			x = array[0],
			y = array[1],
			z = array[2]
		}, new Quaternion(0.0, 0.0, 0.0, 1.0), (IController)(object)nPCController, 1);
		if (val == null)
		{
			return null;
		}
		((Dynel)val).Name = "Cleaning Robot";
		((Dynel)val).Playfield = (IPlayfield)(object)playfield;
		CombatTestMobArchetype.Prepare((ICharacter)(object)val, CombatTestMobArchetype.MalfunctioningCleaningRobot);
		((Dynel)val).Name = "Cleaning Robot";
		((Dynel)val).Stats[(StatIds)1].Value = 15;
		((Dynel)val).Stats[(StatIds)1].BaseValue = 15u;
		((Dynel)val).Stats[(StatIds)27].Value = 15;
		((Dynel)val).Stats[(StatIds)27].BaseValue = 15u;
		((Dynel)val).Stats[(StatIds)54].Value = 1;
		((Dynel)val).Stats[(StatIds)54].BaseValue = 1u;
		((Dynel)val).Stats[(StatIds)360].Value = 100;
		((Dynel)val).Stats[(StatIds)360].BaseValue = 100u;
		((Dynel)val).Stats[(StatIds)0].Value = 268964353;
		((Dynel)val).Stats[(StatIds)0].BaseValue = 268964353u;
		((Dynel)val).Stats[(StatIds)673].Value = 31;
		((Dynel)val).Stats[(StatIds)673].BaseValue = 31u;
		((Dynel)val).Coordinates(new Coordinate
		{
			x = array[0],
			y = array[1],
			z = array[2]
		});
		((Dynel)val).DoNotDoTimers = false;
		activateNpc((ICharacter)(object)val);
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
		return val;
	}

	private static int CountAliveCleaningRobots(Playfield playfield)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		foreach (ICharacter item in Pool.Instance.GetAll<ICharacter>(((PooledObject)playfield).Identity))
		{
			if (item != null && !(((IDynel)item).Controller is PlayerController) && string.Equals(((INamedEntity)item).Name, "Cleaning Robot", StringComparison.OrdinalIgnoreCase) && ((IStats)item).Stats[(StatIds)27].Value > 0)
			{
				num++;
			}
		}
		return num;
	}

	private static bool HasLivingRobotNear(Playfield playfield, float[] pos)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		foreach (ICharacter item in Pool.Instance.GetAll<ICharacter>(((PooledObject)playfield).Identity))
		{
			if (item != null && !(((IDynel)item).Controller is PlayerController) && string.Equals(((INamedEntity)item).Name, "Cleaning Robot", StringComparison.OrdinalIgnoreCase) && ((IStats)item).Stats[(StatIds)27].Value > 0)
			{
				float num = ((IDynel)item).Coordinates().x - pos[0];
				float num2 = ((IDynel)item).Coordinates().z - pos[2];
				if (num * num + num2 * num2 <= 6.25f)
				{
					return true;
				}
			}
		}
		return false;
	}
}
