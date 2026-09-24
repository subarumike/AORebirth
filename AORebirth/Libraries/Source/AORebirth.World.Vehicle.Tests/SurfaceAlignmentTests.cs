using System;
using LostEden.Vehicles;
using LostEden.Vehicles.Surfaces;
using Xunit;

namespace LostEden.Vehicle.Tests
{
    /// <summary>
    /// <c>Vehicle_t::EnsureSurfaceAlignment</c> (<c>1000d1aa</c>) driven through
    /// <see cref="VehicleSim"/>. See <c>Docs/Movement.md</c> §6 and §7.1c.
    /// </summary>
    public class SurfaceAlignmentTests
    {
        /// <summary>A vehicle with a surface bound, shaped like the player factory (10057826).</summary>
        sealed class WalkerSim : VehicleSim
        {
            readonly ISurface _surface;

            public WalkerSim(ISurface surface)
            {
                _surface = surface;
                Mass = 50f;
                MaxForce = 10f;
                MaxVel = 1f;
                NearProbeOffset = 0.5f;
                SlowingDistance = 1.5f;
                FallingEnabled = true;     // EnableFalling()
                SurfaceHug = false;        // DisableSurfaceHug()
            }

            protected override ISurface GetSurface() => _surface;

            public bool Align(Vec3 previous, bool force) => EnsureSurfaceAlignment(previous, force);
        }

        sealed class FlatHeights : ITileHeightSource
        {
            readonly float _y;
            public FlatHeights(float y, float tileSize = 1f) { _y = y; TileSize = tileSize; }
            public int Width => 256;
            public int Height => 256;
            public float TileSize { get; }
            public float SampleHeight(int x, int z) => _y;
        }

        static WalkerSim OnFlatGround(float y = 0f)
            => new WalkerSim(new TilemapSurface(new FlatHeights(y)));

        // ---- the no-surface early out ---------------------------------------

        [Fact]
        public void WithNoSurfaceBoundItIsANoOp()
        {
            // Stock's first line: no body or no surface -> return true, touch nothing.
            // This is what keeps the camera vehicles behaving as they did.
            var sim = new VehicleSim { Position = new Vec3(3f, 17f, 4f) };
            sim.Run(0.016f);
            Assert.Equal(17f, sim.Position.Y, 4);
        }

        // ---- settling onto the ground ---------------------------------------

        [Fact]
        public void ItDoesNotTeleportAFloatingBodyDownToTheGround()
        {
            // The ground clamp prevents penetration and handles landing; it does NOT pull a body
            // down. Gravity does that through the integrator -- see
            // AFallingVehicleComesToRestOnTheGroundAndStaysThere.
            WalkerSim sim = OnFlatGround(0f);
            sim.Position = new Vec3(10f, 6f, 10f);

            // The return value is "the position changed" (FUN_10009b1d), so a stationary body
            // legitimately reports false -- that is what breaks stock's sub-step loop.
            sim.Align(new Vec3(10f, 6f, 10f), false);

            Assert.Equal(6f, sim.Position.Y, 3);
        }

        [Fact]
        public void ABodyBelowTheGroundIsPushedBackUpToIt()
        {
            WalkerSim sim = OnFlatGround(12.5f);
            sim.Position = new Vec3(10f, 8f, 10f);

            sim.Align(new Vec3(10f, 8f, 10f), false);

            Assert.InRange(sim.Position.Y, 12.5f, 12.5f + VehicleSim.StepHeight + 1e-3f);
        }

        [Fact]
        public void TheHorizontalPositionIsNotDisturbedOnFlatGround()
        {
            WalkerSim sim = OnFlatGround(0f);
            sim.Position = new Vec3(10.25f, 3f, 7.75f);

            sim.Align(new Vec3(10.25f, 3f, 7.75f), false);

            Assert.Equal(10.25f, sim.Position.X, 3);
            Assert.Equal(7.75f, sim.Position.Z, 3);
        }

        [Fact]
        public void LandingClearsTheAirborneFlag()
        {
            WalkerSim sim = OnFlatGround(0f);
            sim.Position = new Vec3(10f, 0.005f, 10f);
            sim.Airborne = true;

            sim.Align(new Vec3(10f, 0.005f, 10f), false);

            Assert.False(sim.Airborne);
        }

        // ---- the surface normal ---------------------------------------------

        [Fact]
        public void FlatGroundGivesAnUpwardSurfaceNormal()
        {
            WalkerSim sim = OnFlatGround(0f);
            sim.Position = new Vec3(10f, 2f, 10f);

            sim.Align(new Vec3(10f, 2f, 10f), false);

            Assert.Equal(1f, sim.SurfaceNormal.Y, 3);
            Assert.Equal(0f, sim.SurfaceNormal.X, 3);
            Assert.Equal(0f, sim.SurfaceNormal.Z, 3);
        }

