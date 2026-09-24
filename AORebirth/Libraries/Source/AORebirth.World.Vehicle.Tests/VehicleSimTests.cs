using System;
using System.Linq;
using System.Collections.Generic;
using LostEden.Vehicles;
using Xunit;

namespace LostEden.Vehicle.Tests;

/// <summary>
/// Locks down the recovered <c>Vehicle_t</c> integrator (Docs/Camera.md §4).
/// </summary>
public class VehicleSimTests
{
    /// <summary>A vehicle whose three steering channels are scripted by the test.</summary>
    sealed class ScriptedVehicle : VehicleSim
    {
        public Func<(SteeringResult, Vec3)> Longitudinal = () => (SteeringResult.None, Vec3.Zero);
        public Func<(SteeringResult, Vec3)> Lateral = () => (SteeringResult.None, Vec3.Zero);
        public Func<(SteeringResult, Vec3)> TurnChannel = () => (SteeringResult.None, Vec3.Zero);

        public Func<bool> SurfaceAlignment = () => true;

        public int Steps;
        public readonly List<float> StepSizes = new();
        public int HaltCalls;

        protected override SteeringResult CalcSteering(out Vec3 force)
        {
            Steps++;
            StepSizes.Add(DeltaTimeNow);
            (SteeringResult r, force) = Longitudinal();
            return r;
        }

        protected override SteeringResult CalcLateralSteering(out Vec3 lateral)
        {
            (SteeringResult r, lateral) = Lateral();
            return r;
        }

        protected override SteeringResult CalcTurnSteering(out Vec3 turn)
        {
            (SteeringResult r, turn) = TurnChannel();
            return r;
        }

        protected override bool EnsureSurfaceAlignment(Vec3 previousPosition, bool onlyOnce)
            => SurfaceAlignment();

        public override void Halt()
        {
            HaltCalls++;
            base.Halt();
        }
    }

    // ---- Vec3.Truncate (FUN_1000f12a) -----------------------------------

    [Fact]
    public void Truncate_LeavesShortVectorAloneAndReturnsItsLength()
    {
        var v = new Vec3(3f, 0f, 4f);
        float len = Vec3.Truncate(ref v, 10f);

        Assert.Equal(5f, len, 5);
        Assert.Equal(new Vec3(3f, 0f, 4f), v);
    }

    [Fact]
    public void Truncate_ScalesLongVectorDownAndReturnsTheMax()
    {
        var v = new Vec3(3f, 0f, 4f);
        float len = Vec3.Truncate(ref v, 2.5f);

        Assert.Equal(2.5f, len, 5);
        Assert.Equal(2.5f, v.Length, 5);
        Assert.Equal(1.5f, v.X, 5);
        Assert.Equal(2.0f, v.Z, 5);
    }

    [Fact]
    public void Truncate_ZeroVectorReturnsZeroAndDoesNotDivide()
    {
        var v = Vec3.Zero;
        float len = Vec3.Truncate(ref v, 5f);

        Assert.Equal(0f, len);
        Assert.Equal(Vec3.Zero, v);
    }

    // ---- the sub-stepping loop (1000e3d3) -------------------------------

    [Fact]
    public void Step_LongFrameIsSplitIntoSubStepsCappedAtMaxSubStep()
    {
        var v = new ScriptedVehicle { MaxSubStep = 0.4f };

        v.Run(1.0f);

        // 0.4 + 0.4 + 0.2
        Assert.Equal(3, v.Steps);
        Assert.Equal(new[] { 0.4f, 0.4f, 0.2f }, v.StepSizes.ConvertAll(s => MathF.Round(s, 5)));
    }

    [Fact]
    public void Step_ShortFrameIsOneStepOfExactlyThatLength()
    {
        var v = new ScriptedVehicle { MaxSubStep = 0.4f };

        v.Run(1f / 60f);

        Assert.Equal(1, v.Steps);
        Assert.Equal(1f / 60f, v.StepSizes[0], 6);
    }

