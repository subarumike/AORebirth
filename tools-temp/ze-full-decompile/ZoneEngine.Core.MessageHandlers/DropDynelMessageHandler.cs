using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Vector;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class DropDynelMessageHandler : BaseMessageHandler<DropDynelMessage, DropDynelMessageHandler>
{
	public void Send(ICharacter character, Identity identity, Coordinate position)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<DropDynelMessage>)(object)this).Send(character, Filler(identity, position), false);
	}

	private static MessageDataFiller<DropDynelMessage> Filler(Identity identity, Coordinate position)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return delegate(DropDynelMessage x)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = identity;
			x.Position = ToMessagingVector(position);
		};
	}

	public IMSendAOtomationMessageToPlayfieldOthers CreateIM(Identity targetIdentity, Coordinate position)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		DropDynelMessage body = ((AbstractMessageHandler<DropDynelMessage>)(object)this).Create((ICharacter)null, Filler(targetIdentity, position));
		return new IMSendAOtomationMessageToPlayfieldOthers
		{
			Body = (MessageBody)(object)body,
			Identity = targetIdentity
		};
	}

	public DropDynelMessage Create(Identity identity, Coordinate position)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		return ((AbstractMessageHandler<DropDynelMessage>)(object)this).Create((ICharacter)null, Filler(identity, position));
	}

	private static Vector3 ToMessagingVector(Coordinate position)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Expected O, but got Unknown
		if (position == null)
		{
			return new Vector3();
		}
		return new Vector3
		{
			X = position.x,
			Y = position.y,
			Z = position.z
		};
	}
}
