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
    public class AddPetMessageHandler : BaseMessageHandler<AddPetMessage, AddPetMessageHandler>
    {
        public void SendAddPet(ICharacter owner, Identity petIdentity)
        {
            this.Send(owner, this.AddPetFiller(owner, petIdentity));
        }

        private MessageDataFiller AddPetFiller(ICharacter owner, Identity petIdentity)
        {
            return x =>
            {
                x.Identity = owner.Identity;
                x.Unknown = 0;
                x.PetIdentity = petIdentity;
            };
        }
    }
}
