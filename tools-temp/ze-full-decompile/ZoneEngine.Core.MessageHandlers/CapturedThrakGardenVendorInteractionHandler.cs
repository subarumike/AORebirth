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
using ZoneEngine.Core.Thrak.Quests;

namespace ZoneEngine.Core.MessageHandlers;

internal sealed class CapturedThrakGardenVendorInteractionHandler
{
	internal static readonly CapturedThrakGardenVendorInteractionHandler Default = new CapturedThrakGardenVendorInteractionHandler();

	private CapturedThrakGardenVendorInteractionHandler()
	{
	}

	internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		if (!TryOpenShop(character, target, acknowledgeDeniedOnGate: true))
		{
			if (!CapturedThrakGardenVendorRuntimeRegistry.TryGet(((Identity)(ref target)).Instance, out var _))
			{
				return false;
			}
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(character, message);
			return true;
		}
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(character, message);
		return true;
	}

	internal bool TryOpenShop(ICharacter character, Identity npcIdentity, bool acknowledgeDeniedOnGate = false)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Expected O, but got Unknown
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return false;
		}
		if (!CapturedThrakGardenVendorRuntimeRegistry.TryGet(((Identity)(ref npcIdentity)).Instance, out var runtime))
		{
			return false;
		}
		if (!CapturedThrakGardenVendorRuntimeRegistry.Same(runtime.NpcIdentity, npcIdentity))
		{
			return false;
		}
		if (!CapturedThrakGardenVendorRuntimeRegistry.Same(runtime.PlayfieldIdentity, ((IEntity)((IInstancedEntity)character).Playfield).Identity))
		{
			return false;
		}
		if (runtime.Content != null && runtime.Content.RequiresCompletedGardenKeyQuest && !ThrakGardenKeyQuestRuntime.HasCompletedGardenKeyQuest(character))
		{
			return false;
		}
		Identity val = runtime.VendorIdentity;
		if (((Identity)(ref val)).Instance == 0)
		{
			return false;
		}
		Vendor @object = Pool.Instance.GetObject<Vendor>(((IEntity)((IInstancedEntity)character).Playfield).Identity, runtime.VendorIdentity);
		if (@object == null)
		{
			return false;
		}
		Event val2 = @object.Events.FirstOrDefault((Event candidate) => (int)candidate.EventType == 37);
		if (val2 == null)
		{
			return false;
		}
		val2.Perform(character, (IEntity)(object)@object);
		Identity identity = ((IEntity)character).Identity;
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)51047;
		((Identity)(ref val)).Instance = Pool.Instance.GetFreeInstance<TemporaryBag>(0, (IdentityType)51047);
		TemporaryBag tempBag = (character.ShoppingBag = new TemporaryBag(identity, val, ((IEntity)character).Identity, ((PooledObject)@object).Identity, 255));
		BaseMessageHandler<TradeMessage, TradeMessageHandler>.Default.Send(character, tempBag);
		return true;
	}
}