    [Fact]
    public void Step_FrameLongerThanFourSecondsIsDroppedEntirely()
    {
        var v = new ScriptedVehicle();

        bool moved = v.Run(4.001f);

        Assert.False(moved);
        Assert.Equal(0, v.Steps);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    public void Step_NonPositiveFrameIsDropped(float dt)
    {
        var v = new ScriptedVehicle();

        Assert.False(v.Run(dt));
        Assert.Equal(0, v.Steps);
    }

    [Fact]
    public void Step_BlockedSurfaceAlignmentBreaksOutOfTheSubStepLoop()
    {
        var v = new ScriptedVehicle { MaxSubStep = 0.4f, SurfaceAlignment = () => false };

        v.Run(1.0f);

        Assert.Equal(1, v.Steps);
    }

    static ScriptedVehicle Thruster(float maxSubStep) => new()
    {
        Mass = 50f,
        MaxForce = 1000f,
        MaxVel = 100f,
        MaxSubStep = maxSubStep,
        Longitudinal = () => (SteeringResult.Force, new Vec3(0f, 0f, 500f)),
    };

    static float TravelledOver(float seconds, float frameTime, float maxSubStep)
    {
        var v = Thruster(maxSubStep);
        int frames = (int)MathF.Round(seconds / frameTime);
        for (int i = 0; i < frames; i++)
            v.Run(frameTime);
        return v.Position.Z;
    }

    /// <summary>
    /// The invariant sub-stepping actually guarantees: no single integration step is ever longer
    /// than the cap, whatever the frame time. This is the thing stock relies on — not exactness.
    /// </summary>
    [Theory]
    [InlineData(0.01f)]
    [InlineData(1f / 60f)]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(3.9f)]
    public void Step_NoSubStepEverExceedsTheCap(float frameTime)
    {
        var v = new ScriptedVehicle { MaxSubStep = VehicleSim.CameraSubStep };

        v.Run(frameTime);

        Assert.NotEmpty(v.StepSizes);
        Assert.All(v.StepSizes, s => Assert.True(s <= VehicleSim.CameraSubStep + 1e-6f,
            $"step {s} exceeded the cap"));
        Assert.Equal(frameTime, v.StepSizes.Sum(), 4);
    }

    /// <summary>
    /// Sub-stepping bounds frame-rate divergence, it does not remove it: the integrator is plain
    /// Euler, so the step size still moves the answer. Documents the measured behaviour rather than
    /// the tempting-but-false claim that stock is frame-rate independent (Docs/Camera.md §4.1).
    /// </summary>
    [Fact]
    public void Step_FrameRateStillChangesTheTrajectory_ButTheCapBoundsIt()
    {
        // Characters: the 0.4 s cap never binds at 10 fps or above, so nothing is bounded at all.
        float loose10 = TravelledOver(6f, 0.1f, 0.4f);
        float loose144 = TravelledOver(6f, 1f / 144f, 0.4f);

        // The camera's 0.05 s cap does bind at 10 fps, and halves the spread.
        float tight10 = TravelledOver(6f, 0.1f, VehicleSim.CameraSubStep);
        float tight144 = TravelledOver(6f, 1f / 144f, VehicleSim.CameraSubStep);

        float looseSpread = MathF.Abs(loose10 - loose144);
        float tightSpread = MathF.Abs(tight10 - tight144);

        Assert.True(looseSpread > 2f, $"expected the uncapped spread to be real, got {looseSpread}");
        Assert.True(tightSpread < looseSpread / 2f,
            $"expected the camera cap to at least halve the spread, {tightSpread} vs {looseSpread}");
    }

    /// <summary>
    /// What the cap is really for: a loading hitch must not fling the body across the map.
    /// </summary>
    [Fact]
    public void Step_TheCapLimitsHowFarAHitchThrowsTheVehicle()
    {
        float smooth = TravelledOver(3f, 1f / 60f, VehicleSim.CameraSubStep);
        float hitched = TravelledOver(3f, 0.5f, VehicleSim.CameraSubStep);
        float uncapped = TravelledOver(3f, 0.5f, 0.4f);

        Assert.True(MathF.Abs(hitched - smooth) < MathF.Abs(uncapped - smooth) / 5f,
            $"camera cap {hitched} vs character cap {uncapped}, smooth {smooth}");
    }

    // ---- force integration ----------------------------------------------

    [Fact]
    public void Force_IsTruncatedToMaxForceBeforeIntegration()
    {
        var v = new ScriptedVehicle
        {
            Mass = 50f,
            MaxForce = 100f,
            MaxVel = 1000f,
            MaxSubStep = 1f,
            Longitudinal = () => (SteeringResult.Force, new Vec3(0f, 0f, 10000f)),
        };

        v.Run(1f);

        // a = F/m = 100/50 = 2 m/s^2 over 1 s
        Assert.Equal(2f, v.Velocity.Z, 4);
    }

    [Fact]
    public void Force_VelocityIsTruncatedToMaxVelAndReportedAsSpeed()
    {
        var v = new ScriptedVehicle
        {
            Mass = 50f,
            MaxForce = 1000f,
            MaxVel = 3f,
            MaxSubStep = 1f,
            Longitudinal = () => (SteeringResult.Force, new Vec3(0f, 0f, 1000f)),
        };

        v.Run(1f);

        Assert.Equal(3f, v.Velocity.Z, 4);
        Assert.Equal(3f, v.Speed, 4);
    }

    [Fact]
    public void Force_ResultOtherThanForceLeavesVelocityAlone()
    {
        var v = new ScriptedVehicle
        {
            MaxSubStep = 1f,
            Longitudinal = () => (SteeringResult.None, new Vec3(0f, 0f, 1000f)),
        };

        v.Run(1f);

        Assert.Equal(Vec3.Zero, v.Velocity);
        Assert.Equal(Vec3.Zero, v.SteerForce);
    }

