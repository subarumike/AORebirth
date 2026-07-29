using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Functions.GameFunctions;

public class shophash : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)0;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Expected O, but got Unknown
		//IL_00d9: Expected O, but got Unknown
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Expected O, but got Unknown
		Vendor val = (Vendor)(object)((caller is Vendor) ? caller : null);
		if (val != null)
		{
			if (InventoryContainerRuntimeService.Default.VendorShopNeedsDatabaseEntry(val))
			{
				if (!((object)(Identity)(ref val.OriginalIdentity)).Equals((object)Identity.None))
				{
					Identity identity = ((IEntity)((Dynel)val).Playfield).Identity;
					int num = (((Identity)(ref identity)).Instance << 16) | ((((Identity)(ref val.OriginalIdentity)).Instance >> 16) & 0xFF);
					((IInstancedEntity)(ICharacter)self).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM((ICharacter)self, "This shop has no entry in the database yet. Please enter a new entry with the id " + num + ".", 0, 0));
				}
			}
			else
			{
				BaseMessageHandler<ShopUpdateMessage, ShopUpdateMessageHandler>.Default.Send((ICharacter)self, caller, InventoryContainerRuntimeService.Default.GetVendorStandardInventoryPage((Vendor)caller));
			}
		}
		return true;
	}
}
