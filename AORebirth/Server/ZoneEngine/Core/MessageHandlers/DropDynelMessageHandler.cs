#region License

// Copyright (c) 2005-2014, CellAO Team

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Vector;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.InternalMessages;

    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public class DropDynelMessageHandler : BaseMessageHandler<DropDynelMessage, DropDynelMessageHandler>
    {
        public void Send(ICharacter character, Identity identity, Coordinate position)
        {
            this.Send(character, Filler(identity, position), false);
        }

        private static MessageDataFiller Filler(Identity identity, Coordinate position)
        {
            return x =>
            {
                x.Identity = identity;
                x.Position = ToMessagingVector(position);
            };
        }

        public IMSendAOtomationMessageToPlayfieldOthers CreateIM(Identity targetIdentity, Coordinate position)
        {
            DropDynelMessage message = this.Create(null, Filler(targetIdentity, position));
            return new IMSendAOtomationMessageToPlayfieldOthers { Body = message, Identity = targetIdentity };
        }

        public DropDynelMessage Create(Identity identity, Coordinate position)
        {
            return this.Create(null, Filler(identity, position));
        }

        private static SmokeLounge.AOtomation.Messaging.GameData.Vector3 ToMessagingVector(Coordinate position)
        {
            if (position == null)
            {
                return new SmokeLounge.AOtomation.Messaging.GameData.Vector3();
            }

            return new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                       {
                           X = position.x,
                           Y = position.y,
                           Z = position.z
                       };
        }
    }
}
