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

            if (OrgClient.TryHandleCapturedOrgInfo(message, zoneClient))
            {
                return;
            }

            if (OrgClient.TryHandleCapturedCityControllerBankAdd(message, zoneClient))
            {
                return;
            }

            if (message.Command == OrgClientCommand.CityAdvantages)
            {
                SendCapturedCityAdvantages(message, zoneClient);
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

        private static void SendCapturedCityAdvantages(OrgClientMessage message, ZoneClient client)
        {
            if (client.Controller == null || client.Controller.Character == null)
            {
                client.Server.Info(
                    client,
                    "OrgClient CityAdvantages ignored target={0} unknown1={1} args={2} reason=no_character evidence=live_capture_20260622-093102",
                    message.Target,
                    message.Unknown1,
                    message.CommandArgs ?? string.Empty);
                return;
            }

            var character = client.Controller.Character;
            client.SendCompressed(
                new CityAdvantagesMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Advantages = CreateCapturedCityAdvantages()
                });

            client.Server.Info(
                client,
                "OrgClient CityAdvantages responded character={0} count=4 evidence=live_capture_20260622-093102 no_state_change=1",
                character.Identity);
        }

        private static CityAdvantage[] CreateCapturedCityAdvantages()
        {
            return new[]
                   {
                       new CityAdvantage { LowId = 254403, HighId = 254403, QualityLevel = 300, Unknown = 0 },
                       new CityAdvantage { LowId = 254387, HighId = 254387, QualityLevel = 300, Unknown = 0 },
                       new CityAdvantage { LowId = 254406, HighId = 254406, QualityLevel = 300, Unknown = 0 },
                       new CityAdvantage { LowId = 254395, HighId = 254395, QualityLevel = 300, Unknown = 0 }
                   };
        }
    }
}
