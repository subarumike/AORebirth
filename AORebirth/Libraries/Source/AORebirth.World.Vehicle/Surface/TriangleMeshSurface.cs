using System;

namespace LostEden.Vehicles.Surfaces
{
    /// <summary>
    /// One cell's worth of static collision geometry — a triangle soup in playfield world
    /// coordinates, from AODB record type <b>1000013</b> (<c>SurfaceResource</c>).
    /// See Docs/Movement.md §8.
    ///
    /// <para>
    /// Stock's equivalent is <c>KDTreeSurface_c</c>, which is <b>header-only</b> (RTTI in
    /// Collision.dll, N3.dll and Gamecode.dll, no exported methods, every copy inlined) and was
    /// therefore <b>not read</b>. Its KD-tree is an acceleration structure only: the nearest hit along
    /// a segment is the same whatever structure finds it. Here that structure is a BVH (see
    /// <see cref="GetLineIntersection"/>) — a live cell holds hundreds of triangles (471 average, 1229
    /// max on the login playfield), which made a direct sweep the frame's dominant cost. What is
    /// <i>not</i> merely an optimisation is <see cref="CalculateClosestPoint"/> — see the note there.
    /// </para>
    /// </summary>
    public sealed class TriangleMeshSurface : ISurface
    {
        readonly Vec3[] _vertices;
        readonly int[] _indices;

        // A world AABB, for rejecting a segment before touching any triangle.
        readonly float _minX, _minY, _minZ, _maxX, _maxY, _maxZ;

        // ---- BVH over the triangles (Bikker, "How to build a BVH", parts 1-3) ----
        //
        // Nodes are flat arrays: six padded bounds per node, and leftFirst/count packed as in the
        // 32-byte node — count == 0 means an interior node whose children are leftFirst and
        // leftFirst + 1; count > 0 means a leaf over _bvhTris[leftFirst .. leftFirst + count).
        // _bvhTris holds each triangle's three vertices in leaf order, _bvhTriId its original index,
        // which breaks ties so the answer is the one the in-order sweep would have given.

        /// <summary>
        /// Slack on every node box. <see cref="TilemapSurface.EdgeEpsilon"/> is negative, so
        /// <c>RayTriangle</c> accepts hits a little outside the triangle; the box must not cull them.
        /// </summary>
        const float BoundsPad = 0.05f;

        const int LeafSize = 4;
        const int SahBins = 12;
        const int MaxDepth = 64;

        float[] _nodeBounds;
        int[] _nodeLeftFirst;
        int[] _nodeCount;
        int _nodesUsed;
        Vec3[] _bvhTris;
        int[] _bvhTriId;

        /// <summary>
        /// <paramref name="indices"/> is triples into <paramref name="vertices"/>. Both are taken by
        /// reference and must not be mutated afterwards.
        /// </summary>
        public TriangleMeshSurface(Vec3[] vertices, int[] indices)
        {
            _vertices = vertices ?? Array.Empty<Vec3>();
            _indices = indices ?? Array.Empty<int>();

            _minX = _minY = _minZ = float.MaxValue;
            _maxX = _maxY = _maxZ = float.MinValue;
            foreach (Vec3 v in _vertices)
            {
                if (v.X < _minX) _minX = v.X;
                if (v.X > _maxX) _maxX = v.X;
                if (v.Y < _minY) _minY = v.Y;
                if (v.Y > _maxY) _maxY = v.Y;
                if (v.Z < _minZ) _minZ = v.Z;
                if (v.Z > _maxZ) _maxZ = v.Z;
            }

            BuildBvh();
        }

        public int TriangleCount => _indices.Length / 3;

        /// <summary>The world AABB of the geometry, or an inverted box when empty.</summary>
        public void GetBounds(out Vec3 min, out Vec3 max)
        {
            min = new Vec3(_minX, _minY, _minZ);
            max = new Vec3(_maxX, _maxY, _maxZ);
        }

        // ---- slot 4 ---------------------------------------------------------

