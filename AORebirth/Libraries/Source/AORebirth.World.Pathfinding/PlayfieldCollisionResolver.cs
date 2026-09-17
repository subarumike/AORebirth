namespace AORebirth.World.Pathfinding
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using AORebirth.World.Collision;

    /// <summary>
    /// Loads static playfield collision the same way ZoneEngine merges Collision.dat and dungeon rooms.
    /// </summary>
    public static class PlayfieldCollisionResolver
    {
        public static PlayfieldCollisionSet Resolve(string gameDataRoot, int playfieldId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            if (playfieldId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playfieldId));
            if (DungeonPlayfieldKinds.IsStyleTemplate(gameDataRoot, playfieldId))
            {
                throw new InvalidDataException(
                    "Playfield "
                    + playfieldId
                    + " is a style template (every Rooms.json door ZoneLink is -1).");
            }

            PlayfieldCollisionSet loaded = PlayfieldCollisionLoader.Load(gameDataRoot, playfieldId);
            if (!DungeonCollisionBuilder.TryBuildStatic(gameDataRoot, playfieldId, out DungeonWorldLayout? layout)
                || layout == null
                || !layout.Collision.HasCollision)
                return loaded;

            return Merge(playfieldId, loaded, layout.Collision);
        }

        public static PlayfieldCollisionSet Merge(
            int playfieldId,
            PlayfieldCollisionSet existing,
            PlayfieldCollisionSet dungeon)
        {
            ArgumentNullException.ThrowIfNull(existing);
            ArgumentNullException.ThrowIfNull(dungeon);
            if (!dungeon.HasCollision)
                return existing;
            if (!existing.HasCollision)
                return dungeon;

            var meshes = new List<CollisionTriangleMesh>(
                existing.SurfaceMeshes.Count + dungeon.SurfaceMeshes.Count);
            meshes.AddRange(existing.SurfaceMeshes);
            meshes.AddRange(dungeon.SurfaceMeshes);
            return new PlayfieldCollisionSet(
                playfieldId > 0 ? playfieldId : dungeon.PlayfieldId,
                meshes,
                existing.Terrain ?? dungeon.Terrain);
        }
    }
}
