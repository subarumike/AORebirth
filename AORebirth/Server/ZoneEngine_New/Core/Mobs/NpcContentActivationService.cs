namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

/// <summary>Capabilities belong to a complete loaded content placement, never to an actor name.</summary>
internal sealed record NpcContentBinding(string PlacementIdentity, string SourceIdentity,
    string ContentNpcIdentity, int PlayfieldId, bool HasDialogue, bool HasVendor);

internal sealed record ShopContentBinding(string PlacementIdentity, string SourceIdentity, int PlayfieldId);

/// <summary>One owner per playfield. Runtime identity reuse cannot inherit a placement's capabilities.</summary>
internal sealed class NpcContentActivationService(Playfield playfield, DynelRegistry registry,
    PlayfieldLocality locality, IItemBuilder items, IItemTemplateCatalog catalog, ZoneEngine_New.Core.GameData.IGameData? gameData = null)
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
        foreach (var definition in _content.Shops.Where(d => d.PlayfieldId == playfield.Identity.Instance))
        {
            if (_activated.Contains(definition.Key)) continue;
            if (!TryCreateStandaloneShop(definition, out var shop, out string failure))
            { _unavailableVendorEndpoints[definition.Key] = failure; continue; }
            bool newlyRegistered = registry.TryRegister(shop);
            if (!newlyRegistered && !TryAdoptExactStaticShop(shop, out shop))
                throw new InvalidOperationException("Content standalone vendor identity collision: " + definition.Key);
            try
            {
                _standaloneShops.Add(shop, new(definition.Key, definition.Provenance, definition.PlayfieldId));
                if (newlyRegistered) locality.RegisterDynel(shop);
                _activated.Add(definition.Key);
            }
            catch
            {
                _standaloneShops.Remove(shop);
                if (newlyRegistered) { locality.UnregisterDynel(shop); registry.UnregisterExact(shop); }
                throw;
            }
        }
    }

    bool TryCreateStandaloneShop(ZoneEngine_New.Core.GameData.WorldShopDefinition definition,
        out VendingMachine shop, out string failure)
    {
        if (!WorldNpcFactory.TryCreateShop(definition.Vendor, catalog, out shop, out failure)) return false;
        shop.Playfield = playfield;
        shop.Position = new(definition.Position[0], definition.Position[1], definition.Position[2]);
        shop.Rotation = new(definition.Rotation[0], definition.Rotation[1], definition.Rotation[2], definition.Rotation[3]);
        return true;
    }

    // Dynels.dat is loaded before content capability activation. An already-loaded
    // machine may acquire this exact stock only when every loaded content placement field
    // matches and its stock has never been opened/generated; it is never replaced.
    bool TryAdoptExactStaticShop(VendingMachine proposed, out VendingMachine shop)
    {
        shop = proposed;
        if (!registry.TryGet(proposed.Identity, out var dynel) || dynel is not VendingMachine existing
            || existing.SpawnSource != SpawnSource.StaticDynel || existing.OwnerNpc != null
            || !ReferenceEquals(existing.Playfield, playfield) || existing.Template.Id != proposed.Template.Id
            || existing.Stock.IsGenerated || existing.Stock.Slots.Count != 0
            || existing.Position.x != proposed.Position.x || existing.Position.y != proposed.Position.y || existing.Position.z != proposed.Position.z
            || existing.Rotation.xf != proposed.Rotation.xf || existing.Rotation.yf != proposed.Rotation.yf
            || existing.Rotation.zf != proposed.Rotation.zf || existing.Rotation.wf != proposed.Rotation.wf)
            return false;
        existing.Stock.SetConfiguredSnapshot(proposed.Stock.Slots);
        shop = existing;
        return true;
    }

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
