namespace AORebirth.World.Collision
{
    using System.Numerics;

    /// <summary>
    /// One door of a generated dungeon: where two placed rooms' door connections meet, or the entrance room's
    /// unpaired connection (the exit). Matches all 90 doors in the five captured live ACG dungeons.
    /// </summary>
    public sealed class DungeonDoorPlacement
    {
        public DungeonDoorPlacement(Vector3 position, int yawDegrees, int room, int linkedRoom)
        {
            Position = position;
            YawDegrees = yawDegrees;
            Room = room;
            LinkedRoom = linkedRoom;
        }

        /// <summary>Middle of the connection's tile edge, on the owning room's floor.</summary>
        public Vector3 Position { get; }

        /// <summary>0, 90, 180 or 270: (180 + 90 x ((connection rotation + room facing) mod 4)) mod 360.</summary>
        public int YawDegrees { get; }

        /// <summary>
        /// The generator room index that owns the door (the higher of the two), or -1 for the exit (outside).
        /// </summary>
        public int Room { get; }

        /// <summary>The room it leads to: the lower room index, or 0 (the entrance room) for the exit.</summary>
        public int LinkedRoom { get; }

        public bool IsExit => Room < 0;
    }
}
