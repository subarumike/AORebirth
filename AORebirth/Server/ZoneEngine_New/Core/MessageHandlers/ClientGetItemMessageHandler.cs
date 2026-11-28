namespace ZoneEngine_New.Core.MessageHandlers;

using System;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests;

/// <summary>
/// Retail world-item pickup. An accepted mission objective owns its own eligibility and atomic transfer; any other world
/// item (a world spawn, a player's drop) goes through <see cref="WorldItemTransfers.TryPickup"/>.
/// </summary>
public sealed class ClientGetItemMessageHandler : IMessageHandler<ClientGetItemMessage>
{
    readonly InventoryActionService _inventoryActions;
    readonly IMissionObjectiveModule _missionObjectives;

    public ClientGetItemMessageHandler(InventoryActionService inventoryActions, IMissionObjectiveModule missionObjectives)
    {
        _inventoryActions = inventoryActions ?? throw new ArgumentNullException(nameof(inventoryActions));
        _missionObjectives = missionObjectives ?? throw new ArgumentNullException(nameof(missionObjectives));
    }

    public Type MessageBodyType => typeof(ClientGetItemMessage);

    public void Handle(MessageBody body, IZoneSession session) => Handle((ClientGetItemMessage)body, session);

    public void Handle(ClientGetItemMessage message, IZoneSession session)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(session);
        Player? player = session.Player;
        if (session.State != SessionState.InPlay || player == null || player.IsDead || player.IsPersistenceQuarantined
            || !ReferenceEquals(player.Session, session) || message.Identity != player.Identity
            || player.Playfield is not Playfield playfield)
            return;

        IZoneLogger? logger = playfield.GetService<IZoneLogger>();
        if (!playfield.GetRequiredService<DynelRegistry>().TryGet(message.Identity1, out Dynel? dynel))
        {
            logger?.Warn($"Pickup target unavailable actor={message.Identity} target={message.Identity1} playfield={playfield.Identity.Instance}");
            return;
        }

        if (dynel is MissionObjectiveItem objective)
        {
            _missionObjectives.TryPickup(player, objective);
            return;
        }

        if (dynel is WorldItem worldItem && playfield.GetService<IItemBuilder>() is { } items)
            WorldItemTransfers.TryPickup(player, worldItem, _inventoryActions, items, logger);
    }
}
