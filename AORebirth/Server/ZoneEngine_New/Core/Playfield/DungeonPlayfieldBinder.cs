namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Collections.Generic;

    using AORebirth.World.Collision;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;

    internal static class DungeonPlayfieldBinder
    {
        public static DungeonWorldLayout? TryBuild(
            string gameDataRoot,
            int playfieldId,
            AcgBuildingGeneratorData? generator,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            try
            {
                if (generator != null)
                    return DungeonCollisionBuilder.BuildLayout(gameDataRoot, generator);

                if (DungeonPlayfieldKinds.IsStyleTemplate(gameDataRoot, playfieldId))
                    return null;

                if (!DungeonCollisionBuilder.TryBuildStatic(gameDataRoot, playfieldId, out DungeonWorldLayout? layout))
                    return null;

                return layout;
            }
            catch (Exception exception)
            {
                logger.Warn(
                    "Dungeon layout failed playfield="
                    + playfieldId
                    + ": "
                    + exception.Message);
                return null;
            }
        }

        public static PlayfieldGeometryData WithDungeonCollision(
            int playfieldId,
            PlayfieldGeometryData geometry,
            DungeonWorldLayout? layout)
        {
            ArgumentNullException.ThrowIfNull(geometry);
            if (layout == null || !layout.Collision.HasCollision)
                return geometry;

            PlayfieldCollisionSet? existing = geometry.Collision;
            if (existing == null || !existing.HasCollision)
            {
                return new PlayfieldGeometryData
                {
                    Walls = geometry.Walls,
                    Dynels = geometry.Dynels,
                    Doors = geometry.Doors,
                    Collision = layout.Collision
                };
            }

            var meshes = new List<CollisionTriangleMesh>(
                existing.SurfaceMeshes.Count + layout.Collision.SurfaceMeshes.Count);
            meshes.AddRange(existing.SurfaceMeshes);
            meshes.AddRange(layout.Collision.SurfaceMeshes);
            return new PlayfieldGeometryData
            {
                Walls = geometry.Walls,
                Dynels = geometry.Dynels,
                Doors = geometry.Doors,
                Collision = new PlayfieldCollisionSet(
                    playfieldId > 0 ? playfieldId : layout.Collision.PlayfieldId,
                    meshes,
                    existing.Terrain ?? layout.Collision.Terrain)
            };
        }
    }
}
