namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    public sealed class CharDCMoveMessageHandler : IMessageHandler<CharDCMoveMessage>
    {
        public Type MessageBodyType => typeof(CharDCMoveMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((CharDCMoveMessage)body, session);
        }

        public void Handle(CharDCMoveMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null)
                return;

            // A neighbor's announced move can come back on this session. That packet still
            // names the other character; applying it here is what makes the two clients bounce.
            if (message.Identity.Instance != 0
                && (message.Identity.Type != player.Identity.Type
                    || message.Identity.Instance != player.Identity.Instance))
                return;

            if (!player.Motor.Consume(message))
                return;

            message.Identity = player.Identity;
            // S2C CharDCMove uses unknown 0. The client default of 1 is a local direct-control
            // command, and forwarding it makes the other client apply the move to itself.
            message.Unknown = 0;
            player.Cell?.Announce(message, player);
        }
    }
}
