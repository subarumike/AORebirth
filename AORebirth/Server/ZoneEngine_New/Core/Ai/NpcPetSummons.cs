namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;

    /// <summary>
    /// An NPC's <see cref="MonsterPetRule"/>: once it starts fighting it raises pets from its pet-creation item, one every
    /// <see cref="MonsterPetRule.SummonIntervalSeconds"/> (the first one interval in), up to
    /// <see cref="MonsterPetRule.MaxPets"/>; a pet that dies is replaced
    /// <see cref="MonsterPetRule.RespawnDelaySeconds"/> later while the fight goes on. The pets are ordinary NPC pets
    /// (<see cref="Pets.PetService"/>), so the owner's reset, death or despawn dismisses them.
    /// </summary>
    sealed class NpcPetSummons
    {
        readonly NpcCharacter _npc;
        readonly MonsterPetRule _rule;
        readonly SummonOperation[] _summons;
        readonly List<NpcCharacter> _pets = new();
        readonly List<DateTime> _due = new();
        bool _inCombat;

        NpcPetSummons(NpcCharacter npc, MonsterPetRule rule, SummonOperation[] summons)
        {
            _npc = npc;
            _rule = rule;
            _summons = summons;
        }

        /// <summary>
        /// Uses the actual equipped item and the authored combat policy. NPCs without an encounter override
        /// use the policy defined for the creation item catalog.
        /// </summary>
        public static NpcPetSummons? TryCreate(NpcCharacter npc, IGameData gameData)
        {
            MonsterCombatRulesCatalog catalog = MonsterCombatRulesCatalog.For(gameData.RootPath);
            string? hash = npc.SpawnHash ?? npc.MobTemplate?.Hash;
            MonsterPetRule? rule = catalog.PetRule(hash, npc.MobTemplate?.Hash);
            Item? creator = null;
            foreach (Item item in npc.Equipment.Content.Values)
            {
                if (!catalog.IsPetCreationItem(item.Definition.Id))
                    continue;
                if (creator != null)
                    throw new InvalidDataException($"NPC '{hash}' equips multiple NPC pet creation items.");
                creator = item;
            }

            if (creator == null)
            {
                if (catalog.RequiresPetCreator(hash, npc.MobTemplate?.Hash))
                    throw new InvalidDataException($"NPC '{hash}' has a pet policy but no bound creation item.");
                return null;
            }

            if (rule is not { MaxPets: > 0 })
                return null;

            return new NpcPetSummons(npc, rule, ReadSummons(creator.Definition, gameData));
        }

        internal static SummonOperation[] ReadSummons(ItemTemplate creator, IGameData? gameData)
        {
            if (!creator.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                throw new InvalidDataException($"NPC pet creation item {creator.Id} has no OnUse event.");

            var summons = new List<SummonOperation>();
            foreach (ItemSpell spell in spells)
            {
                bool spawnsMonster = spell.Is(FunctionType.SpawnMonster2);
                if (!spawnsMonster && !spell.Is(FunctionType.SummonPet))
                    continue;
                if (!spell.TryReadString(0, out string petHash) || string.IsNullOrWhiteSpace(petHash))
                    throw new InvalidDataException($"NPC pet creation item {creator.Id} has a summon with no target hash.");

                petHash = petHash.Trim();
                bool hasLevel = spell.TryReadInt(1, out int level);
                bool hasDuration = spell.TryReadInt(2, out int duration);
                if ((spawnsMonster && level <= 0) || (!hasLevel && spell.ArgumentCount > 1)
                    || (!hasDuration && spell.ArgumentCount > 2))
                    throw new InvalidDataException($"NPC pet creation item {creator.Id} has invalid summon arguments for '{petHash}'.");
                if (gameData != null && (!gameData.HasMobTemplate(petHash)
                    || !gameData.TryResolveMobTemplate(petHash, level > 0 ? level : null, out MobTemplate template)
                    || !NpcTemplateValidation.CanSpawn(template)))
                    throw new InvalidDataException($"NPC pet creation item {creator.Id} refers to unspawnable pet '{petHash}'.");

                // SpawnMonster2 does not apply a time-to-live in the normal item function.
                summons.Add(new SummonOperation(spell, petHash, level, spawnsMonster ? 0 : duration));
            }

            if (summons.Count == 0)
                throw new InvalidDataException($"NPC pet creation item {creator.Id} has no OnUse summon operation.");
            return summons.ToArray();
        }

        /// <param name="inCombat">The NPC is fighting and not leashing home.</param>
        public void Tick(bool inCombat, DateTime nowUtc)
        {
            if (!inCombat)
            {
                // Out of the fight nothing new is raised. The reset that follows dismisses the pets.
                _inCombat = false;
                _due.Clear();
                return;
            }

            if (!_inCombat)
            {
                _inCombat = true;
                _pets.RemoveAll(IsGone);
                _due.Clear();
                for (int i = _pets.Count; i < _rule.MaxPets; i++)
                {
                    double delay = (i - _pets.Count + 1) * _rule.SummonIntervalSeconds;
                    _due.Add(nowUtc.AddSeconds(delay));
                }
            }

            // A pet lost mid-fight comes back after the respawn delay.
            for (int i = _pets.Count - 1; i >= 0; i--)
            {
                if (!IsGone(_pets[i]))
                    continue;
                _pets.RemoveAt(i);
                _due.Add(nowUtc.AddSeconds(_rule.RespawnDelaySeconds));
            }

            for (int i = 0; i < _due.Count; i++)
            {
                if (_due[i] > nowUtc || _pets.Count >= _rule.MaxPets)
                    continue;

                if (!TryChoosePet(out SummonOperation summon))
                {
                    // Zero means an immediate initial attempt, not another random roll every world tick.
                    double delay = _rule.SummonIntervalSeconds > 0 ? _rule.SummonIntervalSeconds : _rule.RetryDelaySeconds;
                    _due[i] = nowUtc.AddSeconds(delay);
                    continue;
                }

                NpcCharacter? pet = _npc.Playfield?.GetService<Pets.PetService>()
                    ?.Summon(_npc, summon.Hash, summon.Level, summon.Duration, replaceSameSlot: false);
                if (pet == null)
                {
                    _due[i] = nowUtc.AddSeconds(_rule.RetryDelaySeconds);
                    continue;
                }

                _pets.Add(pet);
                _due.RemoveAt(i);
                i--;
            }
        }

        internal void Reset()
        {
            _inCombat = false;
            _due.Clear();
            _pets.Clear();
        }

        bool TryChoosePet(out SummonOperation selected)
        {
            selected = default;

            // One event per summon opportunity: Rnd rolls once and later choices read LastRnd.
            // A capped result waits for the next opportunity rather than rerolling in an unbounded loop.
            var criteria = new SpellCriteria();
            var self = new RequirementSubject(
                stat => criteria.Resolve(stat, id => _npc.Stats.Get(id)), _npc.ResolveRequirement);
            var owner = new RequirementSubject(stat => _npc.Stats.Get(stat), _npc.ResolveRequirement);
            foreach (SummonOperation summon in _summons)
            {
                if (!ItemTemplate.MeetsRequirements(summon.Spell.Requirements, self, owner, owner))
                    continue;

                if (_rule.MaxPetsPerType is int limit)
                {
                    int alive = 0;
                    foreach (NpcCharacter pet in _pets)
                    {
                        if (!IsGone(pet) && string.Equals(pet.SpawnHash, summon.Hash, StringComparison.OrdinalIgnoreCase))
                            alive++;
                    }
                    if (alive >= limit)
                        return false;
                }

                selected = summon;
                return true;
            }

            return false;
        }

        bool IsGone(NpcCharacter pet)
            => pet.IsDead || pet.Playfield == null || !ReferenceEquals(pet.Pet?.Owner, _npc);

        internal readonly record struct SummonOperation(ItemSpell Spell, string Hash, int Level, int Duration);
    }
}
