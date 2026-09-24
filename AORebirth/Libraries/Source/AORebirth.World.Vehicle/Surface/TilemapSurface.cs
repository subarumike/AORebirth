using System;

namespace LostEden.Vehicles.Surfaces
{
    /// <summary>
    /// Stock <c>n3TilemapSurface_t</c> (<c>N3.dll</c>, vftable <c>1003c97c</c>) — the outdoor
    /// heightmap as a <see cref="ISurface"/>. See <c>Docs/Movement.md</c> §5.2a and §5.2h.
    ///
    /// <para>
    /// Ported: the tile walk (<c>GetLineIntersection</c> slot 4, <c>10018b72</c>), the per-tile
    /// two-triangle test (<c>Intersect_Tile</c>, <c>1001891d</c>), the corner fetch
    /// (<c>1001769d</c>) and the ray/triangle test (<c>100190f8</c>).
    /// </para>
    ///
    /// <para>
    /// <b>Not ported, and deliberately left as seams:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item>The ray clip against the map bounds (<c>FUN_10018540</c>, unread). Stock replaces the
    ///     ray with the clipped segment before walking, which shifts the segment-containment test at
    ///     the end. The walker's own rays are short and start inside the map, so the clip is a no-op
    ///     for them; a ray from outside the map will behave differently from stock.</item>
    ///   <item>The nested surface at <c>this+0xc</c>, tested before the terrain and merged by
    ///     nearest-to-start. That is where rooms and static meshes hang; see
    ///     <see cref="Child"/>.</item>
    ///   <item>Indoor tiles (<c>10016454</c>). <see cref="Intersect_Tile"/> takes the outdoor branch
    ///     only.</item>
    ///   <item><c>VetoPosition</c> (<c>10018a7c</c>, unread) — see
    ///     <see cref="VetoPosition"/>.</item>
    /// </list>
    /// </summary>
    public sealed class TilemapSurface : ISurface
    {
        /// <summary>
        /// The edge tolerance in the ray/triangle test, <c>100190f8</c>. Stock compares the two edge
        /// dot products against this rather than zero, so a ray passing exactly along a shared edge
        /// is accepted by both triangles instead of falling through the gap.
        /// </summary>
        public const float EdgeEpsilon = -0.0005f;

        /// <summary>The scale in the DDA's <c>tDelta</c>. It cancels against <c>tMax</c>; kept so the arithmetic matches stock's (<c>10018b72</c>).</summary>
        const float DdaScale = 100f;

        /// <summary>
        /// The "no closest point" sentinel, <c>1003d2ac</c> = <b>-9999</b>. Stock writes it into the
        /// candidate's Y at <c>10018fb7</c> <i>before</i> asking the cell surface, so a child that has
        /// nothing to say leaves a value that can never win the height comparison at <c>10019018</c>.
        /// A nested surface with no answer must return this rather than the point it was given —
        /// returning the point makes it beat the terrain from any height, which silently stops the
        /// ground clamp pulling a body down.
        /// </summary>
        public const float NoClosestPoint = -9999f;

        readonly ITileHeightSource _tiles;

        public TilemapSurface(ITileHeightSource tiles)
        {
            _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        }

        /// <summary>
        /// Stock's <c>this+0xc</c>: a surface tested against the same ray <b>before</b> the terrain,
        /// with the nearer of the two hits returned. Rooms, static meshes and houses compose here
        /// rather than being walked separately.
        /// </summary>
        public ISurface Child { get; set; }

        // ---- slot 4: the tile walk -----------------------------------------

        /// <summary>
        /// <c>n3TilemapSurface_t::GetLineIntersection</c> (<c>10018b72</c>) — an Amanatides-Woo DDA
        /// across the tile grid, bounded by the Manhattan tile distance.
        /// </summary>
        public bool GetLineIntersection(
            Vec3 start,
            Vec3 end,
            out Vec3 hit,
            out Vec3 normal,
            bool clipToBounds,
            object locality)
        {
            hit = Vec3.Zero;
            normal = Vec3.Zero;

            // The nested surface goes first, against the same ray.
            bool hitChild = false;
            Vec3 childHit = Vec3.Zero;
            Vec3 childNormal = Vec3.Zero;
            if (Child != null)
                hitChild = Child.GetLineIntersection(start, end, out childHit, out childNormal, clipToBounds, locality);

