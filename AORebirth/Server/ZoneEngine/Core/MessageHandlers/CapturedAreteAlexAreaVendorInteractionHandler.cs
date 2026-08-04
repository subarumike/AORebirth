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

    using Utility;

    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Direct Use on freestanding Arete Alex-area VendingMachines
    /// (capture 20260801-215330 / 20260721-lockpick: ICC Tech Supplies 12E77208).
    /// </summary>
    internal sealed class CapturedAreteAlexAreaVendorInteractionHandler
    {
        internal static readonly CapturedAreteAlexAreaVendorInteractionHandler Default =
            new CapturedAreteAlexAreaVendorInteractionHandler();

        private CapturedAreteAlexAreaVendorInteractionHandler()
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

            CapturedAreteAlexAreaVendorRuntimeDefinition runtime;
            if (!CapturedAreteAlexAreaVendorRuntimeRegistry.TryGet(target.Instance, out runtime))
            {
                return false;
            }

            if (!CapturedAreteAlexAreaVendorRuntimeRegistry.Same(runtime.VendorIdentity, target))
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (!CapturedAreteAlexAreaVendorRuntimeRegistry.Same(
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
            if (trade != null)
            {
                trade.Perform(character, vendor);
            }

            // Capture 20260801-215330 / 20260721-lockpick: ShopUpdate then Trade Open (no VMFU on Use).
            ShopUpdateMessageHandler.Default.Send(
                character,
                vendor,
                InventoryContainerRuntimeService.Default.GetVendorStandardInventoryPage(vendor));

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

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Alex-area freestanding shop open name="
                + runtime.DisplayName
                + " character="
                + character.Identity
                + " vendor="
                + vendor.Identity
                + " evidence=20260801-215330");
            return true;
        }
    }
}
