namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Client special-attack request: attacker, target and the special's stat id. The request is echoed back,
    /// then the special runs against the target.
    /// </summary>
    public sealed class CharSecSpecAttackMessageHandler : IMessageHandler<CharSecSpecAttackMessage>
    {
        public Type MessageBodyType => typeof(CharSecSpecAttackMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((CharSecSpecAttackMessage)body, session);
        }

        public void Handle(CharSecSpecAttackMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null || player.IsDead || player.IsPersistenceQuarantined
                || !ReferenceEquals(player.Session, session))
                return;

            Playfield? playfield = player.Playfield;
            if (playfield == null)
                return;

            session.Send(new CharSecSpecAttackMessage
            {
                Identity = player.Identity,
                Unknown = message.Unknown,
                Target = message.Target,
                Stat = message.Stat
            });

            if (!playfield.GetRequiredService<DynelRegistry>().TryGet(message.Target, out Dynel? dynel)
                || dynel is not Character target || target.IsDead)
                return;

            SpecialAttacks.Perform(player, target, (CharacterStat)message.Stat);
        }
    }
}
