using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.Messages;

namespace ZoneEngine.Core.InternalMessages;

public class IMSendAOtomationMessageToClient : InternalMessageBody
{
	public IZoneClient client;

	public Message message;
}
