namespace ZoneEngine_New.Core.GameData
{
    using AODB.Common.RDBObjects;

    /// <summary>
    /// Parsed Walls.dat / Dynels.dat / Collision.dat for one playfield.
    /// Missing files yield null members.
    /// </summary>
    public sealed class PlayfieldGeometryData
    {
        public PlayfieldWalls? Walls { get; init; }

        public PlayfieldDynels? Dynels { get; init; }

        public Tilemap? Tilemap { get; init; }

        public SurfaceResource? Surface { get; init; }

        public bool HasAny =>
            Walls != null || Dynels != null || Tilemap != null || Surface != null;
    }
}
