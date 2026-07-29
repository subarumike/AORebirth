using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class ShopUpdateMessageHandler : BaseMessageHandler<ShopUpdateMessage, ShopUpdateMessageHandler>
{
	public void Send(ICharacter receiver, IEntity shop, IInventoryPage page)
	{
		base.Send(receiver, Filler(shop, page), false);
	}

	private MessageDataFiller<ShopUpdateMessage> Filler(IEntity shop, IInventoryPage page)
	{
		return delegate(ShopUpdateMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Expected O, but got Unknown
			((N3Message)x).Identity = shop.Identity;
			((N3Message)x).Unknown = 1;
			List<VendingMachineSlot> list = new List<VendingMachineSlot>();
			foreach (IItem item in from pair in page.List()
				orderby pair.Key
				select pair.Value)
			{
				VendingMachineSlot val = new VendingMachineSlot();
				val.ItemHighId = item.HighID;
				val.ItemLowId = item.LowID;
				val.Quality = item.Quality;
				list.Add(val);
			}
			x.VendingMachineSlots = list.ToArray();
		};
	}
}
