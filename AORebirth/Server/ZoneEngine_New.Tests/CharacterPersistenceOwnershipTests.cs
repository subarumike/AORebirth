using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AORebirth.Interfaces.Persistence.Characters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ZoneEngine_New.Tests;

[TestClass]
public sealed class CharacterPersistenceOwnershipTests
{
    [TestMethod]
    public void RuntimeAdaptersCannotRegainStorageExecution()
    {
        string root = Root();
        foreach (string name in new[] { "MySqlCharacterRepository", "MySqlStatRepository", "MySqlInventoryRepository",
            "MySqlUploadedNanoRepository", "MySqlItemNameRepository", "MySqlCharacterCoalesceCommit",
            "MySqlInventoryMutationPersistence", "MySqlTradePersistence", "MySqlActiveNanoRepository", "SharedCharacterPersistence" })
        {
            string source = File.ReadAllText(Path.Combine(root, "AORebirth/Server/ZoneEngine_New/Core/Data", name + ".cs"));
            Assert.IsFalse(Regex.IsMatch(source, @"\bMySqlCommand\b|\bBeginTransaction\s*\(|\.Execute(?:Reader|Scalar|NonQuery)\s*\("), name);
        }
    }

    [TestMethod]
    public void SharedContractDoesNotTakeEngineObjects()
    {
        foreach (Type type in typeof(ICharacterPersistenceDao).GetMethods().SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)))
            Check(type);
        foreach (Type type in new[] { typeof(CharacterStateData), typeof(CharacterInventoryMutationData), typeof(ItemCreditMutationData), typeof(CharacterActiveNanoData) })
            foreach (var property in type.GetProperties()) Check(property.PropertyType);

        static void Check(Type type)
        {
            Assert.IsFalse((type.Namespace ?? "").StartsWith("ZoneEngine_New", StringComparison.Ordinal), type.ToString());
            foreach (Type argument in type.GetGenericArguments()) Check(argument);
        }
    }

    static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md"))) return directory.FullName;
        throw new InvalidOperationException("Repository root missing.");
    }
}
