using LostEden.Vehicles;
using LostEden.Vehicles.Surfaces;
using Xunit;
using Path = LostEden.Vehicles.Path;   // System.IO.Path is in scope via ImplicitUsings

namespace LostEden.Vehicle.Tests
{
    /// <summary>
    /// <c>NPCVehicle_t</c> (<c>Gamecode 10161b2c</c>) — see Docs/Movement.md §3.2. Its lateral and turn
    /// channels are stubs and its one channel follows a <c>Path_t</c> through a <c>PathGuide_t</c>.
    /// </summary>
    public class NpcVehicleSimTests
    {
        sealed class Flat : ITileHeightSource
        {
            public int Width => 512;
            public int Height => 512;
            public float TileSize => 1f;
            public float SampleHeight(int x, int z) => 0f;
        }

        static NpcVehicleSim Npc(Vec3 at)
        {
            var sim = new NpcVehicleSim
            {
                Mass = 50f, MaxForce = 10f, MaxVel = 1f, NearProbeOffset = 0.5f,
                SlowingDistance = 1.5f, MovementState = 3, RunSpeedStat = 275f,
                Surface = new TilemapSurface(new Flat()), Position = at,
            };
            sim.EnableFalling();
            sim.DisableSurfaceHug();
            sim.UseSurfaceNormal();
            sim.UpdateMotionConstraints();
            return sim;
        }

        // ---- Path_t ----------------------------------------------------------

        [Fact]
        public void AddWaypointDiscardsTheY()
        {
            // 10005c18 pushes a literal zero between the x and z loads.
            var path = new Path();
            path.AddWaypoint(new Vec3(10f, 77f, 20f));

            Assert.Equal(0f, path.GetWaypoint(0).Y, 5);
        }

        [Fact]
        public void ARepeatedWaypointIsDropped()
        {
            var path = new Path();
            path.AddWaypoint(new Vec3(0f, 0f, 0f));
            path.AddWaypoint(new Vec3(0f, 5f, 0f));     // same XZ, Y is discarded anyway

            Assert.Equal(1, path.Size);
        }

        [Fact]
        public void SegmentLengthsAndTotalAreAccumulated()
        {
            var path = new Path();
            path.AddWaypoint(new Vec3(0f, 0f, 0f));
            path.AddWaypoint(new Vec3(3f, 0f, 0f));
            path.AddWaypoint(new Vec3(3f, 0f, 4f));

            Assert.Equal(3, path.Size);
            Assert.Equal(3f, path.GetSegLen(0), 4);
            Assert.Equal(4f, path.GetSegLen(1), 4);
            Assert.Equal(7f, path.TotalLength, 4);
            Assert.Equal(1f, path.GetDir(0).X, 4);
            Assert.Equal(1f, path.GetDir(1).Z, 4);
        }

        [Fact]
        public void DistanceMapsOntoTheRightSegment()
        {
            var path = new Path();
            path.AddWaypoint(new Vec3(0f, 0f, 0f));
            path.AddWaypoint(new Vec3(10f, 0f, 0f));
            path.AddWaypoint(new Vec3(10f, 0f, 10f));

            Assert.Equal(0f, path.MapPathDistanceToPoint(0f).X, 3);
            Assert.Equal(4f, path.MapPathDistanceToPoint(4f).X, 3);

            Vec3 p = path.MapPathDistanceToPoint(13f);
            Assert.Equal(10f, p.X, 3);
            Assert.Equal(3f, p.Z, 3);
        }

        [Fact]
        public void DistancePastTheEndClampsToTheFinalWaypoint()
        {
            // 10005a77 takes `last - 1` rather than extrapolating.
            var path = new Path();
            path.AddWaypoint(new Vec3(0f, 0f, 0f));
            path.AddWaypoint(new Vec3(10f, 0f, 0f));

            Vec3 p = path.MapPathDistanceToPoint(1000f);
            Assert.Equal(10f, p.X, 3);
            Assert.Equal(0f, p.Z, 3);
        }

        // ---- PathGuide_t -----------------------------------------------------

        [Fact]
        public void AFreshGuideReportsTheOriginUntilItIsUpdated()
        {
            // 100069d9 stores the arguments and does NOT map -- only RestartGuide and the updates do.
            var path = new Path();
            path.AddWaypoint(new Vec3(5f, 0f, 0f));
            path.AddWaypoint(new Vec3(15f, 0f, 0f));

            var guide = new PathGuide(path, 2f, 1f);
            Assert.Equal(0f, guide.GuidePos.X, 4);

            guide.RestartGuide(path, 2f, 1f);
            Assert.Equal(7f, guide.GuidePos.X, 3);      // 5 + 2*1
        }

        [Fact]
        public void TheGuideSlidesAlongThePathAtMaxSpeed()
        {
            var path = new Path();
            path.AddWaypoint(new Vec3(0f, 0f, 0f));
            path.AddWaypoint(new Vec3(100f, 0f, 0f));

            var guide = new PathGuide();
            guide.RestartGuide(path, 6f, 0f);
            Assert.Equal(0f, guide.GuidePos.X, 3);

            for (int i = 0; i < 60; i++)
                guide.UpdateAddTime(1f / 60f);

            Assert.Equal(6f, guide.GuidePos.X, 2);      // one second at 6 m/s
        }

