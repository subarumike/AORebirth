using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class KnuBotRejectedItemsMessageHandler : BaseMessageHandler<KnuBotRejectedItemsMessage, KnuBotRejectedItemsMessageHandler>
{
	public void Send(ICharacter character, Identity knubotTarget, IEnumerable<Item> items)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		Send(character, knubotTarget, items, 1);
	}

	public void Send(ICharacter character, Identity knubotTarget, IEnumerable<Item> items, int unknown2)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<KnuBotRejectedItemsMessage>)(object)this).Send(character, RejectedItems(character, knubotTarget, items, unknown2), false);
	}

	private MessageDataFiller<KnuBotRejectedItemsMessage> RejectedItems(ICharacter character, Identity knubotTarget, IEnumerable<Item> items, int unknown2)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return delegate(KnuBotRejectedItemsMessage x)
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Expected O, but got Unknown
			x.Unknown1 = 2;
			x.Target = knubotTarget;
			((N3Message)x).Identity = ((IEntity)character).Identity;
			List<KnuBotRejectedItem> list = new List<KnuBotRejectedItem>();
			if (items != null)
			{
				foreach (Item item in items)
				{
					if (item != null)
					{
						list.Add(new KnuBotRejectedItem
						{
							HighId = item.HighID,
							LowId = item.LowID,
							Quality = ((item.Quality <= 0) ? 1 : item.Quality),
							Unknown = 1234567890
						});
					}
				}
			}
			x.Items = list.ToArray();
			x.Unknown2 = unknown2;
		};
	}
}
