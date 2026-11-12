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
        readonly string _hash;
        readonly int _level;
        readonly List<NpcCharacter> _pets = new();
        readonly List<DateTime> _due = new();
        bool _inCombat;

        NpcPetSummons(NpcCharacter npc, MonsterPetRule rule, string hash, int level)
        {
            _npc = npc;
            _rule = rule;
            _hash = hash;
            _level = level;
        }

        /// <summary>
        /// Combat pets use the first equipped creation item by slot and the authored policy for the NPC.
        /// </summary>
        public static NpcPetSummons? TryCreate(NpcCharacter npc, IGameData gameData)
        {
            MonsterCombatRulesCatalog catalog = MonsterCombatRulesCatalog.For(gameData.RootPath);
            Item? creator = null;
            int creatorSlot = int.MaxValue;
            foreach (KeyValuePair<int, Item> slot in npc.Equipment.Content)
            {
                if (slot.Key >= creatorSlot || !catalog.IsPetCreationItem(slot.Value.Definition.Id))
                    continue;
                creator = slot.Value;
                creatorSlot = slot.Key;
            }

            if (creator == null)
                return null;

            string? hash = npc.MobTemplate?.Hash ?? npc.SpawnHash;
            MonsterPetRule rule = catalog.PetRule(hash);
            if (rule.MaxPets <= 0)
                return null;

            if (!TryReadSummon(creator.Definition, out string petHash, out int level)
                || !gameData.HasMobTemplate(petHash))
                throw new InvalidDataException($"NPC '{hash}' has an invalid pet summon on item {creator.Definition.Id}.");

            return new NpcPetSummons(npc, rule, petHash, level);
        }

        internal static bool TryReadSummon(ItemTemplate creator, out string petHash, out int level)
        {
            petHash = string.Empty;
            level = 0;
            if (!creator.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                return false;

            foreach (ItemSpell spell in spells)
            {
                if (!spell.Is(FunctionType.SpawnMonster2) && !spell.Is(FunctionType.SummonPet))
                    continue;
                if (!spell.TryReadString(0, out string target) || target.Length == 0)
                    continue;
                petHash = target.Trim();
                spell.TryReadInt(1, out level);
                return petHash.Length > 0;
            }

            return false;
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

                NpcCharacter? pet = _npc.Playfield?.GetService<Pets.PetService>()
                    ?.Summon(_npc, _hash, _level, durationSeconds: 0, replaceSameSlot: false);
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

        bool IsGone(NpcCharacter pet)
            => pet.IsDead || pet.Playfield == null || !ReferenceEquals(pet.Pet?.Owner, _npc);
    }
}
