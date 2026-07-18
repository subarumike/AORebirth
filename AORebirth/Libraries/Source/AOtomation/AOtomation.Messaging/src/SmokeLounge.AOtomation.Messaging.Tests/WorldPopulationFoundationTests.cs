namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Core.Playfields;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class WorldPopulationFoundationTests
    {
        [TestMethod] public void DefinitionsValidateAndSortDeterministically() { WorldSpawnDefinition[] rows = { Spawn("b", 2), Spawn("a", 1) }; WorldPopulationDefinitionValidator.Validate(rows, new[] { Group("g", "a", "b") }, new[] { Fixed("p", 60) }, new[] { "profile" }); CollectionAssert.AreEqual(new[] { "a", "b" }, rows.OrderBy(x => x.SpawnKey, StringComparer.Ordinal).Select(x => x.SpawnKey).ToArray()); }
        [TestMethod] public void DuplicateSpawnAndIdentityFailClosed() { AssertThrows(() => Validate(new[] { Spawn("x", 1), Spawn("x", 2) })); AssertThrows(() => Validate(new[] { Spawn("x", 1), Spawn("y", 1) })); }
        [TestMethod] public void MissingProfilePolicyAndInvalidLocationFailClosed() { WorldSpawnDefinition x = Spawn("x", 1); x.EnemyProfileKey = "missing"; AssertThrows(() => Validate(new[] { x })); x = Spawn("x", 1); x.RespawnPolicyKey = "missing"; AssertThrows(() => Validate(new[] { x })); x = Spawn("x", 1); x.PlayfieldId = 0; AssertThrows(() => Validate(new[] { x })); x = Spawn("x", 1); x.X = float.NaN; AssertThrows(() => Validate(new[] { x })); }
        [TestMethod] public void InvalidRangeSummonsAndScriptedDefinitionsFailClosed() { RespawnPolicyDefinition range = Fixed("p", 1); range.Mode = WorldRespawnMode.RandomDelayRange; range.MinimumDelaySeconds = 10; range.MaximumDelaySeconds = 5; AssertThrows(() => WorldPopulationDefinitionValidator.Validate(new[] { Spawn("x", 1) }, new[] { Group("g", "x") }, new[] { range }, new[] { "profile" })); WorldSpawnDefinition x = Spawn("x", 1); x.OwnedSummon = true; AssertThrows(() => Validate(new[] { x })); x = Spawn("x", 1); x.BossOrScripted = true; AssertThrows(() => Validate(new[] { x })); }
        [TestMethod] public void MissingGroupRespawnPolicyFailsClosed() { SpawnGroupDefinition group = Group("g", "x"); group.SharedRespawnPolicyKey = "missing"; AssertThrows(() => WorldPopulationDefinitionValidator.Validate(new[] { Spawn("x", 1) }, new[] { group }, new[] { Fixed("p", 60) }, new[] { "profile" })); group.SharedRespawnPolicyKey = "p"; WorldPopulationDefinitionValidator.Validate(new[] { Spawn("x", 1) }, new[] { group }, new[] { Fixed("p", 60) }, new[] { "profile" }); }
        [TestMethod] public void SchedulerOrderingIsStableAndBounded() { var s = new WorldRespawnScheduler(); DateTime due = new DateTime(2030, 1, 1); s.Schedule(Schedule("z", 1, due)); s.Schedule(Schedule("a", 2, due)); s.Schedule(Schedule("b", 1, due)); CollectionAssert.AreEqual(new[] { "b", "z" }, s.TakeDue(due, 2).Select(x => x.SpawnKey).ToArray()); Assert.AreEqual(1, s.Count); }
        [TestMethod] public void SchedulerPreventsDuplicatesAndSupportsCancellationScopes() { var s = new WorldRespawnScheduler(); DateTime due = DateTime.UtcNow; Assert.IsTrue(s.Schedule(Schedule("a", 1, due))); Assert.IsFalse(s.Schedule(Schedule("a", 1, due))); Assert.IsTrue(s.Cancel("a")); s.Schedule(Schedule("a", 1, due)); s.Schedule(Schedule("b", 2, due)); s.CancelPlayfield(1); Assert.IsFalse(s.Contains("a")); Assert.IsTrue(s.Contains("b")); }
        [TestMethod] public void FixedAndRandomDelaysAreDeterministic() { Assert.AreEqual(60, WorldRespawnScheduler.SelectDelay(Fixed("p", 60), null).TotalSeconds); RespawnPolicyDefinition range = Fixed("r", 1); range.Mode = WorldRespawnMode.RandomDelayRange; range.FixedDelaySeconds = null; range.MinimumDelaySeconds = 10; range.MaximumDelaySeconds = 20; Assert.AreEqual(12.5, WorldRespawnScheduler.SelectDelay(range, new FixedRandom(0.25)).TotalSeconds); AssertThrows(() => WorldRespawnScheduler.SelectDelay(range, null)); var state = new PopulationRuntimeState { SpawnKey = "random", PlayfieldId = 127, Generation = 1, CurrentRuntimeIdentity = Identity.None }; Assert.IsFalse(WorldRespawnScheduler.TryScheduleForLifecycle(new WorldRespawnScheduler(), state, range, RespawnDelayStartsAt.NpcDespawn, DateTime.UtcNow, new FixedRandom(double.NaN))); }
        [TestMethod] public void ArchitectureGuardrailsKeepPacketsLootAndPerSpawnTimersOut() { string root = FindRepositoryRoot(); string controller = Read(root, "WorldPopulationController.cs"); string scheduler = Read(root, "WorldRespawnScheduler.cs"); string ordinary = Read(root, "OrdinaryEnemyRuntimeService.cs"); Assert.IsFalse(controller.Contains("MessageHandler") || controller.Contains("LootGenerationService") || controller.Contains("System.Threading.Timer")); Assert.IsFalse(scheduler.Contains("System.Threading.Timer")); Assert.IsFalse(ordinary.Contains("ScheduleRespawnAfterDespawn") || ordinary.Contains("pendingRespawns")); }
        [TestMethod] public void MigrationUsesControllerNotificationsDataQuarantineAndDbAdapter() { string root = FindRepositoryRoot(); string controller = Read(root, "WorldPopulationController.cs"); string npc = Read(root, "NPCRuntimeService.cs"); string db = Read(root, "PlayfieldDbMobSpawnRuntimeService.cs"); Assert.IsTrue(controller.Contains("Enabled = row.Disposition == OrdinaryEnemyRuntimeDisposition.Active") && controller.Contains("Quarantined = row.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined")); Assert.IsTrue(npc.Contains("this.worldPopulation.ActivatePlayfield") && npc.Contains("this.worldPopulation.NotifyDeath") && npc.Contains("this.worldPopulation.NotifyNpcDespawn")); Assert.IsTrue(db.Contains("WorldSpawnDefinition AdaptDefinition") && db.Contains("ActivationPolicy = WorldSpawnActivationPolicy.Disabled")); }

        [TestMethod]
        public void FixedAndInclusiveLevelDefinitionsAreDeterministicAndValidated()
        {
            OrdinaryEnemySpawnLevelDefinition fixedLevel = OrdinaryEnemySpawnLevelDefinition.Fixed(
                new OrdinaryEnemySpawnVariant(5, 115, 0, 93, 20, "fixed"),
                OrdinaryEnemyEvidenceState.Observed,
                "fixed-capture");
            Assert.IsTrue(fixedLevel.IsValid);
            Assert.AreEqual(OrdinaryEnemySpawnLevelMode.Fixed, fixedLevel.Mode);
            Assert.AreEqual(5, fixedLevel.SelectVariant(value => { throw new InvalidOperationException("Fixed levels must not consume randomness."); }).Level);

            OrdinaryEnemySpawnLevelDefinition range = Range();
            Assert.IsTrue(range.IsValid);
            Assert.AreEqual(15, range.SelectVariant(value => 0).Level);
            Assert.AreEqual(25, range.SelectVariant(value => value - 1).Level);
            Assert.AreEqual(17, range.SelectVariant(value => 2).Level);

            Assert.IsFalse(new OrdinaryEnemySpawnLevelDefinition(
                OrdinaryEnemySpawnLevelMode.InclusiveRange, 25, 15, 24, 691, 33, 0, 70, 83, 3,
                OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration,
                OrdinaryEnemyEvidenceState.Policy, "bad-bounds").IsValid);
            Assert.IsFalse(new OrdinaryEnemySpawnLevelDefinition(
                OrdinaryEnemySpawnLevelMode.InclusiveRange, 0, 25, 24, 691, 33, 0, 70, 83, 3,
                OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration,
                OrdinaryEnemyEvidenceState.Policy, "bad-level").IsValid);
            Assert.IsFalse(new OrdinaryEnemySpawnLevelDefinition(
                OrdinaryEnemySpawnLevelMode.InclusiveRange, 15, 25, 24, 691, 33, 0, 70, 83, 3,
                OrdinaryEnemyLevelRerollPolicy.Invalid,
                OrdinaryEnemyEvidenceState.Policy, "bad-reroll").IsValid);
            Assert.IsFalse(new OrdinaryEnemySpawnLevelDefinition(
                OrdinaryEnemySpawnLevelMode.InclusiveRange, 1, int.MaxValue, 1, int.MaxValue, int.MaxValue, 0, 70, 83, 3,
                OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration,
                OrdinaryEnemyEvidenceState.Policy, "overflowing-derived-stats").IsValid);
        }

        [TestMethod]
        public void LevelSelectionIsStableWithinGenerationAndRerollsOnlyForANewGeneration()
        {
            int selectorCalls = 0;
            int[] offsets = { 2, 8 };
            var state = new OrdinaryEnemyLevelSelectionState();
            OrdinaryEnemySpawnGeneration initial = state.ResolveForGeneration(
                Range(),
                1,
                value => offsets[selectorCalls++]);
            OrdinaryEnemySpawnGeneration visibilityReentry = state.ResolveForGeneration(Range(), 1, value => offsets[selectorCalls++]);
            OrdinaryEnemySpawnGeneration combatReset = state.ResolveForGeneration(Range(), 1, value => offsets[selectorCalls++]);
            OrdinaryEnemySpawnGeneration corpseTransition = state.ResolveForGeneration(Range(), 1, value => offsets[selectorCalls++]);
            Assert.AreSame(initial, visibilityReentry, "Visibility re-entry must retain the generation selection.");
            Assert.AreSame(initial, combatReset, "Combat reset must retain the generation selection.");
            Assert.AreSame(initial, corpseTransition, "Corpse transition must retain the generation selection.");
            Assert.AreEqual(1, selectorCalls);
            Assert.AreEqual(17, initial.SelectedVariant.Level);

            OrdinaryEnemySpawnGeneration respawn = state.ResolveForGeneration(
                Range(),
                2,
                value => offsets[selectorCalls++]);
            Assert.AreEqual(23, respawn.SelectedVariant.Level);
            Assert.AreEqual(2, selectorCalls);
            Assert.AreNotSame(initial, respawn);
            AssertThrows(() => state.ResolveForGeneration(Range(), 1, value => 0));
        }

        [TestMethod]
        public void FixedLevelAndDerivedStatsRemainStableAcrossRespawnGenerations()
        {
            OrdinaryEnemySpawnLevelDefinition fixedLevel = OrdinaryEnemySpawnLevelDefinition.Fixed(
                new OrdinaryEnemySpawnVariant(9, 247, 7, 94, 31, "captured-row"),
                OrdinaryEnemyEvidenceState.Observed,
                "captured-row");
            var state = new OrdinaryEnemyLevelSelectionState();
            OrdinaryEnemySpawnGeneration first = state.ResolveForGeneration(fixedLevel, 1, null);
            OrdinaryEnemySpawnGeneration second = state.ResolveForGeneration(fixedLevel, 2, null);
            Assert.AreEqual(9, first.SelectedVariant.Level);
            Assert.AreEqual(9, second.SelectedVariant.Level);
            Assert.AreEqual(247, second.SelectedVariant.Health);
            Assert.AreEqual(7, second.SelectedVariant.HealthDamage);
            Assert.AreEqual(94, second.SelectedVariant.MonsterScale);
            Assert.AreEqual(31, second.SelectedVariant.RunSpeed);

            OrdinaryEnemySpawnVariant ranged = Range().Resolve(25);
            Assert.AreEqual(724, ranged.Health);
            Assert.AreEqual(86, ranged.RunSpeed);
            Assert.AreEqual(70, ranged.MonsterScale);
        }

        [TestMethod]
        public void OrdinaryRespawnPolicyHonorsSpawnGroupDefaultAndNoRespawnPrecedence()
        {
            RespawnPolicyDefinition ordinaryDefault = Fixed("ordinary.default", 240);
            RespawnPolicyDefinition groupPolicy = Fixed("group.explicit", 120);
            RespawnPolicyDefinition spawnPolicy = Fixed("spawn.explicit", 60);

            WorldRespawnPolicyResolution inherited = WorldRespawnPolicyResolver.Resolve(
                WorldPopulationClassification.OrdinaryEnemy,
                WorldRespawnPolicyAssignment.Inherit("ordinary"),
                null,
                ordinaryDefault);
            Assert.AreEqual(WorldRespawnPolicyResolutionSource.OrdinaryDefault, inherited.Source);
            Assert.AreEqual(240, inherited.Policy.FixedDelaySeconds.Value);

            WorldRespawnPolicyResolution grouped = WorldRespawnPolicyResolver.Resolve(
                WorldPopulationClassification.OrdinaryEnemy,
                WorldRespawnPolicyAssignment.Inherit("ordinary"),
                WorldRespawnPolicyAssignment.Explicit(groupPolicy),
                ordinaryDefault);
            Assert.AreEqual(WorldRespawnPolicyResolutionSource.ExplicitGroup, grouped.Source);
            Assert.AreEqual(120, grouped.Policy.FixedDelaySeconds.Value);

            WorldRespawnPolicyResolution explicitSpawn = WorldRespawnPolicyResolver.Resolve(
                WorldPopulationClassification.OrdinaryEnemy,
                WorldRespawnPolicyAssignment.Explicit(spawnPolicy),
                WorldRespawnPolicyAssignment.Explicit(groupPolicy),
                ordinaryDefault);
            Assert.AreEqual(WorldRespawnPolicyResolutionSource.ExplicitSpawnOrArchetype, explicitSpawn.Source);
            Assert.AreEqual(60, explicitSpawn.Policy.FixedDelaySeconds.Value);

            WorldRespawnPolicyResolution noRespawn = WorldRespawnPolicyResolver.Resolve(
                WorldPopulationClassification.OrdinaryEnemy,
                WorldRespawnPolicyAssignment.NoRespawn("ordinary.no-respawn.test", "captured-no-respawn", "OBSERVED"),
                WorldRespawnPolicyAssignment.Explicit(groupPolicy),
                ordinaryDefault);
            Assert.AreEqual(WorldRespawnPolicyResolutionSource.ExplicitNoRespawn, noRespawn.Source);
            Assert.AreEqual(WorldRespawnMode.None, noRespawn.Policy.Mode);
        }

        [TestMethod]
        public void PopulationControllerResolvesConfiguredGroupPoliciesAndRejectsConflictingKeys()
        {
            RespawnPolicyDefinition groupPolicy = Fixed("group.explicit", 120);
            var policies = new Dictionary<string, RespawnPolicyDefinition>(StringComparer.Ordinal);
            var configured = new Dictionary<string, RespawnPolicyDefinition>(StringComparer.Ordinal)
            {
                { "g", groupPolicy }
            };
            SpawnGroupDefinition group = Group("g", "enemy");
            WorldRespawnPolicyResolver.ApplyGroupConfiguration(group, configured, policies);
            Assert.AreEqual(groupPolicy.RespawnPolicyKey, group.SharedRespawnPolicyKey);

            WorldRespawnPolicyAssignment assignment = WorldRespawnPolicyResolver.ResolveGroupAssignment(group, policies);
            WorldRespawnPolicyResolution resolution = WorldRespawnPolicyResolver.Resolve(
                WorldPopulationClassification.OrdinaryEnemy,
                WorldRespawnPolicyAssignment.Inherit("ordinary"),
                assignment,
                Fixed("ordinary.default", 240));
            Assert.AreEqual(WorldRespawnPolicyResolutionSource.ExplicitGroup, resolution.Source);
            Assert.AreEqual(120, resolution.Policy.FixedDelaySeconds.Value);

            group.SharedRespawnPolicyKey = "missing";
            Assert.AreEqual(
                WorldRespawnPolicyAssignmentMode.Unresolved,
                WorldRespawnPolicyResolver.ResolveGroupAssignment(group, policies).Mode);

            var registered = new Dictionary<string, RespawnPolicyDefinition>(StringComparer.Ordinal);
            RespawnPolicyDefinition first = Fixed("shared", 60);
            WorldRespawnPolicyValidator.RegisterOrRejectConflict(registered, first);
            WorldRespawnPolicyValidator.RegisterOrRejectConflict(registered, Fixed("shared", 60));
            Assert.AreEqual(1, registered.Count);
            AssertThrows(() => WorldRespawnPolicyValidator.RegisterOrRejectConflict(registered, Fixed("shared", 61)));

            RespawnPolicyDefinition disabledGroup = Fixed("group.disabled", 90);
            disabledGroup.Enabled = false;
            Assert.AreEqual(
                WorldRespawnPolicyResolutionSource.Unresolved,
                WorldRespawnPolicyResolver.Resolve(
                    WorldPopulationClassification.OrdinaryEnemy,
                    WorldRespawnPolicyAssignment.Inherit("ordinary"),
                    WorldRespawnPolicyAssignment.Explicit(disabledGroup),
                    Fixed("ordinary.default", 240)).Source);
        }

        [TestMethod]
        public void ExcludedClassificationsNeverInheritTheOrdinaryDefault()
        {
            WorldPopulationClassification[] excluded =
                {
                    WorldPopulationClassification.NamedEnemy,
                    WorldPopulationClassification.Boss,
                    WorldPopulationClassification.ScriptedEncounter,
                    WorldPopulationClassification.Summon,
                    WorldPopulationClassification.Pet,
                    WorldPopulationClassification.TemporaryEncounterAdd,
                    WorldPopulationClassification.Vendor,
                    WorldPopulationClassification.StaticObject,
                    WorldPopulationClassification.Container,
                    WorldPopulationClassification.QuestOwned
                };
            foreach (WorldPopulationClassification classification in excluded)
            {
                WorldRespawnPolicyResolution result = WorldRespawnPolicyResolver.Resolve(
                    classification,
                    WorldRespawnPolicyAssignment.Inherit("not-ordinary"),
                    null,
                    Fixed("ordinary.default", 240));
                Assert.AreEqual(WorldRespawnPolicyResolutionSource.ExcludedClassification, result.Source, classification.ToString());
                Assert.AreEqual(WorldRespawnMode.None, result.Policy.Mode, classification.ToString());
            }

            WorldRespawnPolicyResolution namedExplicit = WorldRespawnPolicyResolver.Resolve(
                WorldPopulationClassification.NamedEnemy,
                WorldRespawnPolicyAssignment.Explicit(Fixed("named.explicit", 600)),
                null,
                Fixed("ordinary.default", 240));
            Assert.AreEqual(WorldRespawnPolicyResolutionSource.ExplicitSpawnOrArchetype, namedExplicit.Source);
            Assert.AreEqual(600, namedExplicit.Policy.FixedDelaySeconds.Value);
        }

        [TestMethod]
        public void UnsupportedAndInvalidRespawnPoliciesFailClosed()
        {
            WorldRespawnPolicyResolution unsupported = WorldRespawnPolicyResolver.Resolve(
                WorldPopulationClassification.Unsupported,
                WorldRespawnPolicyAssignment.Inherit("unsupported"),
                null,
                Fixed("ordinary.default", 240));
            Assert.IsFalse(unsupported.IsValid);
            Assert.AreEqual(WorldRespawnPolicyResolutionSource.Unresolved, unsupported.Source);
            WorldRespawnPolicyResolution invalidClassification = WorldRespawnPolicyResolver.Resolve(
                (WorldPopulationClassification)999,
                WorldRespawnPolicyAssignment.Explicit(Fixed("invalid.explicit", 60)),
                null,
                Fixed("ordinary.default", 240));
            Assert.IsFalse(invalidClassification.IsValid);

            RespawnPolicyDefinition zero = Fixed("zero", 0);
            Assert.IsFalse(WorldRespawnPolicyValidator.IsValid(zero));
            AssertThrows(() => WorldRespawnScheduler.SelectDelay(zero, null));
            RespawnPolicyDefinition groupZero = Fixed("group-zero", 0);
            groupZero.Mode = WorldRespawnMode.GroupSharedDelay;
            Assert.IsFalse(WorldRespawnPolicyValidator.IsValid(groupZero));
            RespawnPolicyDefinition unsupportedGroupTimer = Fixed("unsupported-group-timer", 60);
            unsupportedGroupTimer.Mode = WorldRespawnMode.GroupSharedDelay;
            unsupportedGroupTimer.SharedGroupTimer = true;
            Assert.IsFalse(WorldRespawnPolicyValidator.IsValid(unsupportedGroupTimer));
            RespawnPolicyDefinition unresolvedStart = Fixed("unresolved-start", 60);
            unresolvedStart.DelayStartsAt = RespawnDelayStartsAt.Unresolved;
            Assert.IsFalse(WorldRespawnPolicyValidator.IsValid(unresolvedStart));
            RespawnPolicyDefinition unsupportedMode = Fixed("unsupported-mode", 60);
            unsupportedMode.Mode = (WorldRespawnMode)999;
            Assert.IsFalse(WorldRespawnPolicyValidator.IsValid(unsupportedMode));
            AssertThrows(() => WorldRespawnScheduler.SelectDelay(unsupportedMode, null));
            RespawnPolicyDefinition unsupportedStart = Fixed("unsupported-start", 60);
            unsupportedStart.DelayStartsAt = (RespawnDelayStartsAt)999;
            Assert.IsFalse(WorldRespawnPolicyValidator.IsValid(unsupportedStart));
            RespawnPolicyDefinition corpseCreationStart = Fixed("corpse-creation-start", 60);
            corpseCreationStart.DelayStartsAt = RespawnDelayStartsAt.CorpseCreation;
            Assert.IsFalse(WorldRespawnPolicyValidator.IsValid(corpseCreationStart));
            RespawnPolicyDefinition infinite = Fixed("infinite", double.PositiveInfinity);
            Assert.IsFalse(WorldRespawnPolicyValidator.IsValid(infinite));
            RespawnPolicyDefinition tooLarge = Fixed("too-large", double.MaxValue);
            Assert.IsFalse(WorldRespawnPolicyValidator.IsValid(tooLarge));
            RespawnPolicyDefinition nanRange = Fixed("nan-range", 60);
            nanRange.Mode = WorldRespawnMode.RandomDelayRange;
            nanRange.FixedDelaySeconds = null;
            nanRange.MinimumDelaySeconds = 1;
            nanRange.MaximumDelaySeconds = double.NaN;
            Assert.IsFalse(WorldRespawnPolicyValidator.IsValid(nanRange));
            RespawnPolicyDefinition scripted = Fixed("scripted", 60);
            scripted.Mode = WorldRespawnMode.Scripted;
            scripted.FixedDelaySeconds = null;
            scripted.DelayStartsAt = RespawnDelayStartsAt.Scripted;
            Assert.AreEqual(
                WorldRespawnPolicyResolutionSource.Unresolved,
                WorldRespawnPolicyResolver.Resolve(
                    WorldPopulationClassification.OrdinaryEnemy,
                    WorldRespawnPolicyAssignment.Explicit(scripted),
                    null,
                    Fixed("ordinary.default", 240)).Source);
        }

        [TestMethod]
        public void CapturedOrdinaryExceptionsAndPopulationBoundaryRemainStable()
        {
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider());
            OrdinaryEnemySpawnDefinition[] spawns = catalog.GetSpawns();
            OrdinaryEnemyProfile[] profiles = catalog.GetProfiles();
            Assert.AreEqual(321, spawns.Length);
            Assert.AreEqual(283, spawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(38, spawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined));
            Assert.IsTrue(spawns.All(value => value.LevelDefinition.IsValid));
            Assert.IsTrue(
                spawns.All(
                    value => value.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Inherit
                             || value.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Explicit
                             || value.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.NoRespawn));

            OrdinaryEnemySpawnDefinition thief = spawns.Single(value => value.SourceIdentity == 0x7953AEA5);
            OrdinaryEnemySpawnDefinition flea = spawns.First(value => profiles.Single(profile => profile.ProfileKey == value.ProfileKey).MonsterData == 17657);
            OrdinaryEnemySpawnDefinition bloodcreeper = spawns.Single(value => value.SourceIdentity == 0x795451C5);
            OrdinaryEnemySpawnDefinition slumRunner = spawns.First(
                value => profiles.Single(profile => profile.ProfileKey == value.ProfileKey).DisplayName == "Slum Runner");
            AssertExplicitDelay(thief, 60);
            AssertExplicitDelay(flea, 240);
            AssertExplicitDelay(bloodcreeper, 240);
            AssertExplicitDelay(slumRunner, 60);
            Assert.IsTrue(
                slumRunner.RespawnPolicy.ExplicitPolicy.Evidence.Contains("20260716-215947"));
            Assert.IsTrue(
                slumRunner.RespawnPolicy.ExplicitPolicy.Evidence.Contains("death-to-respawn=59.433"));
            Assert.AreEqual(OrdinaryEnemySpawnLevelMode.InclusiveRange, bloodcreeper.LevelDefinition.Mode);
            Assert.AreEqual(OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration, bloodcreeper.LevelDefinition.RerollPolicy);

            var profilesByKey = profiles.ToDictionary(value => value.ProfileKey, StringComparer.Ordinal);
            Assert.AreEqual(24, spawns.Count(value => profilesByKey[value.ProfileKey].DisplayName == "Slum Runner"));
            Assert.AreEqual(5, spawns.Count(value => profilesByKey[value.ProfileKey].DisplayName == "Empty Shell"));
            Assert.AreEqual(10, spawns.Count(value => profilesByKey[value.ProfileKey].DisplayName == "Fragmented Soul"));
            Assert.AreEqual(10, spawns.Count(value => profilesByKey[value.ProfileKey].DisplayName == "Incomplete Rebuild"));
            Assert.AreEqual(10, spawns.Count(value => profilesByKey[value.ProfileKey].DisplayName == "Melded Patterns"));
            Assert.AreEqual(9, spawns.Count(value => profilesByKey[value.ProfileKey].DisplayName == "Molested Molecules"));
            Assert.AreEqual(7, spawns.Count(value => profilesByKey[value.ProfileKey].DisplayName == "Premature Pattern"));
            Assert.AreEqual(4, spawns.Count(value => profilesByKey[value.ProfileKey].DisplayName == "Redundant Scan"));
            Assert.AreEqual(6, spawns.Count(value => profilesByKey[value.ProfileKey].DisplayName == "Uncontrollable Anger"));
        }

        [TestMethod]
        public void DisobedientBotCorpseEvidenceKeepsEveryIdentityLinkedCreditOutcome()
        {
            CapturedSubwayCorpseEvidenceDefinition[] evidence =
                new CapturedSubwayOrdinaryContentProvider().GetCorpseEvidence(17649);

            Assert.AreEqual(13, evidence.Length);
            Assert.IsTrue(evidence.All(value => value.MonsterData == 17649));
            Assert.IsTrue(evidence.All(value => value.CatMesh == 15215));
            CollectionAssert.AreEqual(
                new[] { "5:6:2", "6:8:2", "8:10:4", "9:11:3", "10:12:2" },
                evidence
                    .GroupBy(value => new { value.EnemyLevel, value.Credits })
                    .OrderBy(group => group.Key.EnemyLevel)
                    .ThenBy(group => group.Key.Credits)
                    .Select(group => string.Format(
                        "{0}:{1}:{2}",
                        group.Key.EnemyLevel,
                        group.Key.Credits,
                        group.Count()))
                    .ToArray());
            Assert.IsTrue(evidence.Any(value => value.Capture == "20260709-205921" && value.DeadNpcIdentity == "(SimpleChar:795310FB)" && value.CorpseIdentity == "(Corpse:00F6E013)"));
            Assert.IsTrue(evidence.Any(value => value.Capture == "20260712-160257" && value.DeadNpcIdentity == "(SimpleChar:795EC78A)" && value.CorpseIdentity == "(Corpse:00F6C006)"));
            Assert.IsTrue(evidence.Any(value => value.Capture == "20260713-014714" && value.DeadNpcIdentity == "(SimpleChar:79607CD0)" && value.CorpseIdentity == "(Corpse:00F6C005)"));
            Assert.IsTrue(evidence.Any(value => value.Capture == "20260713-033511" && value.DeadNpcIdentity == "(SimpleChar:79607E2C)" && value.CorpseIdentity == "(Corpse:00F6C003)"));
            Assert.IsFalse(evidence.Any(value => value.Capture == "20260713-013906"));
        }

        [TestMethod]
        public void DeepSubwayOrdinaryCombatUsesLocalPlayerNormalHitEvidence()
        {
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider());

            AssertCapturedDamage(catalog, "Incomplete Rebuild", 17, 35);
            AssertCapturedDamage(catalog, "Molested Molecules", 16, 42);
            AssertCapturedDamage(catalog, "Neural Burnout", 16, 22);
            AssertCapturedDamage(catalog, "Redundant Scan", 19, 19);
            AssertCapturedDamage(catalog, "Uncontrollable Anger", 11, 18);

            OrdinaryEnemyProfile molested = catalog.GetProfiles()
                .Single(value => value.DisplayName == "Molested Molecules");
            Assert.IsTrue(molested.Combat.Contract.Evidence.Contains("20260716-221358"));
        }

        [TestMethod]
        public void MeldedPatternsUsesCapturedWeaponRollAndFailsClosedWithoutExactEvidence()
        {
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider());
            OrdinaryEnemyProfile melded = catalog.GetProfiles()
                .Single(value => value.DisplayName == "Melded Patterns");
            CapturedEnemyCombatContract contract = melded.Combat.Contract;

            Assert.AreEqual(OrdinaryEnemyEvidenceState.Observed, melded.Combat.EvidenceState);
            Assert.AreEqual(OrdinaryEnemyCombatMode.EquippedRanged, melded.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.WeaponRoll, melded.Combat.DamageSource);
            Assert.IsTrue(melded.Combat.VisibleWeapon);
            Assert.AreEqual(CapturedEnemyAttackModel.EquippedWeapon, contract.AttackModel);
            Assert.IsTrue(contract.IsCombatReady);
            Assert.AreEqual(121817, NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponLowTemplate);
            Assert.AreEqual(121818, NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponHighTemplate);
            Assert.AreEqual(20, NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponQuality);
            Assert.AreEqual(121817, contract.WeaponLowId);
            Assert.AreEqual(121818, contract.WeaponHighId);
            Assert.AreEqual(20, contract.WeaponQuality);
            Assert.AreEqual(6, contract.WeaponInventorySlot);
            Assert.AreEqual(0, contract.MinDamage, "Observed post-mitigation hits must not become a weapon damage override.");
            Assert.AreEqual(0, contract.MaxDamage, "Observed post-mitigation hits must not become a weapon damage override.");
            Assert.AreEqual(0.0, contract.RechargeSeconds, "Observed timing must not replace item-owned recharge.");
            Assert.IsFalse(contract.HasEmptySpecialAttackWeaponContext, "No special-attack context was promoted.");
            Assert.IsFalse(contract.HasCapturedEquippedAttackInfo, "Generic item-owned AttackInfo must remain in control.");
            Assert.IsTrue(contract.Evidence.Contains("20260716-034559"));
            Assert.IsTrue(contract.Evidence.Contains("21..34"));
            Assert.IsTrue(contract.Evidence.Contains("no observed critical"));

            CapturedEnemyCombatContract missingFocusedCapture =
                CapturedSubwayCombatCatalog.ForOrdinary(
                    BuildMeldedPatternsArchetype(
                        new CapturedSubwayCombatEvidenceDefinition(
                            true,
                            21,
                            34,
                            4.466488,
                            6,
                            0,
                            0,
                            7),
                        "different-capture"));
            CapturedEnemyCombatContract changedNormalHitBoundary =
                CapturedSubwayCombatCatalog.ForOrdinary(
                    BuildMeldedPatternsArchetype(
                        new CapturedSubwayCombatEvidenceDefinition(
                            true,
                            21,
                            35,
                            4.466488,
                            6,
                            0,
                            0,
                            7),
                        "20260716-034559"));
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, missingFocusedCapture.AttackModel);
            Assert.IsFalse(missingFocusedCapture.IsCombatReady);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, changedNormalHitBoundary.AttackModel);
            Assert.IsFalse(changedNormalHitBoundary.IsCombatReady);

            OrdinaryEnemyProfile incomplete = catalog.GetProfiles()
                .Single(value => value.DisplayName == "Incomplete Rebuild");
            Assert.AreEqual(CapturedEnemyAttackModel.FixedAttackInfo, incomplete.Combat.Contract.AttackModel);
            Assert.AreEqual(OrdinaryEnemyDamageSource.CapturedFixed, incomplete.Combat.DamageSource);
            Assert.IsFalse(incomplete.Combat.VisibleWeapon);

            string source = Read(FindRepositoryRoot(), "CapturedEnemyCombatContract.cs");
            int methodStart = source.IndexOf(
                "private static CapturedEnemyCombatContract ForMeldedPatterns",
                StringComparison.Ordinal);
            int methodEnd = source.IndexOf(
                "internal static CapturedEnemyCombatContract ForOrdinary",
                methodStart,
                StringComparison.Ordinal);
            Assert.IsTrue(methodStart >= 0 && methodEnd > methodStart);
            string implementation = source.Substring(methodStart, methodEnd - methodStart);
            Assert.IsTrue(implementation.Contains("CapturedEnemyCombatContract.EquippedWeapon("));
            Assert.IsFalse(implementation.Contains("EquippedWeaponWithEmptySpecialAttackContext"));
            Assert.IsFalse(implementation.Contains("CapturedEnemyCombatContract.FixedAttack("));
        }

        [TestMethod]
        public void WorkmanStrikerResolvesEveryCapturedSourceWeaponAndNeverUsesAggregateFallback()
        {
            var expected = new Dictionary<int, int[]>
            {
                { 0x7953A84F, new[] { 122905, 122906, 19 } },
                { 0x7953A9F0, new[] { 122905, 122906, 17 } },
                { 0x7953AA0D, new[] { 122905, 122906, 18 } },
                { 0x7953AA16, new[] { 122905, 122906, 15 } },
                { 0x7953AA77, new[] { 122905, 122906, 14 } },
                { 0x7953AABE, new[] { 122905, 122906, 13 } },
                { 0x7953AAE9, new[] { 122905, 122906, 14 } },
                { 0x7953AB03, new[] { 122905, 122906, 16 } },
                { 0x7953AF95, new[] { 122905, 122906, 12 } },
                { 0x7953AFB8, new[] { 122905, 122906, 17 } },
                { 0x7953AFBC, new[] { 122905, 122906, 19 } },
                { 0x7953AFDD, new[] { 122905, 122906, 12 } },
                { 0x7953AFF9, new[] { 122905, 122906, 16 } },
                { 0x79545000, new[] { 122906, 122906, 20 } },
                { 0x7954501A, new[] { 122905, 122906, 16 } },
                { 0x79545108, new[] { 122905, 122906, 15 } },
                { 0x795451CA, new[] { 122907, 122908, 27 } },
                { 0x79545205, new[] { 122905, 122906, 11 } },
                { 0x79545213, new[] { 122905, 122906, 14 } },
                { 0x79545219, new[] { 122905, 122906, 19 } },
                { 0x79545224, new[] { 122905, 122906, 14 } }
            };
            var provider = new CapturedSubwayOrdinaryContentProvider();
            CapturedSubwayOrdinaryArchetypeDefinition archetype;
            Assert.IsTrue(provider.TryGetArchetype("workman_striker", out archetype));
            Assert.AreEqual(21, archetype.SourceWeaponEvidence.Length);
            CollectionAssert.AreEquivalent(
                expected.Keys.ToArray(),
                archetype.SourceWeaponEvidence.Select(value => value.SourceInstance).ToArray());

            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                provider);
            OrdinaryEnemyProfile workman = catalog.GetProfiles()
                .Single(value => value.DisplayName == "Workman Striker");
            OrdinaryEnemySpawnDefinition[] spawns = catalog.GetSpawns()
                .Where(value => value.ProfileKey == workman.ProfileKey)
                .ToArray();

            Assert.AreEqual(21, spawns.Length);
            CollectionAssert.AreEquivalent(
                expected.Keys.ToArray(),
                spawns.Select(value => value.SourceIdentity).ToArray());
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Observed, workman.Combat.EvidenceState);
            Assert.AreEqual(OrdinaryEnemyCombatMode.EquippedRanged, workman.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.WeaponRoll, workman.Combat.DamageSource);
            Assert.IsTrue(workman.Combat.VisibleWeapon);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, workman.Combat.Contract.AttackModel);
            Assert.IsFalse(workman.Combat.Contract.IsCombatReady);
            Assert.AreEqual(
                CapturedEnemyAttackModel.Unresolved,
                workman.Combat.ResolveContract(spawns[0].Level).AttackModel,
                "A Workman contract without its source identity must fail closed.");

            foreach (OrdinaryEnemySpawnDefinition spawn in spawns)
            {
                int[] weapon = expected[spawn.SourceIdentity];
                CapturedEnemyCombatContract contract = workman.Combat.ResolveContract(
                    spawn.SourceIdentity,
                    spawn.Level);
                Assert.AreEqual(CapturedEnemyAttackModel.EquippedWeapon, contract.AttackModel);
                Assert.IsTrue(contract.IsCombatReady);
                Assert.AreEqual(weapon[0], contract.WeaponLowId);
                Assert.AreEqual(weapon[1], contract.WeaponHighId);
                Assert.AreEqual(weapon[2], contract.WeaponQuality);
                Assert.AreEqual(6, contract.WeaponInventorySlot);
                Assert.AreEqual(0, contract.MinDamage);
                Assert.AreEqual(0, contract.MaxDamage);
                Assert.AreEqual(0.0, contract.RechargeSeconds);
                Assert.AreEqual(0, contract.AttackInfoWeaponSlot);
                Assert.AreEqual(0, contract.AttackInfoUnknown);
                Assert.AreEqual(0, contract.AttackInfoWeaponInstance);
                Assert.IsFalse(contract.HasEmptySpecialAttackWeaponContext);
                Assert.IsFalse(contract.HasCapturedAttackStartContext);
                Assert.IsFalse(contract.HasCapturedEquippedAttackInfo);
                Assert.IsTrue(
                    contract.Evidence.Contains(
                        "source 0x" + spawn.SourceIdentity.ToString("X8")));
            }

            CapturedEnemyCombatContract unknown = workman.Combat.ResolveContract(
                0x7953FFFF,
                spawns[0].Level);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, unknown.AttackModel);
            Assert.IsFalse(unknown.IsCombatReady);

            OrdinaryEnemyProfile melded = catalog.GetProfiles()
                .Single(value => value.DisplayName == "Melded Patterns");
            Assert.AreSame(
                melded.Combat.Contract,
                melded.Combat.ResolveContract(0x7953FFFF, 20),
                "Profiles without a source resolver must preserve their base contract.");

            var levelResolved = new CapturedEnemyCombatContract { Evidence = "level" };
            var levelAware = new OrdinaryEnemyCombatProfile(
                OrdinaryEnemyCombatMode.Unresolved,
                OrdinaryEnemyDamageSource.Unresolved,
                false,
                new CapturedEnemyCombatContract { Evidence = "base" },
                OrdinaryEnemyEvidenceState.Unresolved,
                contractResolver: level => levelResolved);
            Assert.AreSame(
                levelResolved,
                levelAware.ResolveContract(0x7953FFFF, 10),
                "The source-aware overload must retain the existing level resolver fallback.");

            string source = Read(FindRepositoryRoot(), "CapturedEnemyCombatContract.cs");
            int methodStart = source.IndexOf(
                "private static CapturedEnemyCombatContract ForWorkmanStriker",
                StringComparison.Ordinal);
            int methodEnd = source.IndexOf(
                "private static CapturedEnemyCombatContract ForMeldedPatterns",
                methodStart,
                StringComparison.Ordinal);
            Assert.IsTrue(methodStart >= 0 && methodEnd > methodStart);
            string implementation = source.Substring(methodStart, methodEnd - methodStart);
            Assert.IsTrue(implementation.Contains("CapturedEnemyCombatContract.EquippedWeapon("));
            Assert.IsFalse(implementation.Contains("EquippedWeaponWithEmptySpecialAttackContext"));
            Assert.IsFalse(implementation.Contains("CapturedEnemyCombatContract.FixedAttack("));
            Assert.IsFalse(implementation.Contains("MinDamage"));
            Assert.IsFalse(implementation.Contains("MaxDamage"));
        }

        [TestMethod]
        public void LooterResolvesEveryCapturedSourceWeaponAndFailsClosedWithoutOneExactTuple()
        {
            var expected = new Dictionary<int, int[]>
            {
                { 0x795312DC, new[] { 123038, 123039, 12 } },
                { 0x795313CB, new[] { 123038, 123039, 9 } },
                { 0x7954501B, new[] { 123038, 123039, 8 } },
                { 0x79545029, new[] { 123038, 123039, 9 } },
                { 0x79545034, new[] { 123038, 123039, 12 } },
                { 0x7954503C, new[] { 123038, 123039, 11 } },
                { 0x79557CB8, new[] { 123038, 123039, 8 } },
                { 0x7957E5CD, new[] { 123038, 123039, 9 } }
            };
            var provider = new CapturedSubwayOrdinaryContentProvider();
            CapturedSubwayOrdinaryArchetypeDefinition archetype;
            Assert.IsTrue(provider.TryGetArchetype("looter", out archetype));
            Assert.AreEqual(8, archetype.SourceWeaponEvidence.Length);
            Assert.AreEqual(15, archetype.Combat.ObservedRows);
            Assert.AreEqual(11, archetype.Combat.MinDamage);
            Assert.AreEqual(11, archetype.Combat.MaxDamage);
            Assert.AreEqual(5.282358, archetype.Combat.RechargeSeconds);
            Assert.AreEqual(6, archetype.Combat.WeaponSlot);
            CollectionAssert.AreEquivalent(
                expected.Keys.ToArray(),
                archetype.SourceWeaponEvidence.Select(value => value.SourceInstance).ToArray());

            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                provider);
            OrdinaryEnemyProfile looter = catalog.GetProfiles()
                .Single(value => value.DisplayName == "Looter");
            OrdinaryEnemySpawnDefinition[] spawns = catalog.GetSpawns()
                .Where(value => value.ProfileKey == looter.ProfileKey)
                .ToArray();

            Assert.AreEqual(8, spawns.Length);
            Assert.AreEqual(6, spawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(2, spawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined));
            CollectionAssert.AreEquivalent(
                new[] { 0x795312DC, 0x795313CB, 0x7954501B, 0x79545029, 0x79545034, 0x7954503C },
                spawns
                    .Where(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active)
                    .Select(value => value.SourceIdentity)
                    .ToArray());
            CollectionAssert.AreEquivalent(
                new[] { 0x79557CB8, 0x7957E5CD },
                spawns
                    .Where(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined)
                    .Select(value => value.SourceIdentity)
                    .ToArray());
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Observed, looter.Combat.EvidenceState);
            Assert.AreEqual(OrdinaryEnemyCombatMode.EquippedRanged, looter.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.WeaponRoll, looter.Combat.DamageSource);
            Assert.IsTrue(looter.Combat.VisibleWeapon);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, looter.Combat.Contract.AttackModel);
            Assert.IsFalse(looter.Combat.Contract.IsCombatReady);
            Assert.AreEqual(
                CapturedEnemyAttackModel.Unresolved,
                looter.Combat.ResolveContract(spawns[0].Level).AttackModel,
                "A Looter contract without its source identity must fail closed.");

            foreach (OrdinaryEnemySpawnDefinition spawn in spawns)
            {
                int[] weapon = expected[spawn.SourceIdentity];
                CapturedEnemyCombatContract contract = looter.Combat.ResolveContract(
                    spawn.SourceIdentity,
                    spawn.Level);
                Assert.AreEqual(CapturedEnemyAttackModel.EquippedWeapon, contract.AttackModel);
                Assert.IsTrue(contract.IsCombatReady);
                Assert.AreEqual(weapon[0], contract.WeaponLowId);
                Assert.AreEqual(weapon[1], contract.WeaponHighId);
                Assert.AreEqual(weapon[2], contract.WeaponQuality);
                Assert.AreEqual(6, contract.WeaponInventorySlot);
                Assert.AreEqual(0, contract.MinDamage);
                Assert.AreEqual(0, contract.MaxDamage);
                Assert.AreEqual(0.0, contract.RechargeSeconds);
                Assert.AreEqual(0, contract.AttackInfoWeaponSlot);
                Assert.AreEqual(0, contract.AttackInfoUnknown);
                Assert.AreEqual(0, contract.AttackInfoWeaponInstance);
                Assert.IsFalse(contract.HasEmptySpecialAttackWeaponContext);
                Assert.IsFalse(contract.HasCapturedAttackStartContext);
                Assert.IsFalse(contract.HasCapturedEquippedAttackInfo);
                Assert.IsTrue(contract.Evidence.Contains("Looter source 0x" + spawn.SourceIdentity.ToString("X8")));
                Assert.IsTrue(contract.Evidence.Contains("item owns normal damage and recharge"));
            }

            CapturedEnemyCombatContract unknown = looter.Combat.ResolveContract(
                0x7953FFFF,
                spawns[0].Level);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, unknown.AttackModel);
            Assert.IsFalse(unknown.IsCombatReady);

            CapturedEnemyCombatContract missing = CapturedSubwayCombatCatalog.ForOrdinary(
                BuildLooterArchetype(new CapturedSubwaySourceWeaponEvidenceDefinition[0]),
                0x795312DC);
            CapturedEnemyCombatContract conflicting = CapturedSubwayCombatCatalog.ForOrdinary(
                BuildLooterArchetype(
                    new[]
                    {
                        new CapturedSubwaySourceWeaponEvidenceDefinition(0x795312DC, 123038, 123039, 12, "capture-a"),
                        new CapturedSubwaySourceWeaponEvidenceDefinition(0x795312DC, 123038, 123039, 11, "capture-b")
                    }),
                0x795312DC);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, missing.AttackModel);
            Assert.IsFalse(missing.IsCombatReady);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, conflicting.AttackModel);
            Assert.IsFalse(conflicting.IsCombatReady);
        }

        [TestMethod]
        public void MuggerUsesExactCurrentSourceWeaponsAndCapturedAttackInfoShapeWithoutFixedDamage()
        {
            int[] expectedSources =
            {
                0x7953AA11,
                0x7953AD6B,
                0x795450D4,
                0x795451FE,
                0x79557F14,
                0x7957E5C6,
                0x7957E5C7,
                0x7957E5C8,
                0x7957E5CA
            };
            int[] activeSources =
            {
                0x7953AA11,
                0x7953AD6B,
                0x795450D4,
                0x795451FE
            };
            int[] quarantinedSources =
            {
                0x79557F14,
                0x7957E5C6,
                0x7957E5C7,
                0x7957E5C8,
                0x7957E5CA
            };
            var ordinaryProvider = new CapturedSubwayOrdinaryContentProvider();
            CapturedSubwaySourceWeaponEvidenceDefinition[] sourceEvidence =
                ordinaryProvider.GetSourceWeaponEvidence(203734);

            Assert.AreEqual(9, sourceEvidence.Length);
            CollectionAssert.AreEquivalent(
                expectedSources,
                sourceEvidence.Select(value => value.SourceInstance).ToArray());
            foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in sourceEvidence)
            {
                Assert.AreEqual(121567, evidence.LowId);
                Assert.AreEqual(121567, evidence.HighId);
                Assert.AreEqual(1, evidence.Quality);
                Assert.IsFalse(string.IsNullOrWhiteSpace(evidence.EvidenceCaptures));
            }

            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                ordinaryProvider);
            OrdinaryEnemyProfile mugger = catalog.GetProfiles()
                .Single(value => value.DisplayName == "Mugger");
            OrdinaryEnemySpawnDefinition[] spawns = catalog.GetSpawns()
                .Where(value => value.ProfileKey == mugger.ProfileKey)
                .ToArray();

            Assert.AreEqual(9, spawns.Length);
            CollectionAssert.AreEqual(
                new[]
                {
                    "7953AA11:8:Active",
                    "7953AD6B:10:Active",
                    "795450D4:5:Active",
                    "795451FE:10:Active",
                    "79557F14:10:Quarantined",
                    "7957E5C6:9:Quarantined",
                    "7957E5C7:8:Quarantined",
                    "7957E5C8:8:Quarantined",
                    "7957E5CA:10:Quarantined"
                },
                spawns
                    .OrderBy(value => value.SourceIdentity)
                    .Select(value => string.Format("{0:X8}:{1}:{2}", value.SourceIdentity, value.Level, value.Disposition))
                    .ToArray());
            CollectionAssert.AreEquivalent(
                activeSources,
                spawns
                    .Where(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active)
                    .Select(value => value.SourceIdentity)
                    .ToArray());
            CollectionAssert.AreEquivalent(
                quarantinedSources,
                spawns
                    .Where(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined)
                    .Select(value => value.SourceIdentity)
                    .ToArray());
            Assert.AreEqual(OrdinaryEnemyCombatMode.EquippedRanged, mugger.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.WeaponRoll, mugger.Combat.DamageSource);
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Observed, mugger.Combat.EvidenceState);
            Assert.IsTrue(mugger.Combat.VisibleWeapon);
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Retaliate, mugger.Aggression.Mode);
            Assert.IsTrue(mugger.Aggression.Chase);
            Assert.IsTrue(spawns.All(value => value.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Inherit));
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, mugger.Combat.Contract.AttackModel);
            Assert.IsFalse(mugger.Combat.Contract.IsCombatReady);
            Assert.AreEqual(
                CapturedEnemyAttackModel.Unresolved,
                mugger.Combat.ResolveContract(spawns[0].Level).AttackModel,
                "A Mugger contract without its source identity must fail closed.");

            foreach (OrdinaryEnemySpawnDefinition spawn in spawns)
            {
                CapturedEnemyCombatContract contract = mugger.Combat.ResolveContract(
                    spawn.SourceIdentity,
                    spawn.Level);
                Assert.AreEqual(CapturedEnemyAttackModel.EquippedWeapon, contract.AttackModel);
                Assert.IsTrue(contract.IsCombatReady);
                Assert.AreEqual(121567, contract.WeaponLowId);
                Assert.AreEqual(121567, contract.WeaponHighId);
                Assert.AreEqual(1, contract.WeaponQuality);
                Assert.AreEqual(6, contract.WeaponInventorySlot);
                Assert.AreEqual(0, contract.MinDamage);
                Assert.AreEqual(0, contract.MaxDamage);
                Assert.AreEqual(0.0, contract.RechargeSeconds);
                Assert.IsTrue(contract.HasCapturedEquippedAttackInfo);
                Assert.AreEqual(-1, contract.AttackInfoAmmoCount);
                Assert.AreEqual(6, contract.AttackInfoWeaponSlot);
                Assert.AreEqual(0, contract.AttackInfoUnknown);
                Assert.AreEqual(0, contract.AttackInfoWeaponInstance);
                Assert.IsFalse(contract.HasEmptySpecialAttackWeaponContext);
                Assert.IsFalse(contract.HasCapturedAttackStartContext);
                Assert.IsFalse(contract.HasCapturedCombatStopSequence);
                Assert.IsTrue(contract.Evidence.Contains("38 normal local-player hits"));
                Assert.IsTrue(contract.Evidence.Contains("three 21-point criticals are report-only"));
                Assert.IsTrue(contract.Evidence.Contains("5.816469"));
                Assert.IsTrue(contract.Evidence.Contains("item owns runtime damage, damage bonus, and recharge"));
            }

            CapturedEnemyCombatContract unknown = mugger.Combat.ResolveContract(
                0x7953FFFF,
                spawns[0].Level);
            CapturedEnemyCombatContract missing =
                CapturedSubwayCombatCatalog.ForSupportedSourceWeapon(
                    "Mugger",
                    203734,
                    sourceEvidence.Take(sourceEvidence.Length - 1).ToArray(),
                    expectedSources[0]);
            CapturedEnemyCombatContract conflicting =
                CapturedSubwayCombatCatalog.ForSupportedSourceWeapon(
                    "Mugger",
                    203734,
                    sourceEvidence
                        .Concat(
                            new[]
                            {
                                new CapturedSubwaySourceWeaponEvidenceDefinition(
                                    expectedSources[0],
                                    121567,
                                    121567,
                                    1,
                                    "conflict")
                            })
                        .ToArray(),
                    expectedSources[0]);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, unknown.AttackModel);
            Assert.IsFalse(unknown.IsCombatReady);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, missing.AttackModel);
            Assert.IsFalse(missing.IsCombatReady);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, conflicting.AttackModel);
            Assert.IsFalse(conflicting.IsCombatReady);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, mugger.Loot.PoolMode);
            Assert.IsFalse(mugger.Loot.ItemPoolComplete);
            Assert.AreEqual(17, mugger.Loot.ObservedCompleteInventories);
            Assert.AreEqual(3, mugger.Loot.ObservedEmptyInventories);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "25822:25831:5:1:17", "85711:22014:8:1:17",
                    "123704:123705:9:1:17", "123723:123724:6:1:17",
                    "123976:123977:9:1:17", "124348:124349:7:1:17",
                    "124545:124546:10:1:17", "128636:128637:8:1:17",
                    "128839:128840:9:1:17", "130060:130061:5:1:17",
                    "130060:130061:9:1:17", "131605:131606:7:1:17",
                    "136638:136639:9:1:17", "136638:136639:12:1:17",
                    "136640:136641:7:1:17", "136640:136641:8:1:17",
                    "136640:136641:9:1:17", "136646:136647:9:1:17",
                    "160224:160225:10:1:17", "234875:234875:1:2:17",
                    "234876:234876:1:1:17"
                },
                mugger.Loot.Entries
                    .Select(value => string.Format("{0}:{1}:{2}:{3}:{4}", value.LowId, value.HighId, value.QualityLevel, value.ObservedCount, value.ObservedCorpses))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { "5:44:44:6", "8:71:71:6", "9:80:80:6", "10:88:88:6" },
                mugger.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(value => string.Format("{0}:{1}:{2}:{3}", value.EnemyLevel, value.MinimumCredits, value.MaximumCredits, value.ObservedCorpses))
                    .ToArray());
            Assert.AreEqual(17534, mugger.Corpse.CapturedCatMesh);
            Assert.AreEqual(3.0, mugger.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(240.0, mugger.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(3.0, mugger.Corpse.LootedCleanupSeconds);
        }

        [TestMethod]
        public void DerangedShopperUsesItsOneExactQuarantinedSourceWeaponAndCapturedAttackInfoShape()
        {
            const int sourceIdentity = 0x79574527;
            var ordinaryProvider = new CapturedSubwayOrdinaryContentProvider();
            CapturedSubwaySourceWeaponEvidenceDefinition[] sourceEvidence =
                ordinaryProvider.GetSourceWeaponEvidence(203736);

            Assert.AreEqual(1, sourceEvidence.Length);
            Assert.AreEqual(sourceIdentity, sourceEvidence[0].SourceInstance);
            Assert.AreEqual(125454, sourceEvidence[0].LowId);
            Assert.AreEqual(125455, sourceEvidence[0].HighId);
            Assert.AreEqual(8, sourceEvidence[0].Quality);
            Assert.IsTrue(sourceEvidence[0].EvidenceCaptures.Contains("20260710-202132"));
            string generatedCombatReportText = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    FindRepositoryRoot(),
                    @"docs\generated\subway_enemy_combat_contracts.json"));
            int reportStart = generatedCombatReportText.IndexOf(
                "\"Deranged Shopper\":",
                StringComparison.Ordinal);
            int reportEnd = generatedCombatReportText.IndexOf(
                "\"Discarded Pet\":",
                reportStart,
                StringComparison.Ordinal);
            Assert.IsTrue(reportStart >= 0 && reportEnd > reportStart);
            string report = generatedCombatReportText.Substring(reportStart, reportEnd - reportStart);
            Assert.IsTrue(report.Contains("\"missedAttackInfoRows\": 2"));
            Assert.IsTrue(report.Contains("\"missedAttackShapes\": ["));
            Assert.IsTrue(report.Contains("\"ammoCount\": -1"));
            Assert.IsTrue(report.Contains("\"weaponSlot\": 6"));
            Assert.IsTrue(report.Contains("\"unknown\": 0"));
            Assert.IsTrue(report.Contains("\"rows\": 2"));
            Assert.IsTrue(report.Contains("\"equippedWeaponShapes\": ["));
            Assert.IsTrue(report.Contains("\"lowId\": 125454"));
            Assert.IsTrue(report.Contains("\"highId\": 125455"));
            Assert.IsTrue(report.Contains("\"quality\": 8"));
            Assert.IsTrue(report.Contains("20260710-202132"));
            Assert.IsTrue(report.Contains("(SimpleChar:79574527)"));

            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                ordinaryProvider);
            OrdinaryEnemyProfile shopper = catalog.GetProfiles()
                .Single(value => value.DisplayName == "Deranged Shopper");
            OrdinaryEnemySpawnDefinition spawn = catalog.GetSpawns()
                .Single(value => value.ProfileKey == shopper.ProfileKey);

            Assert.AreEqual(sourceIdentity, spawn.SourceIdentity);
            Assert.AreEqual(8, spawn.Level);
            Assert.AreEqual(256, spawn.LevelDefinition.Resolve(spawn.Level).Health);
            Assert.AreEqual(OrdinaryEnemyRuntimeDisposition.Quarantined, spawn.Disposition);
            Assert.AreEqual(WorldRespawnPolicyAssignmentMode.Inherit, spawn.RespawnPolicy.Mode);
            Assert.AreEqual(OrdinaryEnemyCombatMode.EquippedRanged, shopper.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.WeaponRoll, shopper.Combat.DamageSource);
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Observed, shopper.Combat.EvidenceState);
            Assert.IsTrue(shopper.Combat.VisibleWeapon);
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Retaliate, shopper.Aggression.Mode);
            Assert.IsTrue(shopper.Aggression.Chase);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, shopper.Combat.Contract.AttackModel);
            Assert.IsFalse(shopper.Combat.Contract.IsCombatReady);
            Assert.AreEqual(
                CapturedEnemyAttackModel.Unresolved,
                shopper.Combat.ResolveContract(spawn.Level).AttackModel,
                "Deranged Shopper combat without the source identity must fail closed.");

            CapturedEnemyCombatContract contract = shopper.Combat.ResolveContract(
                spawn.SourceIdentity,
                spawn.Level);
            Assert.AreEqual(CapturedEnemyAttackModel.EquippedWeapon, contract.AttackModel);
            Assert.IsTrue(contract.IsCombatReady);
            Assert.AreEqual(125454, contract.WeaponLowId);
            Assert.AreEqual(125455, contract.WeaponHighId);
            Assert.AreEqual(8, contract.WeaponQuality);
            Assert.AreEqual(6, contract.WeaponInventorySlot);
            Assert.AreEqual(0, contract.MinDamage);
            Assert.AreEqual(0, contract.MaxDamage);
            Assert.AreEqual(0.0, contract.RechargeSeconds);
            Assert.IsTrue(contract.HasCapturedEquippedAttackInfo);
            Assert.AreEqual(-1, contract.AttackInfoAmmoCount);
            Assert.AreEqual(6, contract.AttackInfoWeaponSlot);
            Assert.AreEqual(0, contract.AttackInfoUnknown);
            Assert.AreEqual(0, contract.AttackInfoWeaponInstance);
            Assert.IsFalse(contract.HasEmptySpecialAttackWeaponContext);
            Assert.IsFalse(contract.HasCapturedAttackStartContext);
            Assert.IsFalse(contract.HasCapturedCombatStopSequence);
            Assert.IsTrue(contract.Evidence.Contains("critical is report-only"));
            Assert.IsTrue(contract.Evidence.Contains("one captured miss"));
            Assert.IsTrue(contract.Evidence.Contains("item owns runtime damage, damage bonus, and recharge"));

            CapturedEnemyCombatContract unknown = shopper.Combat.ResolveContract(
                0x7957FFFF,
                spawn.Level);
            CapturedEnemyCombatContract missing = CapturedSubwayCombatCatalog.ForOrdinary(
                BuildDerangedShopperArchetype(new CapturedSubwaySourceWeaponEvidenceDefinition[0]),
                sourceIdentity);
            CapturedEnemyCombatContract conflicting = CapturedSubwayCombatCatalog.ForOrdinary(
                BuildDerangedShopperArchetype(
                    new[]
                    {
                        new CapturedSubwaySourceWeaponEvidenceDefinition(sourceIdentity, 125454, 125455, 8, "capture-a"),
                        new CapturedSubwaySourceWeaponEvidenceDefinition(sourceIdentity, 125454, 125455, 7, "capture-b")
                    }),
                sourceIdentity);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, unknown.AttackModel);
            Assert.IsFalse(unknown.IsCombatReady);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, missing.AttackModel);
            Assert.IsFalse(missing.IsCombatReady);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, conflicting.AttackModel);
            Assert.IsFalse(conflicting.IsCombatReady);

            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, shopper.Loot.PoolMode);
            Assert.IsFalse(shopper.Loot.ItemPoolComplete);
            Assert.AreEqual(2, shopper.Loot.ObservedCompleteInventories);
            Assert.AreEqual(0, shopper.Loot.ObservedEmptyInventories);
            CollectionAssert.AreEquivalent(
                new[] { "123019:123020:6:1:2", "124465:124466:10:1:2" },
                shopper.Loot.Entries
                    .Select(value => string.Format("{0}:{1}:{2}:{3}:{4}", value.LowId, value.HighId, value.QualityLevel, value.ObservedCount, value.ObservedCorpses))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { "8:47:47:1", "9:53:53:1" },
                shopper.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(value => string.Format("{0}:{1}:{2}:{3}", value.EnemyLevel, value.MinimumCredits, value.MaximumCredits, value.ObservedCorpses))
                    .ToArray());
            Assert.AreEqual(5927, shopper.Corpse.CapturedCatMesh);
            Assert.AreEqual(3.0, shopper.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(240.0, shopper.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(3.0, shopper.Corpse.LootedCleanupSeconds);
        }

        [TestMethod]
        public void DistinctNoRespawnAssignmentsKeepStablePolicyKeysAndProvenance()
        {
            WorldRespawnPolicyResolution first = WorldRespawnPolicyResolver.Resolve(
                WorldPopulationClassification.OrdinaryEnemy,
                WorldRespawnPolicyAssignment.NoRespawn("ordinary.none.first", "capture-a", "OBSERVED"),
                null,
                Fixed("ordinary.default", 240));
            WorldRespawnPolicyResolution second = WorldRespawnPolicyResolver.Resolve(
                WorldPopulationClassification.OrdinaryEnemy,
                WorldRespawnPolicyAssignment.NoRespawn("ordinary.none.second", "capture-b", "OBSERVED"),
                null,
                Fixed("ordinary.default", 240));
            var policies = new Dictionary<string, RespawnPolicyDefinition>(StringComparer.Ordinal);
            WorldRespawnPolicyValidator.RegisterOrRejectConflict(policies, first.Policy);
            WorldRespawnPolicyValidator.RegisterOrRejectConflict(policies, second.Policy);
            Assert.AreEqual(2, policies.Count);
            Assert.AreEqual("capture-a", policies["ordinary.none.first"].Evidence);
            Assert.AreEqual("capture-b", policies["ordinary.none.second"].Evidence);
        }

        [TestMethod]
        public void SchedulerRejectsInvalidAndDuplicateGenerationWork()
        {
            var scheduler = new WorldRespawnScheduler();
            DateTime due = new DateTime(2030, 1, 1);
            Assert.IsFalse(scheduler.Schedule(new WorldRespawnSchedule { SpawnKey = "zero-generation", PlayfieldId = 127, DueAtUtc = due, Generation = 0 }));
            Assert.IsFalse(scheduler.Schedule(new WorldRespawnSchedule { SpawnKey = "missing-due", PlayfieldId = 127, Generation = 1 }));
            Assert.IsTrue(scheduler.Schedule(new WorldRespawnSchedule { SpawnKey = "enemy", PlayfieldId = 127, DueAtUtc = due, Generation = 1 }));
            Assert.IsFalse(scheduler.Schedule(new WorldRespawnSchedule { SpawnKey = "enemy", PlayfieldId = 127, DueAtUtc = due, Generation = 1 }));
            Assert.IsFalse(scheduler.Schedule(new WorldRespawnSchedule { SpawnKey = "enemy", PlayfieldId = 127, DueAtUtc = due, Generation = 2 }));
            Assert.AreEqual(1, scheduler.Count);
        }

        [TestMethod]
        public void PopulationControllerPreventsDuplicateAndStaleGenerationRespawnRequests()
        {
            DateTime started = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
            var state = new PopulationRuntimeState
            {
                SpawnKey = "enemy",
                SpawnGroupKey = "g",
                PlayfieldId = 127,
                Generation = 3,
                SelectedLevel = 17,
                CurrentRuntimeIdentity = Identity.None,
                LifecycleState = PopulationLifecycleState.DeadCorpseActive
            };
            var deathScheduler = new WorldRespawnScheduler();
            RespawnPolicyDefinition deathPolicy = Fixed("death", 60);
            deathPolicy.DelayStartsAt = RespawnDelayStartsAt.Death;
            Assert.IsTrue(WorldRespawnScheduler.TryScheduleForLifecycle(deathScheduler, state, deathPolicy, RespawnDelayStartsAt.Death, started));
            Assert.IsFalse(WorldRespawnScheduler.TryScheduleForLifecycle(deathScheduler, state, deathPolicy, RespawnDelayStartsAt.Death, started));
            Assert.AreEqual(1, deathScheduler.Count, "One death may create only one pending respawn.");
            Assert.AreEqual(17, state.SelectedLevel.Value, "Scheduling must not alter the selected generation level.");

            var corpseScheduler = new WorldRespawnScheduler();
            var corpseState = new PopulationRuntimeState
            {
                SpawnKey = "corpse-enemy",
                SpawnGroupKey = "g",
                PlayfieldId = 127,
                Generation = 4,
                SelectedLevel = 19,
                CurrentRuntimeIdentity = Identity.None
            };
            RespawnPolicyDefinition corpsePolicy = Fixed("corpse", 60);
            corpsePolicy.DelayStartsAt = RespawnDelayStartsAt.CorpseRemoval;
            Assert.IsFalse(WorldRespawnScheduler.TryScheduleForLifecycle(corpseScheduler, corpseState, corpsePolicy, RespawnDelayStartsAt.Death, started));
            Assert.IsTrue(WorldRespawnScheduler.TryScheduleForLifecycle(corpseScheduler, corpseState, corpsePolicy, RespawnDelayStartsAt.CorpseRemoval, started));
            Assert.IsFalse(WorldRespawnScheduler.TryScheduleForLifecycle(corpseScheduler, corpseState, corpsePolicy, RespawnDelayStartsAt.CorpseRemoval, started));
            Assert.AreEqual(1, corpseScheduler.Count, "Repeated corpse cleanup may not duplicate a pending respawn.");

            Assert.IsFalse(WorldRespawnScheduler.IsCurrentGeneration(
                corpseState,
                new WorldRespawnSchedule { SpawnKey = corpseState.SpawnKey, Generation = 3 }));
            Assert.IsTrue(WorldRespawnScheduler.IsCurrentGeneration(
                corpseState,
                new WorldRespawnSchedule { SpawnKey = corpseState.SpawnKey, Generation = 4 }));
            corpseState.CurrentRuntimeIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = 55 };
            Assert.IsFalse(WorldRespawnScheduler.IsCurrentGeneration(
                corpseState,
                new WorldRespawnSchedule { SpawnKey = corpseState.SpawnKey, Generation = 4 }));

            var earlyScheduler = new WorldRespawnScheduler();
            DateTime earlyDue = started.AddSeconds(5);
            var earlyState = new PopulationRuntimeState
            {
                SpawnKey = "early-death-policy",
                SpawnGroupKey = "g",
                PlayfieldId = 127,
                Generation = 5,
                CurrentRuntimeIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = 66 },
                RespawnDueAt = earlyDue,
                LifecycleState = PopulationLifecycleState.WaitingForRespawn
            };
            Assert.IsTrue(earlyScheduler.Schedule(new WorldRespawnSchedule { SpawnKey = earlyState.SpawnKey, GroupKey = "g", PlayfieldId = 127, DueAtUtc = earlyDue, Generation = 5 }));
            WorldRespawnSchedule earlyWork = earlyScheduler.TakeDue(earlyDue, 1).Single();
            Assert.IsFalse(WorldRespawnScheduler.IsCurrentGeneration(earlyState, earlyWork));
            earlyState.CurrentRuntimeIdentity = Identity.None;
            Assert.IsTrue(WorldRespawnScheduler.TryResumePendingAfterRuntimeRelease(earlyScheduler, earlyState, deathPolicy, earlyDue.AddSeconds(5)));
            Assert.AreEqual(earlyDue.AddSeconds(5), earlyState.RespawnDueAt.Value);
            Assert.AreEqual(1, earlyScheduler.Count, "An early death-start timer must resume once the dead runtime is released.");
        }

        [TestMethod]
        public void NewGenerationClearsPriorDeathCorpseFailureAndScheduleState()
        {
            DateTime spawned = new DateTime(2026, 7, 16, 13, 0, 0, DateTimeKind.Utc);
            var state = new PopulationRuntimeState
            {
                SpawnKey = "enemy",
                Generation = 3,
                SelectedLevel = 17,
                SpawnedAt = spawned.AddMinutes(-5),
                DiedAt = spawned.AddMinutes(-2),
                CorpseIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = 77 },
                RespawnDueAt = spawned.AddMinutes(2),
                LifecycleState = PopulationLifecycleState.Respawning,
                FailureState = "old-generation-failure"
            };
            var generation = new OrdinaryEnemySpawnGeneration(4, Range().Resolve(23));
            var runtimeIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = 88 };
            WorldPopulationGenerationLifecycle.ApplySpawnSuccess(state, runtimeIdentity, generation, spawned);
            Assert.AreEqual(4, state.Generation);
            Assert.AreEqual(23, state.SelectedLevel.Value);
            Assert.AreEqual(runtimeIdentity, state.CurrentRuntimeIdentity);
            Assert.AreEqual(PopulationLifecycleState.Alive, state.LifecycleState);
            Assert.IsFalse(state.DiedAt.HasValue);
            Assert.AreEqual(Identity.None, state.CorpseIdentity);
            Assert.IsFalse(state.RespawnDueAt.HasValue);
            Assert.IsNull(state.FailureState);
            AssertThrows(() => WorldPopulationGenerationLifecycle.ApplySpawnSuccess(state, runtimeIdentity, generation, spawned));

            state.DiedAt = spawned;
            state.CorpseIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = 99 };
            state.FailureState = "shutdown-stale-state";
            WorldPopulationGenerationLifecycle.ClearRuntime(state, spawned.AddSeconds(1));
            Assert.AreEqual(4, state.Generation, "Clearing a runtime must retain the monotonic generation token.");
            Assert.IsFalse(state.SelectedLevel.HasValue);
            Assert.IsFalse(state.DiedAt.HasValue);
            Assert.AreEqual(Identity.None, state.CorpseIdentity);
            Assert.IsNull(state.FailureState);
            Assert.AreEqual(PopulationLifecycleState.Despawned, state.LifecycleState);
        }

        [TestMethod]
        public void VisibilityCombatCorpseAndNavigationOwnersCannotSelectOrAdvanceLevels()
        {
            string root = FindRepositoryRoot();
            string[] owners =
                {
                    "PlayfieldVisibilityInterestRuntimeService.cs",
                    "NpcCombatTickCoordinator.cs",
                    "NpcCorpseLifecycleCoordinator.cs",
                    "PlayfieldNpcCombatMovementRuntimeService.cs"
                };
            foreach (string owner in owners)
            {
                string source = Read(root, owner);
                Assert.IsFalse(source.Contains("ResolveForGeneration("), owner);
                Assert.IsFalse(source.Contains("SelectVariant("), owner);
                Assert.IsFalse(source.Contains("nextGeneration"), owner);
            }
        }

        [TestMethod]
        public void SharedLevelAndRespawnOwnersContainNoEnemySpecificSelectionLogic()
        {
            string root = FindRepositoryRoot();
            string model = Read(root, "OrdinaryEnemyProfile.cs");
            string runtime = Read(root, "OrdinaryEnemyRuntimeService.cs");
            string population = Read(root, "WorldPopulationDefinitions.cs");
            string controller = Read(root, "WorldPopulationController.cs");
            Assert.IsFalse(model.Contains("Bloodcreeper") || model.Contains("30379"));
            Assert.IsFalse(runtime.Contains("Bloodcreeper") || runtime.Contains("30379"));
            Assert.IsFalse(population.Contains("Bloodcreeper") || population.Contains("30379"));
            Assert.IsFalse(controller.Contains("Bloodcreeper") || controller.Contains("30379"));
            Assert.IsFalse(population.Contains("MonsterData"));
            Assert.IsFalse(controller.Contains("MonsterData"));
            Assert.IsTrue(controller.Contains("WorldRespawnPolicyResolver.ApplyGroupConfiguration"));
        }

        private static void AssertCapturedDamage(OrdinaryEnemyCatalog catalog, string displayName, int minimumDamage, int maximumDamage) { OrdinaryEnemyProfile profile = catalog.GetProfiles().Single(value => value.DisplayName == displayName); Assert.AreEqual(OrdinaryEnemyEvidenceState.Observed, profile.Combat.EvidenceState); Assert.AreEqual(minimumDamage, profile.Combat.Contract.MinDamage); Assert.AreEqual(maximumDamage, profile.Combat.Contract.MaxDamage); }
        private static void Validate(WorldSpawnDefinition[] spawns) { WorldPopulationDefinitionValidator.Validate(spawns, new[] { Group("g", spawns.Select(x => x.SpawnKey).ToArray()) }, new[] { Fixed("p", 60) }, new[] { "profile" }); }
        private static WorldSpawnDefinition Spawn(string key, int id) { return new WorldSpawnDefinition { SpawnKey = key, EnemyProfileKey = "profile", ConfiguredIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = id }, PlayfieldId = 127, X = 1, Y = 2, Z = 3, OrientationW = 1, SpawnGroupKey = "g", RespawnPolicyKey = "p", ActivationPolicy = WorldSpawnActivationPolicy.PlayfieldStart, Classification = WorldPopulationClassification.OrdinaryEnemy, Enabled = true }; }
        private static SpawnGroupDefinition Group(string key, params string[] spawns) { return new SpawnGroupDefinition { SpawnGroupKey = key, PlayfieldId = 127, SpawnKeys = spawns, ActivationPolicy = WorldSpawnActivationPolicy.PlayfieldStart, MinimumAlive = 0, MaximumAlive = spawns.Length, Enabled = true }; }
        private static RespawnPolicyDefinition Fixed(string key, double seconds) { return new RespawnPolicyDefinition { RespawnPolicyKey = key, Mode = WorldRespawnMode.FixedDelay, FixedDelaySeconds = seconds, DelayStartsAt = RespawnDelayStartsAt.NpcDespawn, Enabled = true }; }
        private static WorldRespawnSchedule Schedule(string key, int playfield, DateTime due) { return new WorldRespawnSchedule { SpawnKey = key, PlayfieldId = playfield, DueAtUtc = due, Generation = 1 }; }
        private static OrdinaryEnemySpawnLevelDefinition Range() { return new OrdinaryEnemySpawnLevelDefinition(OrdinaryEnemySpawnLevelMode.InclusiveRange, 15, 25, 24, 691, 33, 0, 70, 83, 3, OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration, OrdinaryEnemyEvidenceState.Policy, "range-policy"); }
        private static void AssertExplicitDelay(OrdinaryEnemySpawnDefinition spawn, double seconds) { Assert.AreEqual(WorldRespawnPolicyAssignmentMode.Explicit, spawn.RespawnPolicy.Mode); Assert.IsNotNull(spawn.RespawnPolicy.ExplicitPolicy); Assert.AreEqual(seconds, spawn.RespawnPolicy.ExplicitPolicy.FixedDelaySeconds.Value); }
        private static CapturedSubwayOrdinaryArchetypeDefinition BuildMeldedPatternsArchetype(CapturedSubwayCombatEvidenceDefinition combat, params string[] captures) { return new CapturedSubwayOrdinaryArchetypeDefinition("melded_patterns_test", "melded_patterns", "Melded Patterns", NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData, 148, 0, 268964353, 0, 0, 31, 0, 1899u, 29701, new CapturedSubwayTextureDefinition[0], new CapturedSubwayMeshDefinition[0], combat, new CapturedSubwayLootEvidenceDefinition[0], new CapturedSubwayLootOutcomeEvidenceDefinition[0], new CapturedSubwayCorpseEvidenceDefinition[0], captures); }
        private static CapturedSubwayOrdinaryArchetypeDefinition BuildLooterArchetype(CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence) { return new CapturedSubwayOrdinaryArchetypeDefinition("looter_test", "looter", "Looter", 203745, 138, 0, 268964353, 0, 0, 31, 1, 1579u, 40695, new CapturedSubwayTextureDefinition[0], new CapturedSubwayMeshDefinition[0], new CapturedSubwayCombatEvidenceDefinition(true, 11, 11, 5.282358, 6, 0, 0, 15), new CapturedSubwayLootEvidenceDefinition[0], new CapturedSubwayLootOutcomeEvidenceDefinition[0], new CapturedSubwayCorpseEvidenceDefinition[0], new[] { "20260709-212115" }, sourceWeaponEvidence); }
        private static CapturedSubwayOrdinaryArchetypeDefinition BuildDerangedShopperArchetype(CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence) { return new CapturedSubwayOrdinaryArchetypeDefinition("deranged_shopper_test", "deranged_shopper", "Deranged Shopper", 203736, 138, 0, 268964353, 0, 0, 31, 1, 1579u, 5927, new CapturedSubwayTextureDefinition[0], new CapturedSubwayMeshDefinition[0], new CapturedSubwayCombatEvidenceDefinition(true, 9, 15, 5.161083, 6, 0, 0, 8), new CapturedSubwayLootEvidenceDefinition[0], new CapturedSubwayLootOutcomeEvidenceDefinition[0], new CapturedSubwayCorpseEvidenceDefinition[0], new[] { "20260710-202132" }, sourceWeaponEvidence); }
        private static void AssertThrows(Action action) { try { action(); Assert.Fail("Expected InvalidOperationException."); } catch (InvalidOperationException) { } }
        private static string Read(string root, string file) { return System.IO.File.ReadAllText(System.IO.Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Playfields", file)); }
        private static string FindRepositoryRoot() { string current = AppDomain.CurrentDomain.BaseDirectory; while (!string.IsNullOrEmpty(current)) { if (System.IO.Directory.Exists(System.IO.Path.Combine(current, ".git"))) return current; System.IO.DirectoryInfo parent = System.IO.Directory.GetParent(current); current = parent == null ? null : parent.FullName; } throw new InvalidOperationException("Repository root not found."); }
        private sealed class FixedRandom : IPopulationRandomSource { private readonly double value; internal FixedRandom(double value) { this.value = value; } public double NextUnit() { return this.value; } }
    }
}
