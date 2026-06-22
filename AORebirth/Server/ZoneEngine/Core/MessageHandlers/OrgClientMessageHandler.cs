namespace ZoneEngine.Core.MessageHandlers
{
    using AORebirth.Core.Components;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;
    using ZoneEngine.Core.PacketHandlers;

    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class OrgClientMessageHandler : BaseMessageHandler<OrgClientMessage, OrgClientMessageHandler>
    {
        protected override void Read(OrgClientMessage message, IZoneClient client)
        {
            ZoneClient zoneClient = client as ZoneClient;
            if (zoneClient == null)
            {
                return;
            }

            if (OrgClient.TryHandleCapturedCityControllerBankAdd(message, zoneClient))
            {
                return;
            }

            if (message.Command == OrgClientCommand.BankAdd)
            {
                client.Server.Info(
                    client,
                    "OrgClient BankAdd ignored target={0} unknown1={1} args={2} evidence_scope=private_city_compat",
                    message.Target,
                    message.Unknown1,
                    message.CommandArgs ?? string.Empty);
            }
        }
    }
}
