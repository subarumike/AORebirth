using AORebirth.Core.Components;
using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.Subway.Quests;
using ZoneEngine.Core.Thrak.Quests;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class KnuBotTradeMessageHandler : BaseMessageHandler<KnuBotTradeMessage, KnuBotTradeMessageHandler>
{
	protected override void Read(KnuBotTradeMessage message, IZoneClient client)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		if (!RexMarcusChainCoordinator.TryStageMarcusTradeItem(client.Controller.Character, message) && !FlintBioComQuestRuntime.TryStageAlexTradeItem(client.Controller.Character, message) && !SurveillanceUplinkQuestRuntime.TryStageBillTradeItem(client.Controller.Character, message) && !SurveillanceUplinkQuestRuntime.ShouldSuppressGenericBillTradeRemove(client.Controller.Character, message.Target) && !ThrakGardenKeyTradeAdapter.TryStageTradeItem(client.Controller.Character, message) && !ThrakGardenKeyTradeAdapter.IsThrakTradeNpc(client.Controller.Character, message.Target) && !WindcallerKarrecTradeAdapter.TryStageTradeItem(client.Controller.Character, message))
		{
			InventoryContainerRuntimeService.Default.HandleKnuBotTradeItemRemove(client, message);
		}
	}
}
