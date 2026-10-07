namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;

    using ZoneEngine_New.Core.Entities;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    public enum ZoneTriggerKind : byte
    {
        WallBorder = 1,
        PortalDynel = 2,

        /// <summary>
        /// A door that sends the character back out the way they came in. It carries no destination
        /// of its own — the landing comes from the return stats saved when they walked in — and it
        /// is not in Dynels.dat at all, so it is registered on arrival rather than baked.
        /// </summary>
        ExitProxy = 3,

        /// <summary>
        /// A dynel whose OnTargetInVicinity spells run when a character steps into its radius.
        /// Grid line-teleport pads use this instead of a walk-in door portal.
        /// </summary>
        TargetVicinity = 4,

        /// <summary>
        /// A MissionEntrance (0xDAC6) doorway into ACG quest dungeons. Walking in hands the character to the quest
        /// dungeon service, which enters them only when a mission key they carry opens a dungeon behind it.
        /// </summary>
        MissionEntrance = 5,

        /// <summary>
        /// A quest dungeon's exit door. Walking into it returns the character outside the ACG entrance the dungeon
        /// was entered from (<see cref="ZoneTriggerVolume.Landing"/> on <see cref="ZoneTriggerVolume.DestPlayfieldId"/>).
        /// </summary>
        DungeonExit = 6,

        /// <summary>
        /// A zone surface teleportal (Surfaces.dat / Collision.dat): stepping inside its X/Z polygon takes the character
        /// to destination line <see cref="ZoneTriggerVolume.DestIndex"/> of <see cref="ZoneTriggerVolume.DestPlayfieldId"/>,
        /// the zoning the client predicts from the same data.
        /// </summary>
        Teleportal = 7,

        /// <summary>
        /// A door whose placement data (Dynels.dat blob) sets ExitInstance: the client walks into it expecting a zone
        /// change. With no teleports route it only returns a character through the entrance they recorded; otherwise
        /// the character is told the destination is unknown. Routed exit doors are baked as
        /// <see cref="PortalDynel"/> instead.
        /// </summary>
        ExitDoor = 8
    }

    public sealed class ZoneTriggerVolume
    {
        public ZoneTriggerKind Kind;
        public int Id;
        public float MinX;
        public float MaxX;
        public float MinZ;
        public float MaxZ;
        public float MinY = float.NegativeInfinity;
        public float MaxY = float.PositiveInfinity;

        // Wall
        public float SegAx;
        public float SegAz;
        public float SegBx;
        public float SegBz;
        public int DestPlayfieldId;
        public byte DestIndex;

        // Portal
        public int DynelInstance;
        public PortalLandingKind LandingKind;
        public int DestDoorInstance;
        public float DoorClearance;

        /// <summary>True when walking this portal should record a way back through it.</summary>
        public bool RecordsReturn;
        public float CenterX;
        public float CenterY;
        public float CenterZ;
        public float Radius = TriggerVolumeCatalog.PortalRadius;

        /// <summary>
        /// The client's collision sphere instead of the default disc: centred at (CenterX, CenterY, CenterZ), with
        /// <see cref="Radius"/> already widened by the player's own sphere, so it fires when the player's sphere centre
        /// (feet + <see cref="ProbeHeight"/>) comes within Radius. See <see cref="TriggerVolumeCatalog.MakeClientSphere"/>.
        /// </summary>
        public bool Sphere;

        /// <summary>Height of the player's collision sphere centre above the feet, for a <see cref="Sphere"/>.</summary>
        public float ProbeHeight;

        /// <summary>
        /// A door's extra walk-in condition: the feet must also lie within <see cref="TriggerVolumeCatalog.DoorPlaneHalfDepth"/>
        /// of the door's plane (normal PlaneNx/Ny/Nz, offset PlaneD). See <see cref="TriggerVolumeCatalog.SetDoorPlane"/>.
        /// </summary>
        public bool DoorPlane;
        public float PlaneNx;
        public float PlaneNy;
        public float PlaneNz;
        public float PlaneD;

        /// <summary>Fixed landing for a <see cref="ZoneTriggerKind.DungeonExit"/>.</summary>
        public Vector3? Landing;

        public AORebirth.Core.Vector.Quaternion? LandingHeading;

        /// <summary>OnTargetInVicinity spells for a <see cref="ZoneTriggerKind.TargetVicinity"/> pad.</summary>
        public Inventory.ItemTemplate? VicinityEvents;

        /// <summary>Area of a <see cref="ZoneTriggerKind.Teleportal"/>.</summary>
        public AODB.Common.RDBObjects.SurfaceTeleportal? Teleportal;
    }

    public readonly struct ZoneTriggerHit
    {
        public ZoneTriggerHit(ZoneTriggerVolume volume, float factor)
        {
            Volume = volume;
            Factor = factor;
        }

        public ZoneTriggerVolume Volume { get; }

        public float Factor { get; }
    }

    /// <summary>XZ spatial hash of soft zoning triggers (not hard collision).</summary>
    public sealed class TriggerVolumeCatalog
    {
        public const float BinSize = 32f;

        /// <summary>
        /// Half-width of a wall-border trigger band (units). Crossing the segment still fires
        /// regardless; this only affects proximity while standing on the line and the bake AABB.
        /// </summary>
        public const float WallProximity = 0.5f;

        /// <summary>
        /// Radius of a door/portal trigger disc (units). Half the legacy statel collision range of
        /// 2.0, which reached wide enough to catch characters walking past a door.
        /// </summary>
        public const float PortalRadius = 1f;

        /// <summary>
        /// Half-height of a door/portal trigger (units). Half the legacy 6.0, which spanned enough
        /// storeys for a door on another floor to trigger.
        /// </summary>
        public const float PortalHalfHeight = 3f;

        /// <summary>
        /// Radius of an OnTargetInVicinity pad when the placement has no VicinityRange.
        /// </summary>
        public const float TargetVicinityRadius = 1f;

        /// <summary>
        /// Radius of a mission entrance trigger (units). A little wider than a portal door: the entrance dynel marks
        /// the building's doorway, which characters approach from anywhere across its width.
        /// </summary>
        public const float MissionEntranceRadius = 1.5f;

        /// <summary>How far the feet may be from a door's plane and still walk through it (client: 0.8).</summary>
        public const float DoorPlaneHalfDepth = 0.8f;

        readonly List<ZoneTriggerVolume> _all = new();
        readonly Dictionary<long, List<ZoneTriggerVolume>> _bins = new();

        public int WallTriggerCount { get; private set; }

        public int PortalTriggerCount { get; private set; }

        public int ExitTriggerCount { get; private set; }

        public int VicinityTriggerCount { get; private set; }

        public int TeleportalTriggerCount { get; private set; }

        public int Count => _all.Count;

        public bool HasDynel(ZoneTriggerKind kind, int dynelInstance)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                ZoneTriggerVolume volume = _all[i];
                if (volume.Kind == kind && volume.DynelInstance == dynelInstance)
                    return true;
            }

            return false;
        }

        public void Add(ZoneTriggerVolume volume)
        {
            ArgumentNullException.ThrowIfNull(volume);
            _all.Add(volume);
            if (volume.Kind == ZoneTriggerKind.WallBorder)
                WallTriggerCount++;
            else if (volume.Kind == ZoneTriggerKind.PortalDynel)
                PortalTriggerCount++;
            else if (volume.Kind == ZoneTriggerKind.ExitProxy)
                ExitTriggerCount++;
            else if (volume.Kind == ZoneTriggerKind.TargetVicinity)
                VicinityTriggerCount++;
            else if (volume.Kind == ZoneTriggerKind.Teleportal)
                TeleportalTriggerCount++;

            int minBinX = (int)MathF.Floor(volume.MinX / BinSize);
            int maxBinX = (int)MathF.Floor(volume.MaxX / BinSize);
            int minBinZ = (int)MathF.Floor(volume.MinZ / BinSize);
            int maxBinZ = (int)MathF.Floor(volume.MaxZ / BinSize);
            for (int bz = minBinZ; bz <= maxBinZ; bz++)
            {
                for (int bx = minBinX; bx <= maxBinX; bx++)
                {
                    long key = ((long)bx << 32) ^ (uint)bz;
                    if (!_bins.TryGetValue(key, out List<ZoneTriggerVolume>? list))
                    {
                        list = new List<ZoneTriggerVolume>();
                        _bins[key] = list;
                    }

                    list.Add(volume);
                }
            }
        }

        /// <summary>
        /// Tests the movement from (<paramref name="fromX"/>, <paramref name="fromZ"/>) to
        /// (<paramref name="x"/>, <paramref name="z"/>) against every trigger the path could touch.
        /// Zone lines are infinitely thin, so proximity alone is not enough: a running character
        /// covers ~0.4u per tick and would step straight over a fixed threshold band. Crossing the
        /// movement segment is the authoritative test; proximity only catches standing on the line.
        /// </summary>
        public bool TrySample(
            float fromX,
            float fromZ,
            float x,
            float y,
            float z,
            HashSet<int> overlappingIds,
            out ZoneTriggerHit hit)
        {
            hit = default;
            float minX = MathF.Min(fromX, x) - WallProximity;
            float maxX = MathF.Max(fromX, x) + WallProximity;
            float minZ = MathF.Min(fromZ, z) - WallProximity;
            float maxZ = MathF.Max(fromZ, z) + WallProximity;
            int minBinX = (int)MathF.Floor(minX / BinSize);
            int maxBinX = (int)MathF.Floor(maxX / BinSize);
            int minBinZ = (int)MathF.Floor(minZ / BinSize);
            int maxBinZ = (int)MathF.Floor(maxZ / BinSize);
            ZoneTriggerHit vicinity = default;
            bool sawVicinity = false;

            for (int bz = minBinZ; bz <= maxBinZ; bz++)
            {
                for (int bx = minBinX; bx <= maxBinX; bx++)
                {
                    long key = ((long)bx << 32) ^ (uint)bz;
                    if (!_bins.TryGetValue(key, out List<ZoneTriggerVolume>? list))
                        continue;

                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!TryHit(list[i], fromX, fromZ, x, y, z, overlappingIds, out ZoneTriggerHit candidate))
                            continue;

                        // A beam the character is standing in must not swallow a door or border
                        // crossed on the same step.
                        if (candidate.Volume.Kind == ZoneTriggerKind.TargetVicinity)
                        {
                            if (!sawVicinity)
                            {
                                vicinity = candidate;
                                sawVicinity = true;
                            }

                            continue;
                        }

                        hit = candidate;
                        return true;
                    }
                }
            }

            if (!sawVicinity)
                return false;

            hit = vicinity;
            return true;
        }

        static bool TryHit(
            ZoneTriggerVolume v,
            float fromX,
            float fromZ,
            float x,
            float y,
            float z,
            HashSet<int> overlappingIds,
            out ZoneTriggerHit hit)
        {
            hit = default;
            if (y < v.MinY || y > v.MaxY)
                return false;

            if (v.Kind == ZoneTriggerKind.WallBorder)
            {
                if (TryCrossing(fromX, fromZ, x, z, v.SegAx, v.SegAz, v.SegBx, v.SegBz, out float crossFactor))
                {
                    if (!overlappingIds.Add(v.Id))
                        return false;

                    hit = new ZoneTriggerHit(v, crossFactor);
                    return true;
                }

                if (MinimalDistance(v.SegAx, v.SegAz, v.SegBx, v.SegBz, x, z) >= WallProximity)
                    return false;

                if (!overlappingIds.Add(v.Id))
                    return false;

                hit = new ZoneTriggerHit(v, ProjectionFactor(v, x, z));
                return true;
            }

            if (v.Kind == ZoneTriggerKind.Teleportal)
            {
                // Inside the area at the end of the step; teleportals are wide regions, not thin lines.
                if (v.Teleportal == null || !v.Teleportal.Contains(x, z))
                    return false;

                if (!overlappingIds.Add(v.Id))
                    return false;

                hit = new ZoneTriggerHit(v, 0f);
                return true;
            }

            if (v.Kind is ZoneTriggerKind.PortalDynel or ZoneTriggerKind.ExitProxy or ZoneTriggerKind.TargetVicinity
                or ZoneTriggerKind.MissionEntrance or ZoneTriggerKind.DungeonExit or ZoneTriggerKind.ExitDoor)
            {
                // Sweep the movement against the trigger so a fast run cannot skip through it.
                float dist = DistanceToSegment(fromX, fromZ, x, z, v.CenterX, v.CenterZ);
                if (v.Sphere)
                {
                    // The client's test: the player's collision sphere touching the dynel's.
                    float dy = (y + v.ProbeHeight) - v.CenterY;
                    if ((dist * dist) + (dy * dy) > v.Radius * v.Radius)
                        return false;
                    if (!InDoorPlane(v, x, y, z))
                        return false;
                }
                else
                {
                    if (MathF.Abs(y - v.CenterY) > HalfHeight(v))
                        return false;
                    if (dist > v.Radius)
                        return false;
                }

                if (!overlappingIds.Add(v.Id))
                    return false;

                hit = new ZoneTriggerHit(v, 0f);
                return true;
            }

            return false;
        }

        static float ProjectionFactor(ZoneTriggerVolume v, float x, float z)
        {
            float abx = v.SegBx - v.SegAx;
            float abz = v.SegBz - v.SegAz;
            float lenSq = (abx * abx) + (abz * abz);
            if (lenSq < 1e-8f)
                return 0f;

            float t = (((x - v.SegAx) * abx) + ((z - v.SegAz) * abz)) / lenSq;
            return Math.Clamp(t, 0f, 1f);
        }

        /// <summary>
        /// 2D segment intersection. <paramref name="factor"/> is where along A→B the crossing
        /// happened, which is what the landing interpolation expects.
        /// </summary>
        static bool TryCrossing(
            float px,
            float pz,
            float qx,
            float qz,
            float ax,
            float az,
            float bx,
            float bz,
            out float factor)
        {
            factor = 0f;
            float rx = qx - px;
            float rz = qz - pz;
            float sx = bx - ax;
            float sz = bz - az;
            float denominator = (rx * sz) - (rz * sx);
            if (MathF.Abs(denominator) < 1e-8f)
                return false;

            float t = (((ax - px) * sz) - ((az - pz) * sx)) / denominator;
            float u = (((ax - px) * rz) - ((az - pz) * rx)) / denominator;
            if (t < 0f || t > 1f || u < 0f || u > 1f)
                return false;

            factor = u;
            return true;
        }

        static float DistanceToSegment(float ax, float az, float bx, float bz, float x, float z)
        {
            float abx = bx - ax;
            float abz = bz - az;
            float lenSq = (abx * abx) + (abz * abz);
            if (lenSq < 1e-8f)
                return Distance(ax, az, x, z);

            float t = Math.Clamp((((x - ax) * abx) + ((z - az) * abz)) / lenSq, 0f, 1f);
            return Distance(ax + (abx * t), az + (abz * t), x, z);
        }

        /// <summary>
        /// Marks every trigger the point already sits inside as triggered without firing it. Used
        /// when a character first appears at a position — arriving through a door lands on top of
        /// that door, and a character logging in can stand on a zone line — so the trigger must
        /// wait for a real re-entry.
        /// </summary>
        public void CollectOverlapping(float x, float y, float z, HashSet<int> overlappingIds)
        {
            ArgumentNullException.ThrowIfNull(overlappingIds);

            int binX = (int)MathF.Floor(x / BinSize);
            int binZ = (int)MathF.Floor(z / BinSize);
            long key = ((long)binX << 32) ^ (uint)binZ;
            if (!_bins.TryGetValue(key, out List<ZoneTriggerVolume>? list))
                return;

            for (int i = 0; i < list.Count; i++)
            {
                if (Contains(list[i], x, y, z))
                    overlappingIds.Add(list[i].Id);
            }
        }

        public void ClearOverlapOutside(float x, float y, float z, HashSet<int> overlappingIds)
        {
            if (overlappingIds.Count == 0)
                return;

            List<int> remove = null!;
            foreach (int id in overlappingIds)
            {
                ZoneTriggerVolume? v = FindById(id);
                if (v == null || !Contains(v, x, y, z))
                {
                    remove ??= new List<int>();
                    remove.Add(id);
                }
            }

            if (remove == null)
                return;

            for (int i = 0; i < remove.Count; i++)
                overlappingIds.Remove(remove[i]);
        }

        static bool Contains(ZoneTriggerVolume v, float x, float y, float z)
        {
            if (v.Kind == ZoneTriggerKind.WallBorder)
                return MinimalDistance(v.SegAx, v.SegAz, v.SegBx, v.SegBz, x, z) < WallProximity;

            if (v.Kind == ZoneTriggerKind.Teleportal)
                return v.Teleportal != null && v.Teleportal.Contains(x, z);

            float dx = x - v.CenterX;
            float dz = z - v.CenterZ;
            if (v.Sphere)
            {
                float dy = (y + v.ProbeHeight) - v.CenterY;
                return (dx * dx) + (dy * dy) + (dz * dz) <= v.Radius * v.Radius && InDoorPlane(v, x, y, z);
            }

            return (dx * dx) + (dz * dz) <= v.Radius * v.Radius
                && MathF.Abs(y - v.CenterY) <= HalfHeight(v);
        }

        /// <summary>
        /// Shapes <paramref name="v"/> as the client's walk-in test for a placed dynel at (<paramref name="x"/>,
        /// <paramref name="y"/>, <paramref name="z"/>): the dynel's collision sphere (radius, centre height) against the
        /// player's (n3VisualDynel_t::UpdateCollision gives each a torso sphere; touching fires OnEnter/OnCollide).
        /// </summary>
        public static void MakeClientSphere(
            ZoneTriggerVolume v,
            float x,
            float y,
            float z,
            float radius,
            float centerY,
            float playerRadius,
            float playerCenterY)
        {
            ArgumentNullException.ThrowIfNull(v);
            float reach = radius + playerRadius;
            v.Sphere = true;
            v.ProbeHeight = playerCenterY;
            v.CenterX = x;
            v.CenterY = y + centerY;
            v.CenterZ = z;
            v.Radius = reach;
            v.MinX = x - reach;
            v.MaxX = x + reach;
            v.MinZ = z - reach;
            v.MaxZ = z + reach;
            // Feet heights whose probe point can still reach the sphere.
            v.MinY = v.CenterY - reach - playerCenterY;
            v.MaxY = v.CenterY + reach - playerCenterY;
        }

        /// <summary>
        /// A door only zones once the player is also in its doorway: the client's Door_t walk-in check
        /// (Gamecode.dll 0x1007f5db) requires |(feet - door) . (door rotation * +Z)| &lt; 0.8, so touching the door's
        /// sphere from beside the door or above it does nothing.
        /// </summary>
        public static void SetDoorPlane(ZoneTriggerVolume v, float x, float y, float z, Quaternion rotation)
        {
            ArgumentNullException.ThrowIfNull(v);
            ArgumentNullException.ThrowIfNull(rotation);
            var normal = (Vector3)Quaternion.RotateVector3(rotation, Vector3.AxisZ);
            v.DoorPlane = true;
            v.PlaneNx = normal.xf;
            v.PlaneNy = normal.yf;
            v.PlaneNz = normal.zf;
            v.PlaneD = (normal.xf * x) + (normal.yf * y) + (normal.zf * z);
        }

        static bool InDoorPlane(ZoneTriggerVolume v, float x, float y, float z)
            => !v.DoorPlane
               || MathF.Abs((v.PlaneNx * x) + (v.PlaneNy * y) + (v.PlaneNz * z) - v.PlaneD) < DoorPlaneHalfDepth;

        /// <summary>
        /// Vertical reach of a disc trigger. A vicinity pad reaches at least as far as a door:
        /// Jobe Platform's exit doors sit 1.9 above the landing in front of them.
        /// </summary>
        public static float HalfHeight(ZoneTriggerVolume v)
            => v.Kind == ZoneTriggerKind.TargetVicinity ? MathF.Max(v.Radius, PortalHalfHeight) : PortalHalfHeight;

        ZoneTriggerVolume? FindById(int id)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Id == id)
                    return _all[i];
            }

            return null;
        }

        static float MinimalDistance(float ax, float az, float bx, float bz, float x, float z)
        {
            float abx = bx - ax;
            float abz = bz - az;
            float apx = x - ax;
            float apz = z - az;
            float abLenSq = (abx * abx) + (abz * abz);
            if (abLenSq < 1e-8f)
                return Distance(ax, az, x, z);

            // Past either end the neighbouring segment of the wall polyline owns the character.
            float t = ((apx * abx) + (apz * abz)) / abLenSq;
            if (t < 0f || t > 1f)
                return float.MaxValue;

            float cross = MathF.Abs((abx * apz) - (abz * apx));
            return cross / MathF.Sqrt(abLenSq);
        }

        static float Distance(float ax, float az, float bx, float bz)
        {
            float dx = bx - ax;
            float dz = bz - az;
            return MathF.Sqrt((dx * dx) + (dz * dz));
        }
    }
}
