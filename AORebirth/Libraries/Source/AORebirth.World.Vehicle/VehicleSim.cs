using System;
using LostEden.Vehicles.Surfaces;

namespace LostEden.Vehicles
{
    /// <summary>
    /// Stock <c>Vehicle_t</c> (<c>Vehicle.dll</c>, exported; ctor <c>1000ce2f</c> ordinal 34,
    /// <c>Run</c> <c>1000e849</c> ordinal 201, physics step <c>FUN_1000e3d3</c>).
    ///
    /// This is the shared motion core: in stock, the camera and every character are the same kind of
    /// object moved by this one integrator, differing only in the steering hooks they override
    /// (<c>CameraVehicle_t</c> and <c>CharVehicle_t</c> are siblings under <c>DummyVehicle_t</c>).
    /// See <c>Docs/Camera.md</c> §3-§4.
    ///
    /// No Unity dependency so the recovered maths can be asserted from plain unit tests; a
    /// <c>MonoBehaviour</c> binds an instance to a transform and supplies surface alignment.
    /// </summary>
    public class VehicleSim
    {
        // ---- stock statics -------------------------------------------------

        /// <summary><c>Vehicle_t::s_vGravityAccel</c> (<c>1001938c</c>).</summary>
        public const float GravityAccel = -20f;

        /// <summary>
        /// Vertical velocity is clamped to this magnitude every step it is integrated
        /// (<c>1000e54a</c>). Note this is a clamp on the gravity accumulator alone, not on speed.
        /// </summary>
        public const float MaxFallSpeed = 50f;

        /// <summary>
        /// A frame longer than this is dropped whole — no integration at all (<c>1000e3e6</c>).
        /// </summary>
        public const float MaxFrameTime = 4f;

        /// <summary>Speed below which the vehicle counts as stopped (<c>1000e5a9</c>).</summary>
        public const float MovingSpeedEpsilon = 0.001f;

        /// <summary>A turn axis shorter than this is ignored (<c>1000e78f</c>).</summary>
        public const float TurnEpsilon = 0.0001f;

        /// <summary>
        /// The step the rest of the engine sees, stock <c>Vehicle_t::s_vDeltaTimeNow</c>
        /// (<c>1001a148</c>, a private static). Set at the top of every sub-step.
        /// Thread-static because each playfield heartbeat steps its vehicles on its own thread.
        /// </summary>
        public static float DeltaTimeNow
        {
            get => s_deltaTimeNow;
            private set => s_deltaTimeNow = value;
        }

        [ThreadStatic]
        static float s_deltaTimeNow;

        // ---- tunables, with the constructor's defaults ----------------------
        // Offsets are Vehicle_t instance offsets; defaults are from the ctor at 1000ce2f, which
        // ends by calling the init helper FUN_1000c45e(mass 50, maxForce 2, maxVel 2, 0.01, 0.1).

        /// <summary>+0x34. <c>SetMass</c> (<c>1000a0a8</c>) floors this at 0.1.</summary>
        public float Mass
        {
            get => _mass;
            set => _mass = value <= 0f ? 0.1f : value;
        }
        float _mass = 50f;

        /// <summary>+0x38. The steering force is truncated to this before integration.</summary>
        public float MaxForce = 2f;

        /// <summary>+0x3c. Velocity is truncated to this after integration.</summary>
        public float MaxVel = 2f;

        /// <summary>
        /// +0x40, written by <c>SetBrakeDistance</c> (<c>1000a166</c>). The distance over which
        /// <see cref="SteeringArrive"/> scales its desired speed down — the arrive damping.
        /// </summary>
        public float SlowingDistance = 0.1f;

        /// <summary>
        /// +0x48. The radius inside which <see cref="SteeringArrive"/> stops outright, when the
        /// caller does not pass one.
        /// </summary>
        public float HaltRadius;

        /// <summary>
        /// +0x4c, written by <c>SetRadius</c> (<c>1000a159</c>). The body half-extent, also used as
        /// the offset the camera's occlusion probe is pushed out by.
        /// <c>EnsureSurfaceAlignment</c> overwrites it from the surface each step (<c>1000d1f7</c>).
        /// </summary>
        public float NearProbeOffset = 0.01f;

        /// <summary>
        /// +0x104, the sub-step ceiling. Per-vehicle: <c>Vehicle_t</c> and so <c>CharVehicle_t</c>
        /// default to 0.4 s (ctor <c>1000ce2f</c>), <c>CameraVehicle_t</c> lowers it to
        /// <see cref="CameraSubStep"/> (ctor <c>1001d54f</c>).
        ///
        /// This bounds frame-rate divergence, it does not remove it — the integrator is plain
        /// semi-implicit Euler, and at 30 fps and above the 0.4 s cap never binds at all. See
        /// Docs/Camera.md §4.1 for the measured spread.
        /// </summary>
        public float MaxSubStep = 0.4f;

        /// <summary><c>CameraVehicle_t</c>'s tighter cap — at least 20 Hz (<c>1001d54f</c>).</summary>
        public const float CameraSubStep = 0.05f;

        // ---- EnsureSurfaceAlignment constants, all read from the image ------

        /// <summary>The vertical lift applied before probing, <c>100127a0</c>.</summary>
        public const float ProbeLift = 0.4f;

        /// <summary>The tripod's radius, <c>100127cc</c>.</summary>
        public const float ProbeRadius = 0.04f;

        /// <summary>The veto retry count, <c>1000d23e</c>.</summary>
        public const int VetoRetries = 9;

        /// <summary>The body half-extent floor, <c>100127d0</c> / <c>100127e0</c>.</summary>
        public const float BodyHalfExtent = 0.48f;

        /// <summary>
        /// Scales the movement length into the probe half-extent, <c>100127d8</c>. This is
        /// <b>not</b> a slope limit, despite <c>2/sqrt(3)</c> looking like one — see
        /// Docs/Movement.md §7.1.
        /// </summary>
        public const float HalfExtentPerMetre = 1.154700517654419f;

        /// <summary>
        /// The slope gate: a surface whose normal Y is below this is not walkable
        /// (<c>1000d?</c>, §6 step 6). Relaxed to <see cref="RelaxedSlopeNormalY"/> when
        /// <see cref="RelaxSlopeGate"/> is set.
        /// </summary>
        public const float SlopeNormalY = 0.5f;

        /// <summary>The relaxed gate when <see cref="RelaxSlopeGate"/> is set.</summary>
        public const float RelaxedSlopeNormalY = 0.001f;

        /// <summary>Step height for an ordinary body, <c>100124e0</c>.</summary>
        public const float StepHeight = 0.01f;

        /// <summary>Step height for collision profile 4, the camera's, <c>100127e8</c>.</summary>
        public const float CameraStepHeight = 0.25f;

        /// <summary>The swept solver's half width and iteration count (<c>1000d3f0</c>, <c>1000d3f9</c>).</summary>
        public const float SweptHalfWidth = 0.4f;

        /// <summary>See <see cref="SweptHalfWidth"/>.</summary>
        public const int SweptIterations = 10;

        /// <summary>
        /// The sweep's lookahead, <c>10012790</c>. Re-seated from the body position on every free
        /// move and every slide, so it is a constant reach and not a budget.
        /// </summary>
        public const float LookAhead = 10f;

        /// <summary>
        /// The sentinel a missed probe contributes to the nearest-hit comparison, <c>10012778</c>.
        /// It is a squared distance, and 10000 is safely past the 100 a 10 m ray can reach.
        /// </summary>
        public const float NoHitDistance = 10000f;

        /// <summary>
        /// The tripod directions, function-level statics in stock behind a one-time init mask at
        /// <c>1001a1c0</c>, built from <c>(1,0,0)</c>, <c>(-1,0,1)</c> and <c>(-1,0,-1)</c> and
        /// normalised (<c>1001a1b4</c>, <c>1001a1a8</c>, <c>1001a19c</c>).
        /// </summary>
        static readonly Vec3[] ProbeDirections =
        {
            new Vec3(1f, 0f, 0f),
            new Vec3(-0.70710678f, 0f, 0.70710678f),
            new Vec3(-0.70710678f, 0f, -0.70710678f),
        };

