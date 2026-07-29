using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Network;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.MessageHandlers;

internal sealed class CapturedHoloDeckVendorInteractionHandler
{
	internal static readonly CapturedHoloDeckVendorInteractionHandler Default = new CapturedHoloDeckVendorInteractionHandler();

	private CapturedHoloDeckVendorInteractionHandler()
	{
	}

	internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Invalid comparison between Unknown and I4
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Expected O, but got Unknown
		if (client == null || message == null || (int)((Identity)(ref target)).Type != 51035)
		{
			return false;
		}
		if (!CapturedHoloDeckVendorRuntimeRegistry.TryGet(((Identity)(ref target)).Instance, out var runtime))
		{
			return false;
		}
		if (!CapturedHoloDeckVendorRuntimeRegistry.Same(runtime.VendorIdentity, target))
		{
			return false;
		}
		ICharacter character = client.Controller.Character;
		if (!CapturedHoloDeckVendorRuntimeRegistry.Same(runtime.PlayfieldIdentity, ((IEntity)((IInstancedEntity)character).Playfield).Identity))
		{
			return false;
		}
		Vendor @object = Pool.Instance.GetObject<Vendor>(((IEntity)((IInstancedEntity)character).Playfield).Identity, runtime.VendorIdentity);
		if (@object == null)
		{
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(character, message);
			return true;
		}
		Event val = @object.Events.FirstOrDefault((Event candidate) => (int)candidate.EventType == 37);
		if (val == null)
		{
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(character, message);
			return true;
		}
		val.Perform(character, (IEntity)(object)@object);
		Identity identity = ((IEntity)character).Identity;
		Identity val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)51047;
		((Identity)(ref val2)).Instance = Pool.Instance.GetFreeInstance<TemporaryBag>(0, (IdentityType)51047);
		TemporaryBag tempBag = (character.ShoppingBag = new TemporaryBag(identity, val2, ((IEntity)character).Identity, ((PooledObject)@object).Identity, 255));
		BaseMessageHandler<TradeMessage, TradeMessageHandler>.Default.Send(character, tempBag);
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
		return true;
	}
}
