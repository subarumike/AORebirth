namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    public enum ItemUseStart
    {
        Rejected,
        Started,
        Executed,
    }

    /// <summary>
    /// Authoritative inventory item Use with the template's AttackDelay. The item stays locked
    /// while the delay runs; every gate is checked again before OnUse runs. One per playfield.
    /// </summary>
    public sealed class ItemUseService
    {
        /// <summary>Upper bound on a template delay so a bad stat cannot lock an item indefinitely.</summary>
        public const int MaxDelayCentiseconds = 6000;

        const string FailedText = "You could not use that item.";

        private readonly object _gate = new();
        private readonly Dictionary<int, PendingItemUse> _pending = new();
        private readonly Playfield _playfield;
        private readonly IZoneLogger _logger;
        private readonly InventoryMoveService _moves;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IItemBuilder _items;

        public ItemUseService(
            Playfield playfield,
            IZoneLogger logger,
            InventoryMoveService moves,
            IInventoryRepository inventoryRepository,
            IItemBuilder items)
        {
            _playfield = playfield ?? throw new ArgumentNullException(nameof(playfield));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _moves = moves ?? throw new ArgumentNullException(nameof(moves));
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public static int ResolveDelayCentiseconds(Item item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return ClampDelay(item.GetStat(CharacterStat.AttackDelay));
        }

        public static int ResolveDelayCentiseconds(StaticDynel dynel)
        {
            ArgumentNullException.ThrowIfNull(dynel);
            return ClampDelay(dynel.Stats.GetOrZero(CharacterStat.AttackDelay));
        }

        static int ClampDelay(int attackDelay)
            => Math.Clamp(StatCollection.Normalize(attackDelay), 0, MaxDelayCentiseconds);

        public bool HasPending(int characterId)
        {
            lock (_gate)
                return _pending.ContainsKey(characterId);
        }

        public ItemUseStart TryBegin(Player player, Identity slot, Item item)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(item);

            if (!ReferenceEquals(player.Playfield, _playfield)
                || HasPending(player.Identity.Instance)
                || _moves.HasPending(player.Identity.Instance))
                return ItemUseStart.Rejected;

            if (item.IsBackpackUse)
                return item.Use(player, slot, _inventoryRepository, _items) ? ItemUseStart.Executed : ItemUseStart.Rejected;

            if (RejectMissingSpawnHash(player, item.Definition))
                return ItemUseStart.Rejected;

            if (!item.CanBeginUse(player))
            {
                if (item.UsesFightingTarget && player.TryResolveFightingTarget() == null)
                    ClientFeedback.Send(player, ClientFeedback.RequiresFightingTarget);
                else
                    RequirementFeedback.SendIfUnmet(player, item.Definition, ActionType.ToUse, resolve: player.ResolvePerkRequirement);
                return ItemUseStart.Rejected;
            }

            // ApplyOnHostile / ApplyOnFightingTarget items are used on a hostile, not on the user (UseTargetRules).
            CanFlags can = UseTargetRules.CanOf(item.Definition);
            Character useTarget = player;
            if (UseTargetRules.IsHostileTargeted(can)
                && !UseTargetRules.TryResolve(player, can, out useTarget, out UseTargetRules.Failure targetFailure))
            {
                UseTargetRules.SendFailure(player, targetFailure, player.TryResolveFightingTarget());
                return ItemUseStart.Rejected;
            }

            int instanceId = item.InstanceId;
            var pending = new PendingItemUse(
                player,
                string.Format(CultureInfo.InvariantCulture, "slot={0}:{1} low={2} instanceId={3} target={4}",
                    slot.Type, slot.Instance, item.LowId, instanceId, useTarget.Identity.Instance),
                () => RevalidateInventory(player, slot, item, instanceId)
                    ?? (UseTargetRules.Revalidate(player, can, useTarget) is { } failure ? "target " + failure : null),
                () => item.ExecuteUse(player, slot, _inventoryRepository, _items, useTarget),
                lockTarget: locked => item.Locked = locked);
            return Begin(pending, ResolveDelayCentiseconds(item));
        }

        /// <summary>
        /// World item / static dynel Use. The template's AttackDelay runs like an inventory use; range,
        /// playfield and use requirements are checked again before OnUse runs.
        /// </summary>
        public ItemUseStart TryBegin(Player player, StaticDynel dynel)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(dynel);

            if (!ReferenceEquals(player.Playfield, _playfield)
                || HasPending(player.Identity.Instance)
                || _moves.HasPending(player.Identity.Instance))
                return ItemUseStart.Rejected;

            if (RejectMissingSpawnHash(player, dynel.Template))
                return ItemUseStart.Rejected;

            if (!dynel.CanBeginUse(player))
            {
                RequirementFeedback.SendIfUnmet(player, dynel.Template, ActionType.ToUse);
                return ItemUseStart.Rejected;
            }

            var pending = new PendingItemUse(
                player,
                string.Format(CultureInfo.InvariantCulture, "dynel={0} template={1}", dynel.Identity, dynel.Template.Id),
                () => RevalidateDynel(player, dynel),
                () => dynel.ExecuteUse(player),
                lockTarget: null);
            return Begin(pending, ResolveDelayCentiseconds(dynel));
        }

        /// <summary>
        /// UsePerk (Perk Actions button). The action template's AttackDelay runs like an item use; the perk is
        /// re-checked (still held, unlocked, requirements, target alive and in AttackRange) before OnUse runs.
        /// </summary>
        public ItemUseStart TryBeginPerkAction(Player player, int hash)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (!ReferenceEquals(player.Playfield, _playfield) || _moves.HasPending(player.Identity.Instance))
                return ItemUseStart.Rejected;

            if (HasPending(player.Identity.Instance))
            {
                ClientFeedback.Send(player, ClientFeedback.AlreadyRunningAction);
                return ItemUseStart.Rejected;
            }

            if (!player.TryPreparePerkAction(hash, out Player.PerkActionUse? use) || use == null)
                return ItemUseStart.Rejected;

            // Attack vs defense rating decides at once. A miss is answered immediately (evade feedback and the
            // OnFailure lock) with no QueuePerk, as in live capture 2026-10-02T13:02:33Z.
            if (!player.PerkActionLands(use))
            {
                player.FailPerkAction(use, _inventoryRepository);
                return ItemUseStart.Executed;
            }

            // A landing action: live answers with QueuePerk (CharacterAction 0x50, Parameter1 2, Parameter2 the
            // action's AttackDelay in centiseconds) and performs it when the delay runs out.
            int delay = ClampDelay(use.Template.Stats.GetValueOrDefault(CharacterStat.AttackDelay));
            player.Session?.Send(new CharacterActionMessage
            {
                Identity = player.Identity,
                Action = CharacterActionType.QueuePerk,
                Target = Identity.None,
                Parameter1 = 2,
                Parameter2 = delay
            });

            var pending = new PendingItemUse(
                player,
                string.Format(CultureInfo.InvariantCulture, "perkAction={0:X8} template={1}", hash, use.Template.Id),
                () => player.RevalidatePerkAction(use),
                () => player.ExecutePerkAction(use, _inventoryRepository),
                lockTarget: null);
            return Begin(pending, delay);
        }

        /// <summary>
        /// A use that would spawn an item or NPC hash the server has no data for is refused before anything runs, with
        /// a chat line naming the hash so the player can report it.
        /// </summary>
        bool RejectMissingSpawnHash(Player player, ItemTemplate template)
        {
            if (!SpawnHashCheck.TryFindMissingHash(template, _playfield.GetRequiredService<IGameData>(), out string hash))
                return false;

            _logger.Warn(string.Format(CultureInfo.InvariantCulture,
                "Use blocked char={0} template={1}: spawn hash {2} is not defined", player.Identity.Instance, template.Id, hash));
            Tell(player, SpawnHashCheck.NotImplementedText(hash));
            return true;
        }

        ItemUseStart Begin(PendingItemUse pending, int delayCentiseconds)
        {
            if (delayCentiseconds <= 0)
                return pending.Execute() ? ItemUseStart.Executed : ItemUseStart.Rejected;

            pending.RemainingSeconds = delayCentiseconds * 0.01;
            pending.SetLocked(true);
            lock (_gate)
            {
                if (_pending.TryAdd(pending.Player.Identity.Instance, pending))
                    return ItemUseStart.Started;
            }

            pending.SetLocked(false);
            return ItemUseStart.Rejected;
        }

        public void CancelPending(int characterId)
        {
            PendingItemUse? pending;
            lock (_gate)
            {
                if (!_pending.Remove(characterId, out pending))
                    return;
            }

            pending.SetLocked(false);
        }

        public void Tick(double deltaTime)
        {
            if (deltaTime <= 0)
                return;

            List<PendingItemUse> due = [];
            List<PendingItemUse> stale = [];
            lock (_gate)
            {
                if (_pending.Count == 0)
                    return;

                List<int> remove = [];
                foreach (KeyValuePair<int, PendingItemUse> pair in _pending)
                {
                    PendingItemUse pending = pair.Value;
                    if (!ReferenceEquals(pending.Player.Playfield, _playfield))
                    {
                        stale.Add(pending);
                        remove.Add(pair.Key);
                        continue;
                    }

                    pending.RemainingSeconds -= deltaTime;
                    if (pending.RemainingSeconds > 0)
                        continue;

                    due.Add(pending);
                    remove.Add(pair.Key);
                }

                foreach (int id in remove)
                    _pending.Remove(id);
            }

            foreach (PendingItemUse pending in stale)
                pending.SetLocked(false);

            foreach (PendingItemUse pending in due)
                Complete(pending);
        }

        void Complete(PendingItemUse pending)
        {
            // DestroyOne/ConsumeCharge refuse locked items.
            pending.SetLocked(false);

            Player player = pending.Player;
            string? failure = RevalidatePlayer(player) ?? pending.Revalidate();
            if (failure == null && pending.Execute())
                return;

            _logger.Warn(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Delayed item use aborted char={0} {1}: {2}",
                    player.Identity.Instance,
                    pending.Description,
                    failure ?? "OnUse spells returned false"));
            Tell(player, FailedText);
        }

        string? RevalidatePlayer(Player player)
        {
            if (player.Session == null || player.Session.State != SessionState.InPlay)
                return "session not InPlay";
            if (player.IsDead || player.IsPersistenceQuarantined)
                return "player dead or quarantined";
            if (!ReferenceEquals(player.Playfield, _playfield))
                return "playfield changed";
            return null;
        }

        static string? RevalidateInventory(Player player, Identity slot, Item item, int instanceId)
        {
            if (!player.Inventory.IsHydrated)
                return "inventory not hydrated";
            if (!player.Inventory.TryGetUseItem(slot, out Item current)
                || !ReferenceEquals(current, item)
                || current.InstanceId != instanceId)
                return "slot changed";
            if (!current.CanBeginUse(player))
                return "use gates failed";
            return null;
        }

        string? RevalidateDynel(Player player, StaticDynel dynel)
        {
            if (!_playfield.GetRequiredService<DynelRegistry>().TryGet(dynel.Identity, out Dynel? current)
                || !ReferenceEquals(current, dynel))
                return "dynel gone";
            if (!dynel.CanBeginUse(player))
                return "use gates failed";
            return null;
        }

        static void Tell(Player player, string text)
        {
            player.Session?.Send(
                new ChatTextMessage
                {
                    Identity = player.Identity,
                    Text = text,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });
        }

        /// <summary>
        /// One delayed use. <see cref="Revalidate"/> re-checks the used thing's own gates (null = still valid);
        /// the player gates are shared. Only inventory items lock while the delay runs.
        /// </summary>
        sealed class PendingItemUse(
            Player player,
            string description,
            Func<string?> revalidate,
            Func<bool> execute,
            Action<bool>? lockTarget)
        {
            public Player Player { get; } = player;

            public string Description { get; } = description;

            public double RemainingSeconds { get; set; }

            public string? Revalidate() => revalidate();

            public bool Execute() => execute();

            public void SetLocked(bool locked) => lockTarget?.Invoke(locked);
        }
    }
}
