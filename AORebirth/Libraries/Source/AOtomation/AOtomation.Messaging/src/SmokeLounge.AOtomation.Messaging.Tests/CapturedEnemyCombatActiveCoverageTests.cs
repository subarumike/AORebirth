namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;
    using System.Web.Script.Serialization;

    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class CapturedEnemyCombatActiveCoverageTests
    {
        private const int ExpectedInitialActorCount = 1512;
        private const int ExpectedBindingRecordCount = 1498;
        private static readonly Lazy<Dictionary<string, object>> CoverageDocument =
            new Lazy<Dictionary<string, object>>(LoadCoverageDocument);

        [TestMethod]
        public void EveryFixedActiveHostileBindingIsCertifiedOrHasAnExactUnresolvedAudit()
        {
            Dictionary<string, object> document = ReadCoverageDocument();
            Assert.AreEqual(1, IntMember(document, "schemaVersion"));
            Dictionary<string, object> population = ObjectMember(document, "populationContract");
            Dictionary<string, object> totals = ObjectMember(document, "totals");
            Dictionary<string, object> migration = ObjectMember(
                document,
                "migrationSummary");
            Dictionary<string, object> bindingTotals = ObjectMember(document, "bindingTotals");
            Dictionary<string, object> corpusSearch = ObjectMember(document, "corpusSearch");
            object[] searchedSessions = ArrayMember(corpusSearch, "sessionsSearched");

            Assert.AreEqual(ExpectedInitialActorCount, IntMember(population, "expectedInitialActorCount"));
            Assert.AreEqual(ExpectedInitialActorCount, IntMember(population, "actualInitialActorCount"));
            Assert.AreEqual(ExpectedInitialActorCount, IntMember(totals, "initialActorCount"));
            Assert.AreEqual(1498, IntMember(population, "configuredMaximumActorCount"));
            Assert.AreEqual(IntMember(corpusSearch, "sessionCount"), searchedSessions.Length);
            Assert.IsTrue(searchedSessions.Length > 0);
            Assert.AreEqual(
                searchedSessions.Length,
                searchedSessions.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture))
                    .Distinct(StringComparer.Ordinal).Count(),
                "The common exhaustive capture-search scope contains duplicate sessions.");

            object[] profileObjects = ArrayMember(document, "profiles");
            var profiles = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            foreach (object profileObject in profileObjects)
            {
                Dictionary<string, object> profile = JsonObject(profileObject, "coverage profile");
                string coverageKey = StringMember(profile, "coverageKey");
                Assert.IsFalse(profiles.ContainsKey(coverageKey), "Duplicate coverage profile " + coverageKey);
                profiles.Add(coverageKey, profile);
            }

            var searchedCaptureIds = new HashSet<string>(
                searchedSessions.Select(
                    value => Path.GetFileName(
                        Convert.ToString(value, CultureInfo.InvariantCulture)
                            .Replace('/', Path.DirectorySeparatorChar))),
                StringComparer.Ordinal);
            int unavailableContentEvidenceProfiles = 0;
            bool missingNascenceCitationDocumented = false;
            foreach (Dictionary<string, object> profile in profiles.Values)
            {
                string[] unknownContentEvidence = ArrayMember(
                        profile,
                        "contentEvidenceCaptureIds")
                    .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture))
                    .Where(value => !searchedCaptureIds.Contains(value))
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                if (unknownContentEvidence.Length == 0)
                {
                    continue;
                }

                unavailableContentEvidenceProfiles++;
                missingNascenceCitationDocumented |= unknownContentEvidence.Contains(
                    "20260716-071407",
                    StringComparer.Ordinal);
                Assert.AreEqual("unresolved", StringMember(profile, "classification"));
                CollectionAssert.AreEqual(
                    unknownContentEvidence,
                    ArrayMember(profile, "unavailableContentEvidenceCaptureIds")
                        .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture))
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray());
                string missingEvidence = string.Join(
                    "\n",
                    ArrayMember(profile, "missingEvidence")
                        .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)));
                foreach (string captureId in unknownContentEvidence)
                {
                    StringAssert.Contains(missingEvidence, captureId);
                }
            }

            Assert.IsTrue(
                unavailableContentEvidenceProfiles > 0,
                "Every absent content-cited capture must remain explicitly unresolved.");
            Assert.IsTrue(
                missingNascenceCitationDocumented,
                "The absent Nascence capture citation must be explicit in the quarantine audit.");

            object[] bindings = ArrayMember(document, "bindings");
            Assert.AreEqual(ExpectedBindingRecordCount, bindings.Length);
            Assert.AreEqual(ExpectedBindingRecordCount, IntMember(bindingTotals, "bindingRecordCount"));
            var bindingKeys = new HashSet<string>(StringComparer.Ordinal);
            var referencedProfiles = new HashSet<string>(StringComparer.Ordinal);
            int actorCount = 0;
            int certifiedActors = 0;
            int unresolvedActors = 0;

            foreach (object bindingObject in bindings)
            {
                Dictionary<string, object> binding = JsonObject(bindingObject, "active hostile binding");
                string bindingKey = StringMember(binding, "bindingKey");
                Assert.IsTrue(bindingKeys.Add(bindingKey), "Duplicate active binding " + bindingKey);
                int bindingActorCount = IntMember(binding, "actorCount");
                Assert.IsTrue(bindingActorCount > 0, bindingKey + " has no actor instances.");
                actorCount += bindingActorCount;

                string coverageKey = StringMember(binding, "coverageKey");
                referencedProfiles.Add(coverageKey);
                Dictionary<string, object> profile;
                Assert.IsTrue(
                    profiles.TryGetValue(coverageKey, out profile),
                    bindingKey + " has no exactly matching coverage profile.");
                string classification = StringMember(binding, "classification");
                Assert.AreEqual(StringMember(profile, "classification"), classification, bindingKey);
                Assert.IsTrue(
                    classification == "certified" || classification == "unresolved",
                    bindingKey + " has an unknown or missing classification: " + classification);

                if (classification == "certified")
                {
                    certifiedActors += bindingActorCount;
                    Assert.IsTrue(BoolMember(binding, "runtimeContractReady"), bindingKey);
                    AssertCertifiedBindingResolves(binding, profile, bindingKey);
                }
                else
                {
                    unresolvedActors += bindingActorCount;
                    Assert.IsFalse(BoolMember(binding, "runtimeContractReady"), bindingKey);
                    AssertExactUnresolvedAudit(profile, bindingKey, searchedSessions);
                }
            }

            Assert.AreEqual(ExpectedInitialActorCount, actorCount);
            Assert.AreEqual(ExpectedInitialActorCount, IntMember(bindingTotals, "actorCount"));
            Assert.AreEqual(IntMember(totals, "certified"), certifiedActors);
            Assert.AreEqual(IntMember(totals, "unresolved"), unresolvedActors);
            Assert.AreEqual(15, IntMember(migration, "priorCaptureCertifiedActorCount"));
            Assert.AreEqual(
                certifiedActors - 15,
                IntMember(migration, "newlyCaptureCertifiedActorCount"));
            Assert.AreEqual(
                certifiedActors,
                IntMember(migration, "finalCaptureCertifiedActorCount"));
            Assert.AreEqual(
                unresolvedActors,
                IntMember(migration, "finalQuarantinedActorCount"));
            Assert.AreEqual(
                profiles.Count,
                IntMember(migration, "fixedProfileRowCount"));
            Assert.IsTrue(
                ArrayMember(migration, "restoredEnemyNames").Length > 0,
                "The migration report must identify restored enemy families.");
            Assert.AreEqual(ExpectedInitialActorCount, certifiedActors + unresolvedActors);
            Assert.IsTrue(
                certifiedActors > 15,
                "The active corpus migration must advance beyond the prior 15 certified actors.");
            CollectionAssert.AreEquivalent(
                profiles.Keys.ToArray(),
                referencedProfiles.ToArray(),
                "Every fixed coverage profile must be backed by at least one exact content binding.");

            AssertExactSurfacePopulation(document, certifiedActors, unresolvedActors);
        }

        [TestMethod]
        public void NonDenominatorAuditsIncludeEveryDynamicAndScriptedHostileWithoutChangingTheFixedDenominator()
        {
            Dictionary<string, object> document = ReadCoverageDocument();
            Dictionary<string, object> audit = ObjectMember(document, "nonDenominatorAudit");
            Dictionary<string, object> corpusSearch = ObjectMember(document, "corpusSearch");
            Assert.AreEqual(0, IntMember(audit, "denominatorContribution"));
            Assert.AreEqual(
                0,
                ArrayMember(audit, "recoverableRuntimeBindingBlockers").Length,
                "A capture-ready dynamic mission contract may not remain unbound.");

            Dictionary<string, Dictionary<string, object>> fixedProfiles =
                ArrayMember(document, "profiles")
                    .Select(value => JsonObject(value, "fixed coverage profile"))
                    .ToDictionary(
                        value => StringMember(value, "coverageKey"),
                        StringComparer.Ordinal);

            object[] records = ArrayMember(audit, "records");
            var recordsByFamily = records.Select(
                value => JsonObject(value, "non-denominator audit record"))
                .GroupBy(value => StringMember(value, "auditFamily"), StringComparer.Ordinal)
                .ToDictionary(value => value.Key, value => value.ToArray(), StringComparer.Ordinal);
            Assert.AreEqual(155, recordsByFamily["dynamic-mission-mobs"].Length);
            Assert.AreEqual(14, recordsByFamily["cleaning-robots"].Length);
            Assert.AreEqual(1, recordsByFamily["scripted-hostiles"].Length);
            Assert.AreEqual(170, records.Length);
            Assert.AreEqual(
                27,
                recordsByFamily["cleaning-robots"].Sum(
                    value => IntMember(value, "fixedDenominatorActorCount")),
                "Cleaning-robot supplemental rows must reconcile to their 27 fixed Arete actors.");

            var auditKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Dictionary<string, object> record in recordsByFamily.Values.SelectMany(value => value))
            {
                string auditKey = StringMember(record, "auditKey");
                Assert.IsTrue(auditKeys.Add(auditKey), "Duplicate supplemental audit " + auditKey);
                Assert.AreEqual(0, IntMember(record, "denominatorContribution"), auditKey);
                Assert.IsFalse(string.IsNullOrWhiteSpace(StringMember(record, "name")), auditKey);
                int monsterData = IntMember(record, "monsterData");
                Assert.IsTrue(monsterData >= 0, auditKey);
                if (monsterData == 0)
                {
                    Assert.AreEqual("unresolved", StringMember(record, "classification"), auditKey);
                }
                Assert.IsTrue(ArrayMember(record, "contentSources").Length > 0, auditKey);

                if (StringMember(record, "auditFamily") == "cleaning-robots")
                {
                    string coverageKey = StringMember(
                        record,
                        "fixedDenominatorCoverageKey");
                    Dictionary<string, object> fixedProfile;
                    Assert.IsTrue(
                        fixedProfiles.TryGetValue(coverageKey, out fixedProfile),
                        auditKey + " has no fixed-denominator profile linkage.");
                    Assert.AreEqual(
                        IntMember(fixedProfile, "actorCount"),
                        IntMember(record, "fixedDenominatorActorCount"),
                        auditKey);
                }

                string classification = StringMember(record, "classification");
                Assert.IsTrue(
                    classification == "certified" || classification == "unresolved",
                    auditKey + " has an unknown classification.");
                if (classification == "unresolved")
                {
                    Assert.AreEqual(
                        "corpusSearch.sessionsSearched",
                        StringMember(record, "captureSearchScope"),
                        auditKey);
                    ArrayMember(record, "captureSessions");
                    ArrayMember(record, "evidenceFound");
                    Assert.IsTrue(ArrayMember(record, "missingEvidence").Length > 0, auditKey);
                    if (StringMember(record, "auditFamily") == "dynamic-mission-mobs"
                        || StringMember(record, "auditFamily") == "scripted-hostiles")
                    {
                        Assert.IsFalse(string.IsNullOrWhiteSpace(StringMember(record, "combatProfileKey")), auditKey);
                        Assert.IsFalse(string.IsNullOrWhiteSpace(StringMember(record, "unresolvedReason")), auditKey);
                        Assert.IsFalse(string.IsNullOrWhiteSpace(StringMember(record, "disabledGameplayCapability")), auditKey);
                    }
                    else
                    {
                        Assert.IsTrue(ArrayMember(record, "unresolvedReasons").Length > 0, auditKey);
                        Assert.IsTrue(ArrayMember(record, "disabledGameplayCapabilities").Length > 0, auditKey);
                    }
                }
            }

            Dictionary<string, object> cursed = recordsByFamily["scripted-hostiles"].Single();
            Assert.AreEqual("Cursed Silvertail", StringMember(cursed, "name"));
            Assert.AreEqual(208922, IntMember(cursed, "monsterData"));
            Assert.AreEqual(8, IntMember(cursed, "level"));
            Assert.AreEqual(4677, IntMember(cursed, "runtimePlayfieldOrResource"));
            Assert.AreEqual("unresolved", StringMember(cursed, "classification"));
            Assert.IsFalse(BoolMember(cursed, "capturedContractDataRuntimeReady"));
            Assert.IsFalse(BoolMember(cursed, "runtimeBindingReady"));
            Assert.AreEqual(
                "corpusSearch.sessionsSearched",
                StringMember(cursed, "captureSearchScope"));
            Assert.AreEqual(374, IntMember(cursed, "captureSessionCountSearched"));
            Assert.AreEqual(
                IntMember(corpusSearch, "sessionCount"),
                IntMember(cursed, "captureSessionCountSearched"));
            Assert.AreEqual(0, IntMember(cursed, "matchingEvidenceSessionCount"));
            Assert.IsTrue(BoolMember(cursed, "noMatchingEvidenceAfterExhaustiveSearch"));
            Assert.AreEqual(0, ArrayMember(cursed, "captureSessions").Length);
            Assert.AreEqual(0, ArrayMember(cursed, "evidencePacketIds").Length);
            Assert.AreEqual(0, ArrayMember(cursed, "evidenceFound").Length);
            CollectionAssert.AreEqual(
                new[] { "20260718-185306" },
                StringArrayMember(cursed, "contentEvidenceCaptureIds"));
            CollectionAssert.AreEqual(
                new[] { "20260718-185306" },
                StringArrayMember(cursed, "unavailableContentEvidenceCaptureIds"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "AORebirth/Server/ZoneEngine/Core/Thrak/Quests/ThrakGardenKeySilvertailTransform.cs"
                },
                StringArrayMember(cursed, "contentSources"));
            string missingEvidence = string.Join(
                "\n",
                StringArrayMember(cursed, "missingEvidence"));
            StringAssert.Contains(missingEvidence, "WeaponItemFullUpdate (WIFU)");
            StringAssert.Contains(missingEvidence, "SpecialAttackWeapon");
            StringAssert.Contains(missingEvidence, "Attack packet");
            StringAssert.Contains(missingEvidence, "AttackInfo");
            StringAssert.Contains(missingEvidence, "maximum attack range");
            StringAssert.Contains(missingEvidence, "runtime resolver binding");
            StringAssert.Contains(missingEvidence, "20260718-185306");
            Assert.AreEqual(
                "NPC auto-attack emission and damage application",
                StringMember(cursed, "disabledGameplayCapability"));

            Dictionary<string, object> totals = ObjectMember(document, "totals");
            Assert.AreEqual(ExpectedInitialActorCount, IntMember(totals, "initialActorCount"));
        }

        [TestMethod]
        public void EveryProductionCapturedEnemyCombatPrepareCallSiteHasAnExplicitCoverageAudit()
        {
            Dictionary<string, object> document = ReadCoverageDocument();
            Dictionary<string, object> audit = ObjectMember(document, "runtimePrepareAudit");
            string root = FindRepositoryRoot();
            string productionRoot = Path.Combine(
                root,
                StringMember(audit, "productionRoot")
                    .Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(Directory.Exists(productionRoot));

            var discovered = new Dictionary<string, int>(StringComparer.Ordinal);
            var preparePattern = new Regex(
                @"\bCapturedEnemyCombatRuntime\s*\.\s*Prepare\s*\(",
                RegexOptions.CultureInvariant);
            foreach (string path in Directory.GetFiles(
                productionRoot,
                "*.cs",
                SearchOption.AllDirectories))
            {
                int callCount = preparePattern.Matches(File.ReadAllText(path)).Count;
                if (callCount == 0)
                {
                    continue;
                }

                string relativePath = path.Substring(root.Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/');
                discovered.Add(relativePath, callCount);
            }

            Dictionary<string, Dictionary<string, object>> recorded =
                ArrayMember(audit, "entries")
                    .Select(value => JsonObject(value, "runtime Prepare entry point"))
                    .ToDictionary(
                        value => StringMember(value, "path"),
                        StringComparer.Ordinal);
            CollectionAssert.AreEquivalent(
                discovered.Keys.ToArray(),
                recorded.Keys.ToArray(),
                "A production CapturedEnemyCombatRuntime.Prepare source is missing from the audit.");
            Assert.AreEqual(discovered.Count, IntMember(audit, "entryPointFileCount"));
            Assert.AreEqual(discovered.Values.Sum(), IntMember(audit, "entryPointCount"));
            Assert.AreEqual(18, discovered.Count);
            Assert.AreEqual(20, discovered.Values.Sum());
            foreach (KeyValuePair<string, int> entryPoint in discovered)
            {
                Dictionary<string, object> record = recorded[entryPoint.Key];
                Assert.AreEqual(
                    entryPoint.Value,
                    IntMember(record, "prepareCallCount"),
                    entryPoint.Key);
                Assert.AreEqual(
                    entryPoint.Value,
                    ArrayMember(record, "prepareCallSourceLines").Length,
                    entryPoint.Key);
                Assert.IsTrue(ArrayMember(record, "auditReferences").Length > 0, entryPoint.Key);
                string auditKind = StringMember(record, "auditKind");
                Assert.IsTrue(
                    auditKind == "fixed-denominator-surfaces"
                    || auditKind == "non-denominator-audit",
                    entryPoint.Key);
            }

            Dictionary<string, object> cursedEntry = recorded[
                "AORebirth/Server/ZoneEngine/Core/Thrak/Quests/ThrakGardenKeySilvertailTransform.cs"];
            Assert.AreEqual("non-denominator-audit", StringMember(cursedEntry, "auditKind"));
            CollectionAssert.AreEqual(
                new[] { "scripted-hostiles" },
                StringArrayMember(cursedEntry, "auditReferences"));
        }

        private static void AssertCertifiedBindingResolves(
            IDictionary<string, object> binding,
            IDictionary<string, object> profile,
            string bindingKey)
        {
            int resource = IntMember(binding, "runtimePlayfieldOrResource");
            string name = StringMember(binding, "name");
            int monsterData = IntMember(binding, "monsterData");
            int sourceIdentity = ParseOptionalIdentity(binding, "runtimeSourceIdentityHint");
            object runtimeProfileSelectorValue;
            Assert.IsTrue(
                binding.TryGetValue("runtimeProfileSelector", out runtimeProfileSelectorValue),
                bindingKey + " has no runtime profile selector field.");
            string runtimeProfileSelector = runtimeProfileSelectorValue as string;
            object[] levels = ArrayMember(binding, "levelCandidates");
            object[] levelCoverage = ArrayMember(profile, "levelCoverage");
            Assert.IsTrue(levels.Length > 0, bindingKey);
            Assert.AreEqual(levels.Length, levelCoverage.Length, bindingKey);

            foreach (object levelValue in levels)
            {
                int level = Convert.ToInt32(levelValue, CultureInfo.InvariantCulture);
                Dictionary<string, object>[] matchingLevelRows = levelCoverage
                    .Select(value => JsonObject(value, bindingKey + " level coverage"))
                    .Where(value => IntMember(value, "level") == level)
                    .ToArray();
                Assert.AreEqual(
                    1,
                    matchingLevelRows.Length,
                    bindingKey + " must have exactly one level-coverage row for level=" + level);
                Dictionary<string, object> levelRow = matchingLevelRows[0];
                string resolutionMode = StringMember(levelRow, "resolutionMode");
                CapturedEnemyCombatContract runtimeBaseline;
                if (resolutionMode == "exact-mathematical-combat-setup")
                {
                    var runtimeCatalog = new OrdinaryEnemyCatalog(
                        new CapturedSubwayContentProvider(),
                        new CapturedSubwayOrdinaryContentProvider(),
                        new CapturedTempleOfThreeWindsContentProvider());
                    OrdinaryEnemyProfile runtimeProfile =
                        runtimeCatalog.GetProfiles().Single(
                            value => value.DisplayName == name
                                     && value.MonsterData == monsterData);
                    if (name == "Melded Patterns"
                        || name == "Fragmented Soul")
                    {
                        OrdinaryEnemySpawnDefinition runtimeSpawn =
                            runtimeCatalog.GetSpawns().Single(
                                value => value.PlayfieldInstance == resource
                                         && value.SourceIdentity
                                            == sourceIdentity);
                        OrdinaryEnemySpawnVariant runtimeVariant =
                            runtimeSpawn.LevelDefinition
                                .GetExplicitVariants()
                                .Where(value => value.Level == level)
                                .OrderBy(
                                    value => value.WeaponLoadout == null
                                                 ? int.MinValue
                                                 : value.WeaponLoadout.Quality)
                                .First();
                        runtimeBaseline =
                            runtimeProfile.Combat.ResolveContract(
                                sourceIdentity,
                                runtimeVariant);
                        runtimeBaseline.Retaliates = true;
                        runtimeBaseline.AiProfile =
                            ZoneEngine.Core.NpcAiProfile.Passive;
                    }
                    else
                    {
                        runtimeBaseline =
                            runtimeProfile.Combat.ResolveContract(
                                sourceIdentity,
                                level);
                    }
                }
                else
                {
                    runtimeBaseline =
                        CapturedEnemyCombatContract.Unresolved(
                            "active coverage guard",
                            true);
                }
                Assert.IsFalse(
                    resolutionMode.StartsWith(
                        "reviewed-specialized-baseline-",
                        StringComparison.Ordinal),
                    bindingKey + " bypasses capture-backed maximum attack range.");
                Assert.IsFalse(
                    levelRow.ContainsKey("reviewedRuntimeBaseline"),
                    bindingKey + " retains specialized runtime-baseline bypass metadata.");

                CapturedEnemyCombatContract resolved;
                string failure;
                Assert.IsTrue(
                    CapturedEnemyCombatProfileCatalog.TryResolve(
                        resource,
                        name,
                        monsterData,
                        level,
                        sourceIdentity,
                        runtimeBaseline,
                        out resolved,
                        out failure),
                    bindingKey + " level=" + level + ": " + failure);
                Assert.IsTrue(resolved.IsCombatReady, bindingKey + " level=" + level);
            }
        }

        private static void AssertExactUnresolvedAudit(
            IDictionary<string, object> profile,
            string bindingKey,
            object[] searchedSessions)
        {
            Assert.AreEqual("unresolved", StringMember(profile, "classification"), bindingKey);
            Assert.IsFalse(BoolMember(profile, "runtimeContractReady"), bindingKey);
            Assert.IsFalse(string.IsNullOrWhiteSpace(StringMember(profile, "coverageKey")), bindingKey);
            Assert.IsFalse(string.IsNullOrWhiteSpace(StringMember(profile, "name")), bindingKey);
            Assert.IsTrue(IntMember(profile, "runtimePlayfieldOrResource") > 0, bindingKey);
            Assert.IsTrue(IntMember(profile, "monsterData") > 0, bindingKey);
            Assert.IsTrue(ArrayMember(profile, "levelCandidates").Length > 0, bindingKey);
            Assert.AreEqual(
                "corpusSearch.sessionsSearched",
                StringMember(profile, "captureSearchScope"),
                bindingKey);
            Assert.IsTrue(searchedSessions.Length > 0, bindingKey);
            Assert.AreEqual(
                searchedSessions.Length,
                IntMember(profile, "captureSessionCountSearched"),
                bindingKey);
            object[] matchingSessions = ArrayMember(profile, "captureSessions");
            Assert.AreEqual(
                matchingSessions.Length,
                IntMember(profile, "matchingEvidenceSessionCount"),
                bindingKey);
            Assert.AreEqual(
                matchingSessions.Length == 0,
                BoolMember(profile, "noMatchingEvidenceAfterExhaustiveSearch"),
                bindingKey);
            var searchSet = new HashSet<string>(
                searchedSessions.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)),
                StringComparer.Ordinal);
            Assert.IsTrue(
                matchingSessions.All(
                    value => searchSet.Contains(Convert.ToString(value, CultureInfo.InvariantCulture))),
                bindingKey + " cites a session outside the exhaustive corpus search.");
            ArrayMember(profile, "evidenceFound");
            Assert.IsTrue(ArrayMember(profile, "missingEvidence").Length > 0, bindingKey);
            Assert.IsTrue(ArrayMember(profile, "unresolvedReasons").Length > 0, bindingKey);
            Assert.IsTrue(ArrayMember(profile, "disabledGameplayCapabilities").Length > 0, bindingKey);

            object[] levelCoverage = ArrayMember(profile, "levelCoverage");
            Assert.AreEqual(ArrayMember(profile, "levelCandidates").Length, levelCoverage.Length, bindingKey);
            int unresolvedLevels = 0;
            foreach (object levelObject in levelCoverage)
            {
                Dictionary<string, object> level = JsonObject(levelObject, bindingKey + " level audit");
                string classification = StringMember(level, "classification");
                Assert.IsTrue(classification == "certified" || classification == "unresolved", bindingKey);
                if (classification != "unresolved")
                {
                    continue;
                }

                unresolvedLevels++;
                Assert.IsFalse(BoolMember(level, "runtimeContractReady"), bindingKey);
                Assert.IsFalse(string.IsNullOrWhiteSpace(StringMember(level, "combatProfileKey")), bindingKey);
                ArrayMember(level, "captureSessions");
                ArrayMember(level, "evidenceFound");
                Assert.IsTrue(ArrayMember(level, "missingEvidence").Length > 0, bindingKey);
                Assert.IsFalse(string.IsNullOrWhiteSpace(StringMember(level, "unresolvedReason")), bindingKey);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(StringMember(level, "disabledGameplayCapability")),
                    bindingKey);
            }

            Assert.IsTrue(unresolvedLevels > 0, bindingKey + " has no unresolved level audit.");
        }

        private static void AssertExactSurfacePopulation(
            IDictionary<string, object> document,
            int certifiedActors,
            int unresolvedActors)
        {
            var expected = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "subway-ordinary", 322 },
                { "subway-initial-encounters", 3 },
                { "temple-ordinary", 153 },
                { "temple-named-encounters", 9 },
                { "temple-reanimated-corpse-adds", 2 },
                { "nascence-core-hecklers", 40 },
                { "nascence-life", 837 },
                { "arete-family", 96 },
                { "arete-additional-captured-actors", 12 },
                { "subway-merchants", 6 },
                { "rome-blue-city", 22 },
                { "thrak-omni-garden", 10 }
            };
            object[] surfaces = ArrayMember(document, "surfaces");
            Assert.AreEqual(expected.Count, surfaces.Length);
            int surfaceCertified = 0;
            int surfaceUnresolved = 0;
            foreach (object surfaceObject in surfaces)
            {
                Dictionary<string, object> surface = JsonObject(surfaceObject, "surface coverage");
                string name = StringMember(surface, "surface");
                int actorCount;
                Assert.IsTrue(expected.TryGetValue(name, out actorCount), "Unexpected surface " + name);
                Assert.AreEqual(actorCount, IntMember(surface, "actorCount"), name);
                surfaceCertified += IntMember(surface, "certified");
                surfaceUnresolved += IntMember(surface, "unresolved");
            }

            Assert.AreEqual(certifiedActors, surfaceCertified);
            Assert.AreEqual(unresolvedActors, surfaceUnresolved);
        }

        private static Dictionary<string, object> ReadCoverageDocument()
        {
            return CoverageDocument.Value;
        }

        private static Dictionary<string, object> LoadCoverageDocument()
        {
            string root = FindRepositoryRoot();
            string path = Path.Combine(
                root,
                "docs",
                "generated",
                "capture_backed_npc_combat_active_coverage.json");
            Assert.IsTrue(File.Exists(path), "Missing generated active combat coverage inventory.");
            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 512
            };
            Dictionary<string, object> document = JsonObject(
                serializer.DeserializeObject(File.ReadAllText(path)),
                "coverage document");
            Dictionary<string, object> combatInventory = ObjectMember(document, "combatInventory");
            AssertGeneratedInputHash(root, combatInventory, "combat inventory");
            foreach (object inputObject in ArrayMember(document, "contentInputs"))
            {
                AssertGeneratedInputHash(
                    root,
                    JsonObject(inputObject, "coverage content input"),
                    "coverage content input");
            }

            return document;
        }

        private static void AssertGeneratedInputHash(
            string root,
            IDictionary<string, object> input,
            string context)
        {
            string relativePath = StringMember(input, "path");
            string path = Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), context + " is missing: " + relativePath);
            Assert.AreEqual(
                StringMember(input, "sha256"),
                Sha256File(path),
                context + " is stale: " + relativePath);
        }

        private static string Sha256File(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static Dictionary<string, object> ObjectMember(
            IDictionary<string, object> value,
            string name)
        {
            object member;
            Assert.IsTrue(value.TryGetValue(name, out member), "Missing JSON object member " + name + ".");
            return JsonObject(member, name);
        }

        private static Dictionary<string, object> JsonObject(object value, string context)
        {
            var result = value as Dictionary<string, object>;
            Assert.IsNotNull(result, context + " is not a JSON object.");
            return result;
        }

        private static object[] ArrayMember(IDictionary<string, object> value, string name)
        {
            object member;
            Assert.IsTrue(value.TryGetValue(name, out member), "Missing JSON array member " + name + ".");
            var result = member as object[];
            Assert.IsNotNull(result, name + " is not a JSON array.");
            return result;
        }

        private static string[] StringArrayMember(
            IDictionary<string, object> value,
            string name)
        {
            return ArrayMember(value, name)
                .Select(item => Convert.ToString(item, CultureInfo.InvariantCulture))
                .ToArray();
        }

        private static string StringMember(IDictionary<string, object> value, string name)
        {
            object member;
            Assert.IsTrue(value.TryGetValue(name, out member), "Missing JSON string member " + name + ".");
            string result = member as string;
            Assert.IsNotNull(result, name + " is not a JSON string.");
            return result;
        }

        private static int IntMember(IDictionary<string, object> value, string name)
        {
            object member;
            Assert.IsTrue(value.TryGetValue(name, out member), "Missing JSON integer member " + name + ".");
            return Convert.ToInt32(member, CultureInfo.InvariantCulture);
        }

        private static bool BoolMember(IDictionary<string, object> value, string name)
        {
            object member;
            Assert.IsTrue(value.TryGetValue(name, out member), "Missing JSON Boolean member " + name + ".");
            return Convert.ToBoolean(member, CultureInfo.InvariantCulture);
        }

        private static int ParseOptionalIdentity(IDictionary<string, object> value, string name)
        {
            object member;
            Assert.IsTrue(value.TryGetValue(name, out member), "Missing identity member " + name + ".");
            if (member == null)
            {
                return 0;
            }

            string identity = member as string;
            Assert.IsNotNull(identity, name + " is not a string or null.");
            Assert.IsTrue(identity.StartsWith("0x", StringComparison.Ordinal), name);
            uint parsed;
            Assert.IsTrue(
                uint.TryParse(
                    identity.Substring(2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out parsed),
                name + " has invalid hexadecimal identity " + identity + ".");
            return unchecked((int)parsed);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AI_START_HERE.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            Assert.Fail("Could not locate the AORebirth repository root from the test output path.");
            return string.Empty;
        }
    }
}
