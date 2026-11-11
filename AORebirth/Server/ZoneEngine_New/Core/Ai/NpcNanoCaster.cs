namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    using Utility;

    /// <summary>
    /// Casts an NPC's uploaded nanos now and then, at the pace Gameplay.json npcNanos sets. Every cast goes through
    /// <see cref="NanoRuntime"/>, so requirements, nano cost, recharge and NCU are the same as for players.
    /// In a fight the NPC casts hostile nanos (on its combat target) and pet summons first, then heals; buffs wait for
    /// the fight to end unless npcNanos allows them in combat. Out of a fight it heals, then buffs. A buff goes on the
    /// caster when it lacks it, otherwise on a random nearby NPC that does. A heal goes on the caster when it is hurt,
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

    /// <summary>What a nano is for, which decides when the NPC casts it and how long it waits afterwards.</summary>
    enum NanoKind
    {
        Hostile,
        Summon,
        Heal,
        Buff
    }

    sealed class NpcNanoCaster
    {
        static readonly NanoKind[] CombatOrder = [NanoKind.Hostile, NanoKind.Summon, NanoKind.Heal];

        static readonly NanoKind[] CombatOrderWithBuffs = [NanoKind.Hostile, NanoKind.Summon, NanoKind.Heal, NanoKind.Buff];

        static readonly NanoKind[] IdleOrder = [NanoKind.Heal, NanoKind.Buff];

        readonly NpcBrain _brain;
        readonly Dictionary<int, NanoSpell?> _spells = new();
        readonly Dictionary<int, NanoKind> _kinds = new();
        readonly List<Character> _allies = new();
        readonly List<Dynel> _nearby = new();
        readonly Random _random;
        DateTime _nextAttemptUtc;

        public NpcNanoCaster(NpcBrain brain)
        {
            _brain = brain;
            _random = new Random(brain.Npc.Identity.Instance);
            GameplayNpcNanos rules = GameplayNpcNanos.Default;
            _nextAttemptUtc = DateTime.UtcNow.AddSeconds(Between(rules.IdleCheckSeconds, rules.MaxCastIntervalSeconds));
        }

        NpcCharacter Npc => _brain.Npc;

        GameplayNpcNanos Rules => Npc.Playfield?.GetService<GameplaySettings>()?.NpcNanos ?? GameplayNpcNanos.Default;

        /// <summary>Fighting someone, or still holding hate for someone (chasing, or between swings).</summary>
        bool InCombat => Npc.FightingTarget.Instance != 0 || !_brain.Hate.IsEmpty;

        public void Tick(DateTime nowUtc)
        {
            if (nowUtc < _nextAttemptUtc)
                return;
            if (Npc.IsDead || Npc.IsStunned || _brain.IsEvading || Npc.IsCastingNano || Npc.IsInNanoRecharge(nowUtc)
                || Npc.UploadedNanoIds.Count == 0 || Npc.Playfield == null)
                return;

            GameplayNpcNanos rules = Rules;
            _nextAttemptUtc = nowUtc.AddSeconds(rules.IdleCheckSeconds);
            _allies.Clear();

            // Hostile nanos and summons have no target outside a fight, so the idle order leaves them out.
            NanoKind[] order = InCombat ? (rules.BuffInCombat ? CombatOrderWithBuffs : CombatOrder) : IdleOrder;
            for (int i = 0; i < order.Length; i++)
            {
                if (TryCast(order[i], rules, nowUtc))
                    return;
            }
        }

        /// <summary>The first castable nano of <paramref name="kind"/>, starting from a random one so NPCs vary.</summary>
        bool TryCast(NanoKind kind, GameplayNpcNanos rules, DateTime nowUtc)
        {
            List<int> nanos = Npc.UploadedNanoIds;
            int start = _random.Next(nanos.Count);
            for (int i = 0; i < nanos.Count; i++)
            {
                int nanoId = nanos[(start + i) % nanos.Count];
                NanoSpell? spell = Resolve(nanoId);
                if (spell == null || KindOf(nanoId, spell) != kind)
                    continue;

                Character? target = ChooseTarget(spell, kind, rules, nowUtc);
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
                        kind,
                        target.Identity.Instance,
                        ReferenceEquals(target, Npc),
                        HealthPercent(target),
                        HealthPercent(Npc)));

                _nextAttemptUtc = kind == NanoKind.Buff
                    ? nowUtc.AddSeconds(Between(rules.MinBuffIntervalSeconds, rules.MaxBuffIntervalSeconds))
                    : nowUtc.AddSeconds(Between(rules.MinCastIntervalSeconds, rules.MaxCastIntervalSeconds));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Hostile, summon, heal (a heal over time counts as a heal, not a buff) or buff. A friendly nano that is none
        /// of those takes the heal path, as before.
        /// </summary>
        NanoKind KindOf(int nanoId, NanoSpell spell)
        {
            if (_kinds.TryGetValue(nanoId, out NanoKind known))
                return known;

            NanoKind kind = spell.IsHostile ? NanoKind.Hostile
                : TrySummonedPetType(spell, out _) ? NanoKind.Summon
                : IsHeal(spell) ? NanoKind.Heal
                : IsBuffLike(spell) ? NanoKind.Buff
                : NanoKind.Heal;
            _kinds[nanoId] = kind;
            return kind;
        }

        /// <summary>
        /// A heal order: casts the first heal this NPC has that can land on <paramref name="target"/> now.
        /// <see cref="HealOrderResult.OutOfRange"/> when a heal exists but the target is too far or out of sight.
        /// </summary>
        public HealOrderResult TryHeal(Character target, DateTime nowUtc)
        {
            if (HealthPercent(target) >= 100)
                return HealOrderResult.Busy;

            if (Npc.IsDead || Npc.IsStunned || Npc.IsCastingNano || Npc.Playfield == null || Npc.UploadedNanoIds.Count == 0)
                return HealOrderResult.Busy;
            if (Npc.IsInNanoRecharge(nowUtc))
                return HealOrderResult.Busy;

            GameplayNpcNanos rules = Rules;
            bool hasHeal = false;
            bool waitingForNano = false;
            foreach (int nanoId in Npc.UploadedNanoIds)
            {
                NanoSpell? spell = Resolve(nanoId);
                if (spell == null || spell.IsHostile || !IsHeal(spell))
                    continue;

                hasHeal = true;
                if (!InRange(spell, target, rules.SupportRange))
                    continue;

                NanoCastRefusal refusal = NanoRuntime.PreviewStartCast(Npc, spell, target, nowUtc);
                if (refusal == NanoCastRefusal.None)
                    refusal = NanoRuntime.TryStartCast(Npc, spell.Id, target.Identity, nowUtc);
                if (refusal == NanoCastRefusal.NotEnoughNano)
                    waitingForNano = true;
                if (refusal != NanoCastRefusal.None)
                    continue;

                _nextAttemptUtc = nowUtc.AddSeconds(Between(rules.MinCastIntervalSeconds, rules.MaxCastIntervalSeconds));
                return HealOrderResult.Cast;
            }

            // A summoned pet replenishes nano. Keep its accepted target while it can still recover in range.
            if (waitingForNano)
                return HealOrderResult.Busy;
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

        Character? ChooseTarget(NanoSpell spell, NanoKind kind, GameplayNpcNanos rules, DateTime nowUtc)
        {
            if (kind == NanoKind.Summon)
            {
                // Pets do not summon; an NPC calls its pet into a fight it is in, once per slot.
                if (Npc.Pet != null || Npc.FightingTarget.Instance == 0 || !TrySummonedPetType(spell, out int petType)
                    || Npc.OwnedPets.InSlotOf(petType) != null)
                    return null;
                return NanoRuntime.CanStartCast(Npc, spell, Npc, nowUtc) ? Npc : null;
            }

            if (kind == NanoKind.Hostile)
            {
                Character? enemy = Npc.FightingTarget.Instance != 0 ? _brain.ResolveCurrentTarget() : null;
                if (enemy == null || !InRange(spell, enemy, NpcAiRules.NearbyRange) || HasNano(enemy, spell))
                    return null;
                return NanoRuntime.CanStartCast(Npc, spell, enemy, nowUtc) ? enemy : null;
            }

            // Heals, including heal-over-time buffs, only go on someone who is actually hurt.
            // The caster looks after itself first; only then does it help a neighbour.
            if (kind == NanoKind.Buff)
            {
                if (!HasNano(Npc, spell) && NanoRuntime.CanStartCast(Npc, spell, Npc, nowUtc))
                    return Npc;

                // Random pick among nearby NPCs that can take it, so NPCs buff each other.
                Character? chosen = null;
                int eligible = 0;
                foreach (Character candidate in FriendlyCandidates(spell, rules.SupportRange))
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

            // A pet told to follow or wait heals no one.
            if (Npc.Pet is { Mode: Pets.PetMode.Follow or Pets.PetMode.Wait })
                return null;

            // A pet heals greedily, all the way to full; other NPCs only once someone is well hurt.
            int healBelow = Npc.Pet != null ? 100 : rules.HealBelowPercent;
            if (HealthPercent(Npc) < healBelow && !HasNano(Npc, spell)
                && NanoRuntime.CanStartCast(Npc, spell, Npc, nowUtc))
                return Npc;

            Character? hurt = null;
            int lowest = healBelow;
            foreach (Character candidate in FriendlyCandidates(spell, rules.SupportRange))
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
        IEnumerable<Character> FriendlyCandidates(NanoSpell spell, double supportRange)
        {
            yield return Npc;

            if (!CanApplyOnFriendly(spell))
                yield break;

            if (Npc.PetOwner is Character owner)
            {
                if (!owner.IsDead && InRange(spell, owner, supportRange))
                    yield return owner;
                foreach (NpcCharacter sibling in owner.OwnedPets.All)
                {
                    if (!ReferenceEquals(sibling, Npc) && !sibling.IsDead && InRange(spell, sibling, supportRange))
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
                    if (Npc.GetEdgeDistanceTo(other) > supportRange)
                        continue;

                    _allies.Add(other);
                }

                _nearby.Clear();
            }

            for (int i = 0; i < _allies.Count; i++)
            {
                Character ally = _allies[i];
                if (!ally.IsDead && InRange(spell, ally, supportRange))
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
                    || (buffs[i].StackingOrder >= spell.StackingOrder && spell.SharesStackingLine(buffs[i])))
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
