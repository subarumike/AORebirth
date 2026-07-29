using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.KnuBot;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class KnuBotCloseChatWindowMessageHandler : BaseMessageHandler<KnuBotCloseChatWindowMessage, KnuBotCloseChatWindowMessageHandler>
{
	public override void Receive(MessageWrapper<KnuBotCloseChatWindowMessage> messageWrapper)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		((IClient)messageWrapper.Client).Server.Info((IClient)(object)messageWrapper.Client, "KnuBotClose target={0} marker={1} seconds={2} unknown3={3}", new object[4]
		{
			messageWrapper.MessageBody.Target,
			messageWrapper.MessageBody.Unknown1,
			messageWrapper.MessageBody.Seconds,
			messageWrapper.MessageBody.Unknown3
		});
		if (!ContentDrivenNpcDialogueRouter.TryHandleClose(messageWrapper.Client.Controller.Character, messageWrapper.MessageBody.Target))
		{
			ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)messageWrapper.Client.Controller.Character).Playfield).Identity, messageWrapper.MessageBody.Target);
			if (@object != null)
			{
				((NPCController)(object)((IDynel)@object).Controller).KnuBot.Answer(KnuBotOptionId.WindowClosed);
			}
		}
	}

	public void Send(ICharacter character, Identity knuBotIdentity, int secondsToClose = 3)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<KnuBotCloseChatWindowMessage>)(object)this).Send(character, FillClose(character, knuBotIdentity, secondsToClose), false);
	}

	private MessageDataFiller<KnuBotCloseChatWindowMessage> FillClose(ICharacter character, Identity knuBotIdentity, int secondsToClose)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return delegate(KnuBotCloseChatWindowMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Target = knuBotIdentity;
			((N3Message)x).Unknown = 0;
			x.Unknown1 = 2;
			x.Seconds = secondsToClose;
			x.Unknown3 = 0;
		};
	}
}
