namespace ZoneEngine_New.Core.Inventory.Dat
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using MsgPack;

    internal static class DatItemMapper
    {
        public static ItemTemplate ToTemplate(DatItemTemplate dat, string name)
        {
            var spellList = new Dictionary<EventType, List<ItemSpell>>();
            if (dat.Events != null)
            {
                foreach (DatEvent ev in dat.Events)
                {
                    var spells = new List<ItemSpell>();
                    if (ev.Functions != null)
                    {
                        foreach (DatFunction function in ev.Functions)
                            spells.Add(ToSpell(function));
                    }

                    if (spellList.TryGetValue(ev.EventType, out List<ItemSpell>? existing))
                        existing.AddRange(spells);
                    else
                        spellList[ev.EventType] = spells;
                }
            }

            var actions = new List<ItemAction>();
            if (dat.Actions != null)
            {
                foreach (DatAction action in dat.Actions)
                    actions.Add(ToAction(action));
            }

            return new ItemTemplate
            {
                Id = dat.ID,
                Name = name,
                Quality = dat.Quality > 0 ? dat.Quality : 1,
                Flags = dat.Flags,
                ItemType = dat.ItemType,
                MultipleCount = dat.MultipleCount,
                Stats = dat.Stats != null ? new Dictionary<int, int>(dat.Stats) : new Dictionary<int, int>(),
                Attack = dat.Attack != null ? new Dictionary<int, int>(dat.Attack) : new Dictionary<int, int>(),
                Defend = dat.Defend != null ? new Dictionary<int, int>(dat.Defend) : new Dictionary<int, int>(),
                SpellList = spellList,
                Actions = actions,
                Relations = dat.Relations != null ? new List<int>(dat.Relations) : new List<int>()
            };
        }

        private static ItemSpell ToSpell(DatFunction function)
        {
            var spell = new ItemSpell
            {
                FunctionType = function.FunctionType,
                Target = function.Target,
                TickCount = function.TickCount,
                TickInterval = function.TickInterval,
                Arguments = new List<object>(),
                Requirements = new List<ItemRequirement>()
            };

            if (function.Arguments?.Values != null)
            {
                foreach (MessagePackObject value in function.Arguments.Values)
                    spell.Arguments.Add(ToClr(value));
            }

            if (function.Requirements != null)
            {
                foreach (DatRequirement requirement in function.Requirements)
                    spell.Requirements.Add(ToRequirement(requirement));
            }

            return spell;
        }

        private static ItemAction ToAction(DatAction action)
        {
            var result = new ItemAction
            {
                ActionType = (int)action.ActionType,
                Requirements = new List<ItemRequirement>()
            };

            if (action.Requirements != null)
            {
                foreach (DatRequirement requirement in action.Requirements)
                    result.Requirements.Add(ToRequirement(requirement));
            }

            return result;
        }

        private static ItemRequirement ToRequirement(DatRequirement requirement)
        {
            return new ItemRequirement
            {
                ChildOperator = (int)requirement.ChildOperator,
                Operator = (int)requirement.Operator,
                StatNumber = requirement.Statnumber,
                Target = (int)requirement.Target,
                Value = requirement.Value
            };
        }

        private static object ToClr(MessagePackObject value)
        {
            if (value.IsTypeOf<int>() == true)
                return value.AsInt32();
            if (value.IsTypeOf<float>() == true)
                return value.AsSingle();
            if (value.IsTypeOf<string>() == true)
                return value.AsString();
            return value.ToObject();
        }
    }
}
