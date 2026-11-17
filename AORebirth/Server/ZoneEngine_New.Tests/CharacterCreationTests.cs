using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using AORebirth.Core.GameData;
using AORebirth.Database.Dao;
using AORebirth.Database.Domain.CharacterCreation;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.CharacterCreation;
using LoginEngine.CharacterCreation;
using LoginEngine.Packets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ZoneEngine_New.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CharacterCreationTests
{
    string directory = null!;
    string? previousOverride;
    object? previousCache;
    static FieldInfo Cache => typeof(NewCharacterStats).GetField("current", BindingFlags.Static | BindingFlags.NonPublic)!;
    static string DefaultFile => Path.Combine(AppContext.BaseDirectory, "GameData", "NewCharacter.json");
    string FilePath => Path.Combine(directory, "NewCharacter.json");

    [TestInitialize]
    public void Initialize()
    {
        directory = Path.Combine(Path.GetTempPath(), "aorebirth-character-creation-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        previousOverride = Environment.GetEnvironmentVariable(GameDataPaths.EnvironmentVariableName);
        previousCache = Cache.GetValue(null);
        Cache.SetValue(null, null);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Cache.SetValue(null, previousCache);
        Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, previousOverride);
        Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public void NoOverrideLoadsTheFileBesideTheRuntime()
    {
        Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, null);
        Assert.AreEqual(Path.GetDirectoryName(DefaultFile), GameDataPaths.ResolveRuntimeRoot());
        NewCharacterStats starter = NewCharacterStats.Current;
        Assert.IsTrue(starter.StartLocation.Playfield > 0);
        Assert.IsTrue(starter.Stats.Single(s => s.Stat == 54).Value > 0);
        foreach (int breed in new[] { 1, 2, 3, 4 })
            Assert.IsNotNull(starter.AbilitiesFor(breed));
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void EveryPlayerBreedLoadsItsAuthoredAbilities(int breed)
    {
        NewCharacterStats starter = NewCharacterStats.Load(DefaultFile);
        JsonObject authored = JsonNode.Parse(File.ReadAllText(DefaultFile))!["Breeds"]!.AsArray()
            .Select(n => n!.AsObject()).Single(n => n["Breed"]!.GetValue<int>() == breed);
        BreedAbilities abilities = starter.AbilitiesFor(breed);
        CollectionAssert.AreEqual(new[] { "Strength", "Psychic", "Sense", "Intelligence", "Stamina", "Agility" }
            .Select(name => authored[name]!.GetValue<int>()).ToArray(), abilities.ToLegacyOrder());
    }

    [DataTestMethod]
    [DataRow(1, 297667)]
    [DataRow(2, 297666)]
    [DataRow(3, 297665)]
    public void NanoTechnicianStarterDeckMatchesThePersistedGender(int gender, int expectedTemplate)
    {
        File.WriteAllText(Path.Combine(directory, JsonCharacterStartPackageDao.FileName), """
            { "packages": [{ "professionId": 11, "items": [
              { "placement": 71, "lowId": 121567, "highId": 121567, "quality": 1, "count": 1 },
              { "placement": 72, "lowId": 297665, "highId": 297665, "quality": 1, "count": 1,
                "genderTemplates": [
                  { "gender": 1, "templateId": 297667 },
                  { "gender": 2, "templateId": 297666 },
                  { "gender": 3, "templateId": 297665 }
                ] }
            ] }] }
            """);
        CharacterStartProfessionPackage template = new JsonCharacterStartPackageDao(directory).TryGetPackage(11);
        CharacterStartProfessionPackage selected = template.ResolveForGender(gender);
        CharacterStartPackageItem deck = selected.Items.Single(item => item.Placement == 72);
        Assert.AreEqual(expectedTemplate, deck.LowId);
        Assert.AreEqual(expectedTemplate, deck.HighId);
        Assert.AreEqual(1, deck.Quality);
        Assert.AreEqual(1, deck.Count);
        Assert.AreEqual(297665, template.Items.Single(item => item.Placement == 72).LowId);
        Assert.AreSame(template.Items[0], selected.Items[0]);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedCurrentLoadCanRecoverAfterTheFileIsCorrected(bool malformed)
    {
        Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, directory);
        if (malformed) File.WriteAllText(FilePath, "{broken-json");
        if (malformed) Assert.ThrowsExactly<InvalidDataException>(() => _ = NewCharacterStats.Current);
        else Assert.ThrowsExactly<FileNotFoundException>(() => _ = NewCharacterStats.Current);
        Assert.IsNull(Cache.GetValue(null));

        File.Copy(DefaultFile, FilePath, overwrite: true);
        NewCharacterStats starter = NewCharacterStats.Current;
        Assert.IsNotNull(starter.AbilitiesFor(4));
        Assert.AreSame(starter, Cache.GetValue(null));
    }

    [TestMethod]
    public void SuccessfulCurrentLoadDoesNotReadTheFileAgain()
    {
        Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, directory);
        File.Copy(DefaultFile, FilePath);
        NewCharacterStats starter = NewCharacterStats.Current;
        File.WriteAllText(FilePath, "{now-invalid");
        Assert.AreSame(starter, NewCharacterStats.Current);
    }

    [TestMethod]
    public void AllExpansionsContainsEveryDefinedIndividualFlag()
    {
        int[] flags = Enum.GetValues<Expansions>().Where(value => value != Expansions.All)
            .Select(value => (int)value).ToArray();
        Assert.AreEqual(11, flags.Length);
        Assert.IsTrue(flags.All(value => value > 0 && (value & (value - 1)) == 0));
        Assert.AreEqual(2047, (int)Expansions.All);
        Assert.AreEqual(flags.Aggregate(0, (all, flag) => all | flag), (int)Expansions.All);
    }

    [DataTestMethod]
    [DataRow("0", "0")]
    [DataRow("127", "1")]
    [DataRow("2047", "511")]
    public void ConsoleAccountCreationAlwaysGrantsAllExpansionsAndPreservesOtherFields(string expansions, string gm)
    {
        string[] arguments = { "adduser", "ConsoleFixture", "Console-Password-123!", "8", expansions, gm,
            "console@example.test", "First", "Last" };
        Type program = typeof(CharacterName).Assembly.GetType("LoginEngine.Program", throwOnError: true)!;
        MethodInfo create = program.GetMethod("CreateAccountData", BindingFlags.Static | BindingFlags.NonPublic)!;
        var account = (DBLoginData)create.Invoke(null, new object[] { arguments })!;

        Assert.AreEqual((int)Expansions.All, account.Expansions);
        Assert.AreEqual(arguments[1], account.Username);
        Assert.AreEqual(8, account.AllowedCharacters);
        Assert.AreEqual(arguments[6], account.Email);
        Assert.AreEqual(arguments[7], account.FirstName);
        Assert.AreEqual(arguments[8], account.LastName);
        Assert.AreEqual(int.Parse(gm), account.GM);
        Assert.AreEqual(0, account.Flags);
        Assert.AreEqual(0, account.AccountFlags);
        Assert.IsFalse(string.IsNullOrEmpty(account.Password));
        Assert.AreNotEqual(arguments[2], account.Password);
    }

    [DataTestMethod]
    [DataRow("Stats")]
    [DataRow("Breeds")]
    [DataRow("StartLocation")]
    [DataRow("X")]
    [DataRow("Level")]
    [DataRow("NanoPool")]
    [DataRow("BodyDevelopment")]
    [DataRow("DuplicateStat")]
    [DataRow("DuplicateBreed")]
    [DataRow("EmptyBreeds")]
    [DataRow("ZeroAbility")]
    [DataRow("ZeroLevel")]
    [DataRow("ZeroPlayfield")]
    public void IncompleteCreationDataIsRejected(string defect)
    {
        JsonObject data = JsonNode.Parse(File.ReadAllText(DefaultFile))!.AsObject();
        JsonArray stats = data["Stats"]!.AsArray();
        JsonArray breeds = data["Breeds"]!.AsArray();
        switch (defect)
        {
            case "Stats": case "Breeds": case "StartLocation": data.Remove(defect); break;
            case "X": data["StartLocation"]!.AsObject().Remove("X"); break;
            case "Level": RemoveStat(54); break;
            case "NanoPool": RemoveStat(132); break;
            case "BodyDevelopment": RemoveStat(152); break;
            case "DuplicateStat": stats.Add(stats[0]!.DeepClone()); break;
            case "DuplicateBreed": breeds.Add(breeds[0]!.DeepClone()); break;
            case "EmptyBreeds": breeds.Clear(); break;
            case "ZeroAbility": breeds[0]!["Strength"] = 0; break;
            case "ZeroLevel": stats.Single(s => s!["Stat"]!.GetValue<int>() == 54)!["Value"] = 0; break;
            case "ZeroPlayfield": data["StartLocation"]!["Playfield"] = 0; break;
            default: Assert.Fail("Unknown test case"); break;
        }
        File.WriteAllText(FilePath, data.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() => NewCharacterStats.Load(FilePath));
        void RemoveStat(int id) => stats.Remove(stats.Single(s => s!["Stat"]!.GetValue<int>() == id));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(5)]
    [DataRow(99)]
    public void InvalidBreedIsRejectedBeforeOpeningAConnection(int breed)
    {
        var character = Character(breed);
        AssertRejectedBeforeConnection(() => character.CreateNewChar(false, NewCharacterStats.Load(DefaultFile),
            Account(), Package(), RefuseConnection));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingAccountIsRejectedBeforeOpeningAConnection(bool emptyAccount)
    {
        AssertRejectedBeforeConnection(() => Character(1).CreateNewChar(false, NewCharacterStats.Load(DefaultFile),
            emptyAccount ? new DBLoginData() : null!, Package(), RefuseConnection));
    }

    static CharacterName Character(int breed) => new CharacterName
    {
        AccountName = "creation_fixture", Name = "CreationFixture", Breed = breed, Profession = 1,
        Gender = 0, HeadMesh = 1, MonsterScale = 100, Fatness = 0
    };
    static DBLoginData Account() => new DBLoginData { Id = 1, Username = "creation_fixture", Expansions = (int)Expansions.All };
    static CharacterStartProfessionPackage Package() => new CharacterStartProfessionPackage(1, "fixture", "test",
        Array.Empty<CharacterStartPackageItem>(), Array.Empty<int>());
    static System.Data.IDbConnection RefuseConnection() => throw new AssertFailedException("Invalid creation opened a database connection.");
    static void AssertRejectedBeforeConnection(Action operation)
    {
        Exception? failure = null;
        try { operation(); } catch (Exception exception) { failure = exception; }
        Assert.IsNotNull(failure);
        Assert.IsTrue(failure is InvalidOperationException || failure is ArgumentException,
            "Expected input rejection, got " + failure.GetType().Name);
    }
}
