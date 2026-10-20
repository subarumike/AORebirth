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

        /// <param name="scripted">
        /// The function names its target NPC by spawn hash (OTAF Official Notice 297243 on an Omni-AF Private "OIUR"): a
        /// quest effect on that NPC, landed outside combat rules as a GM apply is, though the nano carries the hostile
        /// flag and the NPC is NoCombat.
        /// </param>
        public static bool TryExecute(
            Character target,
            Character? source,
            ItemSpell spell,
            IItemBuilder items,
            IInventoryRepository inventory,
            bool scripted = false)
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
                    FunctionType.CastNano => CastNano(caster, target, spell, items, inventory, nowUtc, scripted),
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
            DateTime nowUtc,
            bool scripted = false)
        {
            if (!spell.TryReadInt(0, out int nanoId) || nanoId <= 0)
                return false;

            return NanoRuntime.TryApplyImmediate(caster, target, nanoId, items, inventory, nowUtc, gmApply: scripted);
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

            // A child whose own use requirements pick who gets it (Resolve 2PZ7 297276: NPCFamily 0 and quest 2PZ7
            // active) is a quest effect: it lands on exactly those, outside combat rules.
            ItemTemplate childTemplate = items.CreateTemplate(nanoId, nanoId, quality: 1);
            bool picksRecipients = childTemplate.Actions.Exists(a => a.ActionType == (int)ActionType.ToUse && a.Requirements.Count > 0);

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
                if (picksRecipients)
                {
                    if (!childTemplate.MeetsActionRequirements(stat => candidate.Stats.GetOrZero(stat), ActionType.ToUse,
                            candidate.ResolveRequirement))
                        continue;
                    NanoRuntime.TryApplyImmediate(caster, candidate, nanoId, items, inventory, nowUtc, gmApply: true);
                    continue;
                }
                if (!AcceptsRecipient(caster, candidate, child))
                    continue;

                NanoRuntime.TryApplyImmediate(caster, candidate, nanoId, items, inventory, nowUtc);
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

            for (int i = 0; i < team.MemberIds.Count; i++)
            {
                if (!manager.FindPlayer(team.MemberIds[i], out Player? mate) || mate == null || mate.IsDead)
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

        internal static bool AcceptsRecipient(Character source, Character candidate, NanoSpell child)
        {
            if (candidate.IsDead)
                return false;

            CanFlags can = ReadCan(child);
            bool self = ReferenceEquals(source, candidate);
            if (child.IsHostile && !self && IsSparedByNpcArea(source, candidate))
                return false;
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