        /// <summary>
        /// The SECOND tripod's directions, statics at <c>1001a190</c>, <c>1001a184</c> and
        /// <c>1001a178</c>, built from <c>(-1,0,0)</c>, <c>(1,0,1)</c> and <c>(1,0,-1)</c> and
        /// normalised. Only reached in orientation mode 1 (<c>1000d99a</c>).
        /// </summary>
        static readonly Vec3[] SecondProbeDirections =
        {
            new Vec3(-1f, 0f, 0f),
            new Vec3(0.70710678f, 0f, 0.70710678f),
            new Vec3(0.70710678f, 0f, -0.70710678f),
        };

        /// <summary>The second tripod's near radius, <c>10012298</c>.</summary>
        public const float SecondProbeRadius = 0.2f;

        /// <summary>The second tripod's far radius, <c>100127c8</c>.</summary>
        public const float SecondProbeReach = 0.8f;

        /// <summary>The per-call weight on the new normal in the smoothing, <c>1000d577</c>.</summary>
        public const float NormalSmoothing = 0.1f;

        /// <summary>+0x12c..+0x134, the smoothed surface normal accumulator.</summary>
        public Vec3 SmoothedSurfaceNormal = Vec3.ReferenceUp;

        // ---- EnsureSurfaceAlignment state ----------------------------------

        /// <summary>+0xa0..+0xa8, written by the ground clamp and read by the orientation update.</summary>
        public Vec3 SurfaceNormal = Vec3.ReferenceUp;

        /// <summary>
        /// +0xfc, the collision profile. Only <c>CameraVehicle_t</c>'s ctor writes 4
        /// (<c>1001d555</c>); Gamecode never writes the field, so characters run on 0.
        /// </summary>
        public int CollisionProfile;

        /// <summary>+0x13c. When set, the slope gate relaxes to <see cref="RelaxedSlopeNormalY"/>.</summary>
        public bool RelaxSlopeGate;

        /// <summary>
        /// +0xb0, written by <c>SetOrientationMode</c> (<c>1000a18a</c>). Decides how the body is
        /// turned by the orientation update (<c>FUN_1000c616</c>):
        /// <list type="table">
        ///   <item><term>0</term><description>heading only, the body stays level</description></item>
        ///   <item><term>1</term><description><b>aligned to the surface normal</b> —
        ///     <c>DummyVehicle_t::UseSurfaceNormal</c> (<c>N3 100011b7</c>), which Gamecode calls for
        ///     character vehicles at <c>1006eb9d</c>, <c>1006ec56</c> and
        ///     <c>1006ee00</c></description></item>
        ///   <item><term>2</term><description>unported</description></item>
        ///   <item><term>3</term><description>the camera's, with the vetoes
        ///     (<c>Docs/Camera.md</c> §5.7)</description></item>
        /// </list>
        /// </summary>
        public int OrientationMode;

        /// <summary>+0xb4..+0xbc, the up vector the orientation update last used.</summary>
        public Vec3 OrientationUp = Vec3.ReferenceUp;

        /// <summary><c>DummyVehicle_t::UseSurfaceNormal</c> (<c>100011b7</c>) = <c>SetOrientationMode(1)</c>.</summary>
        public void UseSurfaceNormal() => OrientationMode = 1;

        // ---- state ---------------------------------------------------------

        /// <summary>+0x58..+0x60.</summary>
        public Vec3 Position;

        /// <summary>+0x64..+0x6c.</summary>
        public Vec3 Velocity;

        /// <summary>
        /// +0x80..+0x8c, the <b>visible</b> body rotation (<c>GetBodyRot</c>, <c>1000a071</c>).
        ///
        /// <para>
        /// Assigning it refreshes the cached forward <see cref="GetBodyForward"/> returns, because
        /// stock does exactly that at every writer: the standing-still turn (<c>1000e7d3</c> then
        /// <c>1000e7dc</c>), <c>SetRelRot</c> (<c>1000d13a</c> then <c>1000d14c</c>) and the
        /// orientation update (<c>1000c8d2</c> then <c>1000c8da</c>) all call <c>FUN_1000a47a</c>
        /// immediately after. Without it a turn on the spot would be undone on the next frame, since
        /// the orientation update rebuilds the rotation <i>from</i> that cached forward while stopped.
        /// </para>
        /// </summary>
        public Quat BodyRotation
        {
            get => _bodyRotation;
            set
            {
                _bodyRotation = value;
                CacheBodyForward();
            }
        }

        Quat _bodyRotation = Quat.Identity;

        /// <summary>+0x94..+0x9c, the force the longitudinal channel accumulates into.</summary>
        public Vec3 SteerForce;

        /// <summary>+0x54, the gravity accumulator, kept apart from <see cref="Velocity"/>.</summary>
        public float VerticalVelocity;

        /// <summary>+0xcc, <c>|Velocity|</c> as of the last force integration.</summary>
        public float Speed { get; protected set; }

        /// <summary>+0xd0..+0xd8.</summary>
        public Vec3 PreviousPosition { get; protected set; }

        /// <summary>
        /// +0x50. <c>EnableFalling</c> (<c>1000c394</c>) / <c>DisableFalling</c> (<c>1000c3b7</c>).
        /// </summary>
        public bool FallingEnabled;

        /// <summary>
        /// +0x51. <c>EnableSurfaceHug</c> (<c>10009f49</c>: <c>mov byte [ecx+0x51], 1</c>) /
        /// <c>DisableSurfaceHug</c> (<c>10009f4e</c>). When on and the vehicle is not airborne, the
        /// integrator pins vertical motion to zero every step.
        ///
        /// <para>
        /// <b>It is not what keeps a walker on the ground</b> — an earlier version of this comment
        /// said so, and the character vehicle disproves it: the player factory (<c>10057826</c>)
        /// calls <c>DisableSurfaceHug()</c> <i>and</i> <c>EnableFalling()</c>. A character is a
        /// falling body whose ground contact is made entirely by
        /// <see cref="EnsureSurfaceAlignment"/>. See <c>Docs/Movement.md</c> §4.4.
        /// </para>
        /// </summary>
        public bool SurfaceHug = true;

        /// <summary>+0x52. Airborne right now, which is what gates gravity (<c>1000e4b7</c>).</summary>
        public bool Airborne;

        /// <summary><c>Speed &gt; </c><see cref="MovingSpeedEpsilon"/> (<c>1000e5a9</c>).</summary>
        public bool IsMoving => Speed > MovingSpeedEpsilon;

        // ---- virtual hooks, one per stock vftable slot ----------------------

        /// <summary>vftable +0x48. <c>Run</c> returns immediately when this is false.</summary>
        protected virtual bool IsRunEnabled() => true;

        /// <summary>
        /// vftable +0x4c. The longitudinal channel: fills <paramref name="force"/> and returns how
        /// the integrator should read it. Only <see cref="SteeringResult.Force"/> is integrated.
        /// </summary>
        protected virtual SteeringResult CalcSteering(out Vec3 force)
        {
            force = Vec3.Zero;
            return SteeringResult.None;
        }

        /// <summary>
        /// vftable +0x50. The lateral channel: a velocity applied straight to position. Only
        /// <see cref="SteeringResult.Lateral"/> is honoured.
        /// </summary>
        protected virtual SteeringResult CalcLateralSteering(out Vec3 lateral)
        {
            lateral = Vec3.Zero;
            return SteeringResult.None;
        }

        /// <summary>
        /// vftable +0x54. The turn channel: an axis whose length is the angular rate. Only
        /// <see cref="SteeringResult.Turn"/> is honoured.
        /// </summary>
        protected virtual SteeringResult CalcTurnSteering(out Vec3 turn)
        {
            turn = Vec3.Zero;
            return SteeringResult.None;
        }

