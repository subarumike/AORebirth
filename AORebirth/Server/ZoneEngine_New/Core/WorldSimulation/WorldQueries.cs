namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using BepuPhysics;
    using BepuPhysics.Collidables;
    using BepuPhysics.Trees;
    using BepuUtilities.Memory;

    /// <summary>Raycast / capsule sweep against hard statics (query-only).</summary>
    public sealed class WorldQueries
    {
        readonly Simulation _simulation;
        readonly BufferPool _pool;

        public WorldQueries(Simulation simulation, BufferPool pool)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        public bool Raycast(
            Vector3 origin,
            Vector3 direction,
            float maximumT,
            out float t,
            out Vector3 normal)
        {
            t = maximumT;
            normal = default;
            var handler = new RayHitHandler();
            _simulation.RayCast(origin, direction, maximumT, ref handler);
            if (!handler.Hit)
                return false;

            t = handler.T;
            normal = handler.Normal;
            return true;
        }

        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.Length();
            if (length < 1e-4f)
                return true;

            Vector3 dir = delta / length;
            return !Raycast(from, dir, length, out _, out _);
        }

        public bool CapsuleSweep(
            Vector3 start,
            Vector3 end,
            float radius,
            float halfLength,
            out Vector3 hitPosition)
        {
            hitPosition = end;
            Vector3 delta = end - start;
            float length = delta.Length();
            if (length < 1e-6f)
                return false;

            var capsule = new Capsule(radius, halfLength * 2f);
            var pose = new RigidPose(start);
            var velocity = new BodyVelocity(delta / length, default);
            var handler = new SweepHitHandler { Position = end };
            _simulation.Sweep(ref capsule, ref pose, ref velocity, length, _pool, ref handler);
            if (!handler.Hit)
                return false;

            hitPosition = handler.Position;
            return true;
        }

        struct RayHitHandler : IRayHitHandler
        {
            public bool Hit;
            public float T;
            public Vector3 Normal;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable) => true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable, int childIndex) => true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnRayHit(
                in RayData ray,
                ref float maximumT,
                float t,
                Vector3 normal,
                CollidableReference collidable,
                int childIndex)
            {
                if (t >= maximumT)
                    return;

                maximumT = t;
                T = t;
                Normal = normal;
                Hit = true;
            }
        }

        struct SweepHitHandler : ISweepHitHandler
        {
            public bool Hit;
            public float T;
            public Vector3 Position;

            public bool AllowTest(CollidableReference collidable) => true;

            public bool AllowTest(CollidableReference collidable, int childIndex) => true;

            public void OnHit(
                ref float maximumT,
                float t,
                Vector3 hitLocation,
                Vector3 hitNormal,
                CollidableReference collidable)
            {
                if (t >= maximumT)
                    return;

                maximumT = t;
                T = t;
                Position = hitLocation;
                Hit = true;
            }

            public void OnHitAtZeroT(ref float maximumT, CollidableReference collidable)
            {
                maximumT = 0;
                T = 0;
                Hit = true;
            }
        }
    }
}
