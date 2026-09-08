namespace ZoneEngine_New.Core.WorldSimulation
{
    /// <summary>Mutable holder so playfield DI can receive a world after Build.</summary>
    public sealed class WorldSimulationAccess
    {
        public PlayfieldWorldSimulation? Instance { get; set; }
    }
}
