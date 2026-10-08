namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;

    /// <summary>Collision meshes plus per-room AABBs for one dungeon (static or ACG instance).</summary>
    public sealed class DungeonWorldLayout
    {
        public DungeonWorldLayout(
            PlayfieldCollisionSet collision,
            IReadOnlyList<DungeonRoomBounds> rooms,
            IReadOnlyList<DungeonDoorPlacement>? doors = null,
            IReadOnlyList<DungeonRoomPlacement>? placements = null,
            IReadOnlyDictionary<int, IReadOnlyList<int>>? roomLinks = null)
        {
            Collision = collision ?? throw new ArgumentNullException(nameof(collision));
            Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            Doors = doors ?? Array.Empty<DungeonDoorPlacement>();
            Placements = placements ?? Array.Empty<DungeonRoomPlacement>();
            RoomLinks = roomLinks ?? LinksFromDoors(Doors);
        }

        /// <summary>
        /// Room index -> the rooms its doors open onto (both directions), in door order. A generated layout takes them
        /// from its doors; a static playfield from its rooms' door ZoneLinks.
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyList<int>> RoomLinks { get; }

        static IReadOnlyDictionary<int, IReadOnlyList<int>> LinksFromDoors(IReadOnlyList<DungeonDoorPlacement> doors)
        {
            var links = new Dictionary<int, List<int>>();
            for (int i = 0; i < doors.Count; i++)
            {
                DungeonDoorPlacement door = doors[i];
                if (door.IsExit || door.Room < 0 || door.LinkedRoom < 0 || door.Room == door.LinkedRoom)
                    continue;
                AddLink(links, door.Room, door.LinkedRoom);
                AddLink(links, door.LinkedRoom, door.Room);
            }

            return Freeze(links);
        }

        /// <summary>Links from each room id to its listed neighbours, made symmetric.</summary>
        public static IReadOnlyDictionary<int, IReadOnlyList<int>> SymmetricLinks(IEnumerable<KeyValuePair<int, IReadOnlyList<int>>> rooms)
        {
            ArgumentNullException.ThrowIfNull(rooms);
            var links = new Dictionary<int, List<int>>();
            foreach (KeyValuePair<int, IReadOnlyList<int>> room in rooms)
            {
                for (int i = 0; i < room.Value.Count; i++)
                {
                    if (room.Value[i] == room.Key)
                        continue;
                    AddLink(links, room.Key, room.Value[i]);
                    AddLink(links, room.Value[i], room.Key);
                }
            }

            return Freeze(links);
        }

        static void AddLink(Dictionary<int, List<int>> links, int from, int to)
        {
            if (!links.TryGetValue(from, out List<int>? list))
                links[from] = list = new List<int>();
            if (!list.Contains(to))
                list.Add(to);
        }

        static IReadOnlyDictionary<int, IReadOnlyList<int>> Freeze(Dictionary<int, List<int>> links)
        {
            var frozen = new Dictionary<int, IReadOnlyList<int>>(links.Count);
            foreach (KeyValuePair<int, List<int>> pair in links)
                frozen[pair.Key] = pair.Value.ToArray();
            return frozen;
        }

        public PlayfieldCollisionSet Collision { get; }

        public IReadOnlyList<DungeonRoomBounds> Rooms { get; }

        /// <summary>Doors of a generated (ACG) layout, exit first; empty for static playfields.</summary>
        public IReadOnlyList<DungeonDoorPlacement> Doors { get; }

        /// <summary>How each room of a generated (ACG) layout was placed, in room order; empty for static playfields.</summary>
        public IReadOnlyList<DungeonRoomPlacement> Placements { get; }
    }
}
