namespace ZoneEngine_New.Core.Movement
{
    using System;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Client <c>Vehicle_t::EnsureSurfaceAlignment</c> (Vehicle.dll 0x1000d1aa).
    /// Waypoint paths run this after the polyline sample with sliding off.
    /// Keyed / falling-enabled motion also runs <see cref="Slide"/>.
    /// </summary>
    public static class EnsureSurfaceAlignment
    {
        static readonly Vector3 Tripod0 = Vector3.Normalize(new Vector3(1, 0, 0));
        static readonly Vector3 Tripod1 = Vector3.Normalize(new Vector3(-1, 0, 1));
        static readonly Vector3 Tripod2 = Vector3.Normalize(new Vector3(-1, 0, -1));

        public readonly struct Result
        {
            public Result(Vector3 position, Vector3 normal, bool aligned)
            {
                Position = position;
                Normal = normal;
                Aligned = aligned;
            }

            public Vector3 Position { get; }

            public Vector3 Normal { get; }

            public bool Aligned { get; }
        }

        public static Result Apply(
            IVehicleSurface? surface,
            Vector3 previous,
            Vector3 desired,
            bool allowSlide)
        {
            if (surface == null)
                return new Result(Copy(desired), new Vector3(0, 1, 0), true);

            Vector3 current = Unstick(surface, previous, desired);
            if (allowSlide)
                current = Slide(surface, previous, current);

            if (!TryTripod(surface, current, out float groundY, out Vector3 normal))
                return new Result(current, new Vector3(0, 1, 0), true);

            Vector3 aligned = new(current.x, groundY, current.z);
            return new Result(aligned, normal, true);
        }

        /// <summary>
        /// Client first loop: if the new pose is inside solid, walk it back toward
        /// <paramref name="previous"/> in tenths of the delta (9 attempts).
        /// </summary>
        public static Vector3 Unstick(IVehicleSurface surface, Vector3 previous, Vector3 desired)
        {
            if (!surface.OverlapsTorso(desired))
                return Copy(desired);

            Vector3 delta = (desired - previous) * 0.1;
            for (int step = 9; step > 0; step--)
            {
                Vector3 candidate = previous + (delta * step);
                if (!surface.OverlapsTorso(candidate))
                    return candidate;
            }

            return Copy(previous);
        }

        /// <summary>
        /// <c>FUN_1000b2e5</c> wall slide. Client only enables the lateral probes when
        /// falling is on (<c>Vehicle_t+0x50</c>). Waypoint paths call <c>DisableFalling</c>,
        /// so they never enter this.
        /// </summary>
        public static Vector3 Slide(IVehicleSurface surface, Vector3 previous, Vector3 desired)
        {
            Vector3 pos = Copy(previous);
            Vector3 remaining = desired - previous;
            float lift = MovementConfig.SurfaceHugLift;

            for (int i = 0; i < MovementConfig.SurfaceSlideIterations; i++)
            {
                double len = Vector3.Abs(remaining);
                if (len < 1e-8)
                    break;

                Vector3 from = new(pos.x, pos.y + lift, pos.z);
                Vector3 to = new(pos.x + remaining.x, pos.y + remaining.y + lift, pos.z + remaining.z);
                if (!surface.TryLinecast(from, to, out Vector3 hit, out Vector3 normal))
                {
                    pos = pos + remaining;
                    break;
                }

                Vector3 travel = new(hit.x - from.x, hit.y - from.y, hit.z - from.z);
                double travelLen = Vector3.Abs(travel);
                if (travelLen > MovementConfig.SweepSkin)
                {
                    double keep = (travelLen - MovementConfig.SweepSkin) / travelLen;
                    pos = pos + (travel * keep);
                    remaining = remaining * (1.0 - keep);
                }
                else
                {
                    remaining = remaining - (normal * Vector3.Dot(remaining, normal));
                }

                if (normal.y > MovementConfig.SurfaceSlideFloorY)
                    break;

                remaining = remaining - (normal * Vector3.Dot(remaining, normal));
                if (Vector3.Dot(remaining, desired - previous) <= 0)
                    break;
            }

            return pos;
        }

        /// <summary>
        /// Client <c>Vehicle_t::EnsureSurfaceAlignment</c> tripod: from dest.y+0.4 to world Y=0
        /// at 120-degree offsets of 0.04. Official collision hits along that segment.
        /// A total miss here means our Bepu bake did not hit — keep dest (return false)
        /// instead of copying the Y=0 endpoint, which drops the body to world zero.
        /// </summary>
        public static bool TryTripod(
            IVehicleSurface surface,
            Vector3 foot,
            out float groundY,
            out Vector3 normal)
        {
            groundY = (float)foot.y;
            normal = new Vector3(0, 1, 0);

            float highY = (float)foot.y + MovementConfig.SurfaceHugLift;
            const float lowY = 0f;
            Vector3[] hits = new Vector3[3];
            ReadOnlySpan<Vector3> dirs = [Tripod0, Tripod1, Tripod2];
            float maxY = float.MinValue;
            int found = 0;

            for (int i = 0; i < 3; i++)
            {
                Vector3 offset = dirs[i] * MovementConfig.SurfaceTripodRadius;
                Vector3 from = new(foot.x + offset.x, highY, foot.z + offset.z);
                Vector3 to = new(foot.x + offset.x, lowY, foot.z + offset.z);
                if (!surface.TryLinecast(from, to, out hits[i], out _))
                    continue;

                found++;
                if ((float)hits[i].y > maxY)
                    maxY = (float)hits[i].y;
            }

            if (found == 0)
                return false;

            if (found < 3)
            {
                groundY = maxY;
                return true;
            }

            Vector3 edgeA = hits[2] - hits[0];
            Vector3 edgeB = hits[1] - hits[0];
            Vector3 crossed = Vector3.Cross(edgeB, edgeA);
            if (Vector3.Abs(crossed) < 1e-8)
            {
                groundY = maxY;
                return true;
            }

            normal = Vector3.Normalize(crossed);
            if (normal.y < 0)
                normal = -normal;
            if (normal.y < MovementConfig.SurfaceUprightMinY)
                normal = new Vector3(0, 1, 0);

            groundY = maxY;
            return true;
        }

        static Vector3 Copy(Vector3 v) => new(v.x, v.y, v.z);
    }
}
