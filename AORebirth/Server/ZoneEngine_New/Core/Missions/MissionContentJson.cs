namespace ZoneEngine.Core.Missions;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AORebirth.Core.GameData;

/// <summary>Editable GameData adapter for the existing immutable mission records.
/// Construction still executes each record's structural validators. No content type
/// names, source hashes or reconstruction approval decisions come from the file.</summary>
internal static class MissionContentJson
{
    internal static string RootPath => Path.Combine(GameDataPaths.ResolveRuntimeRoot(), "Missions");
    internal static T Read<T>(string filename) => Read<T>(RootPath, filename);
    internal static T Read<T>(string root, string filename)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, filename)),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        return (T)ReadValue(document.RootElement, typeof(T))!;
    }

    static object? ReadValue(JsonElement node, Type type)
    {
        if (node.ValueKind == JsonValueKind.Null) return null;
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable != null) return ReadValue(node, nullable);
        if (type == typeof(string)) return node.GetString();
        if (type.IsEnum) return node.ValueKind == JsonValueKind.String ? Enum.Parse(type, node.GetString()!, true) : Enum.ToObject(type, node.GetInt32());
        if (type.IsPrimitive || type == typeof(decimal)) return JsonSerializer.Deserialize(node.GetRawText(), type);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var types = type.GetGenericArguments();
            var dictionary = (IDictionary)Activator.CreateInstance(type)!;
            foreach (var property in node.EnumerateObject())
            {
                object key = types[0] == typeof(string) ? property.Name : types[0].IsEnum
                    ? Enum.Parse(types[0], property.Name, true) : Convert.ChangeType(property.Name, types[0], System.Globalization.CultureInfo.InvariantCulture);
                if (dictionary.Contains(key)) throw new InvalidDataException("Duplicate mission content key.");
                dictionary.Add(key, ReadValue(property.Value, types[1]));
            }
            return dictionary;
        }
        if (type.IsArray || type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            Type element = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
            var values = node.EnumerateArray().Select(value => ReadValue(value, element)).ToArray();
            var array = Array.CreateInstance(element, values.Length);
            for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i);
            if (type.IsAssignableFrom(array.GetType())) return array;
            throw new InvalidDataException("Unsupported mission content collection: " + type.Name);
        }
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in node.EnumerateObject())
            if (!properties.TryAdd(property.Name, property.Value)) throw new InvalidDataException("Duplicate mission content field: " + property.Name);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var constructors = type.GetConstructors(flags).Where(ctor => ctor.GetParameters().All(p => properties.ContainsKey(p.Name!)))
            .OrderByDescending(ctor => ctor.GetParameters().Length).ToArray();
        if (constructors.Length == 0) throw new InvalidDataException("Incomplete mission content record: " + type.Name);
        var constructor = constructors[0];
        var instance = constructor.Invoke(constructor.GetParameters().Select(p => ReadValue(properties[p.Name!], p.ParameterType)).ToArray());
        if (constructor.GetParameters().Length == 0)
        {
            foreach (var field in type.GetFields(flags).Where(f => !f.IsInitOnly))
                if (properties.TryGetValue(field.Name, out var fieldValue)) field.SetValue(instance, ReadValue(fieldValue, field.FieldType));
            foreach (var property in type.GetProperties(flags).Where(p => p.SetMethod != null))
                if (properties.TryGetValue(property.Name, out var propertyValue)) property.SetValue(instance, ReadValue(propertyValue, property.PropertyType));
        }
        return instance;
    }
}
