using System.Collections.Generic;

namespace LostEden.Vehicles.Surfaces
{
    /// <summary>
    /// <c>CellSurface_t</c> (<c>Collision.dll</c>, every method exported by name) — a flat grid of
    /// cells, each holding the surfaces whose geometry touches it. This is how stock reaches
    /// non-terrain collision from the single <c>Surface_i*</c> a vehicle owns.
    /// See Docs/Movement.md §8.
    ///
    /// <para>
    /// Geometry is <b>duplicated into every cell it touches</b> — a record's XZ span is 51 m median
    /// against a 40 m cell — so the per-cell hit test rejects any hit outside the cell's own bounds.
    /// That is what makes exactly one cell report each hit, and it is why the ray walk can stop at the
    /// first cell that yields one and still have the nearest.
    /// </para>
    /// </summary>
    public sealed class CellSurface : ISurface
    {
        readonly List<ISurface>[] _cells;

        /// <summary>
        /// <c>CellSurface_t(int cellsX, int cellsZ, float worldWidth, float worldDepth)</c>
        /// (<c>Collision 1000185d</c>). Built by <c>n3TilemapSurface_t::Init</c> (<c>N3 1001879d</c>)
        /// as <c>(tileWidth / 10, tileHeight / 10, worldWidth, worldDepth)</c> — see
        /// <see cref="TilesPerCell"/>.
        /// </summary>
        public CellSurface(int cellsX, int cellsZ, float worldWidth, float worldDepth)
        {
            CellsX = cellsX;
            CellsZ = cellsZ;
            WorldWidth = worldWidth;
            WorldDepth = worldDepth;
            CellSizeX = cellsX > 0 ? worldWidth / cellsX : 0f;    // 100018bd, fidiv by the int
            CellSizeZ = cellsZ > 0 ? worldDepth / cellsZ : 0f;    // 100018c6
            _cells = new List<ISurface>[cellsX > 0 && cellsZ > 0 ? cellsX * cellsZ : 0];
        }

        /// <summary>
        /// Tiles per collision cell — <c>n3Playfield_t +0x50</c>, the divisor in
        /// <c>FUN_1000c50d</c>/<c>FUN_1000c520</c>. It is <b>10</b>: the tightest divisor that keeps
        /// every observed cell index inside the grid across all 328 outdoor playfields, and the only
        /// one that makes the cells exactly square — which stock requires, because
        /// <see cref="GridSpace.IterateCellsAlongLine"/> uses one cell size for both axes. For a
        /// scale-4 map that is a 40 m cell. See Docs/Movement.md §8.5.
        /// </summary>
        public const int TilesPerCell = 10;

        public int CellsX { get; }
        public int CellsZ { get; }
        public float WorldWidth { get; }
        public float WorldDepth { get; }
        public float CellSizeX { get; }

        /// <summary>
        /// <c>GetCellSizeZ</c> (<c>+0x28</c>). Only <see cref="GetCellIdFromPos"/> uses it — the ray
        /// walk and the per-cell bounds test both use <see cref="CellSizeX"/> on both axes
        /// (<c>1000259a</c>, <c>100025cc</c>). They agree only because the grid is square; that
        /// inconsistency is stock's and is deliberately preserved.
        /// </summary>
        public float CellSizeZ { get; }

        public int CellCount => _cells.Length;

        /// <summary><c>GetCellIdFromPos</c> (<c>Collision 1000135f</c>). -1 when outside the grid.</summary>
        public int GetCellIdFromPos(Vec3 p)
        {
            if (CellSizeX <= 0f || CellSizeZ <= 0f)
                return -1;

            int cx = (int)(p.X / CellSizeX);                      // ftol -- truncates toward zero
            int cz = (int)(p.Z / CellSizeZ);

            if (cx < 0 || cx >= CellsX || cz < 0 || cz >= CellsZ)
                return -1;

            return CellsX * cz + cx;
        }

        /// <summary><c>GetSurfaceForCell</c> (slot 9). Null when the cell is empty.</summary>
        public List<ISurface> GetSurfaceForCell(int cellId)
            => cellId >= 0 && cellId < _cells.Length ? _cells[cellId] : null;

        /// <summary><c>GetSurfaceForPos</c> (slot 10).</summary>
        public List<ISurface> GetSurfaceForPos(Vec3 p) => GetSurfaceForCell(GetCellIdFromPos(p));

        /// <summary><c>SetSurfaceForCell</c> (<c>Collision 10001970</c>).</summary>
        public void SetSurfaceForCell(int cellId, ISurface surface)
        {
            if (surface == null || cellId < 0 || cellId >= _cells.Length)
                return;

            (_cells[cellId] ??= new List<ISurface>()).Add(surface);
        }

