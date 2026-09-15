namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using AODB.Common.Enums;
    using AODB.Common.RDBObjects;
    using AODB.Common.Structs;

    using MsgPack;

    using ZoneEngine_New.Core.Inventory.Dat;

    using AoEventType = AODB.Common.Enums.EventType;
    using AoActionType = AODB.Common.Enums.ActionType;
    using AoFunctionType = AODB.Common.Enums.FunctionType;
    using AoOperator = AODB.Common.Enums.Operator;
    using ZeEventType = AORebirth.Enums.EventType;
    using ZeActionType = AORebirth.Enums.ActionType;
    using ZeOperator = AORebirth.Enums.Operator;
    using ZeItemTarget = AORebirth.Enums.ItemTarget;

    /// <summary>
    /// Turns AODB keyed modifiers/requirements into the positional
    /// <see cref="DatItemTemplate"/> layout ZoneEngine consumes.
    /// </summary>
    internal static class ItemRdbMapper
    {
        private const int QualityStat = 54;
        private const int FlagsStat = 0;
        private const int MultipleCountStat = 412;

        private static readonly HashSet<FunctionOperator> MetaOperators = new HashSet<FunctionOperator>
        {
            FunctionOperator.Duration,
            FunctionOperator.Interval,
            FunctionOperator.ApplyOn,
            FunctionOperator.TargetList,
        };

        private static readonly Dictionary<int, string> FunctionSets = LoadFunctionSets();

        internal static DatItemTemplate Map(
            int id,
            int dynelType,
            Dictionary<StatId, uint> stats,
            Dictionary<SkillCheck, Dictionary<StatId, uint>> skillChecks,
            Dictionary<AoEventType, Modifier> modifiers,
            Dictionary<AoActionType, Requirement> requirements)
        {
            DatItemTemplate template = MapStats(id, dynelType, stats);
            MapSkillChecks(template, skillChecks);
            MapModifiers(template, modifiers);
            MapRequirements(template, requirements);
            return template;
        }

        internal static DatItemTemplate MapStats(int id, int dynelType, Dictionary<StatId, uint> stats)
        {
            var template = new DatItemTemplate
            {
                ID = id,
                DynelType = dynelType,
                Quality = 1,
            };

            if (stats == null)
                return template;

            foreach (KeyValuePair<StatId, uint> pair in stats)
            {
                int key = (int)pair.Key;
                int value = unchecked((int)pair.Value);
                template.Stats[key] = value;
                if (key == QualityStat && value > 0)
                    template.Quality = value;
                else if (key == FlagsStat)
                    template.Flags = value;
                else if (key == MultipleCountStat)
                    template.MultipleCount = value;
            }

            return template;
        }

        static void MapSkillChecks(
            DatItemTemplate template,
            Dictionary<SkillCheck, Dictionary<StatId, uint>> skillChecks)
        {
            if (skillChecks == null)
                return;

            foreach (KeyValuePair<SkillCheck, Dictionary<StatId, uint>> group in skillChecks)
            {
                Dictionary<int, int> target = null;
                if (group.Key == SkillCheck.Attack)
                    target = template.Attack;
                else if (group.Key == SkillCheck.Defense)
                    target = template.Defend;

                if (target == null || group.Value == null)
                    continue;

                foreach (KeyValuePair<StatId, uint> pair in group.Value)
                    target[(int)pair.Key] = unchecked((int)pair.Value);
            }
        }

        static void MapModifiers(DatItemTemplate template, Dictionary<AoEventType, Modifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
                return;

            foreach (KeyValuePair<AoEventType, Modifier> eventPair in modifiers)
            {
                if (eventPair.Value?.Modifiers == null || eventPair.Value.Modifiers.Count == 0)
                    continue;

                var datEvent = new DatEvent
                {
                    EventType = (ZeEventType)(int)eventPair.Key,
                    Functions = new List<DatFunction>(),
                };

                foreach (KeyValuePair<AoFunctionType, List<Dictionary<FunctionOperator, object>>> functionPair
                         in eventPair.Value.Modifiers)
                {
                    if (functionPair.Value == null)
                        continue;

                    foreach (Dictionary<FunctionOperator, object> keyedArgs in functionPair.Value)
                    {
                        if (keyedArgs == null)
                            continue;

                        datEvent.Functions.Add(ToFunction((int)functionPair.Key, keyedArgs));
                    }
                }

                if (datEvent.Functions.Count > 0)
                    template.Events.Add(datEvent);
            }
        }

        static void MapRequirements(
            DatItemTemplate template,
            Dictionary<AoActionType, Requirement> requirements)
        {
            if (requirements == null || requirements.Count == 0)
                return;

            foreach (KeyValuePair<AoActionType, Requirement> actionPair in requirements)
            {
                if (actionPair.Value?.Criterion == null)
                    continue;

                var action = new DatAction
                {
                    ActionType = (ZeActionType)(int)actionPair.Key,
                    Requirements = new List<DatRequirement>(),
                };

                foreach (KeyValuePair<AoActionType, List<RequirementCriterion>> criterionPair
                         in actionPair.Value.Criterion)
                {
                    if (criterionPair.Value == null)
                        continue;

                    foreach (RequirementCriterion criterion in criterionPair.Value)
                    {
                        action.Requirements.Add(
                            new DatRequirement
                            {
                                Statnumber = criterion.Stat,
                                Value = unchecked((int)criterion.Value),
                                Operator = (ZeOperator)(int)criterion.Operator,
                                Target = ZeItemTarget.Self,
                            });
                    }
                }

                if (action.Requirements.Count > 0)
                    template.Actions.Add(action);
            }
        }

        internal static DatFunction ToFunction(int functionType, Dictionary<FunctionOperator, object> keyedArgs)
        {
            var function = new DatFunction
            {
                FunctionType = functionType,
                TickCount = 1,
                TickInterval = 0,
                Target = 0,
                Arguments = new DatFunctionArguments(),
                Requirements = new List<DatRequirement>(),
            };

            if (TryGetInt(keyedArgs, FunctionOperator.Duration, out int duration) && duration > 0)
                function.TickCount = duration;
            if (TryGetUInt(keyedArgs, FunctionOperator.Interval, out uint interval))
                function.TickInterval = interval;
            if (TryGetUInt(keyedArgs, FunctionOperator.ApplyOn, out uint applyOn))
                function.Target = unchecked((int)applyOn);

            foreach (object value in SelectPositionalArgs(functionType, keyedArgs))
                function.Arguments.Values.Add(ToMessagePackObject(value));

            return function;
        }

        /// <summary>
        /// AODB stores function args keyed by <see cref="FunctionOperator"/>. Legacy
        /// FunctionSets.cfg describes the positional consumer layout: typed tokens consume
        /// keys in insertion order (after meta), and <c>x</c> tokens skip that many bytes
        /// of keys (4 bytes per skipped int key).
        /// </summary>
        internal static List<object> SelectPositionalArgs(
            int functionType,
            Dictionary<FunctionOperator, object> keyedArgs)
        {
            var result = new List<object>();
            if (!FunctionSets.TryGetValue(functionType, out string format) || string.IsNullOrWhiteSpace(format))
                return result;

            var queue = new Queue<KeyValuePair<FunctionOperator, object>>();
            foreach (KeyValuePair<FunctionOperator, object> pair in keyedArgs)
            {
                if (MetaOperators.Contains(pair.Key))
                    continue;
                queue.Enqueue(pair);
            }

            string[] tokens = format.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim().ToLowerInvariant();
                if (token.Length < 2)
                    continue;

                char kind = token[token.Length - 1];
                if (!int.TryParse(token.Substring(0, token.Length - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                    || count < 0)
                    continue;

                if (kind == 'x')
                {
                    int skipKeys = count / 4;
                    for (int skipped = 0; skipped < skipKeys && queue.Count > 0; skipped++)
                        queue.Dequeue();
                    continue;
                }

                for (int taken = 0; taken < count; taken++)
                {
                    if (queue.Count == 0)
                        break;

                    object value = queue.Dequeue().Value;
                    if (kind == 's' && value is string text)
                        value = text.TrimEnd('\0');
                    result.Add(value);
                }
            }

            return result;
        }

        static bool TryGetInt(
            Dictionary<FunctionOperator, object> keyedArgs,
            FunctionOperator key,
            out int value)
        {
            value = 0;
            if (!keyedArgs.TryGetValue(key, out object raw) || raw == null)
                return false;

            switch (raw)
            {
                case int i:
                    value = i;
                    return true;
                case uint u:
                    value = unchecked((int)u);
                    return true;
                case long l:
                    value = (int)l;
                    return true;
                default:
                    return int.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }
        }

        static bool TryGetUInt(
            Dictionary<FunctionOperator, object> keyedArgs,
            FunctionOperator key,
            out uint value)
        {
            value = 0;
            if (!keyedArgs.TryGetValue(key, out object raw) || raw == null)
                return false;

            switch (raw)
            {
                case uint u:
                    value = u;
                    return true;
                case int i:
                    value = unchecked((uint)i);
                    return true;
                default:
                    return uint.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }
        }

        static MessagePackObject ToMessagePackObject(object value)
        {
            switch (value)
            {
                case null:
                    return MessagePackObject.Nil;
                case string s:
                    return s;
                case int i:
                    return i;
                case uint u:
                    return unchecked((int)u);
                case long l:
                    return (int)l;
                case short s16:
                    return (int)s16;
                case byte b:
                    return (int)b;
                case bool flag:
                    return flag ? 1 : 0;
                default:
                    if (value is Enum)
                        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        static Dictionary<int, string> LoadFunctionSets()
        {
            var result = new Dictionary<int, string>();
            string path = ResolveFunctionSetsPath();
            if (path == null || !File.Exists(path))
                throw new InvalidOperationException("FunctionSets.cfg was not found next to RDBDataExtractor.");

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                int split = line.IndexOf('=');
                if (split <= 0)
                    continue;

                if (!int.TryParse(line.Substring(0, split), NumberStyles.Integer, CultureInfo.InvariantCulture, out int functionType))
                    continue;

                result[functionType] = line.Substring(split + 1).Trim();
            }

            return result;
        }

        static string ResolveFunctionSetsPath()
        {
            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "FunctionSets.cfg"),
                Path.Combine(baseDir, "..", "..", "..", "FunctionSets.cfg"),
                Path.Combine(RepositoryRootResolver.Resolve(), "Tools", "RDBDataExtractor", "FunctionSets.cfg"),
                Path.Combine(RepositoryRootResolver.Resolve(), "Tools", "Algorithman", "Extractor Serializer", "FunctionSets.cfg"),
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string full = Path.GetFullPath(candidates[i]);
                if (File.Exists(full))
                    return full;
            }

            return null;
        }
    }
}
