namespace ZoneEngine_New.Core.Playfield
{
    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// One unit of inbound work for a playfield tick thread. Not a wire packet.
    /// </summary>
    public abstract class PlayfieldInboundItem
    {
        public required IZoneSession Session { get; init; }
    }

    public sealed class GameplayInboundItem : PlayfieldInboundItem
    {
        public required MessageBody Body { get; init; }
    }

    public sealed class PendingSpawnInboundItem : PlayfieldInboundItem
    {
        public required CharacterHydrationResult Hydration { get; init; }
    }

    public sealed class PendingReconnectInboundItem : PlayfieldInboundItem
    {
        public required int CharacterId { get; init; }
    }
}
