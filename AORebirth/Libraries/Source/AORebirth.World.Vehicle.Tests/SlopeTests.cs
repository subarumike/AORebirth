using System;
using LostEden.Vehicles;
using LostEden.Vehicles.Surfaces;
using Xunit;

namespace LostEden.Vehicle.Tests
{
    /// <summary>
    /// Slope behaviour: you slide down steep faces and cannot climb them.
    ///
    /// <para>
    /// The two come from different places in stock. <b>Sliding</b> is the ground clamp leaving the
    /// body AIRBORNE when the surface normal fails the gate (<c>1000d557</c>), after which gravity
    /// does the work. <b>Not climbing</b> is the swept solver flattening a too-steep normal into a
    /// vertical wall (§6.6). Neither is a separate "slope limit" check on movement.
    /// </para>
    /// </summary>
    public class SlopeTests
    {
        /// <summary>A ramp rising along +X at a chosen gradient, flat before <c>startX</c>.</summary>
        sealed class Ramp : ITileHeightSource
        {
            readonly float _rise;
            readonly int _startX;

            public Ramp(float risePerTile, int startX = 10)
            {
                _rise = risePerTile;
                _startX = startX;
            }

            public int Width => 512;
            public int Height => 512;
            public float TileSize => 1f;
            public float SampleHeight(int x, int z) => x <= _startX ? 0f : (x - _startX) * _rise;
        }

        static CharVehicleSim Walker(ITileHeightSource tiles, Vec3 at)
        {
            var sim = new CharVehicleSim
            {
                Mass = 50f,
                MaxForce = 10f,
                MaxVel = 1f,
                NearProbeOffset = 0.5f,
                SlowingDistance = 1.5f,
                MovementState = 3,
                RunSpeedStat = 275f,
                Surface = new TilemapSurface(tiles),
                Position = at,
            };
            sim.EnableFalling();
            sim.DisableSurfaceHug();
            sim.UpdateMotionConstraints();
            return sim;
        }

        [Fact]
        public void AGentleSlopeIsWalkableAndTheBodyStaysGrounded()
        {
            // rise 0.3 per tile -> normal.y ~ 0.96, well above the 0.5 gate
            var sim = Walker(new Ramp(0.3f), new Vec3(20.5f, 3.2f, 20.5f));

            for (int i = 0; i < 120; i++)
                sim.Run(1f / 60f);

            Assert.False(sim.Airborne);
        }

        [Fact]
        public void ASteepSlopeLeavesTheBodyAirborneSoItSlides()
        {
            // rise 4 per tile -> normal.y ~ 0.24, below the 0.5 gate
            var sim = Walker(new Ramp(4f), new Vec3(20.5f, 42f, 20.5f));

            for (int i = 0; i < 30; i++)
                sim.Run(1f / 60f);

            Assert.True(sim.Airborne,
                "a face steeper than the gate must leave the body airborne, which is what makes it slide");
        }

        [Fact]
        public void TheGateIsNormalYOfAHalf()
        {
            // Bracket it: a normal just above 0.5 is walkable, just below is not.
            // rise r over a 1 m tile gives normal.y = 1/sqrt(1 + r*r).
            // r = 1.6 -> 0.530 (walkable); r = 1.9 -> 0.466 (not).
            var shallow = Walker(new Ramp(1.6f), new Vec3(20.5f, 17f, 20.5f));
            for (int i = 0; i < 120; i++)
                shallow.Run(1f / 60f);
            Assert.False(shallow.Airborne, "normal.y 0.53 should be walkable");

            var steep = Walker(new Ramp(1.9f), new Vec3(20.5f, 20f, 20.5f));
            for (int i = 0; i < 60; i++)
                steep.Run(1f / 60f);
            Assert.True(steep.Airborne, "normal.y 0.47 should not be walkable");
        }

        [Fact]
        public void RelaxingTheGateMakesASteepFaceWalkable()
        {
            // +0x13c drops the gate to 0.001, which is what that flag is for.
            var sim = Walker(new Ramp(4f), new Vec3(20.5f, 42f, 20.5f));
            sim.RelaxSlopeGate = true;

            for (int i = 0; i < 120; i++)
                sim.Run(1f / 60f);

            Assert.False(sim.Airborne);
        }

