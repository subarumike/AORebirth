namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// Non-generic registration surface for the message router.
    /// </summary>
    public interface IMessageHandler
    {
        Type MessageBodyType { get; }

        void Handle(MessageBody body, IZoneSession session);
    }

    /// <summary>
    /// Typed inbound handler. Implement this; router discovers via <see cref="IMessageHandler"/>.
    /// </summary>
    public interface IMessageHandler<TBody> : IMessageHandler
        where TBody : MessageBody
    {
        void Handle(TBody body, IZoneSession session);
    }
}
