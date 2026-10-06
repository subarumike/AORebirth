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
    /// Authoritative item, world-object and perk action use with the template's AttackDelay. Items and perks share one
    /// queue per player: each use is checked in full (requirements, target, range, line of sight) when it is asked
    /// for, then waits its turn; its delay starts when it reaches the front. When the delay ends only what keeps it
    /// safe is checked again (still in play, the item still in its slot, the perk held and off cooldown, the target
    /// still alive and attackable). A queued inventory item stays locked until it runs or is cancelled. One per playfield.
    /// </summary>
    public sealed class ItemUseService
    {
        /// <summary>Upper bound on a template delay so a bad stat cannot lock an item indefinitely.</summary>
        public const int MaxDelayCentiseconds = 6000;

        /// <summary>
        /// Uses one player may have waiting, the running one included. A server bound against queue spam, not a
        /// value taken from live.
        /// </summary>
        public const int MaxQueuedUses = 4;

        const string FailedText = "You could not use that item.";

        private readonly object _gate = new();
        private readonly Dictionary<int, List<PendingItemUse>> _queues = new();
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

        /// <summary>True while the player has a use running or queued.</summary>
        public bool HasPending(int characterId)
        {
            lock (_gate)
                return _queues.ContainsKey(characterId);
        }

        /// <summary>True when the player's queue cannot take another use.</summary>
        public bool IsQueueFull(int characterId)
        {
            lock (_gate)
                return _queues.TryGetValue(characterId, out List<PendingItemUse>? queue) && queue.Count >= MaxQueuedUses;
        }

        public ItemUseStart TryBegin(Player player, Identity slot, Item item)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(item);

            if (!ReferenceEquals(player.Playfield, _playfield)
                || IsQueueFull(player.Identity.Instance)
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
                    ?? UseTargetRules.Revalidate(player, can, useTarget) switch
                    {
                        null => null,
                        UseTargetRules.Failure.NoFightingTarget => UseTargetRules.LostFightingTarget,
                        { } failure => "target " + failure
                    },
                () => item.ExecuteUse(player, slot, _inventoryRepository, _items, useTarget),
                lockTarget: locked => item.Locked = locked,
                ResolveDelayCentiseconds(item));
            return Enqueue(pending);
        }

        /// <summary>
        /// World item / static dynel Use, queued like an inventory use. Reach and use requirements are checked when it
        /// is asked for; before OnUse runs, only that the object is still there and the user on its playfield.
        /// </summary>
        public ItemUseStart TryBegin(Player player, StaticDynel dynel)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(dynel);

            if (!ReferenceEquals(player.Playfield, _playfield)
                || IsQueueFull(player.Identity.Instance)
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
                lockTarget: null,
                ResolveDelayCentiseconds(dynel));
            return Enqueue(pending);
        }

        /// <summary>
        /// UsePerk (Perk Actions button). Checked in full now (held, unlocked, requirements, target, range, line of sight)
        /// and attack vs defense rating decides at once: a miss is answered straight away (evade feedback and the OnFailure
        /// lock) with no QueuePerk, as in live capture 2026-10-02T13:02:33Z. A landing action is acknowledged straight away
        /// with QueuePerk (CharacterAction 0x50, Parameter1 2, Parameter2 the AttackDelay in centiseconds), even behind
        /// other queued uses, so the client shows it in its queue: live sent three in a row while one was still running
        /// (capture 2026-10-06T15:00:03Z). It is performed when it reaches the front and its delay runs out.
        /// </summary>
        public ItemUseStart TryBeginPerkAction(Player player, int hash)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (!ReferenceEquals(player.Playfield, _playfield) || _moves.HasPending(player.Identity.Instance))
                return ItemUseStart.Rejected;

            if (IsQueued(player.Identity.Instance, hash))
            {
                ClientFeedback.Send(player, ClientFeedback.AlreadyRunningAction);
                return ItemUseStart.Rejected;
            }

            if (IsQueueFull(player.Identity.Instance))
                return ItemUseStart.Rejected;

            if (!player.TryPreparePerkAction(hash, out Player.PerkActionUse? use) || use == null)
                return ItemUseStart.Rejected;

            if (!player.PerkActionLands(use))
            {
                player.FailPerkAction(use, _inventoryRepository);
                return ItemUseStart.Executed;
            }

            int delay = ClampDelay(use.Template.Stats.GetValueOrDefault(CharacterStat.AttackDelay));
            var pending = new PendingItemUse(
                player,
                string.Format(CultureInfo.InvariantCulture, "perkAction={0:X8} template={1}", hash, use.Template.Id),
                () => player.RevalidatePerkAction(use, starting: false),
                () => player.ExecutePerkAction(use, _inventoryRepository),
                lockTarget: null,
                delay,
                perkHash: hash);
            ItemUseStart start = Enqueue(pending);
            if (start != ItemUseStart.Rejected)
                player.Session?.Send(new CharacterActionMessage
                {
                    Identity = player.Identity,
                    Action = CharacterActionType.QueuePerk,
                    Target = Identity.None,
                    Parameter1 = 2,
                    Parameter2 = delay
                });
            return start;
        }

        bool IsQueued(int characterId, int perkHash)
        {
            lock (_gate)
                return _queues.TryGetValue(characterId, out List<PendingItemUse>? queue)
                    && queue.Exists(pending => pending.PerkHash == perkHash);
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

        /// <summary>
        /// Adds a checked use to the player's queue. At the front of an empty queue it starts at once: a use with no
        /// delay runs now (Executed or Rejected), anything else is Started.
        /// Behind other uses it waits (Started). Rejected when the queue is full.
        /// </summary>
        ItemUseStart Enqueue(PendingItemUse pending)
        {
            int id = pending.Player.Identity.Instance;
            lock (_gate)
            {
                if (!_queues.TryGetValue(id, out List<PendingItemUse>? queue))
                    queue = [];
                if (queue.Count >= MaxQueuedUses)
                    return ItemUseStart.Rejected;

                queue.Add(pending);
                _queues[id] = queue;
                pending.SetLocked(true);
                if (queue.Count > 1)
                    return ItemUseStart.Started;
            }

            ItemUseStart result = StartFront(pending, immediate: true);
            AdvanceQueue(id);
            return result;
        }

        /// <summary>
        /// Starts the use at the front of its queue: Started while its delay runs; otherwise it finishes here and leaves
        /// the queue. <paramref name="immediate"/>: asked for just now with nothing ahead, so its gates were checked a
        /// moment ago and a failure is answered by the caller rather than with <see cref="FailedText"/>.
        /// </summary>
        ItemUseStart StartFront(PendingItemUse pending, bool immediate)
        {
            pending.Started = true;
            if (pending.DelayCentiseconds > 0)
            {
                pending.RemainingSeconds = pending.DelayCentiseconds * 0.01;
                return ItemUseStart.Started;
            }

            RemoveFront(pending);
            if (immediate)
            {
                pending.SetLocked(false);
                return pending.Execute() ? ItemUseStart.Executed : ItemUseStart.Rejected;
            }

            return Complete(pending) ? ItemUseStart.Executed : ItemUseStart.Rejected;
        }

        void RemoveFront(PendingItemUse pending)
        {
            int id = pending.Player.Identity.Instance;
            lock (_gate)
            {
                if (_queues.TryGetValue(id, out List<PendingItemUse>? queue) && queue.Count > 0 && ReferenceEquals(queue[0], pending))
                {
                    queue.RemoveAt(0);
                    if (queue.Count == 0)
                        _queues.Remove(id);
                }
            }
        }

        /// <summary>Starts waiting uses in turn until one is running with a delay or the queue is empty.</summary>
        void AdvanceQueue(int characterId)
        {
            while (true)
            {
                PendingItemUse? next;
                lock (_gate)
                {
                    if (!_queues.TryGetValue(characterId, out List<PendingItemUse>? queue) || queue.Count == 0)
                        return;
                    next = queue[0];
                    if (next.Started)
                        return;
                }

                StartFront(next, immediate: false);
            }
        }

        /// <summary>Drops every running and queued use of the player, unlocking queued items.</summary>
        public void CancelPending(int characterId)
        {
            List<PendingItemUse>? queue;
            lock (_gate)
            {
                if (!_queues.Remove(characterId, out queue))
                    return;
            }

            foreach (PendingItemUse pending in queue)
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
                if (_queues.Count == 0)
                    return;

                List<int> remove = [];
                foreach (KeyValuePair<int, List<PendingItemUse>> pair in _queues)
                {
                    List<PendingItemUse> queue = pair.Value;
                    if (queue.Count == 0 || !ReferenceEquals(queue[0].Player.Playfield, _playfield))
                    {
                        stale.AddRange(queue);
                        remove.Add(pair.Key);
                        continue;
                    }

                    PendingItemUse front = queue[0];
                    if (!front.Started)
                        continue;

                    front.RemainingSeconds -= deltaTime;
                    if (front.RemainingSeconds > 0)
                        continue;

                    due.Add(front);
                    queue.RemoveAt(0);
                    if (queue.Count == 0)
                        remove.Add(pair.Key);
                }

                foreach (int id in remove)
                    _queues.Remove(id);
            }

            foreach (PendingItemUse pending in stale)
                pending.SetLocked(false);

            foreach (PendingItemUse pending in due)
            {
                Complete(pending);
                AdvanceQueue(pending.Player.Identity.Instance);
            }
        }

        bool Complete(PendingItemUse pending)
        {
            // DestroyOne/ConsumeCharge refuse locked items.
            pending.SetLocked(false);

            Player player = pending.Player;
            string? failure = RevalidatePlayer(player) ?? pending.Revalidate();
            if (failure == null && pending.Execute())
                return true;

            _logger.Warn(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Delayed item use aborted char={0} {1}: {2}",
                    player.Identity.Instance,
                    pending.Description,
                    failure ?? "OnUse spells returned false"));
            if (failure == UseTargetRules.LostFightingTarget)
                ClientFeedback.Send(player, ClientFeedback.RequiresFightingTarget);
            else
                Tell(player, FailedText);
            return false;
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
            if (current.UsesFightingTarget && player.TryResolveFightingTarget() == null)
                return UseTargetRules.LostFightingTarget;
            if (!current.CanStillUse(player))
                return "use gates failed";
            return null;
        }

        string? RevalidateDynel(Player player, StaticDynel dynel)
        {
            if (!_playfield.GetRequiredService<DynelRegistry>().TryGet(dynel.Identity, out Dynel? current)
                || !ReferenceEquals(current, dynel))
                return "dynel gone";
            if (!dynel.CanStillUse(player))
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
        /// One queued use; its delay starts when it reaches the front. <see cref="Revalidate"/> re-checks the used thing's own safety gates when the delay
        /// ends (null = still valid); the player gates are shared. Only inventory items lock while queued.
        /// </summary>
        sealed class PendingItemUse(
            Player player,
            string description,
            Func<string?> revalidate,
            Func<bool> execute,
            Action<bool>? lockTarget,
            int delayCentiseconds,
            int? perkHash = null)
        {
            public Player Player { get; } = player;

            public string Description { get; } = description;

            public int DelayCentiseconds { get; } = delayCentiseconds;

            /// <summary>The perk action hash, for refusing the same action twice; null for item uses.</summary>
            public int? PerkHash { get; } = perkHash;

            public bool Started { get; set; }

            public double RemainingSeconds { get; set; }

            public string? Revalidate() => revalidate();

            public bool Execute() => execute();

            public void SetLocked(bool locked) => lockTarget?.Invoke(locked);
        }
    }
}
