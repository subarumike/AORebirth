using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class DoaRewardBoundary
{
    private const string Zone = "AORebirth/Server/ZoneEngine_New/";
    private const string Quests = Zone + "Core/Quests/";
    private const string Missions = Zone + "Core/Missions/";
    private const string Persistence = "AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/";
    internal const string ModulePath = Quests + "RewardExecutionModule.cs";
    internal const string FollowUpPath = Quests + "QuestService.FollowUps.cs";
    private const string ModuleType = "RewardExecutionModule";
    private const string ModuleContract = "IRewardExecutionModule";

    private static readonly string[] ProgressAwards =
        ["AwardXp", "AwardDirectXp", "AwardDirectSk", "ApplyResearchXp", "AddToDeathXpPool"];
    private static readonly string[] RewardValueTargets =
        ["RewardXp", "RewardXP", "RewardCredits", "RewardCash", "RewardWeight", "RewardChance", "RewardProbability"];

    internal static IEnumerable<(SyntaxNode Node, string Rule, string Basis)> SourceIssues(string path, SyntaxNode syntax)
    {
        bool zone = path.StartsWith(Zone, StringComparison.Ordinal);
        if (!zone && !path.StartsWith(Persistence, StringComparison.Ordinal))
            yield break;

        foreach (var type in syntax.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            string name = type.Identifier.ValueText;
            if (name.Contains("Reward", StringComparison.Ordinal)
                && (name.EndsWith("Ledger", StringComparison.Ordinal) || name.EndsWith("Database", StringComparison.Ordinal)
                    || name.EndsWith("Store", StringComparison.Ordinal) || name.EndsWith("Repository", StringComparison.Ordinal)
                    || name.EndsWith("Persistence", StringComparison.Ordinal)))
                yield return (type, "DOA_COMPETING_REWARD_LEDGER",
                    "Reward execution must use the existing mission ledger and DAO contracts.");

            if (path != ModulePath && (name is ModuleType or ModuleContract
                || type.BaseList?.Types.Any(parent => Name(parent.Type) == ModuleContract) == true))
                yield return (type, "DOA_DUPLICATE_REWARD_EXECUTION",
                    "Use the existing reward module instead of declaring another execution owner.");
            if (zone && type.BaseList?.Types.Any(parent => Name(parent.Type) == "ICharacterQuestCompletionTransaction") == true)
                yield return (type, "DOA_COMPETING_REWARD_LEDGER",
                    "Quest reward persistence belongs to the established DAO implementation.");
        }

        if (!zone) yield break;

        bool dispatch = path.StartsWith(Zone + "Core/MessageHandlers/", StringComparison.Ordinal)
            || path.StartsWith(Zone + "Core/Network/", StringComparison.Ordinal) || path == Zone + "Program.cs";
        bool module = path == ModulePath || path.StartsWith(Zone + "Core/Rewards/", StringComparison.Ordinal);
        foreach (var name in syntax.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            // Both an invocation and a captured method group can publish durable effects.
            if (name.Parent is MemberAccessExpressionSyntax access ? access.Name != name
                : name.Parent is not (InvocationExpressionSyntax or MemberBindingExpressionSyntax))
                continue;

            string operation = Name(name);
            if (dispatch && ProgressAwards.Contains(operation, StringComparer.Ordinal))
                yield return (name, "DOA_GAMEPLAY_IN_DISPATCH",
                    "Dispatch reward requests to the owning module instead of changing character progression.");

            bool atomicOperation = operation is "TryEarnCharacterQuest" or "TryCompleteCharacterQuest"
                or "TryApplyCharacterQuestFollowUp" or "TryApplyCharacterStatReward" or "TryApplyCharacterStatRewardWithResearch";
            if (operation is "TryClaimReward" or "TryMarkRewardApplied" or "TryMarkRewardFailed"
                || atomicOperation && !UsesEstablishedTransaction(path, name, operation))
                yield return (name, "DOA_REWARD_TRANSACTION_BYPASS",
                    "Use the coordinated mission reward transaction. Never separate a durable reward from its applied marker.");

            if (module && (ProgressAwards.Contains(operation, StringComparer.Ordinal)
                || operation is "ExecuteOnUseSpells" or "TryAssignTemplate"))
                yield return (name, "DOA_UNTRACKED_REWARD_EFFECT",
                    "Plan durable reward effects under the existing transaction or provide an idempotent recovery contract.");
        }

        if (!module && !path.StartsWith(Quests, StringComparison.Ordinal) && !path.StartsWith(Missions, StringComparison.Ordinal))
            yield break;

        foreach (var assignment in syntax.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (IsAmountLiteral(assignment.Right) && IsAuthoredValue(assignment))
                yield return (assignment, "DOA_FIXED_REWARD_VALUE", "Reward values belong in validated GameData.");
        }

        foreach (var variable in syntax.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (RewardValueTargets.Contains(variable.Identifier.ValueText, StringComparer.Ordinal)
                && variable.Initializer is { Value: var value } && IsAmountLiteral(value))
                yield return (variable, "DOA_FIXED_REWARD_VALUE", "Reward values belong in validated GameData.");
        }

        foreach (var call in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is MemberAccessExpressionSyntax member && Name(member.Expression) == "DirectXpRewardPlan"
                && Name(member) is "Create" or "CreateQuestReward" or "CreatePersistedReward"
                && call.ArgumentList.Arguments.Count == 2 && IsAmountLiteral(call.ArgumentList.Arguments[1].Expression))
                yield return (call, "DOA_FIXED_REWARD_VALUE", "Resolve the XP amount from the validated reward definition.");
        }

        foreach (var fallback in syntax.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (fallback.IsKind(SyntaxKind.CoalesceExpression) && IsAmountLiteral(fallback.Right)
                && RewardValueTargets.Contains(Name(fallback.Left), StringComparer.Ordinal))
                yield return (fallback, "DOA_FIXED_REWARD_VALUE", "Missing reward content must not default to an authored amount.");
        }
    }

    private static bool UsesEstablishedTransaction(string path, SyntaxNode operation, string operationName)
    {
        if (operationName == "TryEarnCharacterQuest")
            return path == Quests + "QuestService.Tokens.cs" && IsMember(operation, "QuestService", "EarnWithToken")
                && InTransaction(operation);

        if (operationName == "TryApplyCharacterQuestFollowUp")
            return path == FollowUpPath && IsMember(operation, "QuestService", "TryApplyQuestFollowUp")
                && operation.Ancestors().OfType<LambdaExpressionSyntax>().Any(callback =>
                    callback.Parent is ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax invoke }
                    && invoke.Expression is MemberAccessExpressionSyntax access && Name(access) == "Execute"
                    && Name(access.Expression) == "_completionDao");

        if (path == ModulePath && operation.Ancestors().OfType<TypeDeclarationSyntax>()
                .FirstOrDefault()?.Identifier.ValueText == ModuleType)
            return InTransaction(operation);

        return operationName is "TryApplyCharacterStatReward" or "TryApplyCharacterStatRewardWithResearch"
            && path == Missions + "AuthoredQuestService.TimedTurnIn.cs"
            && IsMember(operation, "AuthoredQuestService", "TryTurnInTimedItem")
            && InTransaction(operation);
    }

    private static bool InTransaction(SyntaxNode operation)
        => operation.Ancestors().OfType<LambdaExpressionSyntax>().Any(callback =>
            callback.Parent is ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax invoke }
            && (invoke.Expression is MemberAccessExpressionSyntax access && Name(access) == "Execute"
                && DoaModuleBoundary.ReceiverType(invoke, access.Expression) is TypeSyntax receiver
                && Name(receiver is NullableTypeSyntax nullable ? nullable.ElementType : receiver) == "IMissionDao"
                || invoke.Expression is IdentifierNameSyntax target && Name(target) == "Mutate"
                && IsMember(invoke, "AuthoredQuestService", "TryTurnInTimedItem")))
        || operation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.ParameterList.Parameters
            .Any(parameter => parameter.Type != null
                && Name(parameter.Type) is "IMissionDaoTransaction" or "ICharacterQuestCompletionTransaction") == true;

    private static bool IsMember(SyntaxNode node, string owner, string method)
        => node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText == owner
            && node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText == method;

    private static bool IsAuthoredValue(AssignmentExpressionSyntax assignment)
    {
        string target = Name(assignment.Left);
        if (RewardValueTargets.Contains(target, StringComparer.Ordinal)) return true;
        if (assignment.Parent is not InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax definition }) return false;
        string type = definition switch
        {
            ObjectCreationExpressionSyntax explicitType => Name(explicitType.Type),
            _ when definition.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax
                { Parent: VariableDeclarationSyntax declaration } } => Name(declaration.Type),
            _ when definition.Parent is AssignmentExpressionSyntax value && Name(value.Left) == "Reward" => "QuestReward",
            _ => string.Empty
        };
        return type switch
        {
            "QuestReward" => target is "Xp" or "Credits",
            "QuestRewardItem" => target is "Id" or "HighId" or "Quality" or "Count",
            _ => false
        };
    }

    private static bool IsAmountLiteral(ExpressionSyntax value)
    {
        while (value is ParenthesizedExpressionSyntax parentheses) value = parentheses.Expression;
        if (value is PrefixUnaryExpressionSyntax unary && (unary.IsKind(SyntaxKind.UnaryMinusExpression)
            || unary.IsKind(SyntaxKind.UnaryPlusExpression)))
            value = unary.Operand;
        return value is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NumericLiteralExpression)
            && literal.Token.Value is IConvertible amount && amount.ToDouble(null) != 0;
    }

    private static string Name(SyntaxNode node) => node switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        ConditionalAccessExpressionSyntax conditional => Name(conditional.WhenNotNull),
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        PostfixUnaryExpressionSyntax suppression when suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression) => Name(suppression.Operand),
        _ => string.Empty
    };
}
