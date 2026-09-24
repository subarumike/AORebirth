using System;
using LostEden.Vehicles.Surfaces;

namespace LostEden.Vehicles
{
    /// <summary>
    /// Stock <c>CharVehicle_t</c> (<c>Gamecode.dll</c>, vftable <c>101619b4</c>, 45 slots) and the
    /// parts of <c>PlayerVehicle_t</c> (<c>10161bfc</c>) that drive it. The camera's
    /// <c>CameraVehicle_t</c> is its sibling under <c>DummyVehicle_t</c>, so both sit on the same
    /// <see cref="VehicleSim"/> integrator. See <c>Docs/Movement.md</c> §4.
    ///
    /// <para>
    /// <b>Movement input is four float axes, not a flag bitmask.</b> That is the single biggest
    /// correction to the previous developer's model, which is being deleted rather than ported.
    /// </para>
    /// </summary>
    public class CharVehicleSim : VehicleSim
    {
        // ---- the four input axes -------------------------------------------

        /// <summary>
        /// +0x360, written by <c>100717ef</c>. Positive drives forward, negative reverses, zero
        /// means no longitudinal steering at all. It is a <b>gate</b>, not a speed — the speed comes
        /// from <see cref="VehicleSim.MaxVel"/>.
        /// </summary>
        public float ForwardDrive;

        /// <summary>
        /// +0x364, written by <c>1007180f</c>. Unlike the others this <b>is</b> a speed in m/s:
        /// <c>sign(requested) * </c><see cref="StrafeSpeed"/>. Use <see cref="SetStrafe"/>.
        /// </summary>
        public float Strafe;

        /// <summary>+0x368, written by <c>100717ff</c>. Radians per second about world Y.</summary>
        public float TurnRate;

        /// <summary>+0x36c, written by <c>10071840</c>. Along <b>world</b> up, not body up.</summary>
        public float Vertical;

        // ---- the state the speed model keys off -----------------------------

        /// <summary>
        /// <c>FUN_10070a2f</c>. States <b>1, 8 and 9 refuse longitudinal steering</b> outright
        /// (<c>10071537</c>), and 2, 3, 4, 5, 7 each have their own speed curve. 7 is Fly.
        ///
        /// <para>
        /// The names behind the other numbers are not recovered, so this is deliberately an
        /// <c>int</c> rather than an enum — see <c>Docs/Movement.md</c> §9.
        /// </para>
        /// </summary>
        public int MovementState = 3;

        /// <summary>
        /// <c>FUN_10070a37</c>. Selects the forward or backward curve within state 3: <b>2 means
        /// reverse</b>, anything else forward. Distinct from <see cref="VehicleSim.Direction"/>,
        /// which is <c>Vehicle_t</c>'s +0x90 facing flip.
        /// </summary>
        public int CurveDirection = 1;

        /// <summary>The run-speed stat, <c>FUN_1006f2fc</c>.</summary>
        public float RunSpeedStat;

        /// <summary>+0x170, the speed curve's base, stored before the stat is applied.</summary>
        public float SpeedBase { get; private set; }

        // ---- the setters ----------------------------------------------------

        /// <summary><c>100717ef</c>.</summary>
        public void SetForwardDrive(float value) => ForwardDrive = value;

        /// <summary><c>100717ff</c>.</summary>
        public void SetTurnRate(float radiansPerSecond) => TurnRate = radiansPerSecond;

        /// <summary><c>10071840</c>.</summary>
        public void SetVertical(float value) => Vertical = value;

        /// <summary>
        /// <c>1007180f</c> — <c>+0x364 = sign(requested) * StrafeSpeed(state)</c>.
        ///
        /// <para>
        /// Stock takes the sign through <c>FUN_100718f8</c>, which returns +1, 0 or -1, and the
        /// magnitude through vftable slot 34 (<c>1006fddd</c>). So the caller's magnitude is
        /// discarded — only its sign matters.
        /// </para>
        /// </summary>
        public void SetStrafe(float requested)
            => Strafe = Sign(requested) * StrafeSpeed(MovementState);

        /// <summary><c>FUN_100718f8</c> — +1, 0 or -1.</summary>
        internal static float Sign(float v) => v > 0f ? 1f : (v < 0f ? -1f : 0f);

