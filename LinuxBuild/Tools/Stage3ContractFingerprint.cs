using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace AORebirth.LinuxBuild
{
    internal static class Stage3ContractFingerprint
    {
        private const string ManifestName = "AORebirth.Stage3Contracts";
        private const string ManifestVersion = "1";
        private const string TablenameAttributeName = "AORebirth.Database.Dao.TablenameAttribute";
        private const string ForeignKeyAttributeName = "AORebirth.Database.Entities.ForeignKeyAttribute";

        private static readonly string[] LinuxPinnedReferenceIdentities =
        {
            "AORebirth.Database|Dapper|2.0.0.0|neutral|null",
            "AORebirth.Database|Microsoft.Data.SqlClient|7.0.0.0|neutral|23ec7fc2d6eaa4a5",
            "AORebirth.Database|MySqlConnector|2.0.0.0|neutral|d33d3e53aa5f8c92",
            "AORebirth.Database|Npgsql|10.0.3.0|neutral|5d8b90d52f46fda7"
        };

        internal static void WriteLegacy(string manifestPath, Assembly databaseAssembly, Assembly statsAssembly)
        {
            WriteManifest(manifestPath, CreateLegacy(databaseAssembly, statsAssembly));
        }

        internal static void VerifyLegacy(string manifestPath, Assembly databaseAssembly, Assembly statsAssembly)
        {
            string expected = ReadManifest(manifestPath);
            string actual = NormalizeManifest(CreateLegacy(databaseAssembly, statsAssembly));
            VerifyExact(expected, actual, "Legacy Stage 3 contract");
        }

        internal static void VerifyLinux(string manifestPath, Assembly databaseAssembly, Assembly statsAssembly)
        {
            string expectedLegacy = ReadManifest(manifestPath);
            string expectedSemantic = FilterLegacyReferences(expectedLegacy);
            string actualSemantic = NormalizeManifest(CreateSemantic(databaseAssembly, statsAssembly));
            VerifyExact(expectedSemantic, actualSemantic, "Linux Stage 3 semantic contract");
            VerifyMappedReferences(expectedLegacy, databaseAssembly, statsAssembly);
        }

        private static string CreateLegacy(Assembly databaseAssembly, Assembly statsAssembly)
        {
            var lines = SplitLines(CreateSemantic(databaseAssembly, statsAssembly)).ToList();
            AddReferenceLines(lines, "legacy.reference", databaseAssembly, statsAssembly);
            return string.Join("\n", lines) + "\n";
        }

        private static string CreateSemantic(Assembly databaseAssembly, Assembly statsAssembly)
        {
            ValidateAssemblies(databaseAssembly, statsAssembly);
            var lines = new List<string>();
            AddLine(lines, "manifest", ManifestName, ManifestVersion);

            foreach (string contractLine in GetNormalizedApiContractLines(
                Stage2ContractFingerprint.Create(new[] { databaseAssembly, statsAssembly })))
            {
                lines.Add("api|" + contractLine);
            }

            AddDatabaseAttributes(lines, databaseAssembly);
            AddRuntimeArray(lines, statsAssembly, "AORebirth.Stats.SkillTrickleTable", "table");
            AddRuntimeArray(lines, statsAssembly, "AORebirth.Stats.SpecialStats.XPTable", "TableAlienXP");
            AddRuntimeArray(lines, statsAssembly, "AORebirth.Stats.SpecialStats.XPTable", "TableRKXP");
            AddRuntimeArray(lines, statsAssembly, "AORebirth.Stats.SpecialStats.XPTable", "TableShadowLandsSK");
            AddStatsTopology(lines, statsAssembly);
            return string.Join("\n", lines) + "\n";
        }

        private static void ValidateAssemblies(Assembly databaseAssembly, Assembly statsAssembly)
        {
            if (databaseAssembly == null)
            {
                throw new ArgumentNullException(nameof(databaseAssembly));
            }

            if (statsAssembly == null)
            {
                throw new ArgumentNullException(nameof(statsAssembly));
            }

            if (!string.Equals(databaseAssembly.GetName().Name, "AORebirth.Database", StringComparison.Ordinal))
            {
                throw new ArgumentException("Expected AORebirth.Database.", nameof(databaseAssembly));
            }

            if (!string.Equals(statsAssembly.GetName().Name, "AORebirth.Stats", StringComparison.Ordinal))
            {
                throw new ArgumentException("Expected AORebirth.Stats.", nameof(statsAssembly));
            }
        }

        private static void AddDatabaseAttributes(ICollection<string> lines, Assembly databaseAssembly)
        {
            var records = new List<string>();
            foreach (Type type in GetAssemblyTypes(databaseAssembly).OrderBy(Stage2ContractFingerprint.NormalizeType, StringComparer.Ordinal))
            {
                AddAttributeRecords(records, CustomAttributeData.GetCustomAttributes(type), "type", Stage2ContractFingerprint.NormalizeType(type));

                const BindingFlags DeclaredMembers = BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (PropertyInfo property in type.GetProperties(DeclaredMembers).OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    AddAttributeRecords(
                        records,
                        CustomAttributeData.GetCustomAttributes(property),
                        "property",
                        Stage2ContractFingerprint.NormalizeType(type) + "." + property.Name);
                }
            }

            foreach (string record in records.OrderBy(value => value, StringComparer.Ordinal))
            {
                lines.Add(record);
            }
        }

        private static void AddAttributeRecords(
            ICollection<string> records,
            IEnumerable<CustomAttributeData> attributes,
            string targetKind,
            string targetName)
        {
            foreach (CustomAttributeData attribute in attributes.Where(IsDatabaseContractAttribute))
            {
                string constructorArguments = string.Join(
                    ",",
                    attribute.ConstructorArguments.Select(FormatAttributeArgument));
                string namedArguments = string.Join(
                    ",",
                    attribute.NamedArguments
                        .OrderBy(argument => argument.MemberName, StringComparer.Ordinal)
                        .Select(argument => argument.MemberName + "=" + FormatAttributeArgument(argument.TypedValue)));
                AddLine(
                    records,
                    "database.attribute",
                    attribute.AttributeType.FullName,
                    targetKind,
                    targetName,
                    "constructor=" + constructorArguments,
                    "named=" + namedArguments);
            }
        }

        private static bool IsDatabaseContractAttribute(CustomAttributeData attribute)
        {
            string name = attribute.AttributeType.FullName;
            return string.Equals(name, TablenameAttributeName, StringComparison.Ordinal)
                || string.Equals(name, ForeignKeyAttributeName, StringComparison.Ordinal);
        }

        private static string FormatAttributeArgument(CustomAttributeTypedArgument argument)
        {
            if (argument.Value == null)
            {
                return Stage2ContractFingerprint.NormalizeType(argument.ArgumentType) + ":null";
            }

            IList<CustomAttributeTypedArgument> array = argument.Value as IList<CustomAttributeTypedArgument>;
            if (array != null)
            {
                return Stage2ContractFingerprint.NormalizeType(argument.ArgumentType)
                    + ":[" + string.Join(",", array.Select(FormatAttributeArgument)) + "]";
            }

            Type typeValue = argument.Value as Type;
            if (typeValue != null)
            {
                return "System.Type:" + Stage2ContractFingerprint.NormalizeType(typeValue);
            }

            return Stage2ContractFingerprint.NormalizeType(argument.ArgumentType)
                + ":" + Convert.ToString(argument.Value, CultureInfo.InvariantCulture);
        }

        private static void AddRuntimeArray(
            ICollection<string> lines,
            Assembly statsAssembly,
            string typeName,
            string fieldName)
        {
            Type type = statsAssembly.GetType(typeName, true, false);
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                throw new MissingFieldException(typeName, fieldName);
            }

            Array array = field.GetValue(null) as Array;
            if (array == null || array.Rank != 2)
            {
                throw new InvalidDataException(typeName + "." + fieldName + " must be a two-dimensional runtime array.");
            }

            string identity = typeName + "." + fieldName;
            var canonicalRows = new List<string>();
            for (int row = 0; row < array.GetLength(0); row++)
            {
                var values = new string[array.GetLength(1)];
                for (int column = 0; column < array.GetLength(1); column++)
                {
                    values[column] = FormatArrayValue(array.GetValue(row, column), field.FieldType.GetElementType());
                }

                canonicalRows.Add(string.Join(",", values));
            }

            string canonical = Stage2ContractFingerprint.NormalizeType(field.FieldType.GetElementType())
                + "|" + array.GetLength(0).ToString(CultureInfo.InvariantCulture)
                + "|" + array.GetLength(1).ToString(CultureInfo.InvariantCulture)
                + "|" + string.Join("\n", canonicalRows);
            AddLine(
                lines,
                "runtime.array",
                identity,
                "element=" + Stage2ContractFingerprint.NormalizeType(field.FieldType.GetElementType()),
                "lengths=" + array.GetLength(0).ToString(CultureInfo.InvariantCulture)
                    + "," + array.GetLength(1).ToString(CultureInfo.InvariantCulture),
                "sha256=" + ComputeSha256(canonical));
            for (int row = 0; row < canonicalRows.Count; row++)
            {
                AddLine(
                    lines,
                    "runtime.array-row",
                    identity,
                    "row=" + row.ToString(CultureInfo.InvariantCulture),
                    canonicalRows[row]);
            }
        }

        private static string FormatArrayValue(object value, Type elementType)
        {
            if (elementType == typeof(double))
            {
                long bits = BitConverter.DoubleToInt64Bits((double)value);
                return "0x" + unchecked((ulong)bits).ToString("x16", CultureInfo.InvariantCulture);
            }

            if (elementType == typeof(float))
            {
                byte[] bytes = BitConverter.GetBytes((float)value);
                uint bits = BitConverter.ToUInt32(bytes, 0);
                return "0x" + bits.ToString("x8", CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void AddStatsTopology(ICollection<string> lines, Assembly statsAssembly)
        {
            Type statsType = statsAssembly.GetType("AORebirth.Stats.Stats", true, false);
            Type statInterface = statsAssembly.GetType("AORebirth.Stats.IStat", true, false);
            ConstructorInfo constructor = statsType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.GetParameters().Length == 1);
            Type ownerType = constructor.GetParameters()[0].ParameterType;
            object owner = Activator.CreateInstance(ownerType);
            object stats;
            try
            {
                stats = constructor.Invoke(new[] { owner });
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(
                    "Safe Stats construction failed: " + (exception.InnerException == null ? exception.Message : exception.InnerException.Message),
                    exception);
            }

            FieldInfo allField = statsType.GetField("all", BindingFlags.NonPublic | BindingFlags.Instance);
            IEnumerable allEnumerable = allField == null ? null : allField.GetValue(stats) as IEnumerable;
            if (allEnumerable == null)
            {
                throw new InvalidDataException("Stats.all topology was unavailable.");
            }

            var all = allEnumerable.Cast<object>().ToList();
            var topologyLines = new List<string>();
            AddLine(
                topologyLines,
                "runtime.stats.summary",
                "owner=" + Stage2ContractFingerprint.NormalizeType(ownerType),
                "count=" + all.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < all.Count; index++)
            {
                object stat = all[index];
                int statId = GetStatId(stat);
                AddLine(
                    topologyLines,
                    "runtime.stats.item",
                    "index=" + index.ToString(CultureInfo.InvariantCulture),
                    "id=" + statId.ToString(CultureInfo.InvariantCulture),
                    "type=" + Stage2ContractFingerprint.NormalizeType(stat.GetType()));

                PropertyInfo affectsProperty = stat.GetType().GetProperty("Affects", BindingFlags.Public | BindingFlags.Instance);
                IEnumerable affects = affectsProperty == null ? null : affectsProperty.GetValue(stat, null) as IEnumerable;
                if (affects != null)
                {
                    int[] affectedIds = affects.Cast<object>()
                        .Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture))
                        .OrderBy(value => value)
                        .ToArray();
                    if (affectedIds.Length > 0)
                    {
                        AddLine(
                            topologyLines,
                            "runtime.stats.affects",
                            "id=" + statId.ToString(CultureInfo.InvariantCulture),
                            string.Join(",", affectedIds.Select(value => value.ToString(CultureInfo.InvariantCulture))));
                    }
                }
            }

            PropertyInfo[] statProperties = statsType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => property.GetIndexParameters().Length == 0)
                .Where(property => property.GetGetMethod(false) != null)
                .Where(property => statInterface.IsAssignableFrom(property.PropertyType))
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToArray();
            foreach (PropertyInfo property in statProperties)
            {
                object stat = property.GetValue(stats, null);
                if (stat == null)
                {
                    throw new InvalidDataException("Stats property returned null during safe topology inspection: " + property.Name);
                }

                AddLine(
                    topologyLines,
                    "runtime.stats.property",
                    property.Name,
                    "id=" + GetStatId(stat).ToString(CultureInfo.InvariantCulture),
                    "type=" + Stage2ContractFingerprint.NormalizeType(stat.GetType()));
            }

            AddLine(lines, "runtime.stats.sha256", ComputeSha256(string.Join("\n", topologyLines)));
            foreach (string topologyLine in topologyLines)
            {
                lines.Add(topologyLine);
            }
        }

        private static int GetStatId(object stat)
        {
            PropertyInfo property = stat.GetType().GetProperty("StatId", BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                throw new InvalidDataException("A Stats topology entry has no public StatId property: " + stat.GetType().FullName);
            }

            return Convert.ToInt32(property.GetValue(stat, null), CultureInfo.InvariantCulture);
        }

        private static void AddReferenceLines(
            ICollection<string> lines,
            string prefix,
            params Assembly[] assemblies)
        {
            foreach (Assembly assembly in assemblies.OrderBy(value => value.GetName().Name, StringComparer.Ordinal))
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
        }

        private static void VerifyMappedReferences(
            string expectedLegacyManifest,
            params Assembly[] actualAssemblies)
        {
            string[] expectedReferences = SplitLines(expectedLegacyManifest)
                .Where(line => line.StartsWith("legacy.reference|", StringComparison.Ordinal))
                .Select(line => line.Substring("legacy.reference|".Length))
                .Where(line => !IsAllowedReferenceDifference(GetReferenceName(line)))
                .Where(line => !IsFrameworkReference(GetReferenceName(line)))
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToArray();

            var actualReferenceLines = new List<string>();
            AddReferenceLines(actualReferenceLines, "actual.reference", actualAssemblies);
            string[] actualReferences = actualReferenceLines
                .Select(line => line.Substring("actual.reference|".Length))
                .Where(line => !IsAllowedReferenceDifference(GetReferenceName(line)))
                .Where(line => !IsFrameworkReference(GetReferenceName(line)))
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToArray();
            VerifyExact(
                string.Join("\n", expectedReferences) + "\n",
                string.Join("\n", actualReferences) + "\n",
                "Mapped Stage 3 non-provider references");

            string[] actualPinned = actualReferenceLines
                .Select(line => line.Substring("actual.reference|".Length))
                .Where(line => LinuxPinnedReferenceIdentities.Any(pin =>
                    string.Equals(GetReferenceTargetAndName(line), GetReferenceTargetAndName(pin), StringComparison.Ordinal)))
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToArray();
            string[] expectedPinned = LinuxPinnedReferenceIdentities.OrderBy(line => line, StringComparer.Ordinal).ToArray();
            VerifyExact(
                string.Join("\n", expectedPinned) + "\n",
                string.Join("\n", actualPinned) + "\n",
                "Linux Stage 3 pinned provider references");
        }

        private static string GetReferenceName(string referenceLine)
        {
            string[] components = referenceLine.Split('|');
            return components.Length > 1 ? components[1] : string.Empty;
        }

        private static string GetReferenceTargetAndName(string referenceLine)
        {
            string[] components = referenceLine.Split('|');
            return components.Length > 1 ? components[0] + "|" + components[1] : referenceLine;
        }

        private static bool IsAllowedReferenceDifference(string name)
        {
            return string.Equals(name, "Dapper", StringComparison.Ordinal)
                || string.Equals(name, "Npgsql", StringComparison.Ordinal)
                || string.Equals(name, "Microsoft.Data.SqlClient", StringComparison.Ordinal);
        }

        private static bool IsFrameworkReference(string name)
        {
            return string.Equals(name, "mscorlib", StringComparison.Ordinal)
                || string.Equals(name, "netstandard", StringComparison.Ordinal)
                || string.Equals(name, "Microsoft.CSharp", StringComparison.Ordinal)
                || string.Equals(name, "System", StringComparison.Ordinal)
                || name.StartsWith("System.", StringComparison.Ordinal);
        }

        private static string NormalizeProviderApiLine(string line)
        {
            return line
                .Replace("System.Data.SqlClient.", "AORebirth.SqlClient.")
                .Replace("Microsoft.Data.SqlClient.", "AORebirth.SqlClient.");
        }

        private static IEnumerable<string> GetNormalizedApiContractLines(string contract)
        {
            bool skipBinaryCompatibilityType = false;
            foreach (string rawLine in SplitLines(contract))
            {
                string line = NormalizeProviderApiLine(rawLine);
                if (string.Equals(line, "type.begin|System.Data.Linq.Binary", StringComparison.Ordinal))
                {
                    skipBinaryCompatibilityType = true;
                    continue;
                }

                if (skipBinaryCompatibilityType)
                {
                    if (string.Equals(line, "type.end|System.Data.Linq.Binary", StringComparison.Ordinal))
                    {
                        skipBinaryCompatibilityType = false;
                    }

                    continue;
                }

                yield return line;
            }

            if (skipBinaryCompatibilityType)
            {
                throw new InvalidDataException("System.Data.Linq.Binary compatibility contract block was incomplete.");
            }
        }

        private static Type[] GetAssemblyTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                string messages = string.Join(
                    "; ",
                    exception.LoaderExceptions.Where(loader => loader != null).Select(loader => loader.Message));
                throw new InvalidOperationException("Could not load types from " + assembly.GetName().Name + ": " + messages, exception);
            }
        }

        private static string FormatPublicKeyToken(byte[] token)
        {
            if (token == null || token.Length == 0)
            {
                return "null";
            }

            return string.Concat(token.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(new UTF8Encoding(false).GetBytes(value));
                return string.Concat(hash.Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static void AddLine(ICollection<string> lines, params string[] values)
        {
            lines.Add(string.Join("|", values.Select(value => Escape(value ?? string.Empty))));
        }

        private static string Escape(string value)
        {
            return value
                .Replace("%", "%25")
                .Replace("|", "%7C")
                .Replace("\r", "%0D")
                .Replace("\n", "%0A");
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
                throw new FileNotFoundException("Stage 3 contract manifest was not found.", fullPath);
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
    }
}
