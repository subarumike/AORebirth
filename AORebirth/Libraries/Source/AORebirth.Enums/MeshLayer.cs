namespace AORebirth.Enums
{
    /// <summary>
    /// Mesh draw layer for appearance / SCFU mesh entries.
    /// Matches placement→layer mapping used for head (0) and worn/weapon meshes (4).
    /// </summary>
    public enum MeshLayer : byte
    {
        Head = 0,
        /// <summary>A worn helmet (HeadMesh): over the head, which stays listed (live AppearanceUpdate 2026-10-06T19:14:34Z).</summary>
        Helmet = 2,
        Equipment = 4,
    }
}
