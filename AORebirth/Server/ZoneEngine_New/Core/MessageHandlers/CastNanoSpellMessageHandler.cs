namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// Server→client cast-bar visual. Client cast requests arrive as
    /// <see cref="CharacterActionType.CastNano"/> (Parameter2 = nano id).
    /// Starting a cast from this inbound packet double-applies with CharacterAction.
    /// </summary>
    public sealed class CastNanoSpellMessageHandler : IMessageHandler<CastNanoSpellMessage>
    {
        public Type MessageBodyType => typeof(CastNanoSpellMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((CastNanoSpellMessage)body, session);
        }

        public void Handle(CastNanoSpellMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);
        }
    }
}
