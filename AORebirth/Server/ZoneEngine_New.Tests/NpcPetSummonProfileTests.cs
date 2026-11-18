namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;

using AORebirth.Core.GameData;
using AORebirth.Enums;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;

[TestClass]
[DoNotParallelize]
public sealed class NpcPetSummonProfileTests
{
    const int CreationItem = 90001;
    const string ValidProfile = """
        {
          "Version": 1,
          "CreationItemTemplateIds": [90001],
          "Policy": {
            "MaxPets": 3,
            "MaxPetsPerType": 1,
            "SummonIntervalSeconds": 2,
            "RespawnDelaySeconds": 5,
            "RetryDelaySeconds": 1
          }
        }
        """;

    DirectoryInfo _root = null!;
    string? _priorOverride;

    [TestInitialize]
    public void SetUp()
    {
        _root = Directory.CreateTempSubdirectory("npc-pet-profile-tests-");
        _priorOverride = Environment.GetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName);
        Environment.SetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName, null);
    }

    [TestCleanup]
    public void CleanUp()
    {
        Environment.SetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName, _priorOverride);
        _root.Delete(recursive: true);
    }

    [TestMethod]
    public void AbsentProfileLeavesPreActivationStartupCompatible()
    {
        Assert.IsNull(NpcPetSummonProfile.LoadOptional(_root.FullName));
    }

    [TestMethod]
    public void PresentProfileLoadsExplicitPolicyAndResolvesEquippedItemOperations()
    {
        NpcPetSummonProfile profile = WriteAndLoad(ValidProfile);
        GameDataStore data = GameDataWithCreator(equipped: true);
        var item = CreationTemplate(Summon(FunctionType.SpawnMonster2, "PET1", 1, 5), Summon(FunctionType.SummonPet, "PET1", 1, 4));

        profile.ValidateReferences(new ItemCatalog(item), data);

        CollectionAssert.AreEqual(new[] { CreationItem }, profile.CreationItemTemplateIds);
        Assert.AreEqual(3, profile.Policy.MaxPets);
        Assert.AreEqual(5d, profile.Policy.RespawnDelaySeconds);
        NpcPetSummonOperation[] operations = NpcPetSummonProfile.ReadSummons(item, data);
        Assert.AreEqual(2, operations.Length);
        Assert.AreEqual(0, operations[0].Duration);
        Assert.AreEqual(4, operations[1].Duration);
    }

    [TestMethod]
    [DataRow("\"Version\": 1,", "\"Version\": 2,")]
    [DataRow("[90001]", "[90001, 90001]")]
    [DataRow("[90001]", "[0]")]
    [DataRow("\"MaxPetsPerType\": 1,", "")]
    [DataRow("\"RetryDelaySeconds\": 1", "\"RetryDelaySeconds\": 0")]
    [DataRow("\"MaxPets\": 3,", "\"MaxPets\": 3, \"Unknown\": true,")]
    [DataRow("\"MaxPets\": 3,", "\"MaxPets\": 3, \"MaxPets\": 4,")]
    public void RejectsInvalidVersionsIdsAndPolicy(string original, string replacement)
    {
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            WriteAndLoad(ValidProfile.Replace(original, replacement, StringComparison.Ordinal)));
        StringAssert.Contains(error.Message, NpcPetSummonProfile.FileName);
    }

    [TestMethod]
    public void RejectsMissingItemAndMissingEquippedTarget()
    {
        NpcPetSummonProfile profile = WriteAndLoad(ValidProfile);
        GameDataStore data = GameDataWithCreator(equipped: true);
        var missingItem = new ItemCatalog(new ItemTemplate { Id = 90002 });
        var missingTarget = new ItemCatalog(CreationTemplate(Summon(FunctionType.SummonPet, "MISS", 1, 3)));

        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => profile.ValidateReferences(missingItem, data)).Message,
            "missing creation item template");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => profile.ValidateReferences(missingTarget, data)).Message,
            "unspawnable pet 'MISS'");
    }

    [TestMethod]
    public void UnusedItemStillRequiresWellFormedSummonOperations()
    {
        NpcPetSummonProfile profile = WriteAndLoad(ValidProfile);
        GameDataStore data = GameDataWithCreator(equipped: false);
        var unused = new ItemCatalog(CreationTemplate(Summon(FunctionType.SummonPet, "MISS", 1, 3)));
        profile.ValidateReferences(unused, data);

        var malformed = new ItemCatalog(CreationTemplate(Summon(FunctionType.SpawnMonster2, "MISS", 0, 3)));
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => profile.ValidateReferences(malformed, data)).Message,
            "invalid summon arguments");
    }

    NpcPetSummonProfile WriteAndLoad(string json)
    {
        File.WriteAllText(Path.Combine(_root.FullName, NpcPetSummonProfile.FileName), json);
        return NpcPetSummonProfile.LoadOptional(_root.FullName)!;
    }

    GameDataStore GameDataWithCreator(bool equipped)
    {
        string equipment = equipped ? "[[90001,90001]]" : "[]";
        File.WriteAllText(Path.Combine(_root.FullName, "NpcTemplates.json"), $$"""
            {
              "OWNR": { "Templates": [{ "Name": "Owner", "Level": 1, "Stats": {}, "Equipment": {{equipment}} }] },
              "PET1": { "Templates": [{ "Name": "Pet", "Level": 1, "Stats": {} }] }
            }
            """);
        return new GameDataStore(new StubLogger(), null, _root.FullName);
    }

    static ItemTemplate CreationTemplate(params ItemSpell[] operations) => new()
    {
        Id = CreationItem,
        Name = "Independent fixture name",
        SpellList = new Dictionary<EventType, List<ItemSpell>> { [EventType.OnUse] = [.. operations] }
    };

    static ItemSpell Summon(FunctionType function, string hash, int level, int duration) => new()
    {
        FunctionType = (int)function,
        Arguments = [hash, level, duration]
    };

    sealed class ItemCatalog(ItemTemplate template) : IItemTemplateCatalog
    {
        public bool TryGet(int aoid, out ItemTemplate item)
        {
            item = aoid == template.Id ? template : null!;
            return item != null;
        }

        public ItemTemplate Require(int aoid)
            => TryGet(aoid, out ItemTemplate item) ? item : throw new KeyNotFoundException($"Missing fixture item {aoid}.");
    }
}
