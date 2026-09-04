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

            player.Motor.Consume(message);
        }
    }
}
