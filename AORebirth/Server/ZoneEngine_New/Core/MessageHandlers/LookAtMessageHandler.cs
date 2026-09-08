namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

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

            // ReturnInfo=1 means InfoRequest will carry the inspect packet.
            if (message.ReturnInfo == 1 || message.Target.Instance == 0)
                return;

            Playfield? playfield = player.Playfield;
            if (playfield == null)
                return;

            if (!playfield.GetRequiredService<DynelRegistry>().TryGet(message.Target, out Dynel? dynel)
                || dynel is not Character target)
                return;

            session.Send(target.BuildInfoPacket());
        }
    }
}
