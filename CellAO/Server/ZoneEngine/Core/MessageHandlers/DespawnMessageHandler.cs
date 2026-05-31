#region License

// Copyright (c) 2005-2014, CellAO Team

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    using CellAO.Core.Components;
    using CellAO.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public class DespawnMessageHandler : BaseMessageHandler<DespawnMessage, DespawnMessageHandler>
    {
        public void Send(ICharacter character, Identity identity)
        {
            this.Send(character, Filler(identity), false);
        }

        private static MessageDataFiller Filler(Identity identity)
        {
            return x =>
            {
                x.Identity = identity;
                x.Unknown = 1;
            };
        }

        public DespawnMessage Create(Identity identity)
        {
            return this.Create(null, Filler(identity));
        }
    }
}
