#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public class BuffMessageHandler : BaseMessageHandler<BuffMessage, BuffMessageHandler>
    {
        public void SendAddNanoBuff(
            ICharacter character,
            int nanoId,
            bool announceToPlayfield = true)
        {
            this.Send(
                character,
                this.AddNanoBuffFiller(character, nanoId),
                announceToPlayfield);
        }

        public void SendRemoveNanoBuff(ICharacter character, int nanoId)
        {
            this.Send(character, this.RemoveNanoBuffFiller(character, nanoId));
        }

        private MessageDataFiller AddNanoBuffFiller(ICharacter character, int nanoId)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Action = 0;
                x.NanoProgram = new Identity
                {
                    Type = (IdentityType)character.Identity.Instance,
                    Instance = nanoId
                };
            };
        }

        private MessageDataFiller RemoveNanoBuffFiller(ICharacter character, int nanoId)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Action = 0;
                x.NanoProgram = new Identity
                {
                    Type = IdentityType.NanoProgram,
                    Instance = nanoId
                };
            };
        }
    }
}