        /// <summary>
        /// The nearest triangle hit along the segment. Uses
        /// <see cref="TilemapSurface.RayTriangle"/> so statel geometry and terrain are tested with the
        /// same numerics stock's <c>Intersect_Tile</c> uses, which also means the normal it hands back
        /// already faces the caller (it returns <c>-cross(e0, e1)</c> after requiring the ray to run
        /// <i>along</i> that cross product). It is <b>not</b> unit length — stock normalises afterwards,
        /// in <c>Intersect_Tile</c> (<c>10018a47</c>) rather than in the triangle test — so this does.
        ///
        /// <para>
        /// <b>One-sided, with the record's winding reversed.</b> <c>RayTriangle</c> rejects
        /// <c>denominator &lt;= 0</c>, the same test terrain uses. <c>KDTreeSurface_c</c> is unread, so
        /// this rests on observed stock behaviour: a character placed inside static geometry can walk
        /// back out of it, which a two-sided test would not allow. Tested in play: the record's own
        /// order (v0, v1, v2) made walls solid from the inside, so the call passes (v0, v2, v1).
        /// </para>
        /// </summary>
        public bool GetLineIntersection(
            Vec3 start, Vec3 end, out Vec3 hit, out Vec3 normal, bool clipToBounds, object locality)
        {
            hit = Vec3.Zero;
            normal = Vec3.ReferenceUp;

            if (_nodeCount == null || !SegmentHitsBounds(start, end))
                return false;

            Vec3 direction = end - start;
            float length = direction.Length;
            if (length <= 0f)
                return false;

            direction = direction * (1f / length);

            // Per-axis reciprocals for the slab test. An exactly-zero component (every vertical probe)
            // is handled as a containment test instead, so no 0 * inf NaN reaches the comparisons.
            bool zeroX = direction.X == 0f, zeroY = direction.Y == 0f, zeroZ = direction.Z == 0f;
            float invX = zeroX ? 0f : 1f / direction.X;
            float invY = zeroY ? 0f : 1f / direction.Y;
            float invZ = zeroZ ? 0f : 1f / direction.Z;

            bool found = false;
            float best = 0f;
            int bestId = 0;

            // Nearest-child-first traversal with an explicit stack. The far child is re-tested on pop
            // against the shortened segment, so a hit in the near child culls it.
            Span<int> stack = stackalloc int[MaxDepth];
            int sp = 0;
            int node = 0;

            if (!Slab(node, start, invX, invY, invZ, zeroX, zeroY, zeroZ, length, out _))
                return false;

            while (true)
            {
                int count = _nodeCount[node];
                if (count > 0)
                {
                    int first = _nodeLeftFirst[node];
                    for (int k = first; k < first + count; k++)
                    {
                        Vec3 v0 = _bvhTris[k * 3];
                        Vec3 v1 = _bvhTris[k * 3 + 1];
                        Vec3 v2 = _bvhTris[k * 3 + 2];

                        // v2/v1 swapped: record 1000013 winds opposite to terrain tiles.
                        if (!TilemapSurface.RayTriangle(start, direction, v0, v2, v1, out Vec3 h, out Vec3 n))
                            continue;

                        // EdgeEpsilon is an absolute slack sized for 4 m terrain tiles. On a sliver
                        // triangle it widens into a strip of the plane metres long, and a ray through
                        // open air "hits" it -- 60 m out was measured on random geometry. Only hits
                        // within BoundsPad of the triangle itself count.
                        if (!NearTriangle(h, v0, v1, v2))
                            continue;

                        float t = Vec3.Dot(h - start, direction);
                        if (t < 0f || t > length)               // behind the start, or past the end
                            continue;

                        // Nearer wins; an exact tie goes to the lower original index, which is the
                        // triangle an in-order sweep would have kept.
                        int id = _bvhTriId[k];
                        if (found && (t > best || (t == best && id > bestId)))
                            continue;

                        float nl = n.Length;                    // RayTriangle does not normalise
                        hit = h;
                        normal = nl > 0f ? n * (1f / nl) : Vec3.ReferenceUp;
                        best = t;
                        bestId = id;
                        found = true;
                    }
                }
                else
                {
                    float limit = found ? best : length;
                    int a = _nodeLeftFirst[node];
                    int b = a + 1;
                    bool hitA = Slab(a, start, invX, invY, invZ, zeroX, zeroY, zeroZ, limit, out float ta);
                    bool hitB = Slab(b, start, invX, invY, invZ, zeroX, zeroY, zeroZ, limit, out float tb);

                    if (hitA && hitB)
                    {
                        if (tb < ta)
                            (a, b) = (b, a);
                        stack[sp++] = b;
                        node = a;
                        continue;
                    }

                    if (hitA) { node = a; continue; }
                    if (hitB) { node = b; continue; }
                }

                // Pop the next deferred node that still overlaps what is left of the segment.
                bool next = false;
                while (sp > 0)
                {
                    node = stack[--sp];
                    if (Slab(node, start, invX, invY, invZ, zeroX, zeroY, zeroZ, found ? best : length, out _))
                    {
                        next = true;
                        break;
                    }
                }

                if (!next)
                    break;
            }

            return found;
        }

