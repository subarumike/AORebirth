namespace AORebirth.Tools.ItemExpansionCatalog;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AORebirth.Enums;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory.Dat;

internal sealed record ItemEvidence(string PrimaryReason, string[] Reasons,
    ExpansionObservation[] Observations, RequirementExpression[] Expressions,
    bool HasExpansionInformation, bool HasUnsupportedValue, bool HasConflictingEvidence,
    bool HasIncompleteRdb);

internal sealed record ExpansionObservation(string SourceField, int StatId, int Value,
    int? Operator, string? OperatorName, int? Target, string[] DecodedEntitlementBits,
    int UnsupportedBits);

internal sealed record RequirementExpression(string SourceField, RequirementTerm[] Terms,
    string? PackedExpressionBase64 = null);

internal sealed record RequirementTerm(int Index, int StatId, int Value, int Operator,
    string? OperatorName, int? Target, int? ChildOperator);

/// <summary>
/// Records raw expansion facts and complete expressions independently of the requirement classifier.
/// Account/stat values and playfield context alone do not assign an item's expansion.
/// </summary>
internal static class ExpansionEvidenceInspector
{
    public static ItemEvidence Inspect(DatItemTemplate item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var observations = new List<ExpansionObservation>();
        var expressions = new List<RequirementExpression>();
        var reasons = new SortedSet<string>(StringComparer.Ordinal);
        bool unsupported = false;
        bool conflicting = false;
        bool incomplete = item.Rdb == null || !item.Rdb.ReadComplete || item.Rdb.BytesLeft != 0
            || !string.IsNullOrEmpty(item.Rdb.ReadStopReason);

        void Observe(string path, int stat, int value, int? operation = null, int? target = null)
        {
            if (!IsExpansionStat(stat))
                return;
            bool mask = stat == (int)CharacterStat.Expansion && (operation == null
                || operation == (int)AORebirth.Enums.Operator.BitAnd
                || operation == (int)AORebirth.Enums.Operator.NotBitAnd
                || operation == (int)AORebirth.Enums.Operator.EqualTo
                || operation == (int)AORebirth.Enums.Operator.Unequal);
            int unknownBits = mask ? value & ~(int)Expansions.All : 0;
            string[] bits = mask ? Enum.GetValues<Expansions>()
                .Where(bit => IsSingleBit((int)bit) && (value & (int)bit) != 0)
                .OrderBy(bit => (int)bit).Select(bit => bit.ToString()).ToArray() : [];
            observations.Add(new ExpansionObservation(path, stat, value, operation,
                operation.HasValue ? OperationName(operation.Value) : null, target, bits, unknownBits));
            if (unknownBits != 0)
            {
                unsupported = true;
                reasons.Add("UNSUPPORTED_ENTITLEMENT_BITS");
            }
            if (operation.HasValue && !IsKnownComparison(operation.Value))
                reasons.Add("EXPANSION_STAT_ON_NONCOMPARISON_TERM");
        }

        void ObserveStats(string path, Dictionary<int, int>? stats)
        {
            if (stats == null)
                return;
            foreach (var pair in stats.OrderBy(pair => pair.Key))
                Observe(path + "[" + pair.Key + "]", pair.Key, pair.Value);
        }

        void ObservePairs(string path, List<DatIntPair>? pairs)
        {
            if (pairs == null)
                return;
            for (int index = 0; index < pairs.Count; index++)
                Observe(path + "[" + index + "]", pairs[index].Key, pairs[index].Value);
        }

        void CaptureRequirements(string path, List<DatRequirement>? requirements)
        {
            if (requirements == null || !requirements.Any(term => IsExpansionStat(term.Statnumber)))
                return;
            var terms = new RequirementTerm[requirements.Count];
            for (int index = 0; index < requirements.Count; index++)
            {
                DatRequirement term = requirements[index];
                int operation = (int)term.Operator;
                terms[index] = new RequirementTerm(index, term.Statnumber, term.Value, operation,
                    OperationName(operation), (int)term.Target, (int)term.ChildOperator);
                Observe(path + "[" + index + "]", term.Statnumber, term.Value, operation, (int)term.Target);
            }
            expressions.Add(new RequirementExpression(path, terms));
        }

        void CaptureCriteria(string path, List<DatCriterion>? criteria)
        {
            if (criteria == null || !criteria.Any(term => IsExpansionStat(term.Value1)))
                return;
            var terms = new RequirementTerm[criteria.Count];
            for (int index = 0; index < criteria.Count; index++)
            {
                DatCriterion term = criteria[index];
                terms[index] = new RequirementTerm(index, term.Value1, term.Value2, term.Operator,
                    OperationName(term.Operator), null, null);
                Observe(path + "[" + index + "]", term.Value1, term.Value2, term.Operator);
            }
            expressions.Add(new RequirementExpression(path, terms));
        }

        ObserveStats("Stats", item.Stats);
        if (item.Actions != null)
        {
            for (int action = 0; action < item.Actions.Count; action++)
                CaptureRequirements("Actions[" + action + "].Requirements", item.Actions[action].Requirements);
        }
        if (item.Events != null)
        {
            for (int eventIndex = 0; eventIndex < item.Events.Count; eventIndex++)
            {
                List<DatFunction>? functions = item.Events[eventIndex].Functions;
                if (functions == null)
                    continue;
                for (int function = 0; function < functions.Count; function++)
                    CaptureRequirements("Events[" + eventIndex + "].Functions[" + function + "].Requirements",
                        functions[function].Requirements);
            }
        }

        DatRdbTemplate? rdb = item.Rdb;
        if (rdb != null)
        {
            ObserveStats("Rdb.LiveStats", rdb.LiveStats);
            ObservePairs("Rdb.Stats", rdb.Stats);
            ObservePairs("Rdb.Stats18", rdb.Stats18);
            ObservePairs("Rdb.Stats2B", rdb.Stats2B);
            if (rdb.ActionCriteria != null)
            {
                foreach (var pair in rdb.ActionCriteria.OrderBy(pair => pair.Key))
                    CaptureCriteria("Rdb.ActionCriteria[" + pair.Key + "]", pair.Value);
            }
            if (rdb.Events != null)
            {
                foreach (var pair in rdb.Events.OrderBy(pair => pair.Key))
                {
                    for (int spellIndex = 0; spellIndex < pair.Value.Count; spellIndex++)
                    {
                        DatSpell spell = pair.Value[spellIndex];
                        string path = "Rdb.Events[" + pair.Key + "][" + spellIndex + "]";
                        CaptureCriteria(path + ".Criteria", spell.Criteria);
                        CaptureCriteria(path + ".SpecialCriteria", spell.SpecialCriteria);
                        if (ExpressionReferencesExpansion(spell.Expression, 0))
                        {
                            using var bytes = new MemoryStream();
                            using (var packer = Packer.Create(bytes))
                                packer.Pack(spell.Expression);
                            expressions.Add(new RequirementExpression(path + ".Expression", [],
                                Convert.ToBase64String(bytes.ToArray())));
                            reasons.Add("DYNAMIC_EXPANSION_EXPRESSION_RETAINED");
                        }
                    }
                }
            }

            // Match ItemRdbMapper's actual overwrite order, never compare unrelated blocks or class defaults.
            var mergedRaw = new Dictionary<int, int>();
            foreach (List<DatIntPair>? block in new[] { rdb.Stats18, rdb.Stats2B, rdb.Stats })
            {
                if (block == null)
                    continue;
                foreach (DatIntPair pair in block.Where(pair => IsExpansionStat(pair.Key)))
                    mergedRaw[pair.Key] = pair.Value;
            }
            if (rdb.ReadComplete && rdb.BytesLeft == 0 && mergedRaw.Any(pair => item.Stats == null
                    || !item.Stats.TryGetValue(pair.Key, out int value) || value != pair.Value))
            {
                conflicting = true;
                reasons.Add("RAW_AND_MAPPED_STAT_MIRRORS_DISAGREE");
            }
            if (rdb.ReadComplete && rdb.BytesLeft == 0 && ActionMirrorsDisagree(item))
            {
                conflicting = true;
                reasons.Add("RAW_AND_MAPPED_ACTION_MIRRORS_DISAGREE");
            }
        }

        if (rdb == null)
            reasons.Add("RDB_METADATA_UNAVAILABLE");
        else if (incomplete)
            reasons.Add("RDB_READ_INCOMPLETE");
        bool hasInformation = observations.Count > 0 || expressions.Count > 0;
        reasons.Add(hasInformation ? "EXPANSION_OR_PLAYFIELD_METADATA_PRESENT"
            : "NO_EXPLICIT_EXPANSION_METADATA");
        string primary = conflicting ? "CONFLICTING_EXPANSION_EVIDENCE"
            : unsupported ? "UNSUPPORTED_EXPANSION_VALUE"
            : incomplete ? "INCOMPLETE_RDB_METADATA"
            : hasInformation ? "EXPANSION_OR_CONTEXT_METADATA_PRESENT"
            : "MISSING_EXPANSION_METADATA";
        return new ItemEvidence(primary, reasons.ToArray(), observations.ToArray(), expressions.ToArray(),
            hasInformation, unsupported, conflicting, incomplete);
    }

