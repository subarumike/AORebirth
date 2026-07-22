namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    using Coordinate = AORebirth.Core.Vector.Coordinate;

    #endregion

    /// <summary>
    /// Attack-on-sight + combat contract for RK mission interior mobs (not OrdinaryEnemy catalog).
    /// </summary>
    internal static class MissionInstanceMobCombat
    {
        // Capture-backed feel: only aggro when the player is on top of the mob, not mission-wide.
        private const float AggroRadius = 2.0f;

        private static readonly object Gate = new object();

        private static readonly HashSet<int> AggressiveMobs = new HashSet<int>();

        private static readonly HashSet<int> FindItemHosts = new HashSet<int>();

        public static void RegisterAggressive(Identity identity)
        {
            if (identity.Instance == 0)
            {
                return;
            }

            lock (Gate)
            {
                AggressiveMobs.Add(identity.Instance);
            }
        }

        public static void RegisterFindItemHost(Identity identity)
        {
            if (identity.Instance == 0)
            {
                return;
            }

            lock (Gate)
            {
                FindItemHosts.Add(identity.Instance);
            }
        }

        public static bool IsFindItemHost(Identity identity)
        {
            lock (Gate)
            {
                return FindItemHosts.Contains(identity.Instance);
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            // Identities are unique globally; leave entries until corpse/despawn. No PF wipe needed.
        }

        public static bool TryPrepareCombat(Character mob, NPCController controller, int level)
        {
            if (mob == null || controller == null)
            {
                return false;
            }

            int minDamage = 40 + (level / 2);
            int maxDamage = 80 + level;
            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                "mission-instance-auto-aggro",
                minDamage,
                maxDamage,
                2.0,
                NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponSlot,
                0,
                NpcCombatAttackRules.NpcUnarmedRightAttackInfoWeaponInstance,
                0,
                0,
                0,
                0,
                0,
                0);
            string failure;
            return CapturedEnemyCombatRuntime.Prepare(mob, controller, contract, out failure);
        }

        public static ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            if (npc == null || npc.Playfield == null)
            {
                return null;
            }

            lock (Gate)
            {
                if (!AggressiveMobs.Contains(npc.Identity.Instance))
                {
                    return null;
                }
            }

            if (npc.FightingTarget.Instance != 0 || npc.Stats[StatIds.health].Value <= 0)
            {
                return null;
            }

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield == null)
            {
                return null;
            }

            Coordinate npcPos = npc.Coordinates();
            ICharacter nearest = null;
            double nearestDist = AggroRadius;
            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, AggroRadius);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is PlayerController)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                double dist = candidate.Coordinates().coordinate.Distance2D(npcPos.coordinate);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
