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

    internal sealed class CapturedSubwayVendorInteractionHandler
    {
        internal static readonly CapturedSubwayVendorInteractionHandler Default =
            new CapturedSubwayVendorInteractionHandler();

        private CapturedSubwayVendorInteractionHandler()
        {
        }

        internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            CapturedSubwayVendorRuntimeDefinition runtime;
            if (!CapturedSubwayVendorRuntimeRegistry.TryGet(target.Instance, out runtime))
            {
                return false;
            }

            if (!CapturedSubwayVendorRuntimeRegistry.Same(runtime.NpcIdentity, target))
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (!CapturedSubwayVendorRuntimeRegistry.Same(
                    runtime.PlayfieldIdentity,
                    character.Playfield.Identity))
            {
                return false;
            }

            if (runtime.VendorIdentity.Instance == 0)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
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
