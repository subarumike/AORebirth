namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;
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
    using PlayfieldType = ZoneEngine_New.Core.Playfield.Playfield;

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
        readonly IZoneLogger _logger;
        readonly Dictionary<int, PlayerTriggerState> _playerTriggerState = new();
        readonly Dictionary<int, double> _zoneGraceUntil = new();
        readonly Dictionary<long, LosCacheEntry> _losCache = new();
        int _nextTriggerId = 1;
        bool _disposed;

        PlayfieldWorldSimulation(
            BufferPool pool,
            Simulation simulation,
            PlayfieldGeometryData geometry,
            DestinationsCatalog destinations,
            IZoneLogger logger)
        {
            _pool = pool;
            _simulation = simulation;
            _geometry = geometry;
            _destinations = destinations;
            _logger = logger;
            Queries = new WorldQueries(simulation, pool);
        }

        public WorldQueries Queries { get; }

        public int HardStaticCount { get; private set; }

        public int WallTriggerCount => _triggers.WallTriggerCount;

        public int PortalTriggerCount => _triggers.PortalTriggerCount;

        public static PlayfieldWorldSimulation Create(
            int playfieldId,
            PlayfieldGeometryData geometry,
            PlayfieldMetaData? meta,
            DestinationsCatalog destinations,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(geometry);
            ArgumentNullException.ThrowIfNull(destinations);
            ArgumentNullException.ThrowIfNull(logger);

            var pool = new BufferPool();
            var simulation = Simulation.Create(
                pool,
                new NarrowPhaseCallbacks(),
                new PoseIntegratorCallbacks(),
                new SolveDescription(1, 1));

            var world = new PlayfieldWorldSimulation(pool, simulation, geometry, destinations, logger);
            world.HardStaticCount =
                SurfaceCollisionBaker.BakeAll(geometry.Surface, pool, simulation)
                + TileCollisionBaker.BakeAll(geometry.Tilemap, meta, pool, simulation);
            world.BakeWallTriggers(geometry.Walls);
            world.BakePortalTriggers(geometry.Dynels);
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

        public bool TryCapsuleSweep(
            AoVector3 start,
            AoVector3 end,
            float radius,
            float halfHeight,
            out AoVector3 hitPosition)
        {
            bool didHit = Queries.CapsuleSweep(
                new Vector3((float)start.x, (float)start.y, (float)start.z),
                new Vector3((float)end.x, (float)end.y, (float)end.z),
                radius,
                halfHeight,
                out Vector3 hitPos);
            hitPosition = didHit
                ? new AoVector3(hitPos.X, hitPos.Y, hitPos.Z)
                : end;
            return didHit;
        }

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
                    state = new PlayerTriggerState();
                    _playerTriggerState[id] = state;
                }

                _triggers.ClearOverlapOutside(x, y, z, state.Overlapping);

                if (!_triggers.TrySample(x, y, z, state.Overlapping, out ZoneTriggerHit hit))
                    continue;

                if (hit.Volume.Kind == ZoneTriggerKind.WallBorder)
                {
                    if (!WallZoneLandingResolver.TryResolve(
                            _destinations,
                            hit.Volume,
                            hit.Factor,
                            player.Position,
                            out int destPf,
                            out AoVector3 landing))
                    {
                        _logger.Warn(
                            $"Wall trigger landing missing character={id} destPf={hit.Volume.DestPlayfieldId} destIdx={hit.Volume.DestIndex}");
                        continue;
                    }

                    TryTransfer(playfield, player, destPf, landing, now);
                    continue;
                }

                if (hit.Volume.Kind == ZoneTriggerKind.PortalDynel)
                {
                    PlayfieldDynel? dynel = FindDynel(hit.Volume.DynelInstance);
                    if (dynel == null)
                        continue;

                    PlayfieldDoors? destDoors = null;
                    if (PortalDoorLandingResolver.TryResolve(
                            dynel,
                            _geometry.Doors,
                            destDoors,
                            out int destPf,
                            out AoVector3 landing))
                        TryTransfer(playfield, player, destPf, landing, now);
                }
            }
        }

        void TryTransfer(
            PlayfieldType source,
            Player player,
            int destPlayfieldId,
            AoVector3 landing,
            double now)
        {
            if (destPlayfieldId <= 0 || destPlayfieldId == source.Identity.Instance)
                return;

            IZoneSession? session = player.Session;
            if (session == null)
                return;

            PlayfieldType destination = source.GetRequiredService<PlayfieldManager>()
                .GetOrCreate(destPlayfieldId);

            _zoneGraceUntil[player.Identity.Instance] = now + 3.0;
            _logger.Info(
                $"Zone trigger transfer character={player.Identity.Instance} from={source.Identity.Instance} to={destPlayfieldId}");

            session.TransferToPlayfield(destination, landing);
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

                    float minX = MathF.Min(a.X, b.X) - 2f;
                    float maxX = MathF.Max(a.X, b.X) + 2f;
                    float minZ = MathF.Min(a.Z, b.Z) - 2f;
                    float maxZ = MathF.Max(a.Z, b.Z) + 2f;

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

        void BakePortalTriggers(PlayfieldDynels? dynels)
        {
            if (dynels?.Dynels == null)
                return;

            for (int i = 0; i < dynels.Dynels.Count; i++)
            {
                PlayfieldDynel d = dynels.Dynels[i];
                if (!PortalDoorLandingResolver.IsZoningCapable(d))
                    continue;

                float x = d.Position.X;
                float y = d.Position.Y;
                float z = d.Position.Z;
                const float r = 2f;
                _triggers.Add(
                    new ZoneTriggerVolume
                    {
                        Kind = ZoneTriggerKind.PortalDynel,
                        Id = _nextTriggerId++,
                        MinX = x - r,
                        MaxX = x + r,
                        MinZ = z - r,
                        MaxZ = z + r,
                        MinY = y - 6f,
                        MaxY = y + 6f,
                        CenterX = x,
                        CenterY = y,
                        CenterZ = z,
                        Radius = r,
                        DynelInstance = d.IdentityInstance
                    });
            }
        }

        PlayfieldDynel? FindDynel(int instance)
        {
            if (_geometry.Dynels?.Dynels == null)
                return null;

            for (int i = 0; i < _geometry.Dynels.Dynels.Count; i++)
            {
                PlayfieldDynel d = _geometry.Dynels.Dynels[i];
                if (d.IdentityInstance == instance)
                    return d;
            }

            return null;
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
