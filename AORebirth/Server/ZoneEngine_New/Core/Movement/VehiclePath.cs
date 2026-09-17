namespace ZoneEngine_New.Core.Movement
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Vector3 = AORebirth.Core.Vector.Vector3;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    /// <summary>
    /// Client <c>Path_t</c> + <c>PathGuide_t</c> / <c>WaypointPath_c</c>: constant-speed
    /// distance-along-polyline. <c>Vehicle_t::Run</c> samples this instead of steering.
    /// </summary>
    public sealed class VehiclePath
    {
        readonly List<Vector3> _waypoints = new();
        readonly List<Segment> _segments = new();
        float _totalLength;
        float _speed;
        float _time;
        int _segmentIndex;

        public bool IsActive => _waypoints.Count >= 2 && _speed > 0f && Distance < _totalLength;

        public float Distance => _speed * _time;

        public float Speed => _speed;

        public void Clear()
        {
            _waypoints.Clear();
            _segments.Clear();
            _totalLength = 0f;
            _speed = 0f;
            _time = 0f;
            _segmentIndex = 0;
        }

        public void Set(IReadOnlyList<Vector3> waypoints, float speed)
        {
            Clear();
            if (waypoints == null || waypoints.Count == 0)
                return;

            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 point = waypoints[i];
                _waypoints.Add(new Vector3(point.x, point.y, point.z));
            }

            for (int i = 1; i < _waypoints.Count; i++)
            {
                Vector3 from = _waypoints[i - 1];
                Vector3 to = _waypoints[i];
                Vector3 delta = new(to.x - from.x, to.y - from.y, to.z - from.z);
                float length = (float)Vector3.Abs(delta);
                if (length <= 1e-8f)
                    continue;

                _segments.Add(new Segment(delta * (1.0 / length), length));
                _totalLength += length;
            }

            _speed = speed > 0f ? speed : 0f;
        }

        /// <summary>
        /// Client <c>PathGuide_t::UpdateTime</c> then <c>Path_t::MapPathDistanceToPoint</c>.
        /// </summary>
        public bool Advance(float dt, out Vector3 position, out Vector3 direction)
        {
            if (!IsActive)
            {
                position = CurrentOrZero();
                direction = new Vector3(0, 0, 0);
                return false;
            }

            _time += dt;
            MapDistance(Distance, out position, out direction);
            return IsActive;
        }

        public void Sample(out Vector3 position, out Vector3 direction) =>
            MapDistance(Distance, out position, out direction);

        public MsgVector3[] CopyRemainingWaypoints()
        {
            if (_waypoints.Count == 0)
                return [];

            int first = Math.Clamp(_segmentIndex + 1, 0, _waypoints.Count);
            if (first >= _waypoints.Count)
            {
                Vector3 last = _waypoints[_waypoints.Count - 1];
                return [new MsgVector3((float)last.x, (float)last.y, (float)last.z)];
            }

            var remaining = new MsgVector3[_waypoints.Count - first];
            for (int i = 0; i < remaining.Length; i++)
            {
                Vector3 point = _waypoints[first + i];
                remaining[i] = new MsgVector3((float)point.x, (float)point.y, (float)point.z);
            }

            return remaining;
        }

        void MapDistance(float distance, out Vector3 position, out Vector3 direction)
        {
            float remaining = distance;
            _segmentIndex = 0;
            int waypointIndex = 0;

            for (int i = 0; i < _segments.Count; i++)
            {
                Segment segment = _segments[i];
                if (remaining <= segment.Length)
                {
                    _segmentIndex = i;
                    Vector3 start = _waypoints[waypointIndex];
                    position = start + (segment.Direction * remaining);
                    direction = segment.Direction;
                    return;
                }

                remaining -= segment.Length;
                waypointIndex++;
                if (waypointIndex > _waypoints.Count - 2)
                    waypointIndex = _waypoints.Count - 2;
            }

            _segmentIndex = Math.Max(0, _segments.Count - 1);
            position = _waypoints[_waypoints.Count - 1];
            direction = _segments.Count > 0
                ? _segments[_segments.Count - 1].Direction
                : new Vector3(0, 0, 0);
        }

        Vector3 CurrentOrZero()
        {
            if (_waypoints.Count == 0)
                return new Vector3(0, 0, 0);
            return _waypoints[_waypoints.Count - 1];
        }

        readonly struct Segment
        {
            public Segment(Vector3 direction, float length)
            {
                Direction = direction;
                Length = length;
            }

            public Vector3 Direction { get; }

            public float Length { get; }
        }
    }
}
