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
    using ZoneEngine_New.Core.Nanos;

    public sealed class GiveCommand : IGmCommand
    {
        private readonly IItemBuilder _items;
        private readonly IItemTemplateCatalog _catalog;
        private readonly InventoryFlushService _flush;
        private readonly HashItemMinter _minter;
        private readonly IInventoryRepository _inventory;

        public GiveCommand(
            IItemBuilder items,
            IItemTemplateCatalog catalog,
            InventoryFlushService flush,
            HashItemMinter minter,
            IInventoryRepository inventory)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(flush);
            ArgumentNullException.ThrowIfNull(minter);
            ArgumentNullException.ThrowIfNull(inventory);
            _items = items;
            _catalog = catalog;
            _flush = flush;
            _minter = minter;
            _inventory = inventory;
        }

        public string Name => "give";

        public int RequiredGmLevel => 1;

        public string Usage => ".give item <low> <high> <ql> | .give item <hash> <ql> | .give buff <nanoId>";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length < 1)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            string verb = context.Args[0];
            if (string.Equals(verb, "item", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteItem(context);
                return;
            }

            if (string.Equals(verb, "buff", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteBuff(context);
                return;
            }

            GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
        }

        void ExecuteItem(GmCommandContext context)
        {
            if (context.Args.Length == 4
                && int.TryParse(context.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int lowId)
                && int.TryParse(context.Args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int highId)
                && int.TryParse(context.Args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int quality)
                && lowId > 0
                && quality >= 1)
            {
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

                GiveCreatedItem(context, _items.CreateWithNewInstance(lowId, highId, quality, ItemSource.Command));
                return;
            }

            if (context.Args.Length == 3
                && !string.IsNullOrWhiteSpace(context.Args[1])
                && int.TryParse(context.Args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out quality)
                && quality >= 1)
            {
                string hash = context.Args[1];
                if (!_minter.TryMint(hash, quality, ItemSource.Command, out Item item))
                {
                    GmCommandFeedback.Send(
                        context.Session,
                        context.Player,
                        string.Format(CultureInfo.InvariantCulture, "Unknown item hash: {0}", hash));
                    return;
                }

                GiveCreatedItem(context, item);
                return;
            }

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                "Usage: .give item <low> <high> <ql> | .give item <hash> <ql>");
        }

        void GiveCreatedItem(GmCommandContext context, Item item)
        {
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

            string who = PlayerSubjectName(subject, context.Player);
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

        void ExecuteBuff(GmCommandContext context)
        {
            if (context.Args.Length < 2
                || !int.TryParse(context.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int nanoId)
                || nanoId <= 0)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: .give buff <nanoId>");
                return;
            }

            if (!_catalog.TryGet(nanoId, out ItemTemplate template))
            {
                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    string.Format(CultureInfo.InvariantCulture, "Unknown nano: {0}", nanoId));
                return;
            }

            NanoSpell spell = NanoSpell.From(template);
            if (!spell.IsBuff)
            {
                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Not a buff: {0} ({1})",
                        spell.Name,
                        nanoId));
                return;
            }

            if (!context.TryResolveCharacter(out Character subject))
                return;

            if (subject.IsDead)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Target is dead.");
                return;
            }

            if (!NanoRuntime.TryApplyImmediate(context.Player, subject, nanoId, _items, _inventory, DateTime.UtcNow))
            {
                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to apply {0} ({1}) to {2}.",
                        spell.Name,
                        nanoId,
                        CharacterSubjectName(subject)));
                return;
            }

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Applied {0} ({1}) to {2}.",
                    spell.Name,
                    nanoId,
                    CharacterSubjectName(subject)));
        }

        static string PlayerSubjectName(Player subject, Player issuer)
        {
            if (ReferenceEquals(subject, issuer))
                return "self";

            return string.IsNullOrEmpty(subject.Name)
                ? subject.Identity.Instance.ToString(CultureInfo.InvariantCulture)
                : subject.Name;
        }

        static string CharacterSubjectName(Character subject)
        {
            return string.IsNullOrEmpty(subject.Name)
                ? subject.Identity.Instance.ToString(CultureInfo.InvariantCulture)
                : subject.Name;
        }
    }
}