    [Fact]
    public void Halt_ZeroesTheChannelAndCallsHalt()
    {
        var v = new ScriptedVehicle
        {
            MaxSubStep = 1f,
            Velocity = new Vec3(0f, 0f, 5f),
            Longitudinal = () => (SteeringResult.Halt, new Vec3(0f, 0f, 1000f)),
        };

        v.Run(1f);

        Assert.Equal(1, v.HaltCalls);
        Assert.Equal(Vec3.Zero, v.Velocity);
        Assert.Equal(Vec3.Zero, v.SteerForce);
    }

    // ---- gravity ----------------------------------------------------------

    [Fact]
    public void Gravity_AccumulatesOnlyWhileAirborne()
    {
        var v = new ScriptedVehicle { MaxSubStep = 1f, Airborne = true };

        v.Run(1f);

        Assert.Equal(VehicleSim.GravityAccel, v.VerticalVelocity, 4);
        Assert.Equal(VehicleSim.GravityAccel, v.Position.Y, 4);
    }

    [Fact]
    public void Gravity_GroundedWithFallingEnabledClearsVerticalMotion()
    {
        var v = new ScriptedVehicle
        {
            MaxSubStep = 1f,
            Airborne = false,
            FallingEnabled = true,
            VerticalVelocity = -12f,
            Velocity = new Vec3(0f, -3f, 0f),
        };

        v.Run(1f);

        Assert.Equal(0f, v.VerticalVelocity);
        Assert.Equal(0f, v.Velocity.Y);
    }

    [Fact]
    public void Gravity_VerticalVelocityIsClampedToMaxFallSpeed()
    {
        var v = new ScriptedVehicle
        {
            MaxSubStep = 0.4f,
            Airborne = true,
            MaxForce = 0f,
        };

        // 20 m/s^2 for 4 s would reach -80 without the clamp.
        for (int i = 0; i < 10; i++)
            v.Run(0.4f);

        Assert.Equal(-VehicleSim.MaxFallSpeed, v.VerticalVelocity, 4);
    }

    // ---- lateral channel --------------------------------------------------

    [Fact]
    public void Lateral_WhileStoppedIsTruncatedToMaxVelAndMovesPosition()
    {
        var v = new ScriptedVehicle
        {
            MaxSubStep = 1f,
            MaxVel = 2f,
            Lateral = () => (SteeringResult.Lateral, new Vec3(10f, 0f, 0f)),
        };

        v.Run(1f);

        Assert.Equal(2f, v.Position.X, 4);
    }

    /// <summary>
    /// Strafing redirects rather than adding speed: the combined vector is rescaled to the current
    /// speed, so a vehicle already at full speed does not gain any by strafing.
    /// </summary>
    [Fact]
    public void Lateral_WhileMovingRescalesSoCombinedSpeedIsPreserved()
    {
        var v = new ScriptedVehicle
        {
            Mass = 50f,
            MaxForce = 1000f,
            MaxVel = 4f,
            MaxSubStep = 1f,
            Longitudinal = () => (SteeringResult.Force, new Vec3(0f, 0f, 1000f)),
            Lateral = () => (SteeringResult.Lateral, new Vec3(3f, 0f, 0f)),
        };

        v.Run(1f);

        // Forward saturates at MaxVel = 4; lateral is 3. Combined would be 5, rescaled to 4.
        float travelled = new Vec3(v.Position.X, 0f, v.Position.Z).Length;
        Assert.Equal(4f, travelled, 3);
    }

    // ---- turn channel -----------------------------------------------------

    [Fact]
    public void Turn_WhileStationaryRotatesTheBody()
    {
        var v = new ScriptedVehicle
        {
            MaxSubStep = 1f,
            TurnChannel = () => (SteeringResult.Turn, new Vec3(0f, MathF.PI / 2f, 0f)),
        };

        v.Run(1f);

        // A quarter turn about +Y takes (0,0,1) to (1,0,0).
        Vec3 forward = v.BodyRotation * Vec3.ReferenceForward;
        Assert.Equal(1f, forward.X, 4);
        Assert.Equal(0f, forward.Z, 4);
        Assert.Equal(Vec3.Zero, v.Velocity);
    }

    [Fact]
    public void Turn_WhileMovingRotatesTheVelocityAndLeavesTheBody()
    {
        var v = new ScriptedVehicle
        {
            MaxSubStep = 1f,
            Velocity = new Vec3(0f, 0f, 5f),
            TurnChannel = () => (SteeringResult.Turn, new Vec3(0f, MathF.PI / 2f, 0f)),
        };

        v.Run(1f);

        Assert.Equal(5f, v.Velocity.X, 4);
        Assert.Equal(0f, v.Velocity.Z, 4);
        Assert.Equal(Quat.Identity, v.BodyRotation);
    }

