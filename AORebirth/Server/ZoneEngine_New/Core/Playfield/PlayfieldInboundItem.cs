namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// One unit of inbound work for a playfield tick thread. Not a wire packet.
    /// </summary>
    public abstract class PlayfieldInboundItem
    {
    }

    public sealed class GameplayInboundItem : PlayfieldInboundItem
    {
        public required IZoneSession Session { get; init; }
        public required MessageBody Body { get; init; }
    }

    public sealed class PendingSpawnInboundItem : PlayfieldInboundItem
    {
        public required IZoneSession Session { get; init; }
        public required CharacterHydrationResult Hydration { get; init; }
    }

    public sealed class PendingReconnectInboundItem : PlayfieldInboundItem
    {
        public required IZoneSession Session { get; init; }
        public required int CharacterId { get; init; }
    }

    public sealed class PlayerProjectionInboundItem : PlayfieldInboundItem
    {
        public required Player Player { get; init; }
        public required Action Projection { get; init; }
    }

    internal sealed class TransferDepartureInboundItem(PlayfieldTransfer transfer) : PlayfieldInboundItem
    { public PlayfieldTransfer Transfer { get; } = transfer; }
    internal sealed class TransferArrivalInboundItem(PlayfieldTransfer transfer) : PlayfieldInboundItem
    { public PlayfieldTransfer Transfer { get; } = transfer; }
    internal sealed class TransferReturnInboundItem(PlayfieldTransfer transfer) : PlayfieldInboundItem
    { public PlayfieldTransfer Transfer { get; } = transfer; }
}
