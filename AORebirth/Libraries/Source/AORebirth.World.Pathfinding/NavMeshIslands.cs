namespace AORebirth.World.Pathfinding
{
    using System;

    using DotRecast.Detour;

    /// <summary>
    /// Connected regions ("islands") of a navmesh, built once at load. Two polygons on different islands have no
    /// route between them in either direction, so a path query between them can fail without searching: an
    /// unreachable target otherwise costs a search that runs to its iteration cap.
    /// Every link counts as two-way (union-find), so one-way off-mesh links can only merge islands, never split
    /// one: "different islands" is always a true "no path".
    /// </summary>
    internal sealed class NavMeshIslands
    {
        readonly int[][] _islandByTilePoly;

        NavMeshIslands(int[][] islandByTilePoly, int count)
        {
            _islandByTilePoly = islandByTilePoly;
            Count = count;
        }

        /// <summary>Number of separate islands.</summary>
        public int Count { get; }

        public static NavMeshIslands Build(DtNavMesh mesh)
        {
            ArgumentNullException.ThrowIfNull(mesh);

            int maxTiles = mesh.GetMaxTiles();
            var offsets = new int[maxTiles];
            int total = 0;
            for (int t = 0; t < maxTiles; t++)
            {
                offsets[t] = total;
                total += PolyCount(mesh.GetTile(t));
            }

            var parent = new int[total];
            for (int i = 0; i < total; i++)
                parent[i] = i;

            for (int t = 0; t < maxTiles; t++)
            {
                DtMeshTile tile = mesh.GetTile(t);
                int polyCount = PolyCount(tile);
                for (int p = 0; p < polyCount; p++)
                {
                    DtPoly poly = tile.data.polys[p];
                    for (int link = poly.firstLink; link != DtDetour.DT_NULL_LINK; link = tile.links[link].next)
                    {
                        long neighbour = tile.links[link].refs;
                        if (neighbour == 0)
                            continue;

                        int nt = DtDetour.DecodePolyIdTile(neighbour);
                        int np = DtDetour.DecodePolyIdPoly(neighbour);
                        if (nt < 0 || nt >= maxTiles || np < 0 || np >= PolyCount(mesh.GetTile(nt)))
                            continue;

                        Union(parent, offsets[t] + p, offsets[nt] + np);
                    }
                }
            }

            // Compact roots to small island ids, per tile and polygon.
            var idByRoot = new int[total];
            int count = 0;
            var islands = new int[maxTiles][];
            for (int t = 0; t < maxTiles; t++)
            {
                int polyCount = PolyCount(mesh.GetTile(t));
                var row = new int[polyCount];
                for (int p = 0; p < polyCount; p++)
                {
                    int root = Find(parent, offsets[t] + p);
                    if (idByRoot[root] == 0)
                        idByRoot[root] = ++count;
                    row[p] = idByRoot[root];
                }

                islands[t] = row;
            }

            return new NavMeshIslands(islands, count);
        }

        /// <summary>The island of <paramref name="polyRef"/>; 0 when the reference is not on this mesh.</summary>
        public int IslandOf(long polyRef)
        {
            int t = DtDetour.DecodePolyIdTile(polyRef);
            int p = DtDetour.DecodePolyIdPoly(polyRef);
            if (t < 0 || t >= _islandByTilePoly.Length)
                return 0;
            int[] row = _islandByTilePoly[t];
            return p >= 0 && p < row.Length ? row[p] : 0;
        }

        /// <summary>True only when both polygons are known and on different islands.</summary>
        public bool AreDisconnected(long a, long b)
        {
            int islandA = IslandOf(a);
            int islandB = IslandOf(b);
            return islandA != 0 && islandB != 0 && islandA != islandB;
        }

        static int PolyCount(DtMeshTile? tile) => tile?.data?.header == null ? 0 : tile.data.header.polyCount;

        static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        static void Union(int[] parent, int a, int b)
        {
            int rootA = Find(parent, a);
            int rootB = Find(parent, b);
            if (rootA != rootB)
                parent[rootB] = rootA;
        }
    }
}
