using System.Collections.Generic;
using LostEden.Vehicles;
using LostEden.Vehicles.Surfaces;
using Xunit;

namespace LostEden.Vehicle.Tests
{
    /// <summary>
    /// The non-terrain collision composite: <c>CellSurface_t</c> (<c>Collision 1000140a</c>),
    /// <c>GridSpace_t::IterateCellsAlongLine</c> (<c>Vehicle 10003d7d</c>) and the per-cell worker
    /// (<c>Collision 1000250f</c>). See Docs/Movement.md §9.
    /// </summary>
    public class CellSurfaceTests
    {
        sealed class Recorder : ICellWorker
        {
            public readonly List<int> Cells = new List<int>();
            public void DoCell(int cellId) => Cells.Add(cellId);
        }

        // ---- the cell walk ---------------------------------------------------

        [Fact]
        public void TheWalkVisitsOneCellForALineInsideIt()
        {
            var r = new Recorder();
            GridSpace.IterateCellsAlongLine(
                new Vec3(15f, 0f, 15f), new Vec3(25f, 0f, 25f), r, 10, 10, 400f, 400f);

            Assert.Equal(new[] { 0 }, r.Cells);
        }

        [Fact]
        public void TheWalkCrossesCellsInOrderAlongTheLine()
        {
            // 40 m cells; walk +X from cell 0 into cell 3.
            var r = new Recorder();
            GridSpace.IterateCellsAlongLine(
                new Vec3(20f, 0f, 20f), new Vec3(140f, 0f, 20f), r, 10, 10, 400f, 400f);

            Assert.Equal(new[] { 0, 1, 2, 3 }, r.Cells);
        }

        [Fact]
        public void TheWalkGoesBackwardsToo()
        {
            var r = new Recorder();
            GridSpace.IterateCellsAlongLine(
                new Vec3(140f, 0f, 20f), new Vec3(20f, 0f, 20f), r, 10, 10, 400f, 400f);

            Assert.Equal(new[] { 3, 2, 1, 0 }, r.Cells);
        }

        [Fact]
        public void TheWalkStepsRowsWithCellsXStride()
        {
            // +Z from cell 0 into the third row: ids 0, 10, 20.
            var r = new Recorder();
            GridSpace.IterateCellsAlongLine(
                new Vec3(20f, 0f, 20f), new Vec3(20f, 0f, 100f), r, 10, 10, 400f, 400f);

            Assert.Equal(new[] { 0, 10, 20 }, r.Cells);
        }

        [Fact]
        public void AnOutOfRangeCellEndsTheWalkRatherThanBeingSkipped()
        {
            // 10003f0b: out of range RETURNS. Starting outside means nothing is visited at all.
            var r = new Recorder();
            GridSpace.IterateCellsAlongLine(
                new Vec3(-50f, 0f, 20f), new Vec3(140f, 0f, 20f), r, 10, 10, 400f, 400f);

            Assert.Empty(r.Cells);
        }

        [Fact]
        public void TheWalkStopsAtTheGridEdge()
        {
            var r = new Recorder();
            GridSpace.IterateCellsAlongLine(
                new Vec3(340f, 0f, 20f), new Vec3(600f, 0f, 20f), r, 10, 10, 400f, 400f);

            Assert.Equal(new[] { 8, 9 }, r.Cells);
        }

        // ---- the grid --------------------------------------------------------

        static CellSurface Grid() => new CellSurface(10, 10, 400f, 400f);

        [Fact]
        public void CellIdsAreRowMajorAndOutOfRangeIsMinusOne()
        {
            CellSurface g = Grid();
            Assert.Equal(40f, g.CellSizeX);
            Assert.Equal(0, g.GetCellIdFromPos(new Vec3(20f, 0f, 20f)));
            Assert.Equal(1, g.GetCellIdFromPos(new Vec3(60f, 0f, 20f)));
            Assert.Equal(10, g.GetCellIdFromPos(new Vec3(20f, 0f, 60f)));
            Assert.Equal(99, g.GetCellIdFromPos(new Vec3(399f, 0f, 399f)));
            Assert.Equal(-1, g.GetCellIdFromPos(new Vec3(401f, 0f, 20f)));
            Assert.Equal(-1, g.GetCellIdFromPos(new Vec3(-41f, 0f, 20f)));

            // Stock's ftol TRUNCATES toward zero rather than flooring, so the whole strip from -40 to 0
            // maps to column 0 instead of being rejected (Collision 1000136f). Preserved deliberately.
            Assert.Equal(0, g.GetCellIdFromPos(new Vec3(-1f, 0f, 20f)));
        }

