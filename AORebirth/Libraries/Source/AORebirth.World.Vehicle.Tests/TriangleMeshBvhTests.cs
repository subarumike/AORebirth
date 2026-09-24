using LostEden.Vehicles;
using LostEden.Vehicles.Surfaces;
using Xunit;

namespace LostEden.Vehicle.Tests
{
    /// <summary>
    /// The BVH in <see cref="TriangleMeshSurface"/> is an acceleration structure only: every answer
    /// must be the one a sweep over all triangles gives. The references below are that sweep, written
    /// the way the surface did it before the tree.
    /// </summary>
    public class TriangleMeshBvhTests
    {
        // Mixed geometry: thin wall strips, floor slabs, and small debris triangles, over a 120 m area
        // with some tall pieces -- a rough match for a statel cell's spread and sizes.
        static void RandomSoup(int seed, int triangles, out Vec3[] vertices, out int[] indices)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            vertices = new Vec3[triangles * 3];
            indices = new int[triangles * 3];
            for (int t = 0; t < triangles; t++)
            {
                var c = new Vec3(R(0f, 120f), R(0f, 30f), R(0f, 120f));
                float size = t % 5 == 0 ? R(5f, 25f) : R(0.2f, 4f);
                for (int k = 0; k < 3; k++)
                {
                    Vec3 offset = (t % 3) switch
                    {
                        0 => new Vec3(R(-size, size), R(-0.2f, 0.2f), R(-size, size)),   // floor-ish
                        1 => new Vec3(R(-size, size), R(-size, size), R(-0.2f, 0.2f)),   // wall-ish
                        _ => new Vec3(R(-size, size), R(-size, size), R(-size, size)),
                    };
                    vertices[t * 3 + k] = c + offset;
                    indices[t * 3 + k] = t * 3 + k;
                }
            }
        }

        static bool ReferenceLine(Vec3[] v, int[] idx, Vec3 start, Vec3 end, out Vec3 hit, out Vec3 normal)
            => ReferenceLine(v, idx, start, end, out hit, out normal, out _);

        static bool ReferenceLine(Vec3[] v, int[] idx, Vec3 start, Vec3 end, out Vec3 hit, out Vec3 normal, out int tri)
        {
            tri = -1;
            hit = Vec3.Zero;
            normal = Vec3.ReferenceUp;
            Vec3 direction = end - start;
            float length = direction.Length;
            direction = direction * (1f / length);

            bool found = false;
            float best = 0f;
            for (int i = 0; i + 2 < idx.Length; i += 3)
            {
                if (!TilemapSurface.RayTriangle(start, direction, v[idx[i]], v[idx[i + 2]], v[idx[i + 1]],
                        out Vec3 h, out Vec3 n))
                    continue;
                if (!TriangleMeshSurface.NearTriangle(h, v[idx[i]], v[idx[i + 1]], v[idx[i + 2]]))
                    continue;
                float t = Vec3.Dot(h - start, direction);
                if (t < 0f || t > length)
                    continue;
                if (found && t >= best)
                    continue;
                float nl = n.Length;
                hit = h;
                normal = nl > 0f ? n * (1f / nl) : Vec3.ReferenceUp;
                best = t;
                tri = i / 3;
                found = true;
            }
            return found;
        }

        static float ReferenceFloor(Vec3[] v, int[] idx, Vec3 point, out Vec3 normal)
        {
            normal = Vec3.ReferenceUp;
            float ceiling = point.Y + 1e-3f;
            bool found = false;
            float bestY = TilemapSurface.NoClosestPoint;
            for (int i = 0; i + 2 < idx.Length; i += 3)
            {
                if (!HeightAt(point.X, point.Z, v[idx[i]], v[idx[i + 1]], v[idx[i + 2]], out float y, out Vec3 n))
                    continue;
                if (y > ceiling || (found && y <= bestY))
                    continue;
                bestY = y;
                normal = n;
                found = true;
            }
            return bestY;
        }

