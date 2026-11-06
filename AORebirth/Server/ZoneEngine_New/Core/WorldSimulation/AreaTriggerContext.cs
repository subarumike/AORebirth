namespace ZoneEngine_New.Core.WorldSimulation
{
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// The executing object, event source and supplied target are distinct native Beholder roles.
    /// Keep the invoking session so delayed feedback cannot follow an identity onto a new connection.
    /// </summary>
    public sealed record AreaTriggerContext(
        Dynel Owner,
        Dynel Source,
        Player? Invoker,
        Dynel? Target,
        IZoneSession? Session)
    {
        public Playfield.Playfield? Playfield { get; init; } = Owner.Playfield ?? Source.Playfield;

        public Dynel ExecutionTarget { get; init; } = Owner;

        public int SourceDoorInstance { get; init; }

        public bool HasCurrentInvoker => Invoker != null
            && Session != null
            && ReferenceEquals(Invoker.Session, Session)
            && ReferenceEquals(Invoker.Playfield, Playfield)
            && Session.State == SessionState.InPlay;
    }
}
