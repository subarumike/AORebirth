namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Playfields;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    internal sealed class CapturedHoloDeckVendorRuntimeService
    {
        /// <summary>
        /// Live capture template 303217 is often absent from local item DBs.
        /// Fall back to the same Containers shop template Subway vendors use.
        /// </summary>
        private const int RuntimeVendorTemplateFallbackId = 99634;

        private readonly List<Vendor> capturedVendors = new List<Vendor>();

        internal void Spawn(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            if (playfield == null
                || dynelRegistry == null
                || playfieldIdentity.Instance != CapturedHoloDeckVendorContentProvider.HoloDeckPlayfieldId
                || CapturedHoloDeckVendorRuntimeRegistry.ContainsPlayfield(playfieldIdentity))
            {
                return;
            }

            Vendor vendor = this.TryCreateVendor(playfield, playfieldIdentity);
            if (vendor == null)
            {
                return;
            }

            dynelRegistry.Register(vendor);
            this.capturedVendors.Add(vendor);
            CapturedHoloDeckVendorRuntimeRegistry.Register(
                new CapturedHoloDeckVendorRuntimeDefinition(playfieldIdentity, vendor.Identity));

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Captured HoloDeck vendor spawned sourceVendor=VendingMachine:"
                + CapturedHoloDeckVendorContentProvider.SourceVendorInstance.ToString("X8")
                + " runtimeVendor=" + vendor.Identity
                + " template=" + vendor.Stats[0x17].Value
                + " stockRows=" + CapturedHoloDeckVendorContentProvider.Stock.Count
                + " evidence=20260719-155043");
        }

        internal void Clear(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
        {
            CapturedHoloDeckVendorRuntimeRegistry.RemoveForPlayfield(playfieldIdentity);
            foreach (Vendor vendor in this.capturedVendors)
            {
                dynelRegistry.Unregister(vendor.Identity);
                Pool.Instance.RemoveObject(vendor);
            }

            this.capturedVendors.Clear();
        }

        private Vendor TryCreateVendor(Playfield playfield, Identity playfieldIdentity)
        {
            Vendor vendor = null;
            try
            {
                int captureTemplateId = CapturedHoloDeckVendorContentProvider.VendorTemplateId;
                int templateId = captureTemplateId;
                if (!ItemLoader.ItemList.ContainsKey(templateId)
                    || ItemNamesDao.Instance.Get(templateId) == null)
                {
                    if (!ItemLoader.ItemList.ContainsKey(RuntimeVendorTemplateFallbackId))
                    {
                        throw new InvalidOperationException(
                            "missing vendor template item " + captureTemplateId
                            + " and fallback " + RuntimeVendorTemplateFallbackId);
                    }

                    templateId = RuntimeVendorTemplateFallbackId;
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "Captured HoloDeck vendor using fallback template="
                        + templateId
                        + " missingCaptureTemplate="
                        + captureTemplateId);
                }

                var items = new List<KeyValuePair<int, Item>>();
                foreach (CapturedHoloDeckVendorStockDefinition stock in CapturedHoloDeckVendorContentProvider.Stock)
                {
                    if (!ItemLoader.ItemList.ContainsKey(stock.LowId)
                        || !ItemLoader.ItemList.ContainsKey(stock.HighId))
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Engine,
                            "Captured HoloDeck vendor skip missing stock low="
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

                var identity =
                    new Identity
                    {
                        Type = IdentityType.VendingMachine,
                        Instance = Pool.Instance.GetFreeInstance<Vendor>(0x70000000, IdentityType.VendingMachine)
                    };
                vendor = new Vendor(playfieldIdentity, identity, templateId);
                if (string.IsNullOrEmpty(vendor.Name))
                {
                    DBItemName named = ItemNamesDao.Instance.Get(captureTemplateId);
                    vendor.Name = named != null && !string.IsNullOrEmpty(named.Name)
                                      ? named.Name
                                      : "Reward Terminal";
                }

                vendor.NpcIdentity = Identity.None;
                vendor.Position =
                    new AORebirth.Core.Vector.Vector3(
                        CapturedHoloDeckVendorContentProvider.X,
                        CapturedHoloDeckVendorContentProvider.Y,
                        CapturedHoloDeckVendorContentProvider.Z);
                vendor.Rotation =
                    new AORebirth.Core.Vector.Quaternion(
                        CapturedHoloDeckVendorContentProvider.HeadingX,
                        CapturedHoloDeckVendorContentProvider.HeadingY,
                        CapturedHoloDeckVendorContentProvider.HeadingZ,
                        CapturedHoloDeckVendorContentProvider.HeadingW);
                vendor.Playfield = playfield;

                // Preserve capture static instance for client shop identity when using fallback template.
                vendor.Stats[0x17].Value = captureTemplateId;

                if (vendor.BaseInventory != null)
                {
                    int page = vendor.BaseInventory.StandardPage;
                    if (vendor.BaseInventory[page] != null)
                    {
                        vendor.BaseInventory[page].List().Clear();
                        foreach (KeyValuePair<int, Item> item in items)
                        {
                            vendor.BaseInventory.AddToPage(page, item.Key, item.Value);
                        }
                    }
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
                    "Captured HoloDeck vendor refused sourceVendor=VendingMachine:"
                    + CapturedHoloDeckVendorContentProvider.SourceVendorInstance.ToString("X8")
                    + " reason=" + exception.GetType().Name + ": " + exception.Message);
                return null;
            }
        }
    }
}
