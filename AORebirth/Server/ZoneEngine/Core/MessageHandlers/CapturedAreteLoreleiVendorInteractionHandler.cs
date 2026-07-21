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
    /// Capture 20260721-loralei: GenericCmd Use Lorelei → ShopUpdate + Trade Open (cookie stock).
    /// </summary>
    internal sealed class CapturedAreteLoreleiVendorInteractionHandler
    {
        internal static readonly CapturedAreteLoreleiVendorInteractionHandler Default =
            new CapturedAreteLoreleiVendorInteractionHandler();

        private CapturedAreteLoreleiVendorInteractionHandler()
        {
        }

        internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            ICharacter character = client.Controller.Character;
            if (!this.TryOpenShop(character, target))
            {
                CapturedAreteLoreleiVendorRuntimeDefinition runtime;
                if (!CapturedAreteLoreleiVendorRuntimeRegistry.TryGet(target.Instance, out runtime))
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

            // Capture 20260721-loralei: knubot Shop cart / Use Lorelei (SimpleChar) only.
            // Never fall back by playfield — that stole ICC Tech Supplies Use (lockpick vendor).
            if (npcIdentity.Type != IdentityType.CanbeAffected || npcIdentity.Instance == 0)
            {
                return false;
            }

            CapturedAreteLoreleiVendorRuntimeDefinition runtime;
            if (!CapturedAreteLoreleiVendorRuntimeRegistry.TryGet(npcIdentity.Instance, out runtime))
            {
                return false;
            }

            if (!CapturedAreteLoreleiVendorRuntimeRegistry.Same(runtime.NpcIdentity, npcIdentity))
            {
                return false;
            }

            if (!CapturedAreteLoreleiVendorRuntimeRegistry.Same(
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

            // Capture 20260721-loralei: ShopUpdate then Trade Open.
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
                "Lorelei shop open character="
                + character.Identity
                + " vendor="
                + vendor.Identity
                + " slots="
                + InventoryContainerRuntimeService.Default.GetVendorStandardInventoryPage(vendor).List().Count
                + " evidence="
                + CapturedAreteLoreleiVendorContentProvider.Evidence);
            return true;
        }
    }
}
