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
    using ZoneEngine_New.Core.Quests;

    public sealed class GiveCommand : IGmCommand
    {
        private readonly IItemBuilder _items;
        private readonly IItemTemplateCatalog _catalog;
        private readonly InventoryFlushService _flush;
        private readonly HashItemMinter _minter;
        private readonly IInventoryRepository _inventory;
        private readonly QuestService _quests;
        private readonly Setups.GiveSetupService _setups;

        public GiveCommand(
            IItemBuilder items,
            IItemTemplateCatalog catalog,
            InventoryFlushService flush,
            HashItemMinter minter,
            IInventoryRepository inventory,
            QuestService quests,
            Setups.GiveSetupService setups)
        {
            ArgumentNullException.ThrowIfNull(setups);
            _setups = setups;
            ArgumentNullException.ThrowIfNull(quests);
            _quests = quests;
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

        public string Usage => ".give item <low> <high> <ql> | .give item <hash> <ql> | .give buff <nanoId> | .give quest <hash> | .give setup <aosetups url>";

        public GmCommandHelp Help { get; } = new(
            "Gives items, nanos, quests or a whole setup.",
            [
                new(".give item <low> <high> <ql>", "An item by its low and high template ids at a quality."),
                new(".give item <hash> <ql>", "An item from an item hash at a quality."),
                new(".give buff <nanoId>", "Lands a nano program, ignoring combat rules."),
                new(".give quest <hash>", "Starts a quest."),
                new(".give setup <aosetups url>", "Equips a full setup from AO Setups."),
            ],
            [
                new(".give item 266993 266993 300", "A QL 300 item by template id."),
                new(".give item CARO 25", "A QL 25 item from a hash."),
                new(".give buff 214879", "Lands nano 214879 (PvP Enabled)."),
            ],
            "Items, quests and setups go to your look-at player, or to you. Buffs land on your look-at character (player or NPC), or on you.");

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length < 1)
            {
                context.SendUsage();
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

            if (string.Equals(verb, "quest", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteQuest(context);
                return;
            }

            if (string.Equals(verb, "setup", StringComparison.OrdinalIgnoreCase))
            {
                // Always the issuer: a setup replaces worn gear and skills.
                _setups.Start(context.Session, context.Player, context.Args.Length > 1 ? context.Args[1] : string.Empty);
                return;
            }

            context.SendUsage();
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

            context.SendUsage("Usage: .give item <low> <high> <ql> | .give item <hash> <ql>");
        }

        void GiveCreatedItem(GmCommandContext context, Item item)
        {
            Player subject = context.ResolveSubject();
            if (!subject.Inventory.IsHydrated)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Target inventory is not loaded.");
                return;
            }

            if (InventoryStacking.TryGrantOntoStack(subject, item, _flush))
            {
                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Gave {0} QL{1} x{2} to {3} (stacked)",
                        item.Name,
                        item.Quality,
                        item.StackCount,
                        PlayerSubjectName(subject, context.Player)));
                return;
            }

            // The first free inventory slot, else overflow (which keeps only its newest items).
            if (!subject.Inventory.TryPlace(item, out Container page, out int slot))
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Inventory and overflow are full.");
                return;
            }

            subject.Inventory.MarkDirty(item, page, slot);
            _flush.NotifyDirty(subject);

            if (ReferenceEquals(page, subject.Inventory.Overflow))
            {
                OverflowGrants.Send(subject, item);
                OverflowGrants.Trim(subject);
                GmCommandFeedback.Send(context.Session, context.Player, string.Format(CultureInfo.InvariantCulture,
                    "Gave {0} QL{1} x{2} to {3} (overflow)", item.Name, item.Quality, item.StackCount,
                    PlayerSubjectName(subject, context.Player)));
                return;
            }

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

        /// <summary>Gives a Quests.json quest, or an existing generated quest by id, to the targeted player or yourself.</summary>
        void ExecuteQuest(GmCommandContext context)
        {
            if (context.Args.Length < 2 || string.IsNullOrWhiteSpace(context.Args[1]))
            {
                context.SendUsage("Usage: .give quest <hash>");
                return;
            }

            Player subject = context.ResolveSubject();
            string id = context.Args[1].Trim().ToUpperInvariant();
            // Template hashes are four characters; generated quest ids are longer and start with G.
            string result;
            if (id.Length > 4 && id[0] == 'G')
                _quests.TryAssignGenerated(subject, id, out result);
            else
                _quests.TryAssignTemplate(subject, id, out result, force: true);
            GmCommandFeedback.Send(context.Session, context.Player, result);
        }

        void ExecuteBuff(GmCommandContext context)
        {
            if (context.Args.Length < 2
                || !int.TryParse(context.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int nanoId)
                || nanoId <= 0)
            {
                context.SendUsage("Usage: .give buff <nanoId>");
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

            // A self-only nano is only ever cast by its own holder, and its User / Wearer functions act on the caster:
            // the subject casts it on themselves, so a team launcher (223199 -> 217788) reaches the subject's team
            // rather than the GM.
            Character caster = spell.IsSelfOnly ? subject : context.Player;
            if (!NanoRuntime.TryApplyImmediate(caster, subject, nanoId, _items, _inventory, DateTime.UtcNow, gmApply: true))
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
