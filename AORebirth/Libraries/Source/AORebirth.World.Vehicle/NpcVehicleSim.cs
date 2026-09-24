namespace LostEden.Vehicles
{
    /// <summary>
    /// <c>NPCVehicle_t</c> (<c>Gamecode.dll</c>, vftable <c>10161b2c</c>) — the sibling of
    /// <c>PlayerVehicle_t</c>. See Docs/Movement.md §3.2.
    ///
    /// <para>
    /// It is <b>not</b> a variant of the player vehicle. Two of the three steering channels are
    /// literally <c>xor eax,eax; ret 4</c>: an NPC has <b>no strafe and no turn</b>. Its one channel
    /// follows a <see cref="Vehicles.Path"/> through a <see cref="PathGuide"/>, or arrives at a follow
    /// target. So the four-float input model in §4 belongs to the player alone — stock even reuses the
    /// same offsets for different members (<c>+0x360</c> is <c>forwardDrive</c> on a player and an
    /// embedded <c>Path_t</c> on an NPC, with a <c>PathGuide_t</c> at <c>+0x38c</c>).
    /// </para>
    ///
    /// <para>
    /// It still inherits everything in <see cref="CharVehicleSim"/> and <see cref="VehicleSim"/> —
    /// gravity, the ground clamp, statel collision, the orientation update, the speed curve — because
    /// all of that lives in the shared <c>CharVehicle_t</c>/<c>Vehicle_t</c> base.
    /// </para>
    /// </summary>
    public class NpcVehicleSim : CharVehicleSim
    {
        /// <summary>
        /// <c>+0x360</c>, where a player keeps <c>forwardDrive</c>. Empty means "stand still" — the
        /// longitudinal channel halts rather than returning <see cref="SteeringResult.None"/>.
        /// </summary>
        public Path Path { get; } = new Path();

        /// <summary><c>+0x38c</c>, the point that slides along <see cref="Path"/>.</summary>
        public PathGuide Guide { get; } = new PathGuide();

        /// <summary>
        /// <c>FUN_1006f4cb</c>, the follow-target test. When set, the NPC arrives at
        /// <see cref="FollowTarget"/> and the path is ignored entirely (<c>10070efe</c>).
        /// </summary>
        public bool HasFollowTarget { get; set; }

        /// <summary>
        /// What <c>FUN_10070776(0)</c> resolves to — the point a following NPC steers at.
        /// </summary>
        public Vec3 FollowTarget { get; set; }

        /// <summary>
        /// Puts the guide back at the start of the current path at the body's top speed. Stock's
        /// <c>RestartGuide</c> is called from the AI rather than from the vehicle, so this is the seam
        /// the caller drives.
        /// </summary>
        public void RestartPath()
        {
            Guide.RestartGuide(Path, MaxVel, 0f);
        }

        /// <summary>
        /// Advances the guide. Stock drives this from the AI tick, not from <c>Run</c>, so it is left to
        /// the caller — calling it per frame with the frame's <c>dt</c> is the straightforward reading.
        /// </summary>
        public void AdvanceGuide(float dt)
        {
            Guide.UpdateMaxSpeed(MaxVel);
            Guide.UpdateAddTime(dt);
        }

        /// <summary>
        /// <c>NPCVehicle_t</c> slot 19 (<c>10070ed6</c>) — read in full.
        ///
        /// <para>
        /// The target's <b>Y is replaced by the body's own</b> (<c>10070f77</c>:
        /// <c>fld [ebx+0x5c]; fstp [eax+4]</c>), which is what keeps an NPC from steering up or down at
        /// a waypoint. Paths are flat anyway — <see cref="Vehicles.Path.AddWaypoint"/> discards Y.
        /// </para>
        /// </summary>
        protected override SteeringResult CalcSteering(out Vec3 force)
        {
            force = Vec3.Zero;

            // 10070eeb: only state 1 is special-cased, unlike the player's 9/8/1 refusal.
            if (MovementState == 1)
                return SteeringHalt(out force);

            if (HasFollowTarget)                                     // 10070efe
                return SteeringDirArrive(FollowTarget, out force);

            if (Path.Empty)                                          // 10070f2d -> halt, not None
                return SteeringHalt(out force);

            Vec3 target;
            if (Path.Size > 1)
                target = Guide.GuidePos;                             // 10070f40
            else if (Path.Size == 1)
                target = Path.GetWaypoint(0);                        // 10070f5f
            else
                return SteeringHalt(out force);                      // 10070f5d

            target.Y = Position.Y;                                   // 10070f77
            return SteeringDirArrive(target, out force);
        }

        /// <summary>
        /// <c>NPCVehicle_t</c> slot 20 (<c>10070f9b</c>) — <c>xor eax,eax; ret 4</c>. NPCs do not
        /// strafe.
        /// </summary>
        protected override SteeringResult CalcLateralSteering(out Vec3 lateral)
        {
            lateral = Vec3.Zero;
            return SteeringResult.None;
        }

        /// <summary>
        /// <c>NPCVehicle_t</c> slot 21 (<c>10070fa0</c>) — <c>xor eax,eax; ret 4</c>. NPCs do not turn
        /// on the spot; their facing comes from the orientation update following the velocity.
        /// </summary>
        protected override SteeringResult CalcTurnSteering(out Vec3 turn)
        {
            turn = Vec3.Zero;
            return SteeringResult.None;
        }
    }
}
