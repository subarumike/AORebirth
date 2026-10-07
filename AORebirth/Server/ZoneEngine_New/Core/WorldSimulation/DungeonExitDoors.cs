namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AODB.Common.RDBObjects;

    using AORebirth.Core.GameData;

    using IdentityType = SmokeLounge.AOtomation.Messaging.GameData.IdentityType;

    /// <summary>
    /// A dungeon's exits as the client finds them (Door_t::LinkDoorToRooms, Gamecode.dll 0x1007fea8): each door is
    /// matched to a room door connection with n3Room_t::GetDoorLinkFromPos (N3.dll 0x100105f9), and a door whose
    /// connection leads to no room (zone link -1) is registered as the playfield's entrance door. Doors between two
    /// rooms are internal links, whichever entrance lands on them.
    /// </summary>
    public static class DungeonExitDoors
    {
        /// <summary>GetDoorLinkFromPos matches a door within this many meters of a connection, on X and on Z.</summary>
        const float MatchTolerance = 1.2f;

        /// <summary>
        /// Door instances whose connection opens onto nothing, in Dynels.dat order: the order the client registers its
        /// entrance doors, and n3Playfield_t::GetEntranceDoor (N3.dll 0x1000ca9d) falls back to the first. Null when
        /// the rooms carry no such connection or no door sits on one, so the caller keeps its own exits rather than
        /// stranding the playfield without any.
        /// </summary>
        public static int[]? Find(PlayfieldRoomsData? rooms, PlayfieldDynels? dynels, float tileSize)
        {
            if (rooms?.Rooms == null || dynels?.Dynels == null || tileSize <= 0f)
                return null;

            var openings = new List<(float X, float Z)>();
            foreach (PlayfieldRoomEntry room in rooms.Rooms)
            {
                if (room?.DoorConnections == null)
                    continue;

                foreach (PlayfieldRoomDoorLink link in room.DoorConnections)
                {
                    if (link != null && link.ZoneLink == -1
                        && TryConnectionPosition(room, link.PosRot, tileSize, out float x, out float z))
                        openings.Add((x, z));
                }
            }

            var exits = new List<int>();
            foreach (PlayfieldDynel door in dynels.Dynels)
            {
                if (door.IdentityType != (int)IdentityType.Door || exits.Contains(door.IdentityInstance))
                    continue;

                foreach ((float x, float z) in openings)
                {
                    if (MathF.Abs(door.Position.X - x) < MatchTolerance && MathF.Abs(door.Position.Z - z) < MatchTolerance)
                    {
                        exits.Add(door.IdentityInstance);
                        break;
                    }
                }
            }

            return exits.Count > 0 ? [.. exits] : null;
        }

        /// <summary>
        /// The door an arrival lands on. A door that is one of the exits keeps it; any other door (an internal room link)
        /// gives way to the first exit, as GetEntranceDoor does for a door number it does not know.
        /// </summary>
        public static int LandingDoor(int targetDoor, IReadOnlyCollection<int>? exits)
        {
            if (exits == null || exits.Count == 0 || exits.Contains(targetDoor))
                return targetDoor;

            foreach (int exit in exits)
                return exit;
            return targetDoor;
        }

        /// <summary>
        /// World X/Z of one door connection. PosRot packs the wall side in its low two bits and the room tile index
        /// above them (row-major, the room's tile width per row); the point sits 0.99 m toward that side, relative to
        /// the room center, turned by Rotation quarter turns about +Y and placed at the room's position.
        /// </summary>
        static bool TryConnectionPosition(PlayfieldRoomEntry room, int posRot, float tileSize, out float x, out float z)
        {
            x = 0f;
            z = 0f;
            int width = room.TileX2 - room.TileX1;
            if (width <= 0 || room.Center == null || room.Center.Length < 3
                || room.Template == null || room.Template.Length < 3)
                return false;

            int tile = (posRot & 0xFFFF) >> 2;
            float localX = tile % width * tileSize - room.Center[0] * tileSize;
            float localZ = tile / width * tileSize - room.Center[2] * tileSize;
            switch (posRot & 3)
            {
                case 0: localZ += 0.99f; break;
                case 1: localX += 0.99f; break;
                case 2: localZ -= 0.99f; break;
                case 3: localX -= 0.99f; break;
            }

            double angle = room.Rotation * Math.PI / 2;
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            x = room.Template[0] + localX * cos + localZ * sin;
            z = room.Template[2] - localX * sin + localZ * cos;
            return true;
        }
    }
}
