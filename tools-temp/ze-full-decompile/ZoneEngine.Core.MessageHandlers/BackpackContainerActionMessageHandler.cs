using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class BackpackContainerActionMessageHandler : BaseMessageHandler<ActionMessage, BackpackContainerActionMessageHandler>
{
	private const int OpenActionIdentity = 100;

	private const int CloseActionIdentity = 102;

	public void SendOpen(ICharacter character, Identity containerIdentity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		Send(character, containerIdentity, 0, 100);
	}

	public void SendClose(ICharacter character, Identity containerIdentity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		Send(character, containerIdentity, 1, 102);
	}

	private void Send(ICharacter character, Identity containerIdentity, byte unknown, int actionIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		((IDynel)character).Send((MessageBody)new ActionMessage
		{
			Identity = containerIdentity,
			Unknown = unknown,
			ActionCode = 1,
			ActionIdentity = actionIdentity,
			Target = ((IEntity)character).Identity
		}, false);
	}
}
