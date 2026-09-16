namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;

    using AORebirth.World.Collision;

    using BepuPhysics;
    using BepuPhysics.Collidables;
    using BepuUtilities.Memory;

    using System.Numerics;

    /// <summary>Flattens collision triangle meshes into Bepu statics (world-space verts).</summary>
    public static class SurfaceCollisionBaker
    {
        public static int BakeAll(
            PlayfieldCollisionSet? collision,
            BufferPool pool,
            Simulation simulation)
        {
            if (collision == null || collision.SurfaceMeshes.Count == 0)
                return 0;

            return BakeAll(collision.SurfaceMeshes, pool, simulation);
        }

        public static int BakeAll(
            IReadOnlyList<CollisionTriangleMesh> meshes,
            BufferPool pool,
            Simulation simulation)
        {
            ArgumentNullException.ThrowIfNull(meshes);
            int added = 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                if (TryAddMesh(meshes[i], pool, simulation))
                    added++;
            }

            return added;
        }

        static bool TryAddMesh(
            CollisionTriangleMesh mesh,
            BufferPool pool,
            Simulation simulation)
        {
            Vector3[] vertices = mesh.Vertices;
            CollisionTriangle[] triangles = mesh.Triangles;
            if (vertices.Length < 3 || triangles.Length < 1)
                return false;

            int triangleCount = triangles.Length;
            // Bepu mesh rays are one-sided; bake each triangle twice so LOS/sweeps hit either face.
            int bakedCount = triangleCount * 2;
            pool.Take<Triangle>(bakedCount, out Buffer<Triangle> tris);
            try
            {
                for (int t = 0; t < triangleCount; t++)
                {
                    CollisionTriangle tri = triangles[t];
                    if (tri.A < 0 || tri.B < 0 || tri.C < 0
                        || tri.A >= vertices.Length || tri.B >= vertices.Length || tri.C >= vertices.Length)
                    {
                        tris[t] = default;
                        tris[triangleCount + t] = default;
                        continue;
                    }

                    Vector3 a = vertices[tri.A];
                    Vector3 b = vertices[tri.B];
                    Vector3 c = vertices[tri.C];
                    tris[t] = new Triangle(a, b, c);
                    tris[triangleCount + t] = new Triangle(a, c, b);
                }

                var bepuMesh = new Mesh(tris, Vector3.One, pool);
                simulation.Statics.Add(
                    new StaticDescription(
                        RigidPose.Identity,
                        simulation.Shapes.Add(bepuMesh)));
                return true;
            }
            catch
            {
                pool.Return(ref tris);
                return false;
            }
        }
    }
}