        /// <summary><c>RemoveSurfaceForCell</c> (<c>Collision 10001a1e</c>).</summary>
        public void RemoveSurfaceForCell(int cellId, ISurface surface)
            => GetSurfaceForCell(cellId)?.Remove(surface);

        // ---- slot 4 ---------------------------------------------------------

        /// <summary>
        /// <c>GetLineIntersection</c> (<c>Collision 1000140a</c>) — walk the cells the line crosses and
        /// let <see cref="Worker"/> test each cell's surfaces.
        /// </summary>
        public bool GetLineIntersection(
            Vec3 start, Vec3 end, out Vec3 hit, out Vec3 normal, bool clipToBounds, object locality)
        {
            var worker = new Worker(this, start, end);
            GridSpace.IterateCellsAlongLine(start, end, worker, CellsX, CellsZ, WorldWidth, WorldDepth);

            hit = worker.Hit;
            normal = worker.Normal;
            return worker.HitFound;
        }

        /// <summary>
        /// The per-cell visitor (<c>Collision 1000250f</c>, vtable <c>100161b8</c> slot 0). Keeps the
        /// nearest hit within a cell and, because <see cref="HitFound"/> is never cleared, refuses to
        /// look at any later cell once one has been found.
        /// </summary>
        sealed class Worker : ICellWorker
        {
            readonly CellSurface _self;
            readonly Vec3 _a;
            readonly Vec3 _b;
            float _best;

            public Vec3 Hit;
            public Vec3 Normal;
            public bool HitFound;

            public Worker(CellSurface self, Vec3 a, Vec3 b)
            {
                _self = self;
                _a = a;
                _b = b;
            }

            public void DoCell(int cellId)
            {
                if (HitFound)                                     // 10002518 -- stop at the first cell
                    return;
                if (cellId == -1)                                 // 10002522
                    return;

                List<ISurface> surfaces = _self.GetSurfaceForCell(cellId);
                if (surfaces == null)                             // 1000253d
                    return;

                // 1000258d: the cell's own XZ bounds. Stock uses cellSizeX on BOTH axes.
                float size = _self.CellSizeX;
                int cx = cellId % _self.CellsX;
                int cz = cellId / _self.CellsX;
                float loX = cx * size, hiX = (cx + 1) * size;
                float loZ = cz * size, hiZ = (cz + 1) * size;

                foreach (ISurface s in surfaces)
                {
                    if (s == null)                                // 10002566
                        continue;

                    // 10002582: clip = false, locality = null.
                    if (!s.GetLineIntersection(_a, _b, out Vec3 h, out Vec3 n, false, null))
                        continue;

                    // A hit outside this cell belongs to a cell further along the line.
                    if (loX > h.X || hiX < h.X || loZ > h.Z || hiZ < h.Z)
                        continue;

                    float distance = (h - _a).Length;              // 10001ea3 + 100010ec

                    if (!HitFound)                                 // 100025f8
                    {
                        Hit = h;
                        Normal = n;
                        HitFound = true;
                        _best = distance;
                        continue;
                    }

                    if (_best > distance)                          // 1000264c -- nearer wins
                    {
                        Hit = h;
                        Normal = n;
                        _best = distance;
                    }
                }
            }
        }

        // ---- slot 1 ---------------------------------------------------------

        /// <summary>
        /// <c>CalculateClosestPoint</c> (<c>Collision 10001729</c>). Deliberately cheap: it takes the
        /// cell at <paramref name="point"/> and hands the query to the <b>first</b> non-null surface
        /// there, then returns — stock does not compare distances across a cell's surfaces
        /// (<c>1000175d</c> is followed straight by the epilogue).
        /// </summary>
        public void CalculateClosestPoint(Vec3 point, out Vec3 closest, out Vec3 normal, object locality)
        {
            // The sentinel, not the point -- see TilemapSurface.NoClosestPoint.
            closest = new Vec3(point.X, TilemapSurface.NoClosestPoint, point.Z);
            normal = Vec3.ReferenceUp;

            List<ISurface> surfaces = GetSurfaceForPos(point);
            if (surfaces == null)
                return;

            foreach (ISurface s in surfaces)
            {
                if (s == null)
                    continue;

                s.CalculateClosestPoint(point, out closest, out normal, null);
                return;
            }
        }

        /// <summary>
        /// Slot 8. <c>CellSurface_t::VetoPosition</c> (<c>Collision 1000135a</c>) is a one-instruction
        /// stub returning false, like the tilemap's.
        /// </summary>
        public bool VetoPosition(ref Vec3 position, object locality, Vec3 previous) => false;
    }
}
