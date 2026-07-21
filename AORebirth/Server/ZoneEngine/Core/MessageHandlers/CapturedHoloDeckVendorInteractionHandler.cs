namespace ZoneEngine.Core.MessageHandlers
{
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Network;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Direct Use on freestanding Holodeck VendingMachine (capture 20260719-155043).
    /// </summary>
    internal sealed class CapturedHoloDeckVendorInteractionHandler
    {
        internal static readonly CapturedHoloDeckVendorInteractionHandler Default =
            new CapturedHoloDeckVendorInteractionHandler();

        private CapturedHoloDeckVendorInteractionHandler()
        {
        }

        internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null
                || message == null
                || target == null
                || target.Type != IdentityType.VendingMachine)
            {
                return false;
            }

            CapturedHoloDeckVendorRuntimeDefinition runtime;
            if (!CapturedHoloDeckVendorRuntimeRegistry.TryGet(target.Instance, out runtime))
            {
                return false;
            }

            if (!CapturedHoloDeckVendorRuntimeRegistry.Same(runtime.VendorIdentity, target))
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (!CapturedHoloDeckVendorRuntimeRegistry.Same(
                    runtime.PlayfieldIdentity,
                    character.Playfield.Identity))
            {
                return false;
            }

            Vendor vendor = Pool.Instance.GetObject<Vendor>(
                character.Playfield.Identity,
                runtime.VendorIdentity);
            if (vendor == null)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            Event trade = vendor.Events.FirstOrDefault(candidate => candidate.EventType == EventType.OnTrade);
            if (trade == null)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            trade.Perform(character, vendor);
            var temporaryBag =
                new TemporaryBag(
                    character.Identity,
                    new Identity
                    {
                        Type = IdentityType.TempBag,
                        Instance = Pool.Instance.GetFreeInstance<TemporaryBag>(0, IdentityType.TempBag)
                    },
                    character.Identity,
                    vendor.Identity);
            character.ShoppingBag = temporaryBag;
            TradeMessageHandler.Default.Send(character, temporaryBag);
            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            return true;
        }
    }
}
