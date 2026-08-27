namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Controllers;

    using Coordinate = AORebirth.Core.Vector.Coordinate;

    #endregion

    /// <summary>
    /// Capture-backed 5m automatic aggro for Nascence Dungeon 2 mobs.
    /// Doors / ACG cells are room boundaries: no aggro or chase across rooms/floors.
    /// Wired via NPCRuntimeService FindAutomaticAggroTarget chain.
    /// </summary>
    internal static class NascenceDungeon2MobCombat
    {
        private const float AggroRadiusMeters = 5.0f;

        private const float SameFloorMaxYDelta = 10.0f;

        private static readonly object Gate = new object();

        private static readonly HashSet<int> AggressiveMobs = new HashSet<int>();

        internal static void RegisterAggressive(Identity identity)
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

        internal static void UnregisterAggressive(Identity identity)
        {
            if (identity.Instance == 0)
            {
                return;
            }

            lock (Gate)
            {
                AggressiveMobs.Remove(identity.Instance);
            }
        }

        internal static bool IsAggressive(Identity identity)
        {
            if (identity.Instance == 0)
            {
                return false;
            }

            lock (Gate)
            {
                return AggressiveMobs.Contains(identity.Instance);
            }
        }

        /// <summary>
        /// Same ACG reveal cell + same floor. Capture: doors separate rooms; vertical
        /// floors must not aggro through the deck.
        /// </summary>
        internal static bool ShareAggroRoom(ICharacter npc, ICharacter player)
        {
            if (npc == null || player == null)
            {
                return false;
            }

            float nx = (float)npc.RawCoordinates.X;
            float ny = (float)npc.RawCoordinates.Y;
            float nz = (float)npc.RawCoordinates.Z;
            float px = (float)player.RawCoordinates.X;
            float py = (float)player.RawCoordinates.Y;
            float pz = (float)player.RawCoordinates.Z;
            if (System.Math.Abs(ny - py) > SameFloorMaxYDelta)
            {
                return false;
            }

            return NascenceDungeon2RevealZones.ResolveZoneKey(nx, nz)
                   == NascenceDungeon2RevealZones.ResolveZoneKey(px, pz);
        }

        /// <summary>
        /// Drop fight when the player leaves the mob's room (door/floor boundary).
        /// </summary>
        internal static bool TryDropCombatOutsideRoom(ICharacter npc, Playfield playfield)
        {
            if (npc == null
                || playfield == null
                || !NascenceDungeon2Rules.IsDungeonPlayfield(playfield.Identity.Instance)
                || npc.FightingTarget.Instance == 0)
            {
                return false;
            }

            lock (Gate)
            {
                if (!AggressiveMobs.Contains(npc.Identity.Instance))
                {
                    return false;
                }
            }

            ICharacter target = playfield.FindByIdentity<ICharacter>(npc.FightingTarget);
            if (target == null || ShareAggroRoom(npc, target))
            {
                return false;
            }

            npc.SetTarget(Identity.None);
            npc.SetFightingTarget(Identity.None);
            NPCController controller = npc.Controller as NPCController;
            if (controller != null)
            {
                controller.StopFollow();
                controller.State = CharacterState.Idle;
            }

            return true;
        }

        internal static ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            if (npc == null || npc.Playfield == null)
            {
                return null;
            }

            if (!NascenceDungeon2Rules.IsDungeonPlayfield(npc.Playfield.Identity.Instance))
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
            double nearestDist = AggroRadiusMeters;
            List<ICharacter> inRange = playfield.FindCharacterInRange(npc, AggroRadiusMeters);
            for (int i = 0; i < inRange.Count; i++)
            {
                ICharacter candidate = inRange[i];
                if (candidate == null
                    || candidate.Identity.Instance == npc.Identity.Instance
                    || !(candidate.Controller is PlayerController)
                    || candidate.Stats[StatIds.health].Value <= 0
                    || !ShareAggroRoom(npc, candidate))
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