        [Fact]
        public void TheGuideOnAnEmptyPathDoesNotThrow()
        {
            // Stock's UpdateAddTime does not guard the path at all (10006a16); the port does.
            var guide = new PathGuide();
            guide.UpdateAddTime(0.5f);
            Assert.Equal(0f, guide.GuidePos.X, 4);
        }

        // ---- the channels ----------------------------------------------------

        [Fact]
        public void AnNpcHasNoStrafeAndNoTurn()
        {
            // Both slots are `xor eax,eax; ret 4`. Setting the player's axes must do nothing.
            NpcVehicleSim sim = Npc(new Vec3(100f, 0.01f, 100f));
            sim.SetStrafe(1f);
            sim.SetTurnRate(3f);

            Vec3 before = sim.Position;
            Quat facing = sim.BodyRotation;
            for (int i = 0; i < 120; i++)
                sim.Run(1f / 60f);

            Assert.Equal(before.X, sim.Position.X, 3);
            Assert.Equal(before.Z, sim.Position.Z, 3);
            Assert.Equal(facing.Y, sim.BodyRotation.Y, 4);
        }

        [Fact]
        public void AnNpcWithNoPathStandsStill()
        {
            NpcVehicleSim sim = Npc(new Vec3(100f, 0.01f, 100f));
            Vec3 before = sim.Position;

            for (int i = 0; i < 120; i++)
                sim.Run(1f / 60f);

            Assert.Equal(before.X, sim.Position.X, 3);
            Assert.Equal(before.Z, sim.Position.Z, 3);
        }

        [Fact]
        public void AnNpcWalksItsPath()
        {
            NpcVehicleSim sim = Npc(new Vec3(100f, 0.01f, 100f));
            sim.Path.AddWaypoint(new Vec3(100f, 0f, 100f));
            sim.Path.AddWaypoint(new Vec3(130f, 0f, 100f));
            sim.RestartPath();

            for (int i = 0; i < 300; i++)
            {
                sim.AdvanceGuide(1f / 60f);
                sim.Run(1f / 60f);
            }

            Assert.True(sim.Position.X > 115f, $"only reached x={sim.Position.X}");
            Assert.Equal(100f, sim.Position.Z, 1);      // it must not drift off the line
        }

        [Fact]
        public void AnNpcTurnsACornerOnItsPath()
        {
            NpcVehicleSim sim = Npc(new Vec3(100f, 0.01f, 100f));
            sim.Path.AddWaypoint(new Vec3(100f, 0f, 100f));
            sim.Path.AddWaypoint(new Vec3(120f, 0f, 100f));
            sim.Path.AddWaypoint(new Vec3(120f, 0f, 130f));
            sim.RestartPath();

            for (int i = 0; i < 600; i++)
            {
                sim.AdvanceGuide(1f / 60f);
                sim.Run(1f / 60f);
            }

            // Round the corner and head up +Z.
            Assert.True(sim.Position.Z > 110f, $"did not turn the corner, z={sim.Position.Z}");
            Assert.InRange(sim.Position.X, 115f, 125f);
        }

        [Fact]
        public void AnNpcArrivesAtAFollowTargetAndIgnoresThePath()
        {
            NpcVehicleSim sim = Npc(new Vec3(100f, 0.01f, 100f));
            sim.Path.AddWaypoint(new Vec3(100f, 0f, 100f));
            sim.Path.AddWaypoint(new Vec3(60f, 0f, 100f));      // the path goes -X
            sim.RestartPath();

            sim.HasFollowTarget = true;
            sim.FollowTarget = new Vec3(120f, 0f, 100f);         // the target is +X

            for (int i = 0; i < 300; i++)
            {
                sim.AdvanceGuide(1f / 60f);
                sim.Run(1f / 60f);
            }

            Assert.True(sim.Position.X > 110f, $"followed the path instead of the target, x={sim.Position.X}");
        }

        [Fact]
        public void MovementStateOneHaltsAnNpc()
        {
            NpcVehicleSim sim = Npc(new Vec3(100f, 0.01f, 100f));
            sim.Path.AddWaypoint(new Vec3(100f, 0f, 100f));
            sim.Path.AddWaypoint(new Vec3(130f, 0f, 100f));
            sim.RestartPath();

            for (int i = 0; i < 60; i++)
            {
                sim.AdvanceGuide(1f / 60f);
                sim.Run(1f / 60f);
            }
            Assert.True(sim.Speed > 0f);

            sim.MovementState = 1;
            for (int i = 0; i < 120; i++)
            {
                sim.AdvanceGuide(1f / 60f);
                sim.Run(1f / 60f);
            }

            Assert.Equal(0f, sim.Speed, 3);
        }

        // ---- SteeringDirArrive ----------------------------------------------

        [Fact]
        public void DirArriveHaltsOnceTheTargetIsBehind()
        {
            // The XZ dot against the previous position goes negative the moment the body passes the
            // target (1000acd2), which is what stops an NPC circling a waypoint.
            NpcVehicleSim sim = Npc(new Vec3(100f, 0.01f, 100f));
            sim.Path.AddWaypoint(new Vec3(100f, 0f, 100f));
            sim.Path.AddWaypoint(new Vec3(112f, 0f, 100f));
            sim.RestartPath();

            for (int i = 0; i < 600; i++)
            {
                sim.AdvanceGuide(1f / 60f);
                sim.Run(1f / 60f);
            }

            // It should settle near the end of the path, not run away past it.
            Assert.InRange(sim.Position.X, 110f, 114f);
            Assert.Equal(0f, sim.Speed, 2);
        }
    }
}