        /// <summary>
        /// A flat horizontal quad, for putting known geometry in a cell. Wound like record 1000013's
        /// floors, so it is solid from above: the mesh test is one-sided (see TriangleMeshSurface).
        /// </summary>
        static TriangleMeshSurface Slab(float minX, float minZ, float size, float y)
        {
            var v = new[]
            {
                new Vec3(minX, y, minZ),
                new Vec3(minX + size, y, minZ),
                new Vec3(minX + size, y, minZ + size),
                new Vec3(minX, y, minZ + size),
            };
            return new TriangleMeshSurface(v, new[] { 0, 2, 1, 0, 3, 2 });
        }

        [Fact]
        public void ARayFindsGeometryInACellItCrosses()
        {
            CellSurface g = Grid();
            g.SetSurfaceForCell(0, Slab(0f, 0f, 40f, 5f));

            Assert.True(g.GetLineIntersection(
                new Vec3(20f, 20f, 20f), new Vec3(20f, -20f, 20f), out Vec3 hit, out Vec3 n, true, null));
            Assert.Equal(5f, hit.Y, 3);
            Assert.Equal(1f, n.Y, 3);
        }

        [Fact]
        public void AHitOutsideTheCellIsRejectedSoTheRightCellReportsIt()
        {
            // The same slab is registered in cell 0 and cell 1, as stock's duplication would do. A ray
            // down through cell 1 must be answered by cell 1, not by cell 0's copy.
            CellSurface g = Grid();
            TriangleMeshSurface wide = Slab(0f, 0f, 80f, 5f);
            g.SetSurfaceForCell(0, wide);
            g.SetSurfaceForCell(1, wide);

            Assert.True(g.GetLineIntersection(
                new Vec3(60f, 20f, 20f), new Vec3(60f, -20f, 20f), out Vec3 hit, out _, true, null));
            Assert.Equal(60f, hit.X, 3);
        }

        [Fact]
        public void GeometryOnlyInAnotherCellIsNotReported()
        {
            CellSurface g = Grid();
            g.SetSurfaceForCell(0, Slab(0f, 0f, 80f, 5f));      // spans cells 0 and 1, listed in 0 only

            // Straight down inside cell 1: the slab's hit is outside cell 0's bounds, and cell 1 is
            // empty, so nothing is found. This is stock's behaviour and why geometry is duplicated.
            Assert.False(g.GetLineIntersection(
                new Vec3(60f, 20f, 20f), new Vec3(60f, -20f, 20f), out _, out _, true, null));
        }

        [Fact]
        public void TheNearestOfSeveralSurfacesInOneCellWins()
        {
            CellSurface g = Grid();
            g.SetSurfaceForCell(0, Slab(0f, 0f, 40f, 1f));
            g.SetSurfaceForCell(0, Slab(0f, 0f, 40f, 9f));      // nearer to a ray coming down

            Assert.True(g.GetLineIntersection(
                new Vec3(20f, 20f, 20f), new Vec3(20f, -20f, 20f), out Vec3 hit, out _, true, null));
            Assert.Equal(9f, hit.Y, 3);
        }

        [Fact]
        public void TheWalkStopsAtTheFirstCellWithAHit()
        {
            // Cell 1 has nearer geometry than cell 2 along a +X ray, so the walk must stop at 1.
            CellSurface g = Grid();
            g.SetSurfaceForCell(1, Wall(60f));
            g.SetSurfaceForCell(2, Wall(100f));

            Assert.True(g.GetLineIntersection(
                new Vec3(20f, 5f, 20f), new Vec3(140f, 5f, 20f), out Vec3 hit, out _, true, null));
            Assert.Equal(60f, hit.X, 3);
        }

        /// <summary>A vertical quad facing -X at the given X.</summary>
        static TriangleMeshSurface Wall(float x)
        {
            var v = new[]
            {
                new Vec3(x, 0f, 0f),
                new Vec3(x, 0f, 400f),
                new Vec3(x, 20f, 400f),
                new Vec3(x, 20f, 0f),
            };
            return new TriangleMeshSurface(v, new[] { 0, 1, 2, 0, 2, 3 });
        }

        [Fact]
        public void AnEmptyGridReportsNothing()
        {
            Assert.False(Grid().GetLineIntersection(
                new Vec3(20f, 20f, 20f), new Vec3(20f, -20f, 20f), out _, out _, true, null));
        }

        // ---- the closest point ----------------------------------------------

