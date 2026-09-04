namespace ZoneEngine_New.Core.Network
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.MessageHandlers;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Routes parsed inbound messages to connection handlers or playfield inbound queues.
    /// Does not mutate game state.
    /// </summary>
    public sealed class ZoneMessageDispatcher
    {
        private static readonly HashSet<Type> ConnectionScoped = [typeof(ZoneLoginMessage)];

        private readonly ZoneLoginHandler _login;
        private readonly IZoneLogger _logger;

        public ZoneMessageDispatcher(ZoneLoginHandler login, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(login);
            ArgumentNullException.ThrowIfNull(logger);

            _login = login;
            _logger = logger;
        }

        public void Dispatch(Message message, IZoneSession session)
        {
            if (message?.Body == null)
                return;

            MessageBody body = message.Body;
            if (ConnectionScoped.Contains(body.GetType()))
            {
                _login.HandleAsync(body, session);
                return;
            }

            if (session.State != SessionState.InPlay || session.Player?.Playfield is not Playfield playfield)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Dropped inbound {0}: state={1} hasPlayer={2} hasPlayfield={3}",
                        body.GetType().Name,
                        session.State,
                        session.Player != null,
                        session.Player?.Playfield != null));
                return;
            }

            playfield.TryEnqueue(
                new GameplayInboundItem
                {
                    Session = session,
                    Body = body
                });
        }
    }
}
