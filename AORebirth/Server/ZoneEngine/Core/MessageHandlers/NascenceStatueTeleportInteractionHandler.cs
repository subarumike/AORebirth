namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    public sealed class NascenceStatueTeleportInteractionHandler
    {
        public static readonly NascenceStatueTeleportInteractionHandler Default =
            new NascenceStatueTeleportInteractionHandler();

        private NascenceStatueTeleportInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            ICharacter character = client.Controller.Character;
            if (character == null
                || character.Playfield == null
                || target == null
                || target.Type != IdentityType.Terminal)
            {
                return false;
            }

            int sourcePlayfieldId = character.Playfield.Identity.Instance;
            NascenceGardenPassageRoute route;
            if (!NascenceStatueTeleportCatalog.TryGetGardenPassageRoute(
                    sourcePlayfieldId,
                    target.Instance,
                    out route))
            {
                return false;
            }

            this.TeleportCharacter(
                client,
                character,
                message,
                route.DestinationPlayfieldId,
                route.DestinationX,
                route.DestinationY,
                route.DestinationZ,
                "NascenceGardenPassage",
                target,
                route.Evidence);

            return true;
        }

        public bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
        {
            if (UseItemOnItemInteractionRules.ResolveRouteMode(message.Action)
                != UseItemOnItemInteractionRouteMode.UseItemOnItem)
            {
                return false;
            }

            if (message.Target == null
                || message.Target.Length < 2
                || message.Target[1] == null
                || message.Target[1].Type != IdentityType.Terminal)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            int sourcePlayfieldId = character.Playfield.Identity.Instance;
            Identity terminalTarget = message.Target[1];
            if (!NascenceStatueTeleportCatalog.IsNascenceZonePlayfield(sourcePlayfieldId))
            {
                return false;
            }

            if (!this.IsCapturedZoneThrakStatueTerminal(sourcePlayfieldId, terminalTarget)
                && !this.IsThrakStatueTerminalByTemplate(character, terminalTarget))
            {
                return false;
            }

            IItem sourceItem = this.TryResolveSourceItem(character, message);
            if (sourceItem == null
                || !NascenceStatueTeleportCatalog.IsReturnKeyItemTemplate(sourceItem.LowID))
            {
                return false;
            }

            int gardenPlayfieldId = NascenceStatueTeleportCatalog.ResolveReturnGardenPlayfieldId(
                character.Stats[StatIds.otunredeemed].Value);
            float gardenX;
            float gardenY;
            float gardenZ;
            NascenceStatueTeleportCatalog.ResolveReturnGardenPosition(
                gardenPlayfieldId,
                out gardenX,
                out gardenY,
                out gardenZ);

            this.TeleportCharacter(
                client,
                character,
                message,
                gardenPlayfieldId,
                gardenX,
                gardenY,
                gardenZ,
                "NascenceZoneThrakStatue",
                terminalTarget,
                "20260716-nascense-statues UseItemOnItem insignia=" + sourceItem.LowID);

            return true;
        }

        private bool IsCapturedZoneThrakStatueTerminal(int playfieldId, Identity terminalTarget)
        {
            return NascenceStatueTeleportCatalog.IsZoneThrakStatueTerminal(
                playfieldId,
                terminalTarget.Instance);
        }

        private bool IsThrakStatueTerminalByTemplate(ICharacter character, Identity terminalTarget)
        {
            StaticDynel staticDynel =
                Pool.Instance.GetObject<StaticDynel>(
                    character.Playfield.Identity,
                    terminalTarget);
            return staticDynel != null
                   && staticDynel.Template != null
                   && NascenceStatueTeleportCatalog.IsThrakStatueTemplate(staticDynel.Template.ID);
        }

        private IItem TryResolveSourceItem(ICharacter character, GenericCmdMessage message)
        {
            try
            {
                return Pool.Instance.GetObject<IInventoryPage>(
                           new Identity
                           {
                               Type = (IdentityType)character.Identity.Instance,
                               Instance = (int)message.Target[0].Type
                           })[message.Target[0].Instance];
            }
            catch
            {
                return null;
            }
        }

        private void TeleportCharacter(
            IZoneClient client,
            ICharacter character,
            GenericCmdMessage message,
            int destinationPlayfieldId,
            float destinationX,
            float destinationY,
            float destinationZ,
            string routeKind,
            Identity target,
            string evidence)
        {
            character.StopMovement();
            character.Stats[StatIds.externaldoorinstance].BaseValue = 0;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = 0;

            Dynel dynel = character as Dynel;
            if (dynel == null)
            {
                return;
            }

            var destination = new Coordinate(destinationX, destinationY, destinationZ);
            character.Playfield.Teleport(
                dynel,
                destination,
                character.Heading,
                new Identity { Type = IdentityType.Playfield, Instance = destinationPlayfieldId });

            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            client.Server.Info(
                client,
                "Nascence statue teleport handled char={0} target={1} sourcePf={2} destPf={3} dest=({4:F3},{5:F3},{6:F3}) route={7} evidence={8}",
                character.Identity,
                target,
                character.Playfield.Identity.Instance,
                destinationPlayfieldId,
                destinationX,
                destinationY,
                destinationZ,
                routeKind,
                evidence);
        }
    }
}