        /// <summary>
        /// Ray/box slab test against node <paramref name="node"/>, over the ray parameter range
        /// [0, <paramref name="limit"/>] (inclusive, so a tie at the current best is still visited).
        /// </summary>
        bool Slab(int node, Vec3 o, float invX, float invY, float invZ,
            bool zeroX, bool zeroY, bool zeroZ, float limit, out float tEnter)
        {
            int i = node * 6;
            float tMin = float.NegativeInfinity;
            float tMax = float.PositiveInfinity;
            tEnter = 0f;

            float lo = _nodeBounds[i], hi = _nodeBounds[i + 3];
            if (zeroX)
            {
                if (o.X < lo || o.X > hi) return false;
            }
            else
            {
                float t1 = (lo - o.X) * invX, t2 = (hi - o.X) * invX;
                if (t1 > t2) { float s = t1; t1 = t2; t2 = s; }
                if (t1 > tMin) tMin = t1;
                if (t2 < tMax) tMax = t2;
            }

            lo = _nodeBounds[i + 1]; hi = _nodeBounds[i + 4];
            if (zeroY)
            {
                if (o.Y < lo || o.Y > hi) return false;
            }
            else
            {
                float t1 = (lo - o.Y) * invY, t2 = (hi - o.Y) * invY;
                if (t1 > t2) { float s = t1; t1 = t2; t2 = s; }
                if (t1 > tMin) tMin = t1;
                if (t2 < tMax) tMax = t2;
            }

            lo = _nodeBounds[i + 2]; hi = _nodeBounds[i + 5];
            if (zeroZ)
            {
                if (o.Z < lo || o.Z > hi) return false;
            }
            else
            {
                float t1 = (lo - o.Z) * invZ, t2 = (hi - o.Z) * invZ;
                if (t1 > t2) { float s = t1; t1 = t2; t2 = s; }
                if (t1 > tMin) tMin = t1;
                if (t2 < tMax) tMax = t2;
            }

            if (tMax < tMin || tMax < 0f || tMin > limit)
                return false;

            tEnter = tMin;
            return true;
        }

        // ---- BVH build: binned SAH over triangle centroids ----------------

        void BuildBvh()
        {
            int n = _indices.Length / 3;
            if (n == 0)
                return;

            _bvhTris = new Vec3[n * 3];
            _bvhTriId = new int[n];
            var centroids = new Vec3[n];
            for (int t = 0; t < n; t++)
            {
                Vec3 v0 = _vertices[_indices[t * 3]];
                Vec3 v1 = _vertices[_indices[t * 3 + 1]];
                Vec3 v2 = _vertices[_indices[t * 3 + 2]];
                _bvhTris[t * 3] = v0;
                _bvhTris[t * 3 + 1] = v1;
                _bvhTris[t * 3 + 2] = v2;
                _bvhTriId[t] = t;
                centroids[t] = (v0 + v1 + v2) * (1f / 3f);
            }

            int capacity = 2 * n - 1;
            _nodeBounds = new float[capacity * 6];
            _nodeLeftFirst = new int[capacity];
            _nodeCount = new int[capacity];

            _nodeLeftFirst[0] = 0;
            _nodeCount[0] = n;
            _nodesUsed = 1;
            Subdivide(0, 1, centroids);
        }

