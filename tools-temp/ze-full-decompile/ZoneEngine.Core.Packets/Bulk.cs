using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.Messages;
using ZoneEngine.Core.InternalMessages;

namespace ZoneEngine.Core.Packets;

public static class Bulk
{
	public static IMSendAOtomationMessageBodiesToClient CreateIM(IZoneClient client, MessageBody[] messages)
	{
		return new IMSendAOtomationMessageBodiesToClient
		{
			Bodies = messages,
			client = client
		};
	}
}
