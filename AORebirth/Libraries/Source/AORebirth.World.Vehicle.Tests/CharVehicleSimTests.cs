using System;
using LostEden.Vehicles;
using Xunit;

namespace LostEden.Vehicle.Tests
{
    /// <summary>
    /// <see cref="CharVehicleSim"/> — the four input axes, the three steering channels and the speed
    /// model. See <c>Docs/Movement.md</c> §4.
    /// </summary>
    public class CharVehicleSimTests
    {
        static CharVehicleSim Player(int state = 3, float runStat = 0f) => new CharVehicleSim
        {
            // as the player factory builds it, 10057826
            Mass = 50f,
            MaxForce = 10f,
            MaxVel = 1f,
            NearProbeOffset = 0.5f,
            SlowingDistance = 1.5f,
            FallingEnabled = true,
            SurfaceHug = false,
            MovementState = state,
            RunSpeedStat = runStat,
        };

        // ---- the speed model, 1006f9eb ---------------------------------------

        [Theory]
        // state 3 forward: stat/275 + 5, clamped [1.5, 13]
        [InlineData(3, 1, 0f, 5f)]
        [InlineData(3, 1, 275f, 6f)]
        [InlineData(3, 1, 2200f, 13f)]        // 8 + 5 = 13, at the cap
        [InlineData(3, 1, 10000f, 13f)]       // past the cap
        // state 3 reverse: stat*0.7/275 + 3, clamped [1.05, 9.1]
        [InlineData(3, 2, 0f, 3f)]
        [InlineData(3, 2, 275f, 3.7f)]
        // state 4: stat*0.625/275 + 3, clamped [1.5, 8]
        [InlineData(4, 1, 0f, 3f)]
        [InlineData(4, 1, 440f, 4f)]
        // state 7 (Fly): stat/275 + 7, clamped [1.5, 15]
        [InlineData(7, 1, 0f, 7f)]
        [InlineData(7, 1, 275f, 8f)]
        // the constant states
        [InlineData(2, 1, 5000f, 1.5f)]
        [InlineData(5, 1, 5000f, 1f)]
        public void MaxVelFollowsTheStateCurve(int state, int direction, float stat, float expected)
        {
            CharVehicleSim sim = Player(state, stat);
            sim.CurveDirection = direction;

            sim.UpdateMotionConstraints();

            Assert.Equal(expected, sim.MaxVel, 3);
        }

        [Fact]
        public void MaxForceIsTwiceMassTimesSpeed()
        {
            // F = 2mv means the body reaches its max speed in 0.5 s.
            CharVehicleSim sim = Player(3, 275f);
            sim.UpdateMotionConstraints();

            Assert.Equal(6f, sim.MaxVel, 3);
            Assert.Equal(2f * 50f * 6f, sim.MaxForce, 2);
        }

        [Fact]
        public void TheBrakeDistanceIsAQuarterOfTheMaxSpeed()
        {
            // (v*v*m) / (2*v*m) * 0.5 reduces to v/4 in every branch.
            foreach (float stat in new[] { 0f, 275f, 1000f, 5000f })
            {
                CharVehicleSim sim = Player(3, stat);
                sim.UpdateMotionConstraints();
                Assert.Equal(sim.MaxVel / 4f, sim.SlowingDistance, 3);
            }
        }

        [Fact]
        public void MaxForceIsCappedAtTenThousand()
        {
            CharVehicleSim sim = Player(3, 5000f);
            sim.Mass = 500f;
            sim.UpdateMotionConstraints();

            Assert.Equal(10000f, sim.MaxForce, 1);
        }

