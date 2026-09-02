namespace ZoneEngine.Core.MessageHandlers
{
    using AORebirth.Core.Components;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Controllers;

    /// <summary>
    /// Capture 20260902-073932: RaidCmd Command=1 (Convert to Raid).
    /// </summary>
    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class RaidCmdMessageHandler : BaseMessageHandler<RaidCmdMessage, RaidCmdMessageHandler>
    {
        protected override void Read(RaidCmdMessage message, IZoneClient client)
        {
            if (client?.Controller?.Character == null)
            {
                return;
            }

            if (message.Command != 1)
            {
                ChatTextMessageHandler.Default.Send(
                    client.Controller.Character,
                    "Unsupported raid command.");
                return;
            }

            TeamRuntime.ConvertToRaid(client.Controller.Character);
        }
    }
}
