namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using AORebirth.World.Pathfinding;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// One <see cref="ReachMap"/> per target per playfield, shared by every NPC asking whether it can reach that target.
    /// A pack of hundreds costs one flood per target refresh instead of one path search per NPC. A map is rebuilt on
    /// the same terms an NPC's own answer used to expire: after the replan interval, or once the target has moved.
    /// Tick thread only.
    /// </summary>
    internal sealed class ReachMaps
    {
        /// <summary>Maps of targets no NPC has asked about for this long are dropped.</summary>
        const double ForgetSeconds = 30.0;

        const int SweepEvery = 256;

        readonly Dictionary<ulong, Entry> _byTarget = new();
        int _requests;

        /// <summary>The target's map, rebuilt when stale. <paramref name="targetFeet"/> is where the target stands.</summary>
        public ReachMap Get(Character target, NavMeshPathfinder finder, Vector3 targetFeet)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(finder);

            DateTime now = DateTime.UtcNow;
            if (++_requests % SweepEvery == 0)
                Sweep(now);

            ulong key = target.Identity.Long();
            if (_byTarget.TryGetValue(key, out Entry? entry)
                && (now - entry.BuiltUtc).TotalSeconds < NpcFollowTarget.PathReplanSeconds
                && Vector3.Abs(targetFeet - entry.At) < NpcFollowTarget.MinAnnounceDeltaMeters)
            {
                entry.LastUsedUtc = now;
                PathDiag.CacheHit(PathDiag.ReachMap);
                return entry.Map;
            }

            ReachMap map = PathDiag.BuildReachMap(
                finder,
                new System.Numerics.Vector3((float)targetFeet.x, (float)targetFeet.y, (float)targetFeet.z),
                NpcAiRules.ReachRadiusMeters,
                NpcAiRules.ReachMapMaxPolys);
            _byTarget[key] = new Entry(map, new Vector3(targetFeet.x, targetFeet.y, targetFeet.z), now);
            return map;
        }

        void Sweep(DateTime now)
        {
            List<ulong>? stale = null;
            foreach (KeyValuePair<ulong, Entry> pair in _byTarget)
            {
                if ((now - pair.Value.LastUsedUtc).TotalSeconds >= ForgetSeconds)
                    (stale ??= []).Add(pair.Key);
            }

            if (stale == null)
                return;
            foreach (ulong key in stale)
                _byTarget.Remove(key);
        }

        sealed class Entry(ReachMap map, Vector3 at, DateTime builtUtc)
        {
            public ReachMap Map { get; } = map;

            public Vector3 At { get; } = at;

            public DateTime BuiltUtc { get; } = builtUtc;

            public DateTime LastUsedUtc { get; set; } = builtUtc;
        }
    }
}
