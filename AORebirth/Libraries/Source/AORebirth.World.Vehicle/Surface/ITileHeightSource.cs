namespace LostEden.Vehicles.Surfaces
{
    /// <summary>
    /// The terrain data <see cref="TilemapSurface"/> samples, in the shape stock reads it.
    ///
    /// <para>
    /// Stock's <c>n3Tilemap_t</c> is a thin wrapper over the <c>RDBTilemap_t</c> resource
    /// (<c>GetTileMapResource</c> <c>10016320</c> is <c>mov eax,[ecx+0xc]; ret</c>), so the three
    /// properties here are direct reads of resource fields — see <c>Docs/Movement.md</c> §5.2a.
    /// </para>
    ///
    /// <para>
    /// Kept free of Unity and of AODB so <c>Tests/Vehicle</c> can drive the surface with synthetic
    /// terrain; <see cref="ChunkedTileHeightSource"/> is the real implementation.
    /// </para>
    /// </summary>
    public interface ITileHeightSource
    {
        /// <summary><c>n3Tilemap_t::GetWidth</c> (<c>1001639c</c>) — resource <c>+0x825c</c>.</summary>
        int Width { get; }

        /// <summary><c>n3Tilemap_t::GetHeight</c> (<c>100163a6</c>) — resource <c>+0x8260</c>.</summary>
        int Height { get; }

        /// <summary>Resource <c>+0x8264</c>, AODB's <c>MapScale</c>. One tile's world size.</summary>
        float TileSize { get; }

        /// <summary>
        /// <c>10017c3e</c> — the height at a sample position, already scaled:
        /// <c>sample * HeightMod</c>. Callers pass sample coordinates, not tile coordinates; the two
        /// differ at the far edge because patches share their edge samples.
        /// </summary>
        float SampleHeight(int x, int z);
    }
}
