using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.Packets;

public static class UnEquip
{
	private static bool IsWeaponHandSlot(IInventoryPage page, int slotNumber)
	{
		return page is WeaponInventoryPage && (slotNumber == 6 || slotNumber == 8);
	}

	public static void Send(IZoneClient client, IInventoryPage page, int slotNumber)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Expected O, but got Unknown
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Expected O, but got Unknown
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		Identity placement;
		Identity identity;
		if (IsWeaponHandSlot(page, slotNumber))
		{
			CharacterActionMessage val = new CharacterActionMessage
			{
				Identity = ((IEntity)client.Controller.Character).Identity,
				Action = (CharacterActionType)97,
				Parameter2 = slotNumber,
				Unknown = 0
			};
			((IDynel)client.Controller.Character).Send((MessageBody)(object)val, false);
		}
		else if (slotNumber == 6)
		{
			IItem val2 = page[slotNumber];
			if (val2 != null)
			{
				TemplateActionMessage val3 = new TemplateActionMessage
				{
					Identity = ((IEntity)client.Controller.Character).Identity,
					ItemHighId = val2.HighID,
					ItemLowId = val2.LowID,
					Quality = val2.Quality,
					Unknown1 = 1,
					Unknown2 = 7
				};
				placement = default(Identity);
				identity = ((IEntity)page).Identity;
				((Identity)(ref placement)).Type = ((Identity)(ref identity)).Type;
				((Identity)(ref placement)).Instance = slotNumber;
				val3.Placement = placement;
				((N3Message)val3).Unknown = 0;
				TemplateActionMessage val4 = val3;
				((IDynel)client.Controller.Character).Send((MessageBody)(object)val4, false);
			}
		}
		else
		{
			IItem val5 = page[slotNumber];
			TemplateActionMessage val6 = new TemplateActionMessage();
			((N3Message)val6).Identity = ((IEntity)client.Controller.Character).Identity;
			val6.ItemHighId = val5.HighID;
			val6.ItemLowId = val5.LowID;
			val6.Quality = val5.Quality;
			val6.Unknown1 = 1;
			val6.Unknown2 = ((page is SocialArmorInventoryPage) ? 3 : 7);
			placement = default(Identity);
			identity = ((IEntity)page).Identity;
			((Identity)(ref placement)).Type = ((Identity)(ref identity)).Type;
			((Identity)(ref placement)).Instance = slotNumber;
			val6.Placement = placement;
			((N3Message)val6).Unknown = 0;
			TemplateActionMessage val7 = val6;
			((IDynel)client.Controller.Character).Send((MessageBody)(object)val7, false);
		}
	}
}
