namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Enums;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    public sealed partial class InventoryActionService
    {
        readonly object _skillLockGate = new();
        readonly Dictionary<(Player Player, int Stat), DateTimeOffset> _skillLocks = new();
        internal TimeProvider Clock { get; set; } = TimeProvider.System;

        static bool IsStim(Item item) => item.LowId is 291043 or 291044 || item.HighId is 291043 or 291044;
        public static bool IsVitalItem(Item item) => IsStim(item)
            || item.LowId is 291082 or 291083 || item.HighId is 291082 or 291083;

        /// <summary>Exact Legacy health/nano stim and reusable recharger specialization.</summary>
        public bool TryUseVitalItem(Player player, Identity slot, Item item)
        {
            if (!IsVitalItem(item) || player.Session?.State != SessionState.InPlay
                || player.IsDead || player.IsPersistenceQuarantined || item.StackCount <= 0
                || !TryResolveOwnedSlot(player, slot, out Container page, out Item current)
                || !ReferenceEquals(item, current)) return false;

            // Legacy's specialization deliberately resolves template Hit/LockSkill directly;
            // its generic Sitting/InDuel requirement evaluator was not the accepted route.
            bool consumed = IsStim(item);
            if (!TryResolveVitalEffects(item, consumed, out int health, out int nano, out int skill, out int seconds))
                return false;
            lock (player.PersistenceGate)
            {
                DateTimeOffset now = Clock.GetUtcNow();
                lock (_skillLockGate)
                    if (_skillLocks.TryGetValue((player, skill), out DateTimeOffset until) && until > now) return false;
                int previousCount = item.StackCount;
                int beforeHealth = player.Stats.GetOrZero(CharacterStat.Health);
                int beforeNano = player.Stats.GetOrZero(CharacterStat.CurrentNano);
                int restoredHealth = (int)Math.Min(Math.Max(0L, health), Math.Max(0L,
                    (long)Math.Max(1, player.Stats.GetOrZero(CharacterStat.MaxHealth)) - beforeHealth));
                int restoredNano = (int)Math.Min(Math.Max(0L, nano), Math.Max(0L,
                    (long)Math.Max(0, player.Stats.GetOrZero(CharacterStat.MaxNanoEnergy)) - beforeNano));
                int finalHealth = checked(beforeHealth + restoredHealth);
                int finalNano = checked(beforeNano + restoredNano);
                var stats = new List<StatRecord>();
                if (restoredHealth > 0) stats.Add(new StatRecord { StatId = (int)CharacterStat.Health, StatValue = finalHealth });
                if (restoredNano > 0) stats.Add(new StatRecord { StatId = (int)CharacterStat.CurrentNano, StatValue = finalNano });
                bool retired = consumed && previousCount == 1;
                InventoryRowChange[] changes = consumed
                    ? [new InventoryRowChange(item, retired
                        ? new Identity { Type = IdentityType.None, Instance = player.Identity.Instance } : page.Identity,
                        retired ? item.InstanceId : slot.Instance, retired ? previousCount : previousCount - 1, retired)]
                    : [];
                return TryCommit(player, changes,
                    () => IsCurrent(page, slot.Instance, item, item.InstanceId) && item.StackCount == previousCount
                        && player.Stats.GetOrZero(CharacterStat.Health) == beforeHealth
                        && player.Stats.GetOrZero(CharacterStat.CurrentNano) == beforeNano,
                    () =>
                    {
                        if (retired) page.Content.Remove(slot.Instance);
                        else if (consumed) item.StackCount = previousCount - 1;
                        foreach (StatRecord stat in stats)
                            player.Stats.Set((CharacterStat)stat.StatId, stat.StatValue, StatDetail.Base, dirty: true);
                        lock (_skillLockGate) _skillLocks[(player, skill)] = Clock.GetUtcNow().AddSeconds(seconds);
                        player.Session?.Send(new TemplateActionMessage
                        {
                            Identity = player.Identity, ItemLowId = item.LowId, ItemHighId = item.HighId,
                            Quality = item.Quality, Placement = slot, Unknown1 = 1, Unknown2 = 3
                        });
                        player.FlushDirtyStats();
                        if (restoredHealth > 0) player.Session?.Send(new ChatTextMessage
                        {
                            Identity = player.Identity, Text = $"You healed yourself for {restoredHealth} points."
                        });
                        player.Session?.Send(new CharacterActionMessage
                        {
                            Identity = player.Identity, Action = CharacterActionType.SpecialUnavailable,
                            Target = Identity.None, Parameter1 = skill, Parameter2 = seconds
                        });
                        if (retired) player.Session?.Send(new CharacterActionMessage
                        {
                            Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot
                        });
                    }, finalStats: stats);
            }
        }

        // Called by the existing inventory-move owner tick, never a sleeping worker/thread.
        public void Tick(Playfield playfield)
        {
            DateTimeOffset now = Clock.GetUtcNow();
            List<(Player Player, int Stat)> expired;
            lock (_skillLockGate)
            {
                expired = _skillLocks.Where(entry => entry.Value <= now
                    && ReferenceEquals(entry.Key.Player.Playfield, playfield)).Select(entry => entry.Key).ToList();
                foreach (var key in expired) _skillLocks.Remove(key);
                foreach (var key in _skillLocks.Keys.Where(key => key.Player.IsPersistenceQuarantined
                    || ((key.Player.Session == null || key.Player.Session.State == SessionState.Closed)
                        && _skillLocks[key] <= now)).ToArray())
                    _skillLocks.Remove(key);
            }
            foreach (var key in expired)
            {
                if (key.Player.IsPersistenceQuarantined || key.Player.Session?.State != SessionState.InPlay) continue;
                key.Player.Session.Send(new CharacterActionMessage
                {
                    Identity = key.Player.Identity, Action = CharacterActionType.SpecialAvailable,
                    Target = Identity.None, Parameter1 = 0, Parameter2 = key.Stat
                });
            }
        }

        static bool TryResolveVitalEffects(Item item, bool stim, out int health, out int nano, out int skill, out int seconds)
        {
            health = nano = 0;
            skill = (int)(stim ? CharacterStat.FirstAid : CharacterStat.Treatment);
            seconds = stim ? 40 : 15;
            if (item.SpellList.TryGetValue(EventType.OnUse, out var spells))
            {
                foreach (ItemSpell spell in spells)
                {
                    if (spell.FunctionType == (int)FunctionType.Hit)
                    {
                        if (!ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int stat)
                            || !TryResolveVitalHit(spell, out int amount)) return false;
                        if (stat == (int)CharacterStat.Health || stat == (int)CharacterStat.MaxHealth)
                            health = Math.Max(health, amount);
                        else if (stat == (int)CharacterStat.CurrentNano || stat == (int)CharacterStat.NanoPool
                            || stat == (int)CharacterStat.MaxNanoEnergy) nano = Math.Max(nano, amount);
                    }
                    else if (spell.FunctionType == (int)FunctionType.LockSkill)
                    {
                        if (!ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int first)
                            || !ItemUseFunctions.TryReadInt(spell.Arguments, 1, out int second)) return false;
                        if (Enum.IsDefined(typeof(CharacterStat), first)) { skill = first; seconds = second; }
                        else
                        {
                            skill = second;
                            if (!ItemUseFunctions.TryReadInt(spell.Arguments, 2, out seconds)) return false;
                        }
                        if (seconds <= 0 || !Enum.IsDefined(typeof(CharacterStat), skill)) return false;
                    }
                }
            }
            // Existing accepted Legacy QL interpolation, not a new guessed fallback.
            int quality = Math.Clamp(item.Quality, 1, stim ? 200 : 100);
            int fallback = stim ? 30 + (2400 - 30) * (quality - 1) / 199
                : 200 + (5000 - 200) * (quality - 1) / 99;
            if (health <= 0) health = fallback;
            if (nano <= 0) nano = fallback;
            return true;
        }

        static bool TryResolveVitalHit(ItemSpell spell, out int amount)
        {
            amount = 0;
            if (!ItemUseFunctions.TryReadInt(spell.Arguments, 1, out int minimum) || minimum == int.MinValue) return false;
            int maximum = minimum;
            if (spell.Arguments.Count >= 3)
            {
                if (!ItemUseFunctions.TryReadInt(spell.Arguments, 2, out maximum) || maximum == int.MinValue) return false;
                if (minimum < 0 && maximum > 0) maximum = minimum; // Legacy Hit stat/amount/AC-type contract.
            }
            if (minimum > maximum) (minimum, maximum) = (maximum, minimum);
            int delta = minimum == maximum ? minimum : (int)Random.Shared.NextInt64(minimum, (long)maximum + 1);
            amount = Math.Abs(delta);
            return true;
        }
    }
}
