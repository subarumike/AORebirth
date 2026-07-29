namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

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

    #endregion

    /// <summary>
    /// Capture 20260726-sara-greene-vendor: GenericCmd Use Sarah (knubot shop cart)
    /// → ShopUpdate + Trade Open.
    /// </summary>
    internal sealed class CapturedAreteSarahGreeneVendorInteractionHandler
    {
        internal static readonly CapturedAreteSarahGreeneVendorInteractionHandler Default =
            new CapturedAreteSarahGreeneVendorInteractionHandler();

        private CapturedAreteSarahGreeneVendorInteractionHandler()
        {
        }

        internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            ICharacter character = client.Controller.Character;
            if (!this.TryOpenShop(character, target))
            {
                CapturedAreteSarahGreeneVendorRuntimeDefinition runtime;
                if (!CapturedAreteSarahGreeneVendorRuntimeRegistry.TryGet(target.Instance, out runtime))
                {
                    return false;
                }

                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            return true;
        }

        internal bool TryOpenShop(ICharacter character, Identity npcIdentity)
        {
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            // Capture: knubot Shop cart / Use Sarah (SimpleChar) only.
            if (npcIdentity.Type != IdentityType.CanbeAffected || npcIdentity.Instance == 0)
            {
                return false;
            }

            CapturedAreteSarahGreeneVendorRuntimeDefinition runtime;
            if (!CapturedAreteSarahGreeneVendorRuntimeRegistry.TryGet(npcIdentity.Instance, out runtime))
            {
                return false;
            }

            if (!CapturedAreteSarahGreeneVendorRuntimeRegistry.Same(runtime.NpcIdentity, npcIdentity))
            {
                return false;
            }

            if (!CapturedAreteSarahGreeneVendorRuntimeRegistry.Same(
                    runtime.PlayfieldIdentity,
                    character.Playfield.Identity))
            {
                return false;
            }

            if (runtime.VendorIdentity.Instance == 0)
            {
                return false;
            }

            Vendor vendor = Pool.Instance.GetObject<Vendor>(
                character.Playfield.Identity,
                runtime.VendorIdentity);
            if (vendor == null)
            {
                return false;
            }

            Event trade = vendor.Events.FirstOrDefault(candidate => candidate.EventType == EventType.OnTrade);
            if (trade != null)
            {
                trade.Perform(character, vendor);
            }

            VendingMachineFullUpdateMessageHandler.Default.Send(character, vendor);
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

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Sarah Greene shop open character="
                + character.Identity
                + " vendor="
                + vendor.Identity
                + " slots="
                + InventoryContainerRuntimeService.Default.GetVendorStandardInventoryPage(vendor).List().Count
                + " evidence="
                + CapturedAreteSarahGreeneVendorContentProvider.Evidence);
            return true;
        }
    }
}
