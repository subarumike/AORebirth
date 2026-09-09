namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// Client asks to cast a nano. This only starts the cast bar; the buff lands when the bar
    /// finishes, and every gate is re-checked at that point.
    /// </summary>
    public sealed class CastNanoSpellMessageHandler : IMessageHandler<CastNanoSpellMessage>
    {
        public Type MessageBodyType => typeof(CastNanoSpellMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((CastNanoSpellMessage)body, session);
        }

        public void Handle(CastNanoSpellMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player?.Playfield == null)
                return;

            NanoRuntime.TryStartCast(player, message.NanoId, message.Target, DateTime.UtcNow);
        }
    }
}
