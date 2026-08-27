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
    /// Capture-backed 5m automatic aggro for Nascence Dungeon 1 mobs.
    /// Wired via NPCRuntimeService FindAutomaticAggroTarget chain.
    /// </summary>
    internal static class NascenceDungeon1MobCombat
    {
        private const float AggroRadiusMeters = 5.0f;

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

        internal static ICharacter FindAutomaticAggroTarget(ICharacter npc)
        {
            if (npc == null || npc.Playfield == null)
            {
                return null;
            }

            if (!NascenceDungeon1Rules.IsDungeonPlayfield(npc.Playfield.Identity.Instance))
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