        /// <summary>
        /// <c>Vehicle_t::EnsureSurfaceAlignment</c> (<c>1000d1aa</c>) — the ground clamp and
        /// collision gate. A false return <b>breaks the sub-step loop</b>, which is how stock stops a
        /// vehicle that has been blocked partway through a frame.
        ///
        /// <para>
        /// Recovered in full — see Docs/Movement.md §6 for the algorithm and §7.1c for the stack
        /// frame it was read from. With no surface bound it returns true immediately, exactly as
        /// stock does, which is why the camera vehicles are unaffected.
        /// </para>
        /// </summary>
        protected virtual bool EnsureSurfaceAlignment(Vec3 previousPosition, bool force)
        {
            ISurface surface = GetSurface();
            if (surface == null)
                return true;

            Vec3 position = Position;

            // `forced` is a FLOAT in stock, not a flag: it multiplies the movement's Y below.
            float forced = (!FallingEnabled || Airborne || force) ? 1f : 0f;

            // (1) the veto retry -- 9 attempts backing off in fixed tenths of the move
            Vec3 tenth = (position - previousPosition) * 0.1f;
            for (int i = VetoRetries; i > 0; i--)
            {
                if (!surface.VetoPosition(ref position, this, previousPosition))
                    break;
                position = previousPosition + tenth * (float)i;
            }

            Vec3 delta = position - previousPosition;
            Vec3 lift = FallingEnabled ? new Vec3(0f, ProbeLift, 0f) : Vec3.Zero;

            // the probe works from the PREVIOUS position raised by 0.4, not from the current one
            Vec3 probe = previousPosition + new Vec3(0f, ProbeLift, 0f);
            float ceilingY = previousPosition.Y + ProbeLift;

            float movementLength = delta.Length;
            Vec3 horizontal = new Vec3(delta.X, 0f, delta.Z);
            float horizontalLength = horizontal.Length;

            if (movementLength > 0f)
            {
                if (force)
                {
                    probe += new Vec3(delta.X, delta.Y * forced, delta.Z);
                }
                else
                {
                    // stock zeroes the movement's Y when `forced` is 0, so the sweep stays flat
                    Vec3 swept = new Vec3(delta.X, delta.Y * forced, delta.Z);
                    Vec3 direction = swept;
                    if (direction.IsZero)
                        direction = new Vec3(0f, -1f, 0f);
                    else
                        direction = direction * (1f / direction.Length);

                    float totalBudget = horizontalLength < 1e-6f ? movementLength : 10000f;
                    if (forced != 0f)
                        totalBudget = movementLength;

                    float horizontalBudget = horizontalLength;
                    float slopeLimit = (FallingEnabled && !RelaxSlopeGate) ? 0.5f : -1f;

                    SweptMove(
                        ref probe, SweptHalfWidth, direction,
                        ref horizontalBudget, ref totalBudget,
                        surface, ref ceilingY,
                        FallingEnabled ? 3 : 1, SweptIterations, slopeLimit);
                }
            }

            ceilingY = force ? probe.Y + 1f : Math.Max(ceilingY, probe.Y);

            // (2) the step clamp -- undo the lift, ask the surface, raise to the step height
            probe.Y -= ProbeLift;
            surface.CalculateClosestPoint(probe, out Vec3 closest, out _, this);

            float stepHeight = CollisionProfile == 4 ? CameraStepHeight : StepHeight;
            if (probe.Y < closest.Y + stepHeight)
                probe.Y = closest.Y + stepHeight;

            // (3) the tripod -- three downward rays around the body
            Vec3 high = new Vec3(probe.X, ceilingY, probe.Z) + lift;
            Vec3 low = new Vec3(probe.X, 0f, probe.Z);

            Vec3 h0 = ProbeOne(surface, high, low, ProbeDirections[0]);
            Vec3 h1 = ProbeOne(surface, high, low, ProbeDirections[1]);
            Vec3 h2 = ProbeOne(surface, high, low, ProbeDirections[2]);

            Vec3 normal = Vec3.Cross(h1 - h0, h2 - h0);
            if (normal.Y < 0f)
                normal = -normal;

            float normalLength = normal.Length;
            if (normalLength > 0f)
                normal = normal * (1f / normalLength);
            else
                normal = Vec3.ReferenceUp;

            // (4) the ground is the HIGHEST of the three tripod hits
            float tripodMax = Math.Max(h0.Y, Math.Max(h1.Y, h2.Y));

            // The body half-extent grows with how far the body moved this step (1000d508-1000d557):
            // 0.48, or |movement| * 1.1547 + 0.48 while falling is on and it is on the ground.
            float halfExtent = BodyHalfExtent;
            if (FallingEnabled && !Airborne && !force)
            {
                halfExtent = movementLength * HalfExtentPerMetre + BodyHalfExtent;
                if (ceilingY < halfExtent)
                    halfExtent = ceilingY;
            }

            // (5) ground contact, the slope gate, and who is airborne.
            //
            // Stock starts from "airborne" and only clears it when the body is at the ground AND
            // the surface is walkable (1000d539-1000d560, read with the fall/land dispatch at
            // 1000d596). The slope gate does NOT block movement here — it leaves the body
            // AIRBORNE, and gravity then pulls it down the slope. That is what sliding off a steep
            // face is. Climbing is refused separately, in the swept solver, which flattens a
            // too-steep normal into a vertical wall (§6.6).
            bool airborneNow = true;

            if (probe.Y - halfExtent <= tripodMax)
            {
                float groundRef = tripodMax;
                if (groundRef < closest.Y)
                    groundRef = closest.Y;

                bool descending = VerticalVelocity < 0.1f;

                if (descending && FallingEnabled)
                    probe.Y = groundRef + StepHeight;

                float gate = RelaxSlopeGate ? RelaxedSlopeNormalY : SlopeNormalY;
                if (normal.Y >= gate && descending)
                    airborneNow = false;
            }

            // A SECOND tripod runs in orientation mode 1 -- the character's -- at radius 0.2 with a
            // 0.8 reach (1000d99a..1000dd34). Its wider baseline gives a much steadier normal than
            // the 0.04 one, which is the whole reason mode 1 asks for it.
            if (OrientationMode == 1)
            {
                Vec3 secondHigh = probe + new Vec3(0f, ProbeLift, 0f);
                Vec3 secondLow = probe - new Vec3(0f, ProbeLift, 0f);

                Vec3 s0 = ProbeOneAt(surface, secondHigh, secondLow, SecondProbeDirections[0]);
                Vec3 s1 = ProbeOneAt(surface, secondHigh, secondLow, SecondProbeDirections[1]);
                Vec3 s2 = ProbeOneAt(surface, secondHigh, secondLow, SecondProbeDirections[2]);

                Vec3 second = Vec3.Cross(s1 - s0, s2 - s0);
                if (second.Y < 0f)
                    second = -second;

                float secondLength = second.Length;
                normal = secondLength > 0f ? second * (1f / secondLength) : Vec3.ReferenceUp;
            }
            else
            {
                normal = Vec3.ReferenceUp;
            }

            // Stock then SMOOTHS it, per call, 0.9 old to 0.1 new (1000d577..1000d587), and only
            // after that squares up anything steeper than the gate.
            Vec3 blended = SmoothedSurfaceNormal * (1f - NormalSmoothing) + normal * NormalSmoothing;
            float blendedLength = blended.Length;
            SmoothedSurfaceNormal = blendedLength > 0f ? blended * (1f / blendedLength) : Vec3.ReferenceUp;

            normal = SmoothedSurfaceNormal;
            if (normal.Y < SlopeNormalY)
                normal = Vec3.ReferenceUp;
            SurfaceNormal = normal;

            if (FallingEnabled)
            {
                if (airborneNow)
                    BeginFalling();
                else if (Airborne)
                    LandNow(probe.Y);
            }

            surface.VetoPosition(ref probe, this, previousPosition);
            Position = probe;

            // Stock returns whether the position actually CHANGED (FUN_10009b1d at 1000e697 is a
            // three-component inequality test). A false return breaks the sub-stepping loop, which
            // is how a fully blocked move stops the rest of the frame's integration. The orientation
            // update can force it true (1000e710).
            bool moved = !Position.Equals(previousPosition);

            // 1000e710: the orientation update can force the result true (mode 1 returns 1).
            bool reoriented = UpdateOrientation(normal);
            return moved || reoriented;
        }

