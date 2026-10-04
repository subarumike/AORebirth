namespace ZoneEngine_New.Core.Playfield
{
    using System.Threading.Channels;

    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Network;

    internal sealed class PlayfieldInboundQueue
    {
        private readonly Channel<PlayfieldInboundItem> _channel = Channel.CreateUnbounded<PlayfieldInboundItem>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

        public bool TryEnqueue(PlayfieldInboundItem item) =>
            _channel.Writer.TryWrite(item);

        /// <summary>Called from <see cref="Playfield.Tick"/> only.</summary>
        /// <summary>Stall-watch stage per message type ("in.CharDCMoveMessage"), built once per type.</summary>
        static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, string> InboundStageNames = new();

        static string InboundStageName(object body)
            => InboundStageNames.GetOrAdd(body.GetType(), static type => "in." + type.Name);

        static string InboundItemStage(PlayfieldInboundItem item) => item switch
        {
            TransferDepartureInboundItem => "in.transfer-depart",
            TransferArrivalInboundItem => "in.transfer-arrive",
            TransferReturnInboundItem => "in.transfer-return",
            PlayerProjectionInboundItem => "in.projection",
            PendingSpawnInboundItem => "in.spawn",
            PendingReconnectInboundItem => "in.reconnect",
            _ => "in.other"
        };

        public void Drain(IMessageRouter router, SpawnService spawn, Playfield owner)
        {
            while (_channel.Reader.TryRead(out PlayfieldInboundItem? item))
            {
                if (item is not GameplayInboundItem)
                    Metrics.TickStallWatch.Stage(InboundItemStage(item));

                switch (item)
                {
                    case TransferDepartureInboundItem departure:
                        departure.Transfer.Depart();
                        break;
                    case TransferArrivalInboundItem arrival:
                        arrival.Transfer.Arrive();
                        break;
                    case TransferReturnInboundItem returned:
                        returned.Transfer.Return();
                        break;
                    case PlayerProjectionInboundItem projection:
                        if (ReferenceEquals(projection.Player.Playfield, owner))
                            projection.Projection();
                        else
                            projection.Player.Playfield?.DispatchPlayerProjection(projection.Player, projection.Projection);
                        break;

                    case PendingSpawnInboundItem pendingSpawn:
                        spawn.CompletePendingSpawn(pendingSpawn.Session, pendingSpawn);
                        break;

                    case PendingReconnectInboundItem pendingReconnect:
                        spawn.CompletePendingReconnect(pendingReconnect.Session, pendingReconnect);
                        break;

                    case GameplayInboundItem gameplay:
                        Metrics.TickStallWatch.Stage(
                            InboundStageName(gameplay.Body),
                            gameplay.Session.Player?.Identity.Instance ?? 0);
                        router.Route(new Message { Body = gameplay.Body }, gameplay.Session);
                        break;
                }
            }
        }
    }
}
