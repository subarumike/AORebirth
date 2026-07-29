using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedAreteAlexAreaVendorRuntimeService
{
	private readonly List<Vendor> capturedVendors = new List<Vendor>();

	private readonly HashSet<int> spawnedPlayfields = new HashSet<int>();

	internal void Spawn(Playfield playfield, Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null || dynelRegistry == null || ((Identity)(ref playfieldIdentity)).Instance != 6553 || !spawnedPlayfields.Add(((Identity)(ref playfieldIdentity)).Instance))
		{
			return;
		}
		int num = 0;
		foreach (CapturedAreteAlexAreaVendorDefinition vendor in CapturedAreteAlexAreaVendorContentProvider.Vendors)
		{
			Vendor val = TryCreateVendor(playfield, playfieldIdentity, vendor);
			if (val != null)
			{
				dynelRegistry.Register((IEntity)(object)val);
				capturedVendors.Add(val);
				num++;
				string[] obj = new string[11]
				{
					"Captured Arete Alex-area vendor spawned name=",
					vendor.DisplayName,
					" sourceVendor=VendingMachine:",
					vendor.SourceVendorInstance.ToString("X8"),
					" runtimeVendor=",
					null,
					null,
					null,
					null,
					null,
					null
				};
				Identity identity = ((PooledObject)val).Identity;
				obj[5] = ((object)(Identity)(ref identity)).ToString();
				obj[6] = " template=";
				obj[7] = ((Dynel)val).Stats[23].Value.ToString();
				obj[8] = " stockRows=";
				obj[9] = vendor.Stock.Count.ToString();
				obj[10] = " evidence=20260720-074847";
				LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
			}
		}
		if (num == 0)
		{
			spawnedPlayfields.Remove(((Identity)(ref playfieldIdentity)).Instance);
		}
	}

	internal void Clear(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		spawnedPlayfields.Remove(((Identity)(ref playfieldIdentity)).Instance);
		foreach (Vendor capturedVendor in capturedVendors)
		{
			dynelRegistry.Unregister(((PooledObject)capturedVendor).Identity);
			Pool.Instance.RemoveObject<Vendor>(capturedVendor);
		}
		capturedVendors.Clear();
	}

	private Vendor TryCreateVendor(Playfield playfield, Identity playfieldIdentity, CapturedAreteAlexAreaVendorDefinition definition)
	{
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Expected O, but got Unknown
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Expected O, but got Unknown
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Expected O, but got Unknown
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Expected O, but got Unknown
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		Vendor val = null;
		try
		{
			int templateId = definition.TemplateId;
			int num = templateId;
			if (!ItemLoader.ItemList.ContainsKey(num) || ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).Get(num) == null)
			{
				if (!ItemLoader.ItemList.ContainsKey(99634))
				{
					throw new InvalidOperationException("missing vendor template item " + templateId + " and fallback " + 99634);
				}
				num = 99634;
			}
			List<KeyValuePair<int, Item>> list = new List<KeyValuePair<int, Item>>();
			foreach (CapturedAreteAlexAreaVendorStockDefinition item in definition.Stock)
			{
				if (!ItemLoader.ItemList.ContainsKey(item.LowId) || !ItemLoader.ItemList.ContainsKey(item.HighId))
				{
					LogUtil.Debug((DebugInfoDetail)128, "Arete Alex-area vendor skip missing stock low=" + item.LowId + " high=" + item.HighId);
				}
				else
				{
					list.Add(new KeyValuePair<int, Item>(item.Slot, new Item(item.Quality, item.LowId, item.HighId)));
				}
			}
			Identity val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)51035;
			((Identity)(ref val2)).Instance = Pool.Instance.GetFreeInstance<Vendor>(1879048192, (IdentityType)51035);
			Identity val3 = val2;
			val = new Vendor(playfieldIdentity, val3, num);
			((Dynel)val).Name = definition.DisplayName;
			val.NpcIdentity = Identity.None;
			((Dynel)val).RawCoordinates = Vector3.op_Implicit(new Vector3((double)definition.X, (double)definition.Y, (double)definition.Z));
			((Dynel)val).Heading = new Quaternion(0.0, 0.0, 0.0, 1.0);
			((Dynel)val).Playfield = (IPlayfield)(object)playfield;
			((Dynel)val).Stats[23].Value = templateId;
			if (((Dynel)val).BaseInventory != null)
			{
				int standardPage = ((Dynel)val).BaseInventory.StandardPage;
				if (((Dynel)val).BaseInventory[standardPage] != null)
				{
					((Dynel)val).BaseInventory[standardPage].List().Clear();
					foreach (KeyValuePair<int, Item> item2 in list)
					{
						((Dynel)val).BaseInventory.AddToPage(standardPage, item2.Key, (IItem)(object)item2.Value);
					}
				}
			}
			return val;
		}
		catch (Exception ex)
		{
			if (val != null)
			{
				Pool.Instance.RemoveObject<Vendor>(val);
			}
			LogUtil.Debug((DebugInfoDetail)512, "Captured Arete Alex-area vendor refused name=" + definition.DisplayName + " reason=" + ex.GetType().Name + ": " + ex.Message);
			return null;
		}
	}
}
