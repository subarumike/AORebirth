namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    public sealed class GiveItemCommand : IGmCommand
    {
        private readonly IItemBuilder _items;
        private readonly IItemTemplateCatalog _catalog;
        private readonly InventoryFlushService _flush;

        public GiveItemCommand(
            IItemBuilder items,
            IItemTemplateCatalog catalog,
            InventoryFlushService flush)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(flush);
            _items = items;
            _catalog = catalog;
            _flush = flush;
        }

        public string Name => "giveitem";

        public int RequiredGmLevel => 1;

        public string Usage => ".giveitem <lowid> <highid> <ql>";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length < 3
                || !int.TryParse(context.Args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int lowId)
                || !int.TryParse(context.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int highId)
                || !int.TryParse(context.Args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int quality)
                || lowId <= 0
                || quality < 1)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            if (highId <= 0)
                highId = lowId;

            if (!_catalog.TryGet(lowId, out _))
            {
                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    string.Format(CultureInfo.InvariantCulture, "Unknown item: {0}", lowId));
                return;
            }

            Player subject = context.ResolveSubject();
            if (!subject.Inventory.IsHydrated)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Target inventory is not loaded.");
                return;
            }

            int slot = subject.Inventory.Inventory.FindFreeSlot();
            if (slot < 0)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Inventory is full.");
                return;
            }

            Item item = _items.CreateWithNewInstance(lowId, highId, quality, ItemSource.Command);
            if (!subject.Inventory.Inventory.Add(slot, item))
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Could not add item to inventory.");
                return;
            }

            subject.Inventory.MarkDirty(item, subject.Inventory.Inventory, slot);
            _flush.NotifyDirty(subject);

            subject.Session?.Send(
                new AddTemplateMessage
                {
                    Identity = subject.Identity,
                    HighId = item.HighId,
                    LowId = item.LowId,
                    Quality = item.Quality,
                    Count = item.StackCount
                });

            string who = SubjectName(subject, context.Player);
            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Gave {0} QL{1} ({2}/{3}) to {4} slot={5}",
                    item.Name,
                    item.Quality,
                    item.LowId,
                    item.HighId,
                    who,
                    slot));

            if (!ReferenceEquals(subject, context.Player) && subject.Session != null)
            {
                GmCommandFeedback.Send(
                    subject.Session,
                    subject,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Received {0} QL{1}",
                        item.Name,
                        item.Quality));
            }
        }

        static string SubjectName(Player subject, Player issuer)
        {
            if (ReferenceEquals(subject, issuer))
                return "self";

            return string.IsNullOrEmpty(subject.Name)
                ? subject.Identity.Instance.ToString(CultureInfo.InvariantCulture)
                : subject.Name;
        }
    }
}