        // ---- the speed model, 1006f9eb --------------------------------------

        /// <summary>
        /// The per-state speed curve. Returns the max velocity for
        /// <see cref="UpdateMotionConstraints"/> and, halved, the strafe speed.
        /// </summary>
        struct Curve
        {
            public float Divisor;
            public float Base;
            public float Max;
            public float Min;
            public bool Constant;
        }

        static Curve CurveFor(int state, int direction)
        {
            switch (state)
            {
                case 3:
                    return direction == 2
                        ? new Curve { Divisor = 275f / 0.7f, Base = 3f, Max = 9.099999f, Min = 1.05f }
                        : new Curve { Divisor = 275f, Base = 5f, Max = 13f, Min = 1.5f };
                case 4:
                    return new Curve { Divisor = 275f / 0.625f, Base = 3f, Max = 8f, Min = 1.5f };
                case 7:
                    return new Curve { Divisor = 275f, Base = 7f, Max = 15f, Min = 1.5f };
                case 5:
                    return new Curve { Base = 1f, Constant = true };
                default:
                    // state 2 and everything unlisted
                    return new Curve { Base = 1.5f, Constant = true };
            }
        }

        /// <summary>
        /// <c>CharVehicle_t</c> vftable slot 34 (<c>1006fddd</c>) — the strafe speed.
        ///
        /// <para>
        /// It is the forward curve <b>scaled by 0.5</b> (the double at <c>10156f00</c>) with a floor
        /// of 0.75 and no separate maximum: <c>clamp(0.5*stat/divisor + 0.5*base, 0.75, 0.5*max)</c>.
        /// For the run state that is <c>clamp(stat*0.5/275 + 2.5, 0.75, 6.5)</c>.
        /// </para>
        /// </summary>
        public float StrafeSpeed(int state)
        {
            const float Scale = 0.5f;
            const float Floor = 0.75f;

            if (state == 2)
                return Math.Max(Floor, 1.5f);

            Curve curve = CurveFor(state, CurveDirection);
            if (curve.Constant)
                return Math.Max(Floor, curve.Base);

            float v = Scale * RunSpeedStat / curve.Divisor + Scale * curve.Base;
            float max = Scale * curve.Max;
            if (v > max)
                v = max;
            if (v < Floor)
                v = Floor;
            return v;
        }

        /// <summary>
        /// <c>FUN_1006f9eb</c> (149 lines) — called from <b>27 sites</b>, i.e. on every movement
        /// state change. Writes mass, max velocity, max force and brake distance.
        ///
        /// <para>
        /// The brake distance reduces exactly to <c>maxVel / 4</c> in every branch:
        /// <c>(v*v*m) / (2*v*m) * 0.5</c>. Stock computes it the long way; this keeps the long form
        /// so the derivation stays visible next to the addresses.
        /// </para>
        /// </summary>
        public void UpdateMotionConstraints()
        {
            float mass = Mass;
            if (mass == 0f)
                mass = 10f;
            Mass = mass;

            Curve curve = CurveFor(MovementState, CurveDirection);
            SpeedBase = curve.Base;

            float v;
            if (curve.Constant)
            {
                v = curve.Base;
                if (v < 0.01f)
                    v = 0.1f;          // the floor inside 1006f7fe
            }
            else
            {
                v = RunSpeedStat / curve.Divisor + curve.Base;
                if (v > curve.Max)
                    v = curve.Max;
                if (v < curve.Min)
                    v = curve.Min;
            }

            if (MovementState == 7)
                DisableFalling();      // Fly turns gravity off (1006fd?? -> 10155384)

            float force = 2f * v * mass;
            if (force > 100000f)
                force = 100000f;

            float brake = (v * v * mass) / force * 0.5f;

            if (force > 10000f)
                force = 10000f;        // the effective cap is 10000, applied AFTER the brake maths

            MaxVel = v;
            MaxForce = force;
            SlowingDistance = brake;
        }

        // ---- the three steering channels ------------------------------------