    [Fact]
    public void Turn_AxisShorterThanEpsilonIsIgnored()
    {
        var v = new ScriptedVehicle
        {
            MaxSubStep = 1f,
            TurnChannel = () => (SteeringResult.Turn, new Vec3(0f, 1e-6f, 0f)),
        };

        v.Run(1f);

        Assert.Equal(Quat.Identity, v.BodyRotation);
    }

    // ---- SteeringArrive (1000ab28) ---------------------------------------

    [Fact]
    public void SteeringArrive_HaltsInsideTheHaltRadius()
    {
        var v = new ScriptedVehicle { Position = Vec3.Zero, HaltRadius = 2f };

        SteeringResult r = v.SteeringArrive(new Vec3(0f, 0f, 1f), out Vec3 steer);

        Assert.Equal(SteeringResult.Halt, r);
        Assert.Equal(Vec3.Zero, steer);
    }

    [Fact]
    public void SteeringArrive_HaltsInsideTheSquaredSpaceEpsilonEvenWithNoHaltRadius()
    {
        var v = new ScriptedVehicle { Position = Vec3.Zero, HaltRadius = 0f };

        // |d|^2 = 0.0081 < 0.01
        SteeringResult r = v.SteeringArrive(new Vec3(0f, 0f, 0.09f), out _);

        Assert.Equal(SteeringResult.Halt, r);
    }

    [Fact]
    public void SteeringArrive_FarAwayAimsAtMaxVelAndReturnsAForce()
    {
        var v = new ScriptedVehicle
        {
            Position = Vec3.Zero,
            Velocity = Vec3.Zero,
            Mass = 50f,
            MaxVel = 4f,
            SlowingDistance = 0.1f,
        };

        SteeringResult r = v.SteeringArrive(new Vec3(0f, 0f, 100f), out Vec3 steer);

        Assert.Equal(SteeringResult.Force, r);
        // desired = 4 m/s along +Z, velocity 0, so steer = 4 * 50 * 4 = 800.
        Assert.Equal(800f, steer.Z, 3);
        Assert.Equal(0f, steer.X, 4);
    }

    /// <summary>
    /// Inside the arrival radius the desired speed falls off linearly with distance — this is the
    /// slow-down that makes the camera settle instead of overshooting.
    /// </summary>
    [Fact]
    public void SteeringArrive_InsideSlowingDistanceScalesTheDesiredSpeedDown()
    {
        var v = new ScriptedVehicle
        {
            Position = Vec3.Zero,
            Velocity = Vec3.Zero,
            Mass = 50f,
            MaxVel = 4f,
            SlowingDistance = 2f,
            HaltRadius = 0f,
        };

        v.SteeringArrive(new Vec3(0f, 0f, 1f), out Vec3 steer);

        // d/radius = 0.5, so desired = 2 m/s; steer = 2 * 50 * 4 = 400.
        Assert.Equal(400f, steer.Z, 3);
    }

    [Fact]
    public void SteeringArrive_SubtractsCurrentVelocitySoItBrakesWhenOvershooting()
    {
        var v = new ScriptedVehicle
        {
            Position = Vec3.Zero,
            Velocity = new Vec3(0f, 0f, 10f),
            Mass = 50f,
            MaxVel = 4f,
            SlowingDistance = 0.1f,
        };

        v.SteeringArrive(new Vec3(0f, 0f, 100f), out Vec3 steer);

        // desired 4, current 10 -> (4 - 10) * 50 * 4 = -1200, i.e. braking.
        Assert.Equal(-1200f, steer.Z, 3);
    }

    /// <summary>End to end: a vehicle steered by Arrive converges on its target and stops there.</summary>
    [Fact]
    public void SteeringArrive_DrivesTheVehicleToItsTargetAndSettles()
    {
        ScriptedVehicle v = null;
        var target = new Vec3(0f, 0f, 10f);

        v = new ScriptedVehicle
        {
            Mass = 50f,
            MaxForce = 500f,
            MaxVel = 5f,
            SlowingDistance = 2f,
            HaltRadius = 0.05f,
            MaxSubStep = 0.02f,
        };
        v.Longitudinal = () =>
        {
            SteeringResult r = v.SteeringArrive(target, out Vec3 steer);
            return (r, steer);
        };

        for (int i = 0; i < 600; i++)   // 6 s at 100 fps
            v.Run(0.01f);

        Assert.True(Math.Abs(v.Position.Z - target.Z) < 0.2f,
            $"expected to arrive near z=10, got {v.Position}");
        Assert.True(v.Speed < 0.5f, $"expected to have settled, speed was {v.Speed}");
    }
}
