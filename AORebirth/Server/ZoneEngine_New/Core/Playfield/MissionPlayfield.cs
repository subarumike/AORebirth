namespace ZoneEngine_New.Core.Playfield;

using System;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Metrics;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Trade;

/// <summary>A leased mission world is built only from its exact durable binding and accepted bundle.</summary>
public sealed class MissionPlayfield : Playfield
{
    readonly IItemBuilder _items;
    public GeneratedMissionWorld World { get; }
    public MissionPlayfield(GeneratedMissionWorld world, IZoneLogger logger, IMessageRouter router,
        PlayfieldManager manager, PlayerHydrator hydrator, IGameData data, IItemBuilder items,
        HashItemMinter hashItems, IInventoryRepository inventory, IItemInstanceIdAllocator ids,
        InventoryMoveService moves, InventoryFlushService flush, TradeService trades,
        CharacterSnapshotService snapshot, IPlayfieldMetricsRegistry metrics)
        : base(new Identity { Type = IdentityType.Playfield, Instance = world.LivePlayfield }, logger, router,
            manager, hydrator, data, items, hashItems, inventory, ids, moves, flush, trades, snapshot, metrics)
    { World = world ?? throw new ArgumentNullException(nameof(world)); _items = items; }

    public override void Build()
    {
        if (IsBuilt) return;
        // Prepare every actor first. A missing NPC policy/object packet cannot expose half a world.
        var dynels = World.CreateDynels(this, _items);
        var registry = GetRequiredService<DynelRegistry>();
        var locality = GetRequiredService<PlayfieldLocality>();
        foreach (var dynel in dynels) registry.Register(dynel);
        foreach (var dynel in dynels) locality.RegisterDynel(dynel);
        MarkBuilt();
    }

    public override PlayfieldAnarchyFMessage CreatePlayfieldAnarchyFMessage(Vector3 coordinates)
        => World.CreateZoneMessage(coordinates);
}