        // A copy of TriangleMeshSurface.HeightAt, which is private.
        static bool HeightAt(float x, float z, Vec3 v0, Vec3 v1, Vec3 v2, out float y, out Vec3 normal)
        {
            y = 0f;
            normal = Vec3.ReferenceUp;
            float d = (v1.Z - v2.Z) * (v0.X - v2.X) + (v2.X - v1.X) * (v0.Z - v2.Z);
            if (d == 0f) return false;
            float a = ((v1.Z - v2.Z) * (x - v2.X) + (v2.X - v1.X) * (z - v2.Z)) / d;
            if (a < 0f || a > 1f) return false;
            float b = ((v2.Z - v0.Z) * (x - v2.X) + (v0.X - v2.X) * (z - v2.Z)) / d;
            if (b < 0f || a + b > 1f) return false;
            float c = 1f - a - b;
            if (c < 0f) return false;
            y = a * v0.Y + b * v1.Y + c * v2.Y;
            Vec3 n = Vec3.Cross(v1 - v0, v2 - v0);
            if (n.Y < 0f) n = -n;
            float length = n.Length;
            normal = length > 0f ? n * (1f / length) : Vec3.ReferenceUp;
            return true;
        }

        static float Outside(float x, float a, float b, float c)
            => System.Math.Max(0f, System.Math.Max(System.Math.Min(a, System.Math.Min(b, c)) - x, x - System.Math.Max(a, System.Math.Max(b, c))));

        [Theory]
        [InlineData(1, 50)]
        [InlineData(2, 471)]
        [InlineData(3, 1229)]
        public void LineQueriesMatchTheFullSweep(int seed, int triangles)
        {
            RandomSoup(seed, triangles, out Vec3[] v, out int[] idx);
            var mesh = new TriangleMeshSurface(v, idx);
            var rng = new System.Random(seed * 7919);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            int hits = 0;
            for (int q = 0; q < 20000; q++)
            {
                var start = new Vec3(R(-10f, 130f), R(-5f, 40f), R(-10f, 130f));
                Vec3 dir = (q % 4) switch
                {
                    0 => new Vec3(0f, -1f, 0f),                                        // ground probe
                    1 => new Vec3(R(-1f, 1f), 0f, R(-1f, 1f)),                         // level
                    _ => new Vec3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f)),
                };
                if (dir.Length < 1e-3f)
                    continue;
                dir = dir * (1f / dir.Length);
                Vec3 end = start + dir * (q % 4 == 3 ? R(20f, 150f) : R(0.3f, 12f));

                bool expected = ReferenceLine(v, idx, start, end, out Vec3 eh, out Vec3 en, out int tri);
                bool actual = mesh.GetLineIntersection(start, end, out Vec3 ah, out Vec3 an, false, null);

                if (expected != actual)
                {
                    Vec3 a = v[tri * 3], b = v[tri * 3 + 1], c = v[tri * 3 + 2];
                    float ox = Outside(eh.X, a.X, b.X, c.X), oy = Outside(eh.Y, a.Y, b.Y, c.Y), oz = Outside(eh.Z, a.Z, b.Z, c.Z);
                    Assert.Fail($"query {q}: expected {expected} actual {actual}; ref tri {tri} hit {eh.X},{eh.Y},{eh.Z} outside its AABB by {ox},{oy},{oz}; tri {a.X},{a.Y},{a.Z} / {b.X},{b.Y},{b.Z} / {c.X},{c.Y},{c.Z}; start {start.X},{start.Y},{start.Z} end {end.X},{end.Y},{end.Z}");
                }
                if (!expected)
                    continue;
                hits++;
                Assert.Equal(eh, ah);
                Assert.Equal(en, an);
            }

            Assert.True(hits > 100, $"only {hits} hits; the test is not exercising the tree");
        }

        [Theory]
        [InlineData(1, 50)]
        [InlineData(2, 471)]
        [InlineData(3, 1229)]
        public void FloorQueriesMatchTheFullSweep(int seed, int triangles)
        {
            RandomSoup(seed, triangles, out Vec3[] v, out int[] idx);
            var mesh = new TriangleMeshSurface(v, idx);
            var rng = new System.Random(seed * 104729);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            int floors = 0;
            for (int q = 0; q < 20000; q++)
            {
                var p = new Vec3(R(-10f, 130f), R(-5f, 40f), R(-10f, 130f));

                float expected = ReferenceFloor(v, idx, p, out Vec3 en);
                mesh.CalculateClosestPoint(p, out Vec3 c, out Vec3 an, null);

                Assert.Equal(expected, c.Y);
                if (expected == TilemapSurface.NoClosestPoint)
                    continue;
                floors++;
                Assert.Equal(en, an);
            }

            Assert.True(floors > 100, $"only {floors} floors; the test is not exercising the tree");
        }
    }
}
