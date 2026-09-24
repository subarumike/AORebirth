using AODB.Common.RDBObjects;
using LostEden.Vehicles.Surfaces;

namespace LostEden.Vehicles
{
    /// <summary>
    /// Builds a playfield's <see cref="CellSurface"/> — the non-terrain collision grid — from AODB
    /// record type <b>1000013</b> (<c>SurfaceResource</c>). This is the port of what
    /// <c>n3TilemapSurface_t::Init</c> (<c>N3 1001879d</c>) plus the per-zone
    /// <c>n3Zone_t::LoadSurface</c> (<c>N3 1001a947</c>) loop does. See Docs/Movement.md §8.
    ///
    /// <para>
    /// The grid starts <b>empty</b>: <c>SurfaceCellLoader</c> streams cells into it by locality and
    /// removes them again, which is what stock does too — <c>n3Zone_t</c> has both <c>LoadSurface</c>
    /// and <c>UnLoadSurface</c>, and <c>CellSurface_t</c> exports <c>SetSurfaceForCell</c> and
    /// <c>RemoveSurfaceForCell</c>.
    /// </para>
    ///
    /// <para>
    /// <b>A zone is a cell, not an object.</b> <c>LoadSurface</c> hands the zone's instance id straight
    /// to <c>SetSurfaceForCell</c> as the cell index, so there is one record per populated cell and the
    /// record id is <c>(playfieldId &lt;&lt; 16) | cellIndex</c>. That means the whole grid can be filled
    /// from the RDB index with no placement list — 99.1% of all 237,877 records resolve this way.
    /// </para>
    /// </summary>
    public static class PlayfieldCellSurface
    {
        /// <summary>The record type holding static collision geometry.</summary>
        public const int SurfaceResourceType = 1000013;

        /// <summary>
        /// Attaches a cell surface to <paramref name="surface"/> for <paramref name="playfieldId"/>.
        /// Silent no-op when the playfield has no collision records — that is a normal case.
        /// </summary>
        public static CellSurface AttachEmptyGrid(TilemapSurface surface, Tilemap tilemap)
        {
            if (surface == null || tilemap == null)
                return null;

            // n3TilemapSurface_t::Init (N3 1001879d): the grid is tiles/10 on each axis over the world
            // extent the playfield reports, which is (int)mapScale * tiles (N3 1000e3e3 / 1000e40c).
            int cellsX = (int)tilemap.MapWidth / CellSurface.TilesPerCell;
            int cellsZ = (int)tilemap.MapHeight / CellSurface.TilesPerCell;
            if (cellsX <= 0 || cellsZ <= 0)
                return null;

            var cell = new CellSurface(
                cellsX, cellsZ,
                (int)tilemap.MapScale * (int)tilemap.MapWidth,
                (int)tilemap.MapScale * (int)tilemap.MapHeight);

            surface.Child = cell;
            return cell;
        }

        /// <summary>
        /// Builds one cell's geometry from a decoded record. The vertices are already in playfield
        /// world coordinates — measured against the tilemap extents on pf 595 and 505 — which is why
        /// <c>n3Zone_t::LoadSurface</c> applies no transform anywhere.
        /// </summary>
        public static TriangleMeshSurface BuildCell(Vec3[] vertices, int[] indices)
            => vertices == null || indices == null || indices.Length < 3
                ? null
                : new TriangleMeshSurface(vertices, indices);

    }
}
