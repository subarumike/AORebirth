namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Missions;

[TestClass]
public sealed class MissionContentSnapshotTests
{
    [TestMethod]
    public void MissionSnapshotMeasuresSelectedFilesAndKeepsBaseOnlyPolicy()
    {
        using var fixture = new MissionFiles();
        string policy = fixture.PathFor("Missions/RollPolicy.json");
        string overlay = Path.Combine(fixture.Overrides.FullName, "Missions", "RollPolicy.json");
        Directory.CreateDirectory(Path.GetDirectoryName(overlay)!);
        File.WriteAllText(overlay, "{ invalid overlay");

        var slot = MissionContent.Register(new GameDataContentProvider(fixture.Root.FullName, fixture.Overrides.FullName));
        var snapshot = slot.Current;
        Assert.IsTrue(snapshot.Validated);
        Assert.AreEqual(1, snapshot.SchemaVersion);
        Assert.IsTrue(snapshot.Value.Offers.ResponseCount > 0);
        Assert.AreEqual(snapshot.Value.Offers.ResponseCount * 5, snapshot.Value.Offers.DefinitionCount);
        Assert.IsTrue(snapshot.Value.Destinations.ObservedWorldPositionCount > 0);
        var policyRevision = snapshot.Files.Single(file => file.RelativePath == "Missions/RollPolicy.json");
        Assert.AreEqual(ContentFileSource.Base, policyRevision.Source);
        Assert.AreEqual(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(policy))),
            policyRevision.Sha256);
        Assert.AreEqual(snapshot.Value.Offers.Sha256,
            snapshot.Files.Single(file => file.RelativePath == "Missions/MissionOffers.json").Sha256);
        Assert.AreEqual(snapshot.Identity, slot.Reload().Identity);
    }

    [TestMethod]
    public void InvalidDestinationReferenceRetainsPriorMissionSnapshot()
    {
        using var fixture = new MissionFiles();
        var slot = MissionContent.Register(new GameDataContentProvider(fixture.Root.FullName));
        var previous = slot.Current;

        string path = fixture.PathFor("Missions/Destinations/MissionDestinations.json");
        var content = JsonNode.Parse(File.ReadAllText(path))!;
        content["Pools"]![0]!["DestinationIdentities"]![0]!["IdentityInstance"] = uint.MaxValue;
        File.WriteAllText(path, content.ToJsonString());

        var error = Assert.ThrowsExactly<InvalidDataException>(() => slot.Reload());
        StringAssert.Contains(error.Message, "MissionDestinations.json");
        StringAssert.Contains(error.Message, "absent from placements");
        Assert.AreSame(previous, slot.Current);
    }

    sealed class MissionFiles : IDisposable
    {
        internal DirectoryInfo Root { get; } = Directory.CreateTempSubdirectory("aorebirth-mission-inputs-");
        internal DirectoryInfo Overrides => Root.CreateSubdirectory("override");

        internal MissionFiles()
        {
            string source = GameDataPaths.ResolveRuntimeRoot();
            try
            {
                foreach (var file in MissionContent.Shared.Files)
                {
                    string destination = PathFor(file.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(Path.Combine(source, file.RelativePath), destination);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal string PathFor(string relativePath) => Path.Combine(Root.FullName, relativePath);
        public void Dispose() => Root.Delete(recursive: true);
    }
}