        /// <summary>
        /// <c>FUN_1000b2e5</c> — the continuous-collision sweep with wall sliding, ported from the
        /// disassembly (<c>1000b2e5</c>..<c>1000c391</c>, 1259 instructions including the four
        /// out-of-line "budget ran out" blocks the compiler moved past the <c>ret</c>).
        /// See Docs/Movement.md §6.6 for the frame map every slot name below comes from.
        ///
        /// <para>
        /// The only caller is <c>EnsureSurfaceAlignment</c> at <c>1000d41d</c>, which passes
        /// <c>mode = 2 * fallingEnabled + 1</c> and
        /// <c>slopeLimit = (fallingEnabled &amp;&amp; !relaxGate) ? 0.5 : -1</c>. Both read the
        /// <b>gravity-enabled</b> flag (<c>+0x50</c>), not "currently in the air" (<c>+0x52</c>) —
        /// so for every character the shoulder probe is on and the slope limit is 0.5. Only the
        /// camera, which disables falling, gets mode 1 and no slope limit.
        /// </para>
        ///
        /// <para>
        /// The caller also zeroes the movement's Y unless the body is airborne or forced, so a
        /// grounded walker sweeps along a <b>perfectly horizontal</b> direction — which is why the
        /// steep-slope test at <c>1000bc7e</c> is <c>!(0 &gt; move.y)</c> and not
        /// <c>move.y &gt; 0</c>: with <c>move.y == 0</c> it must still fire.
        /// </para>
        /// </summary>
        protected void SweptMove(
            ref Vec3 position,
            float halfWidth,
            Vec3 direction,
            ref float horizontalBudget,
            ref float totalBudget,
            ISurface surface,
            ref float ceilingY,
            int mode,
            int iterations,
            float slopeLimit)
        {
            // 1000b2ee: the lookahead is a flat 10 m, re-seated on every free move and every slide.
            Vec3 target = position + direction * LookAhead;

            // 1000b31a / 1000c22f: `dec` at the top, `jg` at the bottom -- the body runs `iterations`
            // times.
            while (iterations-- > 0)
            {
                Vec3 move = target - position;
                if (move.LengthSquared < 1e-08f)           // 1000b3ea, 1e-08
                    return;
                move = move * (1f / move.Length);          // 1000b404, SetLength(1)

                // ---- the two lateral probes and the two shoulder rays (1000b409..1000b7ff) ----
                bool leftValid = false, rightValid = false;
                Vec3 leftHit = Vec3.Zero, rightHit = Vec3.Zero;
                Vec3 leftNormal = Vec3.Zero, rightNormal = Vec3.Zero;

                if (mode > 1)                              // 1000b409
                {
                    Vec3 side, other;
                    if (move.Y * move.Y < 0.999999f)       // 1000b41a, a double
                    {
                        // 1000b472: normalize(cross(move, worldUp)) * halfWidth, and the opposite
                        // side is the component-wise negation (1000b4e6).
                        Vec3 across = Vec3.Cross(move, Vec3.ReferenceUp);
                        side = across * (halfWidth / across.Length);
                        other = -side;
                    }
                    else
                    {
                        // 1000b427: a move that is essentially straight up or down has no
                        // meaningful "across", so stock falls back to world X.
                        side = new Vec3(halfWidth, 0f, 0f);
                        other = new Vec3(-halfWidth, -0f, -0f);
                    }

                    // 1000b50d / 1000b566: a lateral hit shortens that side to the hit point.
                    if (surface.GetLineIntersection(
                            position, position + side, out Vec3 sideHit, out _, true, this))
                        side = sideHit - position;
                    if (surface.GetLineIntersection(
                            position, position + other, out Vec3 otherHit, out _, true, this))
                        other = otherHit - position;

                    // 1000b5b3: when the two sides reach different depths the body is recentred
                    // between them. The comparison is on the SQUARED lengths.
                    if (side.LengthSquared != other.LengthSquared)
                    {
                        Vec3 a = position + side;
                        Vec3 b = position + other;
                        position = (a + b) / 2f;           // 1000b619, (a + b) / 2
                        side = a - position;
                        other = b - position;

                        if (position.Y > ceilingY)         // 1000b668
                            ceilingY = position.Y;
                    }

                    side = side * 0.5f;                    // 1000b681
                    other = other * 0.5f;

                    leftValid = ProbeShoulder(
                        surface, this, position, target, move, side, out leftHit, out leftNormal);
                    rightValid = ProbeShoulder(
                        surface, this, position, target, move, other, out rightHit, out rightNormal);
                }

                // 1000b801: the centre ray, always.
                bool centreValid = surface.GetLineIntersection(
                    position, target, out Vec3 centreHit, out Vec3 centreNormal, true, this);

                // 1000b821..1000b839: left, then centre, then right -- any hit goes to the response.
                if (!leftValid && !centreValid && !rightValid)
                {
                    // ---- free move (1000b83f..1000b99d) --------------------------------
                    if (position.Equals(target))           // 1000b845, Vector3::operator==
                        return;

                    Vec3 step = target - position;
                    float distance = step.Length;          // 1000b862 + sqrt at 1000b867
                    step = step * ((distance - halfWidth) / distance);

                    // Both budget arms end the whole function when they run out.
                    if (Advance(ref position, step, ref horizontalBudget, ref totalBudget, ref ceilingY)
                        != AdvanceResult.Moved)
                        return;

                    target = position + direction * LookAhead;   // 1000b987
                    continue;
                }

                // ---- the nearest of the three wins (1000bb95..1000bc74) ----------------
                // A probe that did not hit contributes 10000 -- a float sentinel larger than any
                // real squared distance inside the 10 m lookahead.
                float dLeft = leftValid ? (leftHit - position).LengthSquared : NoHitDistance;
                float dRight = rightValid ? (rightHit - position).LengthSquared : NoHitDistance;
                float dCentre = centreValid ? (centreHit - position).LengthSquared : NoHitDistance;

                Vec3 hit, normal;
                if (dRight > dLeft && dCentre > dLeft)     // 1000bc1a, 1000bc29
                {
                    hit = leftHit;
                    normal = leftNormal;
                }
                else if (dCentre > dRight)                 // 1000bc52
                {
                    hit = rightHit;
                    normal = rightNormal;
                }
                else
                {
                    hit = centreHit;
                    normal = centreNormal;
                }

                float pushDistance = halfWidth;            // 1000bc74, the default

                // 1000bc7e: `fldz; fcomp move.y; jp skip` -- skipped only when 0 > move.y, so a
                // perfectly level move (every grounded walker) DOES take this branch.
                // 1000bc90: and only when slopeLimit > normal.y.
                if (!(0f > move.Y) && slopeLimit > normal.Y)
                {
                    pushDistance = normal.Y * normal.Y * halfWidth + halfWidth;   // 1000bca3

                    // 1000bcb3..1000bd0a, in this order:
                    //   normal.y <= -0.99            -> bounce straight back
                    //   |normal|^2 <= 0.001          -> bounce (degenerate normal)
                    //   |normal.x| > 0.001           -> flatten to a vertical wall
                    //   |normal.z| > 0.001           -> flatten
                    //   otherwise                    -> bounce (no horizontal component at all)
                    // The two 0.001 thresholds are different constants: 1001270c is a float and
                    // 10012768 a double, and they are compared against different quantities.
                    bool flatten;
                    if (normal.Y <= -0.99f)
                        flatten = false;
                    else if (normal.LengthSquared <= 0.001f)
                        flatten = false;
                    else if (Math.Abs(normal.X) > 0.001f)
                        flatten = true;
                    else
                        flatten = Math.Abs(normal.Z) > 0.001f;

                    if (flatten)
                    {
                        // 1000be3f: the slope is replaced by the vertical wall under it.
                        Vec3 flat = new Vec3(normal.X, 0f, normal.Z);
                        normal = flat * (1f / flat.Length);
                    }
                    else
                    {
                        normal = -move;                    // 1000bd10
                    }
                }

                // ---- the push-out (1000bd3d..1000bee8) --------------------------------
                Vec3 back = -move;
                float d = Vec3.Dot(back, normal);
                if (0f > d)                                // 1000bd99
                    d = -d;

                float toHitSquared = (hit - position).LengthSquared;

                // The default is the CURRENT position (`mov esi, ebx` at 1000bee6): when the hit is
                // nearer than the push-out distance the move is REFUSED outright, not clamped to the
                // hit. This is what stops a body climbing a face it cannot walk up.
                Vec3 newPosition = position;
                if (d > 0f)                                // 1000bdb2
                {
                    float t = pushDistance / d;
                    if (!(toHitSquared < t * t))           // 1000bde6
                        newPosition = hit + back * t;
                }
                else if (!(halfWidth * halfWidth >= toHitSquared))   // 1000bea1
                {
                    // note the raw halfWidth here, not the (possibly grown) pushDistance
                    newPosition = hit + back * halfWidth;
                }

                // 1000bef8: only advance when the position actually changed; either way the slide
                // below still runs.
                if (!newPosition.Equals(position))
                {
                    if (Advance(ref position, newPosition - position,
                                ref horizontalBudget, ref totalBudget, ref ceilingY)
                        == AdvanceResult.Exhausted)
                        return;
                }

                // ---- the slide (1000c0a7..1000c22f) ----------------------------------
                Vec3 toHit = position - hit;
                if (toHit.IsZero)                          // 1000c0c9
                    return;
                toHit = toHit * (1f / toHit.Length);        // 1000c0e2, SetLength(1)

                float separation = (toHit - normal).LengthSquared;
                if (1e-05f > separation)                   // 1000c120 -- already flush
                    return;
                if (separation > 3.99999f)                 // 1000c134 -- directly opposed
                    return;
                if (normal.IsZero)                         // 1000c147
                    return;

                // cross(cross(toHit, normal), normal) == normal*(toHit.normal) - toHit, i.e. MINUS
                // the part of toHit that lies in the surface -- so it points the way the body was
                // already going, projected onto the face.
                Vec3 slide = Vec3.Cross(Vec3.Cross(toHit, normal), normal);
                slide = slide * (1f / slide.Length);        // 1000c1a0

                if (!(Vec3.Dot(slide, direction) > 0f))     // 1000c1d6 -- sliding backwards, stop
                    return;

                target = position + slide * LookAhead;      // 1000c1fb

                // 1000c20d: a perfectly vertical face is never climbed.
                if (normal.Y == 0f && position.Y < target.Y)
                    target.Y = position.Y;
            }
        }

