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
    /// Copies OnUse functions from a Dynels.dat placement onto an item template. RDB item records
    /// for terminals are often empty shells; the Use behaviour lives on the playfield dynel.
    /// </summary>
    public static class DynelEventSpells
    {
        public static ItemTemplate WithOnUseFromDynel(ItemTemplate template, PlayfieldDynel? dynel)
        {
            ArgumentNullException.ThrowIfNull(template);
            if (dynel?.Modifiers == null
                || !dynel.Modifiers.TryGetValue(AodbEventType.OnUse, out Modifier? modifier)
                || modifier?.Modifiers == null
                || modifier.Modifiers.Count == 0)
                return template;

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
                return template;

            var spellList = new Dictionary<EventType, List<ItemSpell>>(template.SpellList.Count + 1);
            foreach (KeyValuePair<EventType, List<ItemSpell>> existing in template.SpellList)
                spellList[existing.Key] = existing.Value;

            if (spellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? onUse))
            {
                var merged = new List<ItemSpell>(onUse.Count + spells.Count);
                merged.AddRange(onUse);
                merged.AddRange(spells);
                spellList[EventType.OnUse] = merged;
            }
            else
            {
                spellList[EventType.OnUse] = spells;
            }

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
                IsBuff = template.IsBuff,
                CanCancel = template.CanCancel
            };
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

                result.Add(
                    new ItemRequirement
                    {
                        StatNumber = criterion.Stat,
                        Operator = (int)criterion.Operator,
                        Value = unchecked((int)criterion.Value),
                        ChildOperator = (int)AodbOperator.And,
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
