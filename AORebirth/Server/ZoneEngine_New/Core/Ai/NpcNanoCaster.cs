namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    using Utility;

    /// <summary>
    /// Casts an NPC's uploaded nanos now and then. Every cast goes through <see cref="NanoRuntime"/>, so
    /// requirements, nano cost, recharge and NCU are the same as for players.
    /// Hostile nanos go on the combat target. Friendly buffs, in or out of combat, go on the caster when it
    /// lacks them, otherwise on a random nearby NPC that does. Heals go on the caster when it is hurt,
    /// otherwise on the most hurt nearby NPC. A pet looks after its owner and the owner's other pets instead of
    /// nearby NPCs. A summon (SummonPet) nano is cast once a fight starts, while that pet slot is free.
    /// </summary>
    /// <summary>Outcome of a heal order.</summary>
    enum HealOrderResult
    {
        Cast,
        OutOfRange,
        Busy,
        NoHeal
    }

    sealed class NpcNanoCaster
    {
        /// <summary>Wait after a cast before the next one.</summary>
        public const double MinCastIntervalSeconds = 8;

        public const double MaxCastIntervalSeconds = 16;

        /// <summary>Wait after a look that found nothing to cast.</summary>
        public const double IdleCheckSeconds = 2;

        /// <summary>Friendly nanos reach NPCs this close when the nano has no AttackRange.</summary>
        public const double DefaultSupportRange = 20;

        /// <summary>Non-buff friendly nanos (heals) only go on characters under this health.</summary>
        public const int HealBelowPercent = 75;

        readonly NpcBrain _brain;
        readonly Dictionary<int, NanoSpell?> _spells = new();
        readonly List<Character> _allies = new();
        readonly List<Dynel> _nearby = new();
        readonly Random _random;
        DateTime _nextAttemptUtc;

        public NpcNanoCaster(NpcBrain brain)
        {
            _brain = brain;
            _random = new Random(brain.Npc.Identity.Instance);
            _nextAttemptUtc = DateTime.UtcNow.AddSeconds(Between(IdleCheckSeconds, MaxCastIntervalSeconds));
        }

        NpcCharacter Npc => _brain.Npc;

        public void Tick(DateTime nowUtc)
        {
            if (nowUtc < _nextAttemptUtc)
                return;
            if (Npc.IsDead || _brain.IsEvading || Npc.IsCastingNano || Npc.IsInNanoRecharge(nowUtc)
                || Npc.UploadedNanoIds.Count == 0 || Npc.Playfield == null)
                return;

            _nextAttemptUtc = nowUtc.AddSeconds(IdleCheckSeconds);
            _allies.Clear();

            List<int> nanos = Npc.UploadedNanoIds;
            int start = _random.Next(nanos.Count);
            for (int i = 0; i < nanos.Count; i++)
            {
                NanoSpell? spell = Resolve(nanos[(start + i) % nanos.Count]);
                if (spell == null)
                    continue;

                Character? target = ChooseTarget(spell, nowUtc);
                if (target == null)
                    continue;

                if (NanoRuntime.TryStartCast(Npc, spell.Id, target.Identity, nowUtc) != NanoCastRefusal.None)
                    continue;

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "NPC nano cast npc={0} nano={1} {2} kind={3} target={4} self={5} targetHp={6}% npcHp={7}%",
                        Npc.Identity.Instance,
                        spell.Id,
                        spell.Name,
                        spell.IsHostile ? "hostile" : IsHeal(spell) ? "heal" : IsBuffLike(spell) ? "buff" : "friendly",
                        target.Identity.Instance,
                        ReferenceEquals(target, Npc),
                        HealthPercent(target),
                        HealthPercent(Npc)));

                _nextAttemptUtc = nowUtc.AddSeconds(Between(MinCastIntervalSeconds, MaxCastIntervalSeconds));
                return;
            }
        }

        /// <summary>
        /// A heal order: casts the first heal this NPC has that can land on <paramref name="target"/> now.
        /// <see cref="HealOrderResult.OutOfRange"/> when a heal exists but the target is too far or out of sight.
        /// </summary>
        public HealOrderResult TryHeal(Character target, DateTime nowUtc)
        {
            if (HealthPercent(target) >= 100)
                return HealOrderResult.Busy;

            if (Npc.IsDead || Npc.IsCastingNano || Npc.Playfield == null || Npc.UploadedNanoIds.Count == 0)
                return HealOrderResult.Busy;
            if (Npc.IsInNanoRecharge(nowUtc))
                return HealOrderResult.Busy;

            bool hasHeal = false;
            foreach (int nanoId in Npc.UploadedNanoIds)
            {
                NanoSpell? spell = Resolve(nanoId);
                if (spell == null || spell.IsHostile || !IsHeal(spell))
                    continue;

                hasHeal = true;
                if (!InRange(spell, target, DefaultSupportRange) || !NanoRuntime.CanStartCast(Npc, spell, target, nowUtc))
                    continue;
                if (NanoRuntime.TryStartCast(Npc, spell.Id, target.Identity, nowUtc) != NanoCastRefusal.None)
                    continue;

                _nextAttemptUtc = nowUtc.AddSeconds(Between(MinCastIntervalSeconds, MaxCastIntervalSeconds));
                return HealOrderResult.Cast;
            }

            return hasHeal ? HealOrderResult.OutOfRange : HealOrderResult.NoHeal;
        }

        NanoSpell? Resolve(int nanoId)
        {
            if (!_spells.TryGetValue(nanoId, out NanoSpell? spell))
            {
                NanoRuntime.TryGetSpell(Npc, nanoId, out spell);
                _spells[nanoId] = spell;
            }

            return spell;
        }

        Character? ChooseTarget(NanoSpell spell, DateTime nowUtc)
        {
            if (TrySummonedPetType(spell, out int petType))
            {
                // Pets do not summon; an NPC calls its pet into a fight it is in, once per slot.
                if (Npc.Pet != null || Npc.FightingTarget.Instance == 0 || Npc.OwnedPets.InSlotOf(petType) != null)
                    return null;
                return NanoRuntime.CanStartCast(Npc, spell, Npc, nowUtc) ? Npc : null;
            }

            if (spell.IsHostile)
            {
                Character? enemy = Npc.FightingTarget.Instance != 0 ? _brain.ResolveCurrentTarget() : null;
                if (enemy == null || !InRange(spell, enemy, NpcAiRules.NearbyRange) || HasNano(enemy, spell))
                    return null;
                return NanoRuntime.CanStartCast(Npc, spell, enemy, nowUtc) ? enemy : null;
            }

            // Heals, including heal-over-time buffs, only go on someone who is actually hurt.
            // The caster looks after itself first; only then does it help a neighbour.
            if (IsBuffLike(spell) && !IsHeal(spell))
            {
                if (!HasNano(Npc, spell) && NanoRuntime.CanStartCast(Npc, spell, Npc, nowUtc))
                    return Npc;

                // Random pick among nearby NPCs that can take it, so NPCs buff each other.
                Character? chosen = null;
                int eligible = 0;
                foreach (Character candidate in FriendlyCandidates(spell))
                {
                    if (ReferenceEquals(candidate, Npc)
                        || HasNano(candidate, spell) || !NanoRuntime.CanStartCast(Npc, spell, candidate, nowUtc))
                        continue;

                    eligible++;
                    if (_random.Next(eligible) == 0)
                        chosen = candidate;
                }

                return chosen;
            }

            // A pet heals greedily, all the way to full; other NPCs only once someone is well hurt.
            int healBelow = Npc.Pet != null ? 100 : HealBelowPercent;
            if (HealthPercent(Npc) < healBelow && !HasNano(Npc, spell)
                && NanoRuntime.CanStartCast(Npc, spell, Npc, nowUtc))
                return Npc;

            Character? hurt = null;
            int lowest = healBelow;
            foreach (Character candidate in FriendlyCandidates(spell))
            {
                if (ReferenceEquals(candidate, Npc))
                    continue;

                int percent = HealthPercent(candidate);
                if (percent >= lowest || HasNano(candidate, spell) || !NanoRuntime.CanStartCast(Npc, spell, candidate, nowUtc))
                    continue;

                lowest = percent;
                hurt = candidate;
            }

            return hurt;
        }

        /// <summary>
        /// Self, then living NPCs in range and sight that are not player pets. A pet's friends are its owner and
        /// the owner's other pets.
        /// </summary>
        IEnumerable<Character> FriendlyCandidates(NanoSpell spell)
        {
            yield return Npc;

            if (!CanApplyOnFriendly(spell))
                yield break;

            if (Npc.PetOwner is Character owner)
            {
                if (!owner.IsDead && InRange(spell, owner, DefaultSupportRange))
                    yield return owner;
                foreach (NpcCharacter sibling in owner.OwnedPets.All)
                {
                    if (!ReferenceEquals(sibling, Npc) && !sibling.IsDead && InRange(spell, sibling, DefaultSupportRange))
                        yield return sibling;
                }

                yield break;
            }

            if (_allies.Count == 0)
            {
                // Only this NPC's cell and the cells touching it; never the whole playfield.
                Npc.Playfield!.GetRequiredService<PlayfieldLocality>().CollectNearby(Npc, _nearby);
                foreach (Dynel dynel in _nearby)
                {
                    if (dynel is not NpcCharacter other || ReferenceEquals(other, Npc) || other.IsDead)
                        continue;
                    if (other.Stats.GetOrZero(CharacterStat.PetMaster) != 0)
                        continue;
                    if (Npc.GetEdgeDistanceTo(other) > DefaultSupportRange)
                        continue;

                    _allies.Add(other);
                }

                _nearby.Clear();
            }

            for (int i = 0; i < _allies.Count; i++)
            {
                Character ally = _allies[i];
                if (!ally.IsDead && InRange(spell, ally, DefaultSupportRange))
                    yield return ally;
            }
        }

        /// <summary>The nano summons a pet (an OnUse SummonPet); <paramref name="petType"/> is that pet's type.</summary>
        bool TrySummonedPetType(NanoSpell spell, out int petType)
        {
            petType = 0;
            if (!spell.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                return false;

            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i].Is(FunctionType.SummonPet) && spells[i].TryReadString(0, out string hash) && hash.Length > 0)
                {
                    petType = Pets.PetTypeCatalog.For(Npc.Playfield!.GetRequiredService<GameData.IGameData>().RootPath).TypeOf(hash);
                    return true;
                }
            }

            return false;
        }

        /// <summary>A nano flagged only ApplyOnSelf stays on the caster.</summary>
        static bool CanApplyOnFriendly(NanoSpell spell)
        {
            if (!spell.Stats.TryGetValue(CharacterStat.Can, out int raw))
                return true;

            CanFlags can = (CanFlags)(uint)raw;
            return (can & CanFlags.ApplyOnFriendly) != 0 || (can & CanFlags.ApplyOnSelf) == 0;
        }

        bool InRange(NanoSpell spell, Character target, double fallback)
        {
            if (ReferenceEquals(target, Npc))
                return true;

            double range = spell.Stats.TryGetValue(CharacterStat.AttackRange, out int value)
                ? StatCollection.Normalize(value)
                : 0;
            if (range <= 0)
                range = fallback;

            return Npc.GetEdgeDistanceTo(target) <= range && Npc.HasLineOfSightTo(target);
        }

        /// <summary>
        /// The nano, a nano it casts (CastNano wrappers land a child id), or an equal-or-stronger
        /// same-strain nano is already running on the target.
        /// </summary>
        bool HasNano(Character target, NanoSpell spell)
        {
            if (HasRunning(target, spell))
                return true;

            foreach (NanoSpell child in Children(spell))
            {
                if (HasRunning(target, child))
                    return true;
            }

            return false;
        }

        static bool HasRunning(Character target, NanoSpell spell)
        {
            IReadOnlyList<Buff> buffs = target.Buffs;
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].Id == spell.Id
                    || (spell.NanoStrain > 0 && buffs[i].NanoStrain == spell.NanoStrain
                        && buffs[i].StackingOrder >= spell.StackingOrder))
                    return true;
            }

            return false;
        }

        /// <summary>A buff itself, or a wrapper whose CastNano lands one.</summary>
        bool IsBuffLike(NanoSpell spell)
        {
            if (spell.IsBuff)
                return true;

            foreach (NanoSpell child in Children(spell))
            {
                if (child.IsBuff)
                    return true;
            }

            return false;
        }

        /// <summary>Nanos this one lands through its OnUse CastNano functions.</summary>
        IEnumerable<NanoSpell> Children(NanoSpell spell)
        {
            if (!spell.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                yield break;

            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i].Is(FunctionType.CastNano) && spells[i].TryReadInt(0, out int childId)
                    && childId != spell.Id && Resolve(childId) is NanoSpell child)
                    yield return child;
            }
        }

        /// <summary>An OnUse Hit that raises Health, on the nano or a nano it casts: a heal or heal over time.</summary>
        bool IsHeal(NanoSpell spell)
        {
            if (IsDirectHeal(spell))
                return true;

            foreach (NanoSpell child in Children(spell))
            {
                if (IsDirectHeal(child))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A Health-raising Hit in any of the nano's events: a direct heal (OnUse) or a heal over time (its ticks).
        /// Looking only at OnUse let heal-over-time nanos pass as ordinary buffs, which go on anyone lacking them,
        /// hurt or not.
        /// </summary>
        static bool IsDirectHeal(NanoSpell spell)
        {
            foreach (List<ItemSpell> spells in spell.SpellList.Values)
            {
                for (int i = 0; i < spells.Count; i++)
                {
                    ItemSpell hit = spells[i];
                    if (hit.Is(FunctionType.Hit)
                        && hit.TryReadInt(0, out int stat) && stat == (int)CharacterStat.Health
                        && hit.TryReadInt(1, out int amount) && amount > 0)
                        return true;
                }
            }

            return false;
        }

        static int HealthPercent(Character character)
        {
            int max = character.Stats.GetOrZero(CharacterStat.MaxHealth);
            if (max <= 0)
                return 100;
            return (int)(100L * Math.Max(0, character.Stats.GetOrZero(CharacterStat.Health)) / max);
        }

        double Between(double min, double max) => min + (_random.NextDouble() * (max - min));
    }
}
