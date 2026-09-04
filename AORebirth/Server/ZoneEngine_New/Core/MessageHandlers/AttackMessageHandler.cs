namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    public sealed class AttackMessageHandler : IMessageHandler<AttackMessage>
    {
        public Type MessageBodyType => typeof(AttackMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((AttackMessage)body, session);
        }

        public void Handle(AttackMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null || player.IsDead)
                return;

            Playfield? playfield = player.Playfield;
            if (playfield == null)
                return;

            Character? target = null;
            if (message.Target.Instance != 0
                && playfield.GetRequiredService<DynelRegistry>().TryGet(message.Target, out Dynel? dynel)
                && dynel is Character character
                && !character.IsDead
                && character.Identity.Instance != player.Identity.Instance)
            {
                target = character;
            }

            if (target == null)
            {
                player.SetFightingTarget(Identity.None);
                player.Cell?.Announce(
                    new AttackMessage
                    {
                        Identity = player.Identity,
                        Target = Identity.None,
                        Action = 0
                    });
                return;
            }

            player.SetFightingTarget(target.Identity);
            player.ResetAllWeaponAttacks();
            player.Cell?.Announce(
                new AttackMessage
                {
                    Identity = player.Identity,
                    Target = target.Identity,
                    Action = message.Action
                });
        }
    }
}