        void Subdivide(int node, int depth, Vec3[] centroids)
        {
            int first = _nodeLeftFirst[node];
            int count = _nodeCount[node];

            TriangleBounds(first, count, out Vec3 min, out Vec3 max);
            StoreBounds(node, min, max);

            // The traversal stack holds at most one node per level.
            if (count <= LeafSize || depth >= MaxDepth)
                return;

            if (!FindSplit(first, count, centroids, out int axis, out float split, out float cost))
                return;

            // Bikker part 2: split only when it beats leaving the triangles in this node.
            if (cost >= count * HalfArea(min, max))
                return;

            // In-place partition on the centroid, carrying vertices, ids and centroids together.
            int i = first;
            int j = first + count - 1;
            while (i <= j)
            {
                if (Axis(centroids[i], axis) < split)
                {
                    i++;
                    continue;
                }

                SwapTriangle(i, j, centroids);
                j--;
            }

            int leftCount = i - first;
            if (leftCount == 0 || leftCount == count)
                return;

            int left = _nodesUsed++;
            int right = _nodesUsed++;
            _nodeLeftFirst[left] = first;
            _nodeCount[left] = leftCount;
            _nodeLeftFirst[right] = i;
            _nodeCount[right] = count - leftCount;

            _nodeLeftFirst[node] = left;
            _nodeCount[node] = 0;

            Subdivide(left, depth + 1, centroids);
            Subdivide(right, depth + 1, centroids);
        }

        /// <summary>
        /// Binned SAH (Bikker part 3): the cheapest of <see cref="SahBins"/> - 1 planes per axis over
        /// the centroid bounds, costed as leftCount * leftArea + rightCount * rightArea.
        /// </summary>
        bool FindSplit(int first, int count, Vec3[] centroids, out int bestAxis, out float bestSplit, out float bestCost)
        {
            bestAxis = -1;
            bestSplit = 0f;
            bestCost = float.MaxValue;

            var binMin = new Vec3[SahBins];
            var binMax = new Vec3[SahBins];
            var binCount = new int[SahBins];
            var leftArea = new float[SahBins - 1];
            var leftCount = new int[SahBins - 1];

            for (int axis = 0; axis < 3; axis++)
            {
                float cMin = float.MaxValue, cMax = float.MinValue;
                for (int k = first; k < first + count; k++)
                {
                    float c = Axis(centroids[k], axis);
                    if (c < cMin) cMin = c;
                    if (c > cMax) cMax = c;
                }

                if (cMax <= cMin)
                    continue;

                for (int b = 0; b < SahBins; b++)
                {
                    binMin[b] = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
                    binMax[b] = new Vec3(float.MinValue, float.MinValue, float.MinValue);
                    binCount[b] = 0;
                }

                float scale = SahBins / (cMax - cMin);
                for (int k = first; k < first + count; k++)
                {
                    int b = Math.Min(SahBins - 1, (int)((Axis(centroids[k], axis) - cMin) * scale));
                    binCount[b]++;
                    for (int v = 0; v < 3; v++)
                        Grow(ref binMin[b], ref binMax[b], _bvhTris[k * 3 + v]);
                }

                int lCount = 0;
                Vec3 accMin = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vec3 accMax = new Vec3(float.MinValue, float.MinValue, float.MinValue);
                for (int b = 0; b < SahBins - 1; b++)
                {
                    lCount += binCount[b];
                    if (binCount[b] > 0)
                    {
                        Grow(ref accMin, ref accMax, binMin[b]);
                        Grow(ref accMin, ref accMax, binMax[b]);
                    }
                    leftCount[b] = lCount;
                    leftArea[b] = lCount > 0 ? HalfArea(accMin, accMax) : 0f;
                }

                int rCount = 0;
                accMin = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
                accMax = new Vec3(float.MinValue, float.MinValue, float.MinValue);
                for (int b = SahBins - 1; b > 0; b--)
                {
                    rCount += binCount[b];
                    if (binCount[b] > 0)
                    {
                        Grow(ref accMin, ref accMax, binMin[b]);
                        Grow(ref accMin, ref accMax, binMax[b]);
                    }

                    int l = leftCount[b - 1];
                    if (l == 0 || rCount == 0)
                        continue;

                    float cost = l * leftArea[b - 1] + rCount * HalfArea(accMin, accMax);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestAxis = axis;
                        bestSplit = cMin + b / scale;
                    }
                }
            }

            return bestAxis >= 0;
        }

        void TriangleBounds(int first, int count, out Vec3 min, out Vec3 max)
        {
            min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vec3(float.MinValue, float.MinValue, float.MinValue);
            for (int k = first * 3; k < (first + count) * 3; k++)
                Grow(ref min, ref max, _bvhTris[k]);
        }

