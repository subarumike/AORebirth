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
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    /// <summary>
    /// Freestanding Arete vendors: Junk Shop, ICC Ammunition (Alex), ICC Tech Supplies (Vernon / Lock Pick).
    /// Capture 20260801-215330 / 20260721-lockpick.
    /// </summary>
    internal sealed class CapturedAreteAlexAreaVendorRuntimeService
    {
        private readonly List<Vendor> capturedVendors = new List<Vendor>();

        private readonly object syncRoot = new object();

        internal void Spawn(Playfield playfield, Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
        {
            this.EnsurePresent(playfield, playfieldIdentity, dynelRegistry);
        }

        /// <summary>
        /// Heartbeat-safe: spawn any missing Alex/Vernon freestanding shops (esp. ICC Tech Supplies).
        /// </summary>
        internal void EnsurePresent(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            if (playfield == null
                || dynelRegistry == null
                || playfieldIdentity.Instance != CapturedAreteAlexAreaVendorContentProvider.AreteLandingPlayfieldId)
            {
                return;
            }

            lock (this.syncRoot)
            {
                this.PurgeNonRenderingIccTechLocked(playfieldIdentity, dynelRegistry);

                foreach (CapturedAreteAlexAreaVendorDefinition definition in
                    CapturedAreteAlexAreaVendorContentProvider.Vendors)
                {
                    if (this.HasRegisteredVendor(definition.DisplayName, playfieldIdentity))
                    {
                        continue;
                    }

                    Vendor vendor = this.TryCreateVendor(playfield, playfieldIdentity, definition);
                    if (vendor == null)
                    {
                        continue;
                    }

                    dynelRegistry.Register(vendor);
                    this.capturedVendors.Add(vendor);
                    CapturedAreteAlexAreaVendorRuntimeRegistry.Register(
                        new CapturedAreteAlexAreaVendorRuntimeDefinition(
                            playfieldIdentity,
                            vendor.Identity,
                            definition.DisplayName));
                    this.AnnounceToPlayfieldPlayers(playfield, vendor);
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
                        + " evidence=20260801-215330+20260721-lockpick");
                }
            }
        }

        /// <summary>
        /// Replace wrong-mesh ICC Tech boxes (live 300946 invisible; 99634 generic SHOP).
        /// Expected runtime mesh is tip itemref 297290.
        /// </summary>
        private void PurgeNonRenderingIccTechLocked(
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            const int ExpectedIccTechTemplateId = 297290;
            for (int i = this.capturedVendors.Count - 1; i >= 0; i--)
            {
                Vendor vendor = this.capturedVendors[i];
                if (vendor == null
                    || !string.Equals(vendor.Name, "ICC Tech Supplies", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                CapturedAreteAlexAreaVendorRuntimeDefinition runtime;
                if (!CapturedAreteAlexAreaVendorRuntimeRegistry.TryGet(vendor.Identity.Instance, out runtime)
                    || !CapturedAreteAlexAreaVendorRuntimeRegistry.Same(
                        runtime.PlayfieldIdentity,
                        playfieldIdentity))
                {
                    continue;
                }

                int advertised = vendor.Stats[(int)StatIds.staticinstance].Value;
                if (advertised == ExpectedIccTechTemplateId)
                {
                    continue;
                }

                CapturedAreteAlexAreaVendorRuntimeRegistry.Remove(vendor.Identity.Instance);
                if (dynelRegistry != null)
                {
                    dynelRegistry.Unregister(vendor.Identity);
                }

                Pool.Instance.RemoveObject(vendor);
                this.capturedVendors.RemoveAt(i);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Purged wrong-mesh ICC Tech Supplies template="
                    + advertised
                    + " for respawn as "
                    + ExpectedIccTechTemplateId);
            }
        }

        internal void Clear(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
        {
            lock (this.syncRoot)
            {
                CapturedAreteAlexAreaVendorRuntimeRegistry.RemoveForPlayfield(playfieldIdentity);
                foreach (Vendor vendor in this.capturedVendors)
                {
                    if (dynelRegistry != null)
                    {
                        dynelRegistry.Unregister(vendor.Identity);
                    }

                    Pool.Instance.RemoveObject(vendor);
                }

                this.capturedVendors.Clear();
            }
        }

        private bool HasRegisteredVendor(string displayName, Identity playfieldIdentity)
        {
            for (int i = 0; i < this.capturedVendors.Count; i++)
            {
                Vendor vendor = this.capturedVendors[i];
                if (vendor == null
                    || !string.Equals(vendor.Name, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                CapturedAreteAlexAreaVendorRuntimeDefinition runtime;
                if (CapturedAreteAlexAreaVendorRuntimeRegistry.TryGet(vendor.Identity.Instance, out runtime)
                    && CapturedAreteAlexAreaVendorRuntimeRegistry.Same(
                        runtime.PlayfieldIdentity,
                        playfieldIdentity))
                {
                    // Still alive in pool?
                    Vendor live = Pool.Instance.GetObject<Vendor>(
                        playfieldIdentity,
                        vendor.Identity);
                    if (live != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void AnnounceToPlayfieldPlayers(Playfield playfield, Vendor vendor)
        {
            if (playfield == null || vendor == null)
            {
                return;
            }

            foreach (ICharacter character in playfield.EnumerateActiveCharacters())
            {
                if (character == null || !(character.Controller is PlayerController))
                {
                    continue;
                }

                VendingMachineFullUpdateMessageHandler.Default.SendAreteFreestanding(character, vendor);
            }
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
                // Prefer definition template (297290 for ICC Tech). Only fall back to 99634 when
                // the preferred template is missing from ItemLoader/ItemNamesDao.
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

                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "Arete Alex-area vendor template fallback capture="
                        + captureTemplateId
                        + " runtime="
                        + CapturedAreteAlexAreaVendorContentProvider.RuntimeVendorTemplateFallbackId
                        + " name="
                        + definition.DisplayName);
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
                            + stock.HighId
                            + " name="
                            + definition.DisplayName);
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
                vendor.Stats[(int)StatIds.staticinstance].Value = templateId;
                vendor.Stats[0x17].Value = templateId;

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
                    + (definition == null ? "?" : definition.DisplayName)
                    + " reason="
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
                return null;
            }
        }
    }
}
