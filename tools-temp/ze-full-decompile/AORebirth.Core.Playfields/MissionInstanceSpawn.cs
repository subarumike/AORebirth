using System;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Missions;

namespace AORebirth.Core.Playfields;

internal static class MissionInstanceSpawn
{
	private const string TemplateHash = "BART";

	private const int CarloPetMonsterData = 258209;

	private const int CeoGuardianPetMonsterData = 227701;

	public static void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
	{
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_030a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0321: Unknown result type (might be due to invalid IL or missing references)
		//IL_0326: Unknown result type (might be due to invalid IL or missing references)
		//IL_0416: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || !MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref playfieldIdentity)).Instance))
		{
			return;
		}
		MissionShape missionShape = MissionInstanceShapeCatalog.PickShape(((Identity)(ref playfieldIdentity)).Instance, new Random(((Identity)(ref playfieldIdentity)).Instance));
		if (missionShape == null || missionShape.Npcs == null || missionShape.Npcs.Length == 0)
		{
			LogUtil.Debug((DebugInfoDetail)512, "MissionInstanceSpawn no shape for pf=" + ((Identity)(ref playfieldIdentity)).Instance);
			return;
		}
		MissionRollType missionRollType = ResolveObjectiveType(((Identity)(ref playfieldIdentity)).Instance);
		MissionNpc killTarget = null;
		if (missionRollType == MissionRollType.KillPerson)
		{
			killTarget = PickKillTargetFromTrash(missionShape);
		}
		int num = 0;
		bool spawnedObjective = false;
		Character val = null;
		MissionNpc[] npcs = missionShape.Npcs;
		foreach (MissionNpc missionNpc in npcs)
		{
			if (missionNpc != null && !IsPlayerPetCapture(missionNpc) && ShouldSpawnNpc(missionNpc, missionRollType, killTarget, ref spawnedObjective) && SpawnOne(playfield, playfieldIdentity, activateNpc, missionNpc, missionRollType, killTarget, out var mob))
			{
				num++;
				if (missionRollType == MissionRollType.FindItem && val == null && missionNpc.Role == MissionNpcRole.FindTarget)
				{
					val = mob;
				}
			}
		}
		if (missionRollType == MissionRollType.FindItem && val == null)
		{
			MissionNpc missionNpc2 = new MissionNpc();
			missionNpc2.Name = "Mission Cache";
			missionNpc2.Role = MissionNpcRole.Trash;
			missionNpc2.Level = 150;
			missionNpc2.Health = 15000;
			missionNpc2.MonsterData = 26137;
			missionNpc2.Scale = 117;
			missionNpc2.HeadMesh = 40209;
			missionNpc2.X = missionShape.SpawnX + 6f;
			missionNpc2.Y = missionShape.SpawnY;
			missionNpc2.Z = missionShape.SpawnZ + 6f;
			missionNpc2.Hx = 0f;
			missionNpc2.Hy = 0f;
			missionNpc2.Hz = 0f;
			missionNpc2.Hw = 1f;
			missionNpc2.Textures = new int[5][]
			{
				new int[2] { 0, 9418 },
				new int[2] { 1, 8729 },
				new int[2] { 2, 15807 },
				new int[2] { 3, 9419 },
				new int[2] { 4, 9421 }
			};
			missionNpc2.Meshes = new int[3][]
			{
				new int[4] { 0, 20055, 0, 2 },
				new int[4] { 0, 40209, 0, 4 },
				new int[4] { 1, 7826, 0, 2 }
			};
			MissionNpc def = missionNpc2;
			if (SpawnOne(playfield, playfieldIdentity, activateNpc, def, missionRollType, null, out var mob2))
			{
				num++;
				val = mob2;
				spawnedObjective = true;
			}
		}
		if (val != null)
		{
			MissionInstanceMobCombat.RegisterFindItemHost(((PooledObject)val).Identity);
			Identity identity = ((PooledObject)val).Identity;
			LogUtil.Debug((DebugInfoDetail)128, "MissionInstanceSpawn FindItem host id=" + ((object)(Identity)(ref identity)).ToString() + " name=" + ((Dynel)val).Name);
		}
		if (missionRollType == MissionRollType.RepairMachine && !spawnedObjective)
		{
			MissionNpc def2 = new MissionNpc
			{
				Name = "Broken Machine",
				Role = MissionNpcRole.BrokenMachine,
				Level = 1,
				Health = 999999,
				MonsterData = 26092,
				Scale = 150,
				HeadMesh = 0,
				X = missionShape.SpawnX + 8f,
				Y = missionShape.SpawnY,
				Z = missionShape.SpawnZ + 8f,
				Hx = 0f,
				Hy = 0f,
				Hz = 0f,
				Hw = 1f,
				Textures = null,
				Meshes = null
			};
			if (SpawnOne(playfield, playfieldIdentity, activateNpc, def2, missionRollType, null, out var _))
			{
				num++;
				spawnedObjective = true;
			}
		}
		LogUtil.Debug((DebugInfoDetail)128, "MissionInstanceSpawn pf=" + ((Identity)(ref playfieldIdentity)).Instance + " shape=" + missionShape.CapturedPlayfieldId + " objective=" + MissionTypeCatalog.TypeName(missionRollType) + " spawned=" + num + " hasObjective=" + spawnedObjective);
	}

