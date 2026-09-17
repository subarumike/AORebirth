namespace AORebirth.World.Collision.Tests
{
    using System.Numerics;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class TerrainHeightfieldMesherTests
    {
        [TestMethod]
        public void TryBuild_TwoByTwoChunk_EmitsTwoUpwardTriangles()
        {
            var heights = new float[2, 2];
            heights[0, 0] = 10f;
            heights[1, 0] = 20f;
            heights[0, 1] = 30f;
            heights[1, 1] = 40f;

            var terrain = new TerrainHeightfield(
                tileSize: 4f,
                heightScale: 0.2f,
                chunkSize: 2,
                gridWidth: 1,
                chunks: new[] { new TerrainHeightChunk(heights, originX: 8f, originZ: 12f) });

            CollisionTriangleMesh? mesh = TerrainHeightfieldMesher.TryBuild(terrain);

            Assert.IsNotNull(mesh);
            Assert.AreEqual(4, mesh.Vertices.Length);
            Assert.AreEqual(2, mesh.Triangles.Length);
            Assert.AreEqual(new Vector3(8f, 2f, 12f), mesh.Vertices[0]);
            Assert.AreEqual(new Vector3(12f, 4f, 12f), mesh.Vertices[1]);
            Assert.AreEqual(new Vector3(8f, 6f, 16f), mesh.Vertices[2]);
            Assert.AreEqual(new Vector3(12f, 8f, 16f), mesh.Vertices[3]);
            Assert.AreEqual(0, mesh.Triangles[0].A);
            Assert.AreEqual(2, mesh.Triangles[0].B);
            Assert.AreEqual(1, mesh.Triangles[0].C);
            Assert.AreEqual(1, mesh.Triangles[1].A);
            Assert.AreEqual(2, mesh.Triangles[1].B);
            Assert.AreEqual(3, mesh.Triangles[1].C);
            Vector3 e1 = mesh.Vertices[2] - mesh.Vertices[0];
            Vector3 e2 = mesh.Vertices[1] - mesh.Vertices[0];
            Assert.IsTrue(Vector3.Cross(e1, e2).Y > 0f);
        }

        [TestMethod]
        public void TryBuild_EmptyChunks_ReturnsNull()
        {
            var terrain = new TerrainHeightfield(1f, 1f, 2, 1, System.Array.Empty<TerrainHeightChunk>());
            Assert.IsNull(TerrainHeightfieldMesher.TryBuild(terrain));
        }
    }
}
