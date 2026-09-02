namespace ZoneEngine.Core.MessageHandlers
{
    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    /// <summary>
    /// Capture 20260902-073932 / 080839 leader + Pandemonium 071644 member:
    /// server→client Raid ack (Identity=self) after Convert to Raid.
    /// </summary>
    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public class RaidMessageHandler : BaseMessageHandler<RaidMessage, RaidMessageHandler>
    {
        public void Send(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            this.Send(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Unknown1 = 0;
                },
                false);
        }
    }
}