        /// <summary>
        /// One shoulder's forward ray (<c>1000b6d3</c>/<c>1000b772</c> and the projections at
        /// <c>1000b9a2</c>/<c>1000ba34</c>). It runs from the offset shoulder toward the offset
        /// target, and the hit is then <b>projected back onto the movement line</b> so all three
        /// candidates are compared in the same frame of reference. The ray's own normal is kept —
        /// stock does not re-query it.
        /// </summary>
        static bool ProbeShoulder(
            ISurface surface, object locality, Vec3 position, Vec3 target, Vec3 move, Vec3 side,
            out Vec3 hit, out Vec3 normal)
        {
            hit = Vec3.Zero;
            normal = Vec3.Zero;

            if (side.IsZero)                               // 1000b6c6
                return false;

            if (!surface.GetLineIntersection(
                    position + side, target + side, out Vec3 rawHit, out normal, true, locality))
                return false;

            // 1000b730: a hit whose normal faces the same way as the shoulder offset is a back face
            // -- the surface this shoulder is already sliding along -- and is discarded.
            Vec3 unitSide = side * (1f / side.Length);      // 1000b72b, SetLength(1)
            if (Vec3.Dot(unitSide, normal) > 0f)
            {
                normal = Vec3.Zero;
                return false;
            }

            float denominator = Vec3.Dot(move, normal);     // 1000b9a2
            if (denominator == 0f)                          // 1000b9c5
            {
                normal = Vec3.Zero;
                return false;
            }

            float t = Vec3.Dot(rawHit - position, normal) / denominator;
            hit = position + move * t;                      // 1000ba1f
            return true;
        }

        /// <summary>What <see cref="Advance"/> did, mirroring stock's three exits.</summary>
        protected enum AdvanceResult
        {
            /// <summary>The whole step was taken and both budgets were charged for it.</summary>
            Moved,

            /// <summary>
            /// The step had no length. Stock's free-move path returns from the sweep here
            /// (<c>1000c23b</c>/<c>1000c239</c>); its collision path falls through to the slide
            /// (<c>1000c0a7</c>/<c>1000c0a5</c>).
            /// </summary>
            Degenerate,

            /// <summary>
            /// A budget ran out. Stock advances the surviving fraction, zeroes <b>both</b> budgets
            /// and returns from the sweep — the four out-of-line blocks at <c>1000c242</c>,
            /// <c>1000c294</c>, <c>1000c2e8</c> and <c>1000c33d</c>.
            /// </summary>
            Exhausted,
        }

        /// <summary>
        /// Charges <paramref name="step"/> against both budgets and moves the body
        /// (<c>1000b8c3</c>..<c>1000b99d</c> for the free move, <c>1000bf18</c>..<c>1000c002</c> for
        /// the collision response — the same shape twice, plus the four out-of-line partial blocks).
        ///
        /// <para>
        /// Two independent arms, and which one runs is decided <b>before</b> either budget is
        /// compared to the step: the horizontal arm needs both a positive horizontal budget and a
        /// positive horizontal distance, and when it takes the whole step it does <b>not</b> consult
        /// the total budget at all — so the total can legitimately go negative. Only the other arm
        /// checks it.
        /// </para>
        /// </summary>
        static AdvanceResult Advance(
            ref Vec3 position, Vec3 step,
            ref float horizontalBudget, ref float totalBudget, ref float ceilingY)
        {
            // 1000b8ad: the horizontal length is measured through a (1,0,1) component mask, and the
            // sqrt is skipped when the squared length is not positive (1000b8dc).
            float horizontalSquared = new Vec3(step.X, 0f, step.Z).LengthSquared;
            float horizontal = horizontalSquared > 0f
                ? (float)Math.Sqrt(horizontalSquared)
                : horizontalSquared;

            if (horizontalBudget > 0f && horizontal > 0f)        // 1000b8fb, 1000b90d
            {
                if (horizontalBudget <= horizontal)              // 1000b91c
                {
                    position += step * (horizontalBudget / horizontal);
                    if (position.Y > ceilingY)
                        ceilingY = position.Y;
                    totalBudget = 0f;
                    horizontalBudget = 0f;
                    return AdvanceResult.Exhausted;
                }

                position += step;
                if (position.Y > ceilingY)
                    ceilingY = position.Y;
                horizontalBudget -= horizontal;
                totalBudget -= step.Length;
                return AdvanceResult.Moved;
            }

            float totalSquared = step.LengthSquared;
            if (!(0f < totalSquared))                            // 1000badd, 1000c021
                return AdvanceResult.Degenerate;

            float total = (float)Math.Sqrt(totalSquared);
            if (total <= 0f)                                     // 1000bb02, 1000c046
                return AdvanceResult.Degenerate;

            if (totalBudget <= total)                            // 1000bb14, 1000c054
            {
                position += step * (totalBudget / total);
                if (position.Y > ceilingY)
                    ceilingY = position.Y;
                totalBudget = 0f;
                horizontalBudget = 0f;
                return AdvanceResult.Exhausted;
            }

            position += step;
            if (position.Y > ceilingY)
                ceilingY = position.Y;
            horizontalBudget -= horizontal;
            totalBudget -= total;
            return AdvanceResult.Moved;
        }

        /// <summary>
        /// <c>FUN_1000c616</c> — the body orientation update, run at the end of every
        /// <see cref="EnsureSurfaceAlignment"/>.
        ///
        /// <para>
        /// <b>Mode 1 is the character's</b> (`SetOrientationMode(1)` from Gamecode at `1006eb9d`,
        /// `1006ec56`, `1006ee00` — see §7.1l), and the switch at <c>1000c61f</c> reaches it at
        /// <c>1000c88c</c>. The body is turned to <c>LookRotation(forward, surfaceNormal)</c>, where
        /// <b>forward is the velocity when the body is moving</b> and the cached body forward when it
        /// is stopped (<c>1000c88c</c>: the branch on the stored speed <c>+0xcc</c>).
        /// </para>
        ///
        /// <para>
        /// It writes <b>two</b> rotations and the cached forward, and the split is what makes
        /// backpedalling work. With <see cref="Direction"/> negative the visible body rotation
        /// (<c>+0x80</c>) is built from the <b>negated</b> forward, so the character keeps facing the
        /// way it was going while it walks backwards, and the cached forward (<c>+0xc0</c>, which is
        /// what <see cref="SteeringForward"/> and <see cref="SteeringReverse"/> push along) is negated
        /// with it. <see cref="SavedRotation"/> (<c>+0x70</c>) always gets the un-negated one.
        /// </para>
        /// </summary>
        protected bool UpdateOrientation(Vec3 surfaceNormal)
        {
            if (OrientationMode != 1)
                return false;

            // 1000c88c. Note stock does not test this for zero -- it cannot be, because Speed is
            // |Velocity| and the cached forward is a unit vector.
            Vec3 forward = Speed == 0f ? GetBodyForward() : Velocity;

            OrientationUp = surfaceNormal;                  // +0xb4 = the argument, 1000c8b6

            if (Direction < 0)                              // 1000c8a7 / 1000c8f1
            {
                Vec3 back = -forward;
                BodyRotation = Quat.LookRotation(back, surfaceNormal);
                CacheBodyForward(back);                     // 1000c91c
                SavedRotation = Quat.LookRotation(forward, surfaceNormal);   // 1000c936
            }
            else
            {
                BodyRotation = Quat.LookRotation(forward, surfaceNormal);
                CacheBodyForward(forward);                  // 1000c8da
                SavedRotation = BodyRotation;               // 1000c8e5, a 16-byte copy of +0x80
            }

            return true;
        }

