namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using AODB.Common.RDBObjects;

    using AORebirth.Core.GameData;

    /// <summary>Loads playfield Collision.dat / Surfaces.dat into an engine-agnostic collision set.</summary>
    public static class PlayfieldCollisionLoader
    {
        static readonly JsonSerializerOptions MetaDataJsonOptions = new()
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Loads collision geometry for <paramref name="playfieldId"/> under a GameData root
        /// (the directory that contains the <c>Playfields</c> folder).
        /// </summary>
        public static PlayfieldCollisionSet Load(string gameDataRoot, int playfieldId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            if (playfieldId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playfieldId));

            PlayfieldMetaData? meta = TryReadMetaData(gameDataRoot, playfieldId);
            var meshes = new List<CollisionTriangleMesh>();
            TerrainHeightfield? terrain = null;

            string collisionPath = Path.Combine(
                gameDataRoot,
                GameDataPaths.PlayfieldCollisionRelativePath(playfieldId));
            if (File.Exists(collisionPath))
            {
                byte[] framed;
                try
                {
                    framed = File.ReadAllBytes(collisionPath);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        "Playfield Collision.dat could not be read: "
                        + collisionPath
                        + " ("
                        + exception.GetType().Name
                        + ": "
                        + exception.Message
                        + ")",
                        exception);
                }

                byte[] tilemapPayload;
                byte[] surfacePayload;
                try
                {
                    PlayfieldCollisionDat.Parse(framed, out tilemapPayload, out surfacePayload);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        "Playfield Collision.dat framing is invalid: "
                        + collisionPath
                        + " ("
                        + exception.GetType().Name
                        + ": "
                        + exception.Message
                        + ")",
                        exception);
                }

                if (tilemapPayload.Length > 0)
                {
                    Tilemap tilemap = RdbObjectDeserializer.Deserialize<Tilemap>(
                        tilemapPayload,
                        collisionPath + "#tilemap");
                    terrain = TilemapNormalizer.Normalize(tilemap, meta);
                }

                if (surfacePayload.Length > 0)
                {
                    SurfaceResource surface = RdbObjectDeserializer.Deserialize<SurfaceResource>(
                        surfacePayload,
                        collisionPath + "#surface");
                    SurfaceResourceNormalizer.AppendMeshes(surface, cellId: null, meshes);
                }
            }

            AppendCellSurfaces(gameDataRoot, playfieldId, meshes);

            return new PlayfieldCollisionSet(playfieldId, meshes, terrain);
        }

        static void AppendCellSurfaces(
            string gameDataRoot,
            int playfieldId,
            List<CollisionTriangleMesh> meshes)
        {
            string surfacesPath = Path.Combine(
                gameDataRoot,
                GameDataPaths.PlayfieldSurfacesRelativePath(playfieldId));
            if (!File.Exists(surfacesPath))
                return;

            List<PlayfieldSurfaceEntry> entries;
            try
            {
                entries = PlayfieldSurfacesDat.Parse(File.ReadAllBytes(surfacesPath));
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Playfield Surfaces.dat framing is invalid: "
                    + surfacesPath
                    + " ("
                    + exception.GetType().Name
                    + ": "
                    + exception.Message
                    + ")",
                    exception);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Payload == null || entries[i].Payload.Length == 0)
                    continue;

                try
                {
                    SurfaceResource surface = RdbObjectDeserializer.Deserialize<SurfaceResource>(
                        entries[i].Payload,
                        surfacesPath
                        + "#cell"
                        + entries[i].CellId.ToString(CultureInfo.InvariantCulture));
                    SurfaceResourceNormalizer.AppendMeshes(surface, entries[i].CellId, meshes);
                }
                catch
                {
                    // Soft-skip: one bad cell must not cost the playfield its static geometry.
                }
            }
        }

        static PlayfieldMetaData? TryReadMetaData(string gameDataRoot, int playfieldId)
        {
            string metadataPath = Path.Combine(
                gameDataRoot,
                GameDataPaths.PlayfieldMetadataRelativePath(playfieldId));
            if (!File.Exists(metadataPath))
                return null;

            try
            {
                PlayfieldMetaData? metaData = JsonSerializer.Deserialize<PlayfieldMetaData>(
                    File.ReadAllText(metadataPath),
                    MetaDataJsonOptions);
                if (metaData == null || !metaData.IsValid(out _))
                    return null;
                return metaData;
            }
            catch
            {
                return null;
            }
        }
    }
}