            float tileSize = _tiles.TileSize;
            if (tileSize <= 0f)
                return FinishMiss(hitChild, childHit, childNormal, out hit, out normal);

            int width = _tiles.Width;
            int height = _tiles.Height;

            // Tile space.
            float x0 = start.X / tileSize;
            float z0 = start.Z / tileSize;
            float x1 = end.X / tileSize;
            float z1 = end.Z / tileSize;

            int ix = (int)Math.Floor(x0);
            int iz = (int)Math.Floor(z0);
            int ixEnd = (int)Math.Floor(x1);
            int izEnd = (int)Math.Floor(z1);

            SetupAxis(x0, x1, out float tMaxX, out float tDeltaX, out int stepX);
            SetupAxis(z0, z1, out float tMaxZ, out float tDeltaZ, out int stepZ);

            float segmentLengthSquared = (end - start).LengthSquared;

            // stock: n = |dx| + |dz|, and the loop runs while n >= 0
            int n = Math.Abs(ixEnd - ix) + Math.Abs(izEnd - iz);

            while (n >= 0)
            {
                if (ix >= 0 && ix < width && iz >= 0 && iz < height
                    && IntersectTile(ix, iz, start, end, out Vec3 tileHit, out Vec3 tileNormal))
                {
                    // Stock accepts the hit only if it lies within the segment, measured as squared
                    // distances from BOTH ends against the segment's own squared length.
                    if ((tileHit - start).LengthSquared <= segmentLengthSquared
                        && (tileHit - end).LengthSquared <= segmentLengthSquared)
                    {
                        if (!hitChild)
                        {
                            hit = tileHit;
                            normal = tileNormal;
                            return true;
                        }

                        // Both hit: the nearer to the ray's start wins, and stock breaks the tie
                        // towards the TILE -- 10018ef4 is `test ah,0x41 / jne`, i.e. it keeps the tile
                        // whenever tileDistance <= childDistance.
                        if ((tileHit - start).Length > (childHit - start).Length)
                        {
                            hit = childHit;
                            normal = childNormal;
                        }
                        else
                        {
                            hit = tileHit;
                            normal = tileNormal;
                        }

                        return true;
                    }
                }

                if (tMaxZ <= tMaxX)
                {
                    iz += stepZ;
                    tMaxZ += tDeltaZ;
                }
                else
                {
                    ix += stepX;
                    tMaxX += tDeltaX;
                }

                n--;
            }

            return FinishMiss(hitChild, childHit, childNormal, out hit, out normal);
        }

        /// <summary>
        /// Stock writes <c>(-1, -1, -1)</c> into the out parameter when nothing is hit
        /// (<c>10018b72</c> line 162), so callers that read it after a false return see that
        /// sentinel rather than stale data.
        /// </summary>
        static bool FinishMiss(bool hitChild, Vec3 childHit, Vec3 childNormal, out Vec3 hit, out Vec3 normal)
        {
            if (hitChild)
            {
                hit = childHit;
                normal = childNormal;
                return true;
            }

            hit = new Vec3(-1f, -1f, -1f);
            normal = Vec3.Zero;
            return false;
        }

        /// <summary>
        /// One axis of the DDA setup. Stock divides <see cref="DdaScale"/> by the tile-space delta
        /// and takes the fractional distance to the next tile boundary in the direction of travel.
        ///
        /// <para>
        /// <b>The predicate is <c>delta &lt; 0</c>, not <c>delta &lt;= 0</c>.</b> Stock compares
        /// <c>0.0</c> against the delta with <c>fcomp</c> and branches on <c>test ah, 0x41 / jp</c>
        /// (<c>10018d25</c>), which takes the negative arm only when the delta is strictly below
        /// zero — a delta of exactly zero goes to the positive arm and gets
        /// <c>tDelta = 100.0 / 0.0 = +Infinity</c>, so that axis never steps.
        /// </para>
        ///
        /// <para>
        /// Getting that boundary wrong is not cosmetic: with <c>&lt;=</c> a zero delta yields
        /// <c>-Infinity</c>, which compares as the smaller <c>tMax</c> forever, so the walk steps
        /// that axis on every iteration and the other axis never advances. A ray running exactly
        /// along one axis then never reaches anything. Caught by
        /// <c>AShallowRayWalksAcrossTilesUntilItMeetsAWall</c>.
        /// </para>
        /// </summary>
        static void SetupAxis(float from, float to, out float tMax, out float tDelta, out int step)
        {
            float delta = to - from;
            float floor = (float)Math.Floor(from);

            if (delta < 0f)
            {
                tDelta = -DdaScale / delta;      // delta is negative, so tDelta is positive
                step = -1;
                tMax = from - floor;
            }
            else
            {
                tDelta = DdaScale / delta;       // +Infinity when delta is zero
                step = 1;
                tMax = floor + 1f - from;
            }

            tMax *= tDelta;
        }