        /// <summary>
        /// +0x90, <c>GetDir</c> (<c>10009fb1</c>). <b>Negative means the body is travelling
        /// backwards</b>: the orientation update then faces it the other way, so a backpedalling
        /// character keeps looking where it came from. Set it through
        /// <see cref="SetDirection"/>, never directly — changing it halts the vehicle.
        /// </summary>
        public int Direction { get; private set; } = 1;

        /// <summary>
        /// <c>Vehicle_t::SetDirection(int)</c> (<c>1000a6e4</c>, ordinal 205).
        ///
        /// <para>
        /// Gamecode pairs this with every longitudinal drive change and it is <b>not optional</b>:
        /// the backward command is <c>SetForwardDrive(-1); SetDirection(-1)</c> (<c>1006f122</c>), the
        /// forward command is <c>SetDirection(1); SetForwardDrive(1)</c> (<c>1006ef8d</c>) and
        /// releasing both is <c>SetForwardDrive(0); Halt(); SetDirection(1)</c> (<c>1006f23a</c>).
        /// Without the <c>-1</c> the orientation update turns the body to face its backward velocity,
        /// <see cref="SteeringReverse"/> then pushes the other way, and the body oscillates on the
        /// spot instead of backing up.
        /// </para>
        /// </summary>
        public void SetDirection(int direction)
        {
            if (direction != Direction)
            {
                SavedRotation = BodyRotation;               // 1000a6f7, 16 bytes
                HaltIfMoving();                             // 1000a704 -> 1000a688
            }

            Direction = direction;                          // 1000a70e
        }

        /// <summary>
        /// <c>Vehicle_t::SetRelRot(const Quaternion&amp;)</c> (<c>1000d11d</c>, ordinal 219) — turn the
        /// body and <b>re-aim the velocity along the new facing</b>, keeping its magnitude and
        /// multiplying by <see cref="Direction"/> (<c>1000d15f</c>: <c>fild [ebx+0x90]</c>).
        ///
        /// <para>
        /// That last factor is the other half of backpedalling: a body with
        /// <see cref="Direction"/> <c>-1</c> travels <i>opposite</i> to where it is pointing, so
        /// turning while backing up swings the path the right way instead of flipping it.
        /// </para>
        ///
        /// <para>
        /// This is also the <b>only</b> correct way to hand a vehicle an externally-authored heading
        /// while it is moving — the camera's right-drag, a path turn, a warp. Assigning
        /// <see cref="BodyRotation"/> alone is silently undone on the same frame, because orientation
        /// mode 1 rebuilds the rotation from the velocity whenever the body has speed.
        /// </para>
        ///
        /// <para>
        /// <b>Not ported:</b> stock's first line is <c>if (vtable[+0xc]() &lt; 0) return</c>, i.e.
        /// <c>LocalitySource_t::GetZone()</c> (<c>10002009</c>, <c>return this-&gt;0x14</c>) — it
        /// ignores the rotation for a vehicle that is not placed in a zone. There is no zone id on
        /// this sim and a simulated character is always in a playfield, so the guard has nothing to
        /// test.
        /// </para>
        /// </summary>
        public void SetRelRot(Quat rotation)
        {
            float speed = Velocity.Length;                  // 1000d156 + sqrt at 1000d16f

            BodyRotation = rotation;                        // 1000d13a, and refreshes +0xc0
            SavedRotation = rotation;                       // 1000d141
            CacheBodyForward();                             // 1000d14c, explicit in stock

            Velocity = GetBodyForward() * speed * Direction;   // 1000d184..1000d19d
        }

        /// <summary>
        /// <c>FUN_1000a688</c> — the halt hidden inside <see cref="SetDirection"/>. Reversing from a
        /// run stops the body dead first, which is why direction changes are crisp in stock.
        /// </summary>
        void HaltIfMoving()
        {
            if (Speed == 0f)                                // 1000a691
                return;

            Speed = 0f;                                     // 1000a6b7
            Velocity = Vec3.Zero;                           // 1000a6bd
            SavedRotation = BodyRotation;                   // 1000a6c0
            CacheBodyForward();                             // 1000a6cf, FUN_1000a47a(null)
            OnHalt();                                       // 1000a6d8, vtable +0x68
        }

        /// <summary>
        /// +0x70, a second rotation stock keeps beside the body rotation. Mode 1,
        /// <see cref="SetDirection"/> and the halt all write it and nothing in <c>Vehicle.dll</c>
        /// reads it, so it is carried for fidelity: it holds the <b>un-negated</b> facing, i.e. the
        /// way the body is actually travelling.
        /// </summary>
        public Quat SavedRotation = Quat.Identity;

        /// <summary>
        /// +0xc0, the cached forward vector — what <see cref="GetBodyForward"/> returns. Stock keeps
        /// it in sync explicitly: every writer of <see cref="BodyRotation"/> also calls
        /// <c>FUN_1000a47a</c>.
        /// </summary>
        Vec3 _cachedForward = Vec3.ReferenceForward;

        /// <summary>
        /// <c>FUN_1000a47a</c> with a vector — caches it verbatim, <b>without normalising</b>
        /// (<c>1000a4a8</c>).
        /// </summary>
        void CacheBodyForward(Vec3 forward) => _cachedForward = forward;

        /// <summary>
        /// <c>FUN_1000a47a(null)</c> — rebuilds the cache from the body rotation
        /// (<c>1000a48b</c>: <c>s_cReferenceForward</c> taken through <c>+0x80</c>).
        /// </summary>
        void CacheBodyForward() => _cachedForward = BodyRotation * Vec3.ReferenceForward;

        /// <summary>
        /// One leg of the tripod. A miss returns the low point with an upward normal, which is what
        /// stock writes (<c>1000d88e</c>).
        /// </summary>
        /// <summary>
        /// The second tripod's leg: the near end is offset by <see cref="SecondProbeRadius"/> and the
        /// far end by <see cref="SecondProbeReach"/> (<c>1000da7f</c> / <c>1000daea</c>).
        /// </summary>
        Vec3 ProbeOneAt(ISurface surface, Vec3 high, Vec3 low, Vec3 direction)
        {
            Vec3 from = high + direction * SecondProbeRadius;
            Vec3 to = low + direction * SecondProbeReach;

            if (surface.GetLineIntersection(from, to, out Vec3 hit, out _, true, this))
                return hit;

            return to;
        }

        Vec3 ProbeOne(ISurface surface, Vec3 high, Vec3 low, Vec3 direction)
        {
            Vec3 offset = direction * ProbeRadius;
            Vec3 from = high + offset;
            Vec3 to = low + offset;

            if (surface.GetLineIntersection(from, to, out Vec3 hit, out _, true, this))
                return hit;

            return to;
        }

        /// <summary>
        /// <c>Vehicle_t::LandNow</c> (<c>1000a719</c>) — come out of the air at a given height.
        ///
        /// <para>
        /// Zeroes the vertical motion, <b>recomputes the stored speed</b> from the velocity (the
        /// integrator gates translation on the stored speed, so missing this leaves a vehicle
        /// unable to move), notifies through vtable <c>+0x6c</c>, then clears
        /// <see cref="Airborne"/> — in that order.
        /// </para>
        /// </summary>
        public void LandNow(float height)
        {
            Velocity.Y = 0f;
            VerticalVelocity = 0f;
            Speed = Velocity.Length;
            OnLanded(height);
            Airborne = false;
        }

