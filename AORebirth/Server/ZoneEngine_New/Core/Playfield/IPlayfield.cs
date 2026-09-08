namespace ZoneEngine_New.Core.Playfield
{
    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>Playfield host contract. World construction belongs in <see cref="Build"/>.</summary>
    public interface IPlayfield
    {
        Identity Identity { get; }

        /// <summary>
        /// Builds world content (collision, soft triggers, etc.). Called once after construct,
        /// before heartbeat. Safe to call only on the creating thread.
        /// </summary>
        void Build();
    }
}
