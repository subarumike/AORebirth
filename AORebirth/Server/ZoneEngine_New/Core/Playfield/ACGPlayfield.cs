namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Diagnostics;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.WorldSimulation;

    /// <summary>
    /// Outdoor / RDB ACG playfield. <see cref="Build"/> constructs WorldSimulation and zoning triggers.
    /// </summary>
    public sealed class ACGPlayfield : Playfield
    {
        WorldSimulation.PlayfieldWorldSimulation? _world;

        public ACGPlayfield(
            Identity playfieldIdentity,
            IZoneLogger playfieldLogger,
            IMessageRouter router,
            PlayfieldManager playfieldManager,
            PlayerHydrator playerHydrator,
            IGameData gameData,
            IItemBuilder items,
            IInventoryRepository inventoryRepository,
            IItemInstanceIdAllocator instanceIds,
            InventoryMoveService inventoryMoves,
            InventoryFlushService inventoryFlush,
            CharacterSnapshotService characterSnapshot)
            : base(
                playfieldIdentity,
                playfieldLogger,
                router,
                playfieldManager,
                playerHydrator,
                gameData,
                items,
                inventoryRepository,
                instanceIds,
                inventoryMoves,
                inventoryFlush,
                characterSnapshot)
        {
        }

        public WorldSimulation.PlayfieldWorldSimulation? World => _world;

        public override void Build()
        {
            Stopwatch sw = Stopwatch.StartNew();
            int statics = 0;
            int wallTriggers = 0;
            int portalTriggers = 0;
            int doors = Geometry.Doors?.Doors?.Count ?? 0;

            try
            {
                MarkBuilt();
                _world = WorldSimulation.PlayfieldWorldSimulation.Create(
                    Identity.Instance,
                    Geometry,
                    MetaData,
                    DestinationsCatalog.Instance,
                    Logger);

                statics = _world.HardStaticCount;
                wallTriggers = _world.WallTriggerCount;
                portalTriggers = _world.PortalTriggerCount;

                RegisterWorldServices(_world);

                int staticDynels = SpawnStaticDynels();

                sw.Stop();
                Logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Playfield Build complete id={0} elapsedMs={1} statics={2} wallTriggers={3} portalTriggers={4} doors={5} staticDynels={6}",
                        Identity.Instance,
                        sw.ElapsedMilliseconds,
                        statics,
                        wallTriggers,
                        portalTriggers,
                        doors,
                        staticDynels));
            }
            catch (Exception exception)
            {
                sw.Stop();
                Logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Playfield Build failed id={0} elapsedMs={1}: {2}",
                        Identity.Instance,
                        sw.ElapsedMilliseconds,
                        exception.Message));
                _world?.Dispose();
                _world = null;
                throw;
            }
        }

        protected override void OnDispose()
        {
            _world?.Dispose();
            _world = null;
            base.OnDispose();
        }
    }
}