        // ---- Intersect_Tile -------------------------------------------------

        /// <summary>
        /// <c>n3TilemapSurface_t::Intersect_Tile</c> (<c>1001891d</c>) — two triangles per tile,
        /// with a cheap height reject first.
        /// </summary>
        internal bool IntersectTile(int tx, int tz, Vec3 rayStart, Vec3 rayEnd, out Vec3 hit, out Vec3 normal)
        {
            GetTileCorners(tx, tz, out Vec3 c0, out Vec3 c1, out Vec3 c2, out Vec3 c3, out int diagonal);

            // The tile cannot be reached if its highest corner is below both ends of the ray.
            float highest = Math.Max(Math.Max(c0.Y, c1.Y), Math.Max(c2.Y, c3.Y));
            if (highest < rayStart.Y && highest < rayEnd.Y)
            {
                hit = Vec3.Zero;
                normal = Vec3.Zero;
                return false;
            }

            Vec3 direction = rayEnd - rayStart;

            // Vertex order is stock's, taken from the push order at 100189e4 / 10018a0f, and the
            // winding matters: the ray/triangle test is one-sided.
            bool struck;
            if (diagonal == 0)
            {
                // split along c1-c3, the anti-diagonal
                struck = RayTriangle(rayStart, direction, c0, c1, c3, out hit, out normal)
                      || RayTriangle(rayStart, direction, c1, c2, c3, out hit, out normal);
            }
            else
            {
                // split along c0-c2, the main diagonal
                struck = RayTriangle(rayStart, direction, c0, c1, c2, out hit, out normal)
                      || RayTriangle(rayStart, direction, c2, c3, c0, out hit, out normal);
            }

            if (!struck)
                return false;

            // Stock normalises the normal before returning it: Intersect_Tile ends with
            // FUN_10002686(outNormal, 1.0) at 10018a47.
            //
            // This is NOT cosmetic. The raw cross product of two tile edges has a length that grows
            // with the tile's slope -- on a 4:1 face it comes back as (-4, 1, 0), length 4.12. Every
            // consumer then reads a dot product against it as if it were a unit vector. In the swept
            // solver that inflates `d = |dot(back, normal)|` by the same factor, so the push-out
            // distance `pushDistance / d` comes out several times too small, the move is not refused,
            // and the body creeps into the face a little every frame. The ground clamp then settles
            // it onto the terrain at its new position -- which is a climb, at exactly the slope's
            // gradient. That was the "can still walk up illegal slopes" bug.
            float length = normal.Length;
            if (length > 0f)
                normal = normal * (1f / length);

            return true;
        }

