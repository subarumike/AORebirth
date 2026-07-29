using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.Subway.Quests;

internal sealed class WindcallerKarrecNpcRuntimeService
{
	private readonly List<Character> capturedCharacters = new List<Character>();

	internal void Spawn(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc, Action<Identity> deactivateNpc)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || activateNpc == null || deactivateNpc == null || ((Identity)(ref playfieldIdentity)).Instance != 655 || WindcallerKarrecNpcRuntimeRegistry.ContainsPlayfield(playfieldIdentity))
		{
			return;
		}
		List<Character> list = new List<Character>();
		Identity val2;
		try
		{
			foreach (WindcallerKarrecNpcDefinition definition in WindcallerKarrecNpcContent.Definitions)
			{
				list.Add(CreateCharacter(playfield, playfieldIdentity, definition));
			}
			capturedCharacters.AddRange(list);
			for (int i = 0; i < list.Count; i++)
			{
				Character val = list[i];
				WindcallerKarrecNpcDefinition windcallerKarrecNpcDefinition = WindcallerKarrecNpcContent.Definitions[i];
				WindcallerKarrecNpcRuntimeRegistry.Register(new WindcallerKarrecNpcRuntimeDefinition(playfieldIdentity, ((PooledObject)val).Identity, windcallerKarrecNpcDefinition));
				activateNpc((ICharacter)(object)val);
				playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
				string[] obj = new string[10] { "Windcaller Karrec quest NPC spawned sourceNpc=", windcallerKarrecNpcDefinition.SourceNpcIdentity, " runtimeNpc=", null, null, null, null, null, null, null };
				val2 = ((PooledObject)val).Identity;
				obj[3] = ((object)(Identity)(ref val2)).ToString();
				obj[4] = " name=";
				obj[5] = windcallerKarrecNpcDefinition.DisplayName;
				obj[6] = " patrolSegments=";
				obj[7] = windcallerKarrecNpcDefinition.PatrolSegments.Count.ToString();
				obj[8] = " evidence=";
				obj[9] = windcallerKarrecNpcDefinition.Evidence;
				LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
			}
		}
		catch (Exception ex)
		{
			foreach (Character item in list)
			{
				if (!capturedCharacters.Contains(item))
				{
					Pool.Instance.RemoveObject<Character>(item);
				}
			}
			Clear(playfieldIdentity, deactivateNpc);
			val2 = playfieldIdentity;
			LogUtil.Debug((DebugInfoDetail)512, "Windcaller Karrec quest NPC population failed playfield=" + ((object)(Identity)(ref val2)).ToString() + " reason=" + ex.Message);
		}
	}

	internal void Clear(Identity playfieldIdentity, Action<Identity> deactivateNpc)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		WindcallerKarrecNpcRuntimeRegistry.RemoveForPlayfield(playfieldIdentity);
		foreach (Character capturedCharacter in capturedCharacters)
		{
			deactivateNpc(((PooledObject)capturedCharacter).Identity);
			Pool.Instance.RemoveObject<Character>(capturedCharacter);
		}
		capturedCharacters.Clear();
	}

	private Character CreateCharacter(Playfield playfield, Identity playfieldIdentity, WindcallerKarrecNpcDefinition definition)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Expected O, but got Unknown
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Expected O, but got Unknown
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Expected O, but got Unknown
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_0343: Expected O, but got Unknown
		int freeInstance = Pool.Instance.GetFreeInstance<Character>(1000000, (IdentityType)50000);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = freeInstance;
		Identity val2 = val;
		NPCController nPCController = new NPCController
		{
			AiProfile = NpcAiProfile.Social
		};
		Character val3 = new Character(playfieldIdentity, val2, (IController)(object)nPCController);
		((Dynel)val3).Read();
		nPCController.Character = (ICharacter)(object)val3;
		((Dynel)val3).Playfield = (IPlayfield)(object)playfield;
		((Dynel)val3).Name = definition.DisplayName;
		val3.FirstName = string.Empty;
		val3.LastName = string.Empty;
		((Dynel)val3).Coordinates(new Coordinate
		{
			x = definition.X,
			y = definition.Y,
			z = definition.Z
		});
		((Dynel)val3).RawHeading = new Quaternion((double)definition.HeadingX, (double)definition.HeadingY, (double)definition.HeadingZ, (double)definition.HeadingW);
		SetStat((ICharacter)(object)val3, (StatIds)33, definition.Side);
		SetStat((ICharacter)(object)val3, (StatIds)47, definition.Fatness);
		SetStat((ICharacter)(object)val3, (StatIds)4, definition.Breed);
		SetStat((ICharacter)(object)val3, (StatIds)59, definition.Sex);
		SetStat((ICharacter)(object)val3, (StatIds)89, definition.Race);
		SetStat((ICharacter)(object)val3, (StatIds)0, definition.CharacterFlags);
		SetStat((ICharacter)(object)val3, (StatIds)660, 0);
		SetStat((ICharacter)(object)val3, (StatIds)389, 0);
		SetStat((ICharacter)(object)val3, (StatIds)455, definition.NpcFamily);
		SetStat((ICharacter)(object)val3, (StatIds)466, definition.NpcLosHeight);
		SetStat((ICharacter)(object)val3, (StatIds)359, definition.MonsterData);
		SetStat((ICharacter)(object)val3, (StatIds)360, definition.MonsterScale);
		SetStat((ICharacter)(object)val3, (StatIds)64, definition.HeadMesh);
		SetStat((ICharacter)(object)val3, (StatIds)673, definition.VisualFlags);
		SetStat((ICharacter)(object)val3, (StatIds)173, 8);
		SetStat((ICharacter)(object)val3, (StatIds)174, 8);
		SetStat((ICharacter)(object)val3, (StatIds)156, definition.RunSpeed);
		SetStat((ICharacter)(object)val3, (StatIds)54, definition.Level);
		SetStat((ICharacter)(object)val3, (StatIds)1, definition.Health);
		SetStat((ICharacter)(object)val3, (StatIds)27, definition.Health);
		((Dynel)val3).Textures.Clear();
		foreach (WindcallerKarrecNpcTextureDefinition texture in definition.Textures)
		{
			((Dynel)val3).Textures.Add(new AOTextures(texture.Place, texture.Id));
		}
		foreach (WindcallerKarrecNpcMeshDefinition mesh in definition.Meshes)
		{
			((Dynel)val3).MeshLayer.AddMesh(mesh.Position, (int)mesh.Id, mesh.OverrideTextureId, mesh.Layer);
			val3.SocialMeshLayer.AddMesh(mesh.Position, (int)mesh.Id, mesh.OverrideTextureId, mesh.Layer);
		}
		val3.Waypoints.Clear();
		foreach (WindcallerKarrecNpcWaypointDefinition scfuWaypoint in definition.ScfuWaypoints)
		{
			val3.AddWaypoint(new Vector3((double)scfuWaypoint.X, (double)scfuWaypoint.Y, (double)scfuWaypoint.Z), false);
		}
		if (definition.HasPatrol)
		{
			nPCController.SetCapturedPatrolReplaySegments(BuildPatrolReplay(definition));
			nPCController.State = (CharacterState)4;
		}
		((Dynel)val3).DoNotDoTimers = !definition.HasPatrol;
		return val3;
	}

	private static NpcPatrolReplaySegment[] BuildPatrolReplay(WindcallerKarrecNpcDefinition definition)
	{
		return definition.PatrolSegments.Select((WindcallerKarrecNpcPatrolSegment segment) => new NpcPatrolReplaySegment(segment.DelayAfterSeconds, segment.StartX, segment.StartY, segment.StartZ, segment.EndX, segment.EndY, segment.EndZ, segment.MoveMode)).ToArray();
	}

	private static void SetStat(ICharacter character, StatIds stat, int value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected I4, but got Unknown
		((IStats)character).Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
	}
}
