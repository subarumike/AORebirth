using System.Collections.Generic;

namespace LostEden.Vehicles
{
    /// <summary>
    /// <c>Path_t</c> (<c>Vehicle.dll</c>, every method exported by name) — the waypoint list an
    /// <see cref="NpcVehicleSim"/> follows. See Docs/Movement.md §3.2.
    ///
    /// <para>
    /// It keeps two parallel lists: the waypoints themselves and, per segment, a <b>unit direction and
    /// its length</b> (stock's <c>GetDirLenContainer</c>, a <c>vector&lt;pair&lt;Vector3, float&gt;&gt;</c>
    /// of 16-byte entries at <c>+0x14</c>). Both are built incrementally by
    /// <see cref="AddWaypoint"/>, which is why there is no rebuild step.
    /// </para>
    /// </summary>
    public sealed class Path
    {
        readonly List<Vec3> _waypoints = new List<Vec3>();
        readonly List<Vec3> _directions = new List<Vec3>();   // unit, one per segment
        readonly List<float> _lengths = new List<float>();

        /// <summary><c>Size</c> (<c>10005dc5</c>).</summary>
        public int Size => _waypoints.Count;

        /// <summary><c>Empty</c> (<c>10005dd2</c>).</summary>
        public bool Empty => _waypoints.Count == 0;

        /// <summary><c>GetTotalPathLength</c> (<c>+0x28</c>, accumulated by <c>AddWaypoint</c>).</summary>
        public float TotalLength { get; private set; }

        /// <summary><c>SetPathRadius</c> (<c>100054ef</c>). Stored, unused by the steering path.</summary>
        public float Radius { get; set; }

        /// <summary><c>GetWaypoint(int)</c> (<c>10005dde</c>).</summary>
        public Vec3 GetWaypoint(int index) => _waypoints[index];

        /// <summary><c>GetDir(int)</c> (<c>10005dee</c>) — the segment's unit direction.</summary>
        public Vec3 GetDir(int segment) => _directions[segment];

        /// <summary><c>GetSegLen(int)</c> (<c>10005dfe</c>).</summary>
        public float GetSegLen(int segment) => _lengths[segment];

        /// <summary><c>Clear</c> (<c>10005bd3</c>).</summary>
        public void Clear()
        {
            _waypoints.Clear();
            _directions.Clear();
            _lengths.Clear();
            TotalLength = 0f;
        }

        /// <summary>
        /// <c>AddWaypoint</c> (<c>10005c08</c>).
        ///
        /// <para>
        /// <b>The Y is discarded.</b> Stock stores <c>(x, 0, z)</c> — it loads <c>[eax]</c> and
        /// <c>[eax+8]</c> and pushes a literal zero between them (<c>10005c18</c>). Paths are flat,
        /// which is exactly why <see cref="NpcVehicleSim"/> forces the steering target back to the
        /// body's own height.
        /// </para>
        ///
        /// <para>
        /// A waypoint that repeats the previous one is dropped: stock builds the segment direction and
        /// tests it with <c>Vector3::IsZero</c> (<c>10005c5f</c>) before committing.
        /// </para>
        /// </summary>
        public void AddWaypoint(Vec3 waypoint)
        {
            var flat = new Vec3(waypoint.X, 0f, waypoint.Z);

            if (_waypoints.Count == 0)
            {
                _waypoints.Add(flat);
                return;
            }

            Vec3 direction = flat - _waypoints[_waypoints.Count - 1];
            if (direction.IsZero)
                return;

            float length = direction.Length;
            _waypoints.Add(flat);
            _directions.Add(direction * (1f / length));
            _lengths.Add(length);
            TotalLength += length;
        }

