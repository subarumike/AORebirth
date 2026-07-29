using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.Packets;

public static class Equip
{
	private static bool IsWeaponHandSlot(IInventoryPage page, int slotNumber)
	{
		return page is WeaponInventoryPage && (slotNumber == 6 || slotNumber == 8);
	}

	public static void Send(IZoneClient client, IInventoryPage page, int slotNumber)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Expected O, but got Unknown
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Expected O, but got Unknown
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Expected O, but got Unknown
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		Identity placement;
		Identity identity;
		if (IsWeaponHandSlot(page, slotNumber))
		{
			IItem item = page[slotNumber];
			CharacterActionMessage val = new CharacterActionMessage
			{
				Identity = ((IEntity)client.Controller.Character).Identity,
				Action = (CharacterActionType)167,
				Unknown = 0
			};
			((IDynel)client.Controller.Character).Send((MessageBody)(object)val, false);
			CharacterActionMessage val2 = new CharacterActionMessage
			{
				Identity = ((IEntity)client.Controller.Character).Identity,
				Action = (CharacterActionType)131,
				Target = WeaponItemIdentity.GetOrCreate(item),
				Parameter1 = 0,
				Parameter2 = slotNumber,
				Unknown = 0
			};
			((IDynel)client.Controller.Character).Send((MessageBody)(object)val2, false);
			((IInstancedEntity)client.Controller.Character).Playfield.AnnounceOthers((MessageBody)(object)val, ((IEntity)client.Controller.Character).Identity);
			((IInstancedEntity)client.Controller.Character).Playfield.AnnounceOthers((MessageBody)(object)val2, ((IEntity)client.Controller.Character).Identity);
		}
		else if (slotNumber == 6)
		{
			IItem val3 = page[slotNumber];
			if (val3 != null)
			{
				TemplateActionMessage val4 = new TemplateActionMessage
				{
					Identity = ((IEntity)client.Controller.Character).Identity,
					ItemHighId = val3.HighID,
					ItemLowId = val3.LowID,
					Quality = val3.Quality,
					Unknown1 = 1,
					Unknown2 = 6
				};
				placement = default(Identity);
				identity = ((IEntity)page).Identity;
				((Identity)(ref placement)).Type = ((Identity)(ref identity)).Type;
				((Identity)(ref placement)).Instance = slotNumber;
				val4.Placement = placement;
				((N3Message)val4).Unknown = 0;
				TemplateActionMessage val5 = val4;
				((IDynel)client.Controller.Character).Send((MessageBody)(object)val5, false);
			}
		}
		else
		{
			IItem val6 = page[slotNumber];
			TemplateActionMessage val7 = new TemplateActionMessage();
			((N3Message)val7).Identity = ((IEntity)client.Controller.Character).Identity;
			val7.ItemHighId = val6.HighID;
			val7.ItemLowId = val6.LowID;
			val7.Quality = val6.Quality;
			val7.Unknown1 = 1;
			val7.Unknown2 = ((page is SocialArmorInventoryPage) ? 3 : 6);
			placement = default(Identity);
			identity = ((IEntity)page).Identity;
			((Identity)(ref placement)).Type = ((Identity)(ref identity)).Type;
			((Identity)(ref placement)).Instance = slotNumber;
			val7.Placement = placement;
			((N3Message)val7).Unknown = 0;
			TemplateActionMessage val8 = val7;
			((IDynel)client.Controller.Character).Send((MessageBody)(object)val8, false);
		}
	}
}
