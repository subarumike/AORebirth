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
using ZoneEngine_New.Core.WorldSimulation;

/// <summary>A leased mission world is built only from its exact durable binding and accepted bundle.</summary>
public sealed class MissionPlayfield : Playfield
{
    readonly IItemBuilder _items;
    PlayfieldWorldSimulation? _simulation;
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
        var locality = GetRequiredService<PlayfieldLocality>();
        World.TryGetGenerator(out AcgBuildingGeneratorData? generator);
        AORebirth.World.Collision.DungeonWorldLayout? dungeon = DungeonPlayfieldBinder.TryBuild(
            GameData.RootPath, Identity.Instance, generator, Logger);
        if (dungeon != null)
            locality.ApplyDungeonRooms(dungeon.Rooms);

        var geometry = DungeonPlayfieldBinder.WithDungeonCollision(Identity.Instance, Geometry, dungeon);
        if (geometry.Collision?.HasCollision == true)
        {
            _simulation = PlayfieldWorldSimulation.Create(
                Identity.Instance,
                geometry,
                MetaData,
                DestinationsCatalog.Instance,
                GameData,
                Logger);
            RegisterWorldServices(_simulation);
        }

        // Prepare every actor first. A missing NPC policy/object packet cannot expose half a world.
        var dynels = World.CreateDynels(this, _items);
        var registry = GetRequiredService<DynelRegistry>();
        foreach (var dynel in dynels) registry.Register(dynel);
        foreach (var dynel in dynels) locality.RegisterDynel(dynel);
        MarkBuilt();
    }

    protected override void OnDispose()
    {
        _simulation?.Dispose();
        _simulation = null;
        base.OnDispose();
    }

    public override PlayfieldAnarchyFMessage CreatePlayfieldAnarchyFMessage(Vector3 coordinates)
        => World.CreateZoneMessage(coordinates);
}
