namespace ZoneEngine_New.Core.Entities
{
    /// <summary>
    /// World connection phase for an in-memory player (distinct from per-TCP <c>SessionState</c>).
    /// </summary>
    public enum PlayerConnectionPhase
    {
        Online,
        LinkDead
    }
}