        [Fact]
        public void MassIsFlooredByItsSetterSoTheZeroGuardIsUnreachable()
        {
            // 1006f9eb opens with `if (mass == 0) mass = 10`, but SetMass (1000a0a8) floors any
            // value <= 0 at 0.1, so that guard can only fire on a vehicle whose mass was never
            // set at all. Asserting the reachable behaviour rather than the dead branch.
            CharVehicleSim sim = Player();

            sim.Mass = 0f;
            Assert.Equal(0.1f, sim.Mass, 4);

            sim.UpdateMotionConstraints();
            Assert.Equal(0.1f, sim.Mass, 4);
        }

        [Fact]
        public void FlyTurnsGravityOff()
        {
            CharVehicleSim sim = Player(7);
            Assert.True(sim.FallingEnabled);

            sim.UpdateMotionConstraints();

            Assert.False(sim.FallingEnabled);
        }

        // ---- the strafe speed, slot 34 --------------------------------------

        [Theory]
        // half the forward curve, floor 0.75: clamp(stat*0.5/275 + 2.5, 0.75, 6.5)
        [InlineData(3, 0f, 2.5f)]
        [InlineData(3, 275f, 3f)]
        [InlineData(3, 10000f, 6.5f)]
        [InlineData(7, 0f, 3.5f)]
        [InlineData(2, 0f, 1.5f)]
        public void StrafeSpeedIsHalfTheForwardCurve(int state, float stat, float expected)
        {
            CharVehicleSim sim = Player(state, stat);
            Assert.Equal(expected, sim.StrafeSpeed(state), 3);
        }

        [Fact]
        public void SetStrafeKeepsOnlyTheSignOfItsArgument()
        {
            // 1007180f multiplies sign(requested) by the table speed, so the caller's magnitude
            // is discarded entirely.
            CharVehicleSim sim = Player(3, 275f);

            sim.SetStrafe(0.001f);
            Assert.Equal(3f, sim.Strafe, 3);

            sim.SetStrafe(1000f);
            Assert.Equal(3f, sim.Strafe, 3);

            sim.SetStrafe(-0.001f);
            Assert.Equal(-3f, sim.Strafe, 3);

            sim.SetStrafe(0f);
            Assert.Equal(0f, sim.Strafe, 3);
        }

        // ---- the longitudinal channel, 10071537 ------------------------------

        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        [InlineData(9)]
        public void StatesOneEightAndNineRefuseLongitudinalSteering(int state)
        {
            CharVehicleSim sim = Player(state);
            sim.SetForwardDrive(1f);
            sim.Run(1f / 60f);

            Assert.Equal(0f, sim.Speed, 4);
        }

        [Fact]
        public void APositiveDriveGoesForwardAndANegativeOneReverses()
        {
            CharVehicleSim forward = Player(3, 275f);
            forward.UpdateMotionConstraints();
            forward.SetForwardDrive(1f);
            for (int i = 0; i < 60; i++)
                forward.Run(1f / 60f);

            CharVehicleSim back = Player(3, 275f);
            back.UpdateMotionConstraints();
            back.SetForwardDrive(-1f);
            for (int i = 0; i < 60; i++)
                back.Run(1f / 60f);

            // body forward is +Z with an identity rotation
            Assert.True(forward.Position.Z > 0.5f, $"forward went to {forward.Position}");
            Assert.True(back.Position.Z < -0.5f, $"reverse went to {back.Position}");
        }

        [Fact]
        public void AZeroDriveProducesNoLongitudinalSteering()
        {
            CharVehicleSim sim = Player(3, 275f);
            sim.UpdateMotionConstraints();
            sim.SetForwardDrive(0f);

            for (int i = 0; i < 60; i++)
                sim.Run(1f / 60f);

            Assert.Equal(0f, sim.Speed, 4);
        }

        // ---- the lateral channel, 100716d5 -----------------------------------

        [Fact]
        public void StrafeMovesAlongBodyRight()
        {
            CharVehicleSim sim = Player(3, 275f);
            sim.UpdateMotionConstraints();
            sim.SetStrafe(1f);          // +3 m/s at this stat

            sim.Run(1f);

            // identity rotation puts body right at +X
            Assert.True(sim.Position.X > 2.5f, $"strafed to {sim.Position}");
            Assert.Equal(0f, sim.Position.Z, 3);
        }

