namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    public sealed class GenericCmdMessageHandler : IMessageHandler<GenericCmdMessage>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IItemBuilder _items;

        public GenericCmdMessageHandler(IInventoryRepository inventoryRepository, IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);
            _inventoryRepository = inventoryRepository;
            _items = items;
        }

        public Type MessageBodyType => typeof(GenericCmdMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((GenericCmdMessage)body, session);
        }

        public void Handle(GenericCmdMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null || player.IsDead)
                return;

            Playfield? playfield = player.Playfield;
            if (playfield == null)
            {
                Deny(session, message);
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GenericCmd action={0}({1}) target={2} character={3}",
                    message.Action,
                    (int)message.Action,
                    FormatTarget(message),
                    player.Identity.Instance));

            switch (message.Action)
            {
                case GenericCmdAction.Use:
                    HandleUse(message, session, player, playfield);
                    break;

                default:
                    player.Logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Deserialized but unhandled GenericCmd action={0}({1}) character={2}",
                            message.Action,
                            (int)message.Action,
                            player.Identity.Instance));
                    Deny(session, message);
                    break;
            }
        }

        void HandleUse(GenericCmdMessage message, IZoneSession session, Player player, Playfield playfield)
        {
            Identity target = message.Target != null && message.Target.Length > 0
                ? message.Target[0]
                : Identity.None;

            switch (target.Type)
            {
                case IdentityType.Inventory:
                case IdentityType.ArmorPage:
                case IdentityType.SocialPage:
                    HandleUseInventoryItem(message, session, player, target);
                    break;

                case IdentityType.Corpse:
                case IdentityType.Container:
                    HandleUseLootable(message, session, player, playfield, target);
                    break;

                default:
                    player.Logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Deserialized but unhandled GenericCmd Use target type={0}({1}) character={2}",
                            target.Type,
                            (int)target.Type,
                            player.Identity.Instance));
                    Deny(session, message);
                    break;
            }
        }

        void HandleUseInventoryItem(
            GenericCmdMessage message,
            IZoneSession session,
            Player player,
            Identity target)
        {
            if (!player.Inventory.IsHydrated
                || !player.Inventory.TryGetItem(target.Type, target.Instance, out Item item))
            {
                Deny(session, message);
                return;
            }

            if (!item.Use(player, target, _inventoryRepository, _items))
            {
                Deny(session, message);
                return;
            }

            Acknowledge(session, message, target);
        }

        static void HandleUseLootable(
            GenericCmdMessage message,
            IZoneSession session,
            Player player,
            Playfield playfield,
            Identity target)
        {
            if (target.Instance == 0)
            {
                Deny(session, message, player, "lootable target instance=0");
                return;
            }

            if (!playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel))
            {
                Deny(session, message, player, "lootable dynel not in registry: " + target);
                return;
            }

            if (dynel is not LootableDynel lootable)
            {
                Deny(
                    session,
                    message,
                    player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "dynel {0} is {1}, not LootableDynel",
                        target,
                        dynel?.GetType().Name ?? "null"));
                return;
            }

            if (!lootable.TryOpen(player))
            {
                Deny(
                    session,
                    message,
                    player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "TryOpen failed on {0} isOpen={1} opener={2}",
                        lootable.Identity,
                        lootable.IsOpen,
                        lootable.OpenerIdentity));
                return;
            }

            Acknowledge(session, message, lootable.Identity);
        }

        static string FormatTarget(GenericCmdMessage message)
        {
            if (message.Target == null || message.Target.Length == 0)
                return Identity.None.ToString();

            return message.Target[0].ToString();
        }

        static void Acknowledge(IZoneSession session, GenericCmdMessage message, Identity target)
        {
            session.Send(Reply(message, target, temp1: 1));
        }

        static void Deny(IZoneSession session, GenericCmdMessage message)
        {
            session.Send(Reply(message, Identity.None, temp1: 2));
        }

        static void Deny(IZoneSession session, GenericCmdMessage message, Player player, string reason)
        {
            player.Logger.Warn(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GenericCmd Use denied char={0} action={1} target={2}: {3}",
                    player.Identity.Instance,
                    message.Action,
                    FormatTarget(message),
                    reason));
            Deny(session, message);
        }

        static GenericCmdMessage Reply(GenericCmdMessage message, Identity targetOverride, int temp1)
        {
            Identity[] targets = message.Target != null
                ? (Identity[])message.Target.Clone()
                : [];

            if (targetOverride != Identity.None && targets.Length > 0)
                targets[0] = targetOverride;

            return new GenericCmdMessage
            {
                Identity = message.Identity,
                Temp1 = temp1,
                Count = message.Count,
                Action = message.Action,
                Temp4 = message.Temp4,
                User = message.User,
                Target = targets,
                Unknown = 0
            };
        }
    }
}
