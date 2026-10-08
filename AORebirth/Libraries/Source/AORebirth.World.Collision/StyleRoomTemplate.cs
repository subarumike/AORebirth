namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    using AORebirth.Core.GameData;

    /// <summary>
    /// One room in a style / template playfield: tile rect, template pose, and surface id.
    /// </summary>
    public sealed class StyleRoomTemplate
    {
        public StyleRoomTemplate(
            int instanceId,
            int tileMinX,
            int tileMinZ,
            int tileMaxX,
            int tileMaxZ,
            Vector3 templatePos,
            int templateFacing = 0,
            IReadOnlyList<int>? doorPosRots = null,
            string? name = null,
            IReadOnlyList<int>? linkedRooms = null)
        {
            DoorPosRots = doorPosRots ?? Array.Empty<int>();
            LinkedRooms = linkedRooms ?? Array.Empty<int>();
            Name = name ?? string.Empty;
            if (tileMaxX <= tileMinX || tileMaxZ <= tileMinZ)
                throw new ArgumentOutOfRangeException(nameof(tileMaxX), "Room tile rect is empty.");

            InstanceId = instanceId;
            TileMinX = tileMinX;
            TileMinZ = tileMinZ;
            TileMaxX = tileMaxX;
            TileMaxZ = tileMaxZ;
            TemplatePos = templatePos;
            TemplateFacing = templateFacing & 3;
            LocalOrigin = ComputeLocalOrigin(tileMinX, tileMinZ, tileMaxX, tileMaxZ);
        }

        public int InstanceId { get; }

        public int TileMinX { get; }

        public int TileMinZ { get; }

        public int TileMaxX { get; }

        public int TileMaxZ { get; }

        public Vector3 TemplatePos { get; }

        public int TemplateFacing { get; }

        /// <summary>Client <c>n3Room_t+0x68</c> half-tile origin in tile units.</summary>
        public Vector3 LocalOrigin { get; }

        /// <summary>
        /// The room's possible doorways (Rooms.json doorConnections posRot): (z x NumTilesX + x) x 4 + rotation, where
        /// (x, z) is a tile of the room and rotation names that tile's edge (0 high z, 1 high x, 2 low z, 3 low x).
        /// </summary>
        public IReadOnlyList<int> DoorPosRots { get; }

        /// <summary>
        /// Rooms this room's doors open onto, in door order (a static playfield's door ZoneLink; the client keeps them as
        /// the room's neighbours, N3.dll n3Playfield_t::UpdateRoomSpace). Empty for style templates, which link nothing.
        /// </summary>
        public IReadOnlyList<int> LinkedRooms { get; }

        /// <summary>Room name from the template (e.g. clan_mh7, clanvillage_entrance); empty when unknown.</summary>
        public string Name { get; }

        public int NumTilesX => TileMaxX - TileMinX;

        public int NumTilesZ => TileMaxZ - TileMinZ;

        public bool IsAcgSizeValid => NumTilesX % 5 == 0 && NumTilesZ % 5 == 0;

        public static bool TryFromRoomEntry(PlayfieldRoomEntry entry, out StyleRoomTemplate? room)
        {
            room = null;
            if (entry == null)
                return false;

            float[]? template = entry.Template;
            if (template == null || template.Length < 3)
                return false;
            if (entry.TileX2 <= entry.TileX1 || entry.TileY2 <= entry.TileY1)
                return false;

            room = new StyleRoomTemplate(
                entry.Index,
                entry.TileX1,
                entry.TileY1,
                entry.TileX2,
                entry.TileY2,
                new Vector3(template[0], template[1], template[2]),
                entry.Rotation,
                entry.DoorConnections == null ? null : Array.ConvertAll(entry.DoorConnections, link => link.PosRot),
                entry.Name,
                entry.DoorConnections == null
                    ? null
                    : Array.FindAll(Array.ConvertAll(entry.DoorConnections, link => link?.ZoneLink ?? -1), room => room >= 0 && room != 0xFFFF));
            return true;
        }

        internal static Vector3 ComputeLocalOrigin(int tileMinX, int tileMinZ, int tileMaxX, int tileMaxZ)
        {
            int halfX = ((((tileMaxX - tileMinX) - 1) & ~1) + 1);
            int halfZ = ((((tileMaxZ - tileMinZ) - 1) & ~1) + 1);
            return new Vector3(halfX * 0.5f, 0f, halfZ * 0.5f);
        }
    }
}