        [Fact]
        public void TheClosestPointComesFromTheCellAtThePosition()
        {
            CellSurface g = Grid();
            g.SetSurfaceForCell(0, Slab(0f, 0f, 40f, 5f));

            g.CalculateClosestPoint(new Vec3(20f, 6f, 20f), out Vec3 c, out Vec3 n, null);

            Assert.Equal(5f, c.Y, 3);
            Assert.Equal(1f, n.Y, 3);
        }

        [Fact]
        public void AnEmptyCellAnswersWithTheNoClosestPointSentinel()
        {
            // Not the position itself: returning that makes the nested surface beat the terrain from
            // any height, and the ground clamp stops pulling the body down. Stock seeds -9999 for
            // exactly this reason (10018fb7).
            var p = new Vec3(20f, 6f, 20f);
            Grid().CalculateClosestPoint(p, out Vec3 c, out _, null);
            Assert.Equal(TilemapSurface.NoClosestPoint, c.Y, 3);
        }

        // ---- the mesh surface ------------------------------------------------

        [Fact]
        public void AMeshNormalFacesTheCaller()
        {
            TriangleMeshSurface wall = Wall(10f);

            Assert.True(wall.GetLineIntersection(
                new Vec3(0f, 5f, 200f), new Vec3(20f, 5f, 200f), out _, out Vec3 n, true, null));
            Assert.Equal(-1f, n.X, 3);      // walking +X into it, so the normal points back at -X
        }

        [Fact]
        public void AMeshIsSolidFromOneSideOnly()
        {
            // Stock lets a character placed inside static geometry walk back out, so the back of a
            // mesh face does not collide. Wall faces -X: a ray from +X passes through it.
            TriangleMeshSurface wall = Wall(10f);

            Assert.False(wall.GetLineIntersection(
                new Vec3(20f, 5f, 200f), new Vec3(0f, 5f, 200f), out _, out _, true, null));
        }

        [Fact]
        public void AMeshIgnoresAHitBeyondTheSegment()
        {
            TriangleMeshSurface wall = Wall(50f);

            // The segment stops short of the wall.
            Assert.False(wall.GetLineIntersection(
                new Vec3(0f, 5f, 200f), new Vec3(40f, 5f, 200f), out _, out _, true, null));
        }

        [Fact]
        public void TheFloorQueryTakesTheHighestSurfaceAtOrBelowThePoint()
        {
            // Two decks; a body at 11 stands on the one at 10, not the one at 20 above it.
            var deck = new List<Vec3>();
            var idx = new List<int>();
            foreach (float y in new[] { 10f, 20f })
            {
                int b = deck.Count;
                deck.Add(new Vec3(0f, y, 0f));
                deck.Add(new Vec3(40f, y, 0f));
                deck.Add(new Vec3(40f, y, 40f));
                deck.Add(new Vec3(0f, y, 40f));
                idx.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }

            var mesh = new TriangleMeshSurface(deck.ToArray(), idx.ToArray());
            mesh.CalculateClosestPoint(new Vec3(20f, 11f, 20f), out Vec3 c, out _, null);

            Assert.Equal(10f, c.Y, 3);
        }

        [Fact]
        public void TheFloorQueryDoesNotLiftABodyStandingBesideAWall()
        {
            // The reason CalculateClosestPoint is a downward query and not a true 3D closest point: the
            // ground clamp raises the body to closest.Y + stepHeight, so a lateral answer would make it
            // climb. A body next to a wall must get its own height back, unchanged.
            TriangleMeshSurface wall = Wall(10f);
            var p = new Vec3(9.9f, 5f, 200f);

            wall.CalculateClosestPoint(p, out Vec3 c, out _, null);

            Assert.Equal(TilemapSurface.NoClosestPoint, c.Y, 3);
        }

        [Fact]
        public void TheTerrainIsKeptWhereTheCellsHaveNoFloor()
        {
            // The bug real-terrain probing caught: with the nested surface answering "the point you gave
            // me", every probe above the ground looked like a statel and the terrain never won. Over a
            // cell with no geometry the composite must return the terrain height.
            TilemapSurface terrain = TerrainWithCells(out CellSurface cells);
            cells.SetSurfaceForCell(0, Slab(0f, 0f, 40f, 8f));      // geometry far away, not here

            terrain.CalculateClosestPoint(new Vec3(600f, 50f, 600f), out Vec3 c, out _, null);

            Assert.Equal(0f, c.Y, 3);
        }

