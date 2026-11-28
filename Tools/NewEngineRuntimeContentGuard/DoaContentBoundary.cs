using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class DoaContentBoundary
{
    internal const string DirectSourceRule = "DOA_DIRECT_CONTENT_SOURCE";
    private const string ZoneRuntime = "AORebirth/Server/ZoneEngine_New/";
    private const string ProviderPath = ZoneRuntime + "Core/GameData/GameDataContentProvider.cs";
    private const string CoreGameData = "AORebirth/Libraries/Source/AORebirth.Core/GameData/";
    private const string UtilityGameData = "AORebirth/Libraries/Source/Utility/GameData/";

    private static readonly string[] PathResolvers = ["ResolveRuntimeRoot", "ResolveOverrideRoot", "ResolveFile", "Resolve"];
    private static readonly string[] ReadMethods = ["ReadAllText", "ReadAllBytes", "ReadAllLines", "ReadLines",
        "ReadAllTextAsync", "ReadAllBytesAsync", "ReadAllLinesAsync", "OpenRead", "OpenText", "Open"];
    private static readonly string[] DirectorySearchMethods = ["GetFiles", "GetDirectories", "EnumerateFiles", "EnumerateDirectories"];
    private static readonly string[] ProviderTypes = ["GameDataContentProvider", "IGameDataContentProvider"];
    private static readonly string[] ProviderProducts = ["ContentInputs", "ContentSlot", "ContentSnapshot"];

    internal static IEnumerable<(SyntaxNode Node, string Rule, string Basis)> SourceIssues(string path, SyntaxNode syntax)
    {
        if (!path.StartsWith(ZoneRuntime, StringComparison.Ordinal)
            && !path.StartsWith(CoreGameData, StringComparison.Ordinal) && !path.StartsWith(UtilityGameData, StringComparison.Ordinal))
            yield break;

        bool mission = path.Contains("/Missions/", StringComparison.Ordinal) || path.Contains("/Quests/", StringComparison.Ordinal);
        bool catalogFile = mission || syntax.DescendantNodes().Any(node => node switch
        {
            LiteralExpressionSyntax literal => IsCatalogPath(literal),
            SimpleNameSyntax name => name.Identifier.ValueText is "ContentInputs" or "ContentModule",
            _ => false
        });
        var imports = syntax.DescendantNodes().OfType<UsingDirectiveSyntax>().ToArray();
        var paths = TypeAliases(imports, "GameDataPaths");
        var files = TypeAliases(imports, "File");
        var directories = TypeAliases(imports, "Directory");

        foreach (var call in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (IsSourceAuthority(path, call)) continue;
            string method = Name(call.Expression);
            if ((PathResolvers.Contains(method, StringComparer.Ordinal) && CallsType(call, paths, imports))
                || (method == "GetEnvironmentVariable" && call.ArgumentList.Arguments.Any(arg => IsGameDataEnvironmentSetting(arg.Expression)))
                || (catalogFile || call.ArgumentList.DescendantNodes().Any(IsCatalogSource))
                    && (ReadMethods.Contains(method, StringComparer.Ordinal) && CallsType(call, files, imports)
                        || DirectorySearchMethods.Contains(method, StringComparer.Ordinal) && CallsType(call, directories, imports)))
            {
                yield return (call, DirectSourceRule,
                    "Use the authoritative GameData provider and declare the module's content inputs.");
            }
        }

        foreach (var access in syntax.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (IsSourceAuthority(path, access)) continue;
            if (catalogFile && Name(access) == "RootPath" && access.Parent is ArgumentSyntax or EqualsValueClauseSyntax
                && (mission || Name(access.Expression).Contains("gamedata", StringComparison.OrdinalIgnoreCase)))
                yield return (access, DirectSourceRule,
                    "A catalog consumer must receive a provider snapshot instead of the physical GameData root.");
        }

        foreach (var declaration in syntax.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if ((ProviderTypes.Contains(declaration.Identifier.ValueText, StringComparer.Ordinal)
                    || declaration.BaseList?.Types.Any(item => ProviderTypes.Contains(Name(item.Type), StringComparer.Ordinal)) == true)
                && !IsProviderCode(path, declaration))
                yield return (declaration, "DOA_DUPLICATE_CONTENT_AUTHORITY",
                    "The GameData provider is the single runtime content authority.");
        }

        foreach (var creation in syntax.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            string type = CreatedType(creation);
            if (!IsProviderCode(path, creation) && ProviderTypes.Contains(type, StringComparer.Ordinal))
                yield return (creation, "DOA_DUPLICATE_CONTENT_AUTHORITY",
                    "Inject the shared runtime GameData provider instead of constructing another authority.");
            if (!IsProviderCode(path, creation) && ProviderProducts.Contains(type, StringComparer.Ordinal))
                yield return (creation, "DOA_CONTENT_VALIDATION_BYPASS",
                    "Content inputs, publication slots and snapshots must be created by the validating provider.");
            if (!IsSourceAuthority(path, creation) && type is "StreamReader" or "FileStream"
                && creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression is { } source
                && (source.DescendantNodesAndSelf().Any(IsCatalogSource)
                    || catalogFile && (type == "FileStream" || LooksLikePath(source))))
                yield return (creation, DirectSourceRule,
                    "Catalog parsers receive the bytes selected by the authoritative GameData provider.");
        }

        if (!catalogFile) yield break;

        foreach (var binary in syntax.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (binary.IsKind(SyntaxKind.CoalesceExpression) && ContainsRequiredRead(binary.Left)
                && binary.Right is not ThrowExpressionSyntax)
                yield return (binary, "DOA_CONTENT_VALIDATION_BYPASS",
                    "A required provider input or validation failure must not substitute a replacement value.");
        }

        foreach (var clause in syntax.DescendantNodes().OfType<CatchClauseSyntax>())
        {
            if (clause.Parent is TryStatementSyntax attempt && ContainsRequiredRead(attempt.Block)
                && clause.Block.DescendantNodes().OfType<ReturnStatementSyntax>().Any(statement => statement.Expression != null))
                yield return (clause, "DOA_CONTENT_VALIDATION_BYPASS",
                    "A mandatory provider input or validation failure must propagate to the publication boundary.");
        }

        foreach (var expression in syntax.DescendantNodes().OfType<ConditionalExpressionSyntax>())
        {
            if (IsSourceAuthority(path, expression)) continue;
            if (ChecksPathExistence(expression.Condition)
                && (expression.DescendantNodes().Any(IsCatalogSource) || mission
                    && expression.WhenFalse.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(call => Name(call.Expression) == "Combine")))
                yield return (expression, "DOA_UNAUTHORIZED_CATALOG_FALLBACK",
                    "Catalog source fallback and override precedence belong to the authoritative provider.");
        }

        foreach (var condition in syntax.DescendantNodes().OfType<IfStatementSyntax>())
        {
            if (IsSourceAuthority(path, condition) || condition.Else == null) continue;
            if (ChecksPathExistence(condition.Condition) && condition.DescendantNodes().Any(IsCatalogSource)
                && condition.Else.Statement.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>().Any(statement => statement.Expression != null))
                yield return (condition, "DOA_UNAUTHORIZED_CATALOG_FALLBACK",
                    "Select alternate sources only through the module's declared provider override policy.");
        }

        foreach (var call in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (IsSourceAuthority(path, call)) continue;
            if (call.Expression is MemberAccessExpressionSyntax member && Name(member) is "Combine" or "Join"
                && Name(member.Expression) == "Path"
                && call.ArgumentList.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                    .Any(access => Name(access) == "CurrentDirectory" && Name(access.Expression) == "Environment"))
                yield return (call, "DOA_UNAUTHORIZED_CATALOG_FALLBACK",
                    "A gameplay catalog cannot search the process working directory for fallback content.");
        }
    }

    private static bool IsProviderCode(string path, SyntaxNode node)
        => path == ProviderPath && node.AncestorsAndSelf().OfType<TypeDeclarationSyntax>()
            .Any(type => type.Identifier.ValueText is "GameDataContentProvider" or "ContentInputs"
                or "ContentSlot" or "ContentSnapshot" or "IGameDataContentProvider");

    private static bool IsSourceAuthority(string path, SyntaxNode node)
        => (path == ProviderPath && IsInside(node, "GameDataContentProvider"))
            || (path == CoreGameData + "GameDataPaths.cs" && IsInside(node, "GameDataPaths"));

    private static bool IsInside(SyntaxNode node, string typeName)
        => node.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Any(type => type.Identifier.ValueText == typeName);

    private static bool ContainsRequiredRead(SyntaxNode node)
        => node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
            .Any(call => call.Expression is MemberAccessExpressionSyntax access && Name(access) is "Required" or "RequireDependency");

    private static bool ChecksPathExistence(ExpressionSyntax condition)
        => condition.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(call =>
            call.Expression is MemberAccessExpressionSyntax exists
            && Name(exists) == "Exists" && Name(exists.Expression) is "File" or "Directory");

    private static bool IsCatalogSource(SyntaxNode node)
        => node is LiteralExpressionSyntax literal && IsCatalogPath(literal)
            || node is MemberAccessExpressionSyntax member && Name(member) == "RootPath"
            || node is SimpleNameSyntax name && name.Identifier.ValueText is "GameDataPaths" or "GameDataRoot";

    private static bool IsCatalogPath(LiteralExpressionSyntax literal)
    {
        if (!literal.IsKind(SyntaxKind.StringLiteralExpression)) return false;
        string value = literal.Token.ValueText;
        return value.Contains("GameData", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".dat", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGameDataEnvironmentSetting(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal
            && literal.Token.ValueText is "AO_REBIRTH_GAMEDATA_PATH" or "AO_REBIRTH_GAMEDATA_OVERRIDE_PATH"
            || expression is MemberAccessExpressionSyntax member && Name(member.Expression) == "GameDataPaths"
            && Name(member) is "EnvironmentVariableName" or "OverrideEnvironmentVariableName";

    private static bool LooksLikePath(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax literal)
            return literal.IsKind(SyntaxKind.StringLiteralExpression);
        string name = Name(expression);
        return name.Contains("path", StringComparison.OrdinalIgnoreCase)
            || name.Contains("file", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> TypeAliases(IEnumerable<UsingDirectiveSyntax> imports, string type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal) { type };
        foreach (UsingDirectiveSyntax import in imports)
            if (import.Alias is { } alias && import.Name is { } name && Name(name) == type)
                names.Add(alias.Name.Identifier.ValueText);
        return names;
    }

    private static bool CallsType(InvocationExpressionSyntax call, HashSet<string> types, IEnumerable<UsingDirectiveSyntax> imports)
        => call.Expression is MemberAccessExpressionSyntax member ? types.Contains(Name(member.Expression))
            : call.Expression is SimpleNameSyntax && imports.Any(import => import.StaticKeyword.IsKind(SyntaxKind.StaticKeyword)
                && import.Name is { } name && types.Contains(Name(name)));

    private static string CreatedType(BaseObjectCreationExpressionSyntax creation)
        => creation switch
        {
            ObjectCreationExpressionSyntax explicitType => Name(explicitType.Type),
            _ when creation.Parent is EqualsValueClauseSyntax
                { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } } => Name(declaration.Type),
            _ when creation.Parent is EqualsValueClauseSyntax
                { Parent: PropertyDeclarationSyntax property } => Name(property.Type),
            _ when creation.Ancestors().FirstOrDefault(node => node is MethodDeclarationSyntax
                or LocalFunctionStatementSyntax or LambdaExpressionSyntax) is MethodDeclarationSyntax method
                && creation.Parent is ArrowExpressionClauseSyntax or ReturnStatementSyntax => Name(method.ReturnType),
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
