namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    public sealed class LookAtMessageHandler : IMessageHandler<LookAtMessage>
    {
        public Type MessageBodyType => typeof(LookAtMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((LookAtMessage)body, session);
        }

        public void Handle(LookAtMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null)
                return;

            player.Target = message.Target;

            // TODO: Update Quests
        }
    }
}
