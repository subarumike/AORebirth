namespace ZoneEngine_New.Core.Inventory.Dat
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static class DatItemMapper
    {
        public static ItemTemplate ToTemplate(DatItemTemplate dat, string name)
        {
            Dictionary<EventType, List<ItemSpell>> spellList = ToSpellList(dat.Events);
            List<ItemAction> actions = ToActions(dat.Actions);

            return new ItemTemplate
            {
                Id = dat.ID,
                Name = name,
                Quality = dat.Quality > 0 ? dat.Quality : 1,
                Flags = dat.Flags,
                ItemType = dat.ItemType,
                DynelType = dat.DynelType,
                MultipleCount = dat.MultipleCount,
                Stats = ToCharacterStatMap(dat.Stats),
                Attack = ToCharacterStatMap(dat.Attack),
                Defend = ToCharacterStatMap(dat.Defend),
                SpellList = spellList,
                Actions = actions,
                Relations = dat.Relations != null ? new List<int>(dat.Relations) : new List<int>()
            };
        }

        /// <summary>
        /// Rebuilds <paramref name="template"/> with event and action data from the companion
        /// file. Everything the RDB export owns is carried across unchanged.
        /// </summary>
        public static ItemTemplate WithEvents(ItemTemplate template, ItemEventsDatTemplate events)
        {
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
                SpellList = ToSpellList(events.Events),
                Actions = ToActions(events.Actions),
                Relations = events.Relations != null
                    ? new List<int>(events.Relations)
                    : new List<int>(template.Relations),
                IsBuff = template.IsBuff,
                CanCancel = template.CanCancel
            };
        }

        private static Dictionary<EventType, List<ItemSpell>> ToSpellList(List<DatEvent>? events)
        {
            var spellList = new Dictionary<EventType, List<ItemSpell>>();
            if (events == null)
                return spellList;

            foreach (DatEvent ev in events)
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

            return spellList;
        }

        private static List<ItemAction> ToActions(List<DatAction>? actions)
        {
            var result = new List<ItemAction>();
            if (actions == null)
                return result;

            foreach (DatAction action in actions)
                result.Add(ToAction(action));

            return result;
        }

        private static Dictionary<CharacterStat, int> ToCharacterStatMap(Dictionary<int, int>? source)
        {
            var result = new Dictionary<CharacterStat, int>();
            if (source == null)
                return result;

            foreach (KeyValuePair<int, int> pair in source)
                result[(CharacterStat)pair.Key] = pair.Value;

            return result;
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
