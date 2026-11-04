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

    [DataTestMethod]
    [DataRow(2, 297666)]
    [DataRow(3, 297665)]
    [DataRow(1, 297667)]
    public void NativeCyberdeckVariantIsAvailableInTheStarterPackage(int gender, int expectedId)
    {
        var packages = new JsonCharacterStartPackageDao(Path.Combine(AppContext.BaseDirectory, "GameData"));
        CharacterStartProfessionPackage package = packages.TryGetPackage(11);
        Assert.IsNotNull(package);
        Assert.IsTrue(package.Items.Any(item => item.Placement == 72 && item.LowId == expectedId
            && item.HighId == expectedId && item.Quality == 1 && item.Count == 1 && item.Gender == gender),
            "NT starter package lacks the native cyberdeck for Sex " + gender + " (" + expectedId
            + "); slot 72 currently contains " + string.Join(", ", package.Items.Where(item => item.Placement == 72)
                .Select(item => item.LowId + "/" + item.HighId)) + ".");
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void StarterSelectionPreservesEveryProfessionAndNormalizesEveryBreed(int breed)
    {
        var packages = new JsonCharacterStartPackageDao(Path.Combine(AppContext.BaseDirectory, "GameData"));
        CharacterStartPackageCatalog catalog = packages.LoadCatalog();
        Assert.AreEqual(14, catalog.Packages.Count);
        string[] original = StarterSnapshot(catalog);
        NewCharacterStats starter = NewCharacterStats.Load(DefaultFile);

        foreach (CharacterStartProfessionPackage package in catalog.Packages)
        foreach (int requestedGender in new[] { 1, 2, 3 })
        {
            int gender = starter.AbilitiesFor(breed).Gender ?? requestedGender;
            CharacterStartProfessionPackage selected = package.SelectForGender(gender);
            Assert.AreEqual(package.ProfessionId, selected.ProfessionId);
            Assert.AreEqual(package.Profession, selected.Profession);
            Assert.AreEqual(package.Evidence, selected.Evidence);
            CollectionAssert.AreEqual(package.UploadedNanos.ToArray(), selected.UploadedNanos.ToArray());
            CollectionAssert.AreEqual(package.Items.Where(item => !item.Gender.HasValue).ToArray(),
                selected.Items.Where(item => !item.Gender.HasValue).ToArray());
            Assert.AreEqual(selected.Items.Count, selected.Items.Select(item => item.Placement).Distinct().Count());

            if (package.ProfessionId == 11)
            {
                int expectedId = gender == 1 ? 297667 : gender == 2 ? 297666 : 297665;
                CharacterStartPackageItem cyberdeck = selected.Items.Single(item => item.Placement == 72);
                Assert.AreEqual(expectedId, cyberdeck.LowId);
                Assert.AreEqual(expectedId, cyberdeck.HighId);
                Assert.AreEqual(1, cyberdeck.Quality);
                Assert.AreEqual(1, cyberdeck.Count);
                Assert.AreEqual(9, selected.Items.Count);
            }
            else
                CollectionAssert.AreEqual(package.Items.ToArray(), selected.Items.ToArray());
        }

        Assert.AreSame(catalog, packages.LoadCatalog());
        CollectionAssert.AreEqual(original, StarterSnapshot(packages.LoadCatalog()));
    }

    [TestMethod]
    public void LegacyStarterItemConstructorRemainsAvailableAndUnconditional()
    {
        ConstructorInfo? constructor = typeof(CharacterStartPackageItem).GetConstructor(new[]
            { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) });
        Assert.IsNotNull(constructor);
        var item = (CharacterStartPackageItem)constructor.Invoke(new object[] { 64, 100, 101, 5, 25 });
        Assert.AreEqual(64, item.Placement);
        Assert.AreEqual(100, item.LowId);
        Assert.AreEqual(101, item.HighId);
        Assert.AreEqual(5, item.Quality);
        Assert.AreEqual(25, item.Count);
        Assert.IsNull(item.Gender);
    }

    [TestMethod]
    public void UnconditionalStarterRowsAndUploadedNanosRemainAvailableForEveryGender()
    {
        WriteStarterItems(new JsonArray { StarterItem(), StarterItem(65) });
        var packages = new JsonCharacterStartPackageDao(directory);
        CharacterStartProfessionPackage package = packages.TryGetPackage(11);
        foreach (int gender in new[] { 1, 2, 3 })
        {
            CharacterStartProfessionPackage selected = package.SelectForGender(gender);
            CollectionAssert.AreEqual(package.Items.ToArray(), selected.Items.ToArray());
            CollectionAssert.AreEqual(new[] { 1234 }, selected.UploadedNanos.ToArray());
            Assert.IsTrue(selected.Items.All(item => !item.Gender.HasValue));
        }
    }

    [TestMethod]
    public void DisjointStarterGenderSelectorsShareOnePlacementWithoutMutatingTheCache()
    {
        WriteStarterItems(new JsonArray { StarterItem(), StarterItem(65, 1), StarterItem(65, 2), StarterItem(65, 3) });
        var packages = new JsonCharacterStartPackageDao(directory);
        CharacterStartPackageCatalog catalog = packages.LoadCatalog();
        string[] original = StarterSnapshot(catalog);
        foreach (int gender in new[] { 1, 2, 3 })
        {
            CharacterStartProfessionPackage selected = packages.TryGetPackage(11).SelectForGender(gender);
            Assert.AreEqual(2, selected.Items.Count);
            Assert.AreEqual(gender, selected.Items.Single(item => item.Placement == 65).Gender);
            Assert.IsNull(selected.Items.Single(item => item.Placement == 64).Gender);
        }
        CollectionAssert.AreEqual(original, StarterSnapshot(packages.LoadCatalog()));
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(4)]
    [DataRow(99)]
    public void InvalidStarterGenderSelectorsAreRejected(int gender)
    {
        WriteStarterItems(new JsonArray { StarterItem(64, gender) });
        Assert.ThrowsExactly<InvalidDataException>(() => new JsonCharacterStartPackageDao(directory).LoadCatalog());
    }

    [TestMethod]
    public void MalformedStarterGenderSelectorIsRejected()
    {
        JsonObject item = StarterItem();
        item["gender"] = "unknown";
        WriteStarterItems(new JsonArray { item });
        Assert.ThrowsExactly<InvalidDataException>(() => new JsonCharacterStartPackageDao(directory).LoadCatalog());
    }

    [DataTestMethod]
    [DataRow("duplicate")]
    [DataRow("unconditional-first")]
    [DataRow("unconditional-last")]
    [DataRow("unconditional-only")]
    public void OverlappingStarterPlacementsAreRejected(string overlap)
    {
        int? first = overlap == "unconditional-first" || overlap == "unconditional-only" ? null : 2;
        int? second = overlap == "unconditional-last" || overlap == "unconditional-only" ? null : 2;
        WriteStarterItems(new JsonArray { StarterItem(64, first), StarterItem(64, second) });
        Assert.ThrowsExactly<InvalidDataException>(() => new JsonCharacterStartPackageDao(directory).LoadCatalog());
    }

    static JsonObject StarterItem(int placement = 64, int? gender = null)
    {
        var item = new JsonObject
        {
            ["placement"] = placement, ["lowId"] = 100, ["highId"] = 100,
            ["quality"] = 1, ["count"] = 1
        };
        if (gender.HasValue)
            item["gender"] = gender.Value;
        return item;
    }

    void WriteStarterItems(JsonArray items)
    {
        var document = new JsonObject
        {
            ["packages"] = new JsonArray
            {
                new JsonObject
                {
                    ["professionId"] = 11, ["profession"] = "fixture", ["evidence"] = "test",
                    ["items"] = items, ["uploadedNanos"] = new JsonArray { 1234 }
                }
            }
        };
        File.WriteAllText(Path.Combine(directory, JsonCharacterStartPackageDao.FileName), document.ToJsonString());
    }

    static string[] StarterSnapshot(CharacterStartPackageCatalog catalog) => catalog.Packages.SelectMany(package =>
        package.Items.Select(item => string.Join("/", package.ProfessionId, item.Placement, item.LowId, item.HighId,
            item.Quality, item.Count, item.Gender?.ToString() ?? "unconditional"))).ToArray();

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
