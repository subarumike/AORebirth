namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Numerics;
    using System.Text;

    /// <summary>Writes a <see cref="PlayfieldCollisionSet"/> as Wavefront OBJ for visual debug.</summary>
    public static class CollisionObjDumper
    {
        public static void DumpObj(PlayfieldCollisionSet set, string path)
        {
            ArgumentNullException.ThrowIfNull(set);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            var builder = new StringBuilder();
            builder.Append("# AORebirth dungeon/playfield collision dump pf=");
            builder.Append(set.PlayfieldId.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine();
            builder.AppendLine("# OBJ space: X,Y,-Z so DCC viewers match AO left/right.");

            int vertexBase = 1;
            CollisionTriangleMesh? terrain = set.Terrain != null
                ? TerrainHeightfieldMesher.TryBuild(set.Terrain)
                : null;
            if (terrain != null)
                vertexBase = AppendMesh(builder, terrain, "terrain", vertexBase);

            IReadOnlyList<CollisionTriangleMesh> meshes = set.SurfaceMeshes;
            for (int i = 0; i < meshes.Count; i++)
            {
                CollisionTriangleMesh mesh = meshes[i];
                string name = string.IsNullOrWhiteSpace(mesh.Source)
                    ? "mesh" + i.ToString(CultureInfo.InvariantCulture)
                    : mesh.Source;
                if (mesh.CellId is int cellId)
                    name = name + "_cell" + cellId.ToString(CultureInfo.InvariantCulture);
                vertexBase = AppendMesh(builder, mesh, name, vertexBase);
            }

            File.WriteAllText(path, builder.ToString());
        }

        static int AppendMesh(StringBuilder builder, CollisionTriangleMesh mesh, string name, int vertexBase)
        {
            builder.Append("o ");
            builder.AppendLine(Sanitize(name));

            Vector3[] verts = mesh.Vertices;
            for (int v = 0; v < verts.Length; v++)
            {
                Vector3 p = verts[v];
                builder.Append("v ");
                builder.Append(p.X.ToString("G9", CultureInfo.InvariantCulture));
                builder.Append(' ');
                builder.Append(p.Y.ToString("G9", CultureInfo.InvariantCulture));
                builder.Append(' ');
                builder.Append((-p.Z).ToString("G9", CultureInfo.InvariantCulture));
                builder.AppendLine();
            }

            CollisionTriangle[] tris = mesh.Triangles;
            for (int t = 0; t < tris.Length; t++)
            {
                // Mirror Z then reverse winding so outward faces stay outward.
                builder.Append("f ");
                builder.Append((vertexBase + tris[t].A).ToString(CultureInfo.InvariantCulture));
                builder.Append(' ');
                builder.Append((vertexBase + tris[t].C).ToString(CultureInfo.InvariantCulture));
                builder.Append(' ');
                builder.Append((vertexBase + tris[t].B).ToString(CultureInfo.InvariantCulture));
                builder.AppendLine();
            }

            return vertexBase + verts.Length;
        }

        static string Sanitize(string name)
        {
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsLetterOrDigit(chars[i]) || chars[i] == '_' || chars[i] == '-')
                    continue;
                chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
