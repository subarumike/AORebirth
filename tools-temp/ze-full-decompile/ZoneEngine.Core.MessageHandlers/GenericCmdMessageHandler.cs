using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class GenericCmdMessageHandler : BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>
{
	protected override void Read(GenericCmdMessage message, IZoneClient client)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected I4, but got Unknown
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Expected I4, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		Identity val = ((message.Target != null && message.Target.Length != 0) ? message.Target[0] : Identity.None);
		((IClient)client).Server.Info((IClient)(object)client, "GenericCmd action={0}({1}) temp1={2} count={3} temp4={4} user={5} target={6}", new object[7]
		{
			message.Action,
			(int)message.Action,
			message.Temp1,
			message.Count,
			message.Temp4,
			message.User,
			val
		});
		GenericCmdAction action = message.Action;
		GenericCmdAction val2 = action;
		switch (val2 - 1)
		{
		case 0:
			break;
		case 1:
			break;
		case 2:
			if (((IInstancedEntity)client.Controller.Character).Playfield is Playfield playfield && !playfield.TryHandleGenericCmdUse(client, message, val))
			{
			}
			break;
		case 4:
			UseItemOnItemInteractionHandler.Default.TryHandle(client, message);
			break;
		case 3:
			break;
		}
	}

	public void Acknowledge(ICharacter character, GenericCmdMessage message, bool announceToPlayfield = false)
	{
		((AbstractMessageHandler<GenericCmdMessage>)(object)this).Send(character, Reply(character, message), announceToPlayfield);
	}

	public void AcknowledgeDenied(ICharacter character, GenericCmdMessage message, bool announceToPlayfield = false)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<GenericCmdMessage>)(object)this).Send(character, Reply(character, message, Identity.None, message.Temp4, 2), announceToPlayfield);
	}

	public void AcknowledgeWithTarget(ICharacter character, GenericCmdMessage message, Identity target, bool announceToPlayfield = false)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<GenericCmdMessage>)(object)this).Send(character, Reply(character, message, target), announceToPlayfield);
	}

	public void AcknowledgeCorpseUse(ICharacter character, GenericCmdMessage message, Identity corpse, bool announceToPlayfield = false)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<GenericCmdMessage>)(object)this).Send(character, Reply(character, message, corpse, 1), announceToPlayfield);
	}

	private MessageDataFiller<GenericCmdMessage> Reply(ICharacter character, GenericCmdMessage message)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		return Reply(character, message, Identity.None);
	}

	private MessageDataFiller<GenericCmdMessage> Reply(ICharacter character, GenericCmdMessage message, Identity targetOverride)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		return Reply(character, message, targetOverride, message.Temp4);
	}

	private MessageDataFiller<GenericCmdMessage> Reply(ICharacter character, GenericCmdMessage message, Identity targetOverride, int temp4)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		return Reply(character, message, targetOverride, temp4, 1);
	}

	private MessageDataFiller<GenericCmdMessage> Reply(ICharacter character, GenericCmdMessage message, Identity targetOverride, int temp4, int temp1)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(GenericCmdMessage x)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			Identity[] array = message.Target.ToList().ToArray();
			if (targetOverride != Identity.None && array.Length != 0)
			{
				array[0] = targetOverride;
			}
			((N3Message)x).Identity = ((N3Message)message).Identity;
			((N3Message)x).N3MessageType = ((N3Message)message).N3MessageType;
			x.Target = array;
			x.Temp1 = temp1;
			x.Count = message.Count;
			x.Action = message.Action;
			x.Temp4 = temp4;
			x.User = message.User;
			((N3Message)x).Unknown = 0;
		};
	}
}
