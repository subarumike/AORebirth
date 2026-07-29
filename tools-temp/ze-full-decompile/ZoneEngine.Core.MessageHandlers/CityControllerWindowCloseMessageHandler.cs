using AORebirth.Core.Components;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class CityControllerWindowCloseMessageHandler : BaseMessageHandler<CityControllerWindowCloseMessage, CityControllerWindowCloseMessageHandler>
{
	public override void Receive(MessageWrapper<CityControllerWindowCloseMessage> messageWrapper)
	{
		CityControllerInteractionHandler.Default.TryHandleWindowClose(messageWrapper);
	}
}
