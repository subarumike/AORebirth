namespace ZoneEngine_New.Core.WorldSimulation
{
    using System.Numerics;

    using ZoneEngine_New.Core.Movement;

    using AoVector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>Bepu-backed <c>Surface_i</c> for <see cref="EnsureSurfaceAlignment"/>.</summary>
    public sealed class WorldVehicleSurface : IVehicleSurface
    {
        readonly WorldQueries _queries;

        public WorldVehicleSurface(WorldQueries queries)
        {
            _queries = queries;
        }

        public bool TryLinecast(AoVector3 from, AoVector3 to, out AoVector3 hit, out AoVector3 normal)
        {
            hit = from;
            normal = new AoVector3(0, 1, 0);
            var start = new Vector3((float)from.x, (float)from.y, (float)from.z);
            var end = new Vector3((float)to.x, (float)to.y, (float)to.z);
            Vector3 delta = end - start;
            float length = delta.Length();
            if (length < 1e-8f)
                return false;

            Vector3 dir = delta / length;
            if (!_queries.Raycast(start, dir, length, out float t, out Vector3 n))
                return false;

            Vector3 at = start + (dir * t);
            hit = new AoVector3(at.X, at.Y, at.Z);
            normal = new AoVector3(n.X, n.Y, n.Z);
            return true;
        }

        public bool OverlapsTorso(AoVector3 foot)
        {
            var center = new Vector3(
                (float)foot.x,
                (float)(foot.y + MovementConfig.SurfaceHugLift + 0.5f),
                (float)foot.z);
            var down = center + new Vector3(0f, -0.001f, 0f);
            return _queries.CapsuleSweep(
                center,
                down,
                MovementConfig.SurfaceHugLift * 0.5f,
                0.2f,
                out float distance,
                out _)
                && distance <= 0.001f;
        }
    }
}
