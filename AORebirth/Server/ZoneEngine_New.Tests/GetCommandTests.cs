namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Nanos;

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
                Player.FullCharacterStatSets,
                Player.FullCharacterStatSetNames);

            Assert.AreEqual(Player.FullCharacterStatSets.Count, Player.FullCharacterStatSetNames.Count);
            Assert.IsTrue(lines.Count >= 4);
            Assert.IsTrue(lines[0].Contains("text://", System.StringComparison.Ordinal));
            Assert.IsTrue(lines[0].Contains("Account / Flags", System.StringComparison.Ordinal));
            Assert.IsTrue(lines.Any(line => line.Contains("Skills / Core", System.StringComparison.Ordinal)));
            Assert.IsTrue(lines.Any(line => line.Contains("Appearance", System.StringComparison.Ordinal)));
            Assert.IsTrue(lines.Any(line => line.Contains("Absorb / NCU", System.StringComparison.Ordinal)));
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
                Player.FullCharacterStatSetNames,
                maxBodyLength: 120);

            Assert.IsTrue(lines.Any(line => line.Contains("(1/", System.StringComparison.Ordinal)));
            Assert.IsTrue(lines.Any(line => line.Contains("text://", System.StringComparison.Ordinal)));
        }

        [TestMethod]
        public void GetBuffsAomlBuilder_Empty_ReportsNoBuffs()
        {
            IReadOnlyList<string> lines = GetBuffsAomlBuilder.BuildChatLines(
                "Tester",
                [],
                usedNcu: 0,
                maxNcu: 20,
                nowUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.AreEqual(1, lines.Count);
            Assert.IsTrue(lines[0].Contains("text://(no buffs)", StringComparison.Ordinal));
            Assert.IsTrue(lines[0].Contains("Tester Buffs (0, NCU 0/20)", StringComparison.Ordinal));
        }

        [TestMethod]
        public void GetBuffsAomlBuilder_FormatsActiveBuff()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Buff buff = Buff.Create(
                TestNanos.Create(210123, durationCentiseconds: 12500, ncuCost: 4, strain: 12),
                new Identity { Type = IdentityType.CanbeAffected, Instance = 99 },
                nanoInstance: 3,
                start);

            IReadOnlyList<string> lines = GetBuffsAomlBuilder.BuildChatLines(
                "Tester",
                [buff],
                usedNcu: 4,
                maxNcu: 20,
                nowUtc: start.AddSeconds(5));

            Assert.AreEqual(1, lines.Count);
            Assert.IsTrue(lines[0].Contains("Tester Buffs (1, NCU 4/20)", StringComparison.Ordinal));
            Assert.IsTrue(lines[0].Contains("Nano 210123 (210123)", StringComparison.Ordinal));
            Assert.IsTrue(lines[0].Contains("ncu=4", StringComparison.Ordinal));
            Assert.IsTrue(lines[0].Contains("rem=2:00", StringComparison.Ordinal));
            Assert.IsTrue(lines[0].Contains("strain=12", StringComparison.Ordinal));
            Assert.IsTrue(lines[0].Contains("inst=3", StringComparison.Ordinal));
            Assert.IsTrue(lines[0].Contains("src=99", StringComparison.Ordinal));
        }

        [TestMethod]
        public void GetBuffsAomlBuilder_MarksHostileAndLocked()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Buff debuff = Buff.Create(
                TestNanos.Create(1004, can: AORebirth.Enums.CanFlags.ApplyOnHostile, canCancel: false),
                new Identity { Type = IdentityType.CanbeAffected, Instance = 7 },
                nanoInstance: 1,
                start);

            string row = GetBuffsAomlBuilder.FormatRow(debuff, start);
            Assert.IsTrue(row.Contains(" debuff", StringComparison.Ordinal));
            Assert.IsTrue(row.Contains(" locked", StringComparison.Ordinal));
        }

        [TestMethod]
        public void GetCommand_Buffs_SendsSelfPopupWhenNoTarget()
        {
            Player player = TestWorld.CreatePlayer(1, "Tester");
            player.Name = "Tester";
            player.Stats.Set(CharacterStat.MaxNCU, 40, StatDetail.Base);
            Assert.AreEqual(
                BuffApplyDecision.Apply,
                player.TryApplyBuff(
                    TestNanos.Create(210123, ncuCost: 4),
                    player.Identity,
                    DateTime.UtcNow,
                    out _,
                    out _));

            var session = new RecordingZoneSession();
            new GetCommand().Execute(new GmCommandContext(session, player, ["buffs"]));

            ChatTextMessage? message = session.Sent.OfType<ChatTextMessage>().SingleOrDefault();
            Assert.IsNotNull(message);
            Assert.IsTrue(message.Text.Contains("text://", StringComparison.Ordinal));
            Assert.IsTrue(message.Text.Contains("Tester Buffs (1, NCU 4/40)", StringComparison.Ordinal));
            Assert.IsTrue(message.Text.Contains("210123", StringComparison.Ordinal));
        }
    }
}
