namespace AORebirth.Enums
{
    /// <summary>
    /// Mesh draw layer for appearance / SCFU mesh entries.
    /// Matches placement→layer mapping used for head (0) and worn/weapon meshes (4).
    /// </summary>
    public enum MeshLayer : byte
    {
        Head = 0,
        Equipment = 4,
    }
}