        /// <summary>
        /// <c>DummyVehicle_t::GetSurface</c> (<c>N3.dll 100011e0</c>) — the surface this vehicle
        /// collides against, or null. Stock resolves it lazily from the playfield and gates it on
        /// the surface-collision flag <c>+0x15c</c>.
        ///
        /// <para>
        /// Returning null makes <see cref="EnsureSurfaceAlignment"/> a no-op, which is stock's own
        /// first line and what keeps the camera vehicles behaving as they did.
        /// </para>
        /// </summary>
        protected virtual ISurface GetSurface() => null;

        /// <summary>vftable +0x58. Called when a blocked move gives up.</summary>
        protected virtual void OnHalt() { }

        /// <summary>
        /// <c>Vehicle_t::Halt</c> (<c>1000a688</c>) — zeroes the motion state. Called by the
        /// integrator when the longitudinal channel returns <see cref="SteeringResult.Halt"/>.
        /// </summary>
        public virtual void Halt()
        {
            Velocity = Vec3.Zero;
            SteerForce = Vec3.Zero;
            Speed = 0f;
        }

        /// <summary>
        /// <c>Vehicle_t::DisableFalling</c> (<c>1000c3b7</c>).
        ///
        /// <para>
        /// <b>It has two branches</b>, and an earlier version of this port implemented only the
        /// second: when falling is currently <i>enabled</i> it clears the flag and lands, and only
        /// when it is already disabled does it zero the vertical motion directly. Without the first
        /// branch the flag was never cleared, so <c>DisableFalling</c> did not actually disable
        /// falling. Found by <c>CharVehicleSimTests.FlyTurnsGravityOff</c>.
        /// </para>
        ///
        /// <para>It does <b>not</b> touch <see cref="SurfaceHug"/>.</para>
        /// </summary>
        public void DisableFalling()
        {
            if (FallingEnabled)
            {
                FallingEnabled = false;
                LandNow(Position.Y);

                if (InLiquid)
                {
                    OnLeaveLiquid();
                    InLiquid = false;
                }

                return;
            }

            Velocity.Y = 0f;
            VerticalVelocity = 0f;
            Speed = Velocity.Length;
            Airborne = false;
        }

        /// <summary>
        /// <c>Vehicle_t::EnableFalling</c> (<c>1000c394</c>) — sets the flag, lands at the current
        /// height, then immediately begins falling again. A no-op when already enabled.
        /// </summary>
        public void EnableFalling()
        {
            if (FallingEnabled)
                return;

            FallingEnabled = true;
            LandNow(Position.Y);
            BeginFalling();
        }

        /// <summary>
        /// <c>FUN_1000a1a7</c> — go airborne, once. The vtable call at <c>+0x74</c> is a
        /// notification; <see cref="OnBeginFall"/> stands for it.
        /// </summary>
        public void BeginFalling()
        {
            if (Airborne)
                return;

            Airborne = true;
            OnBeginFall();
        }

        /// <summary>
        /// <c>Vehicle_t::Impact</c> (<c>1000a1b8</c>) — an impulse, which stock only accepts when it is
        /// <b>purely vertical</b> and the body is <b>on the ground</b>. Anything else is dropped whole:
        /// airborne returns first, then a non-zero x or z (each <c>fucomp</c> against 0; unordered also
        /// drops it). What survives is <c>+0x54 += y * (1 / mass)</c> followed by
        /// <see cref="BeginFalling"/>.
        /// </summary>
        public void Impact(Vec3 impulse)
        {
            if (Airborne)
                return;
            if (!(impulse.X == 0f) || !(impulse.Z == 0f))
                return;

            VerticalVelocity += 1f / Mass * impulse.Y;
            BeginFalling();
        }

        /// <summary>+0x120, the in-liquid flag. Entering and leaving call vtable slots 32 and 33.</summary>
        public bool InLiquid;

        /// <summary>Stands for the vtable <c>+0x74</c> notification in <c>FUN_1000a1a7</c>.</summary>
        protected virtual void OnBeginFall() { }

        /// <summary>Stands for the vtable <c>+0x6c</c> notification in <c>LandNow</c>.</summary>
        protected virtual void OnLanded(float height) { }

        /// <summary>Stands for the vtable <c>+0x84</c> notification, leaving liquid.</summary>
        protected virtual void OnLeaveLiquid() { }

        /// <summary><c>Vehicle_t::DisableSurfaceHug</c> (<c>10009f4e</c>).</summary>
        public void DisableSurfaceHug() => SurfaceHug = false;

        /// <summary><c>Vehicle_t::EnableSurfaceHug</c> (<c>10009f49</c>).</summary>
        public void EnableSurfaceHug() => SurfaceHug = true;

        /// <summary>
        /// <c>Vehicle_t::SetVel</c> (<c>1000a4b1</c>). Sets the velocity <b>and</b> recomputes
        /// <see cref="Speed"/> — assigning <see cref="Velocity"/> on its own leaves the stored speed
        /// stale, and the integrator gates translation on the stored speed, not on the vector.
        /// </summary>
        public void SetVel(Vec3 velocity)
        {
            Velocity = velocity;
            Speed = velocity.Length;
        }

        /// <summary>
        /// <c>Vehicle_t::SetRelPos</c> (<c>1000e21d</c>) — put the vehicle at a position <b>now</b>,
        /// with collision, bypassing steering entirely. This is how stock does anything that has to
        /// track input one-to-one, mouse-look orbit above all: the camera is moved, not steered at.
        /// </summary>
        public void SetRelPos(Vec3 position)
        {
            if (position.Equals(Position))
                return;

            Vec3 previous = Position;
            Position = position;
            PreviousPosition = position;    // stock stores the new position here, not the old
            EnsureSurfaceAlignment(previous, false);
        }

        // ---- the tick ------------------------------------------------------

        /// <summary>
        /// <c>Vehicle_t::Run(float)</c> (<c>1000e849</c>), physics path only. Returns whether the
        /// vehicle moved. The pending-relative-move and functional-path branches of stock's
        /// <c>Run</c> are not ported yet (Docs/Camera.md §4).
        /// </summary>
        public bool Run(float dt)
        {
            if (!IsRunEnabled())
                return false;

            return Step(dt);
        }

        /// <summary>
        /// <c>FUN_1000e3d3</c> — the sub-stepping physics loop. The step is capped at
        /// <see cref="MaxSubStep"/> and the loop runs as many times as it takes to consume
        /// <paramref name="dt"/>. That bounds how far a long frame can diverge; it does not make the
        /// result frame-rate independent (Docs/Camera.md §4.1).
        /// </summary>
        protected bool Step(float dt)
        {
            // A frame this long is dropped entirely — stock does not try to catch up (1000e3e6).
            if (dt > MaxFrameTime || dt <= 0f)
                return false;

            bool moved = false;
            float elapsed = 0f;

            do
            {
                float step = dt - elapsed;
                if (step > MaxSubStep)
                    step = MaxSubStep;

                DeltaTimeNow = step;

                Vec3 stepStartPosition = Position;

                // 1. Longitudinal steering into the force accumulator.
                SteeringResult longitudinal = CalcSteering(out Vec3 force);
                SteerForce = force;
                if (longitudinal == SteeringResult.Halt)
                {
                    SteerForce = Vec3.Zero;
                    Halt();
                }
                else if (longitudinal != SteeringResult.Force)
                {
                    SteerForce = Vec3.Zero;
                }

                // 2. Gravity. Only accumulates while airborne; when grounded and falling is enabled,
                //    both the accumulator and the vertical component of velocity are cleared.
                if (Airborne)
                {
                    VerticalVelocity += GravityAccel * step;
                }
                else if (SurfaceHug)
                {
                    VerticalVelocity = 0f;
                    Velocity.Y = 0f;
                }

                // 3. Force -> velocity. Runs when the steering asked for it or gravity is in play.
                if (longitudinal == SteeringResult.Force || Airborne)
                {
                    Vec3 f = SteerForce;
                    Vec3.Truncate(ref f, MaxForce);

                    Velocity += (f * step) / Mass;

                    if (VerticalVelocity > MaxFallSpeed)
                        VerticalVelocity = MaxFallSpeed;
                    else if (VerticalVelocity < -MaxFallSpeed)
                        VerticalVelocity = -MaxFallSpeed;

                    Vec3 v = Velocity;
                    Speed = Vec3.Truncate(ref v, MaxVel);
                    Velocity = v;
                }

                bool moving = IsMoving;
                Vec3 velocityThisStep = Velocity;

                // 4. Lateral steering, applied straight to position.
                SteeringResult lateralResult = CalcLateralSteering(out Vec3 lateral);
                if (lateralResult == SteeringResult.Halt)
                {
                    lateral = Vec3.Zero;
                }
                else if (lateralResult == SteeringResult.Lateral)
                {
                    if (!moving)
                    {
                        Vec3.Truncate(ref lateral, MaxVel);
                    }
                    else
                    {
                        // Strafing redirects, it never adds speed: scale the velocity and the
                        // lateral vector together so their sum keeps the current speed (1000e690).
                        float combined = (velocityThisStep + lateral).Length;
                        if (combined > 0f)
                        {
                            float k = Speed / combined;
                            velocityThisStep *= k;
                            lateral *= k;
                        }
                    }

                    Position += lateral * step;
                    moved = true;
                }

                // 5. Translate.
                if (moving || VerticalVelocity != 0f)
                {
                    Position += velocityThisStep * step;
                    Position.Y += VerticalVelocity * step;
                    moved = true;
                }

                // 6. Turn steering. Rotates the body while standing still, the velocity while moving
                //    — so a moving vehicle curves its path rather than sliding sideways (1000e7c6).
                SteeringResult turnResult = CalcTurnSteering(out Vec3 turn);
                if (turnResult == SteeringResult.Halt)
                {
                    turn = Vec3.Zero;
                }
                else if (turnResult == SteeringResult.Turn)
                {
                    float rate = turn.Length;
                    if (rate > TurnEpsilon)
                    {
                        Vec3 axis = turn / rate;
                        float angle = rate * step;
                        Quat rotation = Quat.FromAxisAngle(axis, angle);

                        if (Velocity.X == 0f && Velocity.Z == 0f)
                            BodyRotation = (rotation * BodyRotation).Normalized;
                        else
                            Velocity = rotation * Velocity;
                    }

                    moved = true;
                }

                PreviousPosition = stepStartPosition;

                if (!EnsureSurfaceAlignment(stepStartPosition, false))
                    break;

                elapsed += step;
            }
            while (elapsed < dt);

            return moved;
        }

