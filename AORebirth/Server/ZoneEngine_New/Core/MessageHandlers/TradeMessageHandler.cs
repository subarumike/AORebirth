namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Trade;

    /// <summary>
    /// Routes client trade window traffic to <see cref="TradeService"/>. Runs on the playfield tick
    /// thread via the inbound queue, so no extra synchronization is needed here.
    /// </summary>
    public sealed class TradeMessageHandler : IMessageHandler<TradeMessage>
    {
        public Type MessageBodyType => typeof(TradeMessage);

        public void Handle(MessageBody body, IZoneSession session)
            => Handle((TradeMessage)body, session);

        public void Handle(TradeMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player?.Playfield == null || player.IsDead)
                return;

            player.Playfield.GetRequiredService<TradeService>().Handle(player, message);
        }
    }
}
