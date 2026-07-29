using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Subway.Quests;
using ZoneEngine.Core.Thrak.Quests;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class KnuBotFinishTradeMessageHandler : BaseMessageHandler<KnuBotFinishTradeMessage, KnuBotFinishTradeMessageHandler>
{
	public override void Receive(MessageWrapper<KnuBotFinishTradeMessage> messageWrapper)
	{
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		if (messageWrapper != null && messageWrapper.MessageBody != null && messageWrapper.Client != null && !WindcallerKarrecTradeAdapter.TryFinishTrade(messageWrapper.Client.Controller.Character, messageWrapper.MessageBody) && !ThrakGardenKeyTradeAdapter.TryFinishTrade(messageWrapper.Client.Controller.Character, messageWrapper.MessageBody) && !RexMarcusChainCoordinator.TryFinishMarcusTrade(messageWrapper.Client.Controller.Character, messageWrapper.MessageBody) && !FlintBioComQuestRuntime.TryFinishAlexTrade(messageWrapper.Client.Controller.Character, messageWrapper.MessageBody) && !SurveillanceUplinkQuestRuntime.TryFinishBillTrade(messageWrapper.Client.Controller.Character, messageWrapper.MessageBody))
		{
			ICharacter @object = Pool.Instance.GetObject<ICharacter>(messageWrapper.MessageBody.Target);
			if (@object != null && ((IDynel)@object).Controller is NPCController { KnuBot: not null, KnuBot: var knuBot })
			{
				KnuBotFinishTradeMessage messageBody = messageWrapper.MessageBody;
				knuBot.FinishTrade(messageBody.Amount, messageBody.Decline != 0);
			}
		}
	}
}
