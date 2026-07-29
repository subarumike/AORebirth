using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.Messages;

namespace ZoneEngine.Core.InternalMessages;

public class IMSendAOtomationMessageBodiesToClient : InternalMessageBody
{
	public MessageBody[] Bodies;

	public IZoneClient client;
}
