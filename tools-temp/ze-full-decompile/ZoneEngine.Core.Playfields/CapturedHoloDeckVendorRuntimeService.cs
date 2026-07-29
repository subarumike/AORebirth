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

internal sealed class CapturedHoloDeckVendorRuntimeService
{
	private const int RuntimeVendorTemplateFallbackId = 99634;

	private readonly List<Vendor> capturedVendors = new List<Vendor>();

	internal void Spawn(Playfield playfield, Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		if (playfield != null && dynelRegistry != null && ((Identity)(ref playfieldIdentity)).Instance == 7001 && !CapturedHoloDeckVendorRuntimeRegistry.ContainsPlayfield(playfieldIdentity))
		{
			Vendor val = TryCreateVendor(playfield, playfieldIdentity);
			if (val != null)
			{
				dynelRegistry.Register((IEntity)(object)val);
				capturedVendors.Add(val);
				CapturedHoloDeckVendorRuntimeRegistry.Register(new CapturedHoloDeckVendorRuntimeDefinition(playfieldIdentity, ((PooledObject)val).Identity));
				string[] obj = new string[9]
				{
					"Captured HoloDeck vendor spawned sourceVendor=VendingMachine:",
					317020650.ToString("X8"),
					" runtimeVendor=",
					null,
					null,
					null,
					null,
					null,
					null
				};
				Identity identity = ((PooledObject)val).Identity;
				obj[3] = ((object)(Identity)(ref identity)).ToString();
				obj[4] = " template=";
				obj[5] = ((Dynel)val).Stats[23].Value.ToString();
				obj[6] = " stockRows=";
				obj[7] = CapturedHoloDeckVendorContentProvider.Stock.Count.ToString();
				obj[8] = " evidence=20260719-155043";
				LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
			}
		}
	}

	internal void Clear(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		CapturedHoloDeckVendorRuntimeRegistry.RemoveForPlayfield(playfieldIdentity);
		foreach (Vendor capturedVendor in capturedVendors)
		{
			dynelRegistry.Unregister(((PooledObject)capturedVendor).Identity);
			Pool.Instance.RemoveObject<Vendor>(capturedVendor);
		}
		capturedVendors.Clear();
	}

	private Vendor TryCreateVendor(Playfield playfield, Identity playfieldIdentity)
	{
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Expected O, but got Unknown
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Expected O, but got Unknown
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Expected O, but got Unknown
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Expected O, but got Unknown
		//IL_02fe: Unknown result type (might be due to invalid IL or missing references)
		Vendor val = null;
		try
		{
			int num = 303217;
			int num2 = num;
			if (!ItemLoader.ItemList.ContainsKey(num2) || ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).Get(num2) == null)
			{
				if (!ItemLoader.ItemList.ContainsKey(99634))
				{
					throw new InvalidOperationException("missing vendor template item " + num + " and fallback " + 99634);
				}
				num2 = 99634;
				LogUtil.Debug((DebugInfoDetail)128, "Captured HoloDeck vendor using fallback template=" + num2 + " missingCaptureTemplate=" + num);
			}
			List<KeyValuePair<int, Item>> list = new List<KeyValuePair<int, Item>>();
			foreach (CapturedHoloDeckVendorStockDefinition item in CapturedHoloDeckVendorContentProvider.Stock)
			{
				if (!ItemLoader.ItemList.ContainsKey(item.LowId) || !ItemLoader.ItemList.ContainsKey(item.HighId))
				{
					LogUtil.Debug((DebugInfoDetail)128, "Captured HoloDeck vendor skip missing stock low=" + item.LowId + " high=" + item.HighId);
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
			val = new Vendor(playfieldIdentity, val3, num2);
			if (string.IsNullOrEmpty(((Dynel)val).Name))
			{
				DBItemName val4 = ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).Get(num);
				((Dynel)val).Name = ((val4 != null && !string.IsNullOrEmpty(val4.Name)) ? val4.Name : "Reward Terminal");
			}
			val.NpcIdentity = Identity.None;
			((Dynel)val).RawCoordinates = Vector3.op_Implicit(new Vector3(186.9553985595703, 1.2099989652633667, 201.39390563964844));
			((Dynel)val).Heading = new Quaternion(0.0, 0.9999945759773254, 0.0, 0.0032836890313774347);
			((Dynel)val).Playfield = (IPlayfield)(object)playfield;
			((Dynel)val).Stats[23].Value = num;
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
			LogUtil.Debug((DebugInfoDetail)512, "Captured HoloDeck vendor refused sourceVendor=VendingMachine:" + 317020650.ToString("X8") + " reason=" + ex.GetType().Name + ": " + ex.Message);
			return null;
		}
	}
}
