namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    /// <summary>
    /// Attaches capture-backed VendingMachine shops to existing Thrak Omni garden NPCs (PF 4677).
    /// Capture 20260718-210135.
    /// </summary>
    internal sealed class CapturedThrakGardenVendorRuntimeService
    {
        private readonly List<IEntity> capturedVendors = new List<IEntity>();

        internal void Attach(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            if (playfield == null
                || playfieldIdentity.Instance != CapturedThrakGardenVendorContentProvider.ThrakOmniGardenPlayfieldId
                || CapturedThrakGardenVendorRuntimeRegistry.ContainsPlayfield(playfieldIdentity))
            {
                return;
            }

            Dictionary<string, ICharacter> npcsByName = new Dictionary<string, ICharacter>(
                StringComparer.OrdinalIgnoreCase);
            foreach (ICharacter character in dynelRegistry.Characters())
            {
                if (character == null || string.IsNullOrEmpty(character.Name))
                {
                    continue;
                }

                if (!npcsByName.ContainsKey(character.Name))
                {
                    npcsByName[character.Name] = character;
                }
            }

            int attached = 0;
            foreach (CapturedThrakGardenVendorDefinition definition in CapturedThrakGardenVendorContentProvider.Definitions)
            {
                ICharacter npc;
                if (!npcsByName.TryGetValue(definition.DisplayName, out npc) || npc == null)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "Thrak garden vendor NPC missing name=" + definition.DisplayName
                        + " evidence=" + definition.Evidence);
                    continue;
                }

                Vendor vendor = definition.HasCapturedStock
                    ? this.TryCreateVendor(playfield, playfieldIdentity, npc, definition)
                    : null;
                Identity vendorIdentity = vendor == null ? Identity.None : vendor.Identity;
                if (vendor != null)
                {
                    dynelRegistry.Register(vendor);
                    this.capturedVendors.Add(vendor);
                }

                CapturedThrakGardenVendorRuntimeRegistry.Register(
                    new CapturedThrakGardenVendorRuntimeDefinition(
                        playfieldIdentity,
                        npc.Identity,
                        vendorIdentity,
                        definition));
                attached++;

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Thrak garden vendor attached name=" + definition.DisplayName
                    + " npc=" + npc.Identity
                    + " vendor=" + vendorIdentity
                    + " stockRows=" + definition.Stock.Count
                    + " gated=" + (definition.RequiresCompletedGardenKeyQuest ? 1 : 0)
                    + " evidence=" + definition.Evidence);
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Thrak garden vendors attached=" + attached
                + "/" + CapturedThrakGardenVendorContentProvider.Definitions.Count
                + " pf=" + playfieldIdentity.Instance);
        }

        internal void Clear(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
        {
            CapturedThrakGardenVendorRuntimeRegistry.RemoveForPlayfield(playfieldIdentity);
            foreach (IEntity entity in this.capturedVendors)
            {
                dynelRegistry.Unregister(entity.Identity);
                Vendor vendor = entity as Vendor;
                if (vendor != null)
                {
                    Pool.Instance.RemoveObject(vendor);
                }
            }

            this.capturedVendors.Clear();
        }

        private Vendor TryCreateVendor(
            Playfield playfield,
            Identity playfieldIdentity,
            ICharacter character,
            CapturedThrakGardenVendorDefinition definition)
        {
            Vendor vendor = null;
            try
            {
                if (!ItemLoader.ItemList.ContainsKey(definition.VendorTemplateId))
                {
                    throw new InvalidOperationException(
                        "missing vendor template item " + definition.VendorTemplateId);
                }

                var items = new List<KeyValuePair<int, Item>>();
                int skipped = 0;
                foreach (CapturedThrakGardenVendorStockDefinition stock in definition.Stock)
                {
                    if (!ItemLoader.ItemList.ContainsKey(stock.LowId)
                        || !ItemLoader.ItemList.ContainsKey(stock.HighId))
                    {
                        skipped++;
                        continue;
                    }

                    items.Add(
                        new KeyValuePair<int, Item>(
                            stock.Slot,
                            new Item(stock.Quality, stock.LowId, stock.HighId)));
                }

                if (items.Count == 0)
                {
                    throw new InvalidOperationException(
                        "no stock items resolved (skipped=" + skipped + ")");
                }

                var identity =
                    new Identity
                    {
                        Type = IdentityType.VendingMachine,
                        Instance = Pool.Instance.GetFreeInstance<Vendor>(0x70000000, IdentityType.VendingMachine)
                    };
                vendor = new Vendor(playfieldIdentity, identity, definition.VendorTemplateId);
                vendor.NpcIdentity = character.Identity;
                Character concreteNpc = character as Character;
                if (concreteNpc != null)
                {
                    vendor.Position = concreteNpc.Position;
                    vendor.Rotation = concreteNpc.Rotation;
                }

                vendor.Playfield = playfield;

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
                    "Thrak garden vendor endpoint refused name=" + definition.DisplayName
                    + " reason=" + exception.Message);
                return null;
            }
        }
    }
}
