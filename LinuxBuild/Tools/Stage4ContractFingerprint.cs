using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using AORebirth.Communication;
using AORebirth.Communication.ISComV2Server;
using AORebirth.Communication.Messages;
using Cell.Core;
using MsgPack.Serialization;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace AORebirth.LinuxBuild
{
    internal static class Stage4ContractFingerprint
    {
        private const string ManifestName = "AORebirth.Stage4Contract";
        private const string ManifestVersion = "1";
        private const int ThreadCount = 4;
        private const int ThreadIterations = 4;

        internal static void WriteLegacy(string manifestPath, Assembly communicationAssembly)
        {
            WriteManifest(manifestPath, CreateLegacy(communicationAssembly));
        }

        internal static void VerifyLegacy(string manifestPath, Assembly communicationAssembly)
        {
            VerifyExact(ReadManifest(manifestPath), CreateLegacy(communicationAssembly), "Legacy Stage 4 contract");
        }

        internal static void VerifyLinux(string manifestPath, Assembly communicationAssembly)
        {
            string expectedLegacy = ReadManifest(manifestPath);
            string expectedSemantic = FilterLegacyReferences(expectedLegacy);
            string actualSemantic = CreateSemantic(communicationAssembly);
            VerifyExact(expectedSemantic, actualSemantic, "Stage 4 Communication semantic contract");
            VerifyMappedReferences(expectedLegacy, communicationAssembly);
        }

        private static string CreateLegacy(Assembly communicationAssembly)
        {
            var lines = SplitLines(CreateSemantic(communicationAssembly)).ToList();
            AddReferenceLines(lines, "legacy.reference", communicationAssembly);
            return string.Join("\n", lines) + "\n";
        }

        private static string CreateSemantic(Assembly communicationAssembly)
        {
            RequireCommunicationAssembly(communicationAssembly);

            var lines = new List<string>();
            AddLine(lines, "manifest", ManifestName, ManifestVersion);
            foreach (string apiLine in SplitLines(Stage2ContractFingerprint.Create(new[] { communicationAssembly })))
            {
                lines.Add("api|" + apiLine);
            }

            AddProtectedSurface(lines, communicationAssembly);
            AddLine(
                lines,
                "reference.mapping",
                "AORebirth.Communication",
                "MemBus",
                "legacy-package=2.0.2.0",
                "linux-shim=2.0.2.0",
                "scope=IBus-identity-and-construction-only",
                "behavior=inert-unobserved-bus");
            AddDefaultContracts(lines, communicationAssembly);

            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;
            IDictionary<string, byte[]> canonicalSnapshot;
            try
            {
                SetCurrentCulture(CultureInfo.InvariantCulture);
                FixtureSpec[] fixtures = CreateFixtures();
                canonicalSnapshot = AddSerializationContracts(lines, fixtures);
                AddRejectedSerializationContracts(lines);
                AddTypeResolutionContracts(lines);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
                Thread.CurrentThread.CurrentUICulture = originalUiCulture;
            }

            AddCultureStability(lines, canonicalSnapshot);
            AddThreadStability(lines, canonicalSnapshot);
            return string.Join("\n", lines) + "\n";
        }

        private static void RequireCommunicationAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            if (!string.Equals(assembly.GetName().Name, "AORebirth.Communication", StringComparison.Ordinal))
            {
                throw new ArgumentException("The Stage 4 fixture requires AORebirth.Communication.", nameof(assembly));
            }
        }

        private static void AddProtectedSurface(ICollection<string> lines, Assembly assembly)
        {
            foreach (Type type in GetExportedTypes(assembly).OrderBy(Stage2ContractFingerprint.NormalizeType, StringComparer.Ordinal))
            {
                var members = new List<string>();
                const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

                foreach (ConstructorInfo constructor in type.GetConstructors(Declared).Where(IsProtected))
                {
                    members.Add("constructor|" + FormatMethodBase(constructor));
                }

                foreach (FieldInfo field in type.GetFields(Declared).Where(IsProtected))
                {
                    members.Add(string.Join(
                        "|",
                        "field",
                        GetAccessibility(field),
                        field.IsStatic ? "static" : "instance",
                        Stage2ContractFingerprint.NormalizeType(field.FieldType),
                        field.Name,
                        "initonly=" + field.IsInitOnly.ToString().ToLowerInvariant(),
                        "literal=" + field.IsLiteral.ToString().ToLowerInvariant()));
                }

                foreach (PropertyInfo property in type.GetProperties(Declared).Where(HasProtectedAccessor))
                {
                    MethodInfo getter = property.GetGetMethod(true);
                    MethodInfo setter = property.GetSetMethod(true);
                    MethodInfo representative = getter ?? setter;
                    members.Add(string.Join(
                        "|",
                        "property",
                        representative != null && representative.IsStatic ? "static" : "instance",
                        Stage2ContractFingerprint.NormalizeType(property.PropertyType),
                        property.Name,
                        "index=" + FormatParameters(property.GetIndexParameters()),
                        "get=" + GetAccessibility(getter),
                        "set=" + GetAccessibility(setter)));
                }

                foreach (EventInfo eventInfo in type.GetEvents(Declared).Where(HasProtectedAccessor))
                {
                    MethodInfo addMethod = eventInfo.GetAddMethod(true);
                    MethodInfo removeMethod = eventInfo.GetRemoveMethod(true);
                    MethodInfo representative = addMethod ?? removeMethod;
                    members.Add(string.Join(
                        "|",
                        "event",
                        representative != null && representative.IsStatic ? "static" : "instance",
                        Stage2ContractFingerprint.NormalizeType(eventInfo.EventHandlerType),
                        eventInfo.Name,
                        "add=" + GetAccessibility(addMethod),
                        "remove=" + GetAccessibility(removeMethod)));
                }

                foreach (MethodInfo method in type.GetMethods(Declared).Where(IsProtected))
                {
                    members.Add("method|" + FormatMethod(method));
                }

                foreach (string member in members.OrderBy(value => value, StringComparer.Ordinal))
                {
                    AddLine(lines, "protected.member", Stage2ContractFingerprint.NormalizeType(type), member);
                }
            }
        }

        private static bool IsProtected(MethodBase method)
        {
            return method != null && (method.IsFamily || method.IsFamilyOrAssembly || method.IsFamilyAndAssembly);
        }

        private static bool IsProtected(FieldInfo field)
        {
            return field != null && (field.IsFamily || field.IsFamilyOrAssembly || field.IsFamilyAndAssembly);
        }

        private static bool HasProtectedAccessor(PropertyInfo property)
        {
            return IsProtected(property.GetGetMethod(true)) || IsProtected(property.GetSetMethod(true));
        }

        private static bool HasProtectedAccessor(EventInfo eventInfo)
        {
            return IsProtected(eventInfo.GetAddMethod(true)) || IsProtected(eventInfo.GetRemoveMethod(true));
        }

        private static string FormatMethod(MethodInfo method)
        {
            string genericArguments = method.IsGenericMethod
                ? "<" + string.Join(",", method.GetGenericArguments()
                    .OrderBy(argument => argument.GenericParameterPosition)
                    .Select(Stage2ContractFingerprint.NormalizeType)) + ">"
                : string.Empty;
            return string.Join(
                "|",
                GetAccessibility(method),
                method.IsStatic ? "static" : "instance",
                "abstract=" + method.IsAbstract.ToString().ToLowerInvariant(),
                "virtual=" + method.IsVirtual.ToString().ToLowerInvariant(),
                "final=" + method.IsFinal.ToString().ToLowerInvariant(),
                "special=" + method.IsSpecialName.ToString().ToLowerInvariant(),
                Stage2ContractFingerprint.NormalizeType(method.ReturnType),
                method.Name + genericArguments,
                FormatParameters(method.GetParameters()));
        }

        private static string FormatMethodBase(ConstructorInfo constructor)
        {
            return string.Join(
                "|",
                GetAccessibility(constructor),
                constructor.IsStatic ? "static" : "instance",
                ".ctor",
                FormatParameters(constructor.GetParameters()));
        }

        private static string FormatParameters(IEnumerable<ParameterInfo> parameters)
        {
            return string.Join(",", parameters.OrderBy(parameter => parameter.Position).Select(FormatParameter));
        }

        private static string FormatParameter(ParameterInfo parameter)
        {
            Type parameterType = parameter.ParameterType;
            string direction = string.Empty;
            if (parameterType.IsByRef)
            {
                parameterType = parameterType.GetElementType();
                direction = parameter.IsOut && !parameter.IsIn
                    ? "out "
                    : parameter.IsIn && !parameter.IsOut ? "in " : "ref ";
            }

            string optional = parameter.IsOptional || parameter.HasDefaultValue
                ? " optional=" + FormatConstant(parameter.DefaultValue)
                : string.Empty;
            return direction + Stage2ContractFingerprint.NormalizeType(parameterType) + " " + parameter.Name + optional;
        }

        private static string GetAccessibility(MethodBase method)
        {
            if (method == null)
            {
                return "none";
            }

            if (method.IsPublic) return "public";
            if (method.IsFamily) return "protected";
            if (method.IsFamilyOrAssembly) return "protected-internal";
            if (method.IsFamilyAndAssembly) return "private-protected";
            if (method.IsAssembly) return "internal";
            return "private";
        }

        private static string GetAccessibility(FieldInfo field)
        {
            if (field.IsPublic) return "public";
            if (field.IsFamily) return "protected";
            if (field.IsFamilyOrAssembly) return "protected-internal";
            if (field.IsFamilyAndAssembly) return "private-protected";
            if (field.IsAssembly) return "internal";
            return "private";
        }

        private static string FormatConstant(object value)
        {
            if (value == null) return "null";
            if (value == DBNull.Value) return "dbnull";
            if (value == Missing.Value) return "missing";
            if (value is string) return "string:" + FormatString((string)value);
            if (value is char) return "char:" + ((int)(char)value).ToString(CultureInfo.InvariantCulture);
            if (value is bool) return ((bool)value).ToString().ToLowerInvariant();
            if (value.GetType().IsEnum)
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void AddDefaultContracts(ICollection<string> lines, Assembly communicationAssembly)
        {
            AddDefault(lines, new ChatCommand());
            AddDefault(lines, new CpuRamLoad());
            AddDefault(lines, new DynamicMessage());
            AddDefault(lines, new MessageBase());
            AddDefault(lines, new OnlineCharacter());
            AddDefault(lines, new OnlineCharacters());
            AddDefault(lines, new OnDataReceivedArgs());
            AddDefault(lines, new OnMessageArgs());
            AddDefault(lines, new Ping());
            AddDefault(lines, new PrivateSystemMessage());
            AddDefault(lines, new RequestPlayfieldList());
            AddDefault(lines, new SystemChatMessage());
            AddDefault(lines, new VicinityChatMessage());
            AddLine(lines, "default.static", "AORebirth.Communication.ZoneCom.ClientReconnect", ZoneCom.ClientReconnect.ToString().ToLowerInvariant());

            Type serverType = communicationAssembly.GetType("AORebirth.Communication.ISComV2Server.ISComV2Server", true, false);
            object server = Activator.CreateInstance(serverType);
            FieldInfo clientNumberField = serverType.GetField("lastClientNumber", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo busField = serverType.GetField("bus", BindingFlags.NonPublic | BindingFlags.Instance);
            object bus = busField == null ? null : busField.GetValue(server);
            try
            {
                if (clientNumberField == null || Convert.ToInt32(clientNumberField.GetValue(server), CultureInfo.InvariantCulture) != 0)
                {
                    throw new InvalidDataException("ISComV2Server.lastClientNumber did not retain its zero construction default.");
                }

                if (busField == null
                    || !string.Equals(Stage2ContractFingerprint.NormalizeType(busField.FieldType), "MemBus.IBus", StringComparison.Ordinal)
                    || bus == null
                    || !busField.FieldType.IsInstanceOfType(bus))
                {
                    throw new InvalidDataException("ISComV2Server did not construct a non-null MemBus.IBus.");
                }

                FieldInfo runningField = FindInstanceField(serverType, "_running");
                FieldInfo tcpListenField = FindInstanceField(serverType, "_tcpListen");
                FieldInfo udpListenField = FindInstanceField(serverType, "_udpListen");
                PropertyInfo clientCountProperty = serverType.GetProperty("ClientCount", BindingFlags.Public | BindingFlags.Instance);
                if (runningField == null
                    || Convert.ToBoolean(runningField.GetValue(server), CultureInfo.InvariantCulture)
                    || tcpListenField == null
                    || tcpListenField.GetValue(server) != null
                    || udpListenField == null
                    || udpListenField.GetValue(server) != null
                    || clientCountProperty == null
                    || Convert.ToInt32(clientCountProperty.GetValue(server, null), CultureInfo.InvariantCulture) != 0)
                {
                    throw new InvalidDataException("ISComV2Server construction started or retained observable listener state.");
                }

                AddLine(
                    lines,
                    "default.runtime",
                    "AORebirth.Communication.ISComV2Server.ISComV2Server",
                    "lastClientNumber=0",
                    "bus=non-null-MemBus.IBus",
                    "running=false",
                    "tcp-listener=null",
                    "udp-listener=null",
                    "client-count=0");
            }
            finally
            {
                IDisposable serverDisposable = server as IDisposable;
                if (serverDisposable != null)
                {
                    serverDisposable.Dispose();
                }

                IDisposable busDisposable = bus as IDisposable;
                if (busDisposable != null)
                {
                    busDisposable.Dispose();
                }
            }
        }

        private static FieldInfo FindInstanceField(Type type, string name)
        {
            Type current = type;
            while (current != null)
            {
                FieldInfo field = current.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                current = current.BaseType;
            }

            return null;
        }

        private static void AddDefault(ICollection<string> lines, object value)
        {
            AddLine(lines, "default.instance", value.GetType().FullName, Canonicalize(value));
        }

        private static FixtureSpec[] CreateFixtures()
        {
            return new[]
            {
                new FixtureSpec("chat-command", delegate
                {
                    return new ChatCommand { CharacterId = 305419896, ChatCommandString = "say | hello\r\n\u03a9" };
                }),
                new FixtureSpec("cpu-ram-load", delegate
                {
                    return new CpuRamLoad { CpuLoad = 12.5f, RamLoad = -3.25f };
                }),
                new FixtureSpec("online-character", delegate
                {
                    return new OnlineCharacter { GMLevel = 7, Identity = 123456789, Name = "Zo\u00eb|Line\nTwo" };
                }),
                new FixtureSpec("on-data-received-args", delegate
                {
                    return new OnDataReceivedArgs { dataBytes = new byte[] { 0x00, 0x7c, 0xff, 0x0a } };
                }),
                new FixtureSpec("on-message-args", delegate
                {
                    return new OnMessageArgs
                    {
                        Data = new byte[] { 0x10, 0x00, 0xfe, 0x7c },
                        ID = 0xa5,
                        IsProtocolPacket = true,
                        Length = 0x1234
                    };
                }),
                new FixtureSpec("ping", delegate { return new Ping { dummy = "ping|\u03bc\n" }; }),
                new FixtureSpec("private-system-message", delegate
                {
                    return new PrivateSystemMessage
                    {
                        CharacterId = 101,
                        CharacterName = "Private \u03a9",
                        Text = "private|text\r\nline",
                        Unk1 = -17,
                        Unk2 = 2048
                    };
                }),
                new FixtureSpec("request-playfield-list", delegate
                {
                    return new RequestPlayfieldList
                    {
                        ZoneEngineAddress = "127.0.0.1:7500",
                        PlayfieldIds = new List<Identity>
                        {
                            new Identity { Type = (IdentityType)50000, Instance = 127 },
                            new Identity { Type = (IdentityType)50001, Instance = 0x12345678 }
                        }
                    };
                }),
                new FixtureSpec("system-chat-message", CreateSystemChatMessage),
                new FixtureSpec("vicinity-chat-message", delegate
                {
                    return new VicinityChatMessage
                    {
                        CharacterIds = new List<int> { 1, -2, 2147483647 },
                        MessageType = 42,
                        SenderId = 77,
                        Text = "vicinity|\u03bb\n"
                    };
                })
            };
        }

        private static object CreateSystemChatMessage()
        {
            return new SystemChatMessage
            {
                CharacterId = 202,
                CharacterName = "System \u03a9",
                Source = "Stage4|fixture",
                Text = "system text\r\nline",
                Unk1 = -19,
                Unk2 = 4096
            };
        }

        private static IDictionary<string, byte[]> AddSerializationContracts(ICollection<string> lines, FixtureSpec[] fixtures)
        {
            var snapshot = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (FixtureSpec fixture in fixtures.OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                object value = fixture.Create();
                byte[] bytes = SerializeTyped(value);
                object roundTrip = DeserializeTyped(value.GetType(), bytes);
                string semantic = Canonicalize(value);
                AssertSemantic(fixture.Name, semantic, Canonicalize(roundTrip));
                AddGolden(lines, "message.fixture", fixture.Name, value.GetType().FullName, bytes, semantic);
                snapshot.Add("message." + fixture.Name, bytes);

                MessageBase message = value as MessageBase;
                if (message == null)
                {
                    continue;
                }

                AssertBytes(fixture.Name + " MessageBase.GetData", bytes, message.GetData());
                var dynamicMessage = new DynamicMessage { DataObject = message };
                string dynamicSemantic = Canonicalize(dynamicMessage);

                byte[] typedDynamicBytes = SerializeDynamicTyped(dynamicMessage);
                DynamicMessage typedRoundTrip = DeserializeDynamicTyped(typedDynamicBytes);
                AssertSemantic(fixture.Name + " typed DynamicMessage", dynamicSemantic, Canonicalize(typedRoundTrip));
                AddGolden(lines, "dynamic.typed.fixture", fixture.Name, typeof(DynamicMessage).FullName, typedDynamicBytes, dynamicSemantic);
                snapshot.Add("dynamic.typed." + fixture.Name, typedDynamicBytes);

                byte[] objectDynamicBytes = SerializeDynamicObject(dynamicMessage);
                DynamicMessage wireRoundTrip = DeserializeDynamicTyped(objectDynamicBytes);
                AssertSemantic(fixture.Name + " object DynamicMessage", dynamicSemantic, Canonicalize(wireRoundTrip));
                AddGolden(lines, "dynamic.object.fixture", fixture.Name, typeof(DynamicMessage).FullName, objectDynamicBytes, dynamicSemantic);
                snapshot.Add("dynamic.object." + fixture.Name, objectDynamicBytes);

                byte[] frame = CaptureProductionFrame(dynamicMessage);
                VerifyFrame(frame, objectDynamicBytes);
                AddGolden(
                    lines,
                    "iscom.frame.fixture",
                    fixture.Name,
                    "ISComV2",
                    frame,
                    "magic=0x00ff55aa;payload-length=" + objectDynamicBytes.Length.ToString(CultureInfo.InvariantCulture)
                        + ";payload-sha256=" + ComputeSha256(objectDynamicBytes));
                snapshot.Add("frame." + fixture.Name, frame);
            }

            AddLine(
                lines,
                "serialization.coverage",
                "message-fixtures=" + fixtures.Length.ToString(CultureInfo.InvariantCulture),
                "dynamic-fixtures=" + fixtures.Count(value => typeof(MessageBase).IsAssignableFrom(value.RuntimeType)).ToString(CultureInfo.InvariantCulture),
                "wire-framing=ISComV2-production-send-path-little-endian");
            return snapshot;
        }

        private static void AddGolden(
            ICollection<string> lines,
            string kind,
            string name,
            string typeName,
            byte[] bytes,
            string semantic)
        {
            AddLine(
                lines,
                kind,
                name,
                typeName,
                "length=" + bytes.Length.ToString(CultureInfo.InvariantCulture),
                "sha256=" + ComputeSha256(bytes),
                "base64=" + Convert.ToBase64String(bytes),
                "semantic=" + semantic);
        }

        private static void AddRejectedSerializationContracts(ICollection<string> lines)
        {
            RequireNoSerializableMembers(lines, new MessageBase());
            RequireNoSerializableMembers(lines, new OnlineCharacters());
        }

        private static void RequireNoSerializableMembers(ICollection<string> lines, object value)
        {
            Exception rejection = null;
            try
            {
                SerializeTyped(value);
            }
            catch (Exception exception)
            {
                rejection = exception;
            }

            const string RequiredText = "does not have any serializable fields nor properties";
            if (rejection == null || rejection.Message.IndexOf(RequiredText, StringComparison.Ordinal) < 0)
            {
                throw new InvalidDataException(value.GetType().FullName + " no-member serialization behavior changed.", rejection);
            }

            AddLine(
                lines,
                "serialization.rejected",
                value.GetType().FullName,
                "reason=no-serializable-public-fields-or-properties",
                "message=" + rejection.Message);
        }

        private static byte[] SerializeTyped(object value)
        {
            IMessagePackSingleObjectSerializer serializer = MessagePackSerializer.Create(value.GetType());
            return serializer.PackSingleObject(value);
        }

        private static object DeserializeTyped(Type type, byte[] bytes)
        {
            IMessagePackSingleObjectSerializer serializer = MessagePackSerializer.Create(type);
            return serializer.UnpackSingleObject(bytes);
        }

        private static byte[] SerializeDynamicTyped(DynamicMessage value)
        {
            return MessagePackSerializer.Create<DynamicMessage>().PackSingleObject(value);
        }

        private static byte[] SerializeDynamicObject(DynamicMessage value)
        {
            return MessagePackSerializer.Create<object>().PackSingleObject(value);
        }

        private static DynamicMessage DeserializeDynamicTyped(byte[] bytes)
        {
            return MessagePackSerializer.Create<DynamicMessage>().UnpackSingleObject(bytes);
        }

        private static byte[] CaptureProductionFrame(DynamicMessage value)
        {
            var handler = new CapturingClientHandler();
            try
            {
                return handler.Capture(value);
            }
            finally
            {
                handler.Dispose();
            }
        }

        private static void VerifyFrame(byte[] frame, byte[] payload)
        {
            if (frame.Length != payload.Length + 8
                || frame[0] != 0xaa
                || frame[1] != 0x55
                || frame[2] != 0xff
                || frame[3] != 0x00
                || BitConverter.ToInt32(frame, 4) != payload.Length)
            {
                throw new InvalidDataException("ISComV2 frame header did not match the exact little-endian wire contract.");
            }

            for (int index = 0; index < payload.Length; index++)
            {
                if (frame[index + 8] != payload[index])
                {
                    throw new InvalidDataException("ISComV2 frame payload changed at offset " + index.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }

        private static void AddTypeResolutionContracts(ICollection<string> lines)
        {
            var exact = new DynamicMessage { DataObject = (MessageBase)CreateSystemChatMessage() };
            DynamicMessage exactRoundTrip = DeserializeDynamicTyped(SerializeDynamicTyped(exact));
            RequireResolvedType(exactRoundTrip, typeof(SystemChatMessage), "exact");
            AddLine(lines, "type-resolution", "exact", "requested=" + exact.TypeName, "resolved=" + exactRoundTrip.DataObject.GetType().FullName);

            var shortName = new DynamicMessage { DataObject = (MessageBase)CreateSystemChatMessage() };
            shortName.TypeName = "Legacy.Namespace.SystemChatMessage";
            DynamicMessage shortRoundTrip = DeserializeDynamicTyped(SerializeDynamicTyped(shortName));
            RequireResolvedType(shortRoundTrip, typeof(SystemChatMessage), "short-name");
            AddLine(lines, "type-resolution", "short-name", "requested=" + shortName.TypeName, "resolved=" + shortRoundTrip.DataObject.GetType().FullName);

            const string UnknownTypeName = "Legacy.Namespace.NotACommunicationMessage";
            const string ExpectedMessage = "ISCom DynamicMessage unknown type: " + UnknownTypeName;
            var unknown = new DynamicMessage { DataObject = (MessageBase)CreateSystemChatMessage() };
            unknown.TypeName = UnknownTypeName;
            try
            {
                DeserializeDynamicTyped(SerializeDynamicTyped(unknown));
                throw new InvalidDataException("DynamicMessage accepted the fixed unknown Stage 4 type fixture.");
            }
            catch (Exception exception)
            {
                InvalidOperationException resolutionError = FindResolutionError(exception);
                if (resolutionError == null || !string.Equals(resolutionError.Message, ExpectedMessage, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("DynamicMessage unknown-type behavior changed.", exception);
                }
            }

            AddLine(lines, "type-resolution", "error", "requested=" + UnknownTypeName, "exception=System.InvalidOperationException", "message=" + ExpectedMessage);
        }

        private static InvalidOperationException FindResolutionError(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                InvalidOperationException candidate = current as InvalidOperationException;
                if (candidate != null && candidate.Message.StartsWith("ISCom DynamicMessage unknown type: ", StringComparison.Ordinal))
                {
                    return candidate;
                }

                current = current.InnerException;
            }

            return null;
        }

        private static void RequireResolvedType(DynamicMessage message, Type expectedType, string contract)
        {
            if (message == null || message.DataObject == null || message.DataObject.GetType() != expectedType)
            {
                throw new InvalidDataException("DynamicMessage " + contract + " type resolution changed.");
            }
        }

        private static void AddCultureStability(ICollection<string> lines, IDictionary<string, byte[]> expected)
        {
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;
            string[] names = { "invariant", "en-US", "tr-TR" };
            try
            {
                foreach (string name in names)
                {
                    CultureInfo culture = string.Equals(name, "invariant", StringComparison.Ordinal)
                        ? CultureInfo.InvariantCulture
                        : CultureInfo.GetCultureInfo(name);
                    SetCurrentCulture(culture);
                    AssertSnapshot("culture " + name, expected, CreateByteSnapshot());
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
                Thread.CurrentThread.CurrentUICulture = originalUiCulture;
            }

            AddLine(lines, "serializer.culture-stability", "cultures=invariant,en-US,tr-TR", "fixtures=" + expected.Count.ToString(CultureInfo.InvariantCulture), "stable=true");
        }

        private static void AddThreadStability(ICollection<string> lines, IDictionary<string, byte[]> expected)
        {
            var failures = new List<Exception>();
            var start = new ManualResetEvent(false);
            var threads = new Thread[ThreadCount];
            for (int threadIndex = 0; threadIndex < threads.Length; threadIndex++)
            {
                int capturedIndex = threadIndex;
                threads[threadIndex] = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        SetCurrentCulture(CultureInfo.InvariantCulture);
                        start.WaitOne();
                        for (int iteration = 0; iteration < ThreadIterations; iteration++)
                        {
                            AssertSnapshot(
                                "thread " + capturedIndex.ToString(CultureInfo.InvariantCulture)
                                    + " iteration " + iteration.ToString(CultureInfo.InvariantCulture),
                                expected,
                                CreateByteSnapshot());
                        }
                    }
                    catch (Exception exception)
                    {
                        lock (failures)
                        {
                            failures.Add(exception);
                        }
                    }
                }));
                threads[threadIndex].IsBackground = true;
                threads[threadIndex].Start();
            }

            start.Set();
            foreach (Thread thread in threads)
            {
                thread.Join();
            }
            start.Dispose();

            if (failures.Count != 0)
            {
                throw new InvalidDataException("Concurrent MessagePack serialization changed: " + failures[0].Message, failures[0]);
            }

            AddLine(
                lines,
                "serializer.thread-stability",
                "threads=" + ThreadCount.ToString(CultureInfo.InvariantCulture),
                "iterations-per-thread=" + ThreadIterations.ToString(CultureInfo.InvariantCulture),
                "fixtures=" + expected.Count.ToString(CultureInfo.InvariantCulture),
                "stable=true");
        }

        private static IDictionary<string, byte[]> CreateByteSnapshot()
        {
            var snapshot = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (FixtureSpec fixture in CreateFixtures().OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                object value = fixture.Create();
                snapshot.Add("message." + fixture.Name, SerializeTyped(value));
                MessageBase message = value as MessageBase;
                if (message == null)
                {
                    continue;
                }

                var dynamicMessage = new DynamicMessage { DataObject = message };
                byte[] objectBytes = SerializeDynamicObject(dynamicMessage);
                snapshot.Add("dynamic.typed." + fixture.Name, SerializeDynamicTyped(dynamicMessage));
                snapshot.Add("dynamic.object." + fixture.Name, objectBytes);
                snapshot.Add("frame." + fixture.Name, CaptureProductionFrame(dynamicMessage));
            }

            return snapshot;
        }

        private static void AssertSnapshot(string contract, IDictionary<string, byte[]> expected, IDictionary<string, byte[]> actual)
        {
            string[] expectedKeys = expected.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] actualKeys = actual.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!expectedKeys.SequenceEqual(actualKeys, StringComparer.Ordinal))
            {
                throw new InvalidDataException(contract + " fixture set changed.");
            }

            foreach (string key in expectedKeys)
            {
                AssertBytes(contract + " " + key, expected[key], actual[key]);
            }
        }

        private static void SetCurrentCulture(CultureInfo culture)
        {
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        private static void AssertSemantic(string contract, string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidDataException(contract + " semantic roundtrip changed. Expected " + expected + "; actual " + actual + ".");
            }
        }

        private static void AssertBytes(string contract, byte[] expected, byte[] actual)
        {
            if (expected == null || actual == null || !expected.SequenceEqual(actual))
            {
                throw new InvalidDataException(contract + " bytes changed.");
            }
        }

        private static string Canonicalize(object value)
        {
            if (value == null) return "null";
            Type type = value.GetType();
            if (type == typeof(ChatCommand))
            {
                var item = (ChatCommand)value;
                return "CharacterId=" + item.CharacterId.ToString(CultureInfo.InvariantCulture) + ";ChatCommandString=" + FormatString(item.ChatCommandString);
            }
            if (type == typeof(CpuRamLoad))
            {
                var item = (CpuRamLoad)value;
                return "CpuLoad=" + FormatSingle(item.CpuLoad) + ";RamLoad=" + FormatSingle(item.RamLoad);
            }
            if (type == typeof(DynamicMessage))
            {
                var item = (DynamicMessage)value;
                return "TypeName=" + FormatString(item.TypeName) + ";DataObject=" + Canonicalize(item.DataObject);
            }
            if (type == typeof(MessageBase)) return "{}";
            if (type == typeof(OnlineCharacter))
            {
                var item = (OnlineCharacter)value;
                return "GMLevel=" + item.GMLevel.ToString(CultureInfo.InvariantCulture)
                    + ";Identity=" + item.Identity.ToString(CultureInfo.InvariantCulture)
                    + ";Name=" + FormatString(item.Name);
            }
            if (type == typeof(OnlineCharacters)) return "{}";
            if (type == typeof(OnDataReceivedArgs))
            {
                return "dataBytes=" + FormatBytes(((OnDataReceivedArgs)value).dataBytes);
            }
            if (type == typeof(OnMessageArgs))
            {
                var item = (OnMessageArgs)value;
                return "Data=" + FormatBytes(item.Data)
                    + ";ID=" + item.ID.ToString(CultureInfo.InvariantCulture)
                    + ";IsProtocolPacket=" + item.IsProtocolPacket.ToString().ToLowerInvariant()
                    + ";Length=" + item.Length.ToString(CultureInfo.InvariantCulture);
            }
            if (type == typeof(Ping)) return "dummy=" + FormatString(((Ping)value).dummy);
            if (type == typeof(PrivateSystemMessage))
            {
                var item = (PrivateSystemMessage)value;
                return "CharacterId=" + item.CharacterId.ToString(CultureInfo.InvariantCulture)
                    + ";CharacterName=" + FormatString(item.CharacterName)
                    + ";Text=" + FormatString(item.Text)
                    + ";Unk1=" + item.Unk1.ToString(CultureInfo.InvariantCulture)
                    + ";Unk2=" + item.Unk2.ToString(CultureInfo.InvariantCulture);
            }
            if (type == typeof(RequestPlayfieldList))
            {
                var item = (RequestPlayfieldList)value;
                return "PlayfieldIds=" + FormatIdentityList(item.PlayfieldIds)
                    + ";ZoneEngineAddress=" + FormatString(item.ZoneEngineAddress);
            }
            if (type == typeof(SystemChatMessage))
            {
                var item = (SystemChatMessage)value;
                return "CharacterId=" + item.CharacterId.ToString(CultureInfo.InvariantCulture)
                    + ";CharacterName=" + FormatString(item.CharacterName)
                    + ";Source=" + FormatString(item.Source)
                    + ";Text=" + FormatString(item.Text)
                    + ";Unk1=" + item.Unk1.ToString(CultureInfo.InvariantCulture)
                    + ";Unk2=" + item.Unk2.ToString(CultureInfo.InvariantCulture);
            }
            if (type == typeof(VicinityChatMessage))
            {
                var item = (VicinityChatMessage)value;
                return "CharacterIds=" + FormatIntList(item.CharacterIds)
                    + ";MessageType=" + item.MessageType.ToString(CultureInfo.InvariantCulture)
                    + ";SenderId=" + item.SenderId.ToString(CultureInfo.InvariantCulture)
                    + ";Text=" + FormatString(item.Text);
            }

            throw new InvalidDataException("No safe Stage 4 semantic formatter exists for " + type.FullName + ".");
        }

        private static string FormatString(string value)
        {
            return value == null ? "null" : "utf8:" + Convert.ToBase64String(new UTF8Encoding(false).GetBytes(value));
        }

        private static string FormatBytes(byte[] value)
        {
            return value == null ? "null" : "base64:" + Convert.ToBase64String(value);
        }

        private static string FormatSingle(float value)
        {
            uint bits = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
            return "0x" + bits.ToString("x8", CultureInfo.InvariantCulture);
        }

        private static string FormatIntList(IEnumerable<int> values)
        {
            return values == null
                ? "null"
                : "[" + string.Join(",", values.Select(value => value.ToString(CultureInfo.InvariantCulture))) + "]";
        }

        private static string FormatIdentityList(IEnumerable<Identity> values)
        {
            return values == null
                ? "null"
                : "[" + string.Join(",", values.Select(value =>
                    ((int)value.Type).ToString(CultureInfo.InvariantCulture)
                    + ":" + value.Instance.ToString(CultureInfo.InvariantCulture))) + "]";
        }

        private static void AddReferenceLines(ICollection<string> lines, string prefix, Assembly assembly)
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies().OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                AddLine(
                    lines,
                    prefix,
                    assembly.GetName().Name,
                    reference.Name,
                    reference.Version == null ? string.Empty : reference.Version.ToString(),
                    string.IsNullOrEmpty(reference.CultureName) ? "neutral" : reference.CultureName,
                    FormatPublicKeyToken(reference.GetPublicKeyToken()));
            }
        }

        private static void VerifyMappedReferences(string expectedLegacyManifest, Assembly actualAssembly)
        {
            string[] expected = SplitLines(expectedLegacyManifest)
                .Where(line => line.StartsWith("legacy.reference|", StringComparison.Ordinal))
                .Select(line => line.Substring("legacy.reference|".Length))
                .Where(line => !IsFrameworkReference(GetReferenceName(line)))
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToArray();

            var actualLines = new List<string>();
            AddReferenceLines(actualLines, "actual.reference", actualAssembly);
            string[] actual = actualLines
                .Select(line => line.Substring("actual.reference|".Length))
                .Where(line => !IsFrameworkReference(GetReferenceName(line)))
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToArray();

            RequireMappedMemBusIdentity(expected, "legacy");
            RequireMappedMemBusIdentity(actual, "linux");
            VerifyExact(string.Join("\n", expected) + "\n", string.Join("\n", actual) + "\n", "Stage 4 direct non-framework references");
        }

        private static void RequireMappedMemBusIdentity(IEnumerable<string> references, string runtime)
        {
            string[] matches = references.Where(line => string.Equals(GetReferenceName(line), "MemBus", StringComparison.Ordinal)).ToArray();
            const string Expected = "AORebirth.Communication|MemBus|2.0.2.0|neutral|null";
            if (matches.Length != 1 || !string.Equals(matches[0], Expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The " + runtime + " MemBus mapping must retain exact identity " + Expected + ".");
            }
        }

        private static string GetReferenceName(string referenceLine)
        {
            string[] components = referenceLine.Split('|');
            return components.Length > 1 ? components[1] : string.Empty;
        }

        private static bool IsFrameworkReference(string name)
        {
            return string.Equals(name, "mscorlib", StringComparison.Ordinal)
                || string.Equals(name, "netstandard", StringComparison.Ordinal)
                || string.Equals(name, "Microsoft.CSharp", StringComparison.Ordinal)
                || string.Equals(name, "System", StringComparison.Ordinal)
                || name.StartsWith("System.", StringComparison.Ordinal);
        }

        private static Type[] GetExportedTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                string messages = string.Join(
                    "; ",
                    exception.LoaderExceptions.Where(loader => loader != null).Select(loader => loader.Message));
                throw new InvalidOperationException("Could not load Communication exported types: " + messages, exception);
            }
        }

        private static string ComputeSha256(byte[] value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(value).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static string FormatPublicKeyToken(byte[] token)
        {
            return token == null || token.Length == 0
                ? "null"
                : string.Concat(token.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void AddLine(ICollection<string> lines, params string[] values)
        {
            lines.Add(string.Join("|", values.Select(value => Escape(value ?? string.Empty))));
        }

        private static string Escape(string value)
        {
            return value.Replace("%", "%25").Replace("|", "%7C").Replace("\r", "%0D").Replace("\n", "%0A");
        }

        private static IEnumerable<string> SplitLines(string value)
        {
            return NormalizeManifest(value).Split('\n').Where(line => line.Length > 0);
        }

        private static string FilterLegacyReferences(string manifest)
        {
            return string.Join(
                "\n",
                SplitLines(manifest).Where(line => !line.StartsWith("legacy.reference|", StringComparison.Ordinal))) + "\n";
        }

        private static void WriteManifest(string manifestPath, string value)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new ArgumentException("A manifest path is required.", nameof(manifestPath));
            }

            string fullPath = Path.GetFullPath(manifestPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, NormalizeManifest(value), new UTF8Encoding(false));
        }

        private static string ReadManifest(string manifestPath)
        {
            string fullPath = Path.GetFullPath(manifestPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Stage 4 contract manifest was not found.", fullPath);
            }

            return NormalizeManifest(File.ReadAllText(fullPath));
        }

        private static string NormalizeManifest(string value)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n') + "\n";
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
            int commonLength = Math.Min(expectedLines.Length, actualLines.Length);
            for (int index = 0; index < commonLength; index++)
            {
                if (!string.Equals(expectedLines[index], actualLines[index], StringComparison.Ordinal))
                {
                    throw new InvalidDataException(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} mismatch at line {1}. Expected: {2} Actual: {3}",
                        contractName,
                        index + 1,
                        expectedLines[index],
                        actualLines[index]));
                }
            }

            throw new InvalidDataException(string.Format(
                CultureInfo.InvariantCulture,
                "{0} line count changed. Expected {1}; actual {2}.",
                contractName,
                expectedLines.Length - 1,
                actualLines.Length - 1));
        }

        private sealed class FixtureSpec
        {
            private readonly Func<object> factory;

            internal FixtureSpec(string name, Func<object> factory)
            {
                Name = name;
                this.factory = factory;
                RuntimeType = factory().GetType();
            }

            internal string Name { get; private set; }

            internal Type RuntimeType { get; private set; }

            internal object Create()
            {
                return factory();
            }
        }

        private sealed class CapturingClientHandler : ISComV2ClientHandler
        {
            private readonly List<byte[]> writes = new List<byte[]>();

            internal CapturingClientHandler()
                : base((ServerBase)null)
            {
            }

            public override void Send(byte[] packet, int offset, int length)
            {
                var copy = new byte[length];
                Array.Copy(packet, offset, copy, 0, length);
                writes.Add(copy);
            }

            internal byte[] Capture(DynamicMessage value)
            {
                writes.Clear();
                base.Send(value);
                if (writes.Count != 2 || writes[0].Length != 8)
                {
                    throw new InvalidDataException("ISComV2ClientHandler.Send no longer emitted one header and one payload write.");
                }

                byte[] frame = new byte[writes[0].Length + writes[1].Length];
                writes[0].CopyTo(frame, 0);
                writes[1].CopyTo(frame, writes[0].Length);
                return frame;
            }
        }
    }
}
