namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AODB.Common.RDBObjects;

    using AORebirth.Core.GameData;
    using AORebirth.World.Collision;

    using N3Lite;
    using N3Lite.Surfaces;

    using Utility;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    using EventType = AORebirth.Enums.EventType;
    using AoVector3 = AORebirth.Core.Vector.Vector3;
    using AoQuaternion = AORebirth.Core.Vector.Quaternion;
    using CharacterStat = SmokeLounge.AOtomation.Messaging.GameData.CharacterStat;
    using IdentityType = SmokeLounge.AOtomation.Messaging.GameData.IdentityType;
    using PlayfieldType = ZoneEngine_New.Core.Playfield.Playfield;

    /// <summary>A resolved zone transition: where it goes, and which trigger produced it.</summary>
    public readonly record struct ZoneCrossing(int DestPlayfieldId, AoVector3 Landing, ZoneTriggerVolume Trigger,
        AoQuaternion? Heading = null);

    /// <summary>
    /// Per-playfield static collision (the client's <c>Surface_i</c> world) + soft zoning triggers.
    /// </summary>
    public sealed class PlayfieldWorldSimulation : IDisposable
    {
        readonly TriggerVolumeCatalog _triggers = new();
        readonly DestinationsCatalog _destinations;
        readonly PlayfieldGeometryData _geometry;
        readonly IGameData _gameData;
        readonly IZoneLogger _logger;
        readonly Dictionary<int, PlayerTriggerState> _playerTriggerState = new();
        readonly Dictionary<int, double> _zoneGraceUntil = new();
        readonly Dictionary<int, LosCacheEntry> _losCache = new();
        readonly HashSet<int> _exitProxyDoors = new();
        readonly List<Door> _doors = new();
        readonly int _playfieldId;
        int _nextTriggerId = 1;
        bool _disposed;

        PlayfieldWorldSimulation(
            int playfieldId,
            PlayfieldSurface? surface,
            PlayfieldGeometryData geometry,
            DestinationsCatalog destinations,
            IGameData gameData,
            IZoneLogger logger)
        {
            _playfieldId = playfieldId;
            SurfaceData = surface;
            _geometry = geometry;
            _destinations = destinations;
            _gameData = gameData;
            _logger = logger;
        }

        /// <summary>The terrain and static meshes as built for the vehicles; null with no geometry.</summary>
        public PlayfieldSurface? SurfaceData { get; }

        /// <summary>What every <see cref="CharVehicleSim"/> in this playfield collides against.</summary>
        public ISurface? Surface => SurfaceData?.Root;

        /// <summary>Collision cells holding static geometry, plus the terrain when there is one.</summary>
        public int HardStaticCount =>
            (SurfaceData?.PopulatedCellCount ?? 0) + (SurfaceData?.Terrain != null ? 1 : 0);

        public int WallTriggerCount => _triggers.WallTriggerCount;

        public int PortalTriggerCount => _triggers.PortalTriggerCount;

        public int ExitTriggerCount => _triggers.ExitTriggerCount;

        public int VicinityTriggerCount => _triggers.VicinityTriggerCount;

        public int TeleportalTriggerCount => _triggers.TeleportalTriggerCount;

        public static PlayfieldWorldSimulation Create(
            int playfieldId,
            PlayfieldGeometryData geometry,
            PlayfieldMetaData? meta,
            DestinationsCatalog destinations,
            IGameData gameData,
            IZoneLogger logger,
            IItemTemplateCatalog? itemTemplates = null)
        {
            ArgumentNullException.ThrowIfNull(geometry);
            ArgumentNullException.ThrowIfNull(destinations);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);
            _ = meta;

            PlayfieldSurface? surface = PlayfieldSurfaceFactory.Build(geometry.Collision);
            var world = new PlayfieldWorldSimulation(
                playfieldId,
                surface,
                geometry,
                destinations,
                gameData,
                logger);
            world.BakeWallTriggers(geometry.Walls);
            world.BakePortalTriggers(geometry.Dynels, playfieldId);
            world.BakeVicinityTriggers(geometry.Dynels, itemTemplates);
            world.BakeMissionEntranceTriggers(geometry.Dynels);
            world.BakeExitDoorTriggers(geometry.Dynels, playfieldId);
            // Configured exits (ExitProxyDoors.json, else a dungeon's doors that open onto no room) are the way out
            // whichever door the entrances land on; without them every return-recording landing door is an exit.
            IReadOnlyCollection<int>? configuredExits = gameData.GetConfiguredExitProxyDoorInstances(playfieldId);
            world.BakeExitProxyTriggers(configuredExits != null
                ? [.. configuredExits]
                : gameData.GetExitProxyDoorInstances(playfieldId));
            world.BakeTeleportalTriggers(geometry.Collision?.Teleportals);

            int terrainChunks = surface?.Terrain != null ? geometry.Collision!.Terrain!.Chunks.Count : 0;
            logger.Info(
                $"World bake playfield={playfieldId} terrainChunks={terrainChunks}"
                + $" surfaceCells={surface?.PopulatedCellCount ?? 0} surfaceTriangles={surface?.TriangleCount ?? 0}"
                + $" wallTriggers={world.WallTriggerCount} portalTriggers={world.PortalTriggerCount}"
                + $" vicinityTriggers={world.VicinityTriggerCount} exitTriggers={world.ExitTriggerCount}"
                + $" teleportals={world.TeleportalTriggerCount}");
            if (surface != null && surface.OutsideTriangleCount > 0)
            {
                logger.Warn(
                    $"World bake playfield={playfieldId} dropped {surface.OutsideTriangleCount}"
                    + " surface triangles outside the collision cell grid.");
            }

            if ((surface?.PopulatedCellCount ?? 0) == 0)
            {
                logger.Warn(
                    $"World bake playfield={playfieldId} has no surface geometry;"
                    + " buildings, props and indoor floors will not collide."
                    + " Re-run RDBDataExtractor to produce Surfaces.dat.");
            }

            return world;
        }

        /// <summary>Makes a door block line of sight while it is closed.</summary>
        public void RegisterDoor(Door door) => _doors.Add(door);

        public bool HasLineOfSight(AoVector3 from, AoVector3 to)
            => HasLineOfSight((float)from.x, (float)from.y, (float)from.z, (float)to.x, (float)to.y, (float)to.z);

        /// <summary>Same, from coordinates: the hot path (every NPC engage and aggro check) allocates nothing.</summary>
        public bool HasLineOfSight(float fromX, float fromY, float fromZ, float toX, float toY, float toZ)
        {
            long nowMs = Environment.TickCount64;
            // Quantize to ~0.25u. Include all axes; the prior 8-bit to.xz key collided often.
            int key = HashCode.Combine(
                (int)(fromX * 4f),
                (int)(fromY * 4f),
                (int)(fromZ * 4f),
                (int)(toX * 4f),
                (int)(toY * 4f),
                (int)(toZ * 4f));

            if (_losCache.TryGetValue(key, out LosCacheEntry entry) && nowMs < entry.ExpireMs)
                return entry.Clear;

            bool clear = IsSegmentClear(new Vec3(fromX, fromY, fromZ), new Vec3(toX, toY, toZ));
            _losCache[key] = new LosCacheEntry
            {
                Clear = clear,
                ExpireMs = nowMs + 100
            };

            if (_losCache.Count > 4096)
                _losCache.Clear();

            return clear;
        }

        /// <summary>
        /// Places feet on the walkable surface under this position. The nearest upward-facing
        /// surface (floor, platform, building or terrain) from <see cref="MovementConfig.GroundProbeLift"/>
        /// above the feet down to <see cref="MovementConfig.GroundSnapTolerance"/> below wins.
        /// Feet buried under the terrain are lifted onto it: surfaces are one-sided, so a
        /// downward ray that starts under the ground never hits it.
        /// </summary>
        public bool TrySnapToFloor(AoVector3 feet, out AoVector3 floor)
        {
            floor = feet;
            float feetY = (float)feet.y;
            bool hasTerrain = false;
            float terrainY = 0f;
            TerrainHeightfield? terrain = _geometry.Collision?.Terrain;
            if (terrain != null
                && terrain.TryGetHeight((float)feet.x, (float)feet.z, out terrainY))
                hasTerrain = true;

            float originY = feetY + MovementConfig.GroundProbeLift;
            var origin = new Vec3((float)feet.x, originY, (float)feet.z);
            var end = new Vec3((float)feet.x, feetY - MovementConfig.GroundSnapTolerance, (float)feet.z);
            if (Surface != null && Surface.GetLineIntersection(origin, end, out Vec3 hit, out _, false, null))
            {
                floor = new AoVector3(feet.x, hit.Y, feet.z);
                return true;
            }

            if (hasTerrain && feetY <= terrainY + MovementConfig.GroundSnapTolerance)
            {
                floor = new AoVector3(feet.x, terrainY, feet.z);
                return true;
            }

            return false;
        }

        /// <summary>Casts straight down and reports the surface height under <paramref name="origin"/>.</summary>
        public bool TryRaycastDown(AoVector3 origin, float maxDistance, out AoVector3 hitPosition)
        {
            hitPosition = origin;
            if (maxDistance <= 0f)
                return false;

            if (Surface == null)
                return false;

            Vec3 start = ToVec3(origin);
            var end = new Vec3(start.X, start.Y - maxDistance, start.Z);
            if (!Surface.GetLineIntersection(start, end, out Vec3 hit, out _, false, null))
                return false;

            hitPosition = new AoVector3(origin.x, hit.Y, origin.z);
            return true;
        }

        /// <summary>
        /// Surfaces are one-sided, so the segment is tested both ways: a wall blocks sight
        /// from whichever side it is seen.
        /// </summary>
        bool IsSegmentClear(Vec3 from, Vec3 to)
        {
            for (int i = 0; i < _doors.Count; i++)
            {
                if (_doors[i].BlocksSegment(from.X, from.Y, from.Z, to.X, to.Y, to.Z))
                    return false;
            }

            if (Surface == null)
                return true;
            if ((to - from).LengthSquared < 1e-8f)
                return true;

            return !Surface.GetLineIntersection(from, to, out _, out _, false, null)
                && !Surface.GetLineIntersection(to, from, out _, out _, false, null);
        }

        static Vec3 ToVec3(AoVector3 v) => new((float)v.x, (float)v.y, (float)v.z);

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
                    if (crossing.Trigger.Kind == ZoneTriggerKind.TargetVicinity)
                        FireTargetVicinity(playfield, player, crossing.Trigger);
                    else if (crossing.Trigger.Kind == ZoneTriggerKind.MissionEntrance)
                        EnterMissionEntrance(playfield, player, crossing.Trigger);
                    else if (crossing.Trigger.Kind == ZoneTriggerKind.ExitDoor && crossing.DestPlayfieldId == 0)
                        TellExitDestinationUnknown(player, crossing.Trigger);
                    else
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

            if (LogUtil.HasDetail(DebugInfoDetail.Network))
            {
                _logger.Debug(string.Format(
                    CultureInfo.InvariantCulture,
                    "Zone trigger sampled kind={0} door={1:X8} from=({2:R},{3:R}) to=({4:R},{5:R},{6:R}) returnPf={7} returnDoor={8:X8}",
                    hit.Volume.Kind,
                    hit.Volume.DynelInstance,
                    fromX,
                    fromZ,
                    x,
                    y,
                    z,
                    returnTo.PlayfieldId,
                    returnTo.DoorInstance));
            }

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

            if (hit.Volume.Kind is ZoneTriggerKind.TargetVicinity or ZoneTriggerKind.MissionEntrance)
            {
                crossing = new ZoneCrossing(0, position, hit.Volume);
                return true;
            }

            if (hit.Volume.Kind == ZoneTriggerKind.Teleportal)
            {
                destPlayfieldId = hit.Volume.DestPlayfieldId;
                if (PortalDoorLandingResolver.TryResolveMidpointLanding(
                        _destinations,
                        destPlayfieldId,
                        hit.Volume.DestIndex,
                        out landing,
                        out var teleportalHeading))
                {
                    crossing = new ZoneCrossing(destPlayfieldId, landing, hit.Volume, teleportalHeading);
                    return true;
                }

                _logger.Warn(
                    "Teleportal has no landing destPf="
                    + destPlayfieldId
                    + " destIdx="
                    + hit.Volume.DestIndex
                    + "; Destinations.dat for that playfield is missing or short.");
                return false;
            }

            if (hit.Volume.Kind == ZoneTriggerKind.DungeonExit && hit.Volume.Landing is AoVector3 outside)
            {
                crossing = new ZoneCrossing(hit.Volume.DestPlayfieldId, outside, hit.Volume, hit.Volume.LandingHeading);
                return true;
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

            if (hit.Volume.Kind == ZoneTriggerKind.ExitDoor)
            {
                // Back out through the entrance the character recorded, as an exit proxy would; with none, the
                // destination is unknown and the caller says so (DestPlayfieldId 0).
                if (returnTo.IsSet
                    && MatchesRecordedEntrance(returnTo, hit.Volume.DynelInstance)
                    && PortalDoorLandingResolver.TryResolveDoorLanding(
                        _gameData.GetPlayfieldGeometry(returnTo.PlayfieldId),
                        returnTo.DoorInstance,
                        PortalDoorLandingResolver.ExitDoorClearance,
                        out landing, out var returnHeading))
                {
                    crossing = new ZoneCrossing(returnTo.PlayfieldId, landing, hit.Volume, returnHeading);
                    return true;
                }

                crossing = new ZoneCrossing(0, position, hit.Volume);
                return true;
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

        /// <summary>
        /// True when the character's recorded entrance is a real door in the recorded playfield, so any exit here may send
        /// them back out through it. The record itself is the proof: only a return-recording proxy writes it, at the
        /// moment it moves the character into a playfield, and every other way of changing playfield clears it (GM
        /// jumps, /stuck, teleport items, cross-playfield death respawn).
        /// Re-deriving the entrance from the door's walk-in portal data failed for doors that teleport from their
        /// OnTargetInVicinity event (655 door C000028F into 1702, 566 door C0070236 into 1137): those carry no walk-in
        /// portal, so the character was told the exit's destination is unknown although the return was recorded.
        /// </summary>
        bool MatchesRecordedEntrance(ProxyReturn returnTo, int interiorDoor)
        {
            _ = interiorDoor;
            var doors = _gameData.GetPlayfieldGeometry(returnTo.PlayfieldId).Dynels?.Dynels;
            if (doors == null) return false;
            foreach (PlayfieldDynel door in doors)
            {
                if (door.IdentityType == (int)IdentityType.Door && door.IdentityInstance == returnTo.DoorInstance)
                    return true;
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
                || _triggers.HasDynel(ZoneTriggerKind.TargetVicinity, doorInstance)
                || _triggers.HasDynel(ZoneTriggerKind.ExitDoor, doorInstance)
                || _triggers.HasDynel(ZoneTriggerKind.PortalDynel, doorInstance)
                || !ExitProxyDoorCatalog.ShouldRegister(doorInstance, _gameData.GetConfiguredExitProxyDoorInstances(_playfieldId))
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
                var volume = new ZoneTriggerVolume
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
                };

                // A teleports route on the exit sends everyone to that door instead of back through their entrance
                // (an entrance that records no return, such as 800's into 125, leaves nothing to go back to).
                ApplyClientSphere(volume, d, x, y, z, DoorRotation(d));
                bool routed = TryApplyDoorRoute(volume, d.IdentityInstance);
                _triggers.Add(volume);
                _logger.Info(
                    (routed ? "Routed exit registered playfield=" : "Exit proxy registered playfield=")
                    + _playfieldId
                    + " door="
                    + doorInstance.ToString("X8", CultureInfo.InvariantCulture)
                    + (routed
                        ? " to=" + volume.DestPlayfieldId.ToString(CultureInfo.InvariantCulture) + "/"
                          + volume.DestDoorInstance.ToString("X8", CultureInfo.InvariantCulture)
                        : string.Empty));
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

            if (portal.LandingKind == PortalLandingKind.DestinationMidpoint)
            {
                return PortalDoorLandingResolver.TryResolveMidpointLanding(
                    _destinations,
                    portal.DestPlayfieldId,
                    portal.DestIndex,
                    out landing, out heading);
            }

            // Into a dungeon, an arrival aimed at an inner door lands on the dungeon's entrance instead, as the
            // client's GetEntranceDoor falls back to its first entrance door.
            return PortalDoorLandingResolver.TryResolveDoorLanding(
                _gameData.GetPlayfieldGeometry(portal.DestPlayfieldId),
                DungeonExitDoors.LandingDoor(portal.DestDoorInstance, _gameData.GetDungeonExitDoors(portal.DestPlayfieldId)),
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

            int id = player.Identity.Instance;
            _zoneGraceUntil[id] = now + ZoneGrace.OnZone(id, now, source.Identity.Instance, destPlayfieldId);

            // The character is leaving this world; a stale previous position would fake a crossing
            // if they come back, and the destination reseeds its own overlap set on first sighting.
            _playerTriggerState.Remove(id);

            // An unloaded destination builds in the background; the crossing completes on this player's tick
            // once it is ready, and only if they are still here with a session.
            source.GetRequiredService<PlayfieldManager>().WithPlayfield(destPlayfieldId, player, destination =>
            {
                if (!ReferenceEquals(player.Playfield, source) || player.Session is not IZoneSession current)
                    return;

                WriteProxyReturn(player, source.Identity.Instance, crossing.Trigger);
                if (crossing.Trigger.RecordsReturn
                    && destination is ACGPlayfield acg
                    && acg.World != null)
                {
                    acg.World.RegisterExitProxyDoor(crossing.Trigger.DestDoorInstance);
                }

                _logger.Info(
                    $"Zone trigger transfer character={id} from={source.Identity.Instance} to={destPlayfieldId}");

                // Door and explicit LineTeleport arrivals face along their landing clearance; borders keep
                // the character's current heading rather than imposing a global compass direction.
                if (crossing.Heading is { } heading)
                    current.TransferToPlayfield(destination, crossing.Landing, heading);
                else
                    current.TransferToPlayfield(destination, crossing.Landing);
            });
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

        /// <summary>
        /// Zone surface teleportals (AODB SurfaceResource.Teleportal, gathered by the collision loader): each becomes
        /// an area trigger into its destination playfield's destination line.
        /// </summary>
        void BakeTeleportalTriggers(IReadOnlyList<AODB.Common.RDBObjects.SurfaceTeleportal>? teleportals)
        {
            if (teleportals == null)
                return;

            foreach (AODB.Common.RDBObjects.SurfaceTeleportal teleportal in teleportals)
            {
                if (teleportal.Area.Count < 3 || teleportal.DestinationPlayfield <= 0
                    || teleportal.DestinationLine < 1 || teleportal.DestinationLine > byte.MaxValue)
                    continue;

                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (AODB.Common.Structs.Vector3 point in teleportal.Area)
                {
                    minX = MathF.Min(minX, point.X);
                    maxX = MathF.Max(maxX, point.X);
                    minZ = MathF.Min(minZ, point.Z);
                    maxZ = MathF.Max(maxZ, point.Z);
                }

                _triggers.Add(
                    new ZoneTriggerVolume
                    {
                        Kind = ZoneTriggerKind.Teleportal,
                        Id = _nextTriggerId++,
                        MinX = minX,
                        MaxX = maxX,
                        MinZ = minZ,
                        MaxZ = maxZ,
                        DestPlayfieldId = teleportal.DestinationPlayfield,
                        DestIndex = (byte)teleportal.DestinationLine,
                        Teleportal = teleportal
                    });
            }
        }

        /// <summary>
        /// Doors the client treats as exits: their placement blob sets ExitInstance (the client then registers them as
        /// entrance/exit doors and starts a zone change on contact). The destination is not in the data, so it comes
        /// from the teleports table: a routed door becomes an ordinary door-to-door portal; an unrouted one becomes an
        /// <see cref="ZoneTriggerKind.ExitDoor"/>. Doors that already zone by their own events are left alone.
        /// </summary>
        void BakeExitDoorTriggers(PlayfieldDynels? dynels, int playfieldId)
        {
            if (dynels?.Dynels == null)
                return;

            foreach (PlayfieldDynel d in dynels.Dynels)
            {
                // Only the door's own events count: a dynel instance is unique per identity type, and a Terminal
                // (4313's lift pad) can share this door's number.
                if (d.IdentityType != (int)IdentityType.Door
                    || PortalDoorLandingResolver.TryReadPortal(d, out _)
                    || ExitInstanceOf(d) <= 0)
                    continue;

                float x = d.Position.X;
                float y = d.Position.Y;
                float z = d.Position.Z;
                const float r = TriggerVolumeCatalog.PortalRadius;
                const float h = TriggerVolumeCatalog.PortalHalfHeight;
                var volume = new ZoneTriggerVolume
                {
                    Kind = ZoneTriggerKind.ExitDoor,
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
                    DynelInstance = d.IdentityInstance
                };

                ApplyClientSphere(volume, d, x, y, z, DoorRotation(d));
                TryApplyDoorRoute(volume, d.IdentityInstance);
                _triggers.Add(volume);
            }
        }

        /// <summary>
        /// A teleports route on an exit door turns it into an ordinary door-to-door portal to the routed door. Only
        /// routes to a door of the destination playfield (0xC0nnPPPP with PPPP the playfield) are supported.
        /// </summary>
        bool TryApplyDoorRoute(ZoneTriggerVolume volume, int doorInstance)
        {
            if (!_gameData.TryGetTeleportRoute(
                    _playfieldId,
                    (int)IdentityType.Door,
                    unchecked((uint)doorInstance),
                    out int routedPlayfield,
                    out int routedType,
                    out uint routedInstance))
                return false;

            if (routedType != (int)IdentityType.Door || routedPlayfield <= 0 || routedPlayfield > 0xFFFF
                || (routedInstance & 0xFF000000u) != 0xC0000000u
                || (routedInstance & 0xFFFFu) != (uint)routedPlayfield)
            {
                _logger.Warn(
                    "Exit door route ignored: unsupported target playfield=" + _playfieldId
                    + " door=" + doorInstance.ToString("X8", CultureInfo.InvariantCulture)
                    + " destinationPf=" + routedPlayfield + " destinationType=" + routedType);
                return false;
            }

            volume.Kind = ZoneTriggerKind.PortalDynel;
            volume.DestPlayfieldId = routedPlayfield;
            volume.LandingKind = PortalLandingKind.DoorDynel;
            volume.DestDoorInstance = unchecked((int)routedInstance);
            volume.DoorClearance = PortalDoorLandingResolver.ExitDoorClearance;
            return true;
        }

        /// <summary>Mesh (12) and Scale (360) a placed dynel's own stats set; 0 for each it does not set.</summary>
        static void PlacedLook(PlayfieldDynel dynel, out int mesh, out int scale)
        {
            mesh = 0;
            scale = 0;
            if (dynel.Blob == null || dynel.Blob.Length <= 12)
                return;

            try
            {
                AODB.Common.RDBObjects.DynelBlob blob = new AODB.Common.RDBObjects.ItemBase().ReadDynelBlob(dynel.Blob);
                foreach (KeyValuePair<int, int> stat in blob.AppliedStats)
                {
                    if (stat.Key == (int)CharacterStat.Mesh)
                        mesh = stat.Value;
                    else if (stat.Key == (int)CharacterStat.Scale)
                        scale = stat.Value;
                }
            }
            catch (Exception)
            {
                // An unreadable blob keeps the template's look.
            }
        }

        /// <summary>ExitInstance (189) set by a door's placement blob; 0 when it has none or the blob does not read.</summary>
        static int ExitInstanceOf(PlayfieldDynel door)
        {
            if (door.Blob == null || door.Blob.Length <= 12)
                return 0;

            try
            {
                AODB.Common.RDBObjects.DynelBlob blob = new AODB.Common.RDBObjects.ItemBase().ReadDynelBlob(door.Blob);
                foreach (KeyValuePair<int, int> stat in blob.AppliedStats)
                {
                    if (stat.Key == (int)CharacterStat.ExitInstance)
                        return stat.Value;
                }
            }
            catch (Exception)
            {
                // An unreadable blob is not an exit.
            }

            return 0;
        }

        void TellExitDestinationUnknown(Player player, ZoneTriggerVolume door)
        {
            player.Session?.Send(new SmokeLounge.AOtomation.Messaging.Messages.N3Messages.ChatTextMessage
            {
                Identity = player.Identity,
                Text = "This exit's destination is unknown (door "
                       + door.DynelInstance.ToString("X8", CultureInfo.InvariantCulture)
                       + " on playfield " + _playfieldId + " has no teleports route)."
            });
            _logger.Warn(
                "Exit door has no destination: playfield=" + _playfieldId
                + " door=" + door.DynelInstance.ToString("X8", CultureInfo.InvariantCulture)
                + " character=" + player.Identity.Instance);
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

        /// <summary>
        /// Drops a character's trigger memory and ignores further triggers briefly. A line teleport
        /// calls this so the landing pad does not immediately fire, and so the source pad cannot
        /// fire again while a playfield transfer is still in flight.
        /// </summary>
        public void ForgetCharacterTriggers(int characterId)
        {
            DropCharacterTriggerMemory(characterId);
            double now = Environment.TickCount64 / 1000.0;
            _zoneGraceUntil[characterId] = now + ZoneGrace.Current(characterId, now);
        }

        /// <summary>
        /// How long zone triggers ignore a character after a zone, per character across playfields. The first zone gets
        /// a short grace so the character can turn straight back out (the client freezes a walk-in it thinks will zone
        /// until the server answers). Only bouncing back and forth between the same two playfields builds a streak:
        /// past <see cref="FreeZones"/> such zones, each within <see cref="ResetSeconds"/> of the last, the grace doubles,
        /// which breaks an accidental door loop. Travelling on through different playfields never builds one.
        /// </summary>
        static class ZoneGrace
        {
            const double BaseSeconds = 0.5;
            const double MaxSeconds = 4.0;
            const double ResetSeconds = 10.0;
            const int FreeZones = 3;

            static readonly object Sync = new();
            static readonly Dictionary<int, (double Grace, double LastZone, int Streak, long Pair)> State = new();

            /// <summary>Records a zone from <paramref name="fromPlayfield"/> to <paramref name="toPlayfield"/> and returns the grace it earns.</summary>
            public static double OnZone(int characterId, double now, int fromPlayfield, int toPlayfield)
            {
                long pair = ((long)Math.Min(fromPlayfield, toPlayfield) << 32) | (uint)Math.Max(fromPlayfield, toPlayfield);
                lock (Sync)
                {
                    int streak = State.TryGetValue(characterId, out var last)
                        && last.Pair == pair
                        && now - last.LastZone < ResetSeconds
                        ? last.Streak + 1
                        : 1;
                    double grace = streak <= FreeZones
                        ? BaseSeconds
                        : Math.Min(BaseSeconds * Math.Pow(2.0, streak - FreeZones), MaxSeconds);
                    State[characterId] = (grace, now, streak, pair);
                    return grace;
                }
            }

            /// <summary>The grace from the character's latest zone, or the base grace once it has lapsed.</summary>
            public static double Current(int characterId, double now)
            {
                lock (Sync)
                {
                    if (!State.TryGetValue(characterId, out var last))
                        return BaseSeconds;
                    if (now - last.LastZone >= ResetSeconds)
                    {
                        State.Remove(characterId);
                        return BaseSeconds;
                    }

                    return last.Grace;
                }
            }
        }

        /// <summary>
        /// Forgets where the character last stood on this playfield. A transfer must sample from
        /// the landing alone: a segment drawn from the spot they occupied before they left will
        /// cross the ring they just used and send them straight back.
        /// </summary>
        public void DropCharacterTriggerMemory(int characterId)
        {
            _playerTriggerState.Remove(characterId);
        }

        void FireTargetVicinity(PlayfieldType playfield, Player player, ZoneTriggerVolume pad)
        {
            ItemTemplate? events = pad.VicinityEvents;
            if (events == null || player.Session == null)
                return;

            events.ExecuteSpells(
                EventType.OnTargetInVicinity,
                player,
                playfield.GetRequiredService<IInventoryRepository>(),
                playfield.GetRequiredService<IItemBuilder>(),
                new SpellCriteria { SourceDoorInstance = pad.DynelInstance });
        }

        void BakeVicinityTriggers(PlayfieldDynels? dynels, IItemTemplateCatalog? itemTemplates)
        {
            if (dynels?.Dynels == null)
                return;

            for (int i = 0; i < dynels.Dynels.Count; i++)
            {
                PlayfieldDynel d = dynels.Dynels[i];
                // Placement spells win. Jobe teleporting rings leave the placement empty and keep
                // OnTargetInVicinity Teleport on the item template.
                ItemTemplate events = DynelEventSpells.WithOnUseFromDynel(
                    new ItemTemplate { Id = d.TemplateId },
                    d);
                if (!events.SpellList.ContainsKey(EventType.OnTargetInVicinity)
                    && itemTemplates != null
                    && itemTemplates.TryGet(d.TemplateId, out ItemTemplate? fromTemplate)
                    && fromTemplate.SpellList.ContainsKey(EventType.OnTargetInVicinity))
                    events = fromTemplate;

                if (!events.SpellList.ContainsKey(EventType.OnTargetInVicinity))
                    continue;

                float x = d.Position.X;
                float y = d.Position.Y;
                float z = d.Position.Z;
                float r = TriggerVolumeCatalog.TargetVicinityRadius;
                if (events.Stats.TryGetValue(CharacterStat.VicinityRange, out int vicinityRange) && vicinityRange > 0)
                    r = vicinityRange;
                var pad = new ZoneTriggerVolume
                {
                    Kind = ZoneTriggerKind.TargetVicinity,
                    Id = _nextTriggerId++,
                    MinX = x - r,
                    MaxX = x + r,
                    MinZ = z - r,
                    MaxZ = z + r,
                    CenterX = x,
                    CenterY = y,
                    CenterZ = z,
                    Radius = r,
                    DynelInstance = d.IdentityInstance,
                    VicinityEvents = events
                };
                float h = TriggerVolumeCatalog.HalfHeight(pad);
                pad.MinY = y - h;
                pad.MaxY = y + h;
                _triggers.Add(pad);
            }
        }

        /// <summary>
        /// Makes a quest dungeon's exit door a walk-out: stepping into it returns the character to
        /// <paramref name="landing"/> on <paramref name="exteriorPlayfieldId"/>, outside the ACG entrance. It records no
        /// way back (the entrance, with a key, is the way back in).
        /// </summary>
        public void RegisterDungeonExit(float x, float y, float z, int doorInstance, int exteriorPlayfieldId, AoVector3 landing, AoQuaternion landingHeading,
            int templateId = 0, AoQuaternion? doorRotation = null)
        {
            if (exteriorPlayfieldId <= 0 || _triggers.HasDynel(ZoneTriggerKind.DungeonExit, doorInstance))
                return;

            const float r = TriggerVolumeCatalog.PortalRadius;
            const float h = TriggerVolumeCatalog.PortalHalfHeight;
            var exit =
                new ZoneTriggerVolume
                {
                    Kind = ZoneTriggerKind.DungeonExit,
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
                    DynelInstance = doorInstance,
                    DestPlayfieldId = exteriorPlayfieldId,
                    Landing = landing,
                    LandingHeading = landingHeading
                };
            ApplyClientSphere(exit, templateId, x, y, z, doorRotation);
            _triggers.Add(exit);
        }

        /// <summary>
        /// Gives a placed dynel's walk-in trigger the client's shape: its template's collision sphere against the player's
        /// (GameData DynelCollision.json). Templates without one keep the default disc.
        /// </summary>
        void ApplyClientSphere(ZoneTriggerVolume volume, PlayfieldDynel placed, float x, float y, float z, AoQuaternion? doorRotation = null)
        {
            PlacedLook(placed, out int mesh, out int scale);
            ApplyClientSphere(volume, placed.TemplateId, x, y, z, doorRotation, mesh, scale);
        }

        void ApplyClientSphere(ZoneTriggerVolume volume, int templateId, float x, float y, float z, AoQuaternion? doorRotation = null,
            int placedMesh = 0, int placedScale = 0)
        {
            if (templateId <= 0
                || !_gameData.TryGetDynelCollisionSphere(templateId, placedMesh, placedScale, out float radius, out float centerY)
                || _gameData.PlayerCollisionSphere is not { } player)
                return;

            TriggerVolumeCatalog.MakeClientSphere(volume, x, y, z, radius, centerY, player.Radius, player.CenterY);
            if (doorRotation != null)
                TriggerVolumeCatalog.SetDoorPlane(volume, x, y, z, doorRotation);
        }

        static AoQuaternion DoorRotation(PlayfieldDynel d)
            => new AoQuaternion(d.Heading.X, d.Heading.Y, d.Heading.Z, d.Heading.W);

        /// <summary>
        /// Walking into a mission entrance. The quest dungeon service decides everything (carried key, the quest
        /// behind this entrance, range, cooldown); without a matching key nothing happens and the character walks on.
        /// The overlap memory keeps it from firing again until they step out and back in.
        /// </summary>
        void EnterMissionEntrance(PlayfieldType playfield, Player player, ZoneTriggerVolume entrance)
        {
            try
            {
                playfield.GetService<Quests.Dungeons.QuestDungeonService>()?.TryEnter(
                    player, new SmokeLounge.AOtomation.Messaging.GameData.Identity { Type = IdentityType.MissionEntrance, Instance = entrance.DynelInstance });
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Mission entrance " + entrance.DynelInstance.ToString("X8", CultureInfo.InvariantCulture) + " entry failed");
            }
        }

        /// <summary>One walk-in trigger per MissionEntrance dynel (0xDAC6) in this playfield's Dynels.dat.</summary>
        void BakeMissionEntranceTriggers(PlayfieldDynels? dynels)
        {
            if (dynels?.Dynels == null)
                return;

            for (int i = 0; i < dynels.Dynels.Count; i++)
            {
                PlayfieldDynel d = dynels.Dynels[i];
                if (d.IdentityType != (int)IdentityType.MissionEntrance)
                    continue;

                float x = d.Position.X;
                float y = d.Position.Y;
                float z = d.Position.Z;
                const float r = TriggerVolumeCatalog.MissionEntranceRadius;
                const float h = TriggerVolumeCatalog.PortalHalfHeight;
                var entrance =
                    new ZoneTriggerVolume
                    {
                        Kind = ZoneTriggerKind.MissionEntrance,
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
                        DynelInstance = d.IdentityInstance
                    };
                ApplyClientSphere(entrance, d, x, y, z);
                _triggers.Add(entrance);
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
                var door =
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
                    };
                ApplyClientSphere(door, d, x, y, z,
                    d.IdentityType == (int)IdentityType.Door ? DoorRotation(d) : null);
                _triggers.Add(door);
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
    }
}
