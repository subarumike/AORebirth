namespace AORebirth.World.Collision.Tests
{
    using System;
    using System.IO;

    using AORebirth.Core.GameData;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class PlayfieldCollisionLoaderTests
    {
        static string GameDataRoot
        {
            get
            {
                string repoRoot = FindRepoRoot();
                return Path.Combine(repoRoot, "AORebirth", "GameData");
            }
        }

        [TestMethod]
        public void Load_OutdoorPlayfield100_HasTerrainOrSurfaces()
        {
            string collisionPath = Path.Combine(
                GameDataRoot,
                GameDataPaths.PlayfieldCollisionRelativePath(100));
            Assert.IsTrue(File.Exists(collisionPath), "Expected packaged Collision.dat for playfield 100.");

            PlayfieldCollisionSet set = PlayfieldCollisionLoader.Load(GameDataRoot, 100);

            Assert.AreEqual(100, set.PlayfieldId);
            Assert.IsTrue(
                set.HasCollision,
                "Playfield 100 should expose terrain and/or surface meshes.");
            Assert.IsTrue(
                set.Terrain != null || set.SurfaceMeshes.Count > 0,
                "Expected terrain heightfield or at least one surface mesh.");
        }

        [TestMethod]
        public void Load_MissingCollisionDat_DoesNotThrow_ReturnsEmptyCollision()
        {
            string tempRoot = CreateTempGameDataRoot(playfieldId: 999001, writeCollision: false, writeSurfaces: false);

            try
            {
                PlayfieldCollisionSet set = PlayfieldCollisionLoader.Load(tempRoot, 999001);
                Assert.AreEqual(999001, set.PlayfieldId);
                Assert.IsFalse(set.HasCollision);
                Assert.IsNull(set.Terrain);
                Assert.AreEqual(0, set.SurfaceMeshes.Count);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestMethod]
        public void Load_InvalidCollisionFraming_ThrowsInvalidDataException()
        {
            string tempRoot = CreateTempGameDataRoot(playfieldId: 999002, writeCollision: true, writeSurfaces: false);
            string collisionPath = Path.Combine(
                tempRoot,
                GameDataPaths.PlayfieldCollisionRelativePath(999002));
            File.WriteAllBytes(collisionPath, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x01 });

            try
            {
                Assert.ThrowsExactly<InvalidDataException>(
                    () => PlayfieldCollisionLoader.Load(tempRoot, 999002));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestMethod]
        public void Load_BadCellPayload_IsSkipped_OtherCellsStillLoad()
        {
            string tempRoot = CreateTempGameDataRoot(playfieldId: 999003, writeCollision: false, writeSurfaces: false);
            string surfacesPath = Path.Combine(
                tempRoot,
                GameDataPaths.PlayfieldSurfacesRelativePath(999003));
            byte[] framed = PlayfieldSurfacesDat.Build(
                new[]
                {
                    new PlayfieldSurfaceEntry(1, new byte[] { 0x01, 0x02, 0x03 }),
                    new PlayfieldSurfaceEntry(2, Array.Empty<byte>())
                });
            File.WriteAllBytes(surfacesPath, framed);

            try
            {
                PlayfieldCollisionSet set = PlayfieldCollisionLoader.Load(tempRoot, 999003);
                Assert.AreEqual(0, set.SurfaceMeshes.Count);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        static string CreateTempGameDataRoot(int playfieldId, bool writeCollision, bool writeSurfaces)
        {
            string root = Path.Combine(Path.GetTempPath(), "AORebirth.World.Collision.Tests", Guid.NewGuid().ToString("N"));
            string playfieldDir = Path.Combine(root, GameDataPaths.PlayfieldRelativeDirectory(playfieldId));
            Directory.CreateDirectory(playfieldDir);

            if (writeCollision)
            {
                File.WriteAllBytes(
                    Path.Combine(playfieldDir, GameDataPaths.CollisionFileName),
                    PlayfieldCollisionDat.Build(Array.Empty<byte>(), Array.Empty<byte>()));
            }

            if (writeSurfaces)
            {
                File.WriteAllBytes(
                    Path.Combine(playfieldDir, GameDataPaths.SurfacesFileName),
                    PlayfieldSurfacesDat.Build(Array.Empty<PlayfieldSurfaceEntry>()));
            }

            return root;
        }

        static string FindRepoRoot()
        {
            string? dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "AGENTS.md"))
                    && Directory.Exists(Path.Combine(dir, "AORebirth", "GameData", "Playfields")))
                {
                    return dir;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }

            throw new InvalidOperationException(
                "Could not locate repository root from " + AppContext.BaseDirectory);
        }
    }
}
