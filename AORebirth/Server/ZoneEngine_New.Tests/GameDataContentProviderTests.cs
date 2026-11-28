namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.GameData;

[TestClass]
public sealed class GameDataContentProviderTests
{
    const string Rules = "Missions/Rules.json";
    const string References = "Missions/References.json";

    [TestMethod]
    public void SelectedRootAndDeclaredOverridePrecedenceAreRecorded()
    {
        using var files = new ContentFiles();
        files.Write(Rules, "{\"Version\":1,\"Value\":1}");
        files.Write(References, "{\"Version\":1,\"Value\":2}");
        files.Write(Rules, "{\"Version\":1,\"Value\":3}", useOverride: true);
        files.Write(References, "{\"Version\":1,\"Value\":4}", useOverride: true);

        var provider = new GameDataContentProvider(files.Root, files.Override);
        var slot = provider.Register(new ContentModule<Values>("missions", 1,
            [new(Rules, AllowOverride: true), new(References)], [],
            input => new(Read(input.Required(Rules)), Read(input.Required(References)))));
        var snapshot = slot.Current;

        Assert.AreEqual(new Values(3, 2), snapshot.Value);
        Assert.AreEqual(ContentFileSource.Override, snapshot.Files.Single(file => file.RelativePath == Rules).Source);
        Assert.AreEqual(ContentFileSource.Base, snapshot.Files.Single(file => file.RelativePath == References).Source);
        Assert.IsTrue(snapshot.Validated);

        File.Delete(files.PathFor(Rules, useOverride: true));
        Assert.AreEqual(new Values(1, 2), slot.Reload().Value);
    }

    [TestMethod]
    public void OptionalAbsenceAndRequiredAbsenceHaveDifferentOutcomes()
    {
        using var files = new ContentFiles();
        files.Write(Rules, "{\"Version\":1,\"Value\":1}");

        var provider = new GameDataContentProvider(files.Root, files.Override);
        var optional = provider.Register(new ContentModule<Values>("missions", 1,
            [new(Rules), new(References, Required: false)], [],
            input => new(Read(input.Required(Rules)), input.Optional(References) is { } bytes ? Read(bytes) : null)));
        var snapshot = optional.Current;
        Assert.AreEqual(new Values(1, null), snapshot.Value);
        Assert.AreEqual(new ContentFileRevision(References, ContentFileSource.Missing, null),
            snapshot.Files.Single(file => file.RelativePath == References));

        File.WriteAllText(files.PathFor(References), "invalid");
        Assert.ThrowsExactly<InvalidDataException>(() => optional.Reload());
        Assert.AreSame(snapshot, optional.Current, "An invalid optional file is not an absent file.");

        var required = new GameDataContentProvider(files.Root, files.Override).Register(Single("required", References));
        File.Delete(files.PathFor(References));
        var error = Assert.ThrowsExactly<InvalidDataException>(() => _ = required.Current);
        StringAssert.Contains(error.Message, References);
        StringAssert.Contains(error.Message, "Required");
        Assert.IsNull(required.Published);
    }

    [TestMethod]
    public void InvalidOverrideNeverFallsThroughAndRejectedSchemaPreservesSnapshot()
    {
        using var files = new ContentFiles();
        files.Write(Rules, "{\"Version\":1,\"Value\":1}");
        var provider = new GameDataContentProvider(files.Root, files.Override);
        var slot = provider.Register(Single("missions", Rules, allowOverride: true));
        ContentSnapshot<Values> before = slot.Current;

        files.Write(Rules, "{invalid", useOverride: true);
        var invalid = Assert.ThrowsExactly<InvalidDataException>(() => slot.Reload());
        StringAssert.Contains(invalid.Message, "missions");
        Assert.AreSame(before, slot.Current);

        files.Write(Rules, "{\"Version\":2,\"Value\":1}", useOverride: true);
        var schema = Assert.ThrowsExactly<InvalidDataException>(() => slot.Reload());
        StringAssert.Contains(schema.Message, "schema");
        Assert.AreSame(before, slot.Current);
    }

