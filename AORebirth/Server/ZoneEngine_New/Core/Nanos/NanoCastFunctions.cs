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
    using ZoneEngine_New.Core.Playfield.Locality;
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
                    FunctionType.TeamCastNano => TeamCastNano(caster, target, spell, items, inventory, nowUtc),
                    FunctionType.PlayfieldNano => PlayfieldNano(caster, spell, items, inventory, nowUtc),
                    FunctionType.CastOnPets => CastOnPets(caster, spell, items, inventory, nowUtc),
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
                if (AcceptsRecipient(caster, center, child, area: true))
                    NanoRuntime.TryApplyImmediate(caster, center, nanoId, items, inventory, nowUtc, areaSpread: true);
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
                if (!AcceptsRecipient(caster, candidate, child, area: true))
                    continue;
                // The area spreads from its centre only to what that centre can see: a wall or a closed door between
                // them shelters the candidate (area nukes such as 100198).
                if (!center.HasLineOfSightTo(candidate))
                    continue;

                NanoRuntime.TryApplyImmediate(caster, candidate, nanoId, items, inventory, nowUtc, areaSpread: true);
            }

            return true;
        }

        /// <summary>
        /// Casts the child on the team of the character the function applies to, still from
        /// <paramref name="caster"/>. A self-cast team nano names its caster (User / Wearer); one aimed at a
        /// Target, such as 90449 (child 90445), buffs that target's team. Without a team only that character
        /// gets it.
        /// </summary>
        static bool TeamCastNano(
            Character caster,
            Character recipient,
            ItemSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory,
            DateTime nowUtc)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
                return false;

            if (recipient is not Player player)
            {
                if (!recipient.IsDead)
                    NanoRuntime.TryApplyImmediate(caster, recipient, nanoId, items, inventory, nowUtc);
                return true;
            }

            Playfield? playfield = player.Playfield;
            TeamService? teams = playfield?.GetService<TeamService>();
            PlayfieldManager? manager = playfield?.GetService<PlayfieldManager>();
            TeamSnapshot? team = teams?.GetTeam(player);
            if (team == null || manager == null)
            {
                if (!player.IsDead)
                    NanoRuntime.TryApplyImmediate(caster, player, nanoId, items, inventory, nowUtc);
                return true;
            }

            // Only teammates the team's centre can see: on its playfield and in its client's cell visibility
            // (PlayfieldLocality). The centre itself always gets it.
            PlayfieldLocality? locality = playfield?.GetService<PlayfieldLocality>();
            for (int i = 0; i < team.MemberIds.Count; i++)
            {
                if (!manager.FindPlayer(team.MemberIds[i], out Player? mate) || mate == null || mate.IsDead)
                    continue;
                if (!ReferenceEquals(mate, player)
                    && (!ReferenceEquals(mate.Playfield, playfield) || locality == null || !locality.IsVisibleTo(mate, player)))
                    continue;

                NanoRuntime.TryApplyImmediate(caster, mate, nanoId, items, inventory, nowUtc);
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

        /// <summary>
        /// CastOnPets [nano id]: the child nano lands on each of the caster's living pets whose stats meet the child's
        /// ToUse criteria (Channel Rage 227693's tiers 227683-227692: NPCFamily = 97, the Metaphysicist's demon). With no
        /// such pet it does nothing, successfully.
        /// </summary>
        static bool CastOnPets(
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

            NpcCharacter[] pets = [.. caster.OwnedPets.All];
            foreach (NpcCharacter pet in pets)
            {
                if (pet.IsDead || !ReferenceEquals(pet.Playfield, caster.Playfield))
                    continue;
                if (!child.MeetsActionRequirements(stat => pet.Stats.Get(stat), ActionType.ToUse, pet.ResolveRequirement))
                    continue;

                NanoRuntime.TryApplyImmediate(caster, pet, nanoId, items, inventory, nowUtc);
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

        /// <param name="area">
        /// An AreaCastNano spread: a hostile child spares its caster, friendly pets, and NPCs that cannot be attacked
        /// (or that an NPC caster leaves alone); a friendly one reaches all.
        /// </param>
        internal static bool AcceptsRecipient(Character source, Character candidate, NanoSpell child, bool area = false)
        {
            if (candidate.IsDead)
                return false;

            CanFlags can = ReadCan(child);
            bool self = ReferenceEquals(source, candidate);
            if (child.IsHostile && !self && IsSparedByNpcArea(source, candidate))
                return false;
            if (area)
                return !child.IsHostile || IsCaughtByHostileArea(source, candidate);
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

        /// <summary>
        /// A hostile area effect catches friend and foe alike except its source, pets owned by its source or team/raid,
        /// NPCs that cannot be attacked, and the NPCs an NPC source leaves alone (<see cref="IsSparedByNpcArea"/>).
        /// </summary>
        internal static bool IsCaughtByHostileArea(Character source, Character candidate)
            => !ReferenceEquals(source, candidate)
                && candidate is not NpcCharacter { Attackable: false }
                && !IsFriendlyPet(source, candidate)
                && !IsSparedByNpcArea(source, candidate);

        /// <summary>Pet affiliation follows its owner; a raid uses the existing team membership rules.</summary>
        internal static bool IsFriendlyPet(Character source, Character candidate)
        {
            if (candidate is not NpcCharacter { PetOwner: Character owner })
                return false;

            Character sourceOwner = source is NpcCharacter { PetOwner: Character casterOwner } ? casterOwner : source;
            if (ReferenceEquals(sourceOwner, owner))
                return true;

            return sourceOwner is Player sourcePlayer && owner is Player ownerPlayer
                && (sourcePlayer.Playfield ?? ownerPlayer.Playfield)?.GetService<TeamService>()
                    ?.AreTeammates(sourcePlayer, ownerPlayer) == true;
        }

        /// <summary>
        /// An NPC's hostile area nano leaves other NPCs alone unless it is fighting that NPC.
        /// Players' pets are fair game: they fight for their master. NPC-owned pets are spared.
        /// </summary>
        static bool IsSparedByNpcArea(Character source, Character candidate)
        {
            if (source.IsPlayer || candidate is not NpcCharacter npc)
                return false;
            if (IsPlayerPet(npc))
                return false;

            return source.FightingTarget != npc.Identity;
        }

        static bool IsPlayerPet(NpcCharacter npc)
        {
            int master = npc.Stats.GetOrZero(CharacterStat.PetMaster);
            if (master == 0)
                return false;

            PlayfieldManager? manager = npc.Playfield?.GetService<PlayfieldManager>();
            return manager != null && manager.FindPlayer(master, out _);
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
