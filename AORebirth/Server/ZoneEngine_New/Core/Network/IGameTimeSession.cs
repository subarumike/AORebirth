namespace ZoneEngine_New.Core.Network
{
    using System;

    /// <summary>The actual GameTime send anchors client-facing mission expiry clocks.</summary>
    public interface IGameTimeSession
    {
        DateTime? GameTimeSynchronizedAtUtc { get; }
        void RecordGameTimeSynchronization(DateTime utcNow);
    }
}
