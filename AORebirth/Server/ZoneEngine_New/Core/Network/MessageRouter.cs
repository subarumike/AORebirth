namespace ZoneEngine_New.Core.Network
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.MessageHandlers;

    public interface IMessageRouter
    {
        void Route(Message message, IZoneSession session);
    }

    /// <summary>
    /// Routes inbound bodies by concrete type to registered <see cref="IMessageHandler"/>s.
    /// </summary>
    public sealed class MessageRouter : IMessageRouter
    {
        private readonly Dictionary<Type, IMessageHandler> _handlers = new();
        private readonly IZoneLogger _logger;

        public MessageRouter(IEnumerable<IMessageHandler> handlers, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(handlers);
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;

            foreach (IMessageHandler handler in handlers)
            {
                if (handler?.MessageBodyType == null)
                    continue;

                if (!_handlers.TryAdd(handler.MessageBodyType, handler))
                {
                    throw new InvalidOperationException(
                        "Duplicate message handler for " + handler.MessageBodyType.FullName);
                }
            }
        }

        public void Route(Message message, IZoneSession session)
        {
            if (message?.Body == null)
                return;

            Type bodyType = message.Body.GetType();
            if (!_handlers.TryGetValue(bodyType, out IMessageHandler? handler))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Deserialized but unhandled message type {0} character={1}",
                        bodyType.Name,
                        session?.Player?.Identity.Instance ?? 0));
                return;
            }

            handler.Handle(message.Body, session);
        }
    }
}
