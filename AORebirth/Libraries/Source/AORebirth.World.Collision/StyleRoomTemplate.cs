namespace AORebirth.World.Collision
{
    using System;
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
            int templateFacing = 0)
        {
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
                entry.Rotation);
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
