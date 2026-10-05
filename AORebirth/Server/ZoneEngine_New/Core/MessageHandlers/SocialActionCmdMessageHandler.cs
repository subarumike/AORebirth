namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// Client emote request. Relayed to everyone who can see the player, the player included.
    /// </summary>
    public sealed class SocialActionCmdMessageHandler : IMessageHandler<SocialActionCmdMessage>
    {
        public Type MessageBodyType => typeof(SocialActionCmdMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((SocialActionCmdMessage)body, session);
        }

        public void Handle(SocialActionCmdMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null)
                return;

            if (!player.TryRelaySocialAction(message))
                return;

            // Sleep and lounge are postures, not one-shot emotes: the client leaves them with
            // CharacterAction StandUp, so the motor holds the state until then (like sneak).
            switch (message.Action)
            {
                case SocialAction.RelaxingSleep:
                    player.Motor.ApplyAction(MovementAction.SwitchToSleep);
                    break;

                case SocialAction.RelaxingLounge:
                    player.Motor.ApplyAction(MovementAction.SwitchToLounge);
                    break;
            }
        }
    }
}