    static bool ActionMirrorsDisagree(DatItemTemplate item)
    {
        if (item.Rdb?.ActionCriteria == null)
            return false;
        IEnumerable<int> keys = item.Rdb.ActionCriteria.Keys.Concat(
            item.Actions?.Select(action => (int)action.ActionType) ?? []).Distinct().Order();
        foreach (int key in keys)
        {
            item.Rdb.ActionCriteria.TryGetValue(key, out List<DatCriterion>? raw);
            DatAction[] mapped = item.Actions?.Where(action => (int)action.ActionType == key).ToArray() ?? [];
            bool relevant = raw?.Any(term => IsExpansionStat(term.Value1)) == true
                || mapped.Any(action => action.Requirements?.Any(term => IsExpansionStat(term.Statnumber)) == true);
            if (!relevant)
                continue;
            if (raw == null || mapped.Length != 1 || mapped[0].Requirements == null
                || mapped[0].Requirements.Count != raw.Count)
                return true;
            for (int index = 0; index < raw.Count; index++)
            {
                DatCriterion left = raw[index];
                DatRequirement right = mapped[0].Requirements[index];
                if (left.Value1 != right.Statnumber || left.Value2 != right.Value || left.Operator != (int)right.Operator
                    || right.Target != ItemTarget.Self || (int)right.ChildOperator != 0)
                    return true;
            }
        }
        return false;
    }

