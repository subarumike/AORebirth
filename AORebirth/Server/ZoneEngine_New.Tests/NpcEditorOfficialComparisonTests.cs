namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using AORebirth.NpcEditor.Official;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class NpcEditorOfficialComparisonTests
{
    const string Catalog = """
        {
          "SAME": {"Templates":[{"Name":"Fixture NPC","Level":26,"Stats":{"359":400,"1":200,"27":200}}]},
          "MATCH": {"Templates":[{"Name":"Fixture NPC","Level":25,"Stats":{"359":300,"1":250,"27":250}}]},
          "OTHER": {"Templates":[{"Name":"Other NPC","Level":25,"Stats":{"359":500,"1":300}}]},
          "FAMILY":{"Children":["MATCH","OTHER"]}
        }
        """;

    [TestMethod]
    public void NameAndMonsterDataRankCandidateWithoutInferringHiddenHealth()
    {
        using JsonDocument catalog = JsonDocument.Parse(Catalog);
        var snapshot = new OfficialSnapshot { Name = "Fixture NPC", Level = 25, MonsterData = 300 };
        OfficialComparisonResult result = OfficialNpcComparison.Compare(catalog.RootElement, snapshot, null, CancellationToken.None);
        Assert.AreEqual(2, result.Total);
        Assert.AreEqual("MATCH", result.Candidates[0].TemplateHash);
        Assert.AreEqual("Name and MonsterData candidate", result.Candidates[0].Match);
        OfficialComparisonRow hp = result.Candidates[0].Rows.Single(row => row.Property == "Maximum health");
        Assert.AreEqual("NOT EXPOSED", hp.Observation);
        Assert.AreEqual("NOT_AVAILABLE", hp.ObservationSource);
        Assert.AreEqual("250", hp.Configured);
        Assert.AreEqual("Name candidate", result.Candidates[1].Match);
    }

    [TestMethod]
    public async Task ExplicitTemplateLookupReadsTheSelectedCatalogWithoutChangingIt()
    {
        DirectoryInfo fixture = Directory.CreateTempSubdirectory("aorebirth-official-comparison-");
        string file = Path.Combine(fixture.FullName, "NpcTemplates.json");
        try
        {
            await File.WriteAllTextAsync(file, Catalog);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            OfficialComparisonResult result = await OfficialNpcComparison.ReadAsync(file, new OfficialSnapshot(), "OTHER", deadline.Token);
            OfficialComparisonCandidate candidate = result.Candidates.Single();
            Assert.AreEqual("OTHER", candidate.TemplateHash);
            Assert.AreEqual("Requested AORebirth hash", candidate.Match);
            Assert.AreEqual("UNKNOWN", candidate.Rows.Single(row => row.Property == "MonsterData").Observation);
            Assert.AreEqual(Catalog, await File.ReadAllTextAsync(file, deadline.Token));
        }
        finally
        {
            File.Delete(file);
            fixture.Delete();
        }
    }
}
