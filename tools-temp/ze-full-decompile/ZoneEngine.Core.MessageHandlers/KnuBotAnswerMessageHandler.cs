using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class KnuBotAnswerMessageHandler : BaseMessageHandler<KnuBotAnswerMessage, KnuBotAnswerMessageHandler>
{
	public override void Receive(MessageWrapper<KnuBotAnswerMessage> messageWrapper)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		((IClient)messageWrapper.Client).Server.Info((IClient)(object)messageWrapper.Client, "KnuBotAnswer target={0} answer={1} unknown={2}", new object[3]
		{
			messageWrapper.MessageBody.Target,
			messageWrapper.MessageBody.Answer,
			messageWrapper.MessageBody.Unknown1
		});
		if (!ContentDrivenNpcDialogueRouter.TryHandleAnswer(messageWrapper.Client.Controller.Character, messageWrapper.MessageBody.Target, messageWrapper.MessageBody.Answer))
		{
			ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)messageWrapper.Client.Controller.Character).Playfield).Identity, messageWrapper.MessageBody.Target);
			if (@object != null)
			{
				((NPCController)(object)((IDynel)@object).Controller).KnuBot.Answer(messageWrapper.MessageBody.Answer);
			}
		}
	}
}