        void StoreBounds(int node, Vec3 min, Vec3 max)
        {
            int i = node * 6;
            _nodeBounds[i] = min.X - BoundsPad;
            _nodeBounds[i + 1] = min.Y - BoundsPad;
            _nodeBounds[i + 2] = min.Z - BoundsPad;
            _nodeBounds[i + 3] = max.X + BoundsPad;
            _nodeBounds[i + 4] = max.Y + BoundsPad;
            _nodeBounds[i + 5] = max.Z + BoundsPad;
        }

        void SwapTriangle(int a, int b, Vec3[] centroids)
        {
            for (int v = 0; v < 3; v++)
                (_bvhTris[a * 3 + v], _bvhTris[b * 3 + v]) = (_bvhTris[b * 3 + v], _bvhTris[a * 3 + v]);
            (_bvhTriId[a], _bvhTriId[b]) = (_bvhTriId[b], _bvhTriId[a]);
            (centroids[a], centroids[b]) = (centroids[b], centroids[a]);
        }

        static void Grow(ref Vec3 min, ref Vec3 max, Vec3 p)
        {
            if (p.X < min.X) min.X = p.X;
            if (p.Y < min.Y) min.Y = p.Y;
            if (p.Z < min.Z) min.Z = p.Z;
            if (p.X > max.X) max.X = p.X;
            if (p.Y > max.Y) max.Y = p.Y;
            if (p.Z > max.Z) max.Z = p.Z;
        }

        static float HalfArea(Vec3 min, Vec3 max)
        {
            float x = max.X - min.X, y = max.Y - min.Y, z = max.Z - min.Z;
            return x * y + y * z + z * x;
        }

