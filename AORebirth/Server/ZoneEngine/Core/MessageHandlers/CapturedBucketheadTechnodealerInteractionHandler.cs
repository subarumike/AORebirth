namespace ZoneEngine.Core.MessageHandlers
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Capture 20260723-114826: GenericCmd Use → ACK → ShopUpdate (46) → Trade Open.
    /// Client left-click also sends KnuBotOpenChatWindow (no dialogue on live portable dealer);
    /// open the same shop on that path. Never run OnTrade (99566 = 11× Shophash aborts).
    /// </summary>
    internal sealed class CapturedBucketheadTechnodealerInteractionHandler
    {
        internal static readonly CapturedBucketheadTechnodealerInteractionHandler Default =
            new CapturedBucketheadTechnodealerInteractionHandler();

        private CapturedBucketheadTechnodealerInteractionHandler()
        {
        }

        internal bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            ICharacter character = client.Controller.Character;
            if (character == null || character.Playfield == null || target.Instance == 0)
            {
                return false;
            }

            CapturedBucketheadTechnodealerRuntimeDefinition runtime;
            if (!this.TryResolveRuntime(character, target, out runtime))
            {
                return false;
            }

            Vendor vendor = this.ResolveVendor(character, runtime);
            if (vendor == null)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                ChatTextMessageHandler.Default.Send(
                    character,
                    "Buckethead Technodealer shop is unavailable (vendor missing).");
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Buckethead Technodealer shop open failed target=" + target
                    + " vendorIdentity=" + runtime.VendorIdentity);
                return true;
            }

            // Capture 20260723-114826: ACK first, then ShopUpdate, then Trade Open.
            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            this.SendShopOpen(character, vendor, runtime);
            return true;
        }

        /// <summary>
        /// Left-click / talk: client sends KnuBotOpenChatWindow (ZoneEngine log).
        /// Portable Buckethead has no dialogue — open shop instead.
        /// </summary>
        internal bool TryHandleOpenChatWindow(ICharacter character, Identity target)
        {
            if (character == null || character.Playfield == null || target.Instance == 0)
            {
                return false;
            }

            CapturedBucketheadTechnodealerRuntimeDefinition runtime;
            if (!this.TryResolveRuntime(character, target, out runtime))
            {
                return false;
            }

            Vendor vendor = this.ResolveVendor(character, runtime);
            if (vendor == null)
            {
                ChatTextMessageHandler.Default.Send(
                    character,
                    "Buckethead Technodealer shop is unavailable (vendor missing).");
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Buckethead Technodealer KnuBot shop open failed target=" + target
                    + " vendorIdentity=" + runtime.VendorIdentity);
                return true;
            }

            this.SendShopOpen(character, vendor, runtime);
            return true;
        }

        private void SendShopOpen(
            ICharacter character,
            Vendor vendor,
            CapturedBucketheadTechnodealerRuntimeDefinition runtime)
        {
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
                "Buckethead Technodealer shop open character="
                + character.Identity
                + " npc="
                + runtime.NpcIdentity
                + " vendor="
                + vendor.Identity
                + " slots="
                + InventoryContainerRuntimeService.Default.GetVendorStandardInventoryPage(vendor).List().Count
                + " evidence=20260723-114826");
        }

        private bool TryResolveRuntime(
            ICharacter character,
            Identity target,
            out CapturedBucketheadTechnodealerRuntimeDefinition runtime)
        {
            runtime = null;

            if (CapturedBucketheadTechnodealerRuntimeRegistry.TryGet(target.Instance, out runtime)
                && runtime != null
                && runtime.NpcIdentity.Instance == target.Instance
                && runtime.PlayfieldIdentity.Instance == character.Playfield.Identity.Instance)
            {
                return true;
            }

            if (target.Type == IdentityType.VendingMachine)
            {
                CapturedBucketheadTechnodealerRuntimeDefinition byOwner;
                if (CapturedBucketheadTechnodealerRuntimeRegistry.TryGetByOwner(
                        character.Identity.Instance,
                        out byOwner)
                    && byOwner != null
                    && byOwner.VendorIdentity.Instance == target.Instance
                    && byOwner.PlayfieldIdentity.Instance == character.Playfield.Identity.Instance)
                {
                    runtime = byOwner;
                    return true;
                }
            }

            CapturedBucketheadTechnodealerRuntimeDefinition owned;
            if (CapturedBucketheadTechnodealerRuntimeRegistry.TryGetByOwner(
                    character.Identity.Instance,
                    out owned)
                && owned != null
                && owned.PlayfieldIdentity.Instance == character.Playfield.Identity.Instance)
            {
                ICharacter npc = null;
                try
                {
                    npc = Pool.Instance.GetObject<ICharacter>(character.Playfield.Identity, target);
                }
                catch
                {
                }

                if (npc != null
                    && string.Equals(
                        npc.Name,
                        CapturedBucketheadTechnodealerContentProvider.DisplayName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    runtime = owned;
                    return true;
                }
            }

            runtime = null;
            return false;
        }

        private Vendor ResolveVendor(ICharacter character, CapturedBucketheadTechnodealerRuntimeDefinition runtime)
        {
            if (runtime == null)
            {
                return null;
            }

            if (runtime.Vendor != null)
            {
                return runtime.Vendor;
            }

            if (runtime.VendorIdentity.Instance == 0)
            {
                return null;
            }

            Vendor vendor = SummonedBucketheadTechnodealerRuntime.TryGetVendor(runtime.VendorIdentity);
            if (vendor != null)
            {
                return vendor;
            }

            try
            {
                return Pool.Instance.GetObject<Vendor>(character.Playfield.Identity, runtime.VendorIdentity);
            }
            catch
            {
                return null;
            }
        }
    }
}
