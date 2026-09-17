namespace AORebirth.World.Collision
{
    using System;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Core.GameData;

    /// <summary>
    /// Style / template playfields are room catalogs, not playable static dungeons.
    /// Detected when every Rooms.json door connection has <c>ZoneLink == -1</c>.
    /// </summary>
    public static class DungeonPlayfieldKinds
    {
        static readonly JsonSerializerOptions RoomsJsonOptions = new()
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        public static bool IsStyleTemplate(string gameDataRoot, int playfieldId)
        {
            if (string.IsNullOrWhiteSpace(gameDataRoot) || playfieldId <= 0)
                return false;

            if (!TryReadRooms(gameDataRoot, playfieldId, out PlayfieldRoomsData? rooms))
                return false;

            return IsStyleTemplate(rooms);
        }

        public static bool IsStyleTemplate(PlayfieldRoomsData? rooms)
        {
            if (rooms?.Rooms == null || rooms.Rooms.Length == 0)
                return false;

            int links = 0;
            for (int i = 0; i < rooms.Rooms.Length; i++)
            {
                PlayfieldRoomDoorLink[]? doors = rooms.Rooms[i]?.DoorConnections;
                if (doors == null)
                    continue;

                for (int d = 0; d < doors.Length; d++)
                {
                    if (doors[d] == null)
                        continue;

                    links++;
                    if (doors[d].ZoneLink != -1)
                        return false;
                }
            }

            return links > 0;
        }

        static bool TryReadRooms(string gameDataRoot, int playfieldId, out PlayfieldRoomsData? rooms)
        {
            rooms = null;
            string path = Path.Combine(gameDataRoot, GameDataPaths.PlayfieldRoomsRelativePath(playfieldId));
            if (!File.Exists(path))
                return false;

            try
            {
                rooms = JsonSerializer.Deserialize<PlayfieldRoomsData>(File.ReadAllText(path), RoomsJsonOptions);
            }
            catch
            {
                return false;
            }

            return rooms != null;
        }
    }
}
