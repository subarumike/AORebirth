namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    public sealed class CharInPlayMessageHandler : IMessageHandler<CharInPlayMessage>
    {
        public Type MessageBodyType => typeof(CharInPlayMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((CharInPlayMessage)body, session);
        }

        public void Handle(CharInPlayMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player?.Cell == null)
                return;

            player.Cell.Announce(
                new CharInPlayMessage
                {
                    Identity = player.Identity,
                    Unknown = 0x00
                });
        }
    }
}
