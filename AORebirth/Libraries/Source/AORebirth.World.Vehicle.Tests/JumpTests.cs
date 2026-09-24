using System;
using LostEden.Vehicles;
using LostEden.Vehicles.Surfaces;
using Xunit;

namespace LostEden.Vehicle.Tests
{
    /// <summary>
    /// The stock jump: <c>SimpleChar_t</c>'s height (<c>10058994</c>), <c>CharVehicle_t</c> slot 11
    /// (<c>1006ff32</c>), <c>Vehicle_t::Impact</c> (<c>1000a1b8</c>) and the landing notification
    /// (<c>1006f442</c>).
    /// </summary>
    public class JumpTests
    {
        sealed class Flat : ITileHeightSource
        {
            public int Width => 512;
            public int Height => 512;
            public float TileSize => 1f;
            public float SampleHeight(int x, int z) => 0f;
        }

        /// <summary>The flat terrain with a horizontal ceiling that only upward rays can hit.</summary>
        sealed class Ceiling : ISurface
        {
            readonly ISurface _ground = new TilemapSurface(new Flat());
            readonly float _y;

            public Ceiling(float y) => _y = y;

            public bool GetLineIntersection(
                Vec3 start, Vec3 end, out Vec3 hit, out Vec3 normal, bool clipToBounds, object locality)
            {
                if (end.Y > start.Y && start.Y < _y && _y <= end.Y)
                {
                    hit = new Vec3(start.X, _y, start.Z);
                    normal = new Vec3(0f, -1f, 0f);
                    return true;
                }

                return _ground.GetLineIntersection(start, end, out hit, out normal, clipToBounds, locality);
            }

            public bool VetoPosition(ref Vec3 position, object locality, Vec3 previous)
                => _ground.VetoPosition(ref position, locality, previous);

            public void CalculateClosestPoint(Vec3 point, out Vec3 closest, out Vec3 normal, object locality)
                => _ground.CalculateClosestPoint(point, out closest, out normal, locality);
        }

        static CharVehicleSim Body(ISurface surface = null)
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
                Surface = surface ?? new TilemapSurface(new Flat()),
                Position = new Vec3(20f, 0f, 20f),
            };
            sim.EnableFalling();
            sim.DisableSurfaceHug();
            sim.UpdateMotionConstraints();
            for (int i = 0; i < 30; i++)
                sim.Run(1f / 60f);
            return sim;
        }

        static float LaunchSpeed(float height) => MathF.Sqrt((height + height) * MathF.Abs(VehicleSim.GravityAccel));

        [Theory]
        [InlineData(0, 0, 0, 1f)]
        [InlineData(300, 300, 0, 4f)]
        [InlineData(100, 50, 0, 1.75f)]
        [InlineData(600, 600, 0, 5f)]       // past 800 a non-GM counts as exactly 800
        [InlineData(600, 600, 1, 7f)]       // a GM does not
        [InlineData(-200, -200, 0, 0.5f)]   // the 0.5 floor
        public void HeightFromStats(int strength, int agility, int gmLevel, float expected)
            => Assert.Equal(expected, CharVehicleSim.JumpHeightFromStats(strength, agility, gmLevel), 5);

        [Fact]
        public void LaunchesAtSqrtTwoGH()
        {
            CharVehicleSim sim = Body();
            Assert.False(sim.Airborne);

            Assert.True(sim.Jump(4f));

            Assert.True(sim.Airborne);
            Assert.Equal(4f, sim.JumpHeight);
            Assert.Equal(LaunchSpeed(4f), sim.VerticalVelocity, 4);
        }

        [Fact]
        public void PeaksNearTheHeightAndLandingClearsTheJump()
        {
            CharVehicleSim sim = Body();
            float ground = sim.Position.Y;
            int landed = 0;
            sim.JumpLanded += () => landed++;

            sim.Jump(2f);
            float peak = ground;
            for (int i = 0; i < 240 && (i == 0 || sim.Airborne); i++)
            {
                sim.Run(1f / 60f);
                peak = MathF.Max(peak, sim.Position.Y);
            }

            Assert.False(sim.Airborne);
            Assert.Equal(0f, sim.JumpHeight);
            Assert.Equal(1, landed);
            Assert.InRange(peak - ground, 1.8f, 2.1f);
        }

        [Fact]
        public void RefusedWhileAJumpIsInProgress()
        {
            CharVehicleSim sim = Body();
            sim.Jump(2f);
            sim.Run(1f / 60f);
            float vy = sim.VerticalVelocity;

            Assert.False(sim.Jump(4f));
            Assert.Equal(2f, sim.JumpHeight);
            Assert.Equal(vy, sim.VerticalVelocity);
        }

        [Fact]
        public void InTheAirWithoutAJumpItIsTakenButImpactDropsTheLaunch()
        {
            // No airborne gate in 1006ff32; Impact is what refuses.
            CharVehicleSim sim = Body();
            sim.BeginFalling();
            sim.VerticalVelocity = -3f;

            Assert.True(sim.Jump(2f));
            Assert.Equal(2f, sim.JumpHeight);
            Assert.Equal(-3f, sim.VerticalVelocity);
        }

        [Fact]
        public void CeilingClampsTheHeightToTheHeadroom()
        {
            // headroom = 3 - y - 2 * 1, about 1 for a body resting at the ground
            CharVehicleSim sim = Body(new Ceiling(3f));
            sim.OwnerBodyScale = 1f;
            float headroom = (float)((3.0 - sim.Position.Y) - 2.0);
            Assert.InRange(headroom, 0.9f, 1.0f);

            sim.Jump(4f);

            Assert.Equal(headroom, sim.JumpHeight, 5);
            Assert.Equal(LaunchSpeed(headroom), sim.VerticalVelocity, 4);
        }

        [Fact]
        public void CeilingAboveTheJumpLeavesItAlone()
        {
            CharVehicleSim sim = Body(new Ceiling(10f));

            sim.Jump(4f);

            Assert.Equal(4f, sim.JumpHeight);
        }

        [Fact]
        public void HeadroomIsFlooredAtOneTenth()
        {
            CharVehicleSim sim = Body(new Ceiling(1f));
            sim.OwnerBodyScale = 1f;

            sim.Jump(4f);

            Assert.Equal(0.1f, sim.JumpHeight, 5);
            Assert.Equal(LaunchSpeed(0.1f), sim.VerticalVelocity, 4);
        }

        [Fact]
        public void AnNpcStoresAtLeastOneAndAHalfButLaunchesAtTheRealHeight()
        {
            CharVehicleSim sim = Body(new Ceiling(1f));
            sim.OwnerBodyScale = 1f;
            sim.OwnerIsNpc = true;

            sim.Jump(4f);

            Assert.Equal(1.5f, sim.JumpHeight);
            Assert.Equal(LaunchSpeed(0.1f), sim.VerticalVelocity, 4);
        }

        [Fact]
        public void ImpactOnlyTakesAPurelyVerticalImpulse()
        {
            CharVehicleSim sim = Body();

            sim.Impact(new Vec3(1f, 100f, 0f));
            Assert.False(sim.Airborne);
            Assert.Equal(0f, sim.VerticalVelocity);

            sim.Impact(new Vec3(0f, 100f, 0f));
            Assert.True(sim.Airborne);
            Assert.Equal(2f, sim.VerticalVelocity, 5);     // 100 / mass 50
        }
    }
}
