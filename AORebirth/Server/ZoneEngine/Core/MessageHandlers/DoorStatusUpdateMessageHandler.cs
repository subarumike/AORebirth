namespace ZoneEngine.Core.MessageHandlers
{
    using System;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;

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
                message => FillDoorStatus(message, door, isOpen));
        }

        /// <summary>
        /// Capture 20260823-171238 PF 4310 zone-in DoorStatusUpdate seq #26:
        /// MissionEntrance:C00010D6, Unknown1=2, Unknown3=0 (closed).
        /// Do not use IdentityType.Door or ACGEntrance (0xC7A1) here — client freezes on zone-in.
        /// </summary>
        internal void SendCapturedNascenceDungeonEntranceStatus(ICharacter character)
        {
            if (character == null)
            {
                throw new ArgumentNullException("character");
            }

            var entrance = new Identity
                           {
                               Type = IdentityType.MissionEntrance,
                               Instance = NascenceDungeon1Rules.AcgEntranceInstance
                           };
            this.Send(character, message => FillDoorStatus(message, entrance, false));
        }

        /// <summary>
        /// Capture 20260823-182854 PF 4310: MissionEntrance:C00110D6 (D2), closed.
        /// </summary>
        internal void SendCapturedNascenceDungeon2EntranceStatus(ICharacter character)
        {
            if (character == null)
            {
                throw new ArgumentNullException("character");
            }

            var entrance = new Identity
                           {
                               Type = IdentityType.MissionEntrance,
                               Instance = NascenceDungeon2Rules.AcgEntranceInstance
                           };
            this.Send(character, message => FillDoorStatus(message, entrance, false));
        }

        /// <summary>
        /// Capture 20260830-140240 PF 4311: MissionEntrance:C00010D7 (D3 Collapsed Temple), closed.
        /// </summary>
        internal void SendCapturedNascenceDungeon3EntranceStatus(ICharacter character)
        {
            if (character == null)
            {
                throw new ArgumentNullException("character");
            }

            var entrance = new Identity
                           {
                               Type = IdentityType.MissionEntrance,
                               Instance = NascenceDungeon3Rules.AcgEntranceInstance
                           };
            this.Send(character, message => FillDoorStatus(message, entrance, false));
        }

        /// <summary>
        /// Capture 20260830-143801 PF 4311: MissionEntrance:C00110D7 (D4 A Door), closed.
        /// </summary>
        internal void SendCapturedNascenceDungeon4EntranceStatus(ICharacter character)
        {
            if (character == null)
            {
                throw new ArgumentNullException("character");
            }

            var entrance = new Identity
                           {
                               Type = IdentityType.MissionEntrance,
                               Instance = NascenceDungeon4Rules.AcgEntranceInstance
                           };
            this.Send(character, message => FillDoorStatus(message, entrance, false));
        }

        private static void FillDoorStatus(DoorStatusUpdateMessage message, Identity door, bool isOpen)
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
        }
    }
}
