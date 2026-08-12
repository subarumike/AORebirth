namespace AORebirth.LinuxBuild.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;

    internal static class Stage7ContractFingerprint
    {
        private const string ManifestName = "AORebirth.Stage7LoginEngineContract";
        private const string ManifestVersion = "1";

        private static readonly string[] ContainedCoreTypeNames =
        {
            "AO.Core.Encryption.BigInteger",
            "AO.Core.Encryption.LoginEncryption",
            "AORebirth.Core.Components.IBus",
            "AORebirth.Core.Components.IContainer",
            "AORebirth.Core.Components.IHandle`1",
            "AORebirth.Core.Components.IHandleMessage",
            "AORebirth.Core.Components.IHandleMessage`1",
            "AORebirth.Core.Components.IMessagePublisher",
            "AORebirth.Core.Components.IMessageSerializer",
            "AORebirth.Core.Components.MefContainer",
            "AORebirth.Core.Components.MemBusAdapter",
            "AORebirth.Core.Components.MemBusIoCAdapter",
            "AORebirth.Core.Components.MessagePublisher",
            "AORebirth.Core.Components.MessageSerializer",
            "AORebirth.Core.Encryption.PasswordHash",
            "AORebirth.Core.EventHandlers.Events.MessageReceivedEvent",
            "AORebirth.Core.EventHandlers.Handlers.MessageReceivedHandler"
        };

        private static readonly KeyValuePair<string, string>[] HandlerMappings =
        {
            Pair("LoginEngine.MessageHandlers.CreateCharacterHandler", "SmokeLounge.AOtomation.Messaging.Messages.SystemMessages.CreateCharacterMessage"),
            Pair("LoginEngine.MessageHandlers.DeleteCharacterHandler", "SmokeLounge.AOtomation.Messaging.Messages.SystemMessages.DeleteCharacterMessage"),
            Pair("LoginEngine.MessageHandlers.RandomNameRequestHandler", "SmokeLounge.AOtomation.Messaging.Messages.SystemMessages.RandomNameRequestMessage"),
            Pair("LoginEngine.MessageHandlers.SelectCharacterHandler", "SmokeLounge.AOtomation.Messaging.Messages.SystemMessages.SelectCharacterMessage"),
            Pair("LoginEngine.MessageHandlers.UserCredentialsHandler", "SmokeLounge.AOtomation.Messaging.Messages.SystemMessages.UserCredentialsMessage"),
            Pair("LoginEngine.MessageHandlers.UserLoginHandler", "SmokeLounge.AOtomation.Messaging.Messages.SystemMessages.UserLoginMessage")
        };

        internal static void WriteLegacy(string manifestPath, Assembly loginEngineAssembly, Assembly coreAssembly)
        {
            WriteManifest(manifestPath, Create(loginEngineAssembly, coreAssembly));
        }

        internal static void VerifyLegacy(string manifestPath, Assembly loginEngineAssembly, Assembly coreAssembly)
        {
            VerifyExact(ReadManifest(manifestPath), Create(loginEngineAssembly, coreAssembly), "Legacy Stage 7 LoginEngine contract");
        }

        internal static void VerifyLinux(string manifestPath, Assembly loginEngineAssembly, Assembly coreAssembly)
        {
            VerifyContainedCoreShape(coreAssembly);
            string expected = ReadManifest(manifestPath);
            string actual = Create(loginEngineAssembly, coreAssembly);
            VerifyExact(WithoutReferences(expected), WithoutReferences(actual), "Stage 7 LoginEngine semantic contract");
            VerifyMappedReferences(expected, actual);
        }

        internal static void VerifyOffline(Assembly loginEngineAssembly, Assembly coreAssembly)
        {
            VerifyContainedCoreShape(coreAssembly);
            Create(loginEngineAssembly, coreAssembly);
        }

        internal static void VerifyRepository(string repositoryRoot)
        {
            string root = RequireDirectory(repositoryRoot, "repository root");
            VerifyLoginSourceInventory(root);
            VerifyLinuxProjects(root);
            VerifyHandlerSourceMappings(root);
            VerifySecuritySourceContracts(root);
            VerifyDeploymentDatabaseIdentity(root);
        }

        internal static void VerifyPublish(
            string repositoryRoot,
            string publishDirectory,
            string expectedRuntimeIdentifier,
            string expectedPackageKind)
        {
            string root = RequireDirectory(repositoryRoot, "repository root");
            string publish = RequireDirectory(publishDirectory, "LoginEngine publish directory");
            string runtimeIdentifier = string.IsNullOrWhiteSpace(expectedRuntimeIdentifier)
                ? "linux-x64"
                : expectedRuntimeIdentifier;
            string packageKind = string.IsNullOrWhiteSpace(expectedPackageKind)
                ? "framework-dependent"
                : expectedPackageKind;

            Assert(
                string.Equals(packageKind, "framework-dependent", StringComparison.Ordinal)
                || string.Equals(packageKind, "self-contained", StringComparison.Ordinal),
                "Unknown LoginEngine package kind " + packageKind + ".");
            Assert(
                string.Equals(runtimeIdentifier, "linux-x64", StringComparison.Ordinal)
                || string.Equals(runtimeIdentifier, "linux-arm64", StringComparison.Ordinal),
                "Unsupported LoginEngine runtime identifier " + runtimeIdentifier + ".");

            string loginAssembly = RequireFile(Path.Combine(publish, "LoginEngine.dll"), "published LoginEngine assembly");
            string loginAppHost = RequireFile(Path.Combine(publish, "LoginEngine"), "published Linux LoginEngine apphost");
            RequireFile(Path.Combine(publish, "LoginEngine.deps.json"), "published LoginEngine dependency manifest");
            RequireFile(Path.Combine(publish, "LoginEngine.runtimeconfig.json"), "published LoginEngine runtime configuration");
            RequireFile(Path.Combine(publish, "Config.xml"), "published LoginEngine configuration");
            RequireFile(Path.Combine(publish, "AORebirth.Core.dll"), "published contained AORebirth.Core assembly");
            RequireFile(Path.Combine(publish, "AORebirth.Database.dll"), "published AORebirth.Database assembly");
            RequireFile(Path.Combine(publish, "Cell.Core.dll"), "published Cell.Core assembly");
            RequireFile(Path.Combine(publish, "SmokeLounge.AOtomation.Messaging.dll"), "published messaging assembly");
            RequireFile(Path.Combine(publish, "NLog.dll"), "published NLog assembly");
            string publishedMemBus = RequireFile(Path.Combine(publish, "MemBus.dll"), "published MemBus assembly");
            RequireFile(Path.Combine(publish, "System.ComponentModel.Composition.dll"), "published MEF assembly");

            AssemblyName loginName = AssemblyName.GetAssemblyName(loginAssembly);
            Assert(string.Equals(loginName.Name, "LoginEngine", StringComparison.Ordinal), "Published LoginEngine assembly identity changed.");
            Assert(loginName.Version != null && loginName.Version.Equals(new Version(1, 0, 0, 0)), "Published LoginEngine assembly version changed.");
            Assert(loginName.GetPublicKeyToken() == null || loginName.GetPublicKeyToken().Length == 0, "Published LoginEngine unexpectedly became strong named.");
            VerifyLinuxAppHost(loginAppHost, runtimeIdentifier);
            AssemblyName memBusName = AssemblyName.GetAssemblyName(publishedMemBus);
            Assert(string.Equals(memBusName.Name, "MemBus", StringComparison.Ordinal), "Published MemBus assembly identity changed.");
            Assert(memBusName.Version != null && memBusName.Version.Equals(new Version(4, 0, 1, 0)), "Published MemBus assembly version changed.");
            Assert(memBusName.GetPublicKeyToken() == null || memBusName.GetPublicKeyToken().Length == 0, "Published MemBus unexpectedly became strong named.");

            foreach (string forbidden in new[]
            {
                "AORebirth.Chat.Authentication.dll",
                "NBug.dll",
                "NBug.LoginEngine.config",
                "PlayfieldLoader.dll",
                "LoginEngine.exe"
            })
            {
                Assert(!File.Exists(Path.Combine(publish, forbidden)), "Published LoginEngine retained forbidden artifact " + forbidden + ".");
            }

            AssertFilesEqual(
                Path.Combine(root, "AORebirth", "Config", "Config.xml"),
                Path.Combine(publish, "Config.xml"),
                "published LoginEngine Config.xml");
            VerifyPublishedSqlAssets(root, publish);

            string deps = File.ReadAllText(Path.Combine(publish, "LoginEngine.deps.json"));
            Assert(deps.IndexOf(runtimeIdentifier, StringComparison.Ordinal) >= 0, "Published LoginEngine dependency manifest does not target " + runtimeIdentifier + ".");
            Assert(deps.IndexOf("MemBus/4.0.1", StringComparison.Ordinal) >= 0, "Published LoginEngine dependency manifest does not pin MemBus 4.0.1.");

            bool hasCoreClr = File.Exists(Path.Combine(publish, "libcoreclr.so"));
            bool hasHostFxr = File.Exists(Path.Combine(publish, "libhostfxr.so"));
            bool hasPrivateCoreLib = File.Exists(Path.Combine(publish, "System.Private.CoreLib.dll"));
            if (string.Equals(packageKind, "framework-dependent", StringComparison.Ordinal))
            {
                Assert(!hasCoreClr && !hasHostFxr && !hasPrivateCoreLib, "Framework-dependent LoginEngine publish contains self-contained runtime files.");
            }
            else
            {
                Assert(hasCoreClr && hasHostFxr && hasPrivateCoreLib, "Self-contained LoginEngine publish is missing runtime files.");
            }
        }

        private static string Create(Assembly loginEngineAssembly, Assembly coreAssembly)
        {
            AssertAssemblyName(loginEngineAssembly, "LoginEngine");
            AssertAssemblyName(coreAssembly, "AORebirth.Core");

            var lines = new List<string>();
            AddLine(lines, "manifest", ManifestName, ManifestVersion);
            foreach (string line in SplitLines(AORebirth.LinuxBuild.Stage2ContractFingerprint.Create(new[] { loginEngineAssembly })))
            {
                AddLine(lines, "api", line);
            }

            AddProtectedContracts(lines, loginEngineAssembly);
            AddContainedCoreContracts(lines, coreAssembly);
            AddHandlerMappings(lines, loginEngineAssembly);
            AddReferenceContracts(lines, loginEngineAssembly);
            foreach (string line in SplitLines(Stage7RuntimeFixtures.Create()))
            {
                lines.Add(line);
            }

            AddLine(
                lines,
                "safety",
                "listeners=excluded",
                "dao=guarded-offline",
                "authentication=state-gated",
                "ownership=source-gated",
                "shutdown-drain=linux-verified");
            return NormalizeManifest(string.Join("\n", lines) + "\n");
        }

        private static void AddProtectedContracts(ICollection<string> lines, Assembly assembly)
        {
            const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
            foreach (Type type in GetExportedTypes(assembly).OrderBy(AORebirth.LinuxBuild.Stage2ContractFingerprint.NormalizeType, StringComparer.Ordinal))
            {
                var contracts = new List<string>();
                contracts.AddRange(type.GetConstructors(flags).Where(IsProtected).Select(FormatConstructor));
                contracts.AddRange(type.GetMethods(flags).Where(IsProtected).Select(FormatMethod));
                contracts.AddRange(type.GetFields(flags).Where(IsProtected).Select(FormatField));
                contracts.AddRange(type.GetProperties(flags).Where(IsProtected).Select(FormatProperty));
                contracts.AddRange(type.GetEvents(flags).Where(IsProtected).Select(FormatEvent));
                foreach (string contract in contracts.OrderBy(value => value, StringComparer.Ordinal))
                {
                    AddLine(lines, "protected", AORebirth.LinuxBuild.Stage2ContractFingerprint.NormalizeType(type), contract);
                }
            }
        }

        private static void AddContainedCoreContracts(ICollection<string> lines, Assembly coreAssembly)
        {
            string full = AORebirth.LinuxBuild.Stage2ContractFingerprint.Create(new[] { coreAssembly });
            var expected = new HashSet<string>(
                ContainedCoreTypeNames
                    .Select(name => GetRequiredType(coreAssembly, name, true))
                    .Select(AORebirth.LinuxBuild.Stage2ContractFingerprint.NormalizeType),
                StringComparer.Ordinal);
            var found = new HashSet<string>(StringComparer.Ordinal);
            bool includeType = false;

            foreach (string line in SplitLines(full))
            {
                if (line.StartsWith("assembly.", StringComparison.Ordinal)
                    && !line.StartsWith("assembly.begin", StringComparison.Ordinal)
                    && !line.StartsWith("assembly.end", StringComparison.Ordinal))
                {
                    AddLine(lines, "core.contract", line);
                    continue;
                }

                if (line.StartsWith("type.begin|", StringComparison.Ordinal))
                {
                    string typeName = line.Substring("type.begin|".Length);
                    includeType = expected.Contains(typeName);
                    if (includeType)
                    {
                        found.Add(typeName);
                    }
                }

                if (includeType)
                {
                    AddLine(lines, "core.contract", line);
                }

                if (line.StartsWith("type.end|", StringComparison.Ordinal))
                {
                    includeType = false;
                }
            }

            string[] missing = expected.Except(found).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Assert(missing.Length == 0, "AORebirth.Core is missing Stage 7 types: " + string.Join(", ", missing) + ".");
        }

        private static void AddHandlerMappings(ICollection<string> lines, Assembly assembly)
        {
            Type nonGenericHandler = GetRequiredType(assembly, "AORebirth.Core.Components.IHandleMessage", false);
            foreach (KeyValuePair<string, string> mapping in HandlerMappings)
            {
                Type handler = GetRequiredType(assembly, mapping.Key, true);
                Type[] candidates = handler.GetInterfaces()
                    .Where(value => value.IsGenericType
                        && string.Equals(value.GetGenericTypeDefinition().FullName, "AORebirth.Core.Components.IHandleMessage`1", StringComparison.Ordinal))
                    .ToArray();
                Assert(candidates.Length == 1, mapping.Key + " must implement exactly one generic IHandleMessage contract.");
                string messageType = candidates[0].GetGenericArguments()[0].FullName;
                Assert(string.Equals(messageType, mapping.Value, StringComparison.Ordinal), mapping.Key + " maps to unexpected message " + messageType + ".");
                Assert(nonGenericHandler == null || nonGenericHandler.IsAssignableFrom(handler), mapping.Key + " no longer implements IHandleMessage.");

                CustomAttributeData[] exports = CustomAttributeData.GetCustomAttributes(handler)
                    .Where(value => string.Equals(value.AttributeType.FullName, "System.ComponentModel.Composition.ExportAttribute", StringComparison.Ordinal))
                    .ToArray();
                Assert(exports.Length == 1, mapping.Key + " must have exactly one MEF Export attribute.");
                Assert(exports[0].ConstructorArguments.Count == 1, mapping.Key + " MEF Export must declare its contract type.");
                Type exportedContract = exports[0].ConstructorArguments[0].Value as Type;
                Assert(exportedContract != null
                    && string.Equals(exportedContract.FullName, "AORebirth.Core.Components.IHandleMessage", StringComparison.Ordinal),
                    mapping.Key + " exports the wrong MEF contract.");
                AddLine(lines, "handler", mapping.Key, mapping.Value, "export=IHandleMessage");
            }
        }

        private static void AddReferenceContracts(ICollection<string> lines, Assembly assembly)
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies().OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                AddLine(lines, "reference", reference.Name, reference.Version == null ? string.Empty : reference.Version.ToString(), FormatPublicKeyToken(reference.GetPublicKeyToken()));
            }
        }

        private static void VerifyContainedCoreShape(Assembly coreAssembly)
        {
            AssertAssemblyName(coreAssembly, "AORebirth.Core");
            AssemblyName name = coreAssembly.GetName();
            Assert(name.Version != null && name.Version.Equals(new Version(1, 0, 0, 0)), "Contained AORebirth.Core version changed.");
            Assert(name.GetPublicKeyToken() == null || name.GetPublicKeyToken().Length == 0, "Contained AORebirth.Core unexpectedly became strong named.");
            AssemblyName[] memBusReferences = coreAssembly.GetReferencedAssemblies()
                .Where(value => string.Equals(value.Name, "MemBus", StringComparison.Ordinal))
                .ToArray();
            Assert(memBusReferences.Length == 1, "Contained AORebirth.Core must reference exactly one MemBus assembly.");
            Assert(
                memBusReferences[0].Version != null && memBusReferences[0].Version.Equals(new Version(4, 0, 1, 0)),
                "Contained AORebirth.Core must reference MemBus assembly version 4.0.1.0.");
            Assert(
                memBusReferences[0].GetPublicKeyToken() == null || memBusReferences[0].GetPublicKeyToken().Length == 0,
                "Contained AORebirth.Core MemBus reference unexpectedly became strong named.");

            string[] actual = GetExportedTypes(coreAssembly)
                .Select(value => value.FullName)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] expected = ContainedCoreTypeNames.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            VerifySequence(expected, actual, "contained AORebirth.Core exported type surface");
        }

        private static void VerifyMappedReferences(string expectedManifest, string actualManifest)
        {
            var expected = new HashSet<string>(GetReferenceNames(expectedManifest).Where(name => !IsFrameworkReference(name)), StringComparer.Ordinal);
            var actual = new HashSet<string>(GetReferenceNames(actualManifest).Where(name => !IsFrameworkReference(name)), StringComparer.Ordinal);
            expected.Remove("NBug");
            expected.Remove("locales");

            foreach (string forbidden in new[] { "AORebirth.Chat.Authentication", "NBug", "PlayfieldLoader" })
            {
                Assert(!actual.Contains(forbidden), "Linux LoginEngine retains forbidden direct reference " + forbidden + ".");
            }

            VerifySequence(
                expected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                actual.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                "Stage 7 mapped direct references");
        }

        private static IEnumerable<string> GetReferenceNames(string manifest)
        {
            foreach (string line in SplitLines(manifest))
            {
                string[] parts = SplitEscaped(line);
                if (parts.Length >= 2 && string.Equals(parts[0], "reference", StringComparison.Ordinal))
                {
                    yield return parts[1];
                }
            }
        }

        private static bool IsFrameworkReference(string name)
        {
            return string.Equals(name, "mscorlib", StringComparison.Ordinal)
                || string.Equals(name, "netstandard", StringComparison.Ordinal)
                || string.Equals(name, "Microsoft.CSharp", StringComparison.Ordinal)
                || name.StartsWith("System", StringComparison.Ordinal);
        }

        private static string WithoutReferences(string manifest)
        {
            return NormalizeManifest(string.Join("\n", SplitLines(manifest).Where(line => !line.StartsWith("reference|", StringComparison.Ordinal))) + "\n");
        }

        private static void VerifyLoginSourceInventory(string root)
        {
            string legacyProjectPath = RequireFile(Path.Combine(root, "AORebirth", "Server", "LoginEngine", "LoginEngine.csproj"), "legacy LoginEngine project");
            string inventoryPath = RequireFile(Path.Combine(root, "LinuxBuild", "source-inventory", "LoginEngine.CompileItems.props"), "Linux LoginEngine source inventory");
            XDocument legacy = LoadXml(legacyProjectPath);
            XDocument inventory = LoadXml(inventoryPath);

            string[] legacySources = legacy.Descendants()
                .Where(element => element.Name.LocalName == "Compile")
                .Select(element => NormalizeLegacyCompile(root, legacyProjectPath, RequireAttribute(element, "Include")))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] linuxSources = inventory.Descendants()
                .Where(element => element.Name.LocalName == "Compile")
                .Select(element => NormalizeInventoryInclude(RequireAttribute(element, "Include")))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert(legacySources.Length == 35, "Legacy LoginEngine compile inventory must contain exactly 35 sources.");
            Assert(linuxSources.Length == 35, "Linux LoginEngine compile inventory must contain exactly 35 sources.");
            Assert(legacySources.Distinct(StringComparer.Ordinal).Count() == legacySources.Length, "Legacy LoginEngine compile inventory contains duplicates.");
            Assert(linuxSources.Distinct(StringComparer.Ordinal).Count() == linuxSources.Length, "Linux LoginEngine compile inventory contains duplicates.");
            VerifySequence(legacySources, linuxSources, "LoginEngine linked source inventory");
        }

        private static void VerifyLinuxProjects(string root)
        {
            string loginProjectPath = RequireFile(Path.Combine(root, "LinuxBuild", "Projects", "LoginEngine.Linux.csproj"), "Linux LoginEngine project");
            string coreProjectPath = RequireFile(Path.Combine(root, "LinuxBuild", "Projects", "AORebirth.Core.Login.Linux.csproj"), "contained AORebirth.Core project");
            XDocument login = LoadXml(loginProjectPath);
            XDocument core = LoadXml(coreProjectPath);

            Assert(GetProperty(login, "TargetFramework") == "net10.0", "Linux LoginEngine must target net10.0.");
            Assert(GetProperty(login, "OutputType") == "Exe", "Linux LoginEngine output type changed.");
            Assert(GetProperty(login, "AssemblyName") == "LoginEngine", "Linux LoginEngine assembly name changed.");
            Assert(GetProperty(login, "EnableDefaultCompileItems") == "false", "Linux LoginEngine must disable default compile items.");
            Assert(GetProperty(login, "GenerateAssemblyInfo") == "false", "Linux LoginEngine must use the legacy assembly metadata sources.");
            Assert(GetProperty(login, "PublishTrimmed") == "false", "Linux LoginEngine publish must remain untrimmed.");
            Assert(GetProperty(login, "PublishAot") == "false", "Linux LoginEngine publish must not use AOT.");
            Assert(GetProperty(login, "PublishSingleFile") == "false", "Linux LoginEngine publish must not be single-file.");
            Assert(GetProperty(login, "UseAppHost") == "true", "Linux LoginEngine publish must explicitly enable its native apphost.");
            Assert(GetProperty(login, "DefineConstants").IndexOf("AOREBIRTH_LINUX", StringComparison.Ordinal) >= 0, "Linux LoginEngine must define AOREBIRTH_LINUX.");

            XElement[] imports = login.Descendants().Where(element => element.Name.LocalName == "Import").ToArray();
            Assert(imports.Length == 1, "Linux LoginEngine must import exactly one guarded source inventory.");
            Assert(
                string.Equals(RequireAttribute(imports[0], "Project").Replace('\\', '/'), "../source-inventory/LoginEngine.CompileItems.props", StringComparison.Ordinal),
                "Linux LoginEngine imports the wrong source inventory.");
            XElement[] directCompile = login.Descendants().Where(element => element.Name.LocalName == "Compile").ToArray();
            Assert(directCompile.Length == 1, "Linux LoginEngine must have exactly one Linux-only compile source outside its guarded legacy inventory.");
            Assert(
                string.Equals(
                    NormalizeInventoryInclude(RequireAttribute(directCompile[0], "Include")),
                    "LinuxBuild/Compatibility/LoginEngine/LinuxProgram.cs",
                    StringComparison.Ordinal),
                "Linux LoginEngine has an unexpected compile source outside its guarded legacy inventory.");

            string[] expectedReferences =
            {
                "AORebirth.Database.Linux.csproj",
                "AORebirth.Core.Login.Linux.csproj",
                "Cell.Core.Linux.csproj",
                "SmokeLounge.AOtomation.Messaging.Linux.csproj",
                "Translations.Linux.csproj",
                "Utility.Linux.csproj"
            };
            VerifySequence(
                expectedReferences.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                GetProjectReferenceNames(login),
                "Linux LoginEngine direct project references");
            VerifyPackage(login, "NLog", "6.1.4", "Linux LoginEngine");
            VerifyPackage(login, "System.ComponentModel.Composition", "10.0.10", "Linux LoginEngine");

            Assert(GetProperty(core, "TargetFramework") == "net10.0", "Contained AORebirth.Core must target net10.0.");
            Assert(GetProperty(core, "AssemblyName") == "AORebirth.Core", "Contained AORebirth.Core assembly name changed.");
            Assert(GetProperty(core, "EnableDefaultCompileItems") == "false", "Contained AORebirth.Core must disable default compile items.");
            Assert(GetProperty(core, "GenerateAssemblyInfo") == "false", "Contained AORebirth.Core must use legacy assembly metadata.");
            VerifyPackage(core, "System.ComponentModel.Composition", "10.0.10", "contained AORebirth.Core");
            VerifyPackage(core, "MemBus", "4.0.1", "contained AORebirth.Core");
            VerifyMemBusRestoreSelection(root);
        }

        private static void VerifyMemBusRestoreSelection(string root)
        {
            string assetsPath = RequireFile(
                Path.Combine(
                    root,
                    "LinuxBuild",
                    "Projects",
                    "obj",
                    "AORebirth.Core.Login.Linux",
                    "project.assets.json"),
                "contained AORebirth.Core restore assets");
            string assets = File.ReadAllText(assetsPath);
            Assert(
                Regex.IsMatch(
                    assets,
                    @"""MemBus/4\.0\.1""\s*:\s*\{.*?""compile""\s*:\s*\{\s*""lib/netstandard2\.0/MemBus\.dll""",
                    RegexOptions.Singleline),
                "Contained AORebirth.Core restore did not select MemBus 4.0.1 lib/netstandard2.0 for compile.");
            Assert(
                Regex.IsMatch(
                    assets,
                    @"""MemBus/4\.0\.1""\s*:\s*\{.*?""runtime""\s*:\s*\{\s*""lib/netstandard2\.0/MemBus\.dll""",
                    RegexOptions.Singleline),
                "Contained AORebirth.Core restore did not select MemBus 4.0.1 lib/netstandard2.0 for runtime.");
            Assert(
                !Regex.IsMatch(assets, @"""code""\s*:\s*""NU1701""", RegexOptions.IgnoreCase),
                "Contained AORebirth.Core restore reported NU1701 framework fallback.");

            string buildPropsPath = RequireFile(
                Path.Combine(root, "LinuxBuild", "Projects", "Directory.Build.props"),
                "Linux project build policy");
            XDocument buildProps = LoadXml(buildPropsPath);
            Assert(
                GetProperty(buildProps, "WarningsAsErrors")
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(value => string.Equals(value.Trim(), "NU1701", StringComparison.Ordinal)),
                "Linux project build policy must fail restore on NU1701.");
        }

        private static void VerifyHandlerSourceMappings(string root)
        {
            VerifyHandlerSource(root, "UserLoginHandler.cs", new[] { "LoginErrorMessage", "ServerSaltMessage" }, new[] { "0x00001F83", "0x00002B3F" });
            VerifyHandlerSource(root, "UserCredentialsHandler.cs", new[] { "CharacterListMessage", "LoginErrorMessage" }, new[] { "0x00001F83", "0x0000615B" });
            VerifyHandlerSource(root, "SelectCharacterHandler.cs", new[] { "LoginErrorMessage", "ZoneInfoMessage" }, new[] { "0x00001F83", "0x0000615B" });
            VerifyHandlerSource(root, "RandomNameRequestHandler.cs", new[] { "SuggestNameMessage" }, new[] { "0x0000FFFF" });
            VerifyHandlerSource(root, "DeleteCharacterHandler.cs", new[] { "CharacterDeletedMessage", "LoginErrorMessage" }, new[] { "0x00001F83", "0x0000FFFF" });
            VerifyHandlerSource(root, "CreateCharacterHandler.cs", new[] { "CharacterCreatedMessage", "LoginErrorMessage", "NameInUseMessage" }, new[] { "0x00001F83", "0x0000FFFF" });
        }

        private static void VerifySecuritySourceContracts(string root)
        {
            string coreSourceRoot = Path.Combine(
                root,
                "AORebirth",
                "Libraries",
                "Source",
                "AORebirth.Core");
            string loginSourceRoot = Path.Combine(root, "AORebirth", "Server", "LoginEngine");

            string loginEncryption = ReadSourceWithoutComments(
                Path.Combine(coreSourceRoot, "Encryption", "LoginEncryption.cs"),
                "LoginEncryption security source");
            Assert(
                Regex.Matches(loginEncryption, @"\bpublic\s+bool\s+i_Enable\s*=\s*true\s*;").Count == 1,
                "LoginEncryption must enable authentication in every build configuration.");
            Assert(
                !Regex.IsMatch(loginEncryption, @"\bi_Enable\s*=\s*false\s*;"),
                "LoginEncryption retained a disabled authentication path.");
            Assert(
                loginEncryption.IndexOf("#if DEBUG", StringComparison.Ordinal) < 0,
                "LoginEncryption retained a DEBUG authentication bypass.");
            const string disabledGuard =
                @"if\s*\(\s*this\s*\.\s*i_Enable\s*==\s*false\s*\)\s*\{\s*return\s+false\s*;\s*\}";
            Assert(
                Regex.Matches(loginEncryption, disabledGuard, RegexOptions.Singleline).Count == 2,
                "Both LoginEncryption disabled branches must fail closed.");
            Assert(
                !Regex.IsMatch(
                    loginEncryption,
                    @"if\s*\(\s*this\s*\.\s*i_Enable\s*==\s*false\s*\)\s*\{\s*return\s+true\s*;",
                    RegexOptions.Singleline),
                "LoginEncryption retained a disabled-state return-true bypass.");

            string userLogin = ReadSourceWithoutComments(
                Path.Combine(loginSourceRoot, "MessageHandlers", "UserLoginHandler.cs"),
                "UserLoginHandler security source");
            RequireSourceToken(
                userLogin,
                "RandomNumberGenerator.Create()",
                "UserLoginHandler cryptographic salt generator");
            Assert(
                !Regex.IsMatch(userLogin, @"\bnew\s+(?:System\s*\.\s*)?Random\s*\("),
                "UserLoginHandler retained System.Random salt generation.");
            Assert(
                userLogin.IndexOf("System.Random", StringComparison.Ordinal) < 0,
                "UserLoginHandler retained an explicit System.Random reference.");
            RequireSourceToken(userLogin, "BeginAuthentication", "UserLoginHandler challenge state transition");

            string client = ReadSourceWithoutComments(
                Path.Combine(loginSourceRoot, "CoreClient", "Client.cs"),
                "LoginEngine client authentication state source");
            foreach (string token in new[]
            {
                "authenticationSync",
                "authenticationGeneration",
                "AwaitingLogin",
                "ChallengeIssued",
                "Authenticating",
                "Authenticated",
                "Closed",
                "BeginAuthentication",
                "TryBeginAuthenticationAttempt",
                "CompleteAuthentication",
                "TryGetAuthenticatedAccountName",
                "RejectAuthentication"
            })
            {
                RequireSourceToken(client, token, "LoginEngine client authentication state");
            }

            string create = ReadSourceWithoutComments(
                Path.Combine(loginSourceRoot, "MessageHandlers", "CreateCharacterHandler.cs"),
                "CreateCharacterHandler security source");
            VerifyAnonymousGuardBeforeDataAccess(
                create,
                "CreateCharacterHandler",
                new[] { "new CharacterName" });
            RequireSourceToken(create, "AccountName = authenticatedAccount", "CreateCharacterHandler authenticated identity");

            string select = ReadSourceWithoutComments(
                Path.Combine(loginSourceRoot, "MessageHandlers", "SelectCharacterHandler.cs"),
                "SelectCharacterHandler security source");
            VerifyAnonymousGuardBeforeDataAccess(
                select,
                "SelectCharacterHandler",
                new[] { "new CheckLogin", "CharacterDao.Instance" });
            RequireSourceToken(
                select,
                "IsCharacterOnAccount(authenticatedAccount",
                "SelectCharacterHandler ownership check");

            string delete = ReadSourceWithoutComments(
                Path.Combine(loginSourceRoot, "MessageHandlers", "DeleteCharacterHandler.cs"),
                "DeleteCharacterHandler security source");
            VerifyAnonymousGuardBeforeDataAccess(
                delete,
                "DeleteCharacterHandler",
                new[] { "new CheckLogin", "new CharacterName" });
            RequireSourceToken(
                delete,
                "TryDeleteChar(authenticatedAccount",
                "DeleteCharacterHandler authenticated ownership delete");

            string characterName = ReadSourceWithoutComments(
                Path.Combine(loginSourceRoot, "Packets", "CharacterName.cs"),
                "CharacterName ownership delete source");
            RequireSourceToken(
                characterName,
                "DeleteForUser(accountName, charid)",
                "CharacterName account-scoped delete");

            string characterDao = ReadSourceWithoutComments(
                Path.Combine(
                    root,
                    "AORebirth",
                    "Libraries",
                    "Source",
                    "AORebirth.Database",
                    "Dao",
                    "CharacterDao.cs"),
                "CharacterDao ownership delete source");
            RequireSourceToken(characterDao, "DeleteForUser", "CharacterDao account-scoped delete operation");
            RequireSourceToken(characterDao, "Username = accountName", "CharacterDao delete ownership predicate");
            RequireSourceToken(characterDao, "BeginTransaction()", "CharacterDao transactional ownership delete");
            RequireSourceToken(
                characterDao,
                "DELETE FROM characters WHERE Id=@Id AND Username=@Username",
                "CharacterDao destructive ownership predicate");
            RequireSourceToken(characterDao, "deleted != 1", "CharacterDao ownership race rejection");
            foreach (string table in new[]
            {
                "missionflags",
                "missionstates",
                "missionobjectiveprogress",
                "missionobjectiveobservations",
                "missionrewardledger",
                "characterstimers",
                "charactersactivenanos",
                "charactersmeshs",
                "charactersuploadednanos",
                "charactersperks"
            })
            {
                RequireSourceToken(
                    characterDao,
                    "DELETE FROM " + table + " WHERE CharacterId=@CharacterId",
                    "CharacterDao " + table + " cleanup");
            }

            RequireSourceToken(
                characterDao,
                "ReceivedMessagesDao.Instance.Delete(new { PlayerId = id }, connection, transaction)",
                "CharacterDao receivedmessages PlayerId cleanup");

            string messagePublisher = ReadSourceWithoutComments(
                Path.Combine(coreSourceRoot, "Components", "MessagePublisher.cs"),
                "MessagePublisher ordering source");
            RequireSourceToken(
                messagePublisher,
                "ConditionalWeakTable<object, object>",
                "MessagePublisher per-sender lock table");
            RequireSourceToken(messagePublisher, "GetValue(sender", "MessagePublisher sender lock lookup");
            RequireSourceToken(messagePublisher, "lock (senderSync)", "MessagePublisher per-sender critical section");

            string memBusAdapter = ReadSourceWithoutComments(
                Path.Combine(coreSourceRoot, "Components", "MemBusAdapter.cs"),
                "MemBusAdapter shutdown source");
            foreach (string token in new[]
            {
                "ConditionalWeakTable<object, SenderDispatchQueue>",
                "Queue<MessageReceivedEvent>",
                "CompleteOrderedDispatch",
                "pendingMessages",
                "StopAcceptingMessages",
                "WaitForIdle",
                "TrySetDispatchCompletion",
                "CompleteDispatch"
            })
            {
                RequireSourceToken(memBusAdapter, token, "MemBusAdapter bounded drain");
            }

            string receivedHandler = ReadSourceWithoutComments(
                Path.Combine(coreSourceRoot, "EventHandlers", "Handlers", "MessageReceivedHandler.cs"),
                "MessageReceivedHandler completion source");
            RequireSourceToken(receivedHandler, "finally", "MessageReceivedHandler completion guarantee");
            RequireSourceToken(receivedHandler, "obj.CompleteDispatch()", "MessageReceivedHandler dispatch completion");

            string linuxProgram = ReadSourceWithoutComments(
                Path.Combine(root, "LinuxBuild", "Compatibility", "LoginEngine", "LinuxProgram.cs"),
                "Linux LoginEngine shutdown source");
            int stopIndex = linuxProgram.IndexOf("StopAcceptingMessages", StringComparison.Ordinal);
            int waitIndex = linuxProgram.IndexOf("WaitForIdle", StringComparison.Ordinal);
            Assert(
                stopIndex >= 0 && waitIndex > stopIndex,
                "Linux LoginEngine must stop accepting dispatches before waiting for the bounded drain.");
            RequireSourceToken(
                linuxProgram,
                "TimeSpan.FromSeconds(30)",
                "Linux LoginEngine bounded drain timeout");
        }

        private static void VerifyAnonymousGuardBeforeDataAccess(
            string source,
            string description,
            IEnumerable<string> dataAccessTokens)
        {
            int guard = source.IndexOf("TryGetAuthenticatedAccountName", StringComparison.Ordinal);
            int rejection = source.IndexOf("RejectAuthentication", guard < 0 ? 0 : guard, StringComparison.Ordinal);
            int earlyReturn = source.IndexOf("return;", rejection < 0 ? 0 : rejection, StringComparison.Ordinal);
            int firstDataAccess = dataAccessTokens
                .Select(token => source.IndexOf(token, StringComparison.Ordinal))
                .Where(index => index >= 0)
                .DefaultIfEmpty(-1)
                .Min();
            Assert(guard >= 0, description + " is missing its authenticated-session guard.");
            Assert(rejection > guard, description + " does not reject a failed authenticated-session guard.");
            Assert(firstDataAccess > rejection, description + " authenticates after data access begins.");
            Assert(earlyReturn > rejection && earlyReturn < firstDataAccess, description + " does not return before data access after rejection.");
        }

        private static string ReadSourceWithoutComments(string path, string description)
        {
            string source = File.ReadAllText(RequireFile(path, description));
            source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(source, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }

        private static void RequireSourceToken(string source, string token, string description)
        {
            Assert(
                source.IndexOf(token, StringComparison.Ordinal) >= 0,
                description + " is missing required source token " + token + ".");
        }

        private static void VerifyDeploymentDatabaseIdentity(string root)
        {
            string unitPath = RequireFile(
                Path.Combine(root, "LinuxBuild", "deployment", "systemd", "ao-rebirth-loginengine.service"),
                "LoginEngine systemd unit");
            string environmentPath = RequireFile(
                Path.Combine(root, "LinuxBuild", "deployment", "systemd", "loginengine.env.example"),
                "LoginEngine environment example");
            string unit = File.ReadAllText(unitPath);
            string environment = File.ReadAllText(environmentPath);
            VerifyExactActiveLine(
                unit,
                "Environment=AO_REBIRTH_EXPECTED_DATABASE=aorebirth_chatengine_stage6",
                "LoginEngine systemd database identity");
            VerifyExactActiveLine(
                unit,
                "Environment=AO_REBIRTH_BIND_MODE=Loopback",
                "LoginEngine systemd default bind mode");
            VerifyExactActiveLine(
                unit,
                "ExecStartPre=/usr/bin/test ${AO_REBIRTH_EXPECTED_DATABASE} = aorebirth_chatengine_stage6",
                "LoginEngine systemd effective database guard");
            VerifyExactActiveLine(
                unit,
                "ExecStartPre=/opt/ao-rebirth/loginengine/current/LoginEngine --validate-startup",
                "LoginEngine systemd startup preflight");
            VerifyExactActiveLine(
                unit,
                "ExecStartPre=/opt/ao-rebirth/loginengine/current/LoginEngine --validate-database",
                "LoginEngine systemd database preflight");
            VerifyExactActiveLine(
                environment,
                "AO_REBIRTH_EXPECTED_DATABASE=aorebirth_chatengine_stage6",
                "LoginEngine environment database identity");
            VerifyExactActiveLine(
                environment,
                "AO_REBIRTH_BIND_MODE=Loopback",
                "LoginEngine environment default bind mode");
        }

        private static void VerifyExactActiveLine(string content, string expected, string description)
        {
            string[] matches = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !line.StartsWith("#", StringComparison.Ordinal)
                    && !line.StartsWith(";", StringComparison.Ordinal)
                    && string.Equals(line, expected, StringComparison.Ordinal))
                .ToArray();
            Assert(matches.Length == 1, description + " must contain exactly one active line: " + expected + ".");
        }

        private static void VerifyHandlerSource(string root, string fileName, string[] expectedResponses, string[] expectedReceivers)
        {
            string path = RequireFile(Path.Combine(root, "AORebirth", "Server", "LoginEngine", "MessageHandlers", fileName), "LoginEngine handler source " + fileName);
            string source = File.ReadAllText(path);
            source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            source = Regex.Replace(source, @"//.*?$", string.Empty, RegexOptions.Multiline);
            IEnumerable<string> responseNames = Regex.Matches(source, @"\bnew\s+([A-Za-z][A-Za-z0-9_]*Message)\b")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value);
            if (source.IndexOf("RejectAuthentication", StringComparison.Ordinal) >= 0)
            {
                responseNames = responseNames.Concat(new[] { "LoginErrorMessage" });
            }

            string[] responses = responseNames
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            IEnumerable<string> receiverNames = Regex.Matches(source, @"\bclient\.Send\s*\(\s*(0x[0-9A-Fa-f]+)")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value.ToUpperInvariant().Replace("X", "x"));
            if (source.IndexOf("RejectAuthentication", StringComparison.Ordinal) >= 0)
            {
                receiverNames = receiverNames.Concat(new[] { "0x00001F83" });
            }

            string[] receivers = receiverNames
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            VerifySequence(expectedResponses.OrderBy(value => value, StringComparer.Ordinal).ToArray(), responses, fileName + " response mapping");
            VerifySequence(expectedReceivers.OrderBy(value => value, StringComparer.Ordinal).ToArray(), receivers, fileName + " receiver mapping");
        }

        private static void VerifyPublishedSqlAssets(string root, string publish)
        {
            string inventoryPath = RequireFile(Path.Combine(root, "LinuxBuild", "source-inventory", "AORebirth.Database.ContentItems.props"), "Database SQL content inventory");
            XDocument inventory = LoadXml(inventoryPath);
            XElement[] content = inventory.Descendants().Where(element => element.Name.LocalName == "Content").ToArray();
            Assert(content.Length == 34, "Database SQL content inventory must contain exactly 34 assets.");
            foreach (XElement item in content)
            {
                string source = NormalizeInventoryInclude(RequireAttribute(item, "Include"));
                string link = RequireAttribute(item, "Link");
                Assert(!string.IsNullOrWhiteSpace(link), "Database SQL content item is missing Link metadata.");
                AssertFilesEqual(Path.Combine(root, ToNativePath(source)), Path.Combine(publish, ToNativePath(link.Replace('\\', '/'))), "published SQL asset " + link);
            }
        }

        private static void VerifyLinuxAppHost(string appHostPath, string runtimeIdentifier)
        {
            byte[] image = File.ReadAllBytes(appHostPath);
            Assert(image.Length >= 20, "Published LoginEngine apphost is too small to contain an ELF header.");
            Assert(
                image[0] == 0x7f && image[1] == (byte)'E' && image[2] == (byte)'L' && image[3] == (byte)'F',
                "Published LoginEngine apphost is not a Linux ELF executable.");
            Assert(image[4] == 2, "Published LoginEngine apphost must be ELF64.");
            Assert(image[5] == 1, "Published LoginEngine apphost must be little-endian.");

            ushort machine = (ushort)(image[18] | (image[19] << 8));
            ushort expectedMachine = string.Equals(runtimeIdentifier, "linux-x64", StringComparison.Ordinal)
                ? (ushort)0x003e
                : (ushort)0x00b7;
            Assert(
                machine == expectedMachine,
                "Published LoginEngine apphost architecture does not match " + runtimeIdentifier + ".");
            Assert(
                ContainsBytes(image, Encoding.ASCII.GetBytes("LoginEngine.dll")),
                "Published LoginEngine apphost is not pinned to LoginEngine.dll.");
        }

        private static bool ContainsBytes(byte[] source, byte[] value)
        {
            for (int offset = 0; offset <= source.Length - value.Length; offset++)
            {
                int index = 0;
                while (index < value.Length && source[offset + index] == value[index]) index++;
                if (index == value.Length) return true;
            }

            return false;
        }

        private static string[] GetProjectReferenceNames(XDocument project)
        {
            return project.Descendants()
                .Where(element => element.Name.LocalName == "ProjectReference")
                .Select(element => PortableFileName(RequireAttribute(element, "Include")))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static void VerifyPackage(XDocument project, string packageName, string packageVersion, string description)
        {
            XElement[] matches = project.Descendants()
                .Where(element => element.Name.LocalName == "PackageReference"
                    && string.Equals(RequireAttribute(element, "Include"), packageName, StringComparison.Ordinal))
                .ToArray();
            Assert(matches.Length == 1, description + " must reference exactly one " + packageName + " package.");
            Assert(string.Equals(RequireAttribute(matches[0], "Version"), packageVersion, StringComparison.Ordinal), description + " must pin " + packageName + " " + packageVersion + ".");
        }

        private static string NormalizeLegacyCompile(string root, string projectPath, string include)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath), ToNativePath(include.Replace('\\', '/'))));
            string rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert(fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase), "Legacy LoginEngine compile source escapes the repository: " + include + ".");
            return fullPath.Substring(rootPrefix.Length).Replace('\\', '/');
        }

        private static string NormalizeInventoryInclude(string include)
        {
            string value = include.Replace('\\', '/');
            const string prefix = "$(AORebirthRepositoryRoot)/";
            Assert(value.StartsWith(prefix, StringComparison.Ordinal), "Inventory include does not use AORebirthRepositoryRoot: " + include + ".");
            return value.Substring(prefix.Length);
        }

        private static bool IsProtected(MethodBase method)
        {
            return method.IsFamily || method.IsFamilyOrAssembly || method.IsFamilyAndAssembly;
        }

        private static bool IsProtected(FieldInfo field)
        {
            return field.IsFamily || field.IsFamilyOrAssembly || field.IsFamilyAndAssembly;
        }

        private static bool IsProtected(PropertyInfo property)
        {
            MethodInfo getter = property.GetGetMethod(true);
            MethodInfo setter = property.GetSetMethod(true);
            return (getter != null && IsProtected(getter)) || (setter != null && IsProtected(setter));
        }

        private static bool IsProtected(EventInfo eventInfo)
        {
            MethodInfo add = eventInfo.GetAddMethod(true);
            MethodInfo remove = eventInfo.GetRemoveMethod(true);
            return (add != null && IsProtected(add)) || (remove != null && IsProtected(remove));
        }

        private static string FormatConstructor(ConstructorInfo constructor)
        {
            return "ctor " + FormatAccess(constructor) + " " + (constructor.IsStatic ? "static" : "instance") + "(" + FormatParameters(constructor.GetParameters()) + ")";
        }

        private static string FormatMethod(MethodInfo method)
        {
            string generic = method.IsGenericMethodDefinition ? "<" + string.Join(",", method.GetGenericArguments().Select(value => value.Name)) + ">" : string.Empty;
            return "method " + FormatAccess(method) + " " + (method.IsStatic ? "static" : "instance") + " "
                + AORebirth.LinuxBuild.Stage2ContractFingerprint.NormalizeType(method.ReturnType) + " " + method.Name + generic + "(" + FormatParameters(method.GetParameters()) + ")"
                + " virtual=" + method.IsVirtual.ToString().ToLowerInvariant()
                + " abstract=" + method.IsAbstract.ToString().ToLowerInvariant()
                + " final=" + method.IsFinal.ToString().ToLowerInvariant();
        }

        private static string FormatField(FieldInfo field)
        {
            return "field " + FormatAccess(field) + " " + (field.IsStatic ? "static" : "instance") + " "
                + AORebirth.LinuxBuild.Stage2ContractFingerprint.NormalizeType(field.FieldType) + " " + field.Name
                + " initonly=" + field.IsInitOnly.ToString().ToLowerInvariant()
                + " literal=" + field.IsLiteral.ToString().ToLowerInvariant();
        }

        private static string FormatProperty(PropertyInfo property)
        {
            MethodInfo getter = property.GetGetMethod(true);
            MethodInfo setter = property.GetSetMethod(true);
            string access = getter != null ? FormatAccess(getter) : setter != null ? FormatAccess(setter) : "none";
            return "property " + access + " " + AORebirth.LinuxBuild.Stage2ContractFingerprint.NormalizeType(property.PropertyType) + " " + property.Name
                + "(" + FormatParameters(property.GetIndexParameters()) + ")"
                + " get=" + (getter == null ? "none" : FormatAccess(getter))
                + " set=" + (setter == null ? "none" : FormatAccess(setter));
        }

        private static string FormatEvent(EventInfo eventInfo)
        {
            MethodInfo add = eventInfo.GetAddMethod(true);
            MethodInfo remove = eventInfo.GetRemoveMethod(true);
            return "event " + AORebirth.LinuxBuild.Stage2ContractFingerprint.NormalizeType(eventInfo.EventHandlerType) + " " + eventInfo.Name
                + " add=" + (add == null ? "none" : FormatAccess(add))
                + " remove=" + (remove == null ? "none" : FormatAccess(remove));
        }

        private static string FormatParameters(IEnumerable<ParameterInfo> parameters)
        {
            return string.Join(",", parameters.Select(parameter =>
            {
                string direction = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : string.Empty;
                return direction + AORebirth.LinuxBuild.Stage2ContractFingerprint.NormalizeType(parameter.ParameterType) + " " + parameter.Name;
            }));
        }

        private static string FormatAccess(MethodBase method)
        {
            if (method.IsPublic) return "public";
            if (method.IsFamilyOrAssembly) return "protected-internal";
            if (method.IsFamilyAndAssembly) return "private-protected";
            if (method.IsFamily) return "protected";
            if (method.IsAssembly) return "internal";
            return "private";
        }

        private static string FormatAccess(FieldInfo field)
        {
            if (field.IsPublic) return "public";
            if (field.IsFamilyOrAssembly) return "protected-internal";
            if (field.IsFamilyAndAssembly) return "private-protected";
            if (field.IsFamily) return "protected";
            if (field.IsAssembly) return "internal";
            return "private";
        }

        private static void AssertAssemblyName(Assembly assembly, string expected)
        {
            if (assembly == null) throw new ArgumentNullException("assembly");
            Assert(string.Equals(assembly.GetName().Name, expected, StringComparison.Ordinal), "Expected " + expected + ", found " + assembly.FullName + ".");
        }

        private static Type GetRequiredType(Assembly assembly, string name, bool require)
        {
            Type type = assembly.GetType(name, false, false);
            if (type == null && require)
            {
                throw new InvalidOperationException("Missing required type " + name + " from " + assembly.GetName().Name + ".");
            }

            return type;
        }

        private static Type[] GetExportedTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                string details = string.Join("; ", exception.LoaderExceptions.Where(value => value != null).Select(value => value.Message));
                throw new InvalidOperationException("Could not load exported types from " + assembly.GetName().Name + ": " + details, exception);
            }
        }

        private static XDocument LoadXml(string path)
        {
            string xml = File.ReadAllText(path);
            xml = Regex.Replace(xml, "encoding=\"utf-16\"", "encoding=\"utf-8\"", RegexOptions.IgnoreCase);
            return XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }

        private static string GetProperty(XDocument document, string name)
        {
            XElement element = document.Descendants().FirstOrDefault(value => value.Name.LocalName == name);
            return element == null ? string.Empty : element.Value.Trim();
        }

        private static string GetChildValue(XElement element, string name)
        {
            XElement child = element.Elements().FirstOrDefault(value => value.Name.LocalName == name);
            return child == null ? string.Empty : child.Value.Trim();
        }

        private static string RequireAttribute(XElement element, string name)
        {
            XAttribute attribute = element.Attributes().FirstOrDefault(value => value.Name.LocalName == name);
            if (attribute == null || string.IsNullOrWhiteSpace(attribute.Value))
            {
                throw new InvalidOperationException(element.Name.LocalName + " is missing " + name + " metadata.");
            }

            return attribute.Value.Trim();
        }

        private static string PortableFileName(string path)
        {
            string normalized = path.Replace('\\', '/');
            int separator = normalized.LastIndexOf('/');
            return separator < 0 ? normalized : normalized.Substring(separator + 1);
        }

        private static string ToNativePath(string portablePath)
        {
            return portablePath.Replace('/', Path.DirectorySeparatorChar);
        }

        private static string RequireDirectory(string path, string description)
        {
            string fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException("Missing " + description + ": " + fullPath + ".");
            return fullPath;
        }

        private static string RequireFile(string path, string description)
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("Missing " + description + ".", fullPath);
            return fullPath;
        }

        private static void AssertFilesEqual(string expectedPath, string actualPath, string description)
        {
            expectedPath = RequireFile(expectedPath, "source for " + description);
            actualPath = RequireFile(actualPath, description);
            Assert(File.ReadAllBytes(expectedPath).SequenceEqual(File.ReadAllBytes(actualPath)), description + " differs from its source asset.");
        }

        private static void WriteManifest(string path, string manifest)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, NormalizeManifest(manifest), new UTF8Encoding(false));
        }

        private static string ReadManifest(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("Stage 7 legacy contract manifest was not found.", fullPath);
            return NormalizeManifest(File.ReadAllText(fullPath));
        }

        private static void AddLine(ICollection<string> lines, params object[] values)
        {
            lines.Add(string.Join("|", values.Select(value => Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty))));
        }

        private static string Escape(string value)
        {
            return value.Replace("%", "%25").Replace("|", "%7C").Replace("\r", "%0D").Replace("\n", "%0A");
        }

        private static string[] SplitLines(string value)
        {
            return NormalizeManifest(value).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string[] SplitEscaped(string line)
        {
            return line.Split('|').Select(value => value.Replace("%0A", "\n").Replace("%0D", "\r").Replace("%7C", "|").Replace("%25", "%")).ToArray();
        }

        private static string NormalizeManifest(string value)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n') + "\n";
        }

        private static string FormatPublicKeyToken(byte[] token)
        {
            return token == null || token.Length == 0 ? string.Empty : string.Concat(token.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void VerifyExact(string expected, string actual, string description)
        {
            expected = NormalizeManifest(expected);
            actual = NormalizeManifest(actual);
            if (string.Equals(expected, actual, StringComparison.Ordinal)) return;
            string[] expectedLines = SplitLines(expected);
            string[] actualLines = SplitLines(actual);
            int common = Math.Min(expectedLines.Length, actualLines.Length);
            for (int index = 0; index < common; index++)
            {
                if (!string.Equals(expectedLines[index], actualLines[index], StringComparison.Ordinal))
                {
                    throw new InvalidDataException(description + " differs at line " + (index + 1).ToString(CultureInfo.InvariantCulture) + ". Expected " + expectedLines[index] + " but found " + actualLines[index] + ".");
                }
            }

            throw new InvalidDataException(description + " line count differs. Expected " + expectedLines.Length + " but found " + actualLines.Length + ".");
        }

        private static void VerifySequence(string[] expected, string[] actual, string description)
        {
            if (expected.SequenceEqual(actual, StringComparer.Ordinal)) return;
            throw new InvalidOperationException(description + " differs. Expected [" + string.Join(", ", expected) + "] but found [" + string.Join(", ", actual) + "].");
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

internal static class Stage7ContractFingerprint
{
    internal static void WriteLegacy(string manifestPath, System.Reflection.Assembly loginEngineAssembly, System.Reflection.Assembly coreAssembly)
    {
        AORebirth.LinuxBuild.Contracts.Stage7ContractFingerprint.WriteLegacy(manifestPath, loginEngineAssembly, coreAssembly);
    }

    internal static void VerifyLegacy(string manifestPath, System.Reflection.Assembly loginEngineAssembly, System.Reflection.Assembly coreAssembly)
    {
        AORebirth.LinuxBuild.Contracts.Stage7ContractFingerprint.VerifyLegacy(manifestPath, loginEngineAssembly, coreAssembly);
    }

    internal static void VerifyLinux(string manifestPath, System.Reflection.Assembly loginEngineAssembly, System.Reflection.Assembly coreAssembly)
    {
        AORebirth.LinuxBuild.Contracts.Stage7ContractFingerprint.VerifyLinux(manifestPath, loginEngineAssembly, coreAssembly);
    }

    internal static void VerifyOffline(System.Reflection.Assembly loginEngineAssembly, System.Reflection.Assembly coreAssembly)
    {
        AORebirth.LinuxBuild.Contracts.Stage7ContractFingerprint.VerifyOffline(loginEngineAssembly, coreAssembly);
    }

    internal static void VerifyRepository(string repositoryRoot)
    {
        AORebirth.LinuxBuild.Contracts.Stage7ContractFingerprint.VerifyRepository(repositoryRoot);
    }

    internal static void VerifyPublish(string repositoryRoot, string publishDirectory, string runtimeIdentifier, string packageKind)
    {
        AORebirth.LinuxBuild.Contracts.Stage7ContractFingerprint.VerifyPublish(repositoryRoot, publishDirectory, runtimeIdentifier, packageKind);
    }
}
