using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.Missions;

internal static class MissionRepairService
{
	public static bool IsRepairMission(MissionAcceptedStore.AcceptedMission entry)
	{
		return entry != null && entry.MissionIconId == 11342;
	}

	public static bool IsRepairOffer(QuestInfo offer)
	{
		return offer != null && offer.MissionIconId == 11342;
	}

	public static bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Expected I4, but got Unknown
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || message == null || message.Target == null || message.Target.Length < 2)
		{
			return false;
		}
		ICharacter val = ((client.Controller != null) ? client.Controller.Character : null);
		if (val != null && ((IInstancedEntity)val).Playfield != null)
		{
			Identity val2 = ((IEntity)((IInstancedEntity)val).Playfield).Identity;
			if (MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref val2)).Instance))
			{
				if (!MissionMachineTracker.IsMissionMachine(message.Target[1]))
				{
					return false;
				}
				Pool instance = Pool.Instance;
				val2 = default(Identity);
				Identity identity = ((IEntity)val).Identity;
				((Identity)(ref val2)).Type = (IdentityType)((Identity)(ref identity)).Instance;
				((Identity)(ref val2)).Instance = (int)((Identity)(ref message.Target[0])).Type;
				IInventoryPage @object = instance.GetObject<IInventoryPage>(val2);
				if (@object == null)
				{
					return false;
				}
				IItem val3 = @object[((Identity)(ref message.Target[0])).Instance];
				if (!MissionKeyGrantService.IsRepairTool(val3))
				{
					return false;
				}
				return TryCompleteRepair(client, val, message.Target[1], val3, "RepairMachine");
			}
		}
		return false;
	}

	public static bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || !MissionMachineTracker.IsMissionMachine(target))
		{
			return false;
		}
		ICharacter val = ((client.Controller != null) ? client.Controller.Character : null);
		if (val != null && ((IInstancedEntity)val).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)val).Playfield).Identity;
			if (MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref identity)).Instance))
			{
				if (!MissionKeyGrantService.HasRepairTool(val))
				{
					((IClient)client).Server.Info((IClient)(object)client, "Mission repair: Broken Machine requires Mission Repair Kit", Array.Empty<object>());
					return true;
				}
				if (!TryGetAnyRepairTool(val, out var kit))
				{
					return true;
				}
				return TryCompleteRepair(client, val, target, kit, "RepairMachineUse");
			}
		}
		return false;
	}

	private static bool TryGetAnyRepairTool(ICharacter character, out IItem kit)
	{
		kit = null;
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return false;
		}
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)character).BaseInventory.Pages)
		{
			foreach (KeyValuePair<int, IItem> item in page.Value.List())
			{
				if (MissionKeyGrantService.IsRepairTool(item.Value))
				{
					kit = item.Value;
					return true;
				}
			}
		}
		return false;
	}

	private static bool TryCompleteRepair(IZoneClient client, ICharacter character, Identity machineIdentity, IItem repairItem, string reason)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)character).Identity;
		MissionAcceptedStore.AcceptedMission acceptedMission = FindRepairMission(((Identity)(ref identity)).Instance);
		if (acceptedMission == null)
		{
			LogUtil.Debug((DebugInfoDetail)128, "Mission repair ignored — no RepairMachine accept");
			return false;
		}
		MissionKeyGrantService.TryConsumeRepairTool(client, character, repairItem);
		MissionMachineTracker.Unregister(machineIdentity);
		bool flag = MissionCompleteService.TryComplete(client, character, acceptedMission, reason);
		MissionDiagnostics.Log("REPAIR machine={0} completed={1} reason={2}", machineIdentity, flag, reason);
		return flag;
	}

	private static MissionAcceptedStore.AcceptedMission FindRepairMission(int characterInstance)
	{
		List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(characterInstance);
		for (int num = all.Count - 1; num >= 0; num--)
		{
			if (IsRepairMission(all[num]))
			{
				return all[num];
			}
		}
		return null;
	}
}
