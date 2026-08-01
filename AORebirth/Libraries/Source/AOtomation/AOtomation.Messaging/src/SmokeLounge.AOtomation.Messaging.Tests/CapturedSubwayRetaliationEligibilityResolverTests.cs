namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class CapturedSubwayRetaliationEligibilityResolverTests
    {
        private static readonly HashSet<int> PromotedMuggerSources = new HashSet<int>
        {
            0x7953AA11,
            0x795450D4,
            0x7957E5C6,
            0x7957E5C7,
            0x7957E5C8
        };

        [TestMethod]
        public void ExactPf127BindingsResolveAllTwentyNineDiscardedPetsAndFiveMuggers()
        {
            OrdinaryEnemyCatalog catalog = BuildCatalog();
            Dictionary<string, OrdinaryEnemyProfile> profiles = catalog.GetProfiles()
                .ToDictionary(value => value.ProfileKey, StringComparer.Ordinal);
            OrdinaryEnemySpawnDefinition[] bindings = catalog.GetSpawns()
                .Where(
                    value => value.PlayfieldInstance == 127
                             && (profiles[value.ProfileKey].DisplayName == "Discarded Pet"
                                 || PromotedMuggerSources.Contains(value.SourceIdentity)))
                .ToArray();

            Assert.AreEqual(
                29,
                bindings.Count(value => profiles[value.ProfileKey].DisplayName == "Discarded Pet"));
            Assert.AreEqual(
                5,
                bindings.Count(value => profiles[value.ProfileKey].DisplayName == "Mugger"));

            foreach (OrdinaryEnemySpawnDefinition spawn in bindings)
            {
                OrdinaryEnemyProfile profile = profiles[spawn.ProfileKey];
                OrdinaryEnemySpawnVariant variant = ResolveVariant(spawn);
                CapturedEnemyCombatContract baseline = profile.Combat.ResolveContract(
                    spawn.SourceIdentity,
                    variant);
                Assert.IsFalse(baseline.Retaliates);

                CapturedEnemyCombatContract resolved;
                string failure;
                Assert.IsTrue(
                    CapturedSubwayRetaliationEligibilityResolver.TryResolveExact(
                        spawn.PlayfieldInstance,
                        profile.DisplayName,
                        profile.MonsterData,
                        variant.Level,
                        spawn.SourceIdentity,
                        baseline,
                        out resolved,
                        out failure),
                    string.Format(
                        "{0} level={1} source=0x{2:X8}: {3}",
                        profile.DisplayName,
                        variant.Level,
                        spawn.SourceIdentity,
                        failure));
                Assert.IsNotNull(resolved);
                Assert.AreNotSame(baseline, resolved);
                Assert.IsFalse(baseline.Retaliates);
                Assert.IsTrue(resolved.Retaliates);
                Assert.IsTrue(resolved.IsCombatReady);
                Assert.AreEqual(NpcAiProfile.Passive, resolved.AiProfile);
                StringAssert.Contains(
                    resolved.Evidence,
                    profile.DisplayName == "Discarded Pet"
                        ? "27 player-attack-to-NPC-Attack retaliation rows"
                        : "20 retaliation rows");
            }
        }

        [TestMethod]
        public void ProductionCallPathUsesExactEligibilityBridgeBeforeCombatPrepare()
        {
            string source = File.ReadAllText(RepositoryPath(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            int exactResolverCall = source.IndexOf(
                "CapturedEnemyCombatContract combatContract = ResolveCombatContractForSpawn(",
                StringComparison.Ordinal);
            int prepare = source.IndexOf(
                "CapturedEnemyCombatRuntime.Prepare(",
                StringComparison.Ordinal);
            Assert.IsTrue(exactResolverCall >= 0);
            Assert.IsTrue(prepare > exactResolverCall);
            StringAssert.Contains(source, "profile.Combat.ResolveContract(");
            StringAssert.Contains(
                source,
                "CapturedSubwayRetaliationEligibilityResolver.TryResolveExact(");
            StringAssert.Contains(source, "retaliationEligibilityPromoted");
        }

        [TestMethod]
        public void ExactBindingGuardRejectsCrossPlayfieldWrongMetadataAndUnknownSources()
        {
            string source = File.ReadAllText(RepositoryPath(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayRetaliationEligibilityResolver.cs"));
            Assert.AreEqual(
                34,
                Regex.Matches(
                    source,
                    @"\{ 0x[0-9A-F]{8}, new CapturedSubwayRetaliationBinding\(").Count);
            Assert.AreEqual(
                29,
                Regex.Matches(
                    source,
                    @"new CapturedSubwayRetaliationBinding\(""Discarded Pet""").Count);
            Assert.AreEqual(
                5,
                Regex.Matches(
                    source,
                    @"new CapturedSubwayRetaliationBinding\(""Mugger""").Count);
            StringAssert.Contains(source, "resourceId != SubwayPlayfieldResourceId");
            StringAssert.Contains(source, "!binding.Matches(name, monsterData, level)");
            StringAssert.Contains(source, "baseline.EvidenceSourceIdentityHint != sourceIdentity");
            StringAssert.Contains(source, "CapturedEnemyCombatProfileCatalog.TryResolve(");
            StringAssert.Contains(source, "!candidate.IsCombatReady");
            Assert.IsFalse(source.Contains("{ 0x7953AD6B, new CapturedSubwayRetaliationBinding("));
            Assert.IsFalse(source.Contains("{ 0x795451FE, new CapturedSubwayRetaliationBinding("));
            StringAssert.Contains(source, "27 player-attack-to-NPC-Attack retaliation rows");
            StringAssert.Contains(source, "41 Discarded Pet");
            StringAssert.Contains(source, "20 retaliation rows");
            StringAssert.Contains(source, "41 Mugger AttackInfo rows");

            OrdinaryEnemyCatalog catalog = BuildCatalog();
            Dictionary<string, OrdinaryEnemyProfile> profiles = catalog.GetProfiles()
                .ToDictionary(value => value.ProfileKey, StringComparer.Ordinal);
            OrdinaryEnemySpawnDefinition spawn = catalog.GetSpawns().First(
                value => value.PlayfieldInstance == 127
                         && profiles[value.ProfileKey].DisplayName == "Discarded Pet");
            OrdinaryEnemyProfile profile = profiles[spawn.ProfileKey];
            OrdinaryEnemySpawnVariant variant = ResolveVariant(spawn);
            CapturedEnemyCombatContract baseline = profile.Combat.ResolveContract(
                spawn.SourceIdentity,
                variant);
            AssertRejected(
                128,
                profile.DisplayName,
                profile.MonsterData,
                variant.Level,
                spawn.SourceIdentity,
                baseline);
            AssertRejected(
                127,
                profile.DisplayName + " mismatch",
                profile.MonsterData,
                variant.Level,
                spawn.SourceIdentity,
                baseline);
            AssertRejected(
                127,
                profile.DisplayName,
                profile.MonsterData + 1,
                variant.Level,
                spawn.SourceIdentity,
                baseline);
            AssertRejected(
                127,
                profile.DisplayName,
                profile.MonsterData,
                variant.Level + 1,
                spawn.SourceIdentity,
                baseline);
            AssertRejected(
                127,
                profile.DisplayName,
                profile.MonsterData,
                variant.Level,
                0x70000001,
                baseline);
        }

        private static void AssertRejected(
            int resourceId,
            string name,
            int monsterData,
            int level,
            int sourceIdentity,
            CapturedEnemyCombatContract baseline)
        {
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsFalse(
                CapturedSubwayRetaliationEligibilityResolver.TryResolveExact(
                    resourceId,
                    name,
                    monsterData,
                    level,
                    sourceIdentity,
                    baseline,
                    out resolved,
                    out failure));
            Assert.AreSame(baseline, resolved);
            Assert.IsFalse(string.IsNullOrWhiteSpace(failure));
        }

        private static OrdinaryEnemyCatalog BuildCatalog()
        {
            return new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider(),
                new CapturedTempleOfThreeWindsContentProvider());
        }

        private static OrdinaryEnemySpawnVariant ResolveVariant(
            OrdinaryEnemySpawnDefinition spawn)
        {
            OrdinaryEnemySpawnVariant[] exact = spawn.LevelDefinition
                .GetExplicitVariants()
                .Where(value => value.Level == spawn.Level)
                .ToArray();
            Assert.IsTrue(exact.Length <= 1);
            return exact.Length == 1
                       ? exact[0]
                       : spawn.LevelDefinition.Resolve(spawn.Level);
        }

        private static string RepositoryPath(string relativePath)
        {
            DirectoryInfo cursor = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (cursor != null)
            {
                string candidate = Path.Combine(cursor.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                cursor = cursor.Parent;
            }

            Assert.Fail("Repository file was not found: " + relativePath);
            return string.Empty;
        }
    }
}
