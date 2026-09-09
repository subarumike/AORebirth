namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

/// <summary>Capabilities belong to a complete accepted placement, never to an actor name.</summary>
internal sealed record AcceptedNpcBinding(string PlacementIdentity, string SourceIdentity,
    string ContentNpcIdentity, int PlayfieldId, bool HasDialogue, bool HasVendor);

internal sealed record AcceptedShopBinding(string PlacementIdentity, string SourceIdentity, int PlayfieldId);

/// <summary>One owner per playfield. Runtime identity reuse cannot inherit a placement's capabilities.</summary>
internal sealed class AcceptedNpcActivationService(Playfield playfield, DynelRegistry registry,
    PlayfieldLocality locality, IItemBuilder items, IItemTemplateCatalog catalog)
{
    readonly Dictionary<NpcCharacter, AcceptedNpcBinding> _bindings = new();
    readonly Dictionary<VendingMachine, AcceptedShopBinding> _standaloneShops = new();
    readonly HashSet<string> _activated = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _unavailableVendorEndpoints = new(StringComparer.Ordinal);
    internal IReadOnlyDictionary<string, string> UnavailableVendorEndpoints => _unavailableVendorEndpoints;
    bool _stopped;

    internal void Activate()
    {
        if (_stopped) return;
        foreach (var definition in AcceptedSocialNpcCatalog.Definitions.Where(d => d.Binding.PlayfieldId == playfield.Identity.Instance))
        {
            if (_activated.Contains(definition.Binding.PlacementIdentity)) continue;
            var npc = definition.Create(items);
            npc.Playfield = playfield;
            if (npc is AcceptedSubwayMerchantCatalog.AcceptedSubwayMerchantCharacter
                && !AcceptedSubwayMerchantCatalog.TryAttachShop(npc, items, catalog, out string failure))
                _unavailableVendorEndpoints[definition.Binding.PlacementIdentity] = failure;
            if (npc is AcceptedAreteVendorCatalog.AcceptedAreteVendorCharacter
                && !AcceptedAreteVendorCatalog.TryAttachShop(npc, items, catalog, out string areteFailure))
                _unavailableVendorEndpoints[definition.Binding.PlacementIdentity] = areteFailure;
            if (npc is AcceptedGardenVendorCatalog.AcceptedGardenVendorCharacter
                && !AcceptedGardenVendorCatalog.TryAttachShop(npc, items, catalog, out string gardenFailure))
                _unavailableVendorEndpoints[definition.Binding.PlacementIdentity] = gardenFailure;
            if (!registry.TryRegister(npc))
                throw new InvalidOperationException("Accepted NPC identity collision: " + definition.Binding.PlacementIdentity);
            try
            {
                Bind(npc, definition.Binding);
                npc.Died += OnDied;
                if (npc.Shop != null) npc.Shop.Playfield = playfield;
                locality.RegisterDynel(npc);
                _activated.Add(definition.Binding.PlacementIdentity);
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
        foreach (var definition in AcceptedAreteVendorCatalog.StandaloneDefinitions.Where(d => d.PlayfieldId == playfield.Identity.Instance))
        {
            if (_activated.Contains(definition.PlacementIdentity)) continue;
            if (!AcceptedAreteVendorCatalog.TryCreateStandaloneShop(definition, playfield, catalog, out var shop, out string failure))
            { _unavailableVendorEndpoints[definition.PlacementIdentity] = failure; continue; }
            bool newlyRegistered = registry.TryRegister(shop);
            if (!newlyRegistered && !TryAdoptExactStaticShop(shop, out shop))
                throw new InvalidOperationException("Accepted standalone vendor identity collision: " + definition.PlacementIdentity);
            try
            {
                _standaloneShops.Add(shop, new(definition.PlacementIdentity, definition.SourceIdentity, definition.PlayfieldId));
                if (newlyRegistered) locality.RegisterDynel(shop);
                _activated.Add(definition.PlacementIdentity);
            }
            catch
            {
                _standaloneShops.Remove(shop);
                if (newlyRegistered) { locality.UnregisterDynel(shop); registry.UnregisterExact(shop); }
                throw;
            }
        }
    }

    // Dynels.dat is loaded before accepted capability activation. An already-loaded
    // machine may acquire this exact stock only when every accepted placement field
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
        existing.Stock.SetAcceptedSnapshot(proposed.Stock.Slots);
        shop = existing;
        return true;
    }

    // Only accepted placement construction calls this in production. Packet handlers only query.
    internal void Bind(NpcCharacter npc, AcceptedNpcBinding binding)
    {
        if (_stopped || binding.PlayfieldId != playfield.Identity.Instance
            || string.IsNullOrWhiteSpace(binding.PlacementIdentity) || string.IsNullOrWhiteSpace(binding.SourceIdentity)
            || (binding.HasDialogue && string.IsNullOrWhiteSpace(binding.ContentNpcIdentity)) || !IsCurrent(npc)
            || _bindings.Values.Any(existing => existing.PlacementIdentity == binding.PlacementIdentity))
            throw new InvalidOperationException("Accepted NPC binding is incomplete, duplicate, or no longer current.");
        _bindings.Add(npc, binding);
    }

    internal bool TryGetBinding(NpcCharacter npc, out AcceptedNpcBinding binding)
    {
        binding = null!;
        return !_stopped && IsCurrent(npc) && _bindings.TryGetValue(npc, out binding!);
    }

    bool IsCurrent(NpcCharacter npc) => !playfield.IsDisposed && npc != null && !npc.IsDead && ReferenceEquals(npc.Playfield, playfield)
        && registry.TryGet(npc.Identity, out var current) && ReferenceEquals(current, npc);

    internal bool TryGetShopBinding(VendingMachine shop, out AcceptedShopBinding binding)
    {
        binding = null!;
        if (_stopped || shop == null || !ReferenceEquals(shop.Playfield, playfield) || !shop.Stock.IsAcceptedSnapshot) return false;
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
            playfield.GetRequiredService<ZoneEngine_New.Core.Trade.TradeService>().CloseMachine(shop, "accepted vendor despawned");
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
