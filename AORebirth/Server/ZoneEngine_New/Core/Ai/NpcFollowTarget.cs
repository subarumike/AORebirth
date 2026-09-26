namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Movement;

    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Builds the FollowTarget packets observers need to see NPC path motion.
    /// Coordinate paths are type 1; combat-range / idle stops use the type-2 position settle.
    /// </summary>
    static class NpcFollowTarget
    {
        public const byte CoordinateInfoType = 1;
        public const byte WalkMoveMode = 24;
        public const byte RunMoveMode = 25;
        public const byte CrawlMoveMode = 27;

        /// <summary>Seconds of travel covered by each planned segment.</summary>
        public const double PathLookaheadSeconds = 1.5;

        /// <summary>
        /// The steer point waits at a turn sharper than this until the body reaches it.
        /// Running the steer point past the turn pulls the body back through the wall.
        /// </summary>
        public const float PathCornerTurnDegrees = 25f;

        /// <summary>The steer point held at a turn is released once the body is this close to it.</summary>
        public const float PathCornerReleaseMeters = 0.5f;

        /// <summary>
        /// Seconds of travel before the next segment replaces the current one. Shorter than the lookahead so
        /// the segment end stays ahead of the NPC and neither server nor client brakes between segments.
        /// </summary>
        public const double PathReplanSeconds = 1;

        /// <summary>A pathing NPC that has not moved <see cref="PathStuckProgressMeters"/> in this long is warped ahead.</summary>
        public const double PathStuckWarpSeconds = 3;

        public const float PathStuckProgressMeters = 1f;

        public const float MinAnnounceDeltaMeters = 2.0f;

        public static void AnnounceCoordinatePath(Character character, Vector3 start, IReadOnlyList<Vector3> waypoints)
        {
            ArgumentNullException.ThrowIfNull(character);
            if (waypoints == null || waypoints.Count == 0)
                return;

            var coordinates = new List<MsgVector3>(waypoints.Count + 1)
            {
                ToMsg(start)
            };
            for (int i = 0; i < waypoints.Count; i++)
                coordinates.Add(ToMsg(waypoints[i]));

            character.Cell?.Announce(
                new FollowTargetMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Info = new FollowCoordinateInfo
                    {
                        FollowInfoType = CoordinateInfoType,
                        MoveMode = ResolveMoveMode(character),
                        CoordinateCount = (byte)coordinates.Count,
                        CurrentCoordinates = coordinates[0],
                        EndCoordinates = coordinates[coordinates.Count - 1],
                        Coordinates = coordinates
                    }
                });
        }

        public static void AnnounceStop(Character character, Vector3 position)
        {
            ArgumentNullException.ThrowIfNull(character);

            character.Cell?.Announce(
                new FollowTargetMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Info = new FollowPositionInfo
                    {
                        FollowInfoType = 2,
                        MoveType = RunMoveMode,
                        Unknown1 = 0,
                        Unknown2 = 0,
                        Unknown3 = unchecked((int)0x40000000),
                        Coordinates = ToMsg(position),
                        Unknown4 = 0
                    }
                });
        }

        static byte ResolveMoveMode(Character character)
        {
            return character.Motor.State switch
            {
                MovementState.Walk => WalkMoveMode,
                MovementState.Crawl => CrawlMoveMode,
                _ => RunMoveMode
            };
        }

        static MsgVector3 ToMsg(Vector3 v) => new((float)v.x, (float)v.y, (float)v.z);
    }
}
