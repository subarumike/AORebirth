namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    /// Picks a dungeon room cell. Nested rooms: smallest containing AABB wins; otherwise nearest.
    /// </summary>
    public static class DungeonRoomCellResolver
    {
        public static int Resolve(IReadOnlyList<DungeonRoomBounds> rooms, Vector3 position)
        {
            ArgumentNullException.ThrowIfNull(rooms);
            if (rooms.Count == 0)
                throw new ArgumentException("Dungeon room list is empty.", nameof(rooms));

            int bestContain = -1;
            float bestVolume = float.MaxValue;
            int bestNear = rooms[0].Index;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < rooms.Count; i++)
            {
                DungeonRoomBounds room = rooms[i];
                float distance = room.DistanceSquared(position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestNear = room.Index;
                }

                if (!room.Contains(position))
                    continue;
                if (room.Volume >= bestVolume)
                    continue;

                bestVolume = room.Volume;
                bestContain = room.Index;
            }

            return bestContain >= 0 ? bestContain : bestNear;
        }
    }
}