        [Fact]
        public void TheVerticalAxisIsWorldUpNotBodyUp()
        {
            // The (0,1,0) is built inline at 10071740, so rolling the body must not tilt it.
            CharVehicleSim sim = Player(3, 275f);
            sim.UpdateMotionConstraints();
            sim.DisableFalling();
            sim.BodyRotation = Quat.FromAxisAngle(new Vec3(0f, 0f, 1f), (float)(Math.PI / 4.0));
            sim.SetVertical(2f);

            sim.Run(1f);

            Assert.True(sim.Position.Y > 1.5f, $"expected to rise, got {sim.Position}");
            Assert.Equal(0f, sim.Position.X, 3);
            Assert.Equal(0f, sim.Position.Z, 3);
        }

        [Fact]
        public void NoStrafeAndNoVerticalMeansNoLateralSteering()
        {
            CharVehicleSim sim = Player(3, 275f);
            sim.UpdateMotionConstraints();

            sim.Run(1f / 60f);

            Assert.Equal(0f, sim.Position.X, 5);
            Assert.Equal(0f, sim.Position.Z, 5);
        }

        // ---- the turn channel, 10071795 --------------------------------------

        [Fact]
        public void TurningWhileStandingStillRotatesTheBody()
        {
            CharVehicleSim sim = Player(3, 275f);
            sim.UpdateMotionConstraints();
            sim.SetTurnRate(1f);        // 1 rad/s

            sim.Run(0.5f);

            Vec3 forward = sim.GetBodyForward();
            // half a radian from +Z
            Assert.Equal((float)Math.Sin(0.5), forward.X, 2);
            Assert.Equal((float)Math.Cos(0.5), forward.Z, 2);
        }

        [Fact]
        public void TurningWhileMovingRotatesTheVelocityNotTheBody()
        {
            // The integrator rotates the BODY only when velocity.x and .z are both zero; otherwise
            // it turns the velocity vector. That is what makes a moving character arc.
            CharVehicleSim sim = Player(3, 275f);
            sim.UpdateMotionConstraints();
            sim.SetForwardDrive(1f);
            for (int i = 0; i < 60; i++)
                sim.Run(1f / 60f);

            Quat before = sim.BodyRotation;
            sim.SetTurnRate(1f);
            sim.Run(0.25f);

            // the body has not turned
            Assert.Equal(before.X, sim.BodyRotation.X, 4);
            Assert.Equal(before.Y, sim.BodyRotation.Y, 4);
            Assert.Equal(before.Z, sim.BodyRotation.Z, 4);
            Assert.Equal(before.W, sim.BodyRotation.W, 4);

            // but the velocity has
            Assert.True(Math.Abs(sim.Velocity.X) > 0.01f, $"velocity did not turn: {sim.Velocity}");
        }

        [Fact]
        public void AZeroTurnRateProducesNoTurnSteering()
        {
            CharVehicleSim sim = Player(3, 275f);
            sim.UpdateMotionConstraints();
            sim.SetTurnRate(0f);

            sim.Run(0.5f);

            Assert.Equal(0f, sim.GetBodyForward().X, 5);
            Assert.Equal(1f, sim.GetBodyForward().Z, 5);
        }

        // ---- body right ------------------------------------------------------

        [Fact]
        public void BodyRightIsTheRotationAppliedToXUnit()
        {
            CharVehicleSim sim = Player();
            Assert.Equal(1f, sim.CalcBodyRight().X, 4);

            sim.BodyRotation = Quat.FromAxisAngle(Vec3.ReferenceUp, (float)(Math.PI / 2.0));
            Vec3 right = sim.CalcBodyRight();
            Assert.Equal(-1f, right.Z, 3);
        }
    }
}
