using System.Collections.Generic;
using System.Threading;
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
public class InventoryUpdateMessageHandler : BaseMessageHandler<InventoryUpdateMessage, InventoryUpdateMessageHandler>
{
	private const int BackpackInventoryFirstHandle = 112;

	private static int nextBackpackInventoryHandle = 111;

	public void Send(ICharacter character, IInventoryPage page)
	{
		((AbstractMessageHandler<InventoryUpdateMessage>)(object)this).Send(character, FillData(character, page), false);
	}

	public void SendContainerOpen(ICharacter character, IInventoryPage page)
	{
		SendContainerOpen(character, page, ReserveBackpackInventoryHandle());
	}

	public void SendContainerOpen(ICharacter character, IInventoryPage page, int handle)
	{
		InventoryContainerRuntimeService.Default.RegisterBackpackInventoryHandle(character, page, handle);
		((AbstractMessageHandler<InventoryUpdateMessage>)(object)this).Send(character, FillContainerData(character, page, handle, 1), false);
	}

	public void SendContainerIntroduce(ICharacter character, IInventoryPage page)
	{
		SendContainerIntroduce(character, page, ReserveBackpackInventoryHandle());
	}

	public void SendContainerIntroduce(ICharacter character, IInventoryPage page, int handle)
	{
		InventoryContainerRuntimeService.Default.RegisterBackpackInventoryHandle(character, page, handle);
		((AbstractMessageHandler<InventoryUpdateMessage>)(object)this).Send(character, FillContainerData(character, page, handle, 0), false);
	}

	public void SendFreshContainerOpen(ICharacter character, IInventoryPage page)
	{
		SendFreshContainerOpen(character, page, ReserveBackpackInventoryHandle());
	}

	public void SendFreshContainerOpen(ICharacter character, IInventoryPage page, int handle)
	{
		InventoryContainerRuntimeService.Default.RegisterBackpackInventoryHandle(character, page, handle);
		((AbstractMessageHandler<InventoryUpdateMessage>)(object)this).Send(character, FillContainerData(character, page, handle, 1), false);
	}

	public int ReserveBackpackInventoryHandle()
	{
		return Interlocked.Increment(ref nextBackpackInventoryHandle);
	}

	public MessageDataFiller<InventoryUpdateMessage> FillData(ICharacter character, IInventoryPage page)
	{
		return delegate(InventoryUpdateMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ef: Expected O, but got Unknown
			//IL_012d: Unknown result type (might be due to invalid IL or missing references)
			x.BagIdentity = ((IEntity)page).Identity;
			x.NumberOfSlots = page.MaxSlots;
			x.SlotnumberInMainInventory = 0;
			List<InventoryEntry> list = new List<InventoryEntry>();
			foreach (KeyValuePair<int, IItem> item in page.List())
			{
				list.Add(new InventoryEntry
				{
					Slotnumber = item.Key,
					Identity = ResolveInventoryEntryIdentity(character, page, item.Key, item.Value),
					Quality = item.Value.Quality,
					HighId = item.Value.HighID,
					LowId = item.Value.LowID,
					UnknownFlags = 33,
					Unknown1 = (short)item.Value.MultipleCount,
					Unknown2 = 0
				});
			}
			x.Entries = list.ToArray();
			x.Unknown2 = 1;
			x.Unknown1 = 3;
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 1;
		};
	}

	public MessageDataFiller<InventoryUpdateMessage> FillContainerOpenData(ICharacter character, IInventoryPage page)
	{
		return FillContainerOpenData(character, page, 112);
	}

	public MessageDataFiller<InventoryUpdateMessage> FillContainerOpenData(ICharacter character, IInventoryPage page, int handle)
	{
		return FillContainerData(character, page, handle, 1);
	}

	private MessageDataFiller<InventoryUpdateMessage> FillContainerData(ICharacter character, IInventoryPage page, int handle, int unknown2)
	{
		return delegate(InventoryUpdateMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Expected O, but got Unknown
			//IL_012a: Unknown result type (might be due to invalid IL or missing references)
			x.BagIdentity = ((IEntity)page).Identity;
			x.NumberOfSlots = page.MaxSlots;
			x.SlotnumberInMainInventory = handle;
			List<InventoryEntry> list = new List<InventoryEntry>();
			foreach (KeyValuePair<int, IItem> item in page.List())
			{
				list.Add(new InventoryEntry
				{
					Slotnumber = item.Key,
					Identity = item.Value.Identity,
					Quality = item.Value.Quality,
					HighId = item.Value.HighID,
					LowId = item.Value.LowID,
					UnknownFlags = 33,
					Unknown1 = (short)GetItemCount(item.Value),
					Unknown2 = 0
				});
			}
			x.Entries = list.ToArray();
			x.Unknown2 = unknown2;
			x.Unknown1 = 3;
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 1;
		};
	}

	private int GetItemCount(IItem item)
	{
		return (item.MultipleCount <= 0) ? 1 : item.MultipleCount;
	}

	private static Identity ResolveInventoryEntryIdentity(ICharacter character, IInventoryPage page, int placement, IItem item)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		if (item == null)
		{
			return Identity.None;
		}
		_ = item.Identity;
		Identity val = item.Identity;
		if ((int)((Identity)(ref val)).Type == 51017)
		{
			return item.Identity;
		}
		if (character == null || page == null)
		{
			return Identity.None;
		}
		val = default(Identity);
		_ = ((IEntity)page).Identity;
		Identity identity = ((IEntity)page).Identity;
		((Identity)(ref val)).Type = ((Identity)(ref identity)).Type;
		((Identity)(ref val)).Instance = placement;
		Identity val2 = val;
		Identity result = default(Identity);
		if (InventoryItemRules.TryEnsureMailForbiddenContainerIdentity(item, ((IEntity)character).Identity, val2, ref result))
		{
			return result;
		}
		return Identity.None;
	}
}
