namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using AODB.Common.RDBObjects;
    using AODB.Common.Structs;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.Inventory;

    using AodbEventType = AODB.Common.Enums.EventType;
    using AodbFunctionOperator = AODB.Common.Enums.FunctionOperator;
    using AodbFunctionType = AODB.Common.Enums.FunctionType;
    using AodbOperator = AODB.Common.Enums.Operator;

    /// <summary>
    /// Copies dynel events onto an item template. RDB item records for terminals are often empty
    /// shells; Use and vicinity behaviour lives on the playfield dynel.
    /// </summary>
    public static class DynelEventSpells
    {
        /// <summary>
        /// Copies <see cref="EventType.OnUse"/> and <see cref="EventType.OnTargetInVicinity"/> from
        /// the dynel. Grid teleporters keep <see cref="FunctionType.LineTeleport"/> on vicinity,
        /// not on use.
        /// </summary>
        public static ItemTemplate WithOnUseFromDynel(ItemTemplate template, PlayfieldDynel? dynel)
        {
            ArgumentNullException.ThrowIfNull(template);
            if (dynel?.Modifiers == null)
                return template;

            var spellList = new Dictionary<EventType, List<ItemSpell>>(template.SpellList.Count + 2);
            foreach (KeyValuePair<EventType, List<ItemSpell>> existing in template.SpellList)
                spellList[existing.Key] = existing.Value;

            bool added = CopyEvent(dynel, spellList, AodbEventType.OnUse, EventType.OnUse);
            added |= CopyEvent(dynel, spellList, AodbEventType.OnTargetInVicinity, EventType.OnTargetInVicinity);
            // Ordinary portal entry remains owned by the portal resolver. Trigger-bearing entry
            // lists also need their static owner and authored retained actions on the server.
            List<ItemSpell>? walkIn = ReadWalkIn(dynel);
            if (walkIn != null && walkIn.Exists(IsAreaTrigger))
            {
                spellList[EventType.OnEnter] = walkIn;
                added = true;
            }
            if (!added)
                return template;

            return new ItemTemplate
            {
                Id = template.Id,
                Name = template.Name,
                Quality = template.Quality,
                Flags = template.Flags,
                ItemType = template.ItemType,
                DynelType = template.DynelType,
                MultipleCount = template.MultipleCount,
                Stats = template.Stats,
                Attack = template.Attack,
                Defend = template.Defend,
                SpellList = spellList,
                Actions = template.Actions,
                Relations = template.Relations,
                CanCancel = template.CanCancel
            };
        }

        /// <summary>
        /// The functions a dynel runs when a character walks into it (OnEnter, else OnCollide), with their criteria, or
        /// null when it has none. Door portals gate their teleport on these (Condemned Subway: TeleportProxy only below
        /// level 26, Text "Only level 25 characters and below can enter this dungeon." above it).
        /// </summary>
        public static List<ItemSpell>? ReadWalkIn(PlayfieldDynel? dynel)
        {
            if (dynel?.Modifiers == null)
                return null;

            foreach (AodbEventType source in new[] { AodbEventType.OnEnter, AodbEventType.OnCollide })
            {
                var spells = new Dictionary<EventType, List<ItemSpell>>();
                if (CopyEvent(dynel, spells, source, EventType.OnEnter) && spells.TryGetValue(EventType.OnEnter, out List<ItemSpell>? read))
                    return read;
            }

            return null;
        }

        public static bool IsAreaTrigger(ItemSpell spell)
            => spell.Is(FunctionType.AreaTrigger) || spell.Is(FunctionType.RemoteAreaTrigger);

        static bool CopyEvent(
            PlayfieldDynel dynel,
            Dictionary<EventType, List<ItemSpell>> spellList,
            AodbEventType source,
            EventType destination)
        {
            if (!dynel.Modifiers.TryGetValue(source, out Modifier? modifier)
                || modifier?.Modifiers == null
                || modifier.Modifiers.Count == 0)
                return false;

            var spells = new List<ItemSpell>();
            foreach (KeyValuePair<AodbFunctionType, List<Dictionary<AodbFunctionOperator, object>>> pair
                     in modifier.Modifiers)
            {
                List<Dictionary<AodbFunctionOperator, object>>? sets = pair.Value;
                if (sets == null)
                    continue;

                for (int i = 0; i < sets.Count; i++)
                {
                    ItemSpell? spell = TryMapSpell((int)pair.Key, sets[i]);
                    if (spell != null)
                        spells.Add(spell);
                }
            }

            if (spells.Count == 0)
                return false;

            if (spellList.TryGetValue(destination, out List<ItemSpell>? existing))
            {
                var merged = new List<ItemSpell>(existing.Count + spells.Count);
                merged.AddRange(existing);
                merged.AddRange(spells);
                spellList[destination] = merged;
            }
            else
            {
                spellList[destination] = spells;
            }

            return true;
        }

        static ItemSpell? TryMapSpell(int functionType, Dictionary<AodbFunctionOperator, object>? arguments)
        {
            if (arguments == null)
                return null;

            var spell = new ItemSpell
            {
                FunctionType = functionType,
                Target = ToInt(Get(arguments, AodbFunctionOperator.ApplyOn)),
                TickCount = ToInt(Get(arguments, AodbFunctionOperator.Duration)),
                TickInterval = unchecked((uint)ToInt(Get(arguments, AodbFunctionOperator.Interval))),
                Metadata35 = arguments.TryGetValue(AodbFunctionOperator.TargetList, out object? metadata35)
                    ? ToInt(metadata35) : -1,
                Arguments = MapArguments(Get(arguments, AodbFunctionOperator.Arg1)),
                Requirements = MapCriteria(Get(arguments, AodbFunctionOperator.Criteria))
            };
            return spell;
        }

        static List<object> MapArguments(object? raw)
        {
            var result = new List<object>();
            if (raw is string text)
            {
                result.Add(text);
                return result;
            }

            if (raw is not IList list)
                return result;

            for (int i = 0; i < list.Count; i++)
            {
                object? value = list[i];
                if (value is string s)
                    result.Add(s);
                else
                    result.Add(ToInt(value));
            }

            return result;
        }

        static List<ItemRequirement> MapCriteria(object? raw)
        {
            var result = new List<ItemRequirement>();
            if (raw is not IList list)
                return result;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is not RequirementCriterion criterion)
                    continue;

                // AODB criteria are postfix: leaves, then the And / Or / Not rows that link them. Leaves carry no
                // ChildOperator, so ItemTemplate.MeetsRequirements reads them as that expression ("Level > 1, Cash > 999,
                // Or" is either, not both).
                result.Add(
                    new ItemRequirement
                    {
                        StatNumber = criterion.Stat,
                        Operator = (int)criterion.Operator,
                        Value = unchecked((int)criterion.Value),
                        ChildOperator = 0,
                        Target = 0
                    });
            }

            return result;
        }

        static object? Get(Dictionary<AodbFunctionOperator, object> arguments, AodbFunctionOperator key)
            => arguments.TryGetValue(key, out object? value) ? value : null;

        static int ToInt(object? raw)
        {
            return raw switch
            {
                int i => i,
                short s => s,
                long l => (int)l,
                byte b => b,
                uint u => unchecked((int)u),
                float f => (int)f,
                AodbOperator op => (int)op,
                _ => 0
            };
        }
    }
}
