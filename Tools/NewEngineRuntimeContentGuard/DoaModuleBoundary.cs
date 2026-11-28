using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class DoaModuleBoundary
{
    private const string Zone = "AORebirth/Server/ZoneEngine_New/";
    private const string Core = Zone + "Core/";
    private const string Database = "AORebirth/Libraries/Source/AORebirth.Database/";
    private const string Persistence = "AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/";
    private const string TerminalHandler = Core + "MessageHandlers/QuestAlternativeMessageHandler.cs";
    private const string AcceptanceHandler = Core + "MessageHandlers/CreateQuestMessageHandler.cs";
    private const string JournalHandler = Core + "MessageHandlers/QuestMessageHandler.cs";
    private const string DungeonService = Core + "Quests/Dungeons/QuestDungeonService.cs";
    private const string AcceptanceModule = Core + "Quests/MissionAcceptanceModule.cs";
    private const string ObjectiveModule = Core + "Quests/MissionObjectiveModule.cs";

    private static readonly string[] ProviderNamespaces =
        ["Dapper", "MySqlConnector", "MySql.Data.MySqlClient", "Microsoft.Data.SqlClient", "System.Data.SqlClient",
         "Microsoft.Data.Sqlite", "System.Data.SQLite", "System.Data.Odbc", "Npgsql", "Microsoft.EntityFrameworkCore", "NHibernate"];

    private static readonly Regex Sql = new(
        @"^\s*(?:SELECT\s+(?:\d+\s*;?\s*$|[^;]*?\bFROM\b)|INSERT\s+INTO\b|UPDATE\s+[^.;]+?\s+SET\b|DELETE\s+FROM\b|MERGE\s+INTO\b|(?:CREATE|ALTER|DROP|TRUNCATE)\s+TABLE\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(150));

    private static readonly string[] GameplayOperations =
        ["ReserveIdentities", "PublishOffers", "RollMission", "AcceptMission", "GenerateMission", "GrantReward", "AwardReward",
         "RollLoot", "GenerateLoot", "ApplyDamage", "ResolveAttack", "GrantItem", "MintItem", "SpawnNpc", "TryEquipItem",
         "OnNpcInspected", "TryUseGeneratedMissionItem", "TryEarnCharacterQuest", "TryCompleteCharacterQuest"];

    private static readonly (string Module, string[] Prefixes)[] ModuleOwners =
    [
        ("Mission", ["Mission", "Missions"]),
        ("Quest", ["Quest", "Quests"]),
        ("Loot", ["Loot"]),
        ("Combat", ["Combat"]),
        ("Inventory", ["Inventory"]),
        ("Npc", ["Ai", "Npc", "Npcs", "Mob", "Mobs"]),
        ("Trade", ["Trade"]),
        ("Dialogue", ["Dialogue"]),
        ("Shop", ["Shop", "Shops"])
    ];
    private static readonly IReadOnlyDictionary<string, string> OwnedServices = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GeneratedMissionService"] = "Mission",
        ["QuestDungeonService"] = "Quest",
        ["MissionAcceptanceModule"] = "Quest",
        ["MissionObjectiveModule"] = "Quest",
        ["GeneratedMissionLifecycleModule"] = "Quest",
        ["InventoryActionService"] = "Inventory",
        ["TradeService"] = "Trade"
    };

    internal static IEnumerable<(SyntaxNode Node, string Rule, string Basis)> SourceIssues(string path, SyntaxNode syntax)
    {
        bool zone = path.StartsWith(Zone, StringComparison.Ordinal);
        bool handlers = path.StartsWith(Core + "MessageHandlers/", StringComparison.Ordinal)
            || path.StartsWith(Core + "Network/", StringComparison.Ordinal);
        bool composition = path == Zone + "Program.cs";
        bool daoOwner = path.StartsWith(Database + "Dao/", StringComparison.Ordinal)
            || path.StartsWith(Database + "Domain/", StringComparison.Ordinal);
        bool enforcePersistence = zone || path.StartsWith("AORebirth/Libraries/Source/", StringComparison.Ordinal)
            && !path.Contains("/AOtomation/", StringComparison.Ordinal);
        if (!enforcePersistence) yield break;

        var imports = syntax.DescendantNodes().OfType<UsingDirectiveSyntax>().ToArray();
        var aliases = imports.Where(import => import.Alias != null && import.Name != null)
            .GroupBy(import => import.Alias!.Name.Identifier.ValueText, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(import => Name(import.Name!)).ToArray(), StringComparer.Ordinal);

        if (!daoOwner)
        {
            foreach (var import in imports)
            {
                string imported = import.Name?.ToString().Replace("global::", "", StringComparison.Ordinal) ?? "";
                if (ProviderNamespaces.Any(provider => imported == provider || imported.StartsWith(provider + ".", StringComparison.Ordinal)))
                    yield return (import, "DOA_DIRECT_DATABASE_ACCESS", "Database providers and SQL mappers belong in DAO implementations.");
            }

            foreach (var literal in syntax.DescendantNodes().OfType<LiteralExpressionSyntax>())
            {
                if (literal.Token.Value is string value && Sql.IsMatch(value))
                    yield return (literal, "DOA_DIRECT_DATABASE_ACCESS", "SQL belongs in the DAO implementation.");
            }
            foreach (var interpolation in syntax.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>())
            {
                string text = string.Concat(interpolation.Contents.Select(piece =>
                    piece is InterpolatedStringTextSyntax literal ? literal.TextToken.ValueText : "?"));
                if (Sql.IsMatch(text))
                    yield return (interpolation, "DOA_DIRECT_DATABASE_ACCESS", "Build SQL only inside the DAO implementation.");
            }

            foreach (var creation in syntax.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                string type = CreatedType(creation);
                if (IsType(type, IsDatabaseObject) && !IsConnectorFactory(path, creation))
                    yield return (creation, "DOA_DIRECT_DATABASE_ACCESS", "Construct provider database objects only in their authorized persistence owner.");
                if (IsType(type, IsConcreteDao) && !IsCompositionWiring(path, creation))
                    yield return (creation, "DOA_CONCRETE_DAO", "Inject the existing DAO contract. Bound concrete DAO construction belongs to composition or the DAO implementation.");
            }
        }

        if (zone || path.StartsWith(Persistence, StringComparison.Ordinal))
        {
            foreach (var declaration in syntax.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                string type = declaration.Identifier.ValueText;
                if (IsPersistenceContract(type))
                    yield return (declaration, "DOA_COMPETING_PERSISTENCE", "Extend the existing persistence contract instead of adding a separate persistence abstraction.");
            }
        }

        string? owner = OwnerOfPath(path);
        if (owner != null || handlers)
        {
            foreach (var type in syntax.DescendantNodes().OfType<TypeSyntax>()
                .Where(type => type.Parent is ParameterSyntax or VariableDeclarationSyntax or PropertyDeclarationSyntax or TypeArgumentListSyntax))
            {
                string name = Name(type);
                if (IsType(name, IsConcreteDao))
                    yield return (type, "DOA_CONCRETE_DAO", "Gameplay and network consumers depend on DAO interfaces.");
                if (IsType(name, candidate => ForeignModule(owner, candidate)))
                    yield return (type, "DOA_UNAUTHORIZED_MODULE_DEPENDENCY", "Access another gameplay owner through its module contract.");
            }
            foreach (var creation in syntax.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                if (IsType(Name(creation.Type), candidate => ForeignModule(owner, candidate)))
                    yield return (creation, "DOA_UNAUTHORIZED_MODULE_DEPENDENCY", "Gameplay modules receive other owners through their declared contracts.");

            foreach (var import in imports)
            {
                string[] parts = (import.Name?.ToString() ?? "").Split('.');
                int core = Array.IndexOf(parts, "Core");
                if (core < 0 || core + 2 >= parts.Length) continue;
                string? importedOwner = Owner(parts[core + 1], exact: true);
                if (importedOwner != null && importedOwner != owner && parts.Skip(core + 2).Any(part => part is "Internal" or "Implementation"))
                    yield return (import, "DOA_UNAUTHORIZED_MODULE_DEPENDENCY", "An implementation namespace is private to its gameplay module.");
            }
        }

        foreach (var call in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string method = Name(call.Expression);
            if (!daoOwner && IsDatabaseCall(call, method))
                yield return (call, "DOA_DIRECT_DATABASE_ACCESS", "Execute database commands and open transactions in the DAO implementation.");

            if ((handlers || composition) && GameplayOperations.Contains(method, StringComparer.Ordinal))
                yield return (call, "DOA_GAMEPLAY_IN_DISPATCH", "Delegate gameplay operations to the owning module from a handler or composition root.");

            if (zone && MissionWorkflowIssue(path, call, method) is string reason)
                yield return (call, "DOA_TRANSACTION_BYPASS", reason);

            if ((handlers || composition) && method == "TryAbandon"
                && (call.Expression is not MemberAccessExpressionSyntax journal
                    || !IsType(ReceiverType(call, journal.Expression) ?? "", type => type == "IGeneratedMissionLifecycleModule")))
                yield return (call, "DOA_GAMEPLAY_IN_DISPATCH", "Dispatch journal abandonment through IGeneratedMissionLifecycleModule.");

            // The single-argument DAO operation must pass through the service that coordinates
            // the player's persistence gate, commit outcome and in-memory cash update.
            if (zone && method == "PublishOffers" && call.ArgumentList.Arguments.Count == 1
                && !(path == Core + "Missions/GeneratedMissionService.cs" && IsMember(call, "GeneratedMissionService", "PublishOffers")))
                yield return (call, "DOA_TRANSACTION_BYPASS", "Publish mission offers through GeneratedMissionService.PublishOffers.");

            if (zone && method == "Persist" && call.Expression is MemberAccessExpressionSyntax receiver
                && ReceiverType(call, receiver.Expression) is string contract)
            {
                if (IsType(contract, type => type is "IInventoryMutationPersistence" or "MySqlInventoryMutationPersistence")
                    && !(path == Core + "Inventory/InventoryActionService.cs" && IsMember(call, "InventoryActionService", "TryCommit")))
                    yield return (call, "DOA_TRANSACTION_BYPASS", "Commit inventory mutations through InventoryActionService.TryCommit.");
                if (IsType(contract, type => type is "ITradePersistence" or "MySqlTradePersistence")
                    && !(path == Core + "Trade/TradeService.cs" && IsMember(call, "TradeService", "PersistPlan")))
                    yield return (call, "DOA_TRANSACTION_BYPASS", "Commit trade through TradeService so inventory and currency changes share its transaction.");
            }
        }

        if (path is TerminalHandler or AcceptanceHandler)
        {
            var (contract, action, packet) = path == TerminalHandler
                ? ("IMissionTerminalModule", "Roll", "QuestAlternativeMessage")
                : ("IMissionAcceptanceModule", "Accept", "CreateQuestMessage");
            foreach (var method in syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method => method.Identifier.ValueText == "Handle"))
                if (!IsPacketDispatch(method, contract, action, packet))
                    yield return (method, "DOA_GAMEPLAY_IN_DISPATCH", "Forward the decoded message and session to " + contract + ".");
        }

        if (path == JournalHandler)
        {
            foreach (var method in syntax.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(method => method.Identifier.ValueText == "Handle"
                    && method.ParameterList.Parameters.FirstOrDefault()?.Type is TypeSyntax packet && Name(packet) == "QuestMessage"))
                if (!method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                    call.Expression is MemberAccessExpressionSyntax receiver && Name(receiver) == "TryAbandon"
                    && IsType(ReceiverType(call, receiver.Expression) ?? "", type => type == "IGeneratedMissionLifecycleModule")))
                    yield return (method, "DOA_GAMEPLAY_IN_DISPATCH", "The journal handler must dispatch abandonment to its lifecycle module.");
        }

        bool IsType(string type, Func<string, bool> predicate) => predicate(type)
            || aliases.TryGetValue(type, out string[]? targets) && targets.Any(predicate);
    }

    private static string? MissionWorkflowIssue(string path, InvocationExpressionSyntax call, string method)
    {
        switch (method)
        {
            case "AcceptOffer":
                return path == AcceptanceModule && IsMember(call, "MissionAcceptanceModule", "Accept")
                    ? null : "Accept offers through IMissionAcceptanceModule and the established dungeon transaction owner.";
            case "ReadCurrentOffer":
                return path == DungeonService && IsMember(call, "QuestDungeonService", "AcceptOffer") && HoldsPlayerGate(call)
                    ? null : "Read the exact current offer within the gated acceptance workflow.";
            case "ReadOffers" when path == DungeonService && IsMember(call, "QuestDungeonService", "AcceptOffer"):
                return "Read the exact current offer instead of traversing offer history during acceptance.";
            case "TryAcceptOfferQuest":
                if (path == DungeonService && IsMember(call, "QuestDungeonService", "AcceptOffer") && HoldsPlayerGate(call)
                    || path == Core + "Data/CharacterQuestStore.cs" && IsMember(call, "MySqlCharacterQuestStore", method)
                    || path == Core + "Data/CharacterQuestStore.cs" && IsMember(call, "ICharacterQuestStore", method))
                    return null;
                return "Persist accepted missions only through the existing character-quest store and gated acceptance workflow.";
            case "TryEarnCharacterQuest":
                return path == Core + "Quests/QuestService.Tokens.cs" && IsMember(call, "QuestService", "EarnWithToken")
                    ? null : "Earn mission completion through the existing transaction and token-freeze owner.";
            case "TryEarnBoundObjective":
            case "TryCommitObjectivePickup":
                return path == ObjectiveModule
                    && call.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText == "MissionObjectiveModule"
                    ? null : "Only the mission objective module submits an authorized objective to QuestService for persistence.";
            case "TryCollectCharacterQuestItem":
            case "TryCompleteCharacterQuest":
                return path == Core + "Quests/QuestService.Completion.cs"
                    && (HoldsPlayerGate(call) || RequiresHeldPlayerGate(call))
                    && call.Ancestors().OfType<InvocationExpressionSyntax>().Any(IsMissionCompletionTransaction)
                    ? null : "Use the existing mission DAO transaction under the player persistence gate.";
            default:
                return null;
        }
    }

    private static bool HoldsPlayerGate(SyntaxNode node)
        => node.Ancestors().OfType<LockStatementSyntax>().Any(scope => scope.Expression
            is MemberAccessExpressionSyntax access && Name(access) == "PersistenceGate");

    private static bool IsMissionCompletionTransaction(InvocationExpressionSyntax call)
    {
        if (call.Expression is not MemberAccessExpressionSyntax execute || Name(execute) != "Execute")
            return false;
        if (ReceiverType(call, execute.Expression, unwrapNullable: true) is string type)
            return type == "IMissionDao";

        // The other QuestService partial declares this field. The caller has already checked the completion file.
        ExpressionSyntax receiver = execute.Expression is PostfixUnaryExpressionSyntax suppression
            && suppression.OperatorToken.ValueText == "!" ? suppression.Operand : execute.Expression;
        return receiver is IdentifierNameSyntax name && name.Identifier.ValueText == "_completionDao"
            && call.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText == "QuestService";
    }

    private static bool RequiresHeldPlayerGate(InvocationExpressionSyntax call)
        => call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Body?.Statements.FirstOrDefault() is
            IfStatementSyntax
            {
                Condition: PrefixUnaryExpressionSyntax { Operand: InvocationExpressionSyntax check } negation,
                Statement: ThrowStatementSyntax,
                Else: null
            }
        && negation.OperatorToken.ValueText == "!"
        && check.Expression is MemberAccessExpressionSyntax member && Name(member) == "IsEntered" && Name(member.Expression) == "Monitor"
        && check.ArgumentList.Arguments is [ArgumentSyntax { Expression: MemberAccessExpressionSyntax gate }]
        && Name(gate) == "PersistenceGate";

    private static bool IsPacketDispatch(MethodDeclarationSyntax method, string contract, string action, string packet)
    {
        var parameters = method.ParameterList.Parameters;
        ExpressionSyntax? expression = method.ExpressionBody?.Expression;
        if (method.Body?.Statements is [ExpressionStatementSyntax statement]) expression = statement.Expression;
        if (parameters.Count != 2 || expression is not InvocationExpressionSyntax call
            || call.ArgumentList.Arguments.Count != 2 || call.ArgumentList.Arguments[1].Expression is not IdentifierNameSyntax session
            || Name(session) != parameters[1].Identifier.ValueText)
            return false;

        string input = parameters[0].Identifier.ValueText;
        ExpressionSyntax message = call.ArgumentList.Arguments[0].Expression;
        if (parameters[0].Type != null && Name(parameters[0].Type!) == "MessageBody")
            return call.Expression is IdentifierNameSyntax target && Name(target) == "Handle"
                && message is CastExpressionSyntax cast && Name(cast.Type) == packet
                && cast.Expression is IdentifierNameSyntax argument && Name(argument) == input;

        if (call.Expression is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver } member
            || Name(member) != action || message is not IdentifierNameSyntax request || Name(request) != input
            || method.Parent is not ClassDeclarationSyntax handler)
            return false;

        return handler.ParameterList?.Parameters.Any(parameter => parameter.Type != null
            && Name(parameter.Type) == contract && parameter.Identifier.ValueText == Name(receiver)) == true
            || handler.Members.OfType<FieldDeclarationSyntax>().Any(field => Name(field.Declaration.Type) == contract
                && field.Declaration.Variables.Any(variable => variable.Identifier.ValueText == Name(receiver)));
    }

    private static bool IsDatabaseCall(InvocationExpressionSyntax call, string method)
    {
        if (method is "BeginTransaction" or "BeginTransactionAsync" or "ExecuteNonQuery" or "ExecuteNonQueryAsync"
            or "ExecuteReader" or "ExecuteReaderAsync" or "ExecuteScalar" or "ExecuteScalarAsync" or "CreateCommand")
            return true;
        if (call.Expression is not MemberAccessExpressionSyntax member) return false;
        string receiver = Name(member.Expression);
        return (method is "GetConnection" or "OpenConnection" && (receiver is "Connector" or "DatabaseDaoFactory" || IsDatabaseObject(receiver)))
            || (method is "Query" or "QueryAsync" or "QuerySingle" or "QuerySingleOrDefault" or "Execute" or "ExecuteAsync"
                && (receiver.TrimStart('_').Equals("db", StringComparison.OrdinalIgnoreCase)
                    || receiver.Contains("connection", StringComparison.OrdinalIgnoreCase) || receiver == "SqlMapper"));
    }

    private static string? ReceiverType(InvocationExpressionSyntax call, ExpressionSyntax receiver, bool unwrapNullable = false)
    {
        string TypeName(TypeSyntax type) => Name(unwrapNullable && type is NullableTypeSyntax nullable ? nullable.ElementType : type);

        while (receiver is ParenthesizedExpressionSyntax parentheses) receiver = parentheses.Expression;
        if (unwrapNullable && receiver is PostfixUnaryExpressionSyntax suppression && suppression.OperatorToken.ValueText == "!")
            receiver = suppression.Operand;
        if (receiver is CastExpressionSyntax cast) return TypeName(cast.Type);
        if (receiver is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax resolver }
            } && resolver.Identifier.ValueText is "GetService" or "GetRequiredService"
            && resolver.TypeArgumentList.Arguments is [TypeSyntax type])
            return TypeName(type);

        if (receiver is not (IdentifierNameSyntax or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax }))
            return null;

        string identifier = Name(receiver);
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var owner = call.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        var parameter = method?.ParameterList.Parameters.FirstOrDefault(parameter => parameter.Identifier.ValueText == identifier)
            ?? owner?.ParameterList?.Parameters.FirstOrDefault(parameter => parameter.Identifier.ValueText == identifier);
        if (parameter?.Type is TypeSyntax declared) return TypeName(declared);
        var local = method?.DescendantNodes().OfType<VariableDeclarationSyntax>()
            .FirstOrDefault(declaration => declaration.Variables.Any(variable => variable.Identifier.ValueText == identifier)
                && declaration.Parent is LocalDeclarationStatementSyntax statement
                && statement.SpanStart < call.SpanStart && call.Ancestors().Contains(statement.Parent));
        if (local != null) return TypeName(local.Type);
        var field = owner?.Members.OfType<FieldDeclarationSyntax>().FirstOrDefault(declaration =>
            declaration.Declaration.Variables.Any(variable => variable.Identifier.ValueText == identifier));
        return field == null ? null : TypeName(field.Declaration.Type);
    }

    private static bool IsDatabaseObject(string type) => type is "DbConnection" or "DbCommand" or "DbContext" or "TransactionScope"
        || (type.StartsWith("MySql", StringComparison.Ordinal) || type.StartsWith("Sql", StringComparison.Ordinal)
            || type.StartsWith("SQLite", StringComparison.Ordinal) || type.StartsWith("Npgsql", StringComparison.Ordinal)
            || type.StartsWith("Odbc", StringComparison.Ordinal) || type.StartsWith("OleDb", StringComparison.Ordinal))
        && (type.EndsWith("Connection", StringComparison.Ordinal) || type.EndsWith("Command", StringComparison.Ordinal)
            || type.EndsWith("Transaction", StringComparison.Ordinal));

    private static bool IsConcreteDao(string type)
        => !IsInterfaceName(type) && (type == "Dao" || type.EndsWith("Dao", StringComparison.Ordinal)
            || (type.StartsWith("MySql", StringComparison.Ordinal) || type.StartsWith("Sql", StringComparison.Ordinal))
                && (type.EndsWith("Repository", StringComparison.Ordinal) || type.EndsWith("Persistence", StringComparison.Ordinal)));

    private static bool IsPersistenceContract(string type)
        => IsInterfaceName(type) && (type.EndsWith("Dao", StringComparison.Ordinal)
            || type.EndsWith("DaoTransaction", StringComparison.Ordinal) || type.EndsWith("Repository", StringComparison.Ordinal)
            || type.EndsWith("Persistence", StringComparison.Ordinal) || type.EndsWith("UnitOfWork", StringComparison.Ordinal)
            || type.EndsWith("DataAccess", StringComparison.Ordinal)
            || type.EndsWith("Store", StringComparison.Ordinal) && Owner(type[1..], exact: false) != null)
        || type.EndsWith("DbContext", StringComparison.Ordinal);

    private static bool IsInterfaceName(string type) => type.Length > 1 && type[0] == 'I' && char.IsUpper(type[1]);

    private static bool ForeignModule(string? owner, string candidate)
        => OwnedServices.TryGetValue(candidate, out string? serviceOwner) ? serviceOwner != owner
            : candidate.EndsWith("Module", StringComparison.Ordinal) && !IsInterfaceName(candidate)
                && Owner(candidate, exact: false) is string target && target != owner;

    internal static bool IsGameplayOwner(string path) => OwnerOfPath(path) != null;

    private static string? OwnerOfPath(string path)
        => path.StartsWith(Core, StringComparison.Ordinal) ? Owner(path[Core.Length..].Split('/')[0], exact: true) : null;

    private static string? Owner(string value, bool exact)
        => ModuleOwners.FirstOrDefault(owner => owner.Prefixes.Any(prefix => exact ? value == prefix
            : value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).Module;

    private static bool IsCompositionWiring(string path, SyntaxNode node)
        => path == Zone + "Program.cs" && IsMember(node, "Program", "BuildRootServices")
            && node.Ancestors().OfType<InvocationExpressionSyntax>().Any(call => call.Expression is MemberAccessExpressionSyntax member
                && Name(member.Expression) == "services" && Name(member) is "AddSingleton" or "AddScoped" or "AddTransient");

    private static bool IsConnectorFactory(string path, BaseObjectCreationExpressionSyntax node)
        => IsCompositionWiring(path, node)
            && node.Ancestors().OfType<BaseObjectCreationExpressionSyntax>().Any(owner => IsConcreteDao(CreatedType(owner)));

    private static bool IsMember(SyntaxNode node, string type, string method)
        => node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText == method
            && node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText == type;

    private static string CreatedType(BaseObjectCreationExpressionSyntax creation) => creation switch
    {
        ObjectCreationExpressionSyntax explicitType => Name(explicitType.Type),
        _ when creation.Parent is EqualsValueClauseSyntax
            { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } } => Name(declaration.Type),
        _ when creation.Parent is EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => Name(property.Type),
        _ => string.Empty
    };

    private static string Name(SyntaxNode node) => node switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => string.Empty
    };
}
