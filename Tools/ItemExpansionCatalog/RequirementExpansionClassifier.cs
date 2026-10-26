namespace AORebirth.Tools.ItemExpansionCatalog;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory.Dat;

internal sealed record ClassificationEvidence(string SourceField, int Value, int ActionType,
    string[] Associations, string Logic);

internal sealed record ExpansionClassification(string PrimaryClassification, string[] Associations,
    int? OriginalValue, string? SourceField, string Confidence, string Reason,
    ClassificationEvidence[] Evidence);

/// <summary>
/// Classifies explicit positive item-action expansion requirements under Mike's accepted association rule.
/// It does not infer expansion from names, absent requirements, item stats or character entitlements alone.
/// </summary>
internal static class RequirementExpansionClassifier
{
    // The declared eleven entitlement bits form a finite 0..2047 domain. One bit per possible account mask
    // permits exact consistency checks for positive item requirements, including alternatives.
    static readonly BigInteger AllKnownMasks = (BigInteger.One << ((int)Expansions.All + 1)) - BigInteger.One;
    static readonly ConcurrentDictionary<(int Operation, int Value), BigInteger> PositiveMasks = new();

    public static ExpansionClassification Classify(DatItemTemplate item, ItemEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.HasConflictingEvidence)
            return Unknown("CONFLICTING_EXPANSION_EVIDENCE");
        if (evidence.HasUnsupportedValue)
            return Unknown("UNSUPPORTED_EXPANSION_VALUE");
        // Legacy files can have complete Actions without an Rdb mirror. A partial newer RDB read cannot
        // establish that the available requirement expression is complete.
        if (item.Rdb != null && evidence.HasIncompleteRdb)
            return Unknown("INCOMPLETE_RDB_METADATA");

        var actions = new List<ActionExpression>();
        if (item.Actions is { Count: > 0 })
        {
            for (int actionIndex = 0; actionIndex < item.Actions.Count; actionIndex++)
            {
                DatAction action = item.Actions[actionIndex];
                var terms = new List<Term>();
                for (int index = 0; index < (action.Requirements?.Count ?? 0); index++)
                {
                    DatRequirement requirement = action.Requirements![index];
                    terms.Add(new Term(requirement.Statnumber, requirement.Value, (int)requirement.Operator,
                        (int)requirement.ChildOperator, (int)requirement.Target,
                        "Actions[" + actionIndex + "].Requirements[" + index + "]"));
                }
                actions.Add(new ActionExpression((int)action.ActionType, terms.ToArray()));
            }
        }
        else if (item.Rdb?.ActionCriteria != null)
        {
            foreach (var action in item.Rdb.ActionCriteria.OrderBy(pair => pair.Key))
            {
                actions.Add(new ActionExpression(action.Key, action.Value.Select((term, index) => new Term(
                    term.Value1, term.Value2, term.Operator, 0, (int)ItemTarget.Self,
                    "Rdb.ActionCriteria[" + action.Key + "][" + index + "]")).ToArray()));
            }
        }

        var proven = new List<ClassificationEvidence>();
        bool nonExpansionEntitlementOnly = false;
        foreach (ActionExpression action in actions)
        {
            if (!action.Terms.Any(IsExpansionLeaf))
                continue;
            if (!Enum.IsDefined(typeof(ActionType), action.ActionType))
                return Unknown("UNSUPPORTED_ITEM_ACTION_TYPE");
            if (actions.Count(other => other.ActionType == action.ActionType) != 1)
                return Unknown("DUPLICATE_ITEM_ACTION_REQUIREMENTS");
            Proof proof = Analyze(action);
            if (proof.Error != null)
                return Unknown(proof.Error);
            if (!proof.CanBeTrue)
                return Unknown("UNSATISFIABLE_ITEM_REQUIREMENT_EXPRESSION");
            if (proof.CanPassWithoutPositiveExpansion)
            {
                if (proof.Evidence.Count > 0)
                    return Unknown("EXPANSION_REQUIREMENT_HAS_UNRESTRICTED_ALTERNATIVE");
                nonExpansionEntitlementOnly = true;
                continue;
            }
            proven.AddRange(proof.Evidence.Select(SatisfiableAssociations)
                .Where(origin => origin.Associations.Length > 0));
        }

