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
        public void Drain(IMessageRouter router, SpawnService spawn)
        {
            while (_channel.Reader.TryRead(out PlayfieldInboundItem? item))
            {
                switch (item)
                {
                    case PendingSpawnInboundItem pendingSpawn:
                        spawn.CompletePendingSpawn(pendingSpawn.Session, pendingSpawn);
                        break;

                    case PendingReconnectInboundItem pendingReconnect:
                        spawn.CompletePendingReconnect(pendingReconnect.Session, pendingReconnect);
                        break;

                    case GameplayInboundItem gameplay:
                        router.Route(new Message { Body = gameplay.Body }, gameplay.Session);
                        break;
                }
            }
        }
    }
}
