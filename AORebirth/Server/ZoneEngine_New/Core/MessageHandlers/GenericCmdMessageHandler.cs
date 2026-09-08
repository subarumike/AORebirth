namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Globalization;
    using System.Text;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Missions;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    public sealed class GenericCmdMessageHandler : IMessageHandler<GenericCmdMessage>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IItemBuilder _items;
        private readonly GeneratedMissionAcgService _missions;

        public GenericCmdMessageHandler(IInventoryRepository inventoryRepository, IItemBuilder items, GeneratedMissionAcgService missions)
        {
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);
            _inventoryRepository = inventoryRepository;
            _items = items;
            _missions = missions ?? throw new ArgumentNullException(nameof(missions));
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

            Player? player = session.Player;
            if (session.State != SessionState.InPlay)
            {
                player?.Logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GenericCmd ignored: session not InPlay state={0} action={1}({2}) target={3}",
                        session.State,
                        message.Action,
                        (int)message.Action,
                        FormatTargets(message)));
                return;
            }

            if (player == null || player.IsDead || player.IsPersistenceQuarantined
                || !ReferenceEquals(player.Session, session) || message.Identity != player.Identity)
            {
                player?.Logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GenericCmd ignored: player={0} dead={1} action={2}({3}) target={4}",
                        player == null ? "null" : player.Identity.Instance.ToString(CultureInfo.InvariantCulture),
                        player?.IsDead,
                        message.Action,
                        (int)message.Action,
                        FormatTargets(message)));
                return;
            }

            Playfield? playfield = player.Playfield;
            if (playfield == null)
            {
                Deny(session, message, player, "playfield is null");
                return;
            }

            player.Logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GenericCmd recv char={0} n3={1} action={2}({3}) temp1={4} count={5} temp4={6} user={7} targets={8}",
                    player.Identity.Instance,
                    message.Identity,
                    message.Action,
                    (int)message.Action,
                    message.Temp1,
                    message.Count,
                    message.Temp4,
                    message.User,
                    FormatTargets(message)));

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GenericCmd action={0}({1}) target={2} character={3}",
                    message.Action,
                    (int)message.Action,
                    FormatTargets(message),
                    player.Identity.Instance));

            switch (message.Action)
            {
                case GenericCmdAction.Use:
                    HandleUse(message, session, player, playfield);
                    break;

                case GenericCmdAction.UseItemOnItem:
                    if (message.Target is { Length: >= 2 }
                        && _missions.TryUseItemOnTarget(player, message.Target[0], message.Target[1]))
                        Acknowledge(session, message, message.Target[1]);
                    else Deny(session, message, player, "no accepted item-on-target interaction");
                    break;

                default:
                    player.Logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Deserialized but unhandled GenericCmd action={0}({1}) character={2} targets={3}",
                            message.Action,
                            (int)message.Action,
                            player.Identity.Instance,
                            FormatTargets(message)));
                    Deny(session, message, player, "unhandled action " + message.Action);
                    break;
            }
        }

        void HandleUse(GenericCmdMessage message, IZoneSession session, Player player, Playfield playfield)
        {
            Identity target = message.Target != null && message.Target.Length > 0
                ? message.Target[0]
                : Identity.None;

            player.Logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GenericCmd Use route char={0} targetType={1}({2}/0x{2:X}) inst={3} targetCount={4}",
                    player.Identity.Instance,
                    target.Type,
                    (int)target.Type,
                    target.Instance,
                    message.Target?.Length ?? 0));

            switch (target.Type)
            {
                case IdentityType.Inventory:
                case IdentityType.ArmorPage:
                case IdentityType.SocialPage:
                    HandleUseInventoryItem(message, session, player, target);
                    break;

                default:
                    player.Logger.Info(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "GenericCmd Use treating {0} as world dynel (not Inventory/ArmorPage/SocialPage)",
                            target.Type));
                    HandleUseWorldDynel(message, session, player, playfield, target);
                    break;
            }
        }

        void HandleUseInventoryItem(
            GenericCmdMessage message,
            IZoneSession session,
            Player player,
            Identity target)
        {
            if (!player.Inventory.IsHydrated)
            {
                Deny(session, message, player, "inventory not hydrated");
                return;
            }

            if (!player.Inventory.TryGetItem(target.Type, target.Instance, out Item item))
            {
                Deny(
                    session,
                    message,
                    player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "item not found on page {0} slot={1}. {2}",
                        target.Type,
                        target.Instance,
                        FormatPageSlots(player, target.Type)));
                return;
            }

            player.Logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GenericCmd Use inventory item char={0} slot={1}:{2} name={3} low={4} high={5} ql={6} instanceId={7} itemIdentity={8} locked={9} can=0x{10:X} canUse={11} itemClass={12}({13})",
                    player.Identity.Instance,
                    target.Type,
                    target.Instance,
                    item.Name,
                    item.LowId,
                    item.HighId,
                    item.Quality,
                    item.InstanceId,
                    item.Identity,
                    item.Locked,
                    item.GetStat(CharacterStat.Can),
                    item.Can(CanFlags.Use),
                    (ItemClass)item.GetStat(CharacterStat.ItemClass),
                    item.GetStat(CharacterStat.ItemClass)));

            if (!item.Use(player, target, _inventoryRepository, _items))
            {
                Deny(session, message, player, DescribeInventoryUseFailure(player, item));
                return;
            }

            player.Logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GenericCmd Use inventory item succeeded char={0} name={1} itemIdentity={2} slot={3}:{4}",
                    player.Identity.Instance,
                    item.Name,
                    item.Identity,
                    target.Type,
                    target.Instance));
            Acknowledge(session, message, target);
        }

        void HandleUseWorldDynel(
            GenericCmdMessage message,
            IZoneSession session,
            Player player,
            Playfield playfield,
            Identity target)
        {
            if (target.Instance == 0)
            {
                Deny(session, message, player, "use target instance=0");
                return;
            }

            if (_missions.TryHandleCorpseUse(player, target,
                corpse => Acknowledge(session, message, corpse),
                () => Deny(session, message, player, "generated mission corpse is not eligible")))
                return;

            if (_missions.ClaimsExteriorMarker(player))
            {
                if (_missions.TryEnterExterior(player, target)) Acknowledge(session, message, target);
                else Deny(session, message, player, "generated mission entrance is not eligible");
                return;
            }

            if (!playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel))
            {
                Deny(session, message, player, "dynel not in registry: " + target);
                return;
            }

            if (dynel is not IUsableDynel usable)
            {
                Deny(
                    session,
                    message,
                    player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "dynel {0} is {1}, not IUsableDynel",
                        target,
                        dynel?.GetType().Name ?? "null"));
                return;
            }

            if (!usable.TryUse(player))
            {
                Deny(
                    session,
                    message,
                    player,
                    "TryUse failed on " + target);
                return;
            }

            Acknowledge(session, message, dynel.Identity);
        }

        static string FormatTargets(GenericCmdMessage message)
        {
            if (message.Target == null || message.Target.Length == 0)
                return "(none)";

            var text = new StringBuilder();
            for (int i = 0; i < message.Target.Length; i++)
            {
                if (i > 0)
                    text.Append("; ");

                Identity id = message.Target[i];
                text.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "[{0}] {1}({2}/0x{2:X}) inst={3}",
                    i,
                    id.Type,
                    (int)id.Type,
                    id.Instance);
            }

            return text.ToString();
        }

        static string FormatPageSlots(Player player, IdentityType pageType)
        {
            Container? page = pageType switch
            {
                IdentityType.Inventory => player.Inventory.Inventory,
                IdentityType.WeaponPage => player.Inventory.Equipment,
                IdentityType.ArmorPage => player.Inventory.Armor,
                IdentityType.ImplantPage => player.Inventory.Implant,
                IdentityType.SocialPage => player.Inventory.Social,
                IdentityType.BankByRef => player.Inventory.Bank,
                _ => null
            };

            if (page == null)
                return "no page for type " + pageType;

            if (page.Content.Count == 0)
                return "page empty";

            var text = new StringBuilder("occupied=");
            bool first = true;
            foreach (var slot in page.Content)
            {
                if (!first)
                    text.Append(", ");
                first = false;
                text.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0}:{1}[{2}]",
                    slot.Key,
                    slot.Value.Name,
                    slot.Value.Identity);
            }

            return text.ToString();
        }

        static string DescribeInventoryUseFailure(Player player, Item item)
        {
            if (player.Session == null)
                return "Use failed: player.Session is null";

            if (player.Playfield == null)
                return "Use failed: player.Playfield is null";

            if (!player.Inventory.IsHydrated)
                return "Use failed: inventory not hydrated";

            if (item.Locked)
                return "Use failed: item is locked";

            bool isContainer = item.Identity.Type == IdentityType.Container && item.Identity.Instance != 0;
            bool canUse = item.Can(CanFlags.Use);
            int can = item.GetStat(CharacterStat.Can);

            if (!isContainer && !canUse)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Use failed: not a container identity ({0}) and Can lacks Use (Can=0x{1:X})",
                    item.Identity,
                    can);
            }

            if (isContainer && !canUse)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Use failed: container identity {0} but Can lacks Use (Can=0x{1:X})",
                    item.Identity,
                    can);
            }

            if (!canUse)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Use failed: Can lacks Use (Identity={0} Can=0x{1:X})",
                    item.Identity,
                    can);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Use failed: OnUse spells returned false (Identity={0} canUse={1} isContainer={2})",
                item.Identity,
                canUse,
                isContainer);
        }

        static void Acknowledge(IZoneSession session, GenericCmdMessage message, Identity target)
        {
            session.Player?.Logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "GenericCmd ack temp1=1 action={0} target={1}",
                    message.Action,
                    target));
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
                    FormatTargets(message),
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
