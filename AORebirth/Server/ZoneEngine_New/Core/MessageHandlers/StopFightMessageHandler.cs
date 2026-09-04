namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    public sealed class StopFightMessageHandler : IMessageHandler<StopFightMessage>
    {
        public Type MessageBodyType => typeof(StopFightMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((StopFightMessage)body, session);
        }

        public void Handle(StopFightMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null)
                return;

            player.SetFightingTarget(Identity.None);
            player.Cell?.Announce(
                new StopFightMessage
                {
                    Identity = player.Identity,
                    Unknown1 = message.Unknown1
                });
        }
    }
}
