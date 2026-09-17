namespace AORebirth.World.Collision
{
    /// <summary>
    /// Collision-library equivalent of client <c>BuildingRoomInfo_t</c>
    /// (room template index, floor, grid, facing).
    /// </summary>
    public sealed class DungeonRoomSpec
    {
        public ushort RoomId { get; set; }

        public sbyte Floor { get; set; }

        public byte GridX { get; set; }

        public byte GridZ { get; set; }

        /// <summary>Rotation quadrant 0..3 (0/90/180/270).</summary>
        public byte Facing { get; set; }
    }
}
