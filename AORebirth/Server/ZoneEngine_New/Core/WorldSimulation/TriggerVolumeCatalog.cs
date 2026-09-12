namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;

    using ZoneEngine_New.Core.Entities;

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
        ExitProxy = 3
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

    /// <summary>XZ spatial hash of soft zoning triggers (not Bepu hard).</summary>
    public sealed class TriggerVolumeCatalog
    {
        public const float BinSize = 32f;

        /// <summary>
        /// Half-width of a wall-border trigger band (units). Crossing the segment still fires
        /// regardless; this only affects proximity while standing on the line and the bake AABB.
        /// </summary>
        public const float WallProximity = 0.5f;

        /// <summary>
        /// Radius of a door/portal trigger disc (units), matching Legacy's measured statel
        /// collision envelope.
        /// </summary>
        public const float PortalRadius = 2f;

        /// <summary>
        /// Half-height of a door/portal trigger (units), matching Legacy's measured statel
        /// collision envelope.
        /// </summary>
        public const float PortalHalfHeight = 6f;

        readonly List<ZoneTriggerVolume> _all = new();
        readonly Dictionary<long, List<ZoneTriggerVolume>> _bins = new();

        public int WallTriggerCount { get; private set; }

        public int PortalTriggerCount { get; private set; }

        public int ExitTriggerCount { get; private set; }

        public int Count => _all.Count;

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

            for (int bz = minBinZ; bz <= maxBinZ; bz++)
            {
                for (int bx = minBinX; bx <= maxBinX; bx++)
                {
                    long key = ((long)bx << 32) ^ (uint)bz;
                    if (!_bins.TryGetValue(key, out List<ZoneTriggerVolume>? list))
                        continue;

                    for (int i = 0; i < list.Count; i++)
                    {
                        if (TryHit(list[i], fromX, fromZ, x, y, z, overlappingIds, out hit))
                            return true;
                    }
                }
            }

            return false;
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

            if (v.Kind is ZoneTriggerKind.PortalDynel or ZoneTriggerKind.ExitProxy)
            {
                if (MathF.Abs(y - v.CenterY) > PortalHalfHeight)
                    return false;

                // Sweep the movement against the portal disc so a fast run cannot skip through it.
                float dist = DistanceToSegment(fromX, fromZ, x, z, v.CenterX, v.CenterZ);
                if (dist > v.Radius)
                    return false;

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

            float dx = x - v.CenterX;
            float dz = z - v.CenterZ;
            return (dx * dx) + (dz * dz) <= v.Radius * v.Radius
                && MathF.Abs(y - v.CenterY) <= PortalHalfHeight;
        }

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
