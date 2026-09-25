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
            world.BakeExitProxyTriggers(gameData.GetExitProxyDoorInstances(playfieldId));

            int terrainChunks = surface?.Terrain != null ? geometry.Collision!.Terrain!.Chunks.Count : 0;
            logger.Info(
                $"World bake playfield={playfieldId} terrainChunks={terrainChunks}"
                + $" surfaceCells={surface?.PopulatedCellCount ?? 0} surfaceTriangles={surface?.TriangleCount ?? 0}"
                + $" wallTriggers={world.WallTriggerCount} portalTriggers={world.PortalTriggerCount}"
                + $" vicinityTriggers={world.VicinityTriggerCount} exitTriggers={world.ExitTriggerCount}");
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

        public bool HasLineOfSight(AoVector3 from, AoVector3 to)
        {
            long nowMs = Environment.TickCount64;
            // Quantize to ~0.25u. Include all axes â€” the prior 8-bit to.xz key collided often.
            int key = HashCode.Combine(
                (int)(from.x * 4f),
                (int)(from.y * 4f),
                (int)(from.z * 4f),
                (int)(to.x * 4f),
                (int)(to.y * 4f),
                (int)(to.z * 4f));

            if (_losCache.TryGetValue(key, out LosCacheEntry entry) && nowMs < entry.ExpireMs)
                return entry.Clear;

            bool clear = IsSegmentClear(ToVec3(from), ToVec3(to));
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

            if (hit.Volume.Kind == ZoneTriggerKind.TargetVicinity)
            {
                crossing = new ZoneCrossing(0, position, hit.Volume);
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
                || _triggers.HasDynel(ZoneTriggerKind.TargetVicinity, doorInstance)
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

            if (portal.LandingKind == PortalLandingKind.DestinationMidpoint)
            {
                return PortalDoorLandingResolver.TryResolveMidpointLanding(
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

        /// <summary>
        /// Drops a character's trigger memory and ignores further triggers briefly. A line teleport
        /// calls this so the landing pad does not immediately fire, and so the source pad cannot
        /// fire again while a playfield transfer is still in flight.
        /// </summary>
        public void ForgetCharacterTriggers(int characterId)
        {
            DropCharacterTriggerMemory(characterId);
            _zoneGraceUntil[characterId] = (Environment.TickCount64 / 1000.0) + 3.0;
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