        static float Axis(Vec3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

        /// <summary>True when <paramref name="p"/> lies within <see cref="BoundsPad"/> of the triangle's box.</summary>
        internal static bool NearTriangle(Vec3 p, Vec3 v0, Vec3 v1, Vec3 v2)
        {
            if (p.X < Math.Min(v0.X, Math.Min(v1.X, v2.X)) - BoundsPad) return false;
            if (p.X > Math.Max(v0.X, Math.Max(v1.X, v2.X)) + BoundsPad) return false;
            if (p.Y < Math.Min(v0.Y, Math.Min(v1.Y, v2.Y)) - BoundsPad) return false;
            if (p.Y > Math.Max(v0.Y, Math.Max(v1.Y, v2.Y)) + BoundsPad) return false;
            if (p.Z < Math.Min(v0.Z, Math.Min(v1.Z, v2.Z)) - BoundsPad) return false;
            if (p.Z > Math.Max(v0.Z, Math.Max(v1.Z, v2.Z)) + BoundsPad) return false;
            return true;
        }

        bool SegmentHitsBounds(Vec3 a, Vec3 b)
        {
            if (_minX > _maxX)
                return false;

            if (Math.Max(a.X, b.X) < _minX || Math.Min(a.X, b.X) > _maxX) return false;
            if (Math.Max(a.Y, b.Y) < _minY || Math.Min(a.Y, b.Y) > _maxY) return false;
            if (Math.Max(a.Z, b.Z) < _minZ || Math.Min(a.Z, b.Z) > _maxZ) return false;
            return true;
        }

        // ---- slot 1 ---------------------------------------------------------

        /// <summary>
        /// The <b>floor</b> under <paramref name="point"/>: the highest triangle whose XZ projection
        /// contains it and which is not above it. Returns
        /// <see cref="TilemapSurface.NoClosestPoint"/> when nothing is under it, which makes the caller
        /// keep the terrain.
        ///
        /// <para>
        /// <b>This is a reasoned choice, not a 1:1 port</b> — stock's version is inside the unread
        /// <c>KDTreeSurface_c</c>. A true 3D closest point would be wrong here: the ground clamp raises
        /// the body to <c>closest.Y + stepHeight</c> (§6), so standing beside a wall would return a
        /// point on the wall at roughly the body's own height and lift the body a step every frame —
        /// it would climb walls. A downward floor query is the only reading that makes the clamp
        /// behave, and it is what lets a body stand on a statel deck while still walking under a
        /// bridge.
        /// </para>
        /// </summary>
        public void CalculateClosestPoint(Vec3 point, out Vec3 closest, out Vec3 normal, object locality)
        {
            // TilemapSurface.NoClosestPoint, not the point itself: a surface that returns the point it
            // was handed beats the terrain from any height and the ground clamp stops working.
            closest = new Vec3(point.X, TilemapSurface.NoClosestPoint, point.Z);
            normal = Vec3.ReferenceUp;

            if (_nodeCount == null)
                return;
            if (point.X < _minX || point.X > _maxX || point.Z < _minZ || point.Z > _maxZ)
                return;

            // A float-equality guard, not a game constant: a body resting on a deck sits at the deck's
            // own height, so the surface it stands on must not be excluded by rounding.
            const float Tolerance = 1e-3f;
            float ceiling = point.Y + Tolerance;

            bool found = false;
            float bestY = 0f;
            int bestId = 0;
            Vec3 bestNormal = Vec3.ReferenceUp;

            // The same BVH as the line query, walked as a vertical line: a node is visited only when
            // its XZ box holds the point, its bottom is not above the ceiling, and its top could still
            // beat the best floor so far. Each level pushes two and pops one, so the stack needs at
            // most one slot per level plus the root.
            Span<int> stack = stackalloc int[MaxDepth + 2];
            int sp = 0;
            stack[sp++] = 0;

            while (sp > 0)
            {
                int node = stack[--sp];
                int b = node * 6;
                if (point.X < _nodeBounds[b] || point.X > _nodeBounds[b + 3]
                    || point.Z < _nodeBounds[b + 2] || point.Z > _nodeBounds[b + 5])
                    continue;
                if (_nodeBounds[b + 1] > ceiling)
                    continue;
                if (found && _nodeBounds[b + 4] < bestY)
                    continue;

                int count = _nodeCount[node];
                if (count == 0)
                {
                    // Pop the child with the higher top first; its floor is the likelier winner.
                    int left = _nodeLeftFirst[node];
                    int right = left + 1;
                    if (_nodeBounds[left * 6 + 4] > _nodeBounds[right * 6 + 4])
                        (left, right) = (right, left);
                    stack[sp++] = left;
                    stack[sp++] = right;
                    continue;
                }

                int first = _nodeLeftFirst[node];
                for (int k = first; k < first + count; k++)
                {
                    if (!HeightAt(point.X, point.Z, _bvhTris[k * 3], _bvhTris[k * 3 + 1], _bvhTris[k * 3 + 2],
                            out float y, out Vec3 n))
                        continue;
                    if (y > ceiling)
                        continue;

                    // Higher wins; an exact tie goes to the lower original index, as the in-order
                    // sweep this replaced would have kept.
                    int id = _bvhTriId[k];
                    if (found && (y < bestY || (y == bestY && id > bestId)))
                        continue;

                    bestY = y;
                    bestId = id;
                    bestNormal = n;
                    found = true;
                }
            }

            if (!found)
                return;

            closest = new Vec3(point.X, bestY, point.Z);
            normal = bestNormal;
        }

        /// <summary>
        /// The triangle's height at an XZ position, or false when the position is outside it or the
        /// triangle is vertical (no height to give). Barycentric, so it matches the plane exactly at
        /// the vertices.
        /// </summary>
        static bool HeightAt(float x, float z, Vec3 v0, Vec3 v1, Vec3 v2, out float y, out Vec3 normal)
        {
            y = 0f;
            normal = Vec3.ReferenceUp;

            float d = (v1.Z - v2.Z) * (v0.X - v2.X) + (v2.X - v1.X) * (v0.Z - v2.Z);
            if (d == 0f)
                return false;

            float a = ((v1.Z - v2.Z) * (x - v2.X) + (v2.X - v1.X) * (z - v2.Z)) / d;
            if (a < 0f || a > 1f)
                return false;

            float b = ((v2.Z - v0.Z) * (x - v2.X) + (v0.X - v2.X) * (z - v2.Z)) / d;
            if (b < 0f || a + b > 1f)
                return false;

            float c = 1f - a - b;
            if (c < 0f)
                return false;

            y = a * v0.Y + b * v1.Y + c * v2.Y;

            Vec3 n = Vec3.Cross(v1 - v0, v2 - v0);
            if (n.Y < 0f)
                n = -n;                                          // a floor normal always points up
            float length = n.Length;
            normal = length > 0f ? n * (1f / length) : Vec3.ReferenceUp;
            return true;
        }

        /// <summary>Slot 8 — a stub, like every other surface's.</summary>
        public bool VetoPosition(ref Vec3 position, object locality, Vec3 previous) => false;
    }
}
