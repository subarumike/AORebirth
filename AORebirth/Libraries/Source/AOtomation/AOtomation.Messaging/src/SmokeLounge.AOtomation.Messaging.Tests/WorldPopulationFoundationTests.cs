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
            Assert.AreEqual(260, spawns.Length);
            Assert.AreEqual(222, spawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
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
            AssertExplicitDelay(thief, 60);
            AssertExplicitDelay(flea, 240);
            AssertExplicitDelay(bloodcreeper, 240);
            Assert.AreEqual(OrdinaryEnemySpawnLevelMode.InclusiveRange, bloodcreeper.LevelDefinition.Mode);
            Assert.AreEqual(OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration, bloodcreeper.LevelDefinition.RerollPolicy);
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

        private static void Validate(WorldSpawnDefinition[] spawns) { WorldPopulationDefinitionValidator.Validate(spawns, new[] { Group("g", spawns.Select(x => x.SpawnKey).ToArray()) }, new[] { Fixed("p", 60) }, new[] { "profile" }); }
        private static WorldSpawnDefinition Spawn(string key, int id) { return new WorldSpawnDefinition { SpawnKey = key, EnemyProfileKey = "profile", ConfiguredIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = id }, PlayfieldId = 127, X = 1, Y = 2, Z = 3, OrientationW = 1, SpawnGroupKey = "g", RespawnPolicyKey = "p", ActivationPolicy = WorldSpawnActivationPolicy.PlayfieldStart, Classification = WorldPopulationClassification.OrdinaryEnemy, Enabled = true }; }
        private static SpawnGroupDefinition Group(string key, params string[] spawns) { return new SpawnGroupDefinition { SpawnGroupKey = key, PlayfieldId = 127, SpawnKeys = spawns, ActivationPolicy = WorldSpawnActivationPolicy.PlayfieldStart, MinimumAlive = 0, MaximumAlive = spawns.Length, Enabled = true }; }
        private static RespawnPolicyDefinition Fixed(string key, double seconds) { return new RespawnPolicyDefinition { RespawnPolicyKey = key, Mode = WorldRespawnMode.FixedDelay, FixedDelaySeconds = seconds, DelayStartsAt = RespawnDelayStartsAt.NpcDespawn, Enabled = true }; }
        private static WorldRespawnSchedule Schedule(string key, int playfield, DateTime due) { return new WorldRespawnSchedule { SpawnKey = key, PlayfieldId = playfield, DueAtUtc = due, Generation = 1 }; }
        private static OrdinaryEnemySpawnLevelDefinition Range() { return new OrdinaryEnemySpawnLevelDefinition(OrdinaryEnemySpawnLevelMode.InclusiveRange, 15, 25, 24, 691, 33, 0, 70, 83, 3, OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration, OrdinaryEnemyEvidenceState.Policy, "range-policy"); }
        private static void AssertExplicitDelay(OrdinaryEnemySpawnDefinition spawn, double seconds) { Assert.AreEqual(WorldRespawnPolicyAssignmentMode.Explicit, spawn.RespawnPolicy.Mode); Assert.IsNotNull(spawn.RespawnPolicy.ExplicitPolicy); Assert.AreEqual(seconds, spawn.RespawnPolicy.ExplicitPolicy.FixedDelaySeconds.Value); }
        private static void AssertThrows(Action action) { try { action(); Assert.Fail("Expected InvalidOperationException."); } catch (InvalidOperationException) { } }
        private static string Read(string root, string file) { return System.IO.File.ReadAllText(System.IO.Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Playfields", file)); }
        private static string FindRepositoryRoot() { string current = AppDomain.CurrentDomain.BaseDirectory; while (!string.IsNullOrEmpty(current)) { if (System.IO.Directory.Exists(System.IO.Path.Combine(current, ".git"))) return current; System.IO.DirectoryInfo parent = System.IO.Directory.GetParent(current); current = parent == null ? null : parent.FullName; } throw new InvalidOperationException("Repository root not found."); }
        private sealed class FixedRandom : IPopulationRandomSource { private readonly double value; internal FixedRandom(double value) { this.value = value; } public double NextUnit() { return this.value; } }
    }
}
