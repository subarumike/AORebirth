using System;

namespace LostEden.Vehicles.Surfaces
{
    /// <summary>
    /// <c>CellWorker_i</c> (<c>Vehicle.dll</c>, vftable <c>10012414</c>) — a two-slot interface whose
    /// only real member is the per-cell callback in slot 0. See Docs/Movement.md §8.
    /// </summary>
    public interface ICellWorker
    {
        /// <summary>
        /// Slot 0. Called once per cell the line crosses, <b>in order along the line</b>.
        /// </summary>
        void DoCell(int cellId);
    }

    /// <summary>
    /// <c>GridSpace_t</c> (<c>Vehicle.dll</c>) — the 2D cell traversal the collision grid is walked
    /// with. See Docs/Movement.md §8.8.
    /// </summary>
    public static class GridSpace
    {
        /// <summary>
        /// The tDelta scale, <c>100124d8</c> / <c>100124d0</c> (+/-100.0 as doubles). It is the
        /// <b>same</b> on both axes, so it cannot change which axis steps first — it is kept only
        /// because stock has it.
        /// </summary>
        public const float TScale = 100f;

        /// <summary>
        /// <c>GridSpace_t::IterateCellsAlongLine</c> (<c>Vehicle 10003d7d</c>) — an Amanatides–Woo
        /// walk over the cell grid, calling <paramref name="worker"/> once per cell in order.
        ///
        /// <para>
        /// Three details that are easy to get wrong and are all load-bearing:
        /// <b>worldDepth is never read</b> (stock's <c>[ebp+0x20]</c> is unreferenced) — a single cell
        /// size <c>worldWidth / cellsX</c> is used for both axes, which is why the grid has to be
        /// square; the cell indices come from <b>truncation</b> toward zero, not floor; and a cell
        /// outside the grid <b>ends the walk</b> rather than being skipped.
        /// </para>
        /// </summary>
        public static void IterateCellsAlongLine(
            Vec3 a, Vec3 b, ICellWorker worker, int cellsX, int cellsZ, float worldWidth, float worldDepth)
        {
            if (worker == null || cellsX <= 0 || cellsZ <= 0)
                return;

            float cellSize = worldWidth / cellsX;               // 10003d89, and worldDepth is unused
            if (cellSize <= 0f)
                return;

            float ax = a.X / cellSize, az = a.Z / cellSize;     // 10003d92..10003daa
            float bx = b.X / cellSize, bz = b.Z / cellSize;

            int cellX = (int)ax, cellZ = (int)az;               // 10010a50, a truncating float->int
            int endX = (int)bx, endZ = (int)bz;

            Step(ax, bx, out int stepX, out float tDeltaX, out float tMaxX);
            Step(az, bz, out int stepZ, out float tDeltaZ, out float tMaxZ);

            // 10003ee2: the budget is the Manhattan cell distance, and the loop runs budget + 1 times.
            int budget = Math.Abs(endX - cellX) + Math.Abs(endZ - cellZ);
            if (budget < 0)
                return;

            int row = cellZ * cellsX;                           // 10003f03
            int rowStep = stepZ * cellsX;                        // 10003f07

            do
            {
                // 10003f0b..10003f21: out of range RETURNS -- it does not continue to the next cell.
                if (cellX < 0 || cellZ < 0 || cellX >= cellsX || cellZ >= cellsZ)
                    return;

                worker.DoCell(row + cellX);                      // 10003f2b

                if (tMaxZ <= tMaxX)                              // 10003f37, step the smaller tMax
                {
                    tMaxZ += tDeltaZ;
                    cellZ += stepZ;
                    row += rowStep;
                }
                else
                {
                    tMaxX += tDeltaX;
                    cellX += stepX;
                }
            }
            while (--budget >= 0);                               // 10003f5e
        }

        /// <summary>
        /// One axis of the DDA setup (<c>10003dff</c>..<c>10003edf</c>). A non-negative delta steps
        /// forward and measures to the next boundary above; a negative one steps back and measures to
        /// the boundary below.
        /// </summary>
        static void Step(float from, float to, out int step, out float tDelta, out float tMax)
        {
            float delta = to - from;

            if (!(0f > delta))                                   // 10003e0c
            {
                step = 1;
                tDelta = TScale / delta;                         // +Infinity when delta is 0
                tMax = ((float)Math.Floor(from) + 1f - from) * tDelta;
            }
            else
            {
                step = -1;
                tDelta = -TScale / delta;
                tMax = (from - (float)Math.Floor(from)) * tDelta;
            }
        }
    }
}
