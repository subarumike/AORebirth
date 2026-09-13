namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Numerics;

    using AODB.Common.RDBObjects;

    using AORebirth.Core.GameData;

    using BepuPhysics;
    using BepuPhysics.Collidables;
    using BepuPhysics.CollisionDetection;
    using BepuPhysics.Constraints;
    using BepuUtilities;
    using BepuUtilities.Memory;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    using AoVector3 = AORebirth.Core.Vector.Vector3;
    using AoQuaternion = AORebirth.Core.Vector.Quaternion;
    using CharacterStat = SmokeLounge.AOtomation.Messaging.GameData.CharacterStat;
    using IdentityType = SmokeLounge.AOtomation.Messaging.GameData.IdentityType;
    using PlayfieldType = ZoneEngine_New.Core.Playfield.Playfield;

    /// <summary>A resolved zone transition: where it goes, and which trigger produced it.</summary>
    public readonly record struct ZoneCrossing(int DestPlayfieldId, AoVector3 Landing, ZoneTriggerVolume Trigger,
        AoQuaternion? Heading = null);

    /// <summary>
    /// Per-playfield static collision + soft zoning triggers (query-only Bepu world).
    /// </summary>
    public sealed class PlayfieldWorldSimulation : IDisposable
    {
        readonly BufferPool _pool;
        readonly Simulation _simulation;
        readonly TriggerVolumeCatalog _triggers = new();
        readonly DestinationsCatalog _destinations;
        readonly PlayfieldGeometryData _geometry;
        readonly IGameData _gameData;
        readonly IZoneLogger _logger;
        readonly Dictionary<int, PlayerTriggerState> _playerTriggerState = new();
        readonly Dictionary<int, double> _zoneGraceUntil = new();
        readonly Dictionary<long, LosCacheEntry> _losCache = new();
        readonly HashSet<int> _exitProxyDoors = new();
        readonly int _playfieldId;
        int _nextTriggerId = 1;
        bool _disposed;

        PlayfieldWorldSimulation(
            int playfieldId,
            BufferPool pool,
            Simulation simulation,
            PlayfieldGeometryData geometry,
            DestinationsCatalog destinations,
            IGameData gameData,
            IZoneLogger logger)
        {
            _playfieldId = playfieldId;
            _pool = pool;
            _simulation = simulation;
            _geometry = geometry;
            _destinations = destinations;
            _gameData = gameData;
            _logger = logger;
            Queries = new WorldQueries(simulation, pool);
        }

        public WorldQueries Queries { get; }

        public int HardStaticCount { get; private set; }

        public int WallTriggerCount => _triggers.WallTriggerCount;

        public int PortalTriggerCount => _triggers.PortalTriggerCount;

        public int ExitTriggerCount => _triggers.ExitTriggerCount;

        public static PlayfieldWorldSimulation Create(
            int playfieldId,
            PlayfieldGeometryData geometry,
            PlayfieldMetaData? meta,
            DestinationsCatalog destinations,
            IGameData gameData,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(geometry);
            ArgumentNullException.ThrowIfNull(destinations);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);

            var pool = new BufferPool();
            var simulation = Simulation.Create(
                pool,
                new NarrowPhaseCallbacks(),
                new PoseIntegratorCallbacks(),
                new SolveDescription(1, 1));

            var world = new PlayfieldWorldSimulation(
                playfieldId,
                pool,
                simulation,
                geometry,
                destinations,
                gameData,
                logger);
            int surfaceStatics = SurfaceCollisionBaker.BakeAll(geometry.Surface, pool, simulation);
            foreach (SurfaceResource cellSurface in geometry.CellSurfaces)
                surfaceStatics += SurfaceCollisionBaker.BakeAll(cellSurface, pool, simulation);

            int tileStatics = TileCollisionBaker.BakeAll(
                geometry.Tilemap,
                meta,
                pool,
                simulation,
                out TileBakeReport tiles);
            world.HardStaticCount = surfaceStatics + tileStatics;
            world.BakeWallTriggers(geometry.Walls);
            world.BakePortalTriggers(geometry.Dynels, playfieldId);
            world.BakeExitProxyTriggers(gameData.GetExitProxyDoorInstances(playfieldId));

            logger.Info(
                $"World bake playfield={playfieldId} terrain[{tiles}] surfaceStatics={surfaceStatics}"
                + $" wallTriggers={world.WallTriggerCount} portalTriggers={world.PortalTriggerCount}"
                + $" exitTriggers={world.ExitTriggerCount}");
            if (geometry.Tilemap != null && !tiles.Complete)
            {
                logger.Warn(
                    $"World bake playfield={playfieldId} baked only {tiles.ChunksBaked}/{tiles.ChunksExpected}"
                    + " terrain chunks; expect holes in the ground.");
            }

            if (surfaceStatics == 0)
            {
                logger.Warn(
                    $"World bake playfield={playfieldId} has no surface geometry;"
                    + " buildings, props and indoor floors will not collide."
                    + " Re-run RDBDataExtractor to produce Surfaces.dat.");
            }

            return world;
        }

        public bool HasLineOfSight(AoVector3 from, AoVector3 to)
        {
            long nowMs = Environment.TickCount64;
            // Quantize to ~0.25u so nearby spam hits the same entry.
            long key =
                (((long)(int)(from.x * 4f) & 0xFFFFL) << 48)
                | (((long)(int)(from.y * 4f) & 0xFFFFL) << 32)
                | (((long)(int)(from.z * 4f) & 0xFFFFL) << 16)
                | (((long)(int)(to.x * 4f) & 0xFFL) << 8)
                | (((long)(int)(to.z * 4f) & 0xFFL));

            if (_losCache.TryGetValue(key, out LosCacheEntry entry) && nowMs < entry.ExpireMs)
                return entry.Clear;

            bool clear = Queries.HasLineOfSight(
                new Vector3((float)from.x, (float)from.y, (float)from.z),
                new Vector3((float)to.x, (float)to.y, (float)to.z));
            _losCache[key] = new LosCacheEntry
            {
                Clear = clear,
                ExpireMs = nowMs + 250
            };

            if (_losCache.Count > 4096)
                _losCache.Clear();

            return clear;
        }

        /// <summary>
        /// Moves a character capsule from <paramref name="start"/> toward <paramref name="end"/>,
        /// both given at foot level, and reports how far it can legally travel.
        /// <paramref name="resolved"/> is always a position the capsule can occupy.
        /// </summary>
        public bool TryMoveCapsule(
            AoVector3 start,
            AoVector3 end,
            float radius,
            float halfHeight,
            float centerLift,
            float skin,
            out AoVector3 resolved,
            out AoVector3 normal)
        {
            resolved = end;
            normal = default;

            var from = new Vector3((float)start.x, (float)(start.y + centerLift), (float)start.z);
            var to = new Vector3((float)end.x, (float)(end.y + centerLift), (float)end.z);
            Vector3 delta = to - from;
            float length = delta.Length();
            if (length < 1e-6f)
                return false;

            if (!Queries.CapsuleSweep(from, to, radius, halfHeight, out float distance, out Vector3 hitNormal))
                return false;

            float travel = MathF.Max(0f, distance - skin);
            Vector3 direction = delta / length;
            Vector3 stopped = from + (direction * travel);
            resolved = new AoVector3(stopped.X, stopped.Y - centerLift, stopped.Z);
            normal = new AoVector3(hitNormal.X, hitNormal.Y, hitNormal.Z);
            return true;
        }

        /// <summary>Casts straight down and reports the surface height under <paramref name="origin"/>.</summary>
        public bool TryRaycastDown(AoVector3 origin, float maxDistance, out AoVector3 hitPosition)
        {
            hitPosition = origin;
            if (maxDistance <= 0f)
                return false;

            bool didHit = Queries.Raycast(
                new Vector3((float)origin.x, (float)origin.y, (float)origin.z),
                new Vector3(0f, -1f, 0f),
                maxDistance,
                out float t,
                out _);
            if (!didHit)
                return false;

            hitPosition = new AoVector3(origin.x, origin.y - t, origin.z);
            return true;
        }

        public void TickSoftTriggers(PlayfieldType playfield, double deltaTime)
        {
            ArgumentNullException.ThrowIfNull(playfield);
            double now = Environment.TickCount64 / 1000.0;

            foreach (Player player in playfield.GetRequiredService<DynelRegistry>().PlayerEntities())
            {
                if (player?.Session == null)
                    continue;

                int id = player.Identity.Instance;
                if (_zoneGraceUntil.TryGetValue(id, out double until) && now < until)
                    continue;

                float x = (float)player.Position.x;
                float y = (float)player.Position.y;
                float z = (float)player.Position.z;

                if (!_playerTriggerState.TryGetValue(id, out PlayerTriggerState? state))
                {
                    // First sighting: arriving through a door lands on top of that door, so adopt
                    // whatever the character already overlaps instead of firing it back at them.
                    state = new PlayerTriggerState();
                    _playerTriggerState[id] = state;
                    _triggers.CollectOverlapping(x, y, z, state.Overlapping);
                }

                float fromX = state.HasPrevious ? state.PreviousX : x;
                float fromZ = state.HasPrevious ? state.PreviousZ : z;
                state.PreviousX = x;
                state.PreviousZ = z;
                state.HasPrevious = true;

                _triggers.ClearOverlapOutside(x, y, z, state.Overlapping);

                if (TryResolveZoneCrossing(
                        fromX,
                        fromZ,
                        player.Position,
                        state.Overlapping,
                        ReadProxyReturn(player),
                        out ZoneCrossing crossing))
                {
                    TryTransfer(playfield, player, crossing, now);
                }
            }
        }

        /// <summary>
        /// Tests a movement against the zone triggers and resolves where it would land, without
        /// performing the transfer. <paramref name="overlappingIds"/> carries the caller's
        /// already-triggered set so lingering on a zone line does not re-fire, and
        /// <paramref name="returnTo"/> is the door they arrived through, which is the only thing an
        /// exit proxy has to go on.
        /// </summary>
        public bool TryResolveZoneCrossing(
            float fromX,
            float fromZ,
            AoVector3 position,
            HashSet<int> overlappingIds,
            ProxyReturn returnTo,
            out ZoneCrossing crossing)
        {
            ArgumentNullException.ThrowIfNull(overlappingIds);
            crossing = default;
            int destPlayfieldId;
            AoVector3 landing;

            float x = (float)position.x;
            float y = (float)position.y;
            float z = (float)position.z;
            if (!_triggers.TrySample(fromX, fromZ, x, y, z, overlappingIds, out ZoneTriggerHit hit))
                return false;

            landing = position;
            if (hit.Volume.Kind == ZoneTriggerKind.WallBorder)
            {
                if (WallZoneLandingResolver.TryResolve(
                        _destinations,
                        hit.Volume,
                        hit.Factor,
                        position,
                        out destPlayfieldId,
                        out landing))
                {
                    crossing = new ZoneCrossing(destPlayfieldId, landing, hit.Volume);
                    return true;
                }

                _logger.Warn(
                    "Wall trigger has no landing destPf="
                    + hit.Volume.DestPlayfieldId
                    + " destIdx="
                    + hit.Volume.DestIndex
                    + "; Destinations.dat for that playfield is missing or short.");
                return false;
            }

            if (hit.Volume.Kind == ZoneTriggerKind.PortalDynel)
            {
                destPlayfieldId = hit.Volume.DestPlayfieldId;
                if (TryResolvePortalLanding(hit.Volume, out landing, out var heading))
                {
                    crossing = new ZoneCrossing(destPlayfieldId, landing, hit.Volume, heading);
                    return true;
                }

                _logger.Warn(
                    "Portal door "
                    + hit.Volume.DynelInstance.ToString("X8", CultureInfo.InvariantCulture)
                    + " has no landing in playfield "
                    + destPlayfieldId
                    + (hit.Volume.LandingKind == PortalLandingKind.DoorDynel
                        ? "; no door dynel "
                          + hit.Volume.DestDoorInstance.ToString("X8", CultureInfo.InvariantCulture)
                          + " (Dynels.dat missing or stale)."
                        : "; no destination line "
                          + hit.Volume.DestIndex
                          + " (Destinations.dat missing or short)."));
                return false;
            }

            if (hit.Volume.Kind == ZoneTriggerKind.ExitProxy)
            {
                // No return recorded means the character never walked in through a proxy, so there
                // is nowhere to send them; legacy declines the same way rather than guessing.
                if (!returnTo.IsSet || !MatchesRecordedEntrance(returnTo, hit.Volume.DynelInstance))
                    return false;

                if (PortalDoorLandingResolver.TryResolveDoorLanding(
                        _gameData.GetPlayfieldGeometry(returnTo.PlayfieldId),
                        returnTo.DoorInstance,
                        PortalDoorLandingResolver.ExitDoorClearance,
                        out landing, out var heading))
                {
                    crossing = new ZoneCrossing(returnTo.PlayfieldId, landing, hit.Volume, heading);
                    return true;
                }

                _logger.Warn(
                    "Exit proxy door "
                    + hit.Volume.DynelInstance.ToString("X8", CultureInfo.InvariantCulture)
                    + " cannot return the character: playfield "
                    + returnTo.PlayfieldId
                    + " has no door dynel "
                    + returnTo.DoorInstance.ToString("X8", CultureInfo.InvariantCulture)
                    + ".");
                return false;
            }

            return false;
        }

        bool MatchesRecordedEntrance(ProxyReturn returnTo, int interiorDoor)
        {
            var doors = _gameData.GetPlayfieldGeometry(returnTo.PlayfieldId).Dynels?.Dynels;
            if (doors == null) return false;
            foreach (PlayfieldDynel door in doors)
            {
                if (door.IdentityType != (int)IdentityType.Door || door.IdentityInstance != returnTo.DoorInstance)
                    continue;
                // The global index also sees raw routes with no DAO override. Those may name
                // a different room's door; they cannot redirect this character's recorded entry.
                return PortalDoorLandingResolver.TryReadPortal(door, out var portal)
                    && portal.RecordsReturn && portal.Kind == PortalLandingKind.DoorDynel
                    && portal.PlayfieldId == _playfieldId && portal.DoorInstance == interiorDoor;
            }
            return false;
        }

        /// <summary>
        /// Turns the door a character just arrived through into a way back out. Legacy discovers
        /// these by scanning every playfield's proxies up front; geometry here loads lazily, so the
        /// door is also registered when someone walks in through it (idempotent with bake).
        /// </summary>
        public void RegisterExitProxyDoor(int doorInstance)
        {
            if (doorInstance == 0
                || !ExitProxyDoorCatalog.ShouldRegister(_playfieldId, doorInstance)
                || !_exitProxyDoors.Add(doorInstance))
                return;

            List<PlayfieldDynel>? dynels = _geometry.Dynels?.Dynels;
            if (dynels == null)
                return;

            for (int i = 0; i < dynels.Count; i++)
            {
                PlayfieldDynel d = dynels[i];
                if (d.IdentityInstance != doorInstance || d.IdentityType != (int)IdentityType.Door)
                    continue;

                // A door that already zones somewhere of its own accord keeps that behaviour.
                if (PortalDoorLandingResolver.TryReadPortal(d, out _))
                    return;

                float x = d.Position.X;
                float y = d.Position.Y;
                float z = d.Position.Z;
                const float r = TriggerVolumeCatalog.PortalRadius;
                const float h = TriggerVolumeCatalog.PortalHalfHeight;
                _triggers.Add(
                    new ZoneTriggerVolume
                    {
                        Kind = ZoneTriggerKind.ExitProxy,
                        Id = _nextTriggerId++,
                        MinX = x - r,
                        MaxX = x + r,
                        MinZ = z - r,
                        MaxZ = z + r,
                        MinY = y - h,
                        MaxY = y + h,
                        CenterX = x,
                        CenterY = y,
                        CenterZ = z,
                        Radius = r,
                        DynelInstance = doorInstance
                    });
                _logger.Info(
                    "Exit proxy registered playfield="
                    + _playfieldId
                    + " door="
                    + doorInstance.ToString("X8", CultureInfo.InvariantCulture));
                return;
            }
        }

        void BakeExitProxyTriggers(IReadOnlyList<int> doorInstances)
        {
            ArgumentNullException.ThrowIfNull(doorInstances);
            for (int i = 0; i < doorInstances.Count; i++)
                RegisterExitProxyDoor(doorInstances[i]);
        }

        bool TryResolvePortalLanding(ZoneTriggerVolume portal, out AoVector3 landing, out AoQuaternion? heading)
        {
            heading = null;
            if (portal.LandingKind == PortalLandingKind.DestinationLine)
            {
                return PortalDoorLandingResolver.TryResolveLineLanding(
                    _destinations,
                    portal.DestPlayfieldId,
                    portal.DestIndex,
                    out landing, out heading);
            }

            return PortalDoorLandingResolver.TryResolveDoorLanding(
                _gameData.GetPlayfieldGeometry(portal.DestPlayfieldId),
                portal.DestDoorInstance,
                portal.DoorClearance,
                out landing, out heading);
        }

        void TryTransfer(PlayfieldType source, Player player, ZoneCrossing crossing, double now)
        {
            int destPlayfieldId = crossing.DestPlayfieldId;
            if (destPlayfieldId <= 0 || destPlayfieldId == source.Identity.Instance)
                return;

            IZoneSession? session = player.Session;
            if (session == null)
                return;

            PlayfieldType destination = source.GetRequiredService<PlayfieldManager>()
                .GetOrCreate(destPlayfieldId);

            WriteProxyReturn(player, source.Identity.Instance, crossing.Trigger);
            if (crossing.Trigger.RecordsReturn
                && destination is ACGPlayfield acg
                && acg.World != null)
            {
                acg.World.RegisterExitProxyDoor(crossing.Trigger.DestDoorInstance);
            }

            int id = player.Identity.Instance;
            _zoneGraceUntil[id] = now + 3.0;

            // The character is leaving this world; a stale previous position would fake a crossing
            // if they come back, and the destination reseeds its own overlap set on first sighting.
            _playerTriggerState.Remove(id);
            _logger.Info(
                $"Zone trigger transfer character={id} from={source.Identity.Instance} to={destPlayfieldId}");

            // Door and explicit LineTeleport arrivals face along their landing clearance; borders keep
            // the character's current heading rather than imposing a global compass direction.
            if (crossing.Heading is { } heading)
                session.TransferToPlayfield(destination, crossing.Landing, heading);
            else
                session.TransferToPlayfield(destination, crossing.Landing);
        }

        static ProxyReturn ReadProxyReturn(Player player)
            => new()
            {
                PlayfieldId = player.Stats.GetOrZero(CharacterStat.ExternalPlayfieldInstance),
                DoorInstance = player.Stats.GetOrZero(CharacterStat.ExternalDoorInstance)
            };

        /// <summary>
        /// Remembers the door a proxy sent the character through so its exit can bring them back,
        /// and forgets any earlier one otherwise. Wall borders, destination lines and one-way
        /// proxies all clear it, so a stale door cannot pull a character across the world later.
        /// </summary>
        static void WriteProxyReturn(Player player, int sourcePlayfieldId, ZoneTriggerVolume trigger)
        {
            bool records = trigger.RecordsReturn;
            player.Stats.Set(
                CharacterStat.ExternalPlayfieldInstance,
                records ? sourcePlayfieldId : 0,
                StatDetail.Base,
                dirty: true);
            player.Stats.Set(
                CharacterStat.ExternalDoorInstance,
                records ? trigger.DynelInstance : 0,
                StatDetail.Base,
                dirty: true);
        }

        void BakeWallTriggers(PlayfieldWalls? walls)
        {
            if (walls?.WallGroups == null)
                return;

            for (int g = 0; g < walls.WallGroups.Count; g++)
            {
                PlayfieldWallGroup group = walls.WallGroups[g];
                if (group?.Walls == null || group.Walls.Count < 2)
                    continue;

                int n = group.Walls.Count;
                for (int i = 0; i < n; i++)
                {
                    PlayfieldWall a = group.Walls[i];
                    PlayfieldWall b = group.Walls[(i + 1) % n];
                    if (b.DestinationPlayfield <= 0)
                        continue;

                    // Bin AABB pad only; crossing is authoritative, proximity uses WallProximity.
                    float pad = TriggerVolumeCatalog.WallProximity;
                    float minX = MathF.Min(a.X, b.X) - pad;
                    float maxX = MathF.Max(a.X, b.X) + pad;
                    float minZ = MathF.Min(a.Z, b.Z) - pad;
                    float maxZ = MathF.Max(a.Z, b.Z) + pad;

                    _triggers.Add(
                        new ZoneTriggerVolume
                        {
                            Kind = ZoneTriggerKind.WallBorder,
                            Id = _nextTriggerId++,
                            MinX = minX,
                            MaxX = maxX,
                            MinZ = minZ,
                            MaxZ = maxZ,
                            SegAx = a.X,
                            SegAz = a.Z,
                            SegBx = b.X,
                            SegBz = b.Z,
                            DestPlayfieldId = b.DestinationPlayfield,
                            DestIndex = b.DestinationIndex
                        });
                }
            }
        }

        void BakePortalTriggers(PlayfieldDynels? dynels, int playfieldId)
        {
            if (dynels?.Dynels == null)
                return;

            for (int i = 0; i < dynels.Dynels.Count; i++)
            {
                PlayfieldDynel d = dynels.Dynels[i];
                if (!PortalDoorLandingResolver.TryReadPortal(d, out PortalDestination portal))
                    continue;

                // A LineTeleport with no playfield of its own lands back in this one.
                int destPlayfieldId = portal.PlayfieldId == 0 ? playfieldId : portal.PlayfieldId;

                float x = d.Position.X;
                float y = d.Position.Y;
                float z = d.Position.Z;
                const float r = TriggerVolumeCatalog.PortalRadius;
                const float h = TriggerVolumeCatalog.PortalHalfHeight;
                _triggers.Add(
                    new ZoneTriggerVolume
                    {
                        Kind = ZoneTriggerKind.PortalDynel,
                        Id = _nextTriggerId++,
                        MinX = x - r,
                        MaxX = x + r,
                        MinZ = z - r,
                        MaxZ = z + r,
                        MinY = y - h,
                        MaxY = y + h,
                        CenterX = x,
                        CenterY = y,
                        CenterZ = z,
                        Radius = r,
                        DynelInstance = d.IdentityInstance,
                        DestPlayfieldId = destPlayfieldId,
                        LandingKind = portal.Kind,
                        DestDoorInstance = portal.DoorInstance,
                        DoorClearance = portal.DoorClearance,
                        DestIndex = portal.DestinationIndex,
                        RecordsReturn = portal.RecordsReturn
                    });
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _losCache.Clear();
            _playerTriggerState.Clear();
            _zoneGraceUntil.Clear();
            _simulation.Dispose();
            _pool.Clear();
        }

        sealed class PlayerTriggerState
        {
            public HashSet<int> Overlapping { get; } = new();

            public float PreviousX { get; set; }

            public float PreviousZ { get; set; }

            public bool HasPrevious { get; set; }
        }

        struct LosCacheEntry
        {
            public bool Clear;
            public long ExpireMs;
        }

        struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
        {
            public void Initialize(Simulation simulation)
            {
            }

            public bool AllowContactGeneration(
                int workerIndex,
                CollidableReference a,
                CollidableReference b,
                ref float speculativeMargin)
                => false;

            public bool AllowContactGeneration(
                int workerIndex,
                CollidablePair pair,
                int childIndexA,
                int childIndexB)
                => false;

            public bool ConfigureContactManifold<TManifold>(
                int workerIndex,
                CollidablePair pair,
                ref TManifold manifold,
                out PairMaterialProperties pairMaterial)
                where TManifold : unmanaged, IContactManifold<TManifold>
            {
                pairMaterial = default;
                return false;
            }

            public bool ConfigureContactManifold(
                int workerIndex,
                CollidablePair pair,
                int childIndexA,
                int childIndexB,
                ref ConvexContactManifold manifold)
                => false;

            public void Dispose()
            {
            }
        }

        struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
        {
            public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

            public bool AllowSubstepsForUnconstrainedBodies => false;

            public bool IntegrateVelocityForKinematics => false;

            public void Initialize(Simulation simulation)
            {
            }

            public void PrepareForIntegration(float dt)
            {
            }

            public void IntegrateVelocity(
                System.Numerics.Vector<int> bodyIndices,
                Vector3Wide position,
                QuaternionWide orientation,
                BodyInertiaWide localInertia,
                System.Numerics.Vector<int> integrationMask,
                int workerIndex,
                System.Numerics.Vector<float> dt,
                ref BodyVelocityWide velocity)
            {
            }
        }
    }
}