    [TestMethod]
    public void SnapshotIdentityBindsExactBytesSchemaSourceAndDependencies()
    {
        using var files = new ContentFiles();
        const string content = "{\"Version\":1,\"Value\":7}";
        files.Write(Rules, content);
        files.Write(References, content);
        var source = new ContentSourceRevision("catalog", "revision-a");
        var provider = new GameDataContentProvider(files.Root, source: source);
        var rules = provider.Register(Single("rules", Rules));
        var slot = provider.Register(Dependent("missions", 1));

        ContentSnapshot<Values> before = slot.Current;
        Assert.AreEqual(source, before.Source);
        Assert.AreEqual(1, before.SchemaVersion);
        Assert.AreEqual(rules.Current.Identity, before.Dependencies.Single().Identity);
        ContentSnapshot<Values> repeated = slot.Reload();
        Assert.AreEqual(before.Identity, repeated.Identity);
        before = repeated;
        Assert.AreEqual(rules.Current.Value, before.Value);

        using var equivalent = new ContentFiles();
        equivalent.Write(Rules, content);
        equivalent.Write(References, content);
        var other = new GameDataContentProvider(equivalent.Root, source: source);
        other.Register(Single("rules", Rules));
        Assert.AreEqual(before.Identity, other.Register(Dependent("missions", 1)).Current.Identity);
        var differentSchema = new GameDataContentProvider(equivalent.Root, source: source);
        differentSchema.Register(Single("rules", Rules));
        Assert.AreNotEqual(before.Identity, differentSchema.Register(Dependent("missions", 2)).Current.Identity);

        files.Write(Rules, content + " ");
        ContentSnapshot<Values> withNewBytes = rules.Reload();
        Assert.AreEqual(before.Value, withNewBytes.Value);
        Assert.AreNotEqual(before.Dependencies.Single().Identity, withNewBytes.Identity);
        Assert.AreSame(before, slot.Current);
        Assert.AreNotEqual(before.Identity, slot.Reload().Identity);

        var differentRevision = new GameDataContentProvider(equivalent.Root, source: source with { Revision = "revision-b" });
        differentRevision.Register(Single("rules", Rules));
        Assert.AreNotEqual(before.Identity, differentRevision.Register(Dependent("missions", 1)).Current.Identity);
    }

