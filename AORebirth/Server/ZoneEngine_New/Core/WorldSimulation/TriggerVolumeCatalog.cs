namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;

    using ZoneEngine_New.Core.Entities;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    public enum ZoneTriggerKind : byte
    {
        WallBorder = 1,
        PortalDynel = 2
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
        public float CenterX;
        public float CenterY;
        public float CenterZ;
        public float Radius = 2f;
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
        const float BinSize = 32f;
        const float WallThreshold = 2f;

        readonly List<ZoneTriggerVolume> _all = new();
        readonly Dictionary<long, List<ZoneTriggerVolume>> _bins = new();

        public int WallTriggerCount { get; private set; }

        public int PortalTriggerCount { get; private set; }

        public int Count => _all.Count;

        public void Add(ZoneTriggerVolume volume)
        {
            ArgumentNullException.ThrowIfNull(volume);
            _all.Add(volume);
            if (volume.Kind == ZoneTriggerKind.WallBorder)
                WallTriggerCount++;
            else if (volume.Kind == ZoneTriggerKind.PortalDynel)
                PortalTriggerCount++;

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

        public bool TrySample(
            float x,
            float y,
            float z,
            HashSet<int> overlappingIds,
            out ZoneTriggerHit hit)
        {
            hit = default;
            int bx = (int)MathF.Floor(x / BinSize);
            int bz = (int)MathF.Floor(z / BinSize);
            long key = ((long)bx << 32) ^ (uint)bz;
            if (!_bins.TryGetValue(key, out List<ZoneTriggerVolume>? list))
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                ZoneTriggerVolume v = list[i];
                if (y < v.MinY || y > v.MaxY)
                    continue;

                if (v.Kind == ZoneTriggerKind.WallBorder)
                {
                    float dist = MinimalDistance(v.SegAx, v.SegAz, v.SegBx, v.SegBz, x, z);
                    if (dist >= WallThreshold)
                        continue;

                    if (!overlappingIds.Add(v.Id))
                        continue;

                    float factor = Distance(v.SegAx, v.SegAz, x, z)
                        / MathF.Max(Distance(v.SegAx, v.SegAz, v.SegBx, v.SegBz), 1e-4f);
                    hit = new ZoneTriggerHit(v, Math.Clamp(factor, 0f, 1f));
                    return true;
                }

                if (v.Kind == ZoneTriggerKind.PortalDynel)
                {
                    float dx = x - v.CenterX;
                    float dz = z - v.CenterZ;
                    float dy = y - v.CenterY;
                    if ((dx * dx) + (dz * dz) > v.Radius * v.Radius)
                        continue;
                    if (MathF.Abs(dy) > 6f)
                        continue;

                    if (!overlappingIds.Add(v.Id))
                        continue;

                    hit = new ZoneTriggerHit(v, 0f);
                    return true;
                }
            }

            return false;
        }

        public void ClearOverlapOutside(float x, float y, float z, HashSet<int> overlappingIds)
        {
            if (overlappingIds.Count == 0)
                return;

            List<int> remove = null!;
            foreach (int id in overlappingIds)
            {
                ZoneTriggerVolume? v = FindById(id);
                if (v == null)
                {
                    remove ??= new List<int>();
                    remove.Add(id);
                    continue;
                }

                bool still;
                if (v.Kind == ZoneTriggerKind.WallBorder)
                    still = MinimalDistance(v.SegAx, v.SegAz, v.SegBx, v.SegBz, x, z) < WallThreshold;
                else
                {
                    float dx = x - v.CenterX;
                    float dz = z - v.CenterZ;
                    float dy = y - v.CenterY;
                    still = (dx * dx) + (dz * dz) <= v.Radius * v.Radius && MathF.Abs(dy) <= 6f;
                }

                if (!still)
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

            float t = ((apx * abx) + (apz * abz)) / abLenSq;
            if (t < 0f || t > 1f)
                return 15f;

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