        [Fact]
        public void AStatelFloorWinsOverTheTerrainBelowIt()
        {
            TilemapSurface terrain = TerrainWithCells(out CellSurface cells);
            int cellId = cells.GetCellIdFromPos(new Vec3(100f, 0f, 100f));
            cells.SetSurfaceForCell(cellId, Slab(80f, 80f, 40f, 8f));

            terrain.CalculateClosestPoint(new Vec3(100f, 50f, 100f), out Vec3 c, out _, null);

            Assert.Equal(8f, c.Y, 3);
        }

        // ---- the walker, end to end -----------------------------------------
        //
        // The point of the whole chain: a character must stand on statel geometry and be stopped by it.

        sealed class Flat : ITileHeightSource
        {
            public int Width => 256;
            public int Height => 256;
            public float TileSize => 4f;
            public float SampleHeight(int x, int z) => 0f;
        }

        static CharVehicleSim Walker(ISurface surface, Vec3 at)
        {
            var sim = new CharVehicleSim
            {
                Mass = 50f, MaxForce = 10f, MaxVel = 1f, NearProbeOffset = 0.5f,
                SlowingDistance = 1.5f, MovementState = 3, RunSpeedStat = 275f,
                Surface = surface, Position = at,
            };
            sim.EnableFalling();
            sim.DisableSurfaceHug();
            sim.UseSurfaceNormal();
            sim.UpdateMotionConstraints();
            return sim;
        }

        static TilemapSurface TerrainWithCells(out CellSurface cells)
        {
            var terrain = new TilemapSurface(new Flat());
            cells = new CellSurface(25, 25, 1024f, 1024f);   // 256 tiles / 10 -> 40.96 m cells
            terrain.Child = cells;
            return terrain;
        }

        [Fact]
        public void ACharacterStandsOnAStatelDeckInsteadOfFallingThroughIt()
        {
            TilemapSurface terrain = TerrainWithCells(out CellSurface cells);
            int cellId = cells.GetCellIdFromPos(new Vec3(100f, 0f, 100f));
            cells.SetSurfaceForCell(cellId, Slab(80f, 80f, 40f, 8f));

            CharVehicleSim sim = Walker(terrain, new Vec3(100f, 12f, 100f));
            for (int i = 0; i < 240; i++)
                sim.Run(1f / 60f);

            Assert.InRange(sim.Position.Y, 7.9f, 8.3f);
            Assert.False(sim.Airborne);
        }

        [Fact]
        public void WithoutTheCellSurfaceTheSameCharacterFallsToTheTerrain()
        {
            // The behaviour before this was ported, kept so a regression is obvious.
            var terrain = new TilemapSurface(new Flat());

            CharVehicleSim sim = Walker(terrain, new Vec3(100f, 12f, 100f));
            for (int i = 0; i < 240; i++)
                sim.Run(1f / 60f);

            Assert.InRange(sim.Position.Y, -0.1f, 0.2f);
        }

        [Fact]
        public void ACharacterCannotWalkThroughAStatelWall()
        {
            TilemapSurface terrain = TerrainWithCells(out CellSurface cells);

            // A wall across +X at 110, registered in every cell it touches, as stock duplicates it.
            TriangleMeshSurface wall = Wall(110f);
            for (int cz = 0; cz < 25; cz++)
                cells.SetSurfaceForCell(cells.GetCellIdFromPos(new Vec3(110f, 0f, cz * 40.96f + 20f)), wall);

            CharVehicleSim sim = Walker(terrain, new Vec3(100f, 0.01f, 100f));
            sim.BodyRotation = Quat.FromAxisAngle(Vec3.ReferenceUp, (float)(System.Math.PI / 2.0));
            sim.SetDirection(1);
            sim.SetForwardDrive(1f);

            for (int i = 0; i < 300; i++)
                sim.Run(1f / 60f);

            // It must stop short of the wall rather than pass through it.
            Assert.True(sim.Position.X < 110f, $"walked through the wall to x={sim.Position.X}");
            Assert.True(sim.Position.X > 104f, $"stopped far too early at x={sim.Position.X}");
        }

        [Fact]
        public void WithoutTheCellSurfaceTheSameCharacterWalksStraightThrough()
        {
            var terrain = new TilemapSurface(new Flat());

            CharVehicleSim sim = Walker(terrain, new Vec3(100f, 0.01f, 100f));
            sim.BodyRotation = Quat.FromAxisAngle(Vec3.ReferenceUp, (float)(System.Math.PI / 2.0));
            sim.SetDirection(1);
            sim.SetForwardDrive(1f);

            for (int i = 0; i < 300; i++)
                sim.Run(1f / 60f);

            Assert.True(sim.Position.X > 110f, $"expected to pass x=110, reached {sim.Position.X}");
        }
    }
}