    [TestMethod]
    public void DuplicateOwnersPathsAndMissingReferencesAreRejected()
    {
        using var files = new ContentFiles();
        files.Write(Rules, "{\"Version\":1,\"Value\":1}");
        files.Write(References, "{\"Version\":1,\"Value\":2}");

        var provider = new GameDataContentProvider(files.Root);
        provider.Register(Single("rules", Rules));
        Assert.ThrowsExactly<InvalidOperationException>(() => provider.Register(Single("RULES", References)));
        Assert.ThrowsExactly<InvalidOperationException>(() => provider.Register(Single("other", Rules)));
        Assert.ThrowsExactly<ArgumentException>(() => provider.Register(Single("escape", "../outside.json")));

        var slot = provider.Register(Dependent("missions", 1));
        var unresolved = Assert.ThrowsExactly<InvalidDataException>(() => _ = slot.Current);
        StringAssert.Contains(unresolved.Message, "Referenced content");
        Assert.IsNull(slot.Published);

        var missing = new GameDataContentProvider(files.Root).Register(Dependent("missions", 1));
        var unregistered = Assert.ThrowsExactly<InvalidDataException>(() => _ = missing.Current);
        StringAssert.Contains(unregistered.Message, "Unregistered content dependency");

        var cyclic = new GameDataContentProvider(files.Root);
        cyclic.RegisterCompatibility(new("rules", "Existing catalog owner", null, [Rules], ["missions"]));
        var cycle = cyclic.Register(Dependent("missions", 1));
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => _ = cycle.Current).Message, "Cyclic");
    }

    [TestMethod]
    public void CompatibilityDependencyKeepsItsOwnerAndHonestIdentity()
    {
        using var files = new ContentFiles();
        files.Write(Rules, "{\"Version\":1,\"Value\":9}");

        var provider = new GameDataContentProvider(files.Root);
        provider.RegisterCompatibility(new("items", "Existing item catalog", null, ["items.dat"], []));
        var slot = provider.Register(new ContentModule<Values>("missions", 1,
            [new(Rules)], ["items"], input => new(Read(input.Required(Rules)), null)));

        var snapshot = slot.Current;
        var dependency = snapshot.Dependencies.Single();
        Assert.AreEqual("Existing item catalog", dependency.Owner);
        Assert.IsNull(dependency.Identity);
        Assert.IsFalse(dependency.HasValidatedSnapshot);
        Assert.AreEqual("items.dat", dependency.Files.Single());

        var missingDeclaration = provider.Register(new ContentModule<Values>("other", 1,
            [new(References, Required: false)], [], input => new(Read(input.Required(Rules)), null)));
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => _ = missingDeclaration.Current).Message,
            "Undeclared content file");
    }

    [TestMethod]
    public void ValidatorCannotPublishWhileSkippingDeclaredInputs()
    {
        using var files = new ContentFiles();
        files.Write(Rules, "{}");
        var provider = new GameDataContentProvider(files.Root);
        var slot = provider.Register(new ContentModule<Values>("missions", 1,
            [new(Rules)], [], _ => new(1, null)));

        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => _ = slot.Current).Message,
            "did not read declared content: " + Rules);
        Assert.IsNull(slot.Published);
    }

    [TestMethod]
    public async Task ConcurrentReadersSeePreviousCompleteSnapshotUntilReplacementIsPublished()
    {
        using var files = new ContentFiles();
        using var candidateRead = new ManualResetEventSlim();
        using var finishValidation = new ManualResetEventSlim();
        files.Write(Rules, "{\"Version\":1,\"Value\":1}");
        files.Write(References, "{\"Version\":1,\"Value\":1}");

        var provider = new GameDataContentProvider(files.Root);
        var slot = provider.Register(new ContentModule<Values>("missions", 1, [new(Rules), new(References)], [], input =>
        {
            int number = Read(input.Required(Rules));
            int reference = Read(input.Required(References));
            if (number == 2)
            {
                candidateRead.Set();
                if (!finishValidation.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("The fixture did not release the candidate validation.");
            }

            if (number != reference)
                throw new InvalidDataException("Referenced content does not match the rules.");
            return new(number, reference);
        }));

        ContentSnapshot<Values> before = slot.Current;
        files.Write(Rules, "{\"Version\":1,\"Value\":2}");
        files.Write(References, "{\"Version\":1,\"Value\":2}");
        Task<ContentSnapshot<Values>> update = Task.Run(slot.Reload);
        try
        {
            Assert.IsTrue(candidateRead.Wait(TimeSpan.FromSeconds(3)));
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                for (int iteration = 0; iteration < 100; iteration++)
                    Assert.AreSame(before, slot.Current);
            }))).WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            finishValidation.Set();
        }

        ContentSnapshot<Values> after = await update.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreSame(after, slot.Current);
        Assert.AreEqual(new Values(2, 2), after.Value);
        Assert.AreEqual(new Values(1, 1), before.Value);

        files.Write(References, "{\"Version\":1,\"Value\":3}");
        Assert.ThrowsExactly<InvalidDataException>(() => slot.Reload());
        Assert.AreSame(after, slot.Current);
    }

    static ContentModule<Values> Single(string id, string path, bool allowOverride = false)
        => new(id, 1, [new(path, AllowOverride: allowOverride)], [],
            input => new(Read(input.Required(path)), null));

    static ContentModule<Values> Dependent(string id, int version, string path = References)
        => new(id, version, [new(path)], ["rules"], input =>
        {
            Values rules = input.RequireDependency<Values>("rules").Value;
            if (rules.Primary != Read(input.Required(path)))
                throw new InvalidDataException("Referenced content does not match the rules.");
            return rules;
        });

    static int Read(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        if (document.RootElement.GetProperty("Version").GetInt32() != 1)
            throw new InvalidDataException("Unsupported schema.");
        return document.RootElement.GetProperty("Value").GetInt32();
    }

    sealed record Values(int Primary, int? Reference);

    sealed class ContentFiles : IDisposable
    {
        readonly string _base = Path.Combine(Path.GetTempPath(), "aorebirth-content-provider-" + Guid.NewGuid().ToString("N"));

        internal string Root => Path.Combine(_base, "GameData");
        internal string Override => Path.Combine(_base, "Overrides");

        internal string PathFor(string file, bool useOverride = false)
            => Path.Combine(useOverride ? Override : Root, file);

        internal void Write(string file, string value, bool useOverride = false)
        {
            string path = PathFor(file, useOverride);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, value);
        }

        public void Dispose() => Directory.Delete(_base, recursive: true);
    }
}