        if (proven.Count == 0)
            return Unknown(nonExpansionEntitlementOnly ? "NON_EXPANSION_ENTITLEMENT_ONLY"
                : evidence.HasExpansionInformation ? "NO_SUPPORTED_POSITIVE_ITEM_EXPANSION_REQUIREMENT"
                : evidence.PrimaryReason);
        ClassificationEvidence[] origins = proven.OrderBy(entry => entry.SourceField, StringComparer.Ordinal).ToArray();
        string[] associations = origins.SelectMany(entry => entry.Associations).Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (associations.Length == 0)
            return Unknown("NON_EXPANSION_ENTITLEMENT_ONLY");
        int[] values = origins.Select(entry => entry.Value).Distinct().ToArray();
        return new ExpansionClassification(associations.Length == 1 ? associations[0] : "MultipleExpansions",
            associations, values.Length == 1 ? values[0] : null, origins[0].SourceField,
            "PROVEN_ITEM_REQUIREMENT", associations.Length == 1 ? "POSITIVE_ITEM_EXPANSION_REQUIREMENT"
                : "MULTIPLE_EXPLICIT_ITEM_EXPANSION_ASSOCIATIONS", origins);
    }

    static Proof Analyze(ActionExpression action)
    {
        bool postfix = action.Terms.Any(term => IsLink(term.Operation))
            && action.Terms.Any(term => !IsLink(term.Operation) && !IsSelector(term.Operation))
            && action.Terms.Where(term => !IsLink(term.Operation) && !IsSelector(term.Operation))
                .All(term => term.ChildOperation == 0);
        int subject = (int)Operator.OnSelf;
        if (postfix)
        {
            var stack = new Stack<Proof>();
            foreach (Term term in action.Terms)
            {
                if (IsSelector(term.Operation))
                {
                    subject = term.Operation;
                    continue;
                }
                if (!IsLink(term.Operation))
                {
                    stack.Push(Leaf(term, subject, action.ActionType, "POSTFIX"));
                    continue;
                }
                if (stack.Count == 0)
                    return Invalid("MALFORMED_ITEM_REQUIREMENT_EXPRESSION");
                Proof right = stack.Pop();
                if (term.Operation == (int)Operator.Not)
                {
                    stack.Push(Negate(right));
                    continue;
                }
                if (stack.Count == 0)
                    return Invalid("MALFORMED_ITEM_REQUIREMENT_EXPRESSION");
                stack.Push(Combine(stack.Pop(), right, term.Operation == (int)Operator.Or));
            }
            return stack.Count == 1 ? stack.Pop() : Invalid("MALFORMED_ITEM_REQUIREMENT_EXPRESSION");
        }

        Proof? result = null;
        foreach (Term term in action.Terms)
        {
            if (IsLink(term.Operation))
            {
                if (result != null && term.Operation == (int)Operator.Not)
                    result = Negate(result);
                continue;
            }
            if (IsSelector(term.Operation))
            {
                subject = term.Operation;
                continue;
            }
            if (term.ChildOperation is not 0 and not (int)Operator.And and not (int)Operator.Or)
                return Invalid("UNSUPPORTED_ITEM_REQUIREMENT_CHILD_OPERATOR");
            Proof leaf = Leaf(term, subject, action.ActionType, "LEGACY_FOLD");
            subject = (int)Operator.OnSelf;
            result = result == null ? leaf : Combine(result, leaf, term.ChildOperation == (int)Operator.Or);
        }
        return result ?? Other();
    }

    static Proof Leaf(Term term, int subject, int actionType, string format)
    {
        if (!IsExpansionLeaf(term))
        {
            if (term.Operation == (int)Operator.True)
                return new Proof(true, false, true, false, null, [], AllKnownMasks);
            if (term.Operation == (int)Operator.False)
                return new Proof(false, true, false, false, null, [], BigInteger.Zero);
            return Other();
        }
        if (subject == (int)Operator.OnTarget || term.Target is not 0 and not (int)ItemTarget.Self
                and not (int)ItemTarget.User and not (int)ItemTarget.Wearer)
            return Invalid("EXPANSION_REQUIREMENT_IS_NOT_ON_ITEM_USER");
        if (term.Operation == (int)Operator.NotBitAnd)
            return Invalid("NEGATED_EXPANSION_REQUIREMENT");
        if (term.Operation is not (int)Operator.BitAnd and not (int)Operator.EqualTo)
            return Invalid("UNSUPPORTED_EXPANSION_REQUIREMENT_OPERATOR");
        if (term.Value == 0)
            return Invalid("ZERO_EXPANSION_MASK_HAS_NO_POSITIVE_ASSOCIATION");
        if (term.Value < 0 || (term.Value & ~(int)Expansions.All) != 0)
            return Invalid("UNSUPPORTED_EXPANSION_VALUE");

        string[] associations = Enum.GetValues<Expansions>()
            .Where(bit => (int)bit > 0 && ((int)bit & ((int)bit - 1)) == 0 && (term.Value & (int)bit) != 0)
            .Select(Association).Where(value => value != null).Cast<string>()
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        bool mailOnlyAlternative = term.Operation == (int)Operator.BitAnd
            && (term.Value & (int)Expansions.Mail) != 0;
        string meaning = term.Operation == (int)Operator.BitAnd ? "ANY_SET_BIT" : "EXACT_KNOWN_MASK";
        var origin = new ClassificationEvidence(term.SourceField, term.Value, actionType, associations,
            format + ":POSITIVE_" + meaning + ";FULL_ORDERED_EXPRESSION_RETAINED_IN_EXPANSION_EVIDENCE");
        BigInteger masks = MasksFor(term.Operation, term.Value);
        return new Proof(true, true, associations.Length == 0 || mailOnlyAlternative, true, null,
            associations.Length == 0 ? [] : [new ProvenOrigin(origin, masks)], masks);
    }

    static Proof Combine(Proof left, Proof right, bool isOr)
    {
        string? error = left.Error ?? right.Error;
        BigInteger masks = isOr ? left.PossibleMasks | right.PossibleMasks
            : left.PossibleMasks & right.PossibleMasks;
        bool canTrue = !masks.IsZero;
        bool canFalse = isOr ? left.CanBeFalse && right.CanBeFalse : left.CanBeFalse || right.CanBeFalse;
        bool without = isOr ? left.CanPassWithoutPositiveExpansion || right.CanPassWithoutPositiveExpansion
            : left.CanPassWithoutPositiveExpansion && right.CanPassWithoutPositiveExpansion;
        var origins = new List<ProvenOrigin>();
        if (canTrue)
        {
            if (!isOr || left.CanBeTrue)
                origins.AddRange(left.Evidence);
            if (!isOr || right.CanBeTrue)
                origins.AddRange(right.Evidence);
        }
        // An enclosing exact-mask constraint can make a previously possible OR branch unreachable.
        origins = origins.Select(origin => origin with { PossibleMasks = origin.PossibleMasks & masks })
            .Where(origin => !origin.PossibleMasks.IsZero).ToList();
        return new Proof(canTrue, canFalse, canTrue && without,
            left.ContainsExpansion || right.ContainsExpansion, error, origins, masks);
    }

    static Proof Negate(Proof value)
        => value.ContainsExpansion ? Invalid("NEGATED_EXPANSION_REQUIREMENT")
            : new Proof(value.CanBeFalse, value.CanBeTrue, value.CanBeFalse, false, value.Error, [],
                value.CanBeFalse ? AllKnownMasks : BigInteger.Zero);

    static Proof Other() => new(true, true, true, false, null, [], AllKnownMasks);

    static Proof Invalid(string reason) => new(true, true, true, true, reason, [], AllKnownMasks);

    static BigInteger MasksFor(int operation, int value)
        => PositiveMasks.GetOrAdd((operation, value), key =>
        {
            BigInteger result = BigInteger.Zero;
            for (int mask = 0; mask <= (int)Expansions.All; mask++)
            {
                bool matches = key.Operation == (int)Operator.EqualTo ? mask == key.Value : (mask & key.Value) != 0;
                if (matches)
                    result |= BigInteger.One << mask;
            }
            return result;
        });

    static ClassificationEvidence SatisfiableAssociations(ProvenOrigin origin)
    {
        string[] associations = origin.Evidence.Associations.Where(association =>
        {
            int originalBits = Enum.GetValues<Expansions>()
                .Where(flag => Association(flag) == association)
                .Aggregate(0, (mask, flag) => mask | ((int)flag & origin.Evidence.Value));
            return originalBits != 0
                && !(origin.PossibleMasks & MasksFor((int)Operator.BitAnd, originalBits)).IsZero;
        }).ToArray();
        return origin.Evidence with
        {
            Associations = associations,
            Logic = origin.Evidence.Logic + ";SATISFIABLE_ASSOCIATIONS_ONLY"
        };
    }

    static bool IsExpansionLeaf(Term term)
        => term.Stat == (int)CharacterStat.Expansion && !IsLink(term.Operation) && !IsSelector(term.Operation);

    static bool IsLink(int operation)
        => operation is (int)Operator.And or (int)Operator.Or or (int)Operator.Not;

    static bool IsSelector(int operation)
        => operation is (int)Operator.OnSelf or (int)Operator.OnUser or (int)Operator.OnCaster
            or (int)Operator.OnTarget;

    static string? Association(Expansions value) => value switch
    {
        Expansions.NotumWars => "NotumWars",
        Expansions.ShadowLands or Expansions.ShadowLandsPreOrder => "Shadowlands",
        Expansions.AlienInvasion or Expansions.AlienInvasionPreOrder => "AlienInvasion",
        Expansions.LostEden or Expansions.LostEdenPreOrder => "LostEden",
        Expansions.LegacyOfTheXan or Expansions.LegacyOfTheXanPreOrder => "Xan",
        Expansions.PMVObsidianEdition => "PMVObsidianEdition",
        _ => null
    };

    static ExpansionClassification Unknown(string reason)
        => new("Unknown", [], null, null, "UNKNOWN", reason, []);

    sealed record ActionExpression(int ActionType, Term[] Terms);

    sealed record Term(int Stat, int Value, int Operation, int ChildOperation, int Target, string SourceField);

    sealed record ProvenOrigin(ClassificationEvidence Evidence, BigInteger PossibleMasks);

    sealed record Proof(bool CanBeTrue, bool CanBeFalse, bool CanPassWithoutPositiveExpansion,
        bool ContainsExpansion, string? Error, List<ProvenOrigin> Evidence, BigInteger PossibleMasks);
}
