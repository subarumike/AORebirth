using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.PacketHandlers;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class ZoneLoginMessageHandler : BaseMessageHandler<ZoneLoginMessage, ZoneLoginMessageHandler>
{
	protected override void Read(ZoneLoginMessage message, IZoneClient client)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		ZoneClient zoneClient = (ZoneClient)(object)client;
		PlayerController controller = new PlayerController((IZoneClient)(object)zoneClient);
		zoneClient.Controller = (IController)(object)controller;
		zoneClient.SessionLifecycle.BeginCharacterLoading();
		zoneClient.CreateCharacter(message.CharacterId);
		zoneClient.SendInitiateCompressionMessage((MessageBody)new InitiateCompressionMessage());
		((IInstancedEntity)client.Controller.Character).Playfield = zoneClient.Playfield;
		ClientConnected clientConnected = new ClientConnected();
		clientConnected.Read(message.CharacterId, zoneClient);
	}
}
