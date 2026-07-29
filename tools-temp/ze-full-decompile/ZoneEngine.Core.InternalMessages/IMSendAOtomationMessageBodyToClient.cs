using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.Messages;

namespace ZoneEngine.Core.InternalMessages;

public class IMSendAOtomationMessageBodyToClient : InternalMessageBody
{
	public MessageBody Body;

	public IZoneClient client;
}