        /// <summary>
        /// <c>MapPathDistanceToPoint</c> (<c>10005a2e</c>) — walk the segments consuming
        /// <paramref name="distance"/>, and return
        /// <c>waypoint[segment] + direction[segment] * remaining</c>. Past the end of the path it
        /// returns the <b>final waypoint</b> (<c>10005a77</c> takes <c>last - 1</c>).
        /// </summary>
        public Vec3 MapPathDistanceToPoint(float distance)
        {
            if (_waypoints.Count == 0)
                return Vec3.Zero;

            float remaining = distance;
            int segments = _waypoints.Count - 1;

            for (int segment = 0; segment < segments; segment++)
            {
                float length = _lengths[segment];

                // 10005a64: `remaining <= length` is the hit, so a zero distance lands on the first
                // waypoint rather than skipping a zero-length segment.
                if (remaining <= length)
                    return _waypoints[segment] + _directions[segment] * remaining;

                remaining -= length;
            }

            return _waypoints[_waypoints.Count - 1];
        }
    }

    /// <summary>
    /// <c>PathGuide_t</c> (<c>Vehicle.dll</c>, 24 bytes) — a point that slides along a
    /// <see cref="Path"/> at a fixed speed. The NPC steers at the guide, not at a waypoint, so it cuts
    /// corners smoothly instead of snapping between legs.
    ///
    /// <para>
    /// The whole model is <c>guidePos = path.MapPathDistanceToPoint(maxSpeed * time)</c>. The position
    /// is <b>cached</b> — <c>GetGuidePos</c> (<c>10006a47</c>) is a bare
    /// <c>lea eax, [ecx+8]; ret</c> — so it only moves when one of the update calls runs.
    /// </para>
    /// </summary>
    public sealed class PathGuide
    {
        /// <summary>+0x00.</summary>
        public float MaxSpeed { get; private set; }

        /// <summary>+0x04.</summary>
        public float Time { get; private set; }

        /// <summary>+0x08..+0x10, and what <c>GetGuidePos</c> returns.</summary>
        public Vec3 GuidePos { get; private set; }

        /// <summary>+0x14.</summary>
        public Path Path { get; private set; }

        /// <summary>
        /// <c>PathGuide_t(Path_t*, float maxSpeed, float time)</c> (<c>100069d9</c>). Note it does
        /// <b>not</b> compute the guide position — only <see cref="RestartGuide"/> and the update calls
        /// do, so a freshly constructed guide reports <c>(0,0,0)</c> until one of them runs.
        /// </summary>
        public PathGuide(Path path, float maxSpeed, float time)
        {
            Path = path;
            MaxSpeed = maxSpeed;
            Time = time;
        }

        /// <summary><c>PathGuide_t()</c> (<c>100069fe</c>) — everything zero, no path.</summary>
        public PathGuide()
        {
        }

        /// <summary>
        /// <c>UpdateAddTime(float)</c> (<c>10006a16</c>) — advance by a delta and re-map.
        ///
        /// <para>
        /// Stock does <b>not</b> check the path here, unlike <see cref="UpdateTime"/> and
        /// <see cref="RestartGuide"/>, so in stock this walks a null or empty path. Guarded.
        /// </para>
        /// </summary>
        public void UpdateAddTime(float dt)
        {
            Time += dt;

            if (Path != null && !Path.Empty)
                GuidePos = Path.MapPathDistanceToPoint(Time * MaxSpeed);
        }

        /// <summary><c>UpdateMaxSpeed(float)</c> (<c>10006a4b</c>) — stores only, no re-map.</summary>
        public void UpdateMaxSpeed(float maxSpeed) => MaxSpeed = maxSpeed;

        /// <summary><c>UpdateTime(float)</c> (<c>10006a57</c>) — set the absolute time and re-map.</summary>
        public void UpdateTime(float time)
        {
            Time = time;

            if (Path != null && !Path.Empty)
                GuidePos = Path.MapPathDistanceToPoint(MaxSpeed * time);
        }

        /// <summary><c>RestartGuide(Path_t*, float maxSpeed, float time)</c> (<c>10006a89</c>).</summary>
        public void RestartGuide(Path path, float maxSpeed, float time)
        {
            Path = path;
            MaxSpeed = maxSpeed;
            Time = time;

            if (path != null && !path.Empty)
                GuidePos = path.MapPathDistanceToPoint(maxSpeed * time);
        }
    }
}
