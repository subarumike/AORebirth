namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Network;

    public sealed class ClientContainerAddItemMessageHandler : IMessageHandler<ClientContainerAddItemMessage>
    {
        private readonly InventoryMoveService _moves;

        public ClientContainerAddItemMessageHandler(InventoryMoveService moves)
        {
            ArgumentNullException.ThrowIfNull(moves);
            _moves = moves;
        }

        public Type MessageBodyType => typeof(ClientContainerAddItemMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((ClientContainerAddItemMessage)body, session);
        }

        public void Handle(ClientContainerAddItemMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null || player.IsDead)
                return;

            _moves.Handle(player, message);
        }
    }
}
