namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;

    [TestClass]
    public sealed class GetCommandTests
    {
        [TestMethod]
        public void CharacterStatParser_ParsesNameAndId()
        {
            Assert.IsTrue(CharacterStatParser.TryParse("level", out CharacterStat byName));
            Assert.AreEqual(CharacterStat.Level, byName);

            Assert.IsTrue(CharacterStatParser.TryParse("54", out CharacterStat byId));
            Assert.AreEqual(CharacterStat.Level, byId);
        }

        [TestMethod]
        public void CharacterStatParser_RejectsBadToken()
        {
            Assert.IsFalse(CharacterStatParser.TryParse("not-a-stat", out _));
            Assert.IsFalse(CharacterStatParser.TryParse("", out _));
            Assert.IsFalse(CharacterStatParser.TryParse("999999", out _));
        }

        [TestMethod]
        public void GetStatsAomlBuilder_BuildChatLines_ContainsTextLinksPerSet()
        {
            var stats = new StatCollection();
            stats.Set(CharacterStat.Level, 50, StatDetail.Base);
            stats.Set(CharacterStat.Health, 100, StatDetail.Base);

            IReadOnlyList<string> lines = GetStatsAomlBuilder.BuildChatLines(
                "Tester",
                stats,
                Player.FullCharacterStatSets);

            Assert.IsTrue(lines.Count >= 4);
            Assert.IsTrue(lines[0].Contains("text://", System.StringComparison.Ordinal));
            Assert.IsTrue(lines[0].Contains("Set 1", System.StringComparison.Ordinal));
            Assert.IsTrue(lines.Any(line => line.Contains("Set 2", System.StringComparison.Ordinal)));
        }

        [TestMethod]
        public void GetStatsAomlBuilder_ChunkRows_SplitsWhenOverBudget()
        {
            var rows = new List<string>
            {
                "AAAA",
                "BBBB",
                "CCCC",
                "DDDD",
            };

            // Budget forces roughly one row per chunk after the first join attempt.
            IReadOnlyList<string> chunks = GetStatsAomlBuilder.ChunkRows(rows, maxBodyLength: 10);

            Assert.IsTrue(chunks.Count >= 2);
            foreach (string chunk in chunks)
                Assert.IsTrue(chunk.Length <= 10 || !chunk.Contains("<br>", System.StringComparison.Ordinal));
        }

        [TestMethod]
        public void GetStatsAomlBuilder_BuildChatLines_ForcedSmallBudgetProducesChunkLabels()
        {
            var stats = new StatCollection();
            foreach (CharacterStat[] set in Player.FullCharacterStatSets)
            {
                for (int i = 0; i < set.Length; i++)
                    stats.Set(set[i], i + 1, StatDetail.Base);
            }

            IReadOnlyList<string> lines = GetStatsAomlBuilder.BuildChatLines(
                "Tester",
                stats,
                Player.FullCharacterStatSets,
                maxBodyLength: 120);

            Assert.IsTrue(lines.Any(line => line.Contains("(1/", System.StringComparison.Ordinal)));
            Assert.IsTrue(lines.Any(line => line.Contains("text://", System.StringComparison.Ordinal)));
        }
    }
}
