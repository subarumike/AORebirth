namespace ZoneEngine.Core.MessageHandlers
{
    using System;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public sealed class DoorStatusUpdateMessageHandler :
        BaseMessageHandler<DoorStatusUpdateMessage, DoorStatusUpdateMessageHandler>
    {
        public void SendStatus(ICharacter character, Identity door, bool isOpen)
        {
            if (character == null)
            {
                throw new ArgumentNullException("character");
            }

            if (door.Type != IdentityType.Door)
            {
                throw new ArgumentException("Door status target must be a door identity.", "door");
            }

            this.Send(
                character,
                message =>
                {
                    message.Identity = door;
                    message.Unknown = 0;
                    message.Unknown1 = 2;
                    message.Unknown2 = 0;
                    // Gamecode.dll DoorStatusUpdateIIR_t::PollStatus dispatches this
                    // exact member to the client DoorOpened/DoorClosed paths.
                    message.Unknown3 = isOpen ? (byte)1 : (byte)0;
                    message.Unknown4 = 0;
                    message.Unknown5 = 0;
                    message.Unknown6 = new Identity[0];
                });
        }
    }
}
