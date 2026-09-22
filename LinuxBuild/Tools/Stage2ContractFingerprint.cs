using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace AORebirth.LinuxBuild
{
    internal static class Stage2ContractFingerprint
    {
        private const string ManifestName = "AORebirth.Stage2Contract";
        private const string ManifestVersion = "1";

        internal static string Create(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            Assembly[] orderedAssemblies = assemblies
                .Where(assembly => assembly != null)
                .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
                .ToArray();
            if (orderedAssemblies.Length == 0)
            {
                throw new ArgumentException("At least one assembly is required.", nameof(assemblies));
            }

            string duplicateAssembly = orderedAssemblies
                .GroupBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .FirstOrDefault();
            if (duplicateAssembly != null)
            {
                throw new ArgumentException("Duplicate assembly: " + duplicateAssembly, nameof(assemblies));
            }

            var lines = new List<string>();
            AddLine(lines, "manifest", ManifestName, ManifestVersion);
            foreach (Assembly assembly in orderedAssemblies)
            {
                AddAssembly(lines, assembly);
            }

            return string.Join("\n", lines) + "\n";
        }

        internal static void Write(string manifestPath, IEnumerable<Assembly> assemblies)
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

            File.WriteAllText(fullPath, Create(assemblies), new UTF8Encoding(false));
        }

        internal static void Verify(string manifestPath, IEnumerable<Assembly> assemblies)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new ArgumentException("A manifest path is required.", nameof(manifestPath));
            }

            string fullPath = Path.GetFullPath(manifestPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Contract manifest was not found.", fullPath);
            }

            string expected = NormalizeManifest(File.ReadAllText(fullPath));
            string actual = NormalizeManifest(Create(assemblies));
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidDataException(DescribeFirstDifference(expected, actual));
            }
        }

        private static void AddAssembly(ICollection<string> lines, Assembly assembly)
        {
            AssemblyName assemblyName = assembly.GetName();
            string name = assemblyName.Name;
            AddLine(lines, "assembly.begin", name);
            AddLine(lines, "assembly.name", name);
            AddLine(lines, "assembly.version", assemblyName.Version == null ? string.Empty : assemblyName.Version.ToString());
            AddLine(lines, "assembly.public-key-token", FormatPublicKeyToken(assemblyName.GetPublicKeyToken()));
            AddLine(lines, "assembly.file-version", GetAssemblyAttributeValue(assembly, typeof(AssemblyFileVersionAttribute).FullName, "Version"));
            AddLine(lines, "assembly.title", GetAssemblyAttributeValue(assembly, typeof(AssemblyTitleAttribute).FullName, "Title"));
            AddLine(lines, "assembly.description", GetAssemblyAttributeValue(assembly, typeof(AssemblyDescriptionAttribute).FullName, "Description"));
            AddLine(lines, "assembly.configuration", GetAssemblyAttributeValue(assembly, typeof(AssemblyConfigurationAttribute).FullName, "Configuration"));
            AddLine(lines, "assembly.company", GetAssemblyAttributeValue(assembly, typeof(AssemblyCompanyAttribute).FullName, "Company"));
            AddLine(lines, "assembly.product", GetAssemblyAttributeValue(assembly, typeof(AssemblyProductAttribute).FullName, "Product"));
            AddLine(lines, "assembly.copyright", GetAssemblyAttributeValue(assembly, typeof(AssemblyCopyrightAttribute).FullName, "Copyright"));
            AddLine(lines, "assembly.trademark", GetAssemblyAttributeValue(assembly, typeof(AssemblyTrademarkAttribute).FullName, "Trademark"));
            AddLine(lines, "assembly.culture", GetAssemblyAttributeValue(assembly, typeof(AssemblyCultureAttribute).FullName, "Culture"));
            AddLine(lines, "assembly.guid", GetAssemblyAttributeValue(assembly, typeof(GuidAttribute).FullName, "Value"));
            AddLine(lines, "assembly.com-visible", GetAssemblyAttributeValue(assembly, typeof(ComVisibleAttribute).FullName, "Value").ToLowerInvariant());
            AddLine(lines, "assembly.utility-revision-name", GetAssemblyAttributeValue(assembly, "Utility.RevisionNameAttribute", "RevisionName"));

            Type[] exportedTypes;
            try
            {
                exportedTypes = assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                string loaderMessages = string.Join(
                    "; ",
                    exception.LoaderExceptions
                        .Where(loaderException => loaderException != null)
                        .Select(loaderException => loaderException.Message));
                throw new InvalidOperationException("Could not load exported types from " + name + ": " + loaderMessages, exception);
            }

            foreach (Type type in exportedTypes.OrderBy(NormalizeType, StringComparer.Ordinal))
            {
                AddType(lines, type);
            }

            AddLine(lines, "assembly.end", name);
        }

        private static void AddType(ICollection<string> lines, Type type)
        {
            string typeName = NormalizeType(type);
            AddLine(lines, "type.begin", typeName);
            AddLine(lines, "type.kind", GetTypeKind(type));
            AddLine(lines, "type.modifiers", GetTypeModifiers(type));
            AddLine(
                lines,
                "type.serializable-attribute",
                type.IsEnum || type.IsInterface
                    ? "not-applicable"
                    : type.IsDefined(typeof(SerializableAttribute), false).ToString().ToLowerInvariant());

            foreach (Type genericParameter in GetDeclaredGenericParameters(type))
            {
                AddLine(lines, "type.generic-parameter", FormatGenericParameter(genericParameter));
            }

            if (type.IsEnum)
            {
                AddEnum(lines, type);
            }
            else
            {
                AddLine(lines, "type.base", type.BaseType == null ? string.Empty : NormalizeType(type.BaseType));
                foreach (string interfaceName in GetDeclaredInterfaces(type).Select(NormalizeType).OrderBy(value => value, StringComparer.Ordinal))
                {
                    AddLine(lines, "type.interface", interfaceName);
                }

                AddMembers(lines, type);
            }

            AddLine(lines, "type.end", typeName);
        }

        private static IEnumerable<Type> GetDeclaredInterfaces(Type type)
        {
            Type[] interfaces = type.GetInterfaces();
            if (!type.IsInterface && type.BaseType != null)
            {
                var inherited = new HashSet<Type>(type.BaseType.GetInterfaces());
                return interfaces.Where(candidate => !inherited.Contains(candidate));
            }

            return interfaces.Where(
                candidate => !interfaces.Any(
                    other => other != candidate && other.GetInterfaces().Contains(candidate)));
        }

        private static void AddEnum(ICollection<string> lines, Type type)
        {
            AddLine(lines, "type.enum-underlying", NormalizeType(Enum.GetUnderlyingType(type)));
            AddLine(
                lines,
                "type.enum-flags-attribute",
                type.IsDefined(typeof(FlagsAttribute), false).ToString().ToLowerInvariant());

            FieldInfo[] fields = type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.IsLiteral)
                .OrderBy(field => field.Name, StringComparer.Ordinal)
                .ToArray();
            foreach (FieldInfo field in fields)
            {
                AddLine(lines, "type.enum-value", field.Name, FormatConstant(field.GetRawConstantValue()));
            }
        }

        private static void AddMembers(ICollection<string> lines, Type type)
        {
            const BindingFlags PublicDeclared =
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (string constructor in type.GetConstructors(PublicDeclared).Select(FormatConstructor).OrderBy(value => value, StringComparer.Ordinal))
            {
                AddLine(lines, "type.constructor", constructor);
            }

            foreach (string property in type.GetProperties(PublicDeclared).Select(FormatProperty).OrderBy(value => value, StringComparer.Ordinal))
            {
                AddLine(lines, "type.property", property);
            }

            foreach (FieldInfo field in type.GetFields(PublicDeclared)
                .OrderBy(field => NormalizeType(field.FieldType), StringComparer.Ordinal)
                .ThenBy(field => field.Name, StringComparer.Ordinal))
            {
                AddLine(
                    lines,
                    "type.field",
                    NormalizeType(field.FieldType),
                    field.Name,
                    "static=" + field.IsStatic.ToString().ToLowerInvariant(),
                    "initonly=" + field.IsInitOnly.ToString().ToLowerInvariant(),
                    "literal=" + field.IsLiteral.ToString().ToLowerInvariant(),
                    "constant=" + (field.IsLiteral ? FormatConstant(field.GetRawConstantValue()) : string.Empty));
            }

            foreach (string eventContract in type.GetEvents(PublicDeclared).Select(FormatEvent).OrderBy(value => value, StringComparer.Ordinal))
            {
                AddLine(lines, "type.event", eventContract);
            }

            foreach (string method in type.GetMethods(PublicDeclared).Select(FormatMethod).OrderBy(value => value, StringComparer.Ordinal))
            {
                AddLine(lines, "type.method", method);
            }
        }

        private static IEnumerable<Type> GetDeclaredGenericParameters(Type type)
        {
            if (!type.IsGenericType)
            {
                return Enumerable.Empty<Type>();
            }

            return type.GetGenericArguments()
                .Where(argument => argument.IsGenericParameter && argument.DeclaringType == type)
                .OrderBy(argument => argument.GenericParameterPosition);
        }

        private static string FormatConstructor(ConstructorInfo constructor)
        {
            return "public instance .ctor(" + FormatParameters(constructor.GetParameters()) + ")";
        }

        private static string FormatProperty(PropertyInfo property)
        {
            MethodInfo getter = property.GetGetMethod(true);
            MethodInfo setter = property.GetSetMethod(true);
            MethodInfo representative = getter ?? setter;
            string staticModifier = representative != null && representative.IsStatic ? "static " : "instance ";
            string indexParameters = property.GetIndexParameters().Length == 0
                ? string.Empty
                : "[" + FormatParameters(property.GetIndexParameters()) + "]";
            return string.Format(
                CultureInfo.InvariantCulture,
                "public {0}{1} {2}{3} {{get:{4};set:{5}}}",
                staticModifier,
                NormalizeType(property.PropertyType),
                property.Name,
                indexParameters,
                GetAccessibility(getter),
                GetAccessibility(setter));
        }

        private static string FormatEvent(EventInfo eventInfo)
        {
            MethodInfo addMethod = eventInfo.GetAddMethod(true);
            MethodInfo removeMethod = eventInfo.GetRemoveMethod(true);
            MethodInfo raiseMethod = eventInfo.GetRaiseMethod(true);
            MethodInfo representative = addMethod ?? removeMethod ?? raiseMethod;
            string staticModifier = representative != null && representative.IsStatic ? "static" : "instance";
            return string.Format(
                CultureInfo.InvariantCulture,
                "public {0} {1} {2} {{add:{3};remove:{4};raise:{5}}}",
                staticModifier,
                NormalizeType(eventInfo.EventHandlerType),
                eventInfo.Name,
                GetAccessibility(addMethod),
                GetAccessibility(removeMethod),
                GetAccessibility(raiseMethod));
        }

        private static string FormatMethod(MethodInfo method)
        {
            var modifiers = new List<string> { "public", method.IsStatic ? "static" : "instance" };
            if (method.IsAbstract)
            {
                modifiers.Add("abstract");
            }

            if (method.IsVirtual)
            {
                modifiers.Add("virtual");
            }

            if (method.IsFinal)
            {
                modifiers.Add("final");
            }

            if (method.IsSpecialName)
            {
                modifiers.Add("special-name");
            }

            Type[] genericArguments = method.IsGenericMethod
                ? method.GetGenericArguments().OrderBy(argument => argument.GenericParameterPosition).ToArray()
                : Array.Empty<Type>();
            string genericSuffix = genericArguments.Length == 0
                ? string.Empty
                : "<" + string.Join(",", genericArguments.Select(NormalizeType)) + ">";
            string constraints = genericArguments.Length == 0
                ? string.Empty
                : " where " + string.Join(";", genericArguments.Select(FormatGenericParameter));

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} {2}{3}({4}){5}",
                string.Join(" ", modifiers),
                NormalizeType(method.ReturnType),
                method.Name,
                genericSuffix,
                FormatParameters(method.GetParameters()),
                constraints);
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
                if (parameter.IsOut && !parameter.IsIn)
                {
                    direction = "out ";
                }
                else if (parameter.IsIn && !parameter.IsOut)
                {
                    direction = "in ";
                }
                else
                {
                    direction = "ref ";
                }
            }

            string paramArray = parameter.IsDefined(typeof(ParamArrayAttribute), false) ? "params " : string.Empty;
            string optionalAndDefault = string.Empty;
            if (parameter.IsOptional || parameter.HasDefaultValue)
            {
                optionalAndDefault = " optional=" + parameter.IsOptional.ToString().ToLowerInvariant()
                    + ";default=" + (parameter.HasDefaultValue ? FormatConstant(parameter.DefaultValue) : "none");
            }

            return paramArray + direction + NormalizeType(parameterType) + " " + parameter.Name + optionalAndDefault;
        }

        private static string FormatGenericParameter(Type parameter)
        {
            GenericParameterAttributes attributes = parameter.GenericParameterAttributes;
            var modifiers = new List<string>();
            GenericParameterAttributes variance = attributes & GenericParameterAttributes.VarianceMask;
            if (variance == GenericParameterAttributes.Covariant)
            {
                modifiers.Add("covariant");
            }
            else if (variance == GenericParameterAttributes.Contravariant)
            {
                modifiers.Add("contravariant");
            }

            GenericParameterAttributes constraints = attributes & GenericParameterAttributes.SpecialConstraintMask;
            if ((constraints & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
            {
                modifiers.Add("reference-type");
            }

            if ((constraints & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
            {
                modifiers.Add("non-nullable-value-type");
            }

            if ((constraints & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
            {
                modifiers.Add("default-constructor");
            }

            string[] typeConstraints = parameter
                .GetGenericParameterConstraints()
                .Select(NormalizeType)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}[modifiers={1};constraints={2}]",
                NormalizeType(parameter),
                string.Join(",", modifiers),
                string.Join(",", typeConstraints));
        }

        internal static string NormalizeType(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.IsGenericParameter)
            {
                string prefix = type.DeclaringMethod == null ? "!" : "!!";
                return prefix + type.GenericParameterPosition.ToString(CultureInfo.InvariantCulture) + ":" + type.Name;
            }

            if (type.IsByRef)
            {
                return NormalizeType(type.GetElementType()) + "&";
            }

            if (type.IsPointer)
            {
                return NormalizeType(type.GetElementType()) + "*";
            }

            if (type.IsArray)
            {
                int rank = type.GetArrayRank();
                string suffix;
                if (rank == 1 && type == type.GetElementType().MakeArrayType())
                {
                    suffix = "[]";
                }
                else if (rank == 1)
                {
                    suffix = "[*]";
                }
                else
                {
                    suffix = "[" + new string(',', rank - 1) + "]";
                }

                return NormalizeType(type.GetElementType()) + suffix;
            }

            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                string definitionName = RemoveGenericArity((definition.FullName ?? definition.Name).Replace('+', '.'));
                return definitionName + "<" + string.Join(",", type.GetGenericArguments().Select(NormalizeType)) + ">";
            }

            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        private static string RemoveGenericArity(string name)
        {
            var builder = new StringBuilder(name.Length);
            for (int index = 0; index < name.Length; index++)
            {
                if (name[index] != '`')
                {
                    builder.Append(name[index]);
                    continue;
                }

                while (index + 1 < name.Length && char.IsDigit(name[index + 1]))
                {
                    index++;
                }
            }

            return builder.ToString();
        }

        private static string GetTypeKind(Type type)
        {
            if (type.IsEnum)
            {
                return "enum";
            }

            if (type.IsInterface)
            {
                return "interface";
            }

            if (typeof(MulticastDelegate).IsAssignableFrom(type.BaseType))
            {
                return "delegate";
            }

            if (type.IsValueType)
            {
                return "struct";
            }

            return "class";
        }

        private static string GetTypeModifiers(Type type)
        {
            var modifiers = new List<string>();
            if (type.IsAbstract)
            {
                modifiers.Add("abstract");
            }

            if (type.IsSealed)
            {
                modifiers.Add("sealed");
            }

            return string.Join(",", modifiers);
        }

        private static string GetAccessibility(MethodBase method)
        {
            if (method == null)
            {
                return "none";
            }

            if (method.IsPublic)
            {
                return "public";
            }

            if (method.IsFamilyOrAssembly)
            {
                return "protected-internal";
            }

            if (method.IsFamilyAndAssembly)
            {
                return "private-protected";
            }

            if (method.IsFamily)
            {
                return "protected";
            }

            if (method.IsAssembly)
            {
                return "internal";
            }

            return "private";
        }

        private static string FormatPublicKeyToken(byte[] token)
        {
            if (token == null || token.Length == 0)
            {
                return "null";
            }

            return string.Concat(token.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string GetAssemblyAttributeValue(Assembly assembly, string attributeTypeName, string memberName)
        {
            CustomAttributeData attribute = CustomAttributeData.GetCustomAttributes(assembly)
                .FirstOrDefault(candidate => string.Equals(candidate.AttributeType.FullName, attributeTypeName, StringComparison.Ordinal));
            if (attribute == null)
            {
                return string.Empty;
            }

            foreach (CustomAttributeNamedArgument namedArgument in attribute.NamedArguments)
            {
                if (string.Equals(namedArgument.MemberName, memberName, StringComparison.Ordinal))
                {
                    return Convert.ToString(namedArgument.TypedValue.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }

            if (attribute.ConstructorArguments.Count > 0)
            {
                return Convert.ToString(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return string.Empty;
        }

        private static string FormatConstant(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value == Missing.Value)
            {
                return "missing";
            }

            if (value == DBNull.Value)
            {
                return "dbnull";
            }

            string stringValue = value as string;
            if (stringValue != null)
            {
                return "string:\"" + Escape(stringValue) + "\"";
            }

            if (value is char)
            {
                return "char:" + ((int)(char)value).ToString(CultureInfo.InvariantCulture);
            }

            Type valueType = value.GetType();
            if (valueType.IsEnum)
            {
                return NormalizeType(valueType) + ":" + Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return NormalizeType(valueType) + ":" + Convert.ToString(value, CultureInfo.InvariantCulture);
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

        private static string NormalizeManifest(string value)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n') + "\n";
        }

        private static string DescribeFirstDifference(string expected, string actual)
        {
            string[] expectedLines = expected.Split('\n');
            string[] actualLines = actual.Split('\n');
            int commonLength = Math.Min(expectedLines.Length, actualLines.Length);
            for (int index = 0; index < commonLength; index++)
            {
                if (!string.Equals(expectedLines[index], actualLines[index], StringComparison.Ordinal))
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "Contract mismatch at line {0}. Expected: {1} Actual: {2}",
                        index + 1,
                        expectedLines[index],
                        actualLines[index]);
                }
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Contract line count changed. Expected {0}; actual {1}.",
                expectedLines.Length - 1,
                actualLines.Length - 1);
        }
    }
}