        /// <summary>
        /// <c>FUN_1001769d</c> — the four corner positions of a tile and which way it is split.
        ///
        /// <para>
        /// <b>The diagonal is positional, not stored:</b> <c>(~tz ^ tx) &amp; 1</c>, i.e. tiles whose
        /// <c>tx</c> and <c>tz</c> share parity split one way and the rest the other. Getting it
        /// wrong disagrees with the rendered mesh on half of all tiles.
        /// </para>
        ///
        /// <para>
        /// <b>1 splits along c0-c2 (the main diagonal), 0 splits along c1-c3 (the anti-diagonal).</b>
        /// That mapping is from the corner slots in <c>Intersect_Tile</c>'s frame — <c>[ebp-0xc]</c>
        /// is c0, <c>[ebp-0x30]</c> c1, <c>[ebp-0x24]</c> c2, <c>[ebp-0x18]</c> c3 — cross-checked
        /// against <c>FUN_10017800</c>, whose direct height query compares <c>localX</c> against
        /// <c>localZ</c> for diagonal 1 and against <c>tileSize - localZ</c> for diagonal 0.
        /// </para>
        /// </summary>
        internal void GetTileCorners(int tx, int tz, out Vec3 c0, out Vec3 c1, out Vec3 c2, out Vec3 c3, out int diagonal)
        {
            float tileSize = _tiles.TileSize;
            float x0 = tileSize * tx;
            float z0 = tileSize * tz;

            // The far samples clamp to the last row/column, so the map's edge tiles are flat rather
            // than reading past the end.
            int xp = Math.Min(tx + 1, _tiles.Width - 1);
            int zp = Math.Min(tz + 1, _tiles.Height - 1);

            c0 = new Vec3(x0, _tiles.SampleHeight(tx, tz), z0);
            c1 = new Vec3(x0 + tileSize, _tiles.SampleHeight(xp, tz), z0);
            c2 = new Vec3(x0 + tileSize, _tiles.SampleHeight(xp, zp), z0 + tileSize);
            c3 = new Vec3(x0, _tiles.SampleHeight(tx, zp), z0 + tileSize);

            diagonal = (~tz ^ tx) & 1;
        }

        // ---- the direct height query ----------------------------------------

        /// <summary>
        /// <c>FUN_10017800</c> — the interpolated terrain height under a world position, with the
        /// surface normal. This is the query <c>CalculateClosestPoint</c> is built on, and it is far
        /// cheaper than a ray cast because the tile is found by division rather than by walking.
        ///
        /// <para>
        /// It picks its triangle by comparing the position's offset within the tile against the
        /// split, which is a second, independent statement of the diagonal mapping in
        /// <see cref="GetTileCorners"/>: diagonal 1 compares <c>localX</c> against <c>localZ</c>
        /// (the main diagonal), diagonal 0 against <c>tileSize - localZ</c> (the anti-diagonal).
        /// </para>
        ///
        /// <para>
        /// The plane is solved from a reference corner, which is <c>c0</c> except for diagonal 0's
        /// far triangle — stock swaps the reference to <c>c1</c> there (<c>DAT_1005c774 =
        /// DAT_1005c768</c>), because <c>(c1,c2,c3)</c> does not contain <c>c0</c>.
        /// </para>
        ///
        /// <para>
        /// Returns 0 and a zero normal outside the map, as stock does.
        /// </para>
        /// </summary>
        public float GroundHeightAt(Vec3 point, out Vec3 normal)
        {
            normal = Vec3.Zero;

            float tileSize = _tiles.TileSize;
            if (tileSize <= 0f)
                return 0f;

            if (point.X < 0f || tileSize * _tiles.Width <= point.X)
                return 0f;
            if (point.Z < 0f || tileSize * _tiles.Height <= point.Z)
                return 0f;

            int tx = (int)Math.Floor(point.X / tileSize);
            int tz = (int)Math.Floor(point.Z / tileSize);

            GetTileCorners(tx, tz, out Vec3 c0, out Vec3 c1, out Vec3 c2, out Vec3 c3, out int diagonal);

            float localX = point.X - c0.X;
            float localZ = point.Z - c0.Z;

            Vec3 reference;
            Vec3 edgeA;
            Vec3 edgeB;

            if (diagonal == 0)
            {
                if (tileSize - localZ < localX)
                {
                    // the far triangle (c1, c2, c3) — the reference corner moves to c1
                    reference = c1;
                    edgeA = c1 - c3;
                    edgeB = c1 - c2;
                }
                else
                {
                    reference = c0;
                    edgeA = c0 - c3;
                    edgeB = c0 - c1;
                }
            }
            else
            {
                if (localX <= localZ)
                {
                    reference = c0;
                    edgeA = c0 - c3;
                    edgeB = c0 - c2;
                }
                else
                {
                    reference = c0;
                    edgeA = c0 - c2;
                    edgeB = c0 - c1;
                }
            }

            Vec3 n = Vec3.Cross(edgeA, edgeB);
            if (n.Y == 0f)
                return reference.Y;

            float height = reference.Y
                - ((point.X - reference.X) * n.X + (point.Z - reference.Z) * n.Z) / n.Y;

            float length = n.Length;
            normal = length > 0f ? n * (1f / length) : Vec3.Zero;
            return height;
        }

