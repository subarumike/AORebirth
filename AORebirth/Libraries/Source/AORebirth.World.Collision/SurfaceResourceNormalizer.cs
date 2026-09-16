namespace AORebirth.World.Collision
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Numerics;

    using AODB.Common.RDBObjects;

    /// <summary>Flattens AODB <see cref="SurfaceResource"/> meshes into engine-agnostic triangle meshes.</summary>
    internal static class SurfaceResourceNormalizer
    {
        public static void AppendMeshes(
            SurfaceResource? surface,
            int? cellId,
            List<CollisionTriangleMesh> destination)
        {
            ArgumentNullException.ThrowIfNull(destination);
            if (surface?.Surfaces == null || surface.Surfaces.Count == 0)
                return;

            for (int i = 0; i < surface.Surfaces.Count; i++)
            {
                SurfaceMesh? mesh = surface.Surfaces[i];
                if (mesh?.Vertices == null || mesh.Triangles == null
                    || mesh.Vertices.Count < 3 || mesh.Triangles.Count < 1)
                    continue;

                if (TryNormalize(mesh.Vertices, mesh.Triangles, cellId, out CollisionTriangleMesh? normalized)
                    && normalized != null)
                {
                    destination.Add(normalized);
                }
            }
        }

        static bool TryNormalize(
            IList vertices,
            IList triangles,
            int? cellId,
            out CollisionTriangleMesh? mesh)
        {
            mesh = null;
            int vertexCount = vertices.Count;
            int triangleCount = triangles.Count;
            var verts = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                object? vertex = vertices[i];
                if (vertex == null)
                    return false;
                verts[i] = ReadVec(vertex);
            }

            var tris = new List<CollisionTriangle>(triangleCount);
            for (int t = 0; t < triangleCount; t++)
            {
                object? triObj = triangles[t];
                if (triObj == null)
                    continue;

                int aIdx = ReadIndex(triObj, "A");
                int bIdx = ReadIndex(triObj, "B");
                int cIdx = ReadIndex(triObj, "C");
                if (aIdx < 0 || bIdx < 0 || cIdx < 0
                    || aIdx >= vertexCount || bIdx >= vertexCount || cIdx >= vertexCount)
                    continue;

                tris.Add(new CollisionTriangle(aIdx, bIdx, cIdx));
            }

            if (tris.Count == 0)
                return false;

            mesh = new CollisionTriangleMesh(verts, tris.ToArray(), cellId);
            return true;
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
            if (v is Vector3 numerics)
                return numerics;

            Type t = v.GetType();
            float x = Convert.ToSingle(t.GetProperty("X")?.GetValue(v) ?? t.GetField("X")?.GetValue(v) ?? 0);
            float y = Convert.ToSingle(t.GetProperty("Y")?.GetValue(v) ?? t.GetField("Y")?.GetValue(v) ?? 0);
            float z = Convert.ToSingle(t.GetProperty("Z")?.GetValue(v) ?? t.GetField("Z")?.GetValue(v) ?? 0);
            return new Vector3(x, y, z);
        }
    }
}
