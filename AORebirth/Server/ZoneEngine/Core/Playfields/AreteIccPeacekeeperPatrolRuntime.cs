namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    #endregion

    /// <summary>
    /// Capture 20260722-235242: ICC Peacekeeper FollowTarget NpcPath loops.
    /// Capture 20260722-235510: nearby Peacekeeper attacks mobs that aggro the player
    /// (Rollerrat Attack→player, then Peacekeeper Attack→Rollerrat, 62 dmg one-shot).
    /// </summary>
    internal static class AreteIccPeacekeeperPatrolRuntime
    {
        internal const string PeacekeeperName = "ICC Peacekeeper";

        private const int GatePeacekeeperInstance = unchecked((int)0x797D337E);

        private const int BridgePeacekeeperInstance = unchecked((int)0x7962A3F9);

        // Capture 20260722-235510: PK at ~3411,773 engages rat ~18m away.
        private const float PlayerDefenseRadiusMeters = 30f;

        private static readonly object Gate = new object();

        private static readonly HashSet<int> RegisteredPeacekeeperInstances = new HashSet<int>();

        // Capture destination loop for 797D337E (elevator / HQ approach).
        private static readonly float[][] GateLoopWaypoints =
            {
                new[] { 3399.391f, 9.330f, 801.210f },
                new[] { 3403.470f, 9.010f, 803.082f },
                new[] { 3410.129f, 9.010f, 802.544f },
                new[] { 3414.697f, 9.010f, 807.255f },
                new[] { 3405.303f, 9.010f, 806.514f },
                new[] { 3399.960f, 9.031f, 805.990f },
                new[] { 3396.375f, 10.913f, 805.544f },
                new[] { 3393.544f, 12.215f, 803.782f },
                new[] { 3389.948f, 13.700f, 803.259f },
                new[] { 3385.902f, 15.723f, 803.281f },
                new[] { 3384.125f, 15.781f, 806.142f },
                new[] { 3383.289f, 15.781f, 809.823f },
                new[] { 3378.244f, 17.110f, 814.339f },
                new[] { 3378.237f, 17.110f, 812.480f },
                new[] { 3378.648f, 17.110f, 808.104f },
                new[] { 3379.236f, 17.110f, 802.274f },
                new[] { 3381.993f, 15.018f, 800.573f },
                new[] { 3381.959f, 15.053f, 793.694f },
                new[] { 3383.975f, 15.781f, 801.232f },
                new[] { 3386.452f, 15.392f, 801.756f },
                new[] { 3389.780f, 13.784f, 801.335f },
                new[] { 3392.583f, 12.648f, 802.104f },
            };

        // Capture destination loop for 7962A3F9 (bridge / market path).
        private static readonly float[][] BridgeLoopWaypoints =
            {
                new[] { 3410.723f, 3.393f, 773.751f },
                new[] { 3409.907f, 4.697f, 778.801f },
                new[] { 3413.102f, 4.961f, 782.014f },
                new[] { 3416.903f, 5.032f, 782.055f },
                new[] { 3421.454f, 5.110f, 781.221f },
                new[] { 3422.512f, 5.110f, 781.173f },
                new[] { 3426.892f, 5.110f, 781.125f },
                new[] { 3435.735f, 5.110f, 780.871f },
                new[] { 3439.747f, 4.810f, 781.019f },
                new[] { 3446.074f, 4.810f, 780.168f },
                new[] { 3430.722f, 3.010f, 768.149f },
                new[] { 3422.932f, 3.010f, 766.850f },
                new[] { 3415.636f, 3.010f, 769.056f },
            };

        public static void ClearPlayfield(int playfieldInstance)
        {
            lock (Gate)
            {
                RegisteredPeacekeeperInstances.Clear();
            }
        }

        public static void PrepareSpawnedPeacekeeper(Character mob, NPCController controller)
        {
            if (mob == null || controller == null)
            {
                return;
            }

            // Passive: no AOS on players; CanRetaliate so defense AcquireAggro works.
            controller.AiProfile = NpcAiProfile.Passive;

            // Capture 20260722-235510 AttackInfo Amount=62 WeaponSlot=6 vs Rollerrat.
            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                "arete-icc-peacekeeper-defend-20260722-235510",
                55,
                70,
                2.0,
                6,
                4,
                0);
            string unused;
            CapturedEnemyCombatRuntime.Prepare(mob, controller, contract, out unused);
            controller.AiProfile = NpcAiProfile.Passive;

            Register(mob.Identity.Instance);
        }

        public static bool TryApplyPatrol(int captureInstance, NPCController controller)
        {
            if (controller == null || captureInstance == 0)
            {
                return false;
            }

            float[][] waypoints = null;
            if (captureInstance == GatePeacekeeperInstance)
            {
                waypoints = GateLoopWaypoints;
            }
            else if (captureInstance == BridgePeacekeeperInstance)
            {
                waypoints = BridgeLoopWaypoints;
            }

            if (waypoints == null || waypoints.Length < 2)
            {
                return false;
            }

            controller.SetCapturedPatrolReplaySegments(
                BuildClosedLoop(waypoints),
                false,
                true,
                true);
            controller.State = CharacterState.Patrolling;
            return true;
        }

        /// <summary>
        /// Tick path: Peacekeeper looks for a nearby hostile already fighting a player.
        /// Needed because AcquireAggro defense only runs at first pull — if the fight
        /// starts far away and walks into PK range later, assist must still fire.
        /// </summary>
        public static ICharacter FindDefenseHostile(ICharacter peacekeeper)
        {
            if (peacekeeper == null
                || peacekeeper.Playfield == null
                || peacekeeper.RawCoordinates == null
                || peacekeeper.Stats[StatIds.health].Value <= 0
                || peacekeeper.FightingTarget.Instance != 0
                || !(peacekeeper.Controller is NPCController)
                || !IsRegisteredOrNamedPeacekeeper(peacekeeper))
            {
                return null;
            }

            Playfield playfield = peacekeeper.Playfield as Playfield;
            if (playfield == null)
            {
                return null;
            }

            List<ICharacter> inRange = playfield.FindCharacterInRange(peacekeeper, PlayerDefenseRadiusMeters);
            ICharacter best = null;
            double bestDistance = PlayerDefenseRadiusMeters;
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == peacekeeper.Identity.Instance
                    || !(candidate.Controller is NPCController)
                    || candidate.Stats[StatIds.health].Value <= 0
                    || candidate.FightingTarget.Instance == 0
                    || IsPeacekeeper(candidate)
                    || PlayerVersusPlayerCombatRules.IsPlayerControlledCombatant(candidate))
                {
                    continue;
                }

                ICharacter fighting = playfield.FindByIdentity<ICharacter>(candidate.FightingTarget);
                if (fighting == null
                    || !PlayerVersusPlayerCombatRules.IsPlayerControlledCombatant(fighting))
                {
                    continue;
                }

                double distance = peacekeeper.Coordinates().Distance2D(candidate.Coordinates());
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// When a hostile NPC is fighting a player, return nearby Peacekeepers that should assist.
        /// </summary>
        public static ICharacter[] FindPlayerDefenseAllies(ICharacter player, ICharacter hostile)
        {
            if (player == null
                || hostile == null
                || player.Playfield == null
                || !(hostile.Controller is NPCController)
                || IsPeacekeeper(hostile)
                || PlayerVersusPlayerCombatRules.IsPlayerControlledCombatant(hostile))
            {
                return new ICharacter[0];
            }

            Playfield playfield = player.Playfield as Playfield;
            if (playfield == null || player.RawCoordinates == null || hostile.RawCoordinates == null)
            {
                return new ICharacter[0];
            }

            Coordinate playerCoord = player.Coordinates();
            Coordinate hostileCoord = hostile.Coordinates();
            var allies = new List<ICharacter>();
            var seen = new HashSet<int>();

            // Search around both player and hostile — fight may be nearer the PK than the player.
            CollectDefenseAlliesNear(playfield, player, PlayerDefenseRadiusMeters, playerCoord, hostileCoord, allies, seen);
            CollectDefenseAlliesNear(playfield, hostile, PlayerDefenseRadiusMeters, playerCoord, hostileCoord, allies, seen);

            return allies.ToArray();
        }

        public static bool IsPeacekeeper(ICharacter npc)
        {
            return IsRegisteredOrNamedPeacekeeper(npc);
        }

        private static void CollectDefenseAlliesNear(
            Playfield playfield,
            ICharacter anchor,
            float radius,
            Coordinate playerCoord,
            Coordinate hostileCoord,
            List<ICharacter> allies,
            HashSet<int> seen)
        {
            List<ICharacter> inRange = playfield.FindCharacterInRange(anchor, radius);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || !seen.Add(candidate.Identity.Instance)
                    || !(candidate.Controller is NPCController)
                    || candidate.Stats[StatIds.health].Value <= 0
                    || candidate.FightingTarget.Instance != 0
                    || candidate.RawCoordinates == null
                    || !IsRegisteredOrNamedPeacekeeper(candidate))
                {
                    continue;
                }

                double toPlayer = candidate.Coordinates().Distance2D(playerCoord);
                double toHostile = candidate.Coordinates().Distance2D(hostileCoord);
                if (toPlayer > PlayerDefenseRadiusMeters && toHostile > PlayerDefenseRadiusMeters)
                {
                    continue;
                }

                allies.Add(candidate);
            }
        }

        private static bool IsRegisteredOrNamedPeacekeeper(ICharacter npc)
        {
            if (npc == null)
            {
                return false;
            }

            lock (Gate)
            {
                if (RegisteredPeacekeeperInstances.Contains(npc.Identity.Instance))
                {
                    return true;
                }
            }

            // Name match is enough for defense — monsterdata may differ if spawn stats lag.
            return string.Equals(npc.Name, PeacekeeperName, StringComparison.OrdinalIgnoreCase);
        }

        private static void Register(int npcInstance)
        {
            if (npcInstance == 0)
            {
                return;
            }

            lock (Gate)
            {
                RegisteredPeacekeeperInstances.Add(npcInstance);
            }
        }

        private static NpcPatrolReplaySegment[] BuildClosedLoop(float[][] waypoints)
        {
            var segments = new List<NpcPatrolReplaySegment>(waypoints.Length);
            for (int i = 0; i < waypoints.Length; i++)
            {
                float[] start = waypoints[i];
                float[] end = waypoints[(i + 1) % waypoints.Length];
                segments.Add(
                    new NpcPatrolReplaySegment(
                        0.0,
                        start[0],
                        start[1],
                        start[2],
                        end[0],
                        end[1],
                        end[2]));
            }

            return segments.ToArray();
        }
    }
}
