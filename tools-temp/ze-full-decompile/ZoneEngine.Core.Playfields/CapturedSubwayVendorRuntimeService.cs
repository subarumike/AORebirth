using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Core.Playfields;
using AORebirth.Core.Textures;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedSubwayVendorRuntimeService
{
	private readonly List<IEntity> capturedEntities = new List<IEntity>();

	internal void Spawn(Playfield playfield, Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry, Action<ICharacter> registerNpc)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		if (((Identity)(ref playfieldIdentity)).Instance != 127 || CapturedSubwayVendorRuntimeRegistry.ContainsPlayfield(playfieldIdentity))
		{
			return;
		}
		foreach (CapturedSubwayVendorDefinition definition in CapturedSubwayVendorContentProvider.Definitions)
		{
			Character val = CreateCharacter(playfield, playfieldIdentity, definition);
			if (val != null)
			{
				registerNpc((ICharacter)(object)val);
				capturedEntities.Add((IEntity)(object)val);
				Vendor val2 = (definition.HasCapturedStock ? TryCreateVendor(playfield, playfieldIdentity, val, definition) : null);
				Identity val3 = ((val2 == null) ? Identity.None : ((PooledObject)val2).Identity);
				if (val2 != null)
				{
					dynelRegistry.Register((IEntity)(object)val2);
					capturedEntities.Add((IEntity)(object)val2);
				}
				CapturedSubwayVendorRuntimeRegistry.Register(new CapturedSubwayVendorRuntimeDefinition(playfieldIdentity, ((PooledObject)val).Identity, val3, definition));
				playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
				string[] obj = new string[12]
				{
					"Captured Subway merchant spawned sourceNpc=SimpleChar:",
					definition.SourceNpcInstance.ToString("X8"),
					" runtimeNpc=",
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null
				};
				Identity val4 = ((PooledObject)val).Identity;
				obj[3] = ((object)(Identity)(ref val4)).ToString();
				obj[4] = " runtimeVendor=";
				val4 = val3;
				obj[5] = ((object)(Identity)(ref val4)).ToString();
				obj[6] = " name=";
				obj[7] = definition.DisplayName;
				obj[8] = " stockRows=";
				obj[9] = definition.Stock.Count.ToString();
				obj[10] = " evidence=";
				obj[11] = definition.Evidence;
				LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
			}
		}
	}

	internal void Clear(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		CapturedSubwayVendorRuntimeRegistry.RemoveForPlayfield(playfieldIdentity);
		foreach (IEntity capturedEntity in capturedEntities)
		{
			dynelRegistry.Unregister(capturedEntity.Identity);
			Character val = (Character)(object)((capturedEntity is Character) ? capturedEntity : null);
			if (val != null)
			{
				Pool.Instance.RemoveObject<Character>(val);
				continue;
			}
			Vendor val2 = (Vendor)(object)((capturedEntity is Vendor) ? capturedEntity : null);
			if (val2 != null)
			{
				Pool.Instance.RemoveObject<Vendor>(val2);
			}
		}
		capturedEntities.Clear();
	}

	private Character CreateCharacter(Playfield playfield, Identity playfieldIdentity, CapturedSubwayVendorDefinition definition)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected O, but got Unknown
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Expected O, but got Unknown
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Expected O, but got Unknown
		//IL_0329: Unknown result type (might be due to invalid IL or missing references)
		//IL_0334: Expected O, but got Unknown
		Character val = null;
		try
		{
			int freeInstance = Pool.Instance.GetFreeInstance<Character>(1000000, (IdentityType)50000);
			Identity val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)50000;
			((Identity)(ref val2)).Instance = freeInstance;
			Identity val3 = val2;
			NPCController nPCController = new NPCController();
			val = new Character(playfieldIdentity, val3, (IController)(object)nPCController);
			((Dynel)val).Read();
			nPCController.Character = (ICharacter)(object)val;
			((Dynel)val).Playfield = (IPlayfield)(object)playfield;
			((Dynel)val).Name = definition.DisplayName;
			val.FirstName = string.Empty;
			val.LastName = string.Empty;
			((Dynel)val).Coordinates(new Coordinate
			{
				x = definition.X,
				y = definition.Y,
				z = definition.Z
			});
			((Dynel)val).RawHeading = new Quaternion((double)definition.HeadingX, (double)definition.HeadingY, (double)definition.HeadingZ, (double)definition.HeadingW);
			SetStat((ICharacter)(object)val, (StatIds)33, definition.Side);
			SetStat((ICharacter)(object)val, (StatIds)47, definition.Fatness);
			SetStat((ICharacter)(object)val, (StatIds)4, definition.Breed);
			SetStat((ICharacter)(object)val, (StatIds)59, definition.Sex);
			SetStat((ICharacter)(object)val, (StatIds)89, definition.Race);
			SetStat((ICharacter)(object)val, (StatIds)0, definition.CharacterFlags);
			SetStat((ICharacter)(object)val, (StatIds)660, 0);
			SetStat((ICharacter)(object)val, (StatIds)389, 0);
			SetStat((ICharacter)(object)val, (StatIds)455, 0);
			SetStat((ICharacter)(object)val, (StatIds)466, 0);
			SetStat((ICharacter)(object)val, (StatIds)359, definition.MonsterData);
			SetStat((ICharacter)(object)val, (StatIds)360, definition.MonsterScale);
			SetStat((ICharacter)(object)val, (StatIds)64, definition.HeadMesh);
			SetStat((ICharacter)(object)val, (StatIds)673, definition.VisualFlags);
			SetStat((ICharacter)(object)val, (StatIds)173, 3);
			SetStat((ICharacter)(object)val, (StatIds)174, 3);
			SetStat((ICharacter)(object)val, (StatIds)156, definition.RunSpeed);
			SetStat((ICharacter)(object)val, (StatIds)54, definition.Level);
			SetStat((ICharacter)(object)val, (StatIds)1, definition.Health);
			SetStat((ICharacter)(object)val, (StatIds)27, definition.Health);
			((Dynel)val).Textures.Clear();
			foreach (CapturedSubwayVendorTextureDefinition texture in definition.Textures)
			{
				((Dynel)val).Textures.Add(new AOTextures(texture.Place, texture.Id));
			}
			foreach (CapturedSubwayVendorMeshDefinition mesh in definition.Meshes)
			{
				((Dynel)val).MeshLayer.AddMesh(mesh.Position, (int)mesh.Id, mesh.OverrideTextureId, mesh.Layer);
				val.SocialMeshLayer.AddMesh(mesh.Position, (int)mesh.Id, mesh.OverrideTextureId, mesh.Layer);
			}
			val.Waypoints.Clear();
			foreach (CapturedSubwayVendorWaypointDefinition waypoint in definition.Waypoints)
			{
				val.AddWaypoint(new Vector3((double)waypoint.X, (double)waypoint.Y, (double)waypoint.Z), false);
			}
			((Dynel)val).DoNotDoTimers = true;
			return val;
		}
		catch (Exception ex)
		{
			if (val != null)
			{
				Pool.Instance.RemoveObject<Character>(val);
			}
			LogUtil.Debug((DebugInfoDetail)512, "Captured Subway merchant NPC refused sourceNpc=SimpleChar:" + definition.SourceNpcInstance.ToString("X8") + " reason=" + ex.Message);
			return null;
		}
	}

	private Vendor TryCreateVendor(Playfield playfield, Identity playfieldIdentity, Character character, CapturedSubwayVendorDefinition definition)
	{
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Expected O, but got Unknown
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Expected O, but got Unknown
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Expected O, but got Unknown
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Expected O, but got Unknown
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		Vendor val = null;
		try
		{
			if (!ItemLoader.ItemList.ContainsKey(definition.VendorTemplateId))
			{
				throw new InvalidOperationException("missing vendor template item " + definition.VendorTemplateId);
			}
			List<KeyValuePair<int, Item>> list = new List<KeyValuePair<int, Item>>();
			foreach (CapturedSubwayVendorStockDefinition item in definition.Stock)
			{
				if (!ItemLoader.ItemList.ContainsKey(item.LowId) || !ItemLoader.ItemList.ContainsKey(item.HighId))
				{
					throw new InvalidOperationException("missing stock item low=" + item.LowId + " high=" + item.HighId);
				}
				list.Add(new KeyValuePair<int, Item>(item.Slot, new Item(item.Quality, item.LowId, item.HighId)));
			}
			Identity val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)51035;
			((Identity)(ref val2)).Instance = Pool.Instance.GetFreeInstance<Vendor>(1879048192, (IdentityType)51035);
			Identity val3 = val2;
			val = new Vendor(playfieldIdentity, val3, definition.VendorTemplateId);
			val.NpcIdentity = ((PooledObject)character).Identity;
			((Dynel)val).RawCoordinates = Vector3.op_Implicit(new Vector3((double)definition.X, (double)definition.Y, (double)definition.Z));
			((Dynel)val).Heading = new Quaternion((double)definition.HeadingX, (double)definition.HeadingY, (double)definition.HeadingZ, (double)definition.HeadingW);
			((Dynel)val).Playfield = (IPlayfield)(object)playfield;
			int standardPage = ((Dynel)val).BaseInventory.StandardPage;
			((Dynel)val).BaseInventory[standardPage].List().Clear();
			foreach (KeyValuePair<int, Item> item2 in list)
			{
				((Dynel)val).BaseInventory.AddToPage(standardPage, item2.Key, (IItem)(object)item2.Value);
			}
			return val;
		}
		catch (Exception ex)
		{
			if (val != null)
			{
				Pool.Instance.RemoveObject<Vendor>(val);
			}
			LogUtil.Debug((DebugInfoDetail)512, "Captured Subway merchant endpoint refused atomically sourceVendor=VendingMachine:" + definition.SourceVendorInstance.ToString("X8") + " name=" + definition.DisplayName + " reason=" + ex.Message);
			return null;
		}
	}

	private static void SetStat(ICharacter character, StatIds stat, int value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected I4, but got Unknown
		((IStats)character).Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
	}
}
