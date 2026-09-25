namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// Client agg/def slider. Writes only <see cref="CharacterStat.AggDef"/>,
    /// clamped to <see cref="NanoDelayCalculator.AggDefMin"/>..<see cref="NanoDelayCalculator.AggDefMax"/>.
    /// </summary>
    public sealed class SetStatMessageHandler : IMessageHandler<SetStatMessage>
    {
        public Type MessageBodyType => typeof(SetStatMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((SetStatMessage)body, session);
        }

        public void Handle(SetStatMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null || !ReferenceEquals(player.Session, session) || player.IsPersistenceQuarantined
                || message.Identity != player.Identity || message.Stat != CharacterStat.AggDef)
                return;

            int clamped = Math.Clamp(message.Value, NanoDelayCalculator.AggDefMin, NanoDelayCalculator.AggDefMax);
            int before = player.Stats.GetOrZero(CharacterStat.AggDef, StatDetail.Base);
            bool changed = before != clamped;
            if (changed)
                player.Stats.Set(CharacterStat.AggDef, clamped, StatDetail.Base, dirty: true);

            if (player.Playfield != null)
                player.FlushDirtyStats();

            // Owner still needs the clamped value when locality did not announce it.
            if ((changed && player.Playfield == null) || (!changed && message.Value != clamped))
                session.Send(AggDefStat(player, clamped));
        }

        static StatMessage AggDefStat(Player player, int value)
            => new()
            {
                Identity = player.Identity,
                Stats =
                [
                    new GameTuple<CharacterStat, uint>
                    {
                        Value1 = CharacterStat.AggDef,
                        Value2 = unchecked((uint)value)
                    }
                ]
            };
    }
}
