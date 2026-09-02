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

        /// <summary>
        /// Disk radius around each door CFU center that blocks aggro LOS (open/closed ignored).
        /// </summary>
        private const float DoorBlockRadius = 2.5f;

        private static readonly object Gate = new object();

        private static readonly HashSet<int> AggressiveMobs = new HashSet<int>();

        private static readonly object DoorGate = new object();

        private static DoorBlocker[] doorBlockers;

        private struct DoorBlocker
        {
            public float X;
            public float Y;
            public float Z;
        }

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
        /// Same floor + same ACG reveal cell, and no door disk between NPC and player.
        /// Door open/closed does not matter — doors are hard room boundaries for aggro.
        /// </summary>
        internal static bool ShareAggroRoom(ICharacter npc, ICharacter player)
        {
            if (npc == null || player == null)
            {
                return false;
            }

            float nx = (float)npc.Position.x;
            float ny = (float)npc.Position.y;
            float nz = (float)npc.Position.z;
            float px = (float)player.Position.x;
            float py = (float)player.Position.y;
            float pz = (float)player.Position.z;
            if (System.Math.Abs(ny - py) > SameFloorMaxYDelta)
            {
                return false;
            }

            // Fast reject: different 48m reveal cell → not same room.
            if (NascenceDungeon2RevealZones.ResolveZoneKey(nx, nz)
                != NascenceDungeon2RevealZones.ResolveZoneKey(px, pz))
            {
                return false;
            }

            // Same cell can still span adjacent rooms across a door — block if segment hits a door.
            if (IsDoorBetween(nx, ny, nz, px, py, pz))
            {
                return false;
            }

            return true;
        }

        /// <summary>Door centers cached from capture CFU hex (for diagnostics / tests).</summary>
        internal static int CachedDoorBlockerCount
        {
            get
            {
                EnsureDoorBlockers();
                return doorBlockers == null ? 0 : doorBlockers.Length;
            }
        }

        private static void EnsureDoorBlockers()
        {
            if (doorBlockers != null)
            {
                return;
            }

            lock (DoorGate)
            {
                if (doorBlockers != null)
                {
                    return;
                }

                List<DoorBlocker> list = new List<DoorBlocker>();
                string[] hexList = NascenceDungeon2DoorCapture.ZoneInDoorPacketHex;
                for (int i = 0; i < hexList.Length; i++)
                {
                    float x;
                    float y;
                    float z;
                    if (!NascenceDungeon2RevealZones.TryParseWorldPosition(hexList[i], out x, out y, out z))
                    {
                        continue;
                    }

                    list.Add(new DoorBlocker { X = x, Y = y, Z = z });
                }

                doorBlockers = list.ToArray();
            }
        }

        /// <summary>
        /// True when the XZ segment from NPC to player passes within DoorBlockRadius of a
        /// same-floor door center (projection t in (0,1)). Open/closed ignored.
        /// </summary>
        private static bool IsDoorBetween(float nx, float ny, float nz, float px, float py, float pz)
        {
            EnsureDoorBlockers();
            DoorBlocker[] doors = doorBlockers;
            if (doors == null || doors.Length == 0)
            {
                return false;
            }

            float sx = px - nx;
            float sz = pz - nz;
            float lenSq = (sx * sx) + (sz * sz);
            if (lenSq < 1e-4f)
            {
                return false;
            }

            float radiusSq = DoorBlockRadius * DoorBlockRadius;
            for (int i = 0; i < doors.Length; i++)
            {
                DoorBlocker door = doors[i];
                if (System.Math.Abs(door.Y - ny) > SameFloorMaxYDelta
                    || System.Math.Abs(door.Y - py) > SameFloorMaxYDelta)
                {
                    continue;
                }

                float t = (((door.X - nx) * sx) + ((door.Z - nz) * sz)) / lenSq;
                if (t <= 0f || t >= 1f)
                {
                    continue;
                }

                float closestX = nx + (t * sx);
                float closestZ = nz + (t * sz);
                float dx = door.X - closestX;
                float dz = door.Z - closestZ;
                if (((dx * dx) + (dz * dz)) < radiusSq)
                {
                    return true;
                }
            }

            return false;
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

            Coordinate npcPos = npc.CalculatePredictedPosition();
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

                double dist = candidate.CalculatePredictedPosition().coordinate.Distance2D(npcPos.coordinate);
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