        // ---- ray / triangle --------------------------------------------------

        /// <summary>
        /// <c>FUN_100190f8</c> — a one-sided ray/triangle test.
        ///
        /// <para>
        /// Stock requires <c>dot(direction, cross(e0, e1)) &gt; 0</c>, i.e. the triangle must face
        /// <b>away</b> from the ray, and then returns the <b>negated</b> cross product as the normal —
        /// so the normal handed back always opposes the ray. For a downward probe on upward-facing
        /// terrain that yields the expected upward normal.
        /// </para>
        ///
        /// <para>
        /// The inside test is two edge-cross dot products against <see cref="EdgeEpsilon"/>, not
        /// three; the first cross is reused as the reference for both.
        /// </para>
        /// </summary>
        internal static bool RayTriangle(Vec3 origin, Vec3 direction, Vec3 v0, Vec3 v1, Vec3 v2, out Vec3 hit, out Vec3 normal)
        {
            hit = Vec3.Zero;

            Vec3 e0 = v1 - v0;
            Vec3 e1 = v2 - v0;
            Vec3 n = Vec3.Cross(e0, e1);
            normal = n;

            float denominator = Vec3.Dot(direction, n);
            if (denominator <= 0f)
                return false;

            float t = Vec3.Dot(v0 - origin, n) / denominator;
            hit = origin + direction * t;

            Vec3 reference = Vec3.Cross(e0, hit - v0);

            if (Vec3.Dot(Vec3.Cross(v2 - v1, hit - v1), reference) < EdgeEpsilon)
                return false;

            if (Vec3.Dot(Vec3.Cross(hit - v2, e1), reference) < EdgeEpsilon)
                return false;

            normal = -n;
            return true;
        }

        // ---- slot 8 ----------------------------------------------------------

        /// <summary>
        /// <c>n3TilemapSurface_t::VetoPosition</c> (<c>10018a7c</c>) — <b>not yet recovered</b>.
        ///
        /// <para>
        /// Returning false means "not vetoed", which makes the ground clamp's retry loop a no-op.
        /// That is the honest stand-in: the alternative is to guess what the terrain rejects, and a
        /// wrong veto would push the player around for reasons stock does not have. Read
        /// <c>10018a7c</c> before giving this a body.
        /// </para>
        /// </summary>
        public bool VetoPosition(ref Vec3 position, object locality, Vec3 previous) => false;

        /// <summary>
        /// <c>n3TilemapSurface_t::CalculateClosestPoint</c> (<c>10018f0c</c>, 103 lines).
        ///
        /// <para>
        /// For terrain the whole function reduces to "drop the point onto the ground": both of
        /// stock's branches end with <c>out = point</c> and <c>out.y = groundHeight</c>, where the
        /// height comes from <see cref="GroundHeightAt"/>.
        /// </para>
        ///
        /// <para>
        /// <b>Two branches are not ported.</b> Stock first resolves a cell id
        /// (<c>CellSurface_t::GetCellIdFromPos</c>) and returns without writing anything when it is
        /// -1; then, when the point is <i>above</i> the ground, it asks the surfaces registered in
        /// that cell for a closer point and prefers theirs if it is higher. It finishes by clamping
        /// the result to <c>liquidHeight - 1.2</c>. Cells and liquid are both unported, so this does
        /// the terrain part only and always writes a result.
        /// </para>
        /// </summary>
        public void CalculateClosestPoint(Vec3 point, out Vec3 closest, out Vec3 normal, object locality)
        {
            float groundY = GroundHeightAt(point, out normal);
            closest = new Vec3(point.X, groundY, point.Z);

            // 10018f0c consults the cell surface here too, which is what lets a statel floor hold the
            // body up through the tripod and not just stop a ray.
            //
            // The order in stock: below the ground (10018f5d, `0 > point.y - groundY`) answers with the
            // terrain and returns; at or above it, the cell's surface is asked and then the HIGHER of
            // the two wins (10019018: `groundY >= candidate.y` keeps the terrain).
            if (Child == null || point.Y < groundY)
                return;

            Child.CalculateClosestPoint(point, out Vec3 childClosest, out Vec3 childNormal, locality);

            if (childClosest.Y > groundY)
            {
                closest = childClosest;
                normal = childNormal;
            }
        }
    }
}
