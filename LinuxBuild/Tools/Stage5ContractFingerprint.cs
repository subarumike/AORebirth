namespace AORebirth.LinuxBuild.Contracts
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class Stage5ContractFingerprint
    {
        private const string ManifestName = "AORebirth.Stage5ChatEngineContract";
        private const string ManifestVersion = "1";

        private static readonly string[] AuthenticationTypeNames =
        {
            "AO.Core.Encryption.BigInteger",
            "AO.Core.Encryption.LoginEncryption",
            "AORebirth.Core.Encryption.PasswordHash"
        };

        internal static void WriteLegacy(string manifestPath, Assembly chatEngineAssembly)
        {
            Assembly authenticationAssembly = ResolveLegacyAuthenticationAssembly(chatEngineAssembly);
            string passwordFixture = CreatePasswordFixtureLine(authenticationAssembly);
            WriteManifest(manifestPath, AppendManifestLine(Create(chatEngineAssembly, authenticationAssembly), passwordFixture));
        }

        internal static void VerifyLegacy(string manifestPath, Assembly chatEngineAssembly)
        {
            string expected = ReadManifest(manifestPath);
            string passwordFixture = GetPasswordFixtureLine(expected);
            Assembly authenticationAssembly = ResolveLegacyAuthenticationAssembly(chatEngineAssembly);
            VerifyPasswordFixture(authenticationAssembly, passwordFixture);
            VerifyExact(
                expected,
                AppendManifestLine(Create(chatEngineAssembly, authenticationAssembly), passwordFixture),
                "Legacy Stage 5 ChatEngine contract");
        }

        internal static void VerifyLinux(string manifestPath, Assembly chatEngineAssembly, Assembly authenticationAssembly)
        {
            string expected = ReadManifest(manifestPath);
            string passwordFixture = GetPasswordFixtureLine(expected);
            VerifyPasswordFixture(authenticationAssembly, passwordFixture);
            string actual = AppendManifestLine(Create(chatEngineAssembly, authenticationAssembly), passwordFixture);
            VerifyExact(
                WithoutReferences(expected),
                WithoutReferences(actual),
                "Stage 5 ChatEngine semantic contract");
            VerifyMappedReferences(expected, actual);
            VerifyAuthenticationAssemblyShape(authenticationAssembly);
        }

        internal static void VerifyOffline(Assembly chatEngineAssembly, Assembly authenticationAssembly)
        {
            Create(chatEngineAssembly, authenticationAssembly);
            VerifyAuthenticationAssemblyShape(authenticationAssembly);
            VerifyPasswordFixture(authenticationAssembly, CreatePasswordFixtureLine(authenticationAssembly));
        }

        private static string Create(Assembly chatEngineAssembly, Assembly authenticationAssembly)
        {
            if (chatEngineAssembly == null)
            {
                throw new ArgumentNullException(nameof(chatEngineAssembly));
            }

            if (authenticationAssembly == null)
            {
                throw new ArgumentNullException(nameof(authenticationAssembly));
            }

            if (!string.Equals(chatEngineAssembly.GetName().Name, "ChatEngine", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The Stage 5 fixture must inspect ChatEngine.");
            }

            var lines = new List<string>();
            AddLine(lines, "manifest", ManifestName, ManifestVersion);
            foreach (string line in SplitLines(Stage2ContractFingerprint.Create(new[] { chatEngineAssembly })))
            {
                AddLine(lines, "api", line);
            }

            AddProtectedContracts(lines, chatEngineAssembly);
            AddAuthenticationContracts(lines, authenticationAssembly);
            AddReferenceContracts(lines, chatEngineAssembly);
            AddTeamLevelContracts(lines, chatEngineAssembly);
            AddRegistryContracts(lines, chatEngineAssembly);
            AddTopologyContracts(lines, chatEngineAssembly);
            AddWireContracts(lines, chatEngineAssembly);
            AddParserSafetyContract(lines);
            return NormalizeManifest(string.Join("\n", lines) + "\n");
        }

        private static void AddProtectedContracts(ICollection<string> lines, Assembly assembly)
        {
            const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
            foreach (Type type in GetExportedTypes(assembly).OrderBy(Stage2ContractFingerprint.NormalizeType, StringComparer.Ordinal))
            {
                var contracts = new List<string>();
                contracts.AddRange(type.GetConstructors(flags).Where(IsProtected).Select(FormatConstructor));
                contracts.AddRange(type.GetMethods(flags).Where(IsProtected).Select(FormatMethod));
                contracts.AddRange(type.GetFields(flags).Where(IsProtected).Select(FormatField));
                contracts.AddRange(type.GetProperties(flags).Where(IsProtected).Select(FormatProperty));
                contracts.AddRange(type.GetEvents(flags).Where(IsProtected).Select(FormatEvent));
                foreach (string contract in contracts.OrderBy(value => value, StringComparer.Ordinal))
                {
                    AddLine(lines, "protected", Stage2ContractFingerprint.NormalizeType(type), contract);
                }
            }
        }

        private static void AddAuthenticationContracts(ICollection<string> lines, Assembly assembly)
        {
            Type[] types = AuthenticationTypeNames
                .Select(name => assembly.GetType(name, true, false))
                .OrderBy(Stage2ContractFingerprint.NormalizeType, StringComparer.Ordinal)
                .ToArray();

            AddLine(lines, "auth.type-set", string.Join(",", types.Select(Stage2ContractFingerprint.NormalizeType)));
            foreach (Type type in types)
            {
                AddLine(
                    lines,
                    "auth.type",
                    Stage2ContractFingerprint.NormalizeType(type),
                    type.IsClass ? "class" : type.IsValueType ? "struct" : "other",
                    type.IsSealed.ToString().ToLowerInvariant(),
                    type.IsAbstract.ToString().ToLowerInvariant(),
                    type.BaseType == null ? string.Empty : Stage2ContractFingerprint.NormalizeType(type.BaseType));

                const BindingFlags publicDeclared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;
                var members = new List<string>();
                members.AddRange(type.GetConstructors(publicDeclared).Select(FormatConstructor));
                members.AddRange(type.GetMethods(publicDeclared).Select(FormatMethod));
                members.AddRange(type.GetFields(publicDeclared).Select(FormatField));
                members.AddRange(type.GetProperties(publicDeclared).Select(FormatProperty));
                members.AddRange(type.GetEvents(publicDeclared).Select(FormatEvent));
                foreach (string member in members.OrderBy(value => value, StringComparer.Ordinal))
                {
                    AddLine(lines, "auth.member", Stage2ContractFingerprint.NormalizeType(type), member);
                }
            }
        }

        private static void AddReferenceContracts(ICollection<string> lines, Assembly assembly)
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies().OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                AddLine(
                    lines,
                    "reference",
                    reference.Name,
                    reference.Version == null ? string.Empty : reference.Version.ToString(),
                    FormatPublicKeyToken(reference.GetPublicKeyToken()));
            }
        }

        private static void AddTeamLevelContracts(ICollection<string> lines, Assembly assembly)
        {
            Type type = GetRequiredType(assembly, "ChatEngine.Lists.TeamLevelRanges");
            MethodInfo tryGetRange = GetRequiredMethod(type, "TryGetRange", typeof(int), typeof(int).MakeByRefType(), typeof(int).MakeByRefType());
            MethodInfo isCompatible = GetRequiredMethod(type, "IsCompatible", typeof(int), typeof(int));

            int[] levels = Enumerable.Range(1, 220).Concat(new[] { -1, 0, 221, 1000 }).OrderBy(value => value).ToArray();
            foreach (int level in levels)
            {
                object[] arguments = { level, 0, 0 };
                bool found = (bool)tryGetRange.Invoke(null, arguments);
                int minimum = (int)arguments[1];
                int maximum = (int)arguments[2];
                bool acceptsMinimum = (bool)isCompatible.Invoke(null, new object[] { level, minimum });
                bool acceptsMaximum = (bool)isCompatible.Invoke(null, new object[] { level, maximum });
                AddLine(
                    lines,
                    "team-range",
                    level.ToString(CultureInfo.InvariantCulture),
                    found.ToString().ToLowerInvariant(),
                    minimum.ToString(CultureInfo.InvariantCulture),
                    maximum.ToString(CultureInfo.InvariantCulture),
                    acceptsMinimum.ToString().ToLowerInvariant(),
                    acceptsMaximum.ToString().ToLowerInvariant());
            }
        }

        private static void AddRegistryContracts(ICollection<string> lines, Assembly assembly)
        {
            VerifyLftRegistry(assembly);
            VerifyLftPlayfieldRegistry(assembly);
            AddLine(lines, "static.registry", "LftRegistry", "verified");
            AddLine(lines, "static.registry", "LftPlayfieldRegistry", "verified");
        }

        private static void VerifyLftRegistry(Assembly assembly)
        {
            Type type = GetRequiredType(assembly, "ChatEngine.Lists.LftRegistry");
            MethodInfo upsert = GetRequiredMethod(type, "Upsert", typeof(uint), typeof(string));
            MethodInfo remove = GetRequiredMethod(type, "Remove", typeof(uint));
            MethodInfo tryGet = GetRequiredMethod(type, "TryGetComment", typeof(uint), typeof(string).MakeByRefType());
            MethodInfo snapshot = GetRequiredMethod(type, "Snapshot");
            const uint first = 0xF5001000;

            for (uint index = 0; index < 24; index++)
            {
                remove.Invoke(null, new object[] { first + index });
            }

            upsert.Invoke(null, new object[] { 0u, "ignored" });
            object[] missingArguments = { 0u, null };
            Assert(!(bool)tryGet.Invoke(null, missingArguments), "LftRegistry accepted character id zero.");

            upsert.Invoke(null, new object[] { first, "first" });
            upsert.Invoke(null, new object[] { first, "replacement" });
            object[] getArguments = { first, null };
            Assert((bool)tryGet.Invoke(null, getArguments), "LftRegistry lost an upserted value.");
            Assert(string.Equals((string)getArguments[1], "replacement", StringComparison.Ordinal), "LftRegistry did not replace an existing comment.");
            upsert.Invoke(null, new object[] { first, string.Empty });
            getArguments = new object[] { first, null };
            Assert(!(bool)tryGet.Invoke(null, getArguments), "LftRegistry did not remove an empty comment.");

            var tasks = new List<Task>();
            for (uint index = 1; index < 24; index++)
            {
                uint captured = index;
                tasks.Add(Task.Run(delegate
                {
                    upsert.Invoke(null, new object[] { first + captured, "comment-" + captured.ToString(CultureInfo.InvariantCulture) });
                }));
            }

            Task.WaitAll(tasks.ToArray());
            int found = 0;
            foreach (object pair in (IEnumerable)snapshot.Invoke(null, null))
            {
                uint key = (uint)pair.GetType().GetProperty("Key").GetValue(pair, null);
                if (key > first && key < first + 24)
                {
                    found++;
                }
            }

            Assert(found == 23, "LftRegistry snapshot was not complete after concurrent upserts.");
            for (uint index = 0; index < 24; index++)
            {
                remove.Invoke(null, new object[] { first + index });
            }
        }

        private static void VerifyLftPlayfieldRegistry(Assembly assembly)
        {
            Type type = GetRequiredType(assembly, "ChatEngine.Lists.LftPlayfieldRegistry");
            FieldInfo prefix = type.GetField("PlayfieldCommandPrefix", BindingFlags.Public | BindingFlags.Static);
            Assert(prefix != null && prefix.IsLiteral, "Lft playfield command prefix is not a public constant.");
            Assert(string.Equals((string)prefix.GetRawConstantValue(), "#aorebirth-pf", StringComparison.Ordinal), "Unexpected LFT playfield command prefix.");

            MethodInfo set = GetRequiredMethod(type, "Set", typeof(uint), typeof(int));
            MethodInfo remove = GetRequiredMethod(type, "Remove", typeof(uint));
            MethodInfo tryGet = GetRequiredMethod(type, "TryGet", typeof(uint), typeof(int).MakeByRefType());
            const uint first = 0xF5002000;
            for (uint index = 0; index < 24; index++)
            {
                remove.Invoke(null, new object[] { first + index });
            }

            set.Invoke(null, new object[] { 0u, 123 });
            set.Invoke(null, new object[] { first, 0 });
            object[] invalidArguments = { first, 0 };
            Assert(!(bool)tryGet.Invoke(null, invalidArguments), "LftPlayfieldRegistry accepted invalid input.");

            var tasks = new List<Task>();
            for (uint index = 1; index < 24; index++)
            {
                uint captured = index;
                tasks.Add(Task.Run(delegate
                {
                    set.Invoke(null, new object[] { first + captured, 5000 + (int)captured });
                }));
            }

            Task.WaitAll(tasks.ToArray());
            for (uint index = 1; index < 24; index++)
            {
                object[] arguments = { first + index, 0 };
                Assert((bool)tryGet.Invoke(null, arguments), "LftPlayfieldRegistry lost a concurrent update.");
                Assert((int)arguments[1] == 5000 + index, "LftPlayfieldRegistry returned an incorrect playfield.");
            }

            for (uint index = 0; index < 24; index++)
            {
                remove.Invoke(null, new object[] { first + index });
            }
        }

        private static void AddTopologyContracts(ICollection<string> lines, Assembly assembly)
        {
            string originalDirectory = Environment.CurrentDirectory;
            string originalConfigPath = Environment.GetEnvironmentVariable("AO_REBIRTH_CONFIG_PATH");
            string originalMySqlConnection = Environment.GetEnvironmentVariable(
                "AO_REBIRTH_MYSQL_CONNECTION");
            string temporaryDirectory = Path.Combine(Path.GetTempPath(), "aorebirth-stage5-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            object server = null;
            try
            {
                File.WriteAllText(
                    Path.Combine(temporaryDirectory, "Config.xml"),
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><Config><Locale>en</Locale><Motd>stage5-fixture-motd</Motd></Config>",
                    new UTF8Encoding(false));
                Environment.SetEnvironmentVariable(
                    "AO_REBIRTH_CONFIG_PATH",
                    Path.Combine(temporaryDirectory, "Config.xml"));
                Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", null);
                Environment.CurrentDirectory = temporaryDirectory;

                Type serverType = GetRequiredType(assembly, "ChatEngine.CoreServer.ChatServer");
                server = Activator.CreateInstance(serverType);
                Assert(!(bool)GetRequiredProperty(serverType, "IsRunning").GetValue(server, null), "ChatServer constructor started the server.");
                Assert(Convert.ToInt32(GetRequiredProperty(serverType, "ClientCount").GetValue(server, null), CultureInfo.InvariantCulture) == 0, "ChatServer constructor created clients.");
                Assert(GetInheritedField(serverType, "_tcpListen").GetValue(server) == null, "ChatServer constructor created a TCP listener.");
                Assert(GetInheritedField(serverType, "_udpListen").GetValue(server) == null, "ChatServer constructor created a UDP listener.");

                FieldInfo motdField = serverType.GetField("MessageOfTheDay", BindingFlags.Public | BindingFlags.Instance);
                Assert(motdField != null, "ChatServer MessageOfTheDay field is missing.");
                Assert(string.Equals((string)motdField.GetValue(server), "stage5-fixture-motd", StringComparison.Ordinal), "ChatServer did not read the fixture MOTD.");

                FieldInfo channelsField = serverType.GetField("Channels", BindingFlags.Public | BindingFlags.Instance);
                Assert(channelsField != null, "ChatServer Channels field is missing.");
                var channelContracts = new List<string>();
                foreach (object channel in (IEnumerable)channelsField.GetValue(server))
                {
                    channelContracts.Add(FormatChannel(channel));
                }

                channelContracts.Sort(StringComparer.Ordinal);
                Assert(channelContracts.Count == 8, "ChatServer constructor must create exactly eight channels.");
                foreach (string channelContract in channelContracts)
                {
                    AddLine(lines, "topology.channel", channelContract);
                }

                AddLine(lines, "topology.server", "running=false", "clients=0", "tcp=null", "udp=null", "motd=stage5-fixture-motd");
            }
            finally
            {
                if (server != null)
                {
                    MethodInfo dispose = server.GetType().GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
                    if (dispose != null)
                    {
                        dispose.Invoke(server, null);
                    }
                }

                Environment.CurrentDirectory = originalDirectory;
                Environment.SetEnvironmentVariable("AO_REBIRTH_CONFIG_PATH", originalConfigPath);
                Environment.SetEnvironmentVariable(
                    "AO_REBIRTH_MYSQL_CONNECTION",
                    originalMySqlConnection);
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, true);
                }
            }
        }

        private static string FormatChannel(object channel)
        {
            Type type = channel.GetType();
            var values = new List<string>
            {
                Stage2ContractFingerprint.NormalizeType(type),
                "id=" + Convert.ToString(GetRequiredProperty(type, "ChannelId").GetValue(channel, null), CultureInfo.InvariantCulture),
                "name=" + Convert.ToString(GetRequiredField(type, "ChannelName").GetValue(channel), CultureInfo.InvariantCulture),
                "type=" + Convert.ToString(GetRequiredProperty(type, "channelType").GetValue(channel, null), CultureInfo.InvariantCulture),
                "flags=" + Convert.ToString(GetRequiredField(type, "channelFlags").GetValue(channel), CultureInfo.InvariantCulture)
            };

            AddOptionalField(values, type, channel, "MinLevel");
            AddOptionalField(values, type, channel, "MaxLevel");
            AddOptionalField(values, type, channel, "characterSide");
            return string.Join(";", values);
        }

        private static void AddOptionalField(ICollection<string> values, Type type, object instance, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                values.Add(fieldName + "=" + Convert.ToString(field.GetValue(instance), CultureInfo.InvariantCulture));
            }
        }

        private static void AddWireContracts(ICollection<string> lines, Assembly assembly)
        {
            string baseline = CreateWireContract(assembly);
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                foreach (string cultureName in new[] { "en-US", "tr-TR", "de-DE" })
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
                    VerifyExact(baseline, CreateWireContract(assembly), "Stage 5 culture-stable packet contract for " + cultureName);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
                Thread.CurrentThread.CurrentUICulture = originalUiCulture;
            }

            var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => CreateWireContract(assembly))).ToArray();
            Task.WaitAll(tasks);
            foreach (Task<string> task in tasks)
            {
                VerifyExact(baseline, task.Result, "Stage 5 threaded packet contract");
            }

            foreach (string line in SplitLines(baseline))
            {
                AddLine(lines, "wire", line);
            }
        }

        private static string CreateWireContract(Assembly assembly)
        {
            var fixtures = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            AddPacket(fixtures, "authentication-seed", InvokeBytes(assembly, "ChatEngine.Packets.AuthenticationSeed", "Create", new[] { typeof(string) }, new object[] { "seed|\u03bc\n" }));
            AddPacket(fixtures, "buddy-online", InvokeBytes(assembly, "ChatEngine.Packets.BuddyOnlineStatus", "Create", new[] { typeof(uint), typeof(uint), typeof(byte[]) }, new object[] { 0x01020304u, 1u, new byte[] { 9, 8, 7 } }));
            AddPacket(fixtures, "buddy-removed", InvokeBytes(assembly, "ChatEngine.Packets.BuddyRemoved", "Create", new[] { typeof(uint) }, new object[] { 0x10203040u }));
            AddPacket(fixtures, "channel-join", InvokeBytes(assembly, "ChatEngine.Packets.ChannelJoin", "Create", new[] { typeof(ulong), typeof(string), typeof(uint), typeof(byte[]) }, new object[] { 0x0102030405060708UL, "Stage5|\u03bc", 0x11223344u, new byte[] { 1, 3, 5, 7 } }));
            AddPacket(fixtures, "channel-leave", InvokeBytes(assembly, "ChatEngine.Packets.ChannelLeave", "Create", new[] { typeof(byte[]) }, new object[] { new byte[] { 1, 2, 3, 4, 5 } }));
            AddPacket(fixtures, "lft-clear", InvokeBytes(assembly, "ChatEngine.Packets.LftQueryReply", "CreateClear", Type.EmptyTypes, new object[0]));
            AddPacket(fixtures, "lft-entry", CreateLftEntryPacket(assembly));
            AddPacket(fixtures, "login-error", InvokeBytes(assembly, "ChatEngine.Packets.LoginError", "Create", Type.EmptyTypes, new object[0]));
            AddPacket(fixtures, "login-ok", InvokeBytes(assembly, "ChatEngine.Packets.LoginOk", "Create", Type.EmptyTypes, new object[0]));
            AddPacket(fixtures, "msg-anonymous-vicinity", InvokeBytes(assembly, "ChatEngine.Packets.MsgAnonymousVicinity", "Create", new[] { typeof(string), typeof(string), typeof(string) }, new object[] { "arg", "message|\u03bc", "blob\n" }));
            AddPacket(fixtures, "msg-predefined", InvokeBytes(assembly, "ChatEngine.Packets.MsgPredefined", "Create", new[] { typeof(uint), typeof(uint), typeof(uint), typeof(string) }, new object[] { 11u, 22u, 33u, "args|\u03bc" }));
            AddPacket(fixtures, "msg-private", InvokeBytes(assembly, "ChatEngine.Packets.MsgPrivate", "Create", new[] { typeof(uint), typeof(string), typeof(int), typeof(int) }, new object[] { 44u, "private|\u03bc", -7, 9 }));
            AddPacket(fixtures, "msg-private-group", InvokeBytes(assembly, "ChatEngine.Packets.MsgPrivateGroup", "Create", new[] { typeof(uint), typeof(string), typeof(string) }, new object[] { 55u, "group|\u03bc", "blob\n" }));
            AddPacket(fixtures, "msg-system", InvokeBytes(assembly, "ChatEngine.Packets.MsgSystem", "Create", new[] { typeof(string) }, new object[] { "system|\u03bc\n" }));
            AddPacket(fixtures, "msg-system-pet", InvokeBytes(assembly, "ChatEngine.Packets.MsgSystem", "CreatePet", new[] { typeof(string), typeof(int), typeof(int) }, new object[] { "pet|\u03bc", 17, -19 }));
            AddPacket(fixtures, "msg-vicinity", InvokeBytes(assembly, "ChatEngine.Packets.MsgVicinity", "Create", new[] { typeof(uint), typeof(string), typeof(byte) }, new object[] { 66u, "vicinity|\u03bc", (byte)7 }));
            AddPacket(fixtures, "name-lookup", InvokeBytes(assembly, "ChatEngine.Packets.NameLookupResult", "Create", new[] { typeof(uint), typeof(string) }, new object[] { 77u, "StageFive" }));
            AddPacket(fixtures, "player-id-unknown", InvokeBytes(assembly, "ChatEngine.Packets.PlayerIdUnknown", "Create", new[] { typeof(uint) }, new object[] { 88u }));
            AddPacket(fixtures, "player-name-internal", InvokeBytes(assembly, "ChatEngine.Packets.PlayerName", "Create", new[] { typeof(uint), typeof(string) }, new object[] { 99u, "InternalName" }));
            AddPacket(fixtures, "private-group-invitation", InvokeBytes(assembly, "ChatEngine.Packets.PrivateGroupInvitation", "Create", new[] { typeof(uint) }, new object[] { 101u }));
            AddPacket(fixtures, "private-group-kicked", InvokeBytes(assembly, "ChatEngine.Packets.PrivateGroupKicked", "Create", new[] { typeof(uint) }, new object[] { 102u }));
            AddPacket(fixtures, "private-group-message", InvokeBytes(assembly, "ChatEngine.Packets.PrivateGroupMessage", "Create", new[] { typeof(uint), typeof(uint), typeof(string), typeof(string) }, new object[] { 103u, 104u, "message|\u03bc", "blob\n" }));
            AddPacket(fixtures, "private-group-player-joined", InvokeBytes(assembly, "ChatEngine.Packets.PrivateGroupPlayerJoined", "Create", new[] { typeof(uint), typeof(uint) }, new object[] { 105u, 106u }));
            AddPacket(fixtures, "private-group-player-left", InvokeBytes(assembly, "ChatEngine.Packets.PrivateGroupPlayerLeft", "Create", new[] { typeof(uint), typeof(uint) }, new object[] { 107u, 108u }));

            byte[] packetIo = CreateAndVerifyPacketIo(assembly);
            AddPacket(fixtures, "packet-io", packetIo);

            var lines = new List<string>();
            foreach (KeyValuePair<string, byte[]> fixture in fixtures)
            {
                AddLine(
                    lines,
                    fixture.Key,
                    fixture.Value.Length.ToString(CultureInfo.InvariantCulture),
                    ComputeSha256(fixture.Value),
                    Convert.ToBase64String(fixture.Value));
            }

            return NormalizeManifest(string.Join("\n", lines) + "\n");
        }

        private static byte[] CreateLftEntryPacket(Assembly assembly)
        {
            Type type = GetRequiredType(assembly, "ChatEngine.Packets.LftQueryReply");
            Type entryType = type.GetNestedType("Entry", BindingFlags.Public);
            Assert(entryType != null, "LftQueryReply.Entry is missing.");
            object entry = Activator.CreateInstance(entryType);
            SetRequiredField(entry, "CharacterId", 0x01020304u);
            SetRequiredField(entry, "Name", "FixtureName");
            SetRequiredField(entry, "Level", 220);
            SetRequiredField(entry, "Profession", 12);
            SetRequiredField(entry, "Side", 2);
            SetRequiredField(entry, "Playfield", 127u);
            SetRequiredField(entry, "Comment", "LFT\nfixture");
            MethodInfo method = GetRequiredMethod(type, "CreateEntry", entryType);
            return (byte[])method.Invoke(null, new[] { entry });
        }

        private static byte[] CreateAndVerifyPacketIo(Assembly assembly)
        {
            Type writerType = GetRequiredType(assembly, "ChatEngine.PacketWriter");
            object writer = Activator.CreateInstance(writerType, new object[] { (ushort)0x1234 });
            GetRequiredMethod(writerType, "WriteByte", typeof(byte)).Invoke(writer, new object[] { (byte)0xA5 });
            GetRequiredMethod(writerType, "WriteUInt16", typeof(ushort)).Invoke(writer, new object[] { (ushort)0xBEEF });
            GetRequiredMethod(writerType, "WriteUInt32", typeof(uint)).Invoke(writer, new object[] { 0x10203040u });
            GetRequiredMethod(writerType, "WriteString", typeof(string)).Invoke(writer, new object[] { "packet|\u03bc" });
            GetRequiredMethod(writerType, "WriteBytes", typeof(byte[])).Invoke(writer, new object[] { new byte[] { 7, 8, 9 } });
            byte[] packet = (byte[])GetRequiredMethod(writerType, "Finish").Invoke(writer, null);

            Type readerType = GetRequiredType(assembly, "ChatEngine.PacketReader");
            ConstructorInfo constructor = readerType.GetConstructor(new[] { typeof(byte[]).MakeByRefType() });
            Assert(constructor != null, "PacketReader ref-byte-array constructor is missing.");
            object[] constructorArguments = { packet };
            object reader = constructor.Invoke(constructorArguments);
            try
            {
                Assert((ushort)GetRequiredMethod(readerType, "ReadUInt16").Invoke(reader, null) == 0x1234, "PacketReader type header mismatch.");
                ushort payloadLength = (ushort)GetRequiredMethod(readerType, "ReadUInt16").Invoke(reader, null);
                Assert(payloadLength == packet.Length - 4, "PacketReader payload length mismatch.");
                Assert((byte)GetRequiredMethod(readerType, "ReadByte").Invoke(reader, null) == 0xA5, "PacketReader byte mismatch.");
                Assert((ushort)GetRequiredMethod(readerType, "ReadUInt16").Invoke(reader, null) == 0xBEEF, "PacketReader UInt16 mismatch.");
                Assert((uint)GetRequiredMethod(readerType, "ReadUInt32").Invoke(reader, null) == 0x10203040u, "PacketReader UInt32 mismatch.");
                Assert(string.Equals((string)GetRequiredMethod(readerType, "ReadString").Invoke(reader, null), "packet|\u03bc", StringComparison.Ordinal), "PacketReader string mismatch.");
                byte[] tail = (byte[])GetRequiredMethod(readerType, "ReadBytes", typeof(int)).Invoke(reader, new object[] { 3 });
                Assert(tail.SequenceEqual(new byte[] { 7, 8, 9 }), "PacketReader byte-array mismatch.");
            }
            finally
            {
                GetRequiredMethod(readerType, "Finish").Invoke(reader, null);
            }

            return packet;
        }

        private static void AddParserSafetyContract(ICollection<string> lines)
        {
            AddLine(lines, "parser.mode", "source-mapped-only", "handlers-not-invoked");
            AddLine(lines, "unsafe.excluded", "AccountCharacterList.Create");
            AddLine(lines, "unsafe.excluded", "Authenticate/AuthenticateBot handlers");
            AddLine(lines, "unsafe.excluded", "ChatServer.Start/Parser.Parse/DAO/Connector.Open");
        }

        private static byte[] InvokeBytes(Assembly assembly, string typeName, string methodName, Type[] parameterTypes, object[] arguments)
        {
            MethodInfo method = GetRequiredMethod(GetRequiredType(assembly, typeName), methodName, parameterTypes);
            return (byte[])method.Invoke(null, arguments);
        }

        private static void AddPacket(IDictionary<string, byte[]> fixtures, string name, byte[] bytes)
        {
            Assert(bytes != null, "Packet fixture " + name + " returned null.");
            fixtures.Add(name, bytes);
        }

        private static void SetRequiredField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert(field != null, target.GetType().FullName + "." + name + " is missing.");
            object converted = field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, value) : Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture);
            field.SetValue(target, converted);
        }

        private static Assembly ResolveLegacyAuthenticationAssembly(Assembly chatEngineAssembly)
        {
            AssemblyName reference = chatEngineAssembly.GetReferencedAssemblies()
                .SingleOrDefault(value => string.Equals(value.Name, "AORebirth.Core", StringComparison.Ordinal));
            if (reference == null)
            {
                throw new InvalidOperationException("Legacy ChatEngine no longer references AORebirth.Core for authentication.");
            }

            return Assembly.Load(reference);
        }

        private static void VerifyAuthenticationAssemblyShape(Assembly assembly)
        {
            AssemblyName name = assembly.GetName();
            Assert(string.Equals(name.Name, "AORebirth.Chat.Authentication", StringComparison.Ordinal), "Unexpected Linux chat authentication assembly name.");
            Assert(name.Version != null && name.Version.Equals(new Version(1, 0, 0, 0)), "Unexpected Linux chat authentication assembly version.");

            string[] actualTypes = GetExportedTypes(assembly)
                .Select(Stage2ContractFingerprint.NormalizeType)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] expectedTypes = AuthenticationTypeNames.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Assert(actualTypes.SequenceEqual(expectedTypes), "The Linux chat authentication assembly must export only the exact three legacy encryption types.");
        }

        private static string CreatePasswordFixtureLine(Assembly assembly)
        {
            Type passwordHash = GetRequiredType(assembly, "AORebirth.Core.Encryption.PasswordHash");
            MethodInfo createHash = GetRequiredMethod(passwordHash, "CreateHash", typeof(string));
            const string password = "Stage5-Password-\u03bc";
            string hash = (string)createHash.Invoke(null, new object[] { password });
            Assert(!string.IsNullOrEmpty(hash), "PasswordHash.CreateHash returned an empty hash.");
            var line = new List<string>();
            AddLine(line, "auth.password-fixture", password, hash);
            return line[0];
        }

        private static void VerifyPasswordFixture(Assembly assembly, string fixtureLine)
        {
            string[] parts = SplitEscaped(fixtureLine);
            Assert(parts.Length == 3 && string.Equals(parts[0], "auth.password-fixture", StringComparison.Ordinal), "Stage 5 password fixture is malformed.");
            Type passwordHash = GetRequiredType(assembly, "AORebirth.Core.Encryption.PasswordHash");
            MethodInfo validate = GetRequiredMethod(passwordHash, "ValidatePassword", typeof(string), typeof(string));
            Assert((bool)validate.Invoke(null, new object[] { parts[1], parts[2] }), "Known Stage 5 password did not validate against the legacy-generated hash.");
            Assert(!(bool)validate.Invoke(null, new object[] { parts[1] + "-wrong", parts[2] }), "Wrong Stage 5 password validated against the legacy-generated hash.");
        }

        private static string GetPasswordFixtureLine(string manifest)
        {
            string[] matches = SplitLines(manifest)
                .Where(line => line.StartsWith("auth.password-fixture|", StringComparison.Ordinal))
                .ToArray();
            Assert(matches.Length == 1, "Stage 5 manifest must contain exactly one legacy-generated password fixture.");
            return matches[0];
        }

        private static string AppendManifestLine(string manifest, string line)
        {
            return NormalizeManifest(NormalizeManifest(manifest).TrimEnd('\n') + "\n" + line + "\n");
        }

        private static void VerifyMappedReferences(string expectedManifest, string actualManifest)
        {
            var expected = new HashSet<string>(GetReferenceNames(expectedManifest).Where(name => !IsFrameworkReference(name)), StringComparer.Ordinal);
            var actual = new HashSet<string>(GetReferenceNames(actualManifest).Where(name => !IsFrameworkReference(name)), StringComparer.Ordinal);

            if (expected.Remove("AORebirth.Core"))
            {
                expected.Add("AORebirth.Chat.Authentication");
            }

            expected.Remove("NBug");
            expected.Remove("PlayfieldLoader");
            expected.Remove("Dapper");
            expected.Remove("MsgPack");

            foreach (string forbidden in new[] { "AORebirth.Core", "NBug", "PlayfieldLoader" })
            {
                Assert(!actual.Contains(forbidden), "Linux ChatEngine retains forbidden direct reference " + forbidden + ".");
            }

            VerifyExact(
                string.Join("\n", expected.OrderBy(value => value, StringComparer.Ordinal)) + "\n",
                string.Join("\n", actual.OrderBy(value => value, StringComparer.Ordinal)) + "\n",
                "Stage 5 mapped direct references");
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
            return NormalizeManifest(
                string.Join(
                    "\n",
                    SplitLines(manifest).Where(line => !line.StartsWith("reference|", StringComparison.Ordinal))) + "\n");
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

        private static Type GetRequiredType(Assembly assembly, string name)
        {
            Type type = assembly.GetType(name, false, false);
            if (type == null)
            {
                throw new InvalidOperationException("Required type is missing: " + name + ".");
            }

            return type;
        }

        private static MethodInfo GetRequiredMethod(Type type, string name, params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null,
                parameterTypes,
                null);
            if (method == null)
            {
                throw new InvalidOperationException("Required method is missing: " + type.FullName + "." + name + ".");
            }

            return method;
        }

        private static FieldInfo GetRequiredField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field == null)
            {
                throw new InvalidOperationException("Required field is missing: " + type.FullName + "." + name + ".");
            }

            return field;
        }

        private static FieldInfo GetInheritedField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (field != null)
                {
                    return field;
                }
            }

            throw new InvalidOperationException("Required inherited field is missing: " + type.FullName + "." + name + ".");
        }

        private static PropertyInfo GetRequiredProperty(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (property != null)
                {
                    return property;
                }
            }

            throw new InvalidOperationException("Required property is missing: " + type.FullName + "." + name + ".");
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
            return (property.GetMethod != null && IsProtected(property.GetMethod))
                || (property.SetMethod != null && IsProtected(property.SetMethod));
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
            string generic = method.IsGenericMethodDefinition
                ? "<" + string.Join(",", method.GetGenericArguments().Select(value => value.Name)) + ">"
                : string.Empty;
            return "method " + FormatAccess(method) + " " + (method.IsStatic ? "static" : "instance") + " "
                + Stage2ContractFingerprint.NormalizeType(method.ReturnType) + " " + method.Name + generic + "(" + FormatParameters(method.GetParameters()) + ")"
                + " virtual=" + method.IsVirtual.ToString().ToLowerInvariant()
                + " abstract=" + method.IsAbstract.ToString().ToLowerInvariant()
                + " final=" + method.IsFinal.ToString().ToLowerInvariant();
        }

        private static string FormatField(FieldInfo field)
        {
            string constant = field.IsLiteral ? FormatValue(field.GetRawConstantValue()) : string.Empty;
            return "field " + FormatAccess(field) + " " + (field.IsStatic ? "static" : "instance") + " "
                + Stage2ContractFingerprint.NormalizeType(field.FieldType) + " " + field.Name
                + " initonly=" + field.IsInitOnly.ToString().ToLowerInvariant()
                + " literal=" + field.IsLiteral.ToString().ToLowerInvariant()
                + " constant=" + constant;
        }

        private static string FormatProperty(PropertyInfo property)
        {
            MethodInfo getter = property.GetGetMethod(true);
            MethodInfo setter = property.GetSetMethod(true);
            string access = getter != null ? FormatAccess(getter) : setter != null ? FormatAccess(setter) : "none";
            return "property " + access + " " + Stage2ContractFingerprint.NormalizeType(property.PropertyType) + " " + property.Name
                + "(" + FormatParameters(property.GetIndexParameters()) + ")"
                + " get=" + (getter == null ? "none" : FormatAccess(getter))
                + " set=" + (setter == null ? "none" : FormatAccess(setter));
        }

        private static string FormatEvent(EventInfo eventInfo)
        {
            MethodInfo add = eventInfo.GetAddMethod(true);
            MethodInfo remove = eventInfo.GetRemoveMethod(true);
            return "event " + Stage2ContractFingerprint.NormalizeType(eventInfo.EventHandlerType) + " " + eventInfo.Name
                + " add=" + (add == null ? "none" : FormatAccess(add))
                + " remove=" + (remove == null ? "none" : FormatAccess(remove));
        }

        private static string FormatParameters(IEnumerable<ParameterInfo> parameters)
        {
            return string.Join(",", parameters.Select(parameter =>
            {
                string direction = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : string.Empty;
                return direction + Stage2ContractFingerprint.NormalizeType(parameter.ParameterType) + " " + parameter.Name
                    + (parameter.IsOptional ? "=" + FormatValue(parameter.DefaultValue) : string.Empty);
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

        private static string FormatValue(object value)
        {
            if (value == null) return "null";
            if (ReferenceEquals(value, Missing.Value)) return "missing";
            if (value is string) return "string:" + (string)value;
            if (value is char) return "char:" + ((int)(char)value).ToString(CultureInfo.InvariantCulture);
            if (value is bool) return ((bool)value).ToString().ToLowerInvariant();
            if (value.GetType().IsEnum) return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static string FormatPublicKeyToken(byte[] token)
        {
            return token == null || token.Length == 0
                ? string.Empty
                : string.Concat(token.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AddLine(ICollection<string> lines, params string[] values)
        {
            lines.Add(string.Join("|", values.Select(Escape)));
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("|", "\\|")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string[] SplitEscaped(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool escaped = false;
            foreach (char character in line)
            {
                if (escaped)
                {
                    current.Append(character == 'n' ? '\n' : character == 'r' ? '\r' : character);
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '|')
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(character);
                }
            }

            if (escaped)
            {
                current.Append('\\');
            }

            values.Add(current.ToString());
            return values.ToArray();
        }

        private static IEnumerable<string> SplitLines(string value)
        {
            return NormalizeManifest(value).Split('\n').Where(line => line.Length > 0);
        }

        private static void WriteManifest(string manifestPath, string value)
        {
            string fullPath = Path.GetFullPath(manifestPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Manifest path has no parent directory.");
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, NormalizeManifest(value), new UTF8Encoding(false));
        }

        private static string ReadManifest(string manifestPath)
        {
            string fullPath = Path.GetFullPath(manifestPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Stage 5 contract manifest was not found.", fullPath);
            }

            return NormalizeManifest(File.ReadAllText(fullPath));
        }

        private static string NormalizeManifest(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n') + "\n";
        }

        private static void VerifyExact(string expected, string actual, string contractName)
        {
            expected = NormalizeManifest(expected);
            actual = NormalizeManifest(actual);
            if (string.Equals(expected, actual, StringComparison.Ordinal))
            {
                return;
            }

            string[] expectedLines = expected.Split('\n');
            string[] actualLines = actual.Split('\n');
            int count = Math.Max(expectedLines.Length, actualLines.Length);
            for (int index = 0; index < count; index++)
            {
                string expectedLine = index < expectedLines.Length ? expectedLines[index] : "<missing>";
                string actualLine = index < actualLines.Length ? actualLines[index] : "<missing>";
                if (!string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        contractName + " differs at line " + (index + 1).ToString(CultureInfo.InvariantCulture)
                        + ". Expected: " + expectedLine + ". Actual: " + actualLine + ".");
                }
            }

            throw new InvalidOperationException(contractName + " differs.");
        }
    }
}

internal static class Stage5ContractFingerprint
{
    internal static void WriteLegacy(string manifestPath, System.Reflection.Assembly chatEngineAssembly)
    {
        AORebirth.LinuxBuild.Contracts.Stage5ContractFingerprint.WriteLegacy(manifestPath, chatEngineAssembly);
    }

    internal static void VerifyLegacy(string manifestPath, System.Reflection.Assembly chatEngineAssembly)
    {
        AORebirth.LinuxBuild.Contracts.Stage5ContractFingerprint.VerifyLegacy(manifestPath, chatEngineAssembly);
    }

    internal static void VerifyLinux(string manifestPath, System.Reflection.Assembly chatEngineAssembly, System.Reflection.Assembly authenticationAssembly)
    {
        AORebirth.LinuxBuild.Contracts.Stage5ContractFingerprint.VerifyLinux(manifestPath, chatEngineAssembly, authenticationAssembly);
    }

    internal static void VerifyOffline(System.Reflection.Assembly chatEngineAssembly, System.Reflection.Assembly authenticationAssembly)
    {
        AORebirth.LinuxBuild.Contracts.Stage5ContractFingerprint.VerifyOffline(chatEngineAssembly, authenticationAssembly);
    }
}
