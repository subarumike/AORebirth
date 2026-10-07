namespace ZoneEngine_New.Core.Network
{
    using System;

    /// <summary>The actual GameTime send anchors client-facing mission expiry clocks.</summary>
    public interface IGameTimeSession
    {
        DateTime? GameTimeSynchronizedAtUtc { get; }

        /// <summary>
        /// The server time the last GameTime gave the client (its last field, an int: GameTime_t::Update sets the
        /// client's server-synced system time to it, Gamecode.dll 0x1000b590). The client's clock reads this plus the
        /// seconds since <see cref="GameTimeSynchronizedAtUtc"/>.
        /// </summary>
        int GameTimeServerSeconds { get; }

        void RecordGameTimeSynchronization(DateTime utcNow, int serverSeconds);
    }

    /// <summary>GameTime wire values.</summary>
    public static class GameClock
    {
        /// <summary>
        /// Last GameTime field sent at world entry (official capture 20260623-042326). The message model types it as
        /// a float; its bits are the int server time 1201445800.
        /// </summary>
        public const float WorldEntryServerTime = 80183.3125f;

        /// <summary>The int server time carried by a GameTime last field typed as a float.</summary>
        public static int ServerSeconds(float wireValue) => BitConverter.SingleToInt32Bits(wireValue);
    }
}
