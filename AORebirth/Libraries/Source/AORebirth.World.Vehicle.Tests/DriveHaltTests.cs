using LostEden.Vehicles;
using LostEden.Vehicles.Surfaces;
using Xunit;

namespace LostEden.Vehicle.Tests
{
    /// <summary>
    /// The drive/halt pairing. Stock calls <c>Vehicle_t::Halt()</c> immediately after every
    /// <c>SetForwardDrive</c> (<c>1006f0ca</c>/<c>1006f0d2</c>, <c>1006f257</c>/<c>1006f25f</c>) and
    /// the full-stop handler <c>FUN_1006df31</c> halts before zeroing all three axes.
    ///
    /// <para>
    /// Without it a released key leaves the body coasting: the longitudinal channel returns
    /// <see cref="SteeringResult.None"/>, so the integrator never touches <c>Velocity</c>.
    /// </para>
    /// </summary>
    public class DriveHaltTests
    {
        sealed class Flat : ITileHeightSource
        {
            public int Width => 512;
            public int Height => 512;
            public float TileSize => 1f;
            public float SampleHeight(int x, int z) => 0f;
        }

        static CharVehicleSim Walker()
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
                Surface = new TilemapSurface(new Flat()),
                Position = new Vec3(20f, 0.01f, 20f),
            };
            sim.EnableFalling();
            sim.DisableSurfaceHug();
            sim.UpdateMotionConstraints();
            return sim;
        }

        [Fact]
        public void WithoutAHaltAReleasedDriveWouldCoastForever()
        {
            // Documents the integrator behaviour the halt exists to defeat: zeroing the drive alone
            // leaves the velocity untouched, so the body keeps travelling at its last speed.
            CharVehicleSim sim = Walker();
            sim.SetForwardDrive(1f);
            for (int i = 0; i < 60; i++)
                sim.Run(1f / 60f);

            Assert.True(sim.Speed > 5f);

            sim.SetForwardDrive(0f);        // the axis alone, no halt
            Vec3 a = sim.Position;
            for (int i = 0; i < 120; i++)
                sim.Run(1f / 60f);
            Vec3 b = sim.Position;

            Assert.True((b - a).Length > 5f,
                $"expected coasting without a halt, moved {(b - a).Length}");
        }

        [Fact]
        public void HaltingOnReleaseStopsTheBodyDead()
        {
            CharVehicleSim sim = Walker();
            sim.SetForwardDrive(1f);
            for (int i = 0; i < 60; i++)
                sim.Run(1f / 60f);

            sim.SetForwardDrive(0f);
            sim.Halt();                     // what stock pairs with the setter

            Vec3 a = sim.Position;
            for (int i = 0; i < 120; i++)
                sim.Run(1f / 60f);
            Vec3 b = sim.Position;

            Assert.Equal(0f, sim.Speed, 4);
            Assert.True((b - a).Length < 0.01f,
                $"expected to stop dead, drifted {(b - a).Length}");
        }

        [Fact]
        public void HaltingOnAReversalMakesItStartFromRest()
        {
            CharVehicleSim sim = Walker();
            sim.SetForwardDrive(1f);
            for (int i = 0; i < 60; i++)
                sim.Run(1f / 60f);

            Assert.True(sim.Speed > 5f);

            sim.SetForwardDrive(-1f);
            sim.Halt();

            Assert.Equal(0f, sim.Speed, 4);
        }

        // ---- the direction pairing (backpedalling) ---------------------------
        //
        // Stock's three longitudinal command handlers, each read in full:
        //   forward   1006ef8d:  SetDirection(1);  SetForwardDrive(1)
        //   backward  1006f122:  SetForwardDrive(-1);  SetDirection(-1)
        //   release   1006f23a:  SetForwardDrive(0);  Halt();  SetDirection(1)

        static void Drive(CharVehicleSim sim, float drive)
        {
            if (drive == sim.ForwardDrive)
                return;

            if (drive > 0f)
            {
                sim.SetDirection(1);
                sim.SetForwardDrive(drive);
            }
            else if (drive < 0f)
            {
                sim.SetForwardDrive(drive);
                sim.SetDirection(-1);
            }
            else
            {
                sim.SetForwardDrive(0f);
                sim.Halt();
                sim.SetDirection(1);
            }
        }

        static CharVehicleSim FacingPositiveX()
        {
            CharVehicleSim sim = Walker();
            sim.UseSurfaceNormal();
            sim.BodyRotation = Quat.FromAxisAngle(Vec3.ReferenceUp, (float)(System.Math.PI / 2.0));
            for (int i = 0; i < 20; i++)
                sim.Run(1f / 60f);
            return sim;
        }

        [Fact]
        public void ABackpedallingBodyActuallyTravelsBackwards()
        {
            CharVehicleSim sim = FacingPositiveX();
            float startX = sim.Position.X;

            Drive(sim, -1f);
            for (int i = 0; i < 90; i++)
                sim.Run(1f / 60f);

            // It must cover real ground, not twitch on the spot.
            Assert.True(sim.Position.X < startX - 5f,
                $"backpedalled only {startX - sim.Position.X} m (x {startX} -> {sim.Position.X})");
        }

        [Fact]
        public void ABackpedallingBodyKeepsFacingForwards()
        {
            // 1000c8f1: with Direction < 0 the visible rotation is built from the NEGATED forward,
            // so the character walks backwards while still looking where it came from.
            CharVehicleSim sim = FacingPositiveX();

            Drive(sim, -1f);
            for (int i = 0; i < 90; i++)
                sim.Run(1f / 60f);

            Vec3 facing = sim.BodyRotation * Vec3.ReferenceForward;
            Assert.Equal(1f, facing.X, 3);
            Assert.Equal(0f, facing.Z, 3);

            // and the un-negated rotation is kept beside it (+0x70)
            Vec3 travelling = sim.SavedRotation * Vec3.ReferenceForward;
            Assert.Equal(-1f, travelling.X, 3);
        }

        [Fact]
        public void BackpedallingDoesNotOscillate()
        {
            // The bug this closes: without SetDirection(-1) the orientation update faced the body
            // down its own backward velocity, SteeringReverse then pushed the other way, and the
            // body flipped sign every frame. Every step must go the same way.
            CharVehicleSim sim = FacingPositiveX();
            Drive(sim, -1f);

            for (int i = 0; i < 90; i++)
            {
                float before = sim.Position.X;
                sim.Run(1f / 60f);
                Assert.True(sim.Position.X <= before,
                    $"frame {i} moved forwards: {before} -> {sim.Position.X}");
            }
        }

        [Fact]
        public void ReversingFromARunStartsFromRest()
        {
            // SetDirection halts when the value changes (FUN_1000a688 at 1000a704).
            CharVehicleSim sim = FacingPositiveX();
            Drive(sim, 1f);
            for (int i = 0; i < 60; i++)
                sim.Run(1f / 60f);
            Assert.True(sim.Speed > 5f);

            Drive(sim, -1f);
            Assert.Equal(0f, sim.Speed, 5);
            Assert.True(sim.Velocity.IsZero);
        }

        [Fact]
        public void ReleasingBackwardRestoresTheForwardDirection()
        {
            CharVehicleSim sim = FacingPositiveX();
            Drive(sim, -1f);
            for (int i = 0; i < 30; i++)
                sim.Run(1f / 60f);
            Assert.Equal(-1, sim.Direction);

            Drive(sim, 0f);
            Assert.Equal(1, sim.Direction);
            Assert.Equal(0f, sim.Speed, 5);
        }

        [Fact]
        public void TurningOnTheSpotIsNotUndoneByTheOrientationUpdate()
        {
            // The cached forward (+0xc0) is what mode 1 rebuilds the rotation from while stopped, so
            // every writer of the rotation has to refresh it (1000e7dc, 1000d14c, 1000c8da).
            CharVehicleSim sim = FacingPositiveX();
            sim.BodyRotation = Quat.FromAxisAngle(Vec3.ReferenceUp, 0f);   // face +Z

            for (int i = 0; i < 10; i++)
                sim.Run(1f / 60f);

            Vec3 facing = sim.BodyRotation * Vec3.ReferenceForward;
            Assert.Equal(0f, facing.X, 3);
            Assert.Equal(1f, facing.Z, 3);
        }

        [Fact]
        public void SetRelRotReAimsTheVelocityThroughDirection()
        {
            CharVehicleSim sim = FacingPositiveX();
            Drive(sim, -1f);
            for (int i = 0; i < 60; i++)
                sim.Run(1f / 60f);

            float speed = sim.Velocity.Length;
            Assert.True(speed > 5f);

            // face +Z instead; a body with Direction -1 must now travel towards -Z
            sim.SetRelRot(Quat.FromAxisAngle(Vec3.ReferenceUp, 0f));

            Assert.Equal(speed, sim.Velocity.Length, 3);
            Assert.Equal(-speed, sim.Velocity.Z, 3);
            Assert.Equal(0f, sim.Velocity.X, 3);
        }

        // ---- turning while moving -------------------------------------------

        [Fact]
        public void TurningWhileRunningActuallyTurnsTheBody()
        {
            // The Lock-mode right-drag bug: assigning the body rotation alone works while stopped
            // (mode 1 rebuilds it from the cached forward) but is discarded while moving (mode 1
            // rebuilds it from the velocity). SetRelRot is stock's answer -- 1000d11d.
            CharVehicleSim sim = FacingPositiveX();
            Drive(sim, 1f);
            for (int i = 0; i < 60; i++)
                sim.Run(1f / 60f);
            Assert.True(sim.Speed > 5f);

            // yaw to face +Z, the way the camera's right-drag does
            sim.SetRelRot(Quat.FromAxisAngle(Vec3.ReferenceUp, 0f));

            float z = sim.Position.Z;
            for (int i = 0; i < 30; i++)
                sim.Run(1f / 60f);

            Vec3 facing = sim.BodyRotation * Vec3.ReferenceForward;
            Assert.Equal(1f, facing.Z, 2);
            Assert.True(sim.Position.Z > z + 1f,
                $"after turning to +Z the body only reached z={sim.Position.Z} from {z}");
        }

        [Fact]
        public void TurningWhileStandingStillAlsoTurnsTheBody()
        {
            // The half that already worked, kept as a guard so a fix to the moving case cannot break
            // the stopped one.
            CharVehicleSim sim = FacingPositiveX();

            sim.SetRelRot(Quat.FromAxisAngle(Vec3.ReferenceUp, 0f));
            for (int i = 0; i < 10; i++)
                sim.Run(1f / 60f);

            Vec3 facing = sim.BodyRotation * Vec3.ReferenceForward;
            Assert.Equal(1f, facing.Z, 2);
        }

        [Fact]
        public void TurningWhileBackpedallingKeepsTravellingBackwards()
        {
            CharVehicleSim sim = FacingPositiveX();
            Drive(sim, -1f);
            for (int i = 0; i < 60; i++)
                sim.Run(1f / 60f);

            sim.SetRelRot(Quat.FromAxisAngle(Vec3.ReferenceUp, 0f));   // face +Z

            float z = sim.Position.Z;
            for (int i = 0; i < 30; i++)
                sim.Run(1f / 60f);

            // still facing +Z, still going the other way
            Vec3 facing = sim.BodyRotation * Vec3.ReferenceForward;
            Assert.Equal(1f, facing.Z, 2);
            Assert.True(sim.Position.Z < z - 1f,
                $"backpedalling towards +Z facing should move to -Z; z went {z} -> {sim.Position.Z}");
        }
    }
}