    static bool ExpressionReferencesExpansion(MessagePackObject value, int depth)
    {
        if (depth > 64 || !value.IsArray)
            return false;
        IList<MessagePackObject> children = value.AsList();
        if (children.Count == 3 && children[0].IsTypeOf<int>() == true && children[0].AsInt32() == 2
            && children[1].IsTypeOf<int>() == true && IsExpansionStat(children[1].AsInt32()))
            return true;
        return children.Any(child => child.IsArray && ExpressionReferencesExpansion(child, depth + 1));
    }

    static bool IsExpansionStat(int stat)
        => stat == (int)CharacterStat.Expansion || stat == (int)CharacterStat.ExpansionPlayfield;

    static bool IsSingleBit(int value) => value > 0 && (value & (value - 1)) == 0;

    static bool IsKnownComparison(int operation)
        => operation is (int)AORebirth.Enums.Operator.EqualTo or (int)AORebirth.Enums.Operator.Unequal
            or (int)AORebirth.Enums.Operator.BitAnd or (int)AORebirth.Enums.Operator.NotBitAnd
            or (int)AORebirth.Enums.Operator.GreaterThan or (int)AORebirth.Enums.Operator.LessThan;

    static string? OperationName(int operation)
        => Enum.GetName(typeof(AORebirth.Enums.Operator), operation);
}
