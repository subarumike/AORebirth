namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Interfaces.Persistence.Shops;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Trade;

/// <summary>Capabilities belong to a complete loaded content placement, never to an actor name.</summary>
internal sealed record NpcContentBinding(string PlacementIdentity, string SourceIdentity,
    string ContentNpcIdentity, int PlayfieldId, bool HasDialogue, bool HasVendor);

internal sealed record ShopContentBinding(string PlacementIdentity, string SourceIdentity, int PlayfieldId);

/// <summary>One owner per playfield. Runtime identity reuse cannot inherit a placement's capabilities.</summary>
internal sealed class NpcContentActivationService(Playfield playfield, DynelRegistry registry,
    PlayfieldLocality locality, IItemBuilder items, IItemTemplateCatalog catalog, IGameData? gameData = null,
    IShopDao? shopDao = null)
{
    readonly ZoneEngine_New.Core.GameData.WorldContentCatalog _content = gameData?.WorldContent ?? ZoneEngine_New.Core.GameData.WorldContentCatalog.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "GameData"));
    readonly Dictionary<NpcCharacter, NpcContentBinding> _bindings = new();
    readonly Dictionary<VendingMachine, ShopContentBinding> _standaloneShops = new();
    readonly HashSet<string> _activated = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _unavailableVendorEndpoints = new(StringComparer.Ordinal);
    internal IReadOnlyDictionary<string, string> UnavailableVendorEndpoints => _unavailableVendorEndpoints;
    bool _stopped;

    internal void Activate()
    {
        if (_stopped) return;
        foreach (var definition in _content.Npcs.Where(d => d.PlayfieldId == playfield.Identity.Instance))
        {
            if (_activated.Contains(definition.Key)) continue;
            var npc = WorldNpcFactory.Create(definition, items);
            npc.Playfield = playfield;
            if (definition.Vendor != null && !WorldNpcFactory.TryAttachShop(npc, definition.Vendor, catalog, out string failure))
                _unavailableVendorEndpoints[definition.Key] = failure;
            if (!registry.TryRegister(npc))
                throw new InvalidOperationException("Content NPC identity collision: " + definition.Key);
            try
            {
                Bind(npc, new(definition.Key, definition.Provenance, definition.ContentNpcIdentity, definition.PlayfieldId, definition.HasDialogue, definition.Vendor != null));
                npc.Died += OnDied;
                if (npc.Shop != null) npc.Shop.Playfield = playfield;
                locality.RegisterDynel(npc);
                _activated.Add(definition.Key);
            }
            catch
            {
                _bindings.Remove(npc);
                npc.Died -= OnDied;
                locality.UnregisterDynel(npc);
                registry.UnregisterExact(npc);
                throw;
            }
        }
        ActivateDatabaseShops();
    }

    void ActivateDatabaseShops()
    {
        if (shopDao == null || gameData == null) return;

        int playfieldId = playfield.Identity.Instance;
        Dictionary<int, VendingMachine[]> machinesByVendorId = registry.Dynels()
            .OfType<VendingMachine>()
            .Where(machine => machine.SpawnSource == SpawnSource.StaticDynel && machine.OwnerNpc == null)
            .GroupBy(machine => DatabaseVendorId(playfieldId, machine.PlacementIdentity.Instance))
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (ShopVendorData vendor in shopDao.ListForPlayfield(playfieldId))
        {
            string key = "database:" + vendor.VendorId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!machinesByVendorId.TryGetValue(vendor.VendorId, out VendingMachine[]? candidates)
                || candidates.Length != 1)
            {
                _unavailableVendorEndpoints[key] = candidates == null
                    ? "No exact static vending-machine identity matched database vendor " + vendor.VendorId + "."
                    : "Multiple static vending machines matched database vendor " + vendor.VendorId + ".";
                continue;
            }

            VendingMachine machine = candidates[0];
            if (machine.Stock.IsConfiguredSnapshot)
            {
                // An exact accepted content binding already owns this runtime endpoint.
                continue;
            }

            machine.BindDatabaseDefinition(
                vendor.VendorId,
                vendor.VendorTemplateHash,
                vendor.StockGroupHash,
                vendor.PricingSkill);

            if (machine.Template.Id != vendor.PlacedTemplateId)
            {
                string failure = "Static template " + machine.Template.Id + " does not match database template "
                    + vendor.PlacedTemplateId + ".";
                machine.Stock.SetConfigurationUnavailable(failure);
                _unavailableVendorEndpoints[key] = failure;
                continue;
            }

            if (!TryBuildDatabaseStock(vendor, gameData, catalog, out ShopStockRange[] ranges, out string failureReason))
            {
                machine.Stock.SetConfigurationUnavailable(failureReason);
                _unavailableVendorEndpoints[key] = failureReason;
                continue;
            }

            machine.Stats.Set(CharacterStat.BuyModifier, (int)(vendor.BuyModifier * 100.0f));
            machine.Stats.Set(CharacterStat.SellModifier, (int)(vendor.SellModifier * 100.0f));
            machine.Stock.SetConfiguredRanges(ranges, Random.Shared);
            _standaloneShops.Add(machine, new ShopContentBinding(
                key,
                "database:vendor=" + vendor.VendorId + ";template=" + vendor.VendorTemplateHash
                    + ";shopInvHash=" + vendor.StockGroupHash,
                playfieldId));
        }
    }

    internal static int DatabaseVendorId(int playfieldId, int vendingMachineInstance)
        => checked((playfieldId << 16) | ((vendingMachineInstance >> 16) & 0xff));

    internal static bool TryBuildDatabaseStock(
        ShopVendorData vendor,
        IGameData gameData,
        IItemTemplateCatalog catalog,
        out ShopStockRange[] ranges,
        out string failure)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(catalog);

        var built = new List<ShopStockRange>(vendor.Stock.Count);
        foreach (ShopStockData row in vendor.Stock)
        {
            if (!string.Equals(row.StockGroupHash, vendor.StockGroupHash, StringComparison.Ordinal))
            {
                ranges = [];
                failure = "Stock row " + row.StockRowId + " does not retain ShopInvHash " + vendor.StockGroupHash + ".";
                return false;
            }
            if (!gameData.TryGetAssignedItemHash(row.LowId, row.HighId, out string assignedHash)
                || !gameData.TryGetHashInstance(assignedHash, out HashInstance assigned)
                || !ExactPair(assigned, row.LowId, row.HighId))
            {
                ranges = [];
                failure = "Stock row " + row.StockRowId + " has no unique exact hash assignment for "
                    + row.LowId + "/" + row.HighId + ".";
                return false;
            }
            if (!catalog.TryGet(row.LowId, out _) || !catalog.TryGet(row.HighId, out _))
            {
                ranges = [];
                failure = "Stock row " + row.StockRowId + " references unavailable item templates "
                    + row.LowId + "/" + row.HighId + ".";
                return false;
            }
            if (row.EffectiveMinimumQuality <= 0 || row.EffectiveMaximumQuality < row.EffectiveMinimumQuality)
            {
                ranges = [];
                failure = "Stock row " + row.StockRowId + " has an invalid effective QL range.";
                return false;
            }

            built.Add(new ShopStockRange(
                assignedHash,
                row.LowId,
                row.HighId,
                row.EffectiveMinimumQuality,
                row.EffectiveMaximumQuality));
        }

        if (built.Count == 0)
        {
            ranges = [];
            failure = "ShopInvHash " + vendor.StockGroupHash + " has no active stock rows in the vendor QL range.";
            return false;
        }

        ranges = built.ToArray();
        failure = string.Empty;
        return true;
    }

    static bool ExactPair(HashInstance instance, int lowId, int highId)
        => instance.TemplateIds.Length == 1
            ? instance.TemplateIds[0] == lowId && lowId == highId
            : instance.TemplateIds.Length == 2
                && instance.TemplateIds[0] == lowId && instance.TemplateIds[1] == highId;
    // Only loaded content placement construction calls this in production. Packet handlers only query.
    internal void Bind(NpcCharacter npc, NpcContentBinding binding)
    {
        if (_stopped || binding.PlayfieldId != playfield.Identity.Instance
            || string.IsNullOrWhiteSpace(binding.PlacementIdentity)
            || (binding.HasDialogue && string.IsNullOrWhiteSpace(binding.ContentNpcIdentity)) || !IsCurrent(npc)
            || _bindings.Values.Any(existing => existing.PlacementIdentity == binding.PlacementIdentity))
            throw new InvalidOperationException("Content NPC binding is incomplete, duplicate, or no longer current.");
        _bindings.Add(npc, binding);
    }

    internal bool TryGetBinding(NpcCharacter npc, out NpcContentBinding binding)
    {
        binding = null!;
        return !_stopped && IsCurrent(npc) && _bindings.TryGetValue(npc, out binding!);
    }

    bool IsCurrent(NpcCharacter npc) => !playfield.IsDisposed && npc != null && !npc.IsDead && ReferenceEquals(npc.Playfield, playfield)
        && registry.TryGet(npc.Identity, out var current) && ReferenceEquals(current, npc);

    internal bool TryGetShopBinding(VendingMachine shop, out ShopContentBinding binding)
    {
        binding = null!;
        if (_stopped || shop == null || !ReferenceEquals(shop.Playfield, playfield) || !shop.Stock.IsConfiguredSnapshot) return false;
        if (shop.OwnerNpc is { } owner)
        {
            if (!ReferenceEquals(owner.Shop, shop) || !TryGetBinding(owner, out var npcBinding) || !npcBinding.HasVendor) return false;
            binding = new(npcBinding.PlacementIdentity, npcBinding.SourceIdentity, npcBinding.PlayfieldId);
            return true;
        }
        return registry.TryGet(shop.Identity, out var current) && ReferenceEquals(current, shop)
            && _standaloneShops.TryGetValue(shop, out binding!);
    }

    internal void Tick()
    {
        // Dead actors retain the death subscription until the shared corpse swap fires.
        foreach (var npc in _bindings.Keys.Where(npc => !IsCurrent(npc) && (!npc.IsDead
            || !registry.TryGet(npc.Identity, out var current) || !ReferenceEquals(current, npc))).ToArray())
            Detached(npc);
        foreach (var shop in _standaloneShops.Keys.Where(shop => !ReferenceEquals(shop.Playfield, playfield)
            || !registry.TryGet(shop.Identity, out var current) || !ReferenceEquals(current, shop)).ToArray())
            _standaloneShops.Remove(shop);
    }

    internal void Detached(NpcCharacter npc)
    {
        if (!_bindings.Remove(npc)) return;
        npc.Died -= OnDied;
        if (npc.Shop is { } shop)
        {
            playfield.GetRequiredService<ZoneEngine_New.Core.Trade.TradeService>().CloseMachine(shop, "content vendor despawned");
            shop.Playfield = null;
        }
    }

    void OnDied(Character character)
    {
        if (character is not NpcCharacter npc) return;
        npc.Died -= OnDied;
        _bindings.Remove(npc);
        if (npc.Shop is { } shop && ReferenceEquals(shop.Playfield, playfield)) shop.Playfield = null;
        registry.UnregisterExact(npc);
        if (ReferenceEquals(npc.Playfield, playfield))
        {
            locality.UnregisterDynel(npc);
            npc.Playfield = null;
        }
    }

    internal void Shutdown()
    {
        _stopped = true;
        foreach (var npc in _bindings.Keys)
        {
            npc.Died -= OnDied;
            if (npc.Shop is { } shop)
            {
                playfield.GetRequiredService<ZoneEngine_New.Core.Trade.TradeService>().CloseMachine(shop, "playfield shutdown");
                shop.Playfield = null;
            }
        }
        _bindings.Clear();
        foreach (var shop in _standaloneShops.Keys)
        {
            playfield.GetRequiredService<ZoneEngine_New.Core.Trade.TradeService>().CloseMachine(shop, "playfield shutdown");
            shop.Playfield = null;
        }
        _standaloneShops.Clear();
        _unavailableVendorEndpoints.Clear();
    }
}
