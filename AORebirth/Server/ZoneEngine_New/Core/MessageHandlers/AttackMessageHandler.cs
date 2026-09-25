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
            if (player == null || player.IsDead || player.IsPersistenceQuarantined
                || !ReferenceEquals(player.Session, session))
                return;

            Playfield? playfield = player.Playfield;
            if (playfield == null)
                return;

            if (message.Target.Instance == 0)
            {
                ClearAttack(player);
                return;
            }

            Character? target = null;
            if (playfield.GetRequiredService<DynelRegistry>().TryGet(message.Target, out Dynel? dynel)
                && dynel is Character character)
                target = character;

            if (target != null && CombatRules.CanAttack(player, target))
            {
                player.StartFighting(target.Identity, message.Action);
                return;
            }

            if (target != null && CombatRules.IsPvpAttackBlocked(player, target))
                ClientFeedback.Send(player, "Feedback_PvpNotAllowedSinceYouAreNeutral");
            else
                ClientFeedback.Send(player, "Feedback_StartingAttackFailed");

            ClearAttack(player);
        }

        static void ClearAttack(Player player)
        {
            player.SetFightingTarget(Identity.None);
            player.Cell?.Announce(
                new AttackMessage
                {
                    Identity = player.Identity,
                    Target = Identity.None,
                    Action = 0
                });
        }
    }
}
