using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class SocialActionCmdMessageHandler : BaseMessageHandler<SocialActionCmdMessage, SocialActionCmdMessageHandler>
{
	protected override void Read(SocialActionCmdMessage body, IZoneClient client)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Expected O, but got Unknown
		((IClient)client).Server.Info((IClient)(object)client, "SocialActionCmd action={0} unknown1={1} unknown2={2} unknown3={3} unknown5={4}", new object[5] { body.Action, body.Unknown1, body.Unknown2, body.Unknown3, body.Unknown5 });
		SocialActionCmdMessage body2 = new SocialActionCmdMessage
		{
			Identity = ((N3Message)body).Identity,
			Unknown = 0,
			Unknown1 = body.Unknown1,
			Unknown2 = body.Unknown2,
			Unknown3 = body.Unknown3,
			Unknown4 = 1,
			Unknown5 = body.Unknown5,
			Action = body.Action
		};
		((IInstancedEntity)client.Controller.Character).Playfield.Publish((object)new IMSendAOtomationMessageToPlayfield
		{
			Body = (MessageBody)(object)body2
		});
	}
}