        // ---- steering behaviours -------------------------------------------

        /// <summary>
        /// <c>Vehicle_t::SteeringHalt</c> (<c>1000a2de</c>, ordinal 227). Asks the integrator to stop
        /// this channel.
        /// </summary>
        public SteeringResult SteeringHalt(out Vec3 steer)
        {
            steer = Vec3.Zero;
            return SteeringResult.Halt;
        }

        /// <summary>
        /// <c>Vehicle_t::SteeringForward</c> (<c>1000ca73</c>, ordinal 226) — full max force along
        /// the body's forward. Note it returns the force already at <see cref="MaxForce"/>, so the
        /// integrator's clamp is exactly saturated.
        /// </summary>
        public SteeringResult SteeringForward(out Vec3 steer)
        {
            steer = GetBodyForward() * MaxForce;
            return SteeringResult.Force;
        }

        /// <summary><c>Vehicle_t::SteeringReverse</c> (<c>1000cab3</c>, ordinal 228).</summary>
        public SteeringResult SteeringReverse(out Vec3 steer)
        {
            steer = GetBodyForward() * -MaxForce;
            return SteeringResult.Force;
        }

        /// <summary>
        /// <c>Vehicle_t::GetBodyForward</c> (<c>1000c5f3</c>) — returns the <b>cached</b> forward
        /// (<c>+0xc0</c>), rebuilding it only when the body rotation is still the zero quaternion
        /// (<c>10009c11</c> tests all four components, so this is a lazy-init guard and not a dirty
        /// flag). Every writer of the rotation refreshes the cache itself.
        /// </summary>
        public Vec3 GetBodyForward()
        {
            if (BodyRotation.X == 0f && BodyRotation.Y == 0f && BodyRotation.Z == 0f && BodyRotation.W == 0f)
                CacheBodyForward();

            return _cachedForward;
        }

        /// <summary>
        /// <c>Vehicle_t::SteeringSeek</c> (<c>1000a87c</c>, ordinal 229) — go at it flat out. Unlike
        /// <see cref="SteeringArrive"/> there is no slow-down and no brake distance, and the result
        /// is scaled by <see cref="MaxForce"/> rather than by mass, so it saturates the integrator's
        /// clamp immediately.
        /// </summary>
        public SteeringResult SteeringSeek(Vec3 target, out Vec3 steer)
        {
            steer = Vec3.Zero;

            Vec3 toTarget = target - Position;
            float len = toTarget.Length;
            if (len == 0f)
                return SteeringResult.None;

            Vec3 desired = (toTarget / len) * MaxVel;
            steer = (desired - Velocity) * MaxForce;
            return SteeringResult.Force;
        }

        /// <summary>
        /// <c>Vehicle_t::SteeringArrive</c> (<c>1000ab28</c>, ordinal 223) — the behaviour the camera
        /// steers with. Slows down inside <see cref="SlowingDistance"/> and halts inside
        /// <paramref name="haltRadius"/>.
        ///
        /// The <c>* Mass * 4</c> is the whole acceleration model: aim to reach the desired velocity
        /// in 0.25 s, then convert that acceleration to a force. The integrator's
        /// <see cref="MaxForce"/> clamp is what actually limits how sharply the vehicle responds.
        /// </summary>
        /// <param name="haltRadius">0 means "use <see cref="HaltRadius"/>".</param>
        /// <summary>
        /// <c>Vehicle_t::SteeringDirArrive</c> (<c>1000ac8c</c>, ordinal 224) — arrive at a target, but
        /// <b>halt instead if the body has already gone past it</b>.
        ///
        /// <para>
        /// The overshoot test is an XZ dot product between "target from here" and "target from where I
        /// was last frame" (<c>1000acd2</c>, using <c>+0x58</c>/<c>+0x60</c> against
        /// <c>+0xd0</c>/<c>+0xd8</c>). Once the body passes the target those two point opposite ways and
        /// the dot goes negative, which is what stops an NPC orbiting a waypoint it cannot quite land on.
        /// Y is ignored entirely.
        /// </para>
        ///
        /// <para>
        /// Otherwise it is <see cref="SteeringArrive"/> with a brake distance of <b>0.2</b>
        /// (<c>10012298</c>).
        /// </para>
        /// </summary>
        public SteeringResult SteeringDirArrive(Vec3 target, out Vec3 steer)
        {
            float toHereX = target.X - Position.X;
            float toHereZ = target.Z - Position.Z;
            float toPrevX = target.X - PreviousPosition.X;
            float toPrevZ = target.Z - PreviousPosition.Z;

            // 1000acee: `dot >= 0` arrives, `dot < 0` halts.
            float dot = toPrevZ * toHereZ + toPrevX * toHereX;
            if (dot < 0f)
                return SteeringHalt(out steer);

            return SteeringArrive(target, out steer, DirArriveBrakeDistance);
        }

        /// <summary>The brake distance <c>SteeringDirArrive</c> passes on, <c>10012298</c>.</summary>
        public const float DirArriveBrakeDistance = 0.2f;

        public SteeringResult SteeringArrive(Vec3 target, out Vec3 steer, float haltRadius = 0f)
        {
            if (haltRadius == 0f)
                haltRadius = HaltRadius;

            Vec3 toTarget = target - Position;
            float d2 = toTarget.LengthSquared;

            // Note stock compares the *squared* distance against both the squared halt radius and
            // the literal 0.01 — the second is a squared-space epsilon, i.e. 0.1 m (1000ab7c).
            if (d2 < haltRadius * haltRadius || d2 < 0.01f)
                return SteeringHalt(out steer);

            float d = (float)Math.Sqrt(d2);

            // The divisor is the SLOWING distance (+0x40), which UpdateMotionConstraints keeps at
            // maxVel * 0.3. Dividing by a small constant instead makes this saturate at maxVel at
            // every distance, so the camera charges its goal flat out and overshoots — a visible
            // wobble while walking.
            float speed = (d / SlowingDistance) * MaxVel;
            if (speed > MaxVel)
                speed = MaxVel;

            Vec3 desired = toTarget * (speed / d);
            steer = (desired - Velocity) * (Mass * 4f);

            // A force this large means the state has blown up; stock declines to steer (1000ac6e).
            return steer.Length <= 1e7f ? SteeringResult.Force : SteeringResult.None;
        }
    }
}