        /// <summary>
        /// <c>PlayerVehicle_t</c> slot 19 (<c>10071537</c>, 55 lines) — the longitudinal channel.
        ///
        /// <para>
        /// The follow-target branch (<c>SteeringDirArrive</c> toward a looked-up point) is not
        /// ported: it needs the targeting surface on <c>n3Camera_t</c>, which is a separate system.
        /// </para>
        /// </summary>
        protected override SteeringResult CalcSteering(out Vec3 force)
        {
            force = Vec3.Zero;

            if (MovementState == 9 || MovementState == 8 || MovementState == 1)
                return SteeringResult.None;

            if (ForwardDrive == 0f)
                return SteeringResult.None;

            return ForwardDrive > 0f
                ? SteeringForward(out force)
                : SteeringReverse(out force);
        }

        /// <summary>
        /// <c>PlayerVehicle_t</c> slot 20 (<c>100716d5</c>) — taken from disassembly, because the
        /// decompiler lost the operand order.
        ///
        /// <para>
        /// <c>lateral = bodyRight * strafe + worldUp * vertical</c>. The up vector is built inline
        /// as <c>(0,1,0)</c> at <c>10071740</c>, so it is world up and not affected by the body's
        /// orientation.
        /// </para>
        /// </summary>
        protected override SteeringResult CalcLateralSteering(out Vec3 lateral)
        {
            lateral = Vec3.Zero;

            if (Strafe == 0f && Vertical == 0f)
                return SteeringResult.None;

            lateral = CalcBodyRight() * Strafe + Vec3.ReferenceUp * Vertical;
            return SteeringResult.Lateral;
        }

        /// <summary>
        /// <c>PlayerVehicle_t</c> slot 21 (<c>10071795</c>) — an axis of <c>(0, turnRate, 0)</c>.
        ///
        /// <para>
        /// The integrator reads a <see cref="SteeringResult.Turn"/> result as an axis whose
        /// <b>length is the angular rate</b>, and — the part that gives AO its feel — rotates the
        /// <b>body</b> when standing still and the <b>velocity</b> when moving
        /// (<c>Docs/Camera.md</c> §4.2 step 7).
        /// </para>
        /// </summary>
        protected override SteeringResult CalcTurnSteering(out Vec3 turn)
        {
            turn = Vec3.Zero;

            if (TurnRate == 0f)
                return SteeringResult.None;

            turn = new Vec3(0f, TurnRate, 0f);
            return SteeringResult.Turn;
        }

        /// <summary><c>Vehicle_t::CalcBodyRight</c> (<c>1000a078</c>) — the body rotation applied to <c>(1,0,0)</c>.</summary>
        public Vec3 CalcBodyRight() => BodyRotation * new Vec3(1f, 0f, 0f);

        // ---- the jump --------------------------------------------------------

        /// <summary>
        /// +0x164, the height of the jump in progress. Zero means none: <see cref="Jump"/> refuses
        /// while it is non-zero (<c>1006f503</c>), and landing clears it (<c>1006f442</c>). The ctor
        /// (<c>1006f5c9</c>) starts it at zero.
        /// </summary>
        public float JumpHeight { get; private set; }

        /// <summary>
        /// The owning character's <c>SimpleChar_t +0x21c</c> — the byte
        /// <c>n3EngineClientAnarchy_t::N3Msg_IsNpc</c> (<c>100166d4</c>) reads. Stock reaches the
        /// character through <c>1006f753</c> (the dynel looked up by the identity at +0x168).
        /// </summary>
        public bool OwnerIsNpc;

        /// <summary>
        /// The owning character's <c>n3VisualDynel_t::GetBodyScale</c> (N3 <c>10019622</c>, its
        /// +0xac). The ceiling clamp in <see cref="Jump"/> subtracts twice this.
        /// </summary>
        public float OwnerBodyScale = 1f;

        /// <summary>Raised from <see cref="OnLanded"/> when a jump in progress ends.</summary>
        public event Action JumpLanded;

        /// <summary>
        /// <c>SimpleChar_t</c>'s jump height (<c>Gamecode 10058994</c>), from Strength (stat 16),
        /// Agility (17) and GmLevel (215): <c>(str + agi) / 200 + 1</c>, at least 0.5. Past 800 in
        /// total a non-GM counts as exactly 800 (<c>str = 800, agi = 0</c>).
        /// </summary>
        public static float JumpHeightFromStats(int strength, int agility, int gmLevel)
        {
            float str = strength;
            float agi = agility;
            if (800f < agi + str && gmLevel == 0)
            {
                str = 800f;
                agi = 0f;
            }

            float height = (float)((agi + str) / 200.0 + 1.0);
            if (height < 0.5f)
                height = 0.5f;
            return height;
        }

