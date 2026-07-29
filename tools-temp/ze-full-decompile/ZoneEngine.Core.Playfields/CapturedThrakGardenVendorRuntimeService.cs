using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Core.Playfields;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedThrakGardenVendorRuntimeService
{
	private readonly List<IEntity> capturedVendors = new List<IEntity>();

	internal void Attach(Playfield playfield, Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || ((Identity)(ref playfieldIdentity)).Instance != 4677 || CapturedThrakGardenVendorRuntimeRegistry.ContainsPlayfield(playfieldIdentity))
		{
			return;
		}
		Dictionary<string, ICharacter> dictionary = new Dictionary<string, ICharacter>(StringComparer.OrdinalIgnoreCase);
		foreach (ICharacter item in dynelRegistry.Characters())
		{
			if (item != null && !string.IsNullOrEmpty(((INamedEntity)item).Name) && !dictionary.ContainsKey(((INamedEntity)item).Name))
			{
				dictionary[((INamedEntity)item).Name] = item;
			}
		}
		int num = 0;
		foreach (CapturedThrakGardenVendorDefinition definition in CapturedThrakGardenVendorContentProvider.Definitions)
		{
			if (!dictionary.TryGetValue(definition.DisplayName, out var value) || value == null)
			{
				LogUtil.Debug((DebugInfoDetail)512, "Thrak garden vendor NPC missing name=" + definition.DisplayName + " evidence=" + definition.Evidence);
				continue;
			}
			Vendor val = (definition.HasCapturedStock ? TryCreateVendor(playfield, playfieldIdentity, value, definition) : null);
			Identity val2 = ((val == null) ? Identity.None : ((PooledObject)val).Identity);
			if (val != null)
			{
				dynelRegistry.Register((IEntity)(object)val);
				capturedVendors.Add((IEntity)(object)val);
			}
			CapturedThrakGardenVendorRuntimeRegistry.Register(new CapturedThrakGardenVendorRuntimeDefinition(playfieldIdentity, ((IEntity)value).Identity, val2, definition));
			num++;
			string[] obj = new string[12]
			{
				"Thrak garden vendor attached name=", definition.DisplayName, " npc=", null, null, null, null, null, null, null,
				null, null
			};
			Identity val3 = ((IEntity)value).Identity;
			obj[3] = ((object)(Identity)(ref val3)).ToString();
			obj[4] = " vendor=";
			val3 = val2;
			obj[5] = ((object)(Identity)(ref val3)).ToString();
			obj[6] = " stockRows=";
			obj[7] = definition.Stock.Count.ToString();
			obj[8] = " gated=";
			obj[9] = (definition.RequiresCompletedGardenKeyQuest ? 1 : 0).ToString();
			obj[10] = " evidence=";
			obj[11] = definition.Evidence;
			LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
		}
		LogUtil.Debug((DebugInfoDetail)128, "Thrak garden vendors attached=" + num + "/" + CapturedThrakGardenVendorContentProvider.Definitions.Count + " pf=" + ((Identity)(ref playfieldIdentity)).Instance);
	}

	internal void Clear(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		CapturedThrakGardenVendorRuntimeRegistry.RemoveForPlayfield(playfieldIdentity);
		foreach (IEntity capturedVendor in capturedVendors)
		{
			dynelRegistry.Unregister(capturedVendor.Identity);
			Vendor val = (Vendor)(object)((capturedVendor is Vendor) ? capturedVendor : null);
			if (val != null)
			{
				Pool.Instance.RemoveObject<Vendor>(val);
			}
		}
		capturedVendors.Clear();
	}

	private Vendor TryCreateVendor(Playfield playfield, Identity playfieldIdentity, ICharacter character, CapturedThrakGardenVendorDefinition definition)
	{
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Expected O, but got Unknown
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Expected O, but got Unknown
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		Vendor val = null;
		try
		{
			if (!ItemLoader.ItemList.ContainsKey(definition.VendorTemplateId))
			{
				throw new InvalidOperationException("missing vendor template item " + definition.VendorTemplateId);
			}
			List<KeyValuePair<int, Item>> list = new List<KeyValuePair<int, Item>>();
			int num = 0;
			foreach (CapturedThrakGardenVendorStockDefinition item in definition.Stock)
			{
				if (!ItemLoader.ItemList.ContainsKey(item.LowId) || !ItemLoader.ItemList.ContainsKey(item.HighId))
				{
					num++;
				}
				else
				{
					list.Add(new KeyValuePair<int, Item>(item.Slot, new Item(item.Quality, item.LowId, item.HighId)));
				}
			}
			if (list.Count == 0)
			{
				throw new InvalidOperationException("no stock items resolved (skipped=" + num + ")");
			}
			Identity val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)51035;
			((Identity)(ref val2)).Instance = Pool.Instance.GetFreeInstance<Vendor>(1879048192, (IdentityType)51035);
			Identity val3 = val2;
			val = new Vendor(playfieldIdentity, val3, definition.VendorTemplateId);
			val.NpcIdentity = ((IEntity)character).Identity;
			Character val4 = (Character)(object)((character is Character) ? character : null);
			if (val4 != null)
			{
				((Dynel)val).RawCoordinates = ((Dynel)val4).RawCoordinates;
				((Dynel)val).Heading = ((Dynel)val4).RawHeading;
			}
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
			LogUtil.Debug((DebugInfoDetail)512, "Thrak garden vendor endpoint refused name=" + definition.DisplayName + " reason=" + ex.Message);
			return null;
		}
	}
}
