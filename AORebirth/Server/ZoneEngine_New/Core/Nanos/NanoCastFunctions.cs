namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Threading;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Teams;

    /// <summary>
    /// Instant item/nano functions that land a child nano without touching cast state.
    /// </summary>
    internal static class NanoCastFunctions
    {
        const int MaxDepth = 8;

        static readonly AsyncLocal<int> Depth = new();

        public static bool TryExecute(
            Character target,
            Character? source,
            ItemSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(spell);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(inventory);

            Character caster = source ?? target;
            DateTime nowUtc = DateTime.UtcNow;
            int depth = Depth.Value;
            if (depth >= MaxDepth)
                return false;

            Depth.Value = depth + 1;
            try
            {
                return ((FunctionType)spell.FunctionType) switch
                {
                    FunctionType.CastNano => CastNano(caster, target, spell, items, inventory, nowUtc),
                    FunctionType.CastNanoIfPossible => CastNanoIfPossible(caster, target, spell, items, inventory, nowUtc),
                    FunctionType.NpcCastNanoIfPossible => CastNanoIfPossible(caster, target, spell, items, inventory, nowUtc),
                    FunctionType.CastNanoIfPossibleOnFightTarget => CastNanoOnFightTarget(caster, spell, items, inventory, nowUtc),
                    FunctionType.NpcCastNanoIfPossibleOnFightTarget => CastNanoOnFightTarget(caster, spell, items, inventory, nowUtc),
                    FunctionType.AreaCastNano => AreaCastNano(caster, target, spell, items, inventory, nowUtc),
                    FunctionType.TeamCastNano => TeamCastNano(caster, spell, items, inventory, nowUtc),
                    FunctionType.PlayfieldNano => PlayfieldNano(caster, spell, items, inventory, nowUtc),
                    _ => false
                };
            }
            finally
            {
                Depth.Value = depth;
            }
        }

        static bool CastNano(
            Character caster,
            Character target,
            ItemSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory,
            DateTime nowUtc)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
                return false;

            return NanoRuntime.TryApplyImmediate(caster, target, nanoId, items, inventory, nowUtc);
        }

        /// <summary>
        /// Same nano id as <see cref="FunctionType.CastNano"/>. A child that cannot land
        /// does not fail the parent event.
        /// </summary>
        static bool CastNanoIfPossible(
            Character caster,
            Character target,
            ItemSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory,
            DateTime nowUtc)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
                return false;

            NanoRuntime.TryApplyImmediate(caster, target, nanoId, items, inventory, nowUtc);
            return true;
        }

        /// <summary>
        /// Same nano id as <see cref="FunctionType.CastNano"/>, aimed at the caster's fighting target.
        /// No current target is a successful no-op.
        /// </summary>
        static bool CastNanoOnFightTarget(
            Character caster,
            ItemSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory,
            DateTime nowUtc)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
                return false;

            Character? fightTarget = caster.TryResolveFightingTarget();
            if (fightTarget != null)
                NanoRuntime.TryApplyImmediate(caster, fightTarget, nanoId, items, inventory, nowUtc);
            return true;
        }

        static bool AreaCastNano(
            Character caster,
            Character target,
            ItemSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory,
            DateTime nowUtc)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
                return false;
            if (!spell.TryReadInt(1, out int radius) || radius < 0)
                return false;
            if (!TryResolveChild(items, nanoId, out NanoSpell? child) || child == null)
                return false;

            // ItemTarget.Target (nukes such as 28638) is centered on the cast recipient.
            // User and Wearer stay centered on the caster.
            Character center = spell.Target == (int)ItemTarget.Target ? target : caster;
            Playfield? playfield = center.Playfield ?? caster.Playfield;
            DynelRegistry? registry = playfield?.GetService<DynelRegistry>();
            if (registry == null)
            {
                if (AcceptsRecipient(caster, center, child))
                    NanoRuntime.TryApplyImmediate(caster, center, nanoId, items, inventory, nowUtc);
                return true;
            }

            foreach (Dynel dynel in registry.Dynels())
            {
                if (dynel is not Character candidate)
                    continue;
                if (!ReferenceEquals(candidate.Playfield, playfield))
                    continue;
                if (center.Distance3D(candidate) > radius)
                    continue;
                if (!AcceptsRecipient(caster, candidate, child))
                    continue;

                NanoRuntime.TryApplyImmediate(caster, candidate, nanoId, items, inventory, nowUtc);
            }

            return true;
        }

        static bool TeamCastNano(
            Character caster,
            ItemSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory,
            DateTime nowUtc)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
                return false;

            if (caster is not Player player)
            {
                if (!caster.IsDead)
                    NanoRuntime.TryApplyImmediate(caster, caster, nanoId, items, inventory, nowUtc);
                return true;
            }

            Playfield? playfield = player.Playfield;
            TeamService? teams = playfield?.GetService<TeamService>();
            PlayfieldManager? manager = playfield?.GetService<PlayfieldManager>();
            TeamSnapshot? team = teams?.GetTeam(player);
            if (team == null || manager == null)
            {
                if (!player.IsDead)
                    NanoRuntime.TryApplyImmediate(player, player, nanoId, items, inventory, nowUtc);
                return true;
            }

            for (int i = 0; i < team.MemberIds.Count; i++)
            {
                if (!manager.FindPlayer(team.MemberIds[i], out Player? mate) || mate == null || mate.IsDead)
                    continue;

                NanoRuntime.TryApplyImmediate(player, mate, nanoId, items, inventory, nowUtc);
            }

            return true;
        }

        static bool PlayfieldNano(
            Character caster,
            ItemSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory,
            DateTime nowUtc)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
                return false;
            if (!TryResolveChild(items, nanoId, out NanoSpell? child) || child == null)
                return false;

            Playfield? playfield = caster.Playfield;
            DynelRegistry? registry = playfield?.GetService<DynelRegistry>();
            if (registry == null)
            {
                if (AcceptsRecipient(caster, caster, child))
                    NanoRuntime.TryApplyImmediate(caster, caster, nanoId, items, inventory, nowUtc);
                return true;
            }

            foreach (Dynel dynel in registry.Dynels())
            {
                if (dynel is not Character candidate)
                    continue;
                if (!ReferenceEquals(candidate.Playfield, playfield))
                    continue;
                if (!AcceptsRecipient(caster, candidate, child))
                    continue;

                NanoRuntime.TryApplyImmediate(caster, candidate, nanoId, items, inventory, nowUtc);
            }

            return true;
        }

        static bool TryResolveChild(IItemBuilder items, int nanoId, out NanoSpell? spell)
        {
            spell = null;
            ItemTemplate template = items.CreateTemplate(nanoId, nanoId, quality: 1);
            if (template.Id != nanoId)
                return false;

            spell = NanoSpell.From(template);
            return true;
        }

        internal static bool AcceptsRecipient(Character source, Character candidate, NanoSpell child)
        {
            if (candidate.IsDead)
                return false;

            CanFlags can = ReadCan(child);
            bool self = ReferenceEquals(source, candidate);
            if ((can & (CanFlags.ApplyOnSelf | CanFlags.ApplyOnFriendly | CanFlags.ApplyOnHostile)) == 0)
            {
                // Area nukes such as Volcanic Eruption store Can as 0 and IsHostile on the child.
                if (child.IsHostile)
                    return CombatRules.CanAttack(source, candidate);

                return self;
            }

            bool accepted;
            if (self)
                accepted = (can & CanFlags.ApplyOnSelf) != 0;
            else if (candidate.IsPlayer)
                accepted = (can & CanFlags.ApplyOnFriendly) != 0;
            else
                accepted = candidate is NpcCharacter npc
                    && npc.Attackable
                    && (can & CanFlags.ApplyOnHostile) != 0;

            return !child.IsHostile || (accepted && CombatRules.CanAttack(source, candidate));
        }

        static CanFlags ReadCan(NanoSpell child)
        {
            if (!child.Stats.TryGetValue(CharacterStat.Can, out int value) || StatCollection.IsUnset(value))
                return 0;

            // Bit 31 (ApplyOnFightingTarget) is negative as a signed int. Keep the flag word.
            return (CanFlags)(uint)value;
        }
    }
}
