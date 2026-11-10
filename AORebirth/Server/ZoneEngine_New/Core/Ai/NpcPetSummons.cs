namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.Entities;
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
        /// <summary>A summon that could not be selected or placed is tried again this much later.</summary>
        const double RetrySeconds = 1;

        readonly NpcCharacter _npc;
        readonly MonsterPetRule _rule;
        readonly ItemSpell[] _summons;
        readonly List<NpcCharacter> _pets = new();
        readonly List<DateTime> _due = new();
        bool _inCombat;

        NpcPetSummons(NpcCharacter npc, MonsterPetRule rule, ItemSpell[] summons)
        {
            _npc = npc;
            _rule = rule;
            _summons = summons;
        }

        /// <summary>Name of the equipment item whose OnUse raises an NPC's combat pets (one template per pet kind).</summary>
        public const string PetCreationItemName = "NPC Pet Creation Item";

        /// <summary>
        /// Combat pets for an NPC whose equipment holds a <see cref="PetCreationItemName"/> (the first, by slot, when it
        /// has several) with a pet spawn on use; otherwise null. The NPC's hash may override the
        /// <see cref="MonsterPetRule"/> defaults in MonsterCombatRules.json.
        /// </summary>
        public static NpcPetSummons? TryCreate(NpcCharacter npc, string gameDataRoot)
        {
            Item? creator = null;
            int creatorSlot = int.MaxValue;
            foreach (KeyValuePair<int, Item> slot in npc.Equipment.Content)
            {
                if (slot.Key >= creatorSlot
                    || !string.Equals(slot.Value.Name, PetCreationItemName, StringComparison.OrdinalIgnoreCase))
                    continue;
                creator = slot.Value;
                creatorSlot = slot.Key;
            }

            if (creator == null || !creator.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                return null;

            string? hash = npc.MobTemplate?.Hash ?? npc.SpawnHash;
            MonsterPetRule rule = MonsterCombatRulesCatalog.For(gameDataRoot).TryGet(hash, out MonsterCombatRules rules)
                && rules.Pets is MonsterPetRule overridden
                    ? overridden
                    : new MonsterPetRule();
            if (rule.MaxPets <= 0)
                return null;

            var summons = new List<ItemSpell>();
            foreach (ItemSpell spell in spells)
            {
                if (!spell.Is(FunctionType.SpawnMonster2) && !spell.Is(FunctionType.SummonPet))
                    continue;
                if (!spell.TryReadString(0, out string petHash) || petHash.Length == 0)
                    continue;
                summons.Add(spell);
            }

            return summons.Count == 0 ? null : new NpcPetSummons(npc, rule, summons.ToArray());
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
                    _due.Add(nowUtc.AddSeconds(Math.Max(0, delay)));
                }
            }

            // A pet lost mid-fight comes back after the respawn delay.
            for (int i = _pets.Count - 1; i >= 0; i--)
            {
                if (!IsGone(_pets[i]))
                    continue;
                _pets.RemoveAt(i);
                _due.Add(nowUtc.AddSeconds(Math.Max(0, _rule.RespawnDelaySeconds)));
            }

            for (int i = 0; i < _due.Count; i++)
            {
                if (_due[i] > nowUtc || _pets.Count >= _rule.MaxPets)
                    continue;

                if (!TryChoosePet(out string hash, out int level, out int duration))
                {
                    _due[i] = nowUtc.AddSeconds(RetrySeconds);
                    continue;
                }

                NpcCharacter? pet = _npc.Playfield?.GetService<Pets.PetService>()
                    ?.Summon(_npc, hash, level, durationSeconds: duration, replaceSameSlot: false);
                if (pet == null)
                {
                    _due[i] = nowUtc.AddSeconds(RetrySeconds);
                    continue;
                }

                _pets.Add(pet);
                _due.RemoveAt(i);
                i--;
            }
        }

        bool TryChoosePet(out string hash, out int level, out int duration)
        {
            hash = string.Empty;
            level = 0;
            duration = 0;

            // One event per summon opportunity: Rnd rolls once and later choices read LastRnd.
            // A capped result waits for the next opportunity rather than rerolling in an unbounded loop.
            var criteria = new SpellCriteria();
            var self = new RequirementSubject(
                stat => criteria.Resolve(stat, id => _npc.Stats.Get(id)), _npc.ResolveRequirement);
            var owner = new RequirementSubject(stat => _npc.Stats.Get(stat), _npc.ResolveRequirement);
            foreach (ItemSpell spell in _summons)
            {
                if (!ItemTemplate.MeetsRequirements(spell.Requirements, self, owner, owner))
                    continue;
                if (!spell.TryReadString(0, out string selectedHash) || string.IsNullOrWhiteSpace(selectedHash))
                    continue;

                hash = selectedHash.Trim();
                if (_rule.MaxPetsPerType is int limit)
                {
                    int alive = 0;
                    foreach (NpcCharacter pet in _pets)
                    {
                        if (!IsGone(pet) && string.Equals(pet.SpawnHash, hash, StringComparison.OrdinalIgnoreCase))
                            alive++;
                    }
                    if (alive >= limit)
                        return false;
                }

                if (!spell.TryReadInt(1, out level))
                    level = 0;
                if (!spell.TryReadInt(2, out duration))
                    duration = 0;
                return true;
            }

            return false;
        }

        bool IsGone(NpcCharacter pet)
            => pet.IsDead || pet.Playfield == null || !ReferenceEquals(pet.Pet?.Owner, _npc);
    }
}
