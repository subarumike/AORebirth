namespace ZoneEngine_New.Core.GameData
{
    using System.Collections.Generic;

    using AODB.Common.RDBObjects;

    /// <summary>
    /// Parsed Walls.dat / Dynels.dat / Doors.dat / Collision.dat / Surfaces.dat for one playfield.
    /// Missing files yield null or empty members.
    /// </summary>
    public sealed class PlayfieldGeometryData
    {
        public PlayfieldWalls? Walls { get; init; }

        public PlayfieldDynels? Dynels { get; init; }

        public PlayfieldDoors? Doors { get; init; }

        public Tilemap? Tilemap { get; init; }

        public SurfaceResource? Surface { get; init; }

        /// <summary>Per-locality-cell static geometry, which is how outdoor playfields store it.</summary>
        public IReadOnlyList<SurfaceResource> CellSurfaces { get; init; } = [];

        public bool HasAny =>
            Walls != null
            || Dynels != null
            || Doors != null
            || Tilemap != null
            || Surface != null
            || CellSurfaces.Count > 0;
    }
}
