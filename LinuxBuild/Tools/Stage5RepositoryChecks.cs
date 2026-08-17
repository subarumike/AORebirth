namespace AORebirth.LinuxBuild.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;

    internal static class Stage5RepositoryChecks
    {
        private static readonly string[] ExpectedChatProjectReferences =
        {
            "AORebirth.Chat.Authentication.Linux.csproj",
            "AORebirth.Communication.Linux.csproj",
            "AORebirth.Database.Linux.csproj",
            "AORebirth.Enums.Linux.csproj",
            "AORebirth.Stats.Linux.csproj",
            "Cell.Core.Linux.csproj",
            "SmokeLounge.AOtomation.Messaging.Linux.csproj",
            "Translations.Linux.csproj",
            "Utility.Linux.csproj"
        };

        private static readonly string[] ExpectedAuthenticationSources =
        {
            "AORebirth/Libraries/Source/AORebirth.Core/Encryption/BigInteger.cs",
            "AORebirth/Libraries/Source/AORebirth.Core/Encryption/LoginEncryption.cs",
            "AORebirth/Libraries/Source/AORebirth.Core/Encryption/PasswordHash.cs"
        };

        private static readonly string[] RequiredPublishAssemblies =
        {
            "AORebirth.Chat.Authentication.dll",
            "AORebirth.Communication.dll",
            "AORebirth.Database.dll",
            "AORebirth.Enums.dll",
            "AORebirth.Core.Exceptions.dll",
            "AORebirth.Interfaces.dll",
            "AORebirth.Stats.dll",
            "Cell.Core.dll",
            "Cell.Util.dll",
            "Dapper.dll",
            "Ionic.Zlib.dll",
            "MemBus.dll",
            "Microsoft.Data.SqlClient.dll",
            "MySqlConnector.dll",
            "MsgPack.dll",
            "NLog.dll",
            "Npgsql.dll",
            "SmokeLounge.AOtomation.Messaging.dll",
            "Utility.dll",
            "locales.dll"
        };

        private static readonly string[] ForbiddenPublishFiles =
        {
            "AORebirth.Core.dll",
            "App.config",
            "ChatEngine.exe.config",
            "NBug.ChatEngine.config",
            "NBug.dll",
            "PresentationCore.dll",
            "PresentationFramework.dll",
            "PlayfieldLoader.dll",
            "System.Windows.Forms.dll",
            "playfields.dat"
        };

        internal static void VerifyRepository(string repositoryRoot)
        {
            string root = RequireDirectory(repositoryRoot, "repository root");
            VerifyChatSourceInventory(root);
            VerifyChatProjectDependencies(root);
            VerifyAuthenticationExtraction(root);
            VerifyParserMap(root);
            VerifyDeploymentFiles(root);
        }

        internal static void VerifyPublish(string repositoryRoot, string publishDirectory)
        {
            VerifyPublish(repositoryRoot, publishDirectory, "linux-x64", "framework-dependent");
        }

        internal static void VerifyPublish(
            string repositoryRoot,
            string publishDirectory,
            string expectedRuntimeIdentifier,
            string expectedPackageKind)
        {
            string root = RequireDirectory(repositoryRoot, "repository root");
            string publish = RequireDirectory(publishDirectory, "ChatEngine publish directory");

            foreach (string required in new[] { "ChatEngine", "ChatEngine.dll", "ChatEngine.deps.json", "ChatEngine.runtimeconfig.json", "Config.xml" })
            {
                RequireFile(Path.Combine(publish, required), "required Stage 5 publish file");
            }

            foreach (string required in RequiredPublishAssemblies)
            {
                RequireFile(Path.Combine(publish, required), "required Stage 5 dependency");
            }

            var topLevelNames = new HashSet<string>(
                Directory.EnumerateFiles(publish, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName),
                StringComparer.Ordinal);
            Assert(topLevelNames.Contains("Config.xml"), "Publish output does not preserve exact Config.xml casing.");
            foreach (string forbidden in ForbiddenPublishFiles)
            {
                Assert(!topLevelNames.Contains(forbidden), "Forbidden Stage 5 publish file is present: " + forbidden + ".");
            }

            string sourceConfig = Path.Combine(root, "AORebirth", "Config", "Config.xml");
            AssertFilesEqual(sourceConfig, Path.Combine(publish, "Config.xml"), "published Config.xml");
            VerifyPublishedAssembly(
                root,
                publish,
                "ChatEngine.Linux",
                "ChatEngine.dll",
                "ChatEngine",
                expectedRuntimeIdentifier);
            VerifyPublishedAssembly(
                root,
                publish,
                "AORebirth.Chat.Authentication.Linux",
                "AORebirth.Chat.Authentication.dll",
                "AORebirth.Chat.Authentication",
                null);
            VerifyPublishedSqlAssets(root, publish);
            if (!string.IsNullOrWhiteSpace(expectedRuntimeIdentifier)
                || !string.IsNullOrWhiteSpace(expectedPackageKind))
            {
                VerifyPublishRuntimeShape(publish, expectedRuntimeIdentifier, expectedPackageKind);
            }
        }

        private static void VerifyPublishRuntimeShape(
            string publish,
            string expectedRuntimeIdentifier,
            string expectedPackageKind)
        {
            Assert(
                expectedRuntimeIdentifier == "linux-x64" || expectedRuntimeIdentifier == "linux-arm64",
                "Unsupported expected Stage 5 runtime identifier.");
            Assert(
                expectedPackageKind == "framework-dependent" || expectedPackageKind == "self-contained",
                "Unsupported expected Stage 5 package kind.");

            string runtimeConfig = File.ReadAllText(
                RequireFile(Path.Combine(publish, "ChatEngine.runtimeconfig.json"), "ChatEngine runtime config"));
            string dependencies = File.ReadAllText(
                RequireFile(Path.Combine(publish, "ChatEngine.deps.json"), "ChatEngine dependency manifest"));
            Assert(
                dependencies.Contains("/" + expectedRuntimeIdentifier + "\"", StringComparison.Ordinal),
                "Published dependency manifest targets the wrong runtime identifier.");

            bool selfContained = expectedPackageKind == "self-contained";
            Assert(
                runtimeConfig.Contains(selfContained ? "\"includedFrameworks\"" : "\"framework\"", StringComparison.Ordinal),
                "Published runtime config does not match the requested package kind.");
            Assert(
                !selfContained || !runtimeConfig.Contains("\"framework\":", StringComparison.Ordinal),
                "Self-contained runtime config unexpectedly requires a shared framework.");

            foreach (string nativeRuntimeFile in new[] { "libcoreclr.so", "libhostfxr.so", "libhostpolicy.so" })
            {
                bool exists = File.Exists(Path.Combine(publish, nativeRuntimeFile));
                Assert(
                    selfContained ? exists : !exists,
                    "Published native runtime asset does not match " + expectedPackageKind + ": " + nativeRuntimeFile + ".");
            }

            if (selfContained)
            {
                RequireFile(Path.Combine(publish, "System.Private.CoreLib.dll"), "self-contained core library");
            }

            byte[] appHost = File.ReadAllBytes(RequireFile(Path.Combine(publish, "ChatEngine"), "Linux ChatEngine apphost"));
            Assert(
                appHost.Length >= 20
                && appHost[0] == 0x7f
                && appHost[1] == (byte)'E'
                && appHost[2] == (byte)'L'
                && appHost[3] == (byte)'F'
                && appHost[5] == 1,
                "Published ChatEngine apphost is not a little-endian ELF binary.");
            int machine = appHost[18] | (appHost[19] << 8);
            int expectedMachine = expectedRuntimeIdentifier == "linux-x64" ? 62 : 183;
            Assert(machine == expectedMachine, "Published ChatEngine apphost architecture is incorrect.");
        }

        private static void VerifyPublishedAssembly(
            string root,
            string publish,
            string projectOutputName,
            string fileName,
            string expectedAssemblyName,
            string runtimeIdentifier)
        {
            string publishedPath = RequireFile(
                Path.Combine(publish, fileName),
                "published Stage 5 assembly");
            AssemblyName identity = AssemblyName.GetAssemblyName(publishedPath);
            Assert(
                string.Equals(identity.Name, expectedAssemblyName, StringComparison.Ordinal),
                "Published assembly identity differs for " + fileName + ".");
            Assert(
                identity.Version != null && identity.Version.Equals(new Version(1, 0, 0, 0)),
                "Published assembly version differs for " + fileName + ".");
            byte[] publicKeyToken = identity.GetPublicKeyToken();
            Assert(
                publicKeyToken == null || publicKeyToken.Length == 0,
                "Published assembly unexpectedly became strong-name signed: " + fileName + ".");

            string buildPath = Path.Combine(
                root,
                "LinuxBuild",
                "Projects",
                "bin",
                projectOutputName,
                "Release",
                "net10.0");
            if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
            {
                buildPath = Path.Combine(buildPath, runtimeIdentifier);
            }

            buildPath = RequireFile(Path.Combine(buildPath, fileName), "exact Stage 5 build output");
            Assert(
                string.Equals(ComputeSha256(publishedPath), ComputeSha256(buildPath), StringComparison.Ordinal),
                "Published assembly does not match its exact fresh Stage 5 build output: " + fileName + ".");
        }

        private static void VerifyChatSourceInventory(string root)
        {
            string legacyProjectPath = Path.Combine(root, "AORebirth", "Server", "ChatEngine", "ChatEngine.csproj");
            string linuxItemsPath = Path.Combine(root, "LinuxBuild", "source-inventory", "ChatEngine.CompileItems.props");
            XDocument legacyProject = LoadXml(RequireFile(legacyProjectPath, "legacy ChatEngine project"));
            XDocument linuxItems = LoadXml(RequireFile(linuxItemsPath, "Linux ChatEngine compile inventory"));

            string legacyDirectory = Path.GetDirectoryName(legacyProjectPath);
            string[] legacySources = legacyProject.Descendants()
                .Where(element => element.Name.LocalName == "Compile")
                .Select(element => NormalizeRepositoryPath(root, ResolvePath(legacyDirectory, RequireAttribute(element, "Include"))))
                .ToArray();
            string[] linuxSources = linuxItems.Descendants()
                .Where(element => element.Name.LocalName == "Compile")
                .Select(element => NormalizeInventoryInclude(RequireAttribute(element, "Include")))
                .ToArray();

            Assert(legacySources.Length == 75, "Legacy ChatEngine compile inventory must contain exactly 75 items.");
            Assert(linuxSources.Length == 75, "Linux ChatEngine compile inventory must contain exactly 75 items.");
            VerifySequence(legacySources, linuxSources, "ChatEngine compile inventory");
            foreach (string source in linuxSources)
            {
                RequireFile(Path.Combine(root, ToNativePath(source)), "ChatEngine compile source");
            }
        }

        private static void VerifyDeploymentFiles(string root)
        {
            string servicePath = Path.Combine(
                root,
                "LinuxBuild",
                "deployment",
                "systemd",
                "ao-rebirth-chatengine.service");
            string environmentPath = Path.Combine(
                root,
                "LinuxBuild",
                "deployment",
                "systemd",
                "chatengine.env.example");
            string service = File.ReadAllText(RequireFile(servicePath, "ChatEngine systemd unit"));
            string environment = File.ReadAllText(
                RequireFile(environmentPath, "ChatEngine systemd environment example"));
            string program = File.ReadAllText(
                RequireFile(
                    Path.Combine(root, "AORebirth", "Server", "ChatEngine", "Program.cs"),
                    "ChatEngine service entry point"));
            string configuration = File.ReadAllText(
                RequireFile(
                    Path.Combine(root, "AORebirth", "Config", "Config.xml"),
                    "ChatEngine deployment configuration"));

            VerifyActiveLines(
                service,
                new[]
            {
                "Type=notify",
                "NotifyAccess=main",
                "User=aorebirth",
                "Group=aorebirth",
                "WorkingDirectory=/opt/ao-rebirth/chatengine/current",
                "Environment=AO_REBIRTH_REQUIRED_SQL_TYPE=MySql",
                "EnvironmentFile=/etc/ao-rebirth/chatengine/chatengine.env",
                "ExecStartPre=/opt/ao-rebirth/chatengine/current/ChatEngine --validate-startup",
                "ExecStartPre=/opt/ao-rebirth/chatengine/current/ChatEngine --validate-database",
                "ExecStart=/opt/ao-rebirth/chatengine/current/ChatEngine --headless",
                "KillSignal=SIGTERM",
                "ProtectSystem=strict",
                "Restart=on-failure"
            },
                "Systemd unit");
            Assert(!ContainsActiveLine(service, "Type=simple"), "Systemd unit must not use process-only readiness.");
            Assert(
                program.Contains(
                    "NotifySystemd(\"READY=1\\nSTATUS=ChatEngine listeners are ready\")",
                    StringComparison.Ordinal),
                "ChatEngine must notify systemd only after its listener checks pass.");
            Assert(
                program.Contains(
                    "NotifySystemd(\"STOPPING=1\\nSTATUS=ChatEngine is stopping\")",
                    StringComparison.Ordinal),
                "ChatEngine must notify systemd before listener teardown.");
            Assert(
                program.Contains("Environment.GetEnvironmentVariable(\"NOTIFY_SOCKET\")", StringComparison.Ordinal)
                && program.Contains("new UnixDomainSocketEndPoint(notifySocket)", StringComparison.Ordinal),
                "ChatEngine systemd notification transport is missing.");
            Assert(
                configuration.Contains("REPLACE_WITH_", StringComparison.Ordinal),
                "Checked-in Config.xml must keep a placeholder database credential.");

            VerifyActiveLines(
                environment,
                new[]
            {
                "AO_REBIRTH_CONFIG_PATH=/etc/ao-rebirth/chatengine/Config.xml",
                "AO_REBIRTH_CHAT_LISTEN_IP=127.0.0.1",
                "AO_REBIRTH_ISCOM_LISTEN_IP=127.0.0.1",
                "AO_REBIRTH_REQUIRED_SQL_TYPE=MySql"
            },
                "Environment example");

            foreach (string line in environment.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.TrimStart();
                Assert(
                    trimmed.StartsWith("#", StringComparison.Ordinal)
                    || !trimmed.StartsWith("AO_REBIRTH_MYSQL_CONNECTION=", StringComparison.Ordinal),
                    "Environment example must not contain a database credential value.");
            }
        }

        private static void VerifyChatProjectDependencies(string root)
        {
            string projectPath = Path.Combine(root, "LinuxBuild", "Projects", "ChatEngine.Linux.csproj");
            XDocument project = LoadXml(RequireFile(projectPath, "Linux ChatEngine project"));
            XElement[] imports = project.Descendants().Where(element => element.Name.LocalName == "Import").ToArray();
            Assert(imports.Length == 1, "Linux ChatEngine must import exactly one guarded source inventory.");
            Assert(
                string.Equals(
                    RequireAttribute(imports[0], "Project").Replace('\\', '/'),
                    "../source-inventory/ChatEngine.CompileItems.props",
                    StringComparison.Ordinal),
                "Linux ChatEngine imports the wrong source inventory.");
            Assert(
                !project.Descendants().Any(element => element.Name.LocalName == "Compile"),
                "Linux ChatEngine project must not bypass its guarded inventory with direct Compile items.");
            string[] projectReferences = project.Descendants()
                .Where(element => element.Name.LocalName == "ProjectReference")
                .Select(element => PortableFileName(RequireAttribute(element, "Include")))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            VerifySequence(
                ExpectedChatProjectReferences.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                projectReferences,
                "Linux ChatEngine direct project references");

            XElement[] packages = project.Descendants().Where(element => element.Name.LocalName == "PackageReference").ToArray();
            Assert(packages.Length == 1, "Linux ChatEngine must have exactly one direct package reference.");
            Assert(string.Equals(RequireAttribute(packages[0], "Include"), "NLog", StringComparison.Ordinal), "Linux ChatEngine direct package must be NLog.");
            Assert(string.Equals(RequireAttribute(packages[0], "Version"), "6.1.4", StringComparison.Ordinal), "Linux ChatEngine must pin NLog 6.1.4.");

            Assert(GetProperty(project, "EnableDefaultCompileItems") == "false", "Linux ChatEngine must disable default compile items.");
            Assert(GetProperty(project, "AssemblyName") == "ChatEngine", "Linux ChatEngine assembly name changed.");
            Assert(GetProperty(project, "TargetFramework") == "net10.0", "Linux ChatEngine must target net10.0.");
            Assert(GetProperty(project, "PublishTrimmed") == "false", "Linux ChatEngine publish must remain untrimmed.");
            Assert(GetProperty(project, "PublishAot") == "false", "Linux ChatEngine publish must not use AOT.");
            Assert(GetProperty(project, "PublishSingleFile") == "false", "Linux ChatEngine publish must not be single-file.");

            XElement[] content = project.Descendants().Where(element => element.Name.LocalName == "Content").ToArray();
            Assert(content.Length == 1, "Linux ChatEngine must publish only its explicit Config.xml content item.");
            Assert(string.Equals(GetChildValue(content[0], "Link"), "Config.xml", StringComparison.Ordinal), "Linux ChatEngine Config.xml link casing changed.");
            Assert(string.Equals(GetChildValue(content[0], "CopyToOutputDirectory"), "PreserveNewest", StringComparison.Ordinal), "Config.xml must copy to build output.");
            Assert(string.Equals(GetChildValue(content[0], "CopyToPublishDirectory"), "PreserveNewest", StringComparison.Ordinal), "Config.xml must copy to publish output.");
        }

        private static void VerifyAuthenticationExtraction(string root)
        {
            string projectPath = Path.Combine(root, "LinuxBuild", "Projects", "AORebirth.Chat.Authentication.Linux.csproj");
            XDocument project = LoadXml(RequireFile(projectPath, "Linux chat authentication project"));
            Assert(GetProperty(project, "EnableDefaultCompileItems") == "false", "Authentication extraction must disable default compile items.");
            Assert(GetProperty(project, "AssemblyName") == "AORebirth.Chat.Authentication", "Authentication extraction assembly name changed.");

            string[] actualSources = project.Descendants()
                .Where(element => element.Name.LocalName == "Compile")
                .Select(element => NormalizeInventoryInclude(RequireAttribute(element, "Include")))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            VerifySequence(
                ExpectedAuthenticationSources.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                actualSources,
                "authentication extraction source set");
            foreach (string source in actualSources)
            {
                RequireFile(Path.Combine(root, ToNativePath(source)), "authentication extraction source");
            }

            string[] projectReferences = project.Descendants()
                .Where(element => element.Name.LocalName == "ProjectReference")
                .Select(element => PortableFileName(RequireAttribute(element, "Include")))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            VerifySequence(
                new[] { "AORebirth.Database.Linux.csproj", "Utility.Linux.csproj" },
                projectReferences,
                "authentication extraction direct references");

            string authenticate = File.ReadAllText(Path.Combine(root, "AORebirth", "Server", "ChatEngine", "PacketHandlers", "Authenticate.cs"));
            string authenticateBot = File.ReadAllText(Path.Combine(root, "AORebirth", "Server", "ChatEngine", "PacketHandlers", "AuthenticateBot.cs"));
            Assert(authenticate.Contains("LoginDataDao.Instance.GetByUsername")
                && authenticate.Contains("CharacterDao.Instance.IsCharacterOnAccount")
                && authenticate.Contains("LoginOk.Create()")
                && authenticate.Contains("m_reader.ReadBytes(loginKeyLength)")
                && !authenticate.Contains("new LoginEncryption()")
                && !authenticate.Contains("IsValidLogin"),
                "Authenticate.cs is no longer bound to the username/account/character ownership login implementation.");
            Assert(authenticateBot.Contains("new LoginEncryption()") && authenticateBot.Contains("IsValidLogin"), "AuthenticateBot.cs is no longer bound to the legacy login implementation.");
        }

        private static void VerifyParserMap(string root)
        {
            string parserPath = Path.Combine(root, "AORebirth", "Server", "ChatEngine", "CoreClient", "Parser.cs");
            string source = File.ReadAllText(RequireFile(parserPath, "ChatEngine parser source"));
            source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            source = Regex.Replace(source, @"//.*?$", string.Empty, RegexOptions.Multiline);

            MatchCollection labels = Regex.Matches(source, @"\b(case\s+(\d+)\s*:|default\s*:)");
            int[] actualIds = labels.Cast<Match>()
                .Where(match => match.Groups[2].Success)
                .Select(match => int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
                .ToArray();
            int[] expectedIds = { 0, 2, 3, 21, 30, 40, 41, 42, 50, 51, 52, 53, 54, 57, 64, 65, 66, 70, 71, 100, 110, 120, 1500, 1501, 1502 };
            Assert(actualIds.SequenceEqual(expectedIds), "Parser message-number cases changed.");

            var expectedHandlers = new Dictionary<int, string>
            {
                { 0, "Authenticate" }, { 2, "AuthenticateBot" }, { 3, "LoginCharacter" },
                { 21, "PlayerNameLookup" }, { 30, "Tell" }, { 40, "BuddyAdd" },
                { 41, "BuddyRemove" }, { 42, "OnlineStatus" }, { 50, "PrivateGroupInvitePlayer" },
                { 51, "PrivateGroupKickPlayer" }, { 52, "PrivateGroupJoin" }, { 53, "PrivateGroupLeave" },
                { 54, "PrivateGroupKickEveryone" }, { 57, "PrivateGroupMessage" }, { 64, "ChannelDataSet" },
                { 65, "ChannelMessage" }, { 66, "ChannelMode" }, { 120, "ChatCommand" },
                { 1500, "LftRegister" }, { 1501, "LftUnregister" }, { 1502, "LftSearch" }
            };

            for (int index = 0; index < labels.Count; index++)
            {
                Match label = labels[index];
                if (!label.Groups[2].Success)
                {
                    string defaultBlock = source.Substring(label.Index + label.Length);
                    Assert(defaultBlock.Contains("Warning") && defaultBlock.Contains("return false"), "Parser default case must warn and reject.");
                    continue;
                }

                int id = int.Parse(label.Groups[2].Value, CultureInfo.InvariantCulture);
                int blockStart = label.Index + label.Length;
                int blockEnd = index + 1 < labels.Count ? labels[index + 1].Index : source.Length;
                string block = source.Substring(blockStart, blockEnd - blockStart);
                string handler;
                if (expectedHandlers.TryGetValue(id, out handler))
                {
                    Assert(block.Contains(handler) && block.Contains("Read"), "Parser case " + id + " no longer maps to " + handler + ".");
                }
                else if (id == 100)
                {
                    Assert(block.Contains("client.Send(packet)"), "Parser case 100 no longer echoes the packet.");
                }
                else
                {
                    Assert(!block.Contains(".Read") && !block.Contains(".Send"), "Parser no-op case " + id + " gained executable dispatch.");
                }
            }
        }

        private static void VerifyPublishedSqlAssets(string root, string publish)
        {
            string propsPath = Path.Combine(root, "LinuxBuild", "source-inventory", "AORebirth.Database.ContentItems.props");
            XDocument props = LoadXml(RequireFile(propsPath, "Database SQL content inventory"));
            XElement[] contentItems = props.Descendants().Where(element => element.Name.LocalName == "Content").ToArray();
            Assert(contentItems.Length == 34, "Database SQL content inventory must contain exactly 34 assets.");
            foreach (XElement content in contentItems)
            {
                string sourceRelative = NormalizeInventoryInclude(RequireAttribute(content, "Include"));
                string link = GetChildValue(content, "Link");
                Assert(!string.IsNullOrEmpty(link), "Database SQL content item is missing Link metadata.");
                string sourcePath = Path.Combine(root, ToNativePath(sourceRelative));
                string publishPath = Path.Combine(publish, ToNativePath(link.Replace('\\', '/')));
                AssertFilesEqual(sourcePath, publishPath, "published SQL asset " + link);
            }
        }

        private static string NormalizeInventoryInclude(string include)
        {
            string value = include.Replace('\\', '/');
            const string prefix = "$(AORebirthRepositoryRoot)/";
            Assert(value.StartsWith(prefix, StringComparison.Ordinal), "Inventory include does not use AORebirthRepositoryRoot: " + include + ".");
            return value.Substring(prefix.Length);
        }

        private static XDocument LoadXml(string path)
        {
            string xml = File.ReadAllText(path);
            xml = Regex.Replace(xml, "encoding=\"utf-16\"", "encoding=\"utf-8\"", RegexOptions.IgnoreCase);
            return XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }

        private static string NormalizeRepositoryPath(string root, string fullPath)
        {
            string relative = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
            Assert(!relative.StartsWith("../", StringComparison.Ordinal), "Compile source escapes the repository: " + fullPath + ".");
            return relative;
        }

        private static string ResolvePath(string baseDirectory, string path)
        {
            return Path.GetFullPath(Path.Combine(baseDirectory, ToNativePath(path.Replace('\\', '/'))));
        }

        private static string ToNativePath(string portablePath)
        {
            return portablePath.Replace('/', Path.DirectorySeparatorChar);
        }

        private static string PortableFileName(string path)
        {
            string normalized = path.Replace('\\', '/');
            int separator = normalized.LastIndexOf('/');
            return separator < 0 ? normalized : normalized.Substring(separator + 1);
        }

        private static string GetProperty(XDocument document, string name)
        {
            XElement element = document.Descendants().FirstOrDefault(value => value.Name.LocalName == name);
            return element == null ? string.Empty : element.Value.Trim();
        }

        private static string GetChildValue(XElement element, string name)
        {
            XAttribute attribute = element.Attributes().FirstOrDefault(value => value.Name.LocalName == name);
            if (attribute != null)
            {
                return attribute.Value.Trim();
            }

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

        private static void VerifyActiveLines(
            string content,
            IEnumerable<string> requiredLines,
            string description)
        {
            HashSet<string> activeLines = GetActiveLines(content);
            foreach (string required in requiredLines)
            {
                Assert(activeLines.Contains(required), description + " is missing: " + required + ".");
            }
        }

        private static bool ContainsActiveLine(string content, string expected)
        {
            return GetActiveLines(content).Contains(expected);
        }

        private static HashSet<string> GetActiveLines(string content)
        {
            return new HashSet<string>(
                content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => line.Length != 0
                        && !line.StartsWith("#", StringComparison.Ordinal)
                        && !line.StartsWith(";", StringComparison.Ordinal)),
                StringComparer.Ordinal);
        }

        private static string RequireDirectory(string path, string description)
        {
            string fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException("Missing " + description + ": " + fullPath + ".");
            }

            return fullPath;
        }

        private static string RequireFile(string path, string description)
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Missing " + description + ".", fullPath);
            }

            return fullPath;
        }

        private static void AssertFilesEqual(string expectedPath, string actualPath, string description)
        {
            expectedPath = RequireFile(expectedPath, "source for " + description);
            actualPath = RequireFile(actualPath, description);
            string expected = ComputeSha256(expectedPath);
            string actual = ComputeSha256(actualPath);
            Assert(string.Equals(expected, actual, StringComparison.Ordinal), description + " differs from its source asset.");
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static void VerifySequence(string[] expected, string[] actual, string contract)
        {
            if (expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                contract + " differs. Expected [" + string.Join(", ", expected) + "] but found [" + string.Join(", ", actual) + "].");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}

internal static class Stage5RepositoryChecks
{
    internal static void VerifyRepository(string repositoryRoot)
    {
        AORebirth.LinuxBuild.Contracts.Stage5RepositoryChecks.VerifyRepository(repositoryRoot);
    }

    internal static void VerifyPublish(string repositoryRoot, string publishDirectory)
    {
        AORebirth.LinuxBuild.Contracts.Stage5RepositoryChecks.VerifyPublish(repositoryRoot, publishDirectory);
    }

    internal static void VerifyPublish(
        string repositoryRoot,
        string publishDirectory,
        string expectedRuntimeIdentifier,
        string expectedPackageKind)
    {
        AORebirth.LinuxBuild.Contracts.Stage5RepositoryChecks.VerifyPublish(
            repositoryRoot,
            publishDirectory,
            expectedRuntimeIdentifier,
            expectedPackageKind);
    }
}
