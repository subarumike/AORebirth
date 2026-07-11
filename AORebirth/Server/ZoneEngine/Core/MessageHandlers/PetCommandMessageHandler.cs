#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    using Utility;

    using ZoneEngine.Core;

    #endregion

    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class PetCommandMessageHandler : BaseMessageHandler<PetCommandMessage, PetCommandMessageHandler>
    {
        protected override void Read(PetCommandMessage message, IZoneClient client)
        {
            ICharacter owner = client.Controller != null ? client.Controller.Character : null;
            if (owner == null || message == null)
            {
                return;
            }

            // Capture 20260710-220653: Unknown2 is the command id, Unknown1=1 means all pets.
            int commandId = message.Unknown2;
            bool applyToAllPets = message.Unknown1 == 1;
            Identity petIdentity = this.ResolvePetIdentity(message);
            Identity rawCommandTarget = this.ResolveCommandTarget(message);
            PetCommandService.CommitHealTargetFromPacket(owner, petIdentity, rawCommandTarget);

            Identity commandTarget = rawCommandTarget;
            if (commandTarget.Instance == 0 && commandId != PetCommandService.CommandHeal)
            {
                commandTarget = owner.SelectedTarget;
            }

            if (commandId == PetCommandService.CommandHeal)
            {
                commandTarget = PetCommandService.ResolveHealCommandTarget(owner, petIdentity, rawCommandTarget);
                PetCommandService.SyncOwnerHealSelectedTarget(owner, commandTarget);
            }
            else if (PetCommandService.HasActiveHealCommand(owner)
                && commandTarget.Instance != 0)
            {
                PetCommandService.SyncOwnerHealSelectedTarget(owner, commandTarget);
            }

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "PetCommandMessage owner={0} pet={1} commandId={2} all={3} target={4} u1={5} u3={6} u4={7} idCount={8} name={9}",
                    owner.Identity,
                    petIdentity,
                    commandId,
                    applyToAllPets,
                    commandTarget,
                    message.Unknown1,
                    message.Unknown3,
                    message.Unknown4,
                    message.Identities == null ? 0 : message.Identities.Length,
                    message.Name ?? string.Empty));

            PetCommandService.HandlePetCommandMessage(
                client,
                owner,
                commandId,
                applyToAllPets,
                petIdentity,
                commandTarget);
        }

        private Identity ResolvePetIdentity(PetCommandMessage message)
        {
            if (message.Identities != null && message.Identities.Length > 0 && message.Identities[0].Instance != 0)
            {
                return message.Identities[0];
            }

            return Identity.None;
        }

        private Identity ResolveCommandTarget(PetCommandMessage message)
        {
            if (message.Identities != null && message.Identities.Length > 1 && message.Identities[1].Instance != 0)
            {
                return message.Identities[1];
            }

            return Identity.None;
        }
    }
}
