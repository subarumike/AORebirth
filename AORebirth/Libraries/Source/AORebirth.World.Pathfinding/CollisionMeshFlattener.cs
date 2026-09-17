namespace AORebirth.World.Pathfinding
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    using AORebirth.World.Collision;

    internal static class CollisionMeshFlattener
    {
        public static void Flatten(PlayfieldCollisionSet set, out float[] vertices, out int[] triangles, out int triangleCount)
        {
            ArgumentNullException.ThrowIfNull(set);

            var verts = new List<float>();
            var faces = new List<int>();
            CollisionTriangleMesh? terrain = set.Terrain != null
                ? TerrainHeightfieldMesher.TryBuild(set.Terrain)
                : null;
            if (terrain != null)
                Append(terrain, verts, faces);

            IReadOnlyList<CollisionTriangleMesh> meshes = set.SurfaceMeshes;
            for (int i = 0; i < meshes.Count; i++)
                Append(meshes[i], verts, faces);

            vertices = verts.ToArray();
            triangles = faces.ToArray();
            triangleCount = faces.Count / 3;
        }

        static void Append(CollisionTriangleMesh mesh, List<float> vertices, List<int> triangles)
        {
            Vector3[] source = mesh.Vertices;
            int vertexBase = vertices.Count / 3;
            for (int i = 0; i < source.Length; i++)
            {
                vertices.Add(source[i].X);
                vertices.Add(source[i].Y);
                vertices.Add(source[i].Z);
            }

            CollisionTriangle[] faces = mesh.Triangles;
            for (int i = 0; i < faces.Length; i++)
            {
                triangles.Add(vertexBase + faces[i].A);
                triangles.Add(vertexBase + faces[i].B);
                triangles.Add(vertexBase + faces[i].C);
            }
        }
    }
}