        /// <summary>
        /// <c>CharVehicle_t</c> vftable slot 11 (<c>1006ff32</c>) — what
        /// <c>n3Dynel_t::VehicleJump</c> (N3 <c>10004290</c>) and <c>JumpStartTransitionAction_t</c>
        /// (<c>1006dcdb</c>) call with <see cref="JumpHeightFromStats"/>. Returns false when refused
        /// because a jump is already in progress; that return is the port's, stock's is void.
        ///
        /// <para>
        /// In order: refuse unless <see cref="JumpHeight"/> is zero. Probe the surface straight up —
        /// slot 3's <c>GetLineIntersection</c>, which is slot 4 with its normal discarded (N3
        /// <c>10018877</c>) — from the position to the position plus <c>(0, 100, 0)</c>. On a hit the
        /// headroom is <c>hit.y - y - 2 * bodyScale</c>, floored at 0.1, and the height becomes the
        /// smaller of the two. Store it at +0x164; an NPC's stored value (<b>only</b> the stored one)
        /// is raised to 1.5. Launch at <c>sqrt(2 * height * |g|)</c> through <see cref="VehicleSim.Impact"/>
        /// as <c>(0, v * mass, 0)</c>, then <see cref="VehicleSim.EnableFalling"/>.
        /// </para>
        ///
        /// <para>
        /// The gate is the only one: stock does not check <see cref="VehicleSim.Airborne"/> here.
        /// A jump started in the air sets <see cref="JumpHeight"/> but <see cref="VehicleSim.Impact"/>
        /// drops the launch. Stock also stores the take-off height at +0x174; nothing in the port reads
        /// that field, so it is not carried.
        /// </para>
        /// </summary>
        public bool Jump(float height)
        {
            if (JumpHeight != 0f)
                return false;

            ISurface surface = GetSurface();
            if (surface != null
                && surface.GetLineIntersection(
                    Position, Position + new Vec3(0f, 100f, 0f), out Vec3 hit, out _, false, null))
            {
                // x87 keeps hit.y - y unrounded, spills it as a double, and only rounds to float
                // once the body scale is off (1006ffb4..1006ffd1).
                float headroom = (float)(((double)hit.Y - Position.Y) - (OwnerBodyScale + OwnerBodyScale));
                if (headroom < 0.1f)
                    headroom = 0.1f;
                if (headroom <= height)
                    height = headroom;
            }

            JumpHeight = height;
            if (OwnerIsNpc && JumpHeight < 1.5f)
                JumpHeight = 1.5f;

            float speed = MathF.Sqrt((height + height) * MathF.Abs(GravityAccel));
            Impact(new Vec3(0f, speed * Mass, 0f));
            EnableFalling();
            return true;
        }

        /// <summary>
        /// <c>CharVehicle_t</c> vftable slot 27 (<c>1006f442</c>), the <c>+0x6c</c> notification
        /// <see cref="VehicleSim.LandNow"/> sends. Clears <see cref="JumpHeight"/>. Stock first sends
        /// event 0x10 to the character's state machine (+0x178) when that is in state 3; the state
        /// machine is not ported, so <see cref="JumpLanded"/> stands in for it, raised only when a
        /// jump was in progress.
        /// </summary>
        protected override void OnLanded(float height)
        {
            bool wasJumping = JumpHeight != 0f;
            JumpHeight = 0f;
            if (wasJumping)
                JumpLanded?.Invoke();
        }

        // ---- the surface binding --------------------------------------------

        /// <summary>
        /// The world this vehicle collides against. Stands for
        /// <c>DummyVehicle_t</c>'s lazily-resolved <c>+0x154</c> (<c>100011e0</c>); the binding
        /// layer sets it when the playfield's terrain is ready.
        ///
        /// <para>Null leaves <see cref="VehicleSim.EnsureSurfaceAlignment"/> a no-op, as in stock.</para>
        /// </summary>
        public ISurface Surface { get; set; }

        /// <inheritdoc/>
        protected override ISurface GetSurface() => Surface;
    }
}
