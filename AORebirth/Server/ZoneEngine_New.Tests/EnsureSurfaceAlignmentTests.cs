namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Movement;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class EnsureSurfaceAlignmentTests
    {
        [TestMethod]
        public void Tripod_OnFlatPlane_SnapsToPlaneHeight()
        {
            var surface = new PlaneSurface(5f);
            Assert.IsTrue(EnsureSurfaceAlignment.TryTripod(
                surface,
                new Vector3(10, 8, 3),
                out float groundY,
                out Vector3 normal));

            Assert.AreEqual(5f, groundY, 0.01f);
            Assert.AreEqual(0f, (float)normal.x, 0.05f);
            Assert.AreEqual(1f, (float)normal.y, 0.05f);
            Assert.AreEqual(0f, (float)normal.z, 0.05f);
        }

        [TestMethod]
        public void Tripod_AllMisses_KeepsDest()
        {
            var surface = new MissSurface();
            var dest = new Vector3(10, 35, 3);
            EnsureSurfaceAlignment.Result result = EnsureSurfaceAlignment.Apply(
                surface,
                dest,
                dest,
                allowSlide: false);
            Assert.AreEqual(35f, (float)result.Position.y, 0.01f);
        }

        [TestMethod]
        public void Apply_WithoutSlide_KeepsPlanarPathSample()
        {
            var surface = new PlaneSurface(2f);
            var previous = new Vector3(0, 2, 0);
            var desired = new Vector3(3, 2, 0);

            EnsureSurfaceAlignment.Result result = EnsureSurfaceAlignment.Apply(
                surface,
                previous,
                desired,
                allowSlide: false);

            Assert.AreEqual(3f, (float)result.Position.x, 0.01f);
            Assert.AreEqual(2f, (float)result.Position.y, 0.01f);
            Assert.AreEqual(0f, (float)result.Position.z, 0.01f);
        }

        [TestMethod]
        public void Slide_StopsAgainstVerticalWall()
        {
            var surface = new WallSurface(x: 1f);
            var previous = new Vector3(0, 1, 0);
            var desired = new Vector3(4, 1, 0);

            Vector3 slid = EnsureSurfaceAlignment.Slide(surface, previous, desired);
            Assert.IsTrue((float)slid.x <= 1.05f);
            Assert.IsTrue((float)slid.x >= 0f);
        }

        sealed class MissSurface : IVehicleSurface
        {
            public bool TryLinecast(Vector3 from, Vector3 to, out Vector3 hit, out Vector3 normal)
            {
                hit = from;
                normal = new Vector3(0, 1, 0);
                return false;
            }

            public bool OverlapsTorso(Vector3 foot) => false;
        }

        sealed class PlaneSurface : IVehicleSurface
        {
            readonly float _y;

            public PlaneSurface(float y) => _y = y;

            public bool TryLinecast(Vector3 from, Vector3 to, out Vector3 hit, out Vector3 normal)
            {
                normal = new Vector3(0, 1, 0);
                hit = from;
                double dy = to.y - from.y;
                if (dy >= 0)
                    return false;
                if (from.y < _y || to.y > _y)
                    return false;

                double t = (from.y - _y) / -dy;
                hit = new Vector3(
                    from.x + ((to.x - from.x) * t),
                    _y,
                    from.z + ((to.z - from.z) * t));
                return true;
            }

            public bool OverlapsTorso(Vector3 foot) => false;
        }

        sealed class WallSurface : IVehicleSurface
        {
            readonly float _x;

            public WallSurface(float x) => _x = x;

            public bool TryLinecast(Vector3 from, Vector3 to, out Vector3 hit, out Vector3 normal)
            {
                normal = new Vector3(-1, 0, 0);
                hit = from;
                double dx = to.x - from.x;
                if (dx <= 0)
                    return false;
                if (from.x >= _x || to.x < _x)
                    return false;

                double t = (_x - from.x) / dx;
                hit = new Vector3(
                    _x,
                    from.y + ((to.y - from.y) * t),
                    from.z + ((to.z - from.z) * t));
                return true;
            }

            public bool OverlapsTorso(Vector3 foot) => false;
        }
    }
}