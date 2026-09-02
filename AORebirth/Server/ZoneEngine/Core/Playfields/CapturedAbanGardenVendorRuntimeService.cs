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
    /// Attaches capture-backed VendingMachine shops to existing Aban Redeemed garden NPCs (PF 4676).
    /// Capture 20260823-205320. Duplicate display names ("Or-Mada of Protection") are matched by
    /// closest unmatched NPC in XZ to ExpectedX/ExpectedZ on each definition.
    /// </summary>
    internal sealed class CapturedAbanGardenVendorRuntimeService
    {
        private readonly List<IEntity> capturedVendors = new List<IEntity>();

        internal void Attach(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry)
        {
            if (playfield == null
                || playfieldIdentity.Instance != CapturedAbanGardenVendorContentProvider.GardenOfAbanPlayfieldId
                || CapturedAbanGardenVendorRuntimeRegistry.ContainsPlayfield(playfieldIdentity))
            {
                return;
            }

            List<ICharacter> availableNpcs = new List<ICharacter>();
            foreach (ICharacter character in dynelRegistry.Characters())
            {
                if (character == null || string.IsNullOrEmpty(character.Name))
                {
                    continue;
                }

                availableNpcs.Add(character);
            }

            int attached = 0;
            foreach (CapturedAbanGardenVendorDefinition definition in CapturedAbanGardenVendorContentProvider.Definitions)
            {
                ICharacter npc = TryTakeClosestNpcByName(availableNpcs, definition);
                if (npc == null)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "Aban garden vendor NPC missing name=" + definition.DisplayName
                        + " key=" + definition.DefinitionKey
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

                CapturedAbanGardenVendorRuntimeRegistry.Register(
                    new CapturedAbanGardenVendorRuntimeDefinition(
                        playfieldIdentity,
                        npc.Identity,
                        vendorIdentity,
                        definition));
                attached++;

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Aban garden vendor attached name=" + definition.DisplayName
                    + " key=" + definition.DefinitionKey
                    + " npc=" + npc.Identity
                    + " vendor=" + vendorIdentity
                    + " stockRows=" + definition.Stock.Count
                    + " gated=" + (definition.RequiresGardenKey ? 1 : 0)
                    + " evidence=" + definition.Evidence);
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Aban garden vendors attached=" + attached
                + "/" + CapturedAbanGardenVendorContentProvider.Definitions.Count
                + " pf=" + playfieldIdentity.Instance);
        }

        internal void Clear(Identity playfieldIdentity, PlayfieldDynelRegistry dynelRegistry)
        {
            CapturedAbanGardenVendorRuntimeRegistry.RemoveForPlayfield(playfieldIdentity);
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

        private static ICharacter TryTakeClosestNpcByName(
            List<ICharacter> availableNpcs,
            CapturedAbanGardenVendorDefinition definition)
        {
            ICharacter best = null;
            int bestIndex = -1;
            double bestDistSq = double.MaxValue;

            for (int i = 0; i < availableNpcs.Count; i++)
            {
                ICharacter candidate = availableNpcs[i];
                if (candidate == null
                    || !string.Equals(
                        candidate.Name,
                        definition.DisplayName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                double distSq = DistanceSquaredXz(candidate, definition.ExpectedX, definition.ExpectedZ);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = candidate;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                availableNpcs.RemoveAt(bestIndex);
            }

            return best;
        }

        private static double DistanceSquaredXz(ICharacter character, float expectedX, float expectedZ)
        {
            Character concrete = character as Character;
            if (concrete == null || concrete.Position == null)
            {
                return double.MaxValue;
            }

            double dx = (float)concrete.Position.x - expectedX;
            double dz = (float)concrete.Position.z - expectedZ;
            return (dx * dx) + (dz * dz);
        }

        private Vendor TryCreateVendor(
            Playfield playfield,
            Identity playfieldIdentity,
            ICharacter character,
            CapturedAbanGardenVendorDefinition definition)
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
                foreach (CapturedAbanGardenVendorStockDefinition stock in definition.Stock)
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
                    "Aban garden vendor endpoint refused name=" + definition.DisplayName
                    + " key=" + definition.DefinitionKey
                    + " reason=" + exception.Message);
                return null;
            }
        }
    }
}