        [Theory]
        // normal.y = 1/sqrt(1+rise^2); the gate is 0.5, so rise 1.732 is the boundary.
        [InlineData(0.2f, true)]
        [InlineData(0.5f, true)]
        [InlineData(1.0f, true)]
        [InlineData(1.7f, true)]    // normal.y 0.507 -- just walkable
        [InlineData(1.9f, false)]   // normal.y 0.466 -- just not
        [InlineData(4.0f, false)]
        [InlineData(10.0f, false)]
        [InlineData(20.0f, false)]
        public void OnlyWalkableSlopesCanBeClimbed(float rise, bool shouldClimb)
        {
            var sim = Walker(new Ramp(rise, startX: 25), new Vec3(20.5f, 0.01f, 20.5f));
            sim.UseSurfaceNormal();
            sim.SetForwardDrive(1f);
            sim.BodyRotation = Quat.FromAxisAngle(Vec3.ReferenceUp, (float)(Math.PI / 2.0));

            for (int i = 0; i < 300; i++)
                sim.Run(1f / 60f);

            // A walkable slope is climbed for tens of metres; an unwalkable one stops within a few.
            // The small initial ride on an unwalkable face is stock's: the surface normal is smoothed
            // 0.9/0.1 per call (1000d577), so it takes ~7 calls to cross the gate and the body gets a
            // little way up before it is refused. Measured: 4.8-35.6 m when walkable, 0.5-2.0 m when
            // not.
            double ny = 1.0 / Math.Sqrt(1 + rise * rise);
            if (shouldClimb)
                Assert.True(sim.Position.Y > 4f,
                    $"rise {rise} (normal.y {ny:F3}) is walkable but the body only reached y={sim.Position.Y}");
            else
                Assert.True(sim.Position.Y < 3f,
                    $"rise {rise} (normal.y {ny:F3}) is not walkable but the body climbed to y={sim.Position.Y}");
        }

        [Fact]
        public void TheSurfaceNormalFromARayIsUnitLength()
        {
            // Intersect_Tile normalises before returning (10018a47). Without it the raw cross product
            // of two tile edges grows with the slope -- (-4,1,0) on a 4:1 face -- and every dot
            // product downstream is wrong by that factor. In the swept solver it shrank the push-out
            // by ~4x, which let the body creep into a face and walk up it.
            var surface = new TilemapSurface(new Ramp(4f, 10));

            Assert.True(surface.GetLineIntersection(
                new Vec3(14.5f, 30f, 20.5f), new Vec3(14.5f, -30f, 20.5f),
                out _, out Vec3 normal, true, null));

            Assert.Equal(1f, normal.Length, 3);
        }

        [Fact]
        public void TheSurfaceNormalIsSquaredUpOnAnythingSteep()
        {
            // Below the gate the normal handed to the orientation update becomes (0,1,0), so the
            // body is never banked over by a cliff face.
            var sim = Walker(new Ramp(4f), new Vec3(20.5f, 42f, 20.5f));
            sim.Run(1f / 60f);

            Assert.Equal(1f, sim.SurfaceNormal.Y, 3);
        }

        [Theory]
        [InlineData(1.9f)]
        [InlineData(2.5f)]
        [InlineData(4.0f)]
        [InlineData(10.0f)]
        [InlineData(20.0f)]
        public void HoldingForwardIntoAnUnwalkableSlopeMovesTheBodyNotOneMillimetre(float rise)
        {
            // The user's report, in one sentence: "if i try to hold W on a slope that i cant climb
            // it NEVER jitters, i am RUNNING in the SAME EXACT LOCATION and i dont for 1 frame go UP
            // the slope." That is the swept solver refusing the move outright (1000bee6) and then
            // bailing out of the slide because the body is already flush with the flattened face
            // (1000c120).
            var sim = Walker(new Ramp(rise, startX: 25), new Vec3(20.5f, 0.01f, 20.5f));
            sim.UseSurfaceNormal();
            sim.SetForwardDrive(1f);
            sim.BodyRotation = Quat.FromAxisAngle(Vec3.ReferenceUp, (float)(Math.PI / 2.0));

            float highest = sim.Position.Y;
            for (int i = 0; i < 600; i++)
            {
                sim.Run(1f / 60f);
                if (sim.Position.Y > highest)
                    highest = sim.Position.Y;
            }

            // The ramp starts at x = 25 and the body starts at x = 20.5, so it is free to walk the
            // 4.5 m of flat ground first; what it must never do is gain height.
            Assert.True(highest < 0.05f,
                $"rise {rise}: the body climbed to y={highest} (final y={sim.Position.Y}, x={sim.Position.X})");
        }

        [Fact]
        public void TheBodyComesToRestAgainstAnUnwalkableSlopeAndStopsMovingForward()
        {
            var sim = Walker(new Ramp(4f, startX: 25), new Vec3(20.5f, 0.01f, 20.5f));
            sim.UseSurfaceNormal();
            sim.SetForwardDrive(1f);
            sim.BodyRotation = Quat.FromAxisAngle(Vec3.ReferenceUp, (float)(Math.PI / 2.0));

            for (int i = 0; i < 400; i++)
                sim.Run(1f / 60f);

            float restX = sim.Position.X;
            float restY = sim.Position.Y;

            for (int i = 0; i < 120; i++)
                sim.Run(1f / 60f);

            Assert.Equal(restX, sim.Position.X, 3);
            Assert.Equal(restY, sim.Position.Y, 3);
        }
    }
}
