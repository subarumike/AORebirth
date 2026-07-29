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

internal sealed class CapturedSubwayVendorInteractionHandler
{
	internal static readonly CapturedSubwayVendorInteractionHandler Default = new CapturedSubwayVendorInteractionHandler();

	private CapturedSubwayVendorInteractionHandler()
	{
	}

	internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Expected O, but got Unknown
		if (!CapturedSubwayVendorRuntimeRegistry.TryGet(((Identity)(ref target)).Instance, out var runtime))
		{
			return false;
		}
		if (!CapturedSubwayVendorRuntimeRegistry.Same(runtime.NpcIdentity, target))
		{
			return false;
		}
		ICharacter character = client.Controller.Character;
		if (!CapturedSubwayVendorRuntimeRegistry.Same(runtime.PlayfieldIdentity, ((IEntity)((IInstancedEntity)character).Playfield).Identity))
		{
			return false;
		}
		Identity val = runtime.VendorIdentity;
		if (((Identity)(ref val)).Instance == 0)
		{
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(character, message);
			return true;
		}
		Vendor @object = Pool.Instance.GetObject<Vendor>(((IEntity)((IInstancedEntity)character).Playfield).Identity, runtime.VendorIdentity);
		if (@object == null)
		{
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(character, message);
			return true;
		}
		Event val2 = @object.Events.FirstOrDefault((Event candidate) => (int)candidate.EventType == 37);
		if (val2 == null)
		{
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(character, message);
			return true;
		}
		val2.Perform(character, (IEntity)(object)@object);
		Identity identity = ((IEntity)character).Identity;
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)51047;
		((Identity)(ref val)).Instance = Pool.Instance.GetFreeInstance<TemporaryBag>(0, (IdentityType)51047);
		TemporaryBag tempBag = (character.ShoppingBag = new TemporaryBag(identity, val, ((IEntity)character).Identity, ((PooledObject)@object).Identity, 255));
		BaseMessageHandler<TradeMessage, TradeMessageHandler>.Default.Send(character, tempBag);
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
		return true;
	}
}