        [Fact]
        public void ASteepNormalIsReplacedByStraightUp()
        {
            // The gate is normal.y >= 0.5, and below it stock substitutes (0,1,0) rather than
            // keeping the steep normal (1000d588-ish, §6 step 8).
            var steep = new TilemapSurface(new FuncTiles(256, 256, 1f, (x, _) => x * 40f));
            var sim = new WalkerSim(steep) { Position = new Vec3(10f, 500f, 10f) };

            sim.Align(new Vec3(10f, 500f, 10f), false);

            Assert.Equal(1f, sim.SurfaceNormal.Y, 3);
        }

        sealed class FuncTiles : ITileHeightSource
        {
            readonly Func<int, int, float> _f;
            public FuncTiles(int w, int h, float tileSize, Func<int, int, float> f)
            { Width = w; Height = h; TileSize = tileSize; _f = f; }
            public int Width { get; }
            public int Height { get; }
            public float TileSize { get; }
            public float SampleHeight(int x, int z) => _f(x, z);
        }

        // ---- the veto retry --------------------------------------------------

        /// <summary>Vetoes every position until the caller has backed off far enough.</summary>
        sealed class VetoingSurface : ISurface
        {
            readonly ISurface _inner;
            readonly float _vetoAbove;
            public int Calls;

            public VetoingSurface(ISurface inner, float vetoAbove)
            {
                _inner = inner;
                _vetoAbove = vetoAbove;
            }

            public bool GetLineIntersection(Vec3 s, Vec3 e, out Vec3 hit, out Vec3 n, bool clip, object loc)
                => _inner.GetLineIntersection(s, e, out hit, out n, clip, loc);

            public void CalculateClosestPoint(Vec3 p, out Vec3 c, out Vec3 n, object loc)
                => _inner.CalculateClosestPoint(p, out c, out n, loc);

            public bool VetoPosition(ref Vec3 position, object locality, Vec3 previous)
            {
                Calls++;
                return position.X > _vetoAbove;
            }
        }

        [Fact]
        public void AVetoedMoveIsBackedOffTowardsThePreviousPosition()
        {
            var inner = new TilemapSurface(new FlatHeights(0f));
            var veto = new VetoingSurface(inner, vetoAbove: 10.5f);
            var sim = new WalkerSim(veto);

            // try to move from x=10 to x=20; everything past 10.5 is refused
            sim.Position = new Vec3(20f, 1f, 10f);
            sim.Align(new Vec3(10f, 1f, 10f), false);

            Assert.True(veto.Calls > 1, "the retry loop should have run more than once");

            // The loop runs i = 9 down to 1 (dec then `jg` at 1000d281), so the furthest it can
            // back off is one tenth of the move -- NOT all the way to the previous position.
            // tenth = (20 - 10) * 0.1 = 1.0, so the final attempt is x = 10 + 1.0 = 11.
            Assert.Equal(11f, sim.Position.X, 3);
        }

        [Fact]
        public void TheRetryGivesUpAfterNineAttempts()
        {
            var inner = new TilemapSurface(new FlatHeights(0f));
            // refuse everything
            var veto = new VetoingSurface(inner, vetoAbove: float.NegativeInfinity);
            var sim = new WalkerSim(veto);

            sim.Position = new Vec3(20f, 1f, 10f);
            sim.Align(new Vec3(10f, 1f, 10f), false);

            // 9 loop attempts plus the final placement veto
            Assert.Equal(VehicleSim.VetoRetries + 1, veto.Calls);
        }

        // ---- the run loop end to end ----------------------------------------

        [Fact]
        public void AFallingVehicleComesToRestOnTheGroundAndStaysThere()
        {
            WalkerSim sim = OnFlatGround(0f);
            sim.Position = new Vec3(20f, 8f, 20f);
            sim.Airborne = true;

            for (int i = 0; i < 200; i++)
                sim.Run(1f / 60f);

            Assert.InRange(sim.Position.Y, -0.05f, VehicleSim.StepHeight + 0.05f);
            Assert.False(sim.Airborne);
        }

        [Fact]
        public void ItDoesNotSinkThroughTheGroundOverManyFrames()
        {
            WalkerSim sim = OnFlatGround(5f);
            sim.Position = new Vec3(20f, 5f, 20f);

            for (int i = 0; i < 600; i++)
            {
                sim.Run(1f / 60f);
                Assert.True(sim.Position.Y > 4.5f,
                    $"sank to {sim.Position.Y} on frame {i}");
            }
        }
    }
}
