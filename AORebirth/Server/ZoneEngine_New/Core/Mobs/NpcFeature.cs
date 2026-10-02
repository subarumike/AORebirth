namespace ZoneEngine_New.Core.Mobs
{
    /// <summary>
    /// Behaviour switches an NPC template level entry lists in <c>NpcTemplates.json</c> ("Features": ["NoCombat"]).
    /// </summary>
    public enum NpcFeature
    {
        /// <summary>Cannot be attacked and gets no combat brain (never aggroes or fights back).</summary>
        NoCombat,

        NoMove,

        Wander,
    }
}
