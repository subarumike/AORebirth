using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;

namespace ZoneEngine.Core.InternalMessages;

public class IMSendAOtomationMessageToPlayfieldOthers : InternalMessageBody
{
	public MessageBody Body;

	public Identity Identity;
}
