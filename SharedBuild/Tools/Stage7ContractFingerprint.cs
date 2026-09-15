namespace AORebirth.SharedBuild.Contracts
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

        private static string Create(Assembly loginEngineAssembly, Assembly coreAssembly)
        {
            AssertAssemblyName(loginEngineAssembly, "LoginEngine");
            AssertAssemblyName(coreAssembly, "AORebirth.Core");

            var lines = new List<string>();
            AddLine(lines, "manifest", ManifestName, ManifestVersion);
            foreach (string line in SplitLines(AORebirth.SharedBuild.Stage2ContractFingerprint.Create(new[] { loginEngineAssembly })))
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
            foreach (Type type in GetExportedTypes(assembly).OrderBy(AORebirth.SharedBuild.Stage2ContractFingerprint.NormalizeType, StringComparer.Ordinal))
            {
                var contracts = new List<string>();
                contracts.AddRange(type.GetConstructors(flags).Where(IsProtected).Select(FormatConstructor));
                contracts.AddRange(type.GetMethods(flags).Where(IsProtected).Select(FormatMethod));
                contracts.AddRange(type.GetFields(flags).Where(IsProtected).Select(FormatField));
                contracts.AddRange(type.GetProperties(flags).Where(IsProtected).Select(FormatProperty));
                contracts.AddRange(type.GetEvents(flags).Where(IsProtected).Select(FormatEvent));
                foreach (string contract in contracts.OrderBy(value => value, StringComparer.Ordinal))
                {
                    AddLine(lines, "protected", AORebirth.SharedBuild.Stage2ContractFingerprint.NormalizeType(type), contract);
                }
            }
        }

        private static void AddContainedCoreContracts(ICollection<string> lines, Assembly coreAssembly)
        {
            string full = AORebirth.SharedBuild.Stage2ContractFingerprint.Create(new[] { coreAssembly });
            var expected = new HashSet<string>(
                ContainedCoreTypeNames
                    .Select(name => GetRequiredType(coreAssembly, name, true))
                    .Select(AORebirth.SharedBuild.Stage2ContractFingerprint.NormalizeType),
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
                + AORebirth.SharedBuild.Stage2ContractFingerprint.NormalizeType(method.ReturnType) + " " + method.Name + generic + "(" + FormatParameters(method.GetParameters()) + ")"
                + " virtual=" + method.IsVirtual.ToString().ToLowerInvariant()
                + " abstract=" + method.IsAbstract.ToString().ToLowerInvariant()
                + " final=" + method.IsFinal.ToString().ToLowerInvariant();
        }

        private static string FormatField(FieldInfo field)
        {
            return "field " + FormatAccess(field) + " " + (field.IsStatic ? "static" : "instance") + " "
                + AORebirth.SharedBuild.Stage2ContractFingerprint.NormalizeType(field.FieldType) + " " + field.Name
                + " initonly=" + field.IsInitOnly.ToString().ToLowerInvariant()
                + " literal=" + field.IsLiteral.ToString().ToLowerInvariant();
        }

        private static string FormatProperty(PropertyInfo property)
        {
            MethodInfo getter = property.GetGetMethod(true);
            MethodInfo setter = property.GetSetMethod(true);
            string access = getter != null ? FormatAccess(getter) : setter != null ? FormatAccess(setter) : "none";
            return "property " + access + " " + AORebirth.SharedBuild.Stage2ContractFingerprint.NormalizeType(property.PropertyType) + " " + property.Name
                + "(" + FormatParameters(property.GetIndexParameters()) + ")"
                + " get=" + (getter == null ? "none" : FormatAccess(getter))
                + " set=" + (setter == null ? "none" : FormatAccess(setter));
        }

        private static string FormatEvent(EventInfo eventInfo)
        {
            MethodInfo add = eventInfo.GetAddMethod(true);
            MethodInfo remove = eventInfo.GetRemoveMethod(true);
            return "event " + AORebirth.SharedBuild.Stage2ContractFingerprint.NormalizeType(eventInfo.EventHandlerType) + " " + eventInfo.Name
                + " add=" + (add == null ? "none" : FormatAccess(add))
                + " remove=" + (remove == null ? "none" : FormatAccess(remove));
        }

        private static string FormatParameters(IEnumerable<ParameterInfo> parameters)
        {
            return string.Join(",", parameters.Select(parameter =>
            {
                string direction = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : string.Empty;
                return direction + AORebirth.SharedBuild.Stage2ContractFingerprint.NormalizeType(parameter.ParameterType) + " " + parameter.Name;
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
        AORebirth.SharedBuild.Contracts.Stage7ContractFingerprint.WriteLegacy(manifestPath, loginEngineAssembly, coreAssembly);
    }

    internal static void VerifyLegacy(string manifestPath, System.Reflection.Assembly loginEngineAssembly, System.Reflection.Assembly coreAssembly)
    {
        AORebirth.SharedBuild.Contracts.Stage7ContractFingerprint.VerifyLegacy(manifestPath, loginEngineAssembly, coreAssembly);
    }
}
