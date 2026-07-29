namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Playfields;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    /// <summary>
    /// Attaches capture-backed general store / weapons shop to Antonio Stacklund (PF 6553).
    /// Capture 20260726-Antonio-Stacklund: Use Antonio (shop cart) → ShopUpdate 12E7720D.
    /// </summary>
    internal sealed class CapturedAreteAntonioStacklundVendorRuntimeService
    {
        private readonly List<Vendor> capturedVendors = new List<Vendor>();

        internal void Attach(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            if (playfield == null
                || dynelRegistry == null
                || playfieldIdentity.Instance != CapturedAreteAntonioStacklundVendorContentProvider.AreteLandingPlayfieldId
                || CapturedAreteAntonioStacklundVendorRuntimeRegistry.ContainsPlayfield(playfieldIdentity))
            {
                return;
            }

            ICharacter antonio = null;
            foreach (ICharacter character in dynelRegistry.Characters())
            {
                if (character == null)
                {
                    continue;
                }

                if (character.Identity.Instance == CapturedAreteAntonioStacklundVendorContentProvider.SourceNpcInstance
                    || string.Equals(
                        character.Name,
                        CapturedAreteAntonioStacklundVendorContentProvider.DisplayName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    antonio = character;
                    break;
                }
            }

            if (antonio == null)
            {
                return;
            }

            Vendor vendor = this.TryCreateVendor(playfield, playfieldIdentity, antonio);
            Identity vendorIdentity = vendor == null ? Identity.None : vendor.Identity;
            if (vendor != null)
            {
                dynelRegistry.Register(vendor);
                this.capturedVendors.Add(vendor);
            }

            CapturedAreteAntonioStacklundVendorRuntimeRegistry.Register(
                new CapturedAreteAntonioStacklundVendorRuntimeDefinition(
                    playfieldIdentity,
                    antonio.Identity,
                    vendorIdentity));

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Antonio Stacklund Antonio vendor attached npc="
                + antonio.Identity
                + " vendor="
                + vendorIdentity
                + " stockRows="
                + CapturedAreteAntonioStacklundVendorContentProvider.Stock.Count
                + " evidence="
                + CapturedAreteAntonioStacklundVendorContentProvider.Evidence);
        }

        internal void Clear(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
        {
            CapturedAreteAntonioStacklundVendorRuntimeRegistry.RemoveForPlayfield(playfieldIdentity);
            foreach (Vendor vendor in this.capturedVendors)
            {
                dynelRegistry.Unregister(vendor.Identity);
                Pool.Instance.RemoveObject(vendor);
            }

            this.capturedVendors.Clear();
        }

        private Vendor TryCreateVendor(
            Playfield playfield,
            Identity playfieldIdentity,
            ICharacter character)
        {
            Vendor vendor = null;
            try
            {
                int captureTemplateId = CapturedAreteAntonioStacklundVendorContentProvider.CaptureVendorTemplateId;
                int templateId = captureTemplateId;
                if (!ItemLoader.ItemList.ContainsKey(templateId)
                    || ItemNamesDao.Instance.Get(templateId) == null)
                {
                    if (!ItemLoader.ItemList.ContainsKey(
                            CapturedAreteAntonioStacklundVendorContentProvider.RuntimeVendorTemplateFallbackId))
                    {
                        throw new InvalidOperationException(
                            "missing vendor template item "
                            + captureTemplateId
                            + " and fallback "
                            + CapturedAreteAntonioStacklundVendorContentProvider.RuntimeVendorTemplateFallbackId);
                    }

                    templateId = CapturedAreteAntonioStacklundVendorContentProvider.RuntimeVendorTemplateFallbackId;
                }

                var items = new List<KeyValuePair<int, Item>>();
                foreach (CapturedAreteAlexAreaVendorStockDefinition stock in
                    CapturedAreteAntonioStacklundVendorContentProvider.Stock)
                {
                    if (!ItemLoader.ItemList.ContainsKey(stock.LowId)
                        || !ItemLoader.ItemList.ContainsKey(stock.HighId))
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Engine,
                            "Antonio Stacklund vendor skip missing stock low="
                            + stock.LowId
                            + " high="
                            + stock.HighId);
                        continue;
                    }

                    items.Add(
                        new KeyValuePair<int, Item>(
                            stock.Slot,
                            new Item(stock.Quality, stock.LowId, stock.HighId)));
                }

                if (items.Count == 0)
                {
                    throw new InvalidOperationException("no stock items resolved");
                }

                var identity = new Identity
                               {
                                   Type = IdentityType.VendingMachine,
                                   Instance =
                                       Pool.Instance.GetFreeInstance<Vendor>(
                                           0x70000000,
                                           IdentityType.VendingMachine)
                               };
                vendor = new Vendor(playfieldIdentity, identity, templateId);
                vendor.Name = CapturedAreteAntonioStacklundVendorContentProvider.DisplayName;
                vendor.NpcIdentity = character.Identity;
                Character concreteNpc = character as Character;
                if (concreteNpc != null)
                {
                    vendor.RawCoordinates = concreteNpc.RawCoordinates;
                    vendor.Heading = concreteNpc.RawHeading;
                }

                vendor.Playfield = playfield;
                vendor.Stats[(int)StatIds.staticinstance].Value = captureTemplateId;

                int page = vendor.BaseInventory.StandardPage;
                vendor.BaseInventory[page].List().Clear();
                foreach (KeyValuePair<int, Item> item in items)
                {
                    vendor.BaseInventory.AddToPage(page, item.Key, item.Value);
                }

                return vendor;
            }
            catch (Exception exception)
            {
                if (vendor != null)
                {
                    Pool.Instance.RemoveObject(vendor);
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Antonio Stacklund vendor endpoint refused reason=" + exception.Message);
                return null;
            }
        }
    }
}