	private static bool IsPlayerPetCapture(MissionNpc def)
	{
		if (def.MonsterData == 258209 || def.MonsterData == 227701)
		{
			return true;
		}
		if (string.Equals(def.Name, "Carlo Pinnetti", StringComparison.OrdinalIgnoreCase) || string.Equals(def.Name, "CEO Guardian", StringComparison.OrdinalIgnoreCase) || string.Equals(def.Name, "Corporate Guardian", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return false;
	}

	private static MissionNpc PickKillTargetFromTrash(MissionShape shape)
	{
		MissionNpc missionNpc = null;
		for (int i = 0; i < shape.Npcs.Length; i++)
		{
			MissionNpc missionNpc2 = shape.Npcs[i];
			if (missionNpc2 != null && !IsPlayerPetCapture(missionNpc2) && missionNpc2.Role == MissionNpcRole.Trash && (missionNpc == null || missionNpc2.Level > missionNpc.Level || (missionNpc2.Level == missionNpc.Level && missionNpc2.Health > missionNpc.Health)))
			{
				missionNpc = missionNpc2;
			}
		}
		return missionNpc;
	}

	private static MissionRollType ResolveObjectiveType(int playfieldInstance)
	{
		MissionRollType result = MissionRollType.KillPerson;
		try
		{
			if (MissionInstanceService.TryGetStampedObjective(playfieldInstance, out var type))
			{
				return type;
			}
		}
		catch
		{
		}
		return result;
	}

	private static bool ShouldSpawnNpc(MissionNpc def, MissionRollType objective, MissionNpc killTarget, ref bool spawnedObjective)
	{
		switch (def.Role)
		{
		case MissionNpcRole.Trash:
			if (objective == MissionRollType.KillPerson && killTarget != null && def == killTarget && !spawnedObjective)
			{
				spawnedObjective = true;
			}
			return true;
		case MissionNpcRole.KillBoss:
		case MissionNpcRole.KillGuard:
			return false;
		case MissionNpcRole.FindTarget:
			if (objective == MissionRollType.FindPerson && !spawnedObjective)
			{
				spawnedObjective = true;
				return true;
			}
			if (objective == MissionRollType.FindItem && !spawnedObjective)
			{
				spawnedObjective = true;
				return true;
			}
			return false;
		case MissionNpcRole.BrokenMachine:
			if (objective == MissionRollType.RepairMachine && !spawnedObjective)
			{
				spawnedObjective = true;
				return true;
			}
			return false;
		default:
			return true;
		}
	}

	private static bool SpawnOne(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc, MissionNpc def, MissionRollType objective, MissionNpc killTarget, out Character mob)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Expected O, but got Unknown
		//IL_006c: Expected O, but got Unknown
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Expected O, but got Unknown
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0304: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0321: Unknown result type (might be due to invalid IL or missing references)
		mob = null;
		NPCController nPCController = new NPCController();
		mob = NonPlayerCharacterHandler.SpawnMobFromTemplate("BART", playfieldIdentity, new Coordinate
		{
			x = def.X,
			y = def.Y,
			z = def.Z
		}, new Quaternion((double)def.Hx, (double)def.Hy, (double)def.Hz, (double)def.Hw), (IController)(object)nPCController, def.Level);
		if (mob == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "MissionInstanceSpawn FAILED template=BART npc=" + def.Name);
			return false;
		}
		((Dynel)mob).Name = def.Name;
		((Dynel)mob).Playfield = (IPlayfield)(object)playfield;
		((Dynel)mob).Stats.SetBaseValueWithoutTriggering(359, (uint)def.MonsterData);
		((Dynel)mob).Stats.SetBaseValueWithoutTriggering(1, (uint)def.Health);
		((Dynel)mob).Stats.SetBaseValueWithoutTriggering(27, (uint)def.Health);
		((Dynel)mob).Stats.SetBaseValueWithoutTriggering(54, (uint)def.Level);
		((Dynel)mob).Stats.SetBaseValueWithoutTriggering(33, 0u);
		((Dynel)mob).Stats.SetBaseValueWithoutTriggering(673, 31u);
		if (def.Scale > 0)
		{
			((Dynel)mob).Stats.SetBaseValueWithoutTriggering(360, (uint)def.Scale);
		}
		if (def.HeadMesh > 0)
		{
			((Dynel)mob).Stats.SetBaseValueWithoutTriggering(64, (uint)def.HeadMesh);
		}
		ApplyAppearance(mob, def);
		((Dynel)mob).Coordinates(new Coordinate
		{
			x = def.X,
			y = def.Y,
			z = def.Z
		});
		((Dynel)mob).DoNotDoTimers = false;
		bool flag = def.Role != MissionNpcRole.BrokenMachine && def.Role != MissionNpcRole.FindTarget;
		if (objective == MissionRollType.FindItem && def.Role == MissionNpcRole.FindTarget)
		{
			flag = false;
		}
		if (flag)
		{
			MissionInstanceMobCombat.TryPrepareCombat(mob, nPCController, def.Level);
			MissionInstanceMobCombat.RegisterAggressive(((PooledObject)mob).Identity);
		}
		else
		{
			nPCController.AiProfile = NpcAiProfile.Passive;
		}
		activateNpc((ICharacter)(object)mob);
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)mob, Identity.None);
		bool flag2 = objective == MissionRollType.KillPerson && killTarget != null && def == killTarget;
		bool flag3 = def.Role == MissionNpcRole.FindTarget && objective == MissionRollType.FindPerson;
		Identity identity;
		if (flag2 || flag3)
		{
			MissionTargetTracker.Register(((PooledObject)mob).Identity);
			string[] obj = new string[6]
			{
				"MissionInstanceSpawn objective-target role=",
				def.Role.ToString(),
				" id=",
				null,
				null,
				null
			};
			identity = ((PooledObject)mob).Identity;
			obj[3] = ((object)(Identity)(ref identity)).ToString();
			obj[4] = " name=";
			obj[5] = def.Name;
			LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
		}
		if (def.Role == MissionNpcRole.BrokenMachine)
		{
			MissionMachineTracker.Register(((PooledObject)mob).Identity);
			identity = ((PooledObject)mob).Identity;
			LogUtil.Debug((DebugInfoDetail)128, "MissionInstanceSpawn Broken Machine registered id=" + ((object)(Identity)(ref identity)).ToString());
		}
		return true;
	}

	private static void ApplyAppearance(Character mob, MissionNpc def)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Expected O, but got Unknown
		if (def.Textures != null && def.Textures.Length != 0)
		{
			((Dynel)mob).Textures.Clear();
			int[][] textures = def.Textures;
			foreach (int[] array in textures)
			{
				if (array != null && array.Length >= 2 && array[1] > 0)
				{
					((Dynel)mob).Textures.Add(new AOTextures(array[0], array[1]));
				}
			}
		}
		if (def.Meshes == null || def.Meshes.Length == 0)
		{
			return;
		}
		((Dynel)mob).MeshLayer.Clear();
		mob.SocialMeshLayer.Clear();
		int[][] meshes = def.Meshes;
		foreach (int[] array2 in meshes)
		{
			if (array2 != null && array2.Length >= 4 && array2[1] > 0)
			{
				((Dynel)mob).MeshLayer.AddMesh(array2[0], array2[1], array2[2], array2[3]);
				mob.SocialMeshLayer.AddMesh(array2[0], array2[1], array2[2], array2[3]);
			}
		}
	}
}
