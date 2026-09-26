namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Mail;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// Routes Mail Terminal traffic to <see cref="MailService"/>. Runs on the playfield tick
    /// thread via the inbound queue.
    /// </summary>
    public sealed class MailMessageHandler : IMessageHandler<MailMessage>
    {
        readonly MailService _mail;

        public MailMessageHandler(MailService mail)
        {
            _mail = mail ?? throw new ArgumentNullException(nameof(mail));
        }

        public Type MessageBodyType => typeof(MailMessage);

        public void Handle(MessageBody body, IZoneSession session)
            => Handle((MailMessage)body, session);

        public void Handle(MailMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player?.Playfield == null || player.IsDead || player.IsPersistenceQuarantined)
                return;

            _mail.Handle(player, message);
        }
    }
}
