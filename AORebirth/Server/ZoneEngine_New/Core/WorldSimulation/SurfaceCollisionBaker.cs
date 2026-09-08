namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using AODB.Common.RDBObjects;

    using BepuPhysics;
    using BepuPhysics.Collidables;
    using BepuUtilities.Memory;

    using System.Numerics;

    /// <summary>Flattens SurfaceResource meshes into Bepu statics (world-space verts).</summary>
    public static class SurfaceCollisionBaker
    {
        public static int BakeAll(
            SurfaceResource? surface,
            BufferPool pool,
            Simulation simulation)
        {
            if (surface?.Surfaces == null || surface.Surfaces.Count == 0)
                return 0;

            int added = 0;
            for (int i = 0; i < surface.Surfaces.Count; i++)
            {
                SurfaceMesh? mesh = surface.Surfaces[i];
                if (mesh?.Vertices == null || mesh.Triangles == null
                    || mesh.Vertices.Count < 3 || mesh.Triangles.Count < 1)
                    continue;

                if (TryAddMesh(mesh.Vertices, mesh.Triangles, pool, simulation))
                    added++;
            }

            return added;
        }

        static bool TryAddMesh(
            IList vertices,
            IList triangles,
            BufferPool pool,
            Simulation simulation)
        {
            int vertexCount = vertices.Count;
            int triangleCount = triangles.Count;
            pool.Take<Triangle>(triangleCount, out Buffer<Triangle> tris);
            try
            {
                for (int t = 0; t < triangleCount; t++)
                {
                    object triObj = triangles[t]!;
                    int aIdx = ReadIndex(triObj, "A");
                    int bIdx = ReadIndex(triObj, "B");
                    int cIdx = ReadIndex(triObj, "C");
                    if (aIdx < 0 || bIdx < 0 || cIdx < 0
                        || aIdx >= vertexCount || bIdx >= vertexCount || cIdx >= vertexCount)
                    {
                        tris[t] = default;
                        continue;
                    }

                    tris[t] = new Triangle(
                        ReadVec(vertices[aIdx]!),
                        ReadVec(vertices[bIdx]!),
                        ReadVec(vertices[cIdx]!));
                }

                var mesh = new Mesh(tris, Vector3.One, pool);
                simulation.Statics.Add(
                    new StaticDescription(
                        RigidPose.Identity,
                        simulation.Shapes.Add(mesh)));
                return true;
            }
            catch
            {
                pool.Return(ref tris);
                return false;
            }
        }

        static int ReadIndex(object tri, string name)
        {
            object? v = tri.GetType().GetProperty(name)?.GetValue(tri)
                ?? tri.GetType().GetField(name)?.GetValue(tri);
            return v switch
            {
                int i => i,
                short s => s,
                _ => -1
            };
        }

        static Vector3 ReadVec(object v)
        {
            Type t = v.GetType();
            float x = Convert.ToSingle(t.GetProperty("X")?.GetValue(v) ?? 0);
            float y = Convert.ToSingle(t.GetProperty("Y")?.GetValue(v) ?? 0);
            float z = Convert.ToSingle(t.GetProperty("Z")?.GetValue(v) ?? 0);
            return new Vector3(x, y, z);
        }
    }
}
