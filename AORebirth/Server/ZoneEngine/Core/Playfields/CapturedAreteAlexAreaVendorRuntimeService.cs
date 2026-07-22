namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    internal sealed class CapturedAreteAlexAreaVendorRuntimeService
    {
        private readonly List<Vendor> capturedVendors = new List<Vendor>();

        private readonly HashSet<int> spawnedPlayfields = new HashSet<int>();

        internal void Spawn(Playfield playfield, Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
        {
            if (playfield == null
                || dynelRegistry == null
                || playfieldIdentity.Instance != CapturedAreteAlexAreaVendorContentProvider.AreteLandingPlayfieldId
                || !this.spawnedPlayfields.Add(playfieldIdentity.Instance))
            {
                return;
            }

            int spawned = 0;
            foreach (CapturedAreteAlexAreaVendorDefinition definition in CapturedAreteAlexAreaVendorContentProvider.Vendors)
            {
                Vendor vendor = this.TryCreateVendor(playfield, playfieldIdentity, definition);
                if (vendor == null)
                {
                    continue;
                }

                dynelRegistry.Register(vendor);
                this.capturedVendors.Add(vendor);
                spawned++;
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Captured Arete Alex-area vendor spawned name="
                    + definition.DisplayName
                    + " sourceVendor=VendingMachine:"
                    + definition.SourceVendorInstance.ToString("X8")
                    + " runtimeVendor="
                    + vendor.Identity
                    + " template="
                    + vendor.Stats[(int)StatIds.staticinstance].Value
                    + " stockRows="
                    + definition.Stock.Count
                    + " evidence=20260720-074847+20260721-lockpick");
            }

            if (spawned == 0)
            {
                this.spawnedPlayfields.Remove(playfieldIdentity.Instance);
            }
        }

        internal void Clear(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
        {
            this.spawnedPlayfields.Remove(playfieldIdentity.Instance);
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
            CapturedAreteAlexAreaVendorDefinition definition)
        {
            Vendor vendor = null;
            try
            {
                int captureTemplateId = definition.TemplateId;
                int templateId = captureTemplateId;
                if (!ItemLoader.ItemList.ContainsKey(templateId)
                    || ItemNamesDao.Instance.Get(templateId) == null)
                {
                    if (!ItemLoader.ItemList.ContainsKey(
                            CapturedAreteAlexAreaVendorContentProvider.RuntimeVendorTemplateFallbackId))
                    {
                        throw new InvalidOperationException(
                            "missing vendor template item "
                            + captureTemplateId
                            + " and fallback "
                            + CapturedAreteAlexAreaVendorContentProvider.RuntimeVendorTemplateFallbackId);
                    }

                    templateId = CapturedAreteAlexAreaVendorContentProvider.RuntimeVendorTemplateFallbackId;
                }

                var items = new List<KeyValuePair<int, Item>>();
                foreach (CapturedAreteAlexAreaVendorStockDefinition stock in definition.Stock)
                {
                    if (!ItemLoader.ItemList.ContainsKey(stock.LowId)
                        || !ItemLoader.ItemList.ContainsKey(stock.HighId))
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Engine,
                            "Arete Alex-area vendor skip missing stock low="
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

                var identity = new Identity
                               {
                                   Type = IdentityType.VendingMachine,
                                   Instance =
                                       Pool.Instance.GetFreeInstance<Vendor>(
                                           0x70000000,
                                           IdentityType.VendingMachine)
                               };
                vendor = new Vendor(playfieldIdentity, identity, templateId);
                vendor.Name = definition.DisplayName;
                vendor.NpcIdentity = Identity.None;
                vendor.RawCoordinates = new Vector3(definition.X, definition.Y, definition.Z);
                vendor.Heading = new Quaternion(
                    definition.HeadingX,
                    definition.HeadingY,
                    definition.HeadingZ,
                    definition.HeadingW);
                vendor.Playfield = playfield;
                vendor.Stats[(int)StatIds.staticinstance].Value = captureTemplateId;

                if (vendor.BaseInventory != null)
                {
                    int standardPage = vendor.BaseInventory.StandardPage;
                    if (vendor.BaseInventory[standardPage] != null)
                    {
                        vendor.BaseInventory[standardPage].List().Clear();
                        foreach (KeyValuePair<int, Item> item in items)
                        {
                            vendor.BaseInventory.AddToPage(standardPage, item.Key, item.Value);
                        }
                    }
                }

                return vendor;
            }
            catch (Exception ex)
            {
                if (vendor != null)
                {
                    Pool.Instance.RemoveObject(vendor);
                }

                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Captured Arete Alex-area vendor refused name="
                    + definition.DisplayName
                    + " reason="
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
                return null;
            }
        }
    }
}
