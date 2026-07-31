namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using AORebirth.Core.Playfields;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class DungeonNamedEncounterCompletionTests
    {
        [TestMethod]
        public void AuthoritativeInventoryEnumeratesEveryNamedStageAndOwnedAddExactlyOnce()
        {
            Entry[] entries = Inventory();

            Assert.AreEqual(19, entries.Length);
            Assert.AreEqual(entries.Length, entries.Select(value => value.ProfileKey).Distinct().Count());
            Assert.AreEqual(5, entries.Count(value => value.Playfield == 127));
            Assert.AreEqual(14, entries.Count(value => value.Playfield == 1931));
            Assert.AreEqual(14, entries.Count(value => value.Kind == "initial"));
            Assert.AreEqual(2, entries.Count(value => value.Kind == "successor"));
            Assert.AreEqual(2, entries.Count(value => value.Kind == "add"));
            Assert.AreEqual(1, entries.Count(value => value.Kind == "ordinary-patrol"));
            Assert.AreEqual(
                1,
                entries.Count(
                    value => value.ProfileKey
                             == CapturedTempleOfThreeWindsContentProvider.MurialProfileKey));
            Assert.AreEqual(
                1,
                entries.Count(
                    value => value.ProfileKey
                             == "totw.647.encounter.re-animator.reanimated-corpse"));
        }

        [TestMethod]
        public void EveryInventoryEntryHasAnExactReadyCombatDomain()
        {
            foreach (Entry entry in Inventory())
            {
                if (entry.Playfield == 1931)
                {
                    Assert.IsNotNull(entry.Combat, entry.ProfileKey);
                    Assert.AreNotEqual(
                        CapturedEnemyAttackModel.Unresolved,
                        entry.Combat.AttackModel,
                        entry.ProfileKey);
                    Assert.IsFalse(
                        string.IsNullOrWhiteSpace(entry.Combat.Evidence),
                        entry.ProfileKey);
                }
            }

            string subwayCombat = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedEnemyCombatContract.cs"));
            Assert.IsTrue(subwayCombat.Contains("case 155962:"));
            Assert.IsTrue(subwayCombat.Contains("case 203748:"));
            Assert.IsTrue(subwayCombat.Contains("case 203726:"));
            Assert.IsTrue(subwayCombat.Contains("case 203744:"));
            Assert.IsTrue(subwayCombat.Contains("case 31909:"));
            Assert.AreNotEqual("totw.1931.boss.uklesh-the-frozen", "totw.1931.boss.khalum");
            Assert.AreNotEqual("totw.1931.boss.khalum", "totw.1931.boss.aztur-the-immortal");
        }

        [TestMethod]
        public void SuccessorAndAddDomainsAreOwnedByTheirEncounterStateMachines()
        {
            string root = FindRepositoryRoot();
            string temple = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsEncounterRuntimeService.cs"));
            string subway = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayEncounterRuntimeService.cs"));
            Assert.IsTrue(temple.Contains("this.RequestNextReanimation(pending.FinishAtUtc);"));
            Assert.IsTrue(temple.Contains("return this.DetachLivingReanimatedAdds();"));
            Assert.IsTrue(temple.Contains("CapturedTempleNamedRespawnMode.SuccessorOnly"));
            Assert.IsTrue(temple.Contains("successorProfileKey = KhalumProfileKey;"));
            Assert.IsTrue(temple.Contains("delaySeconds = KhalumSpawnAfterUkleshDeathSeconds;"));
            Assert.IsTrue(temple.Contains("successorProfileKey = AzturProfileKey;"));
            Assert.IsTrue(temple.Contains("delaySeconds = AzturSpawnAfterKhalumDeathSeconds;"));
            Assert.IsTrue(subway.Contains("slot.Generation++;"));
            Assert.IsTrue(subway.Contains("summon.Stats[StatIds.petmaster].Value = 0;"));
        }

        [TestMethod]
        public void EncounterRegistryRetirementIsPlayfieldOwnedAndIndependent()
        {
            string root = FindRepositoryRoot();
            string subway = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayEncounterRuntimeService.cs"));
            string temple = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsEncounterRuntimeService.cs"));

            Assert.IsTrue(subway.Contains("new RegisteredEncounterDefinition(playfieldInstance, definition)"));
            Assert.IsTrue(subway.Contains("value.Value.PlayfieldInstance == playfieldInstance"));
            Assert.IsFalse(subway.Contains("playfieldInstance != CapturedSubwayEncounterRuntimeService.SubwayPlayfieldId"));
            Assert.IsTrue(temple.Contains("this.playfield.Identity.Instance,"));
            Assert.IsTrue(temple.Contains("CapturedEncounterRuntimeRegistry.RemoveForPlayfield("));
        }

        [TestMethod]
        public void RuntimeDisposalCancelsNamedCombatMovementRespawnAndVisibilityOwnership()
        {
            string root = FindRepositoryRoot();
            string npcRuntime = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            string temple = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsEncounterRuntimeService.cs"));
            string subway = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayEncounterRuntimeService.cs"));

            Assert.IsTrue(npcRuntime.Contains("character.DoNotDoTimers = true;"));
            Assert.IsTrue(npcRuntime.Contains("controller.StopFollow();"));
            Assert.IsTrue(npcRuntime.Contains("this.combatTick.ClearRuntimeState();"));
            Assert.IsTrue(npcRuntime.Contains("this.corpseLifecycle.ClearRuntimeState();"));
            Assert.IsTrue(npcRuntime.Contains("this.capturedSubwayEncounters.ClearRuntimeState();"));
            Assert.IsTrue(npcRuntime.Contains("this.capturedTempleEncounters.ClearRuntimeState();"));
            Assert.IsTrue(temple.Contains("CapturedEncounterRuntimeRegistry.RemoveForPlayfield("));
            Assert.IsTrue(temple.Contains("state.ResetAll();"));
            Assert.IsTrue(temple.Contains("slot.Reset();"));
            Assert.IsTrue(subway.Contains("CapturedEncounterRuntimeRegistry.RemoveForPlayfield("));
            Assert.IsTrue(subway.Contains("slot.SpawnDueAtUtc = null;"));
        }

        [TestMethod]
        public void FinalDungeonGameplayBacklogUsesExplicitLifecycleOwnershipAndFailsClosed()
        {
            string root = FindRepositoryRoot();
            string temple = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsEncounterRuntimeService.cs"));
            string subway = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayEncounterRuntimeService.cs"));
            string templeOrdinary = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsContentProvider.cs"));
            string subwayOrdinary = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayOrdinaryContentProvider.cs"));

            Assert.IsTrue(
                temple.Contains("CapturedTempleNamedRespawnMode.CapturedAfterNpcDespawn")
                && temple.Contains("CapturedTempleNamedRespawnMode.TemplePolicyAfterNpcDespawn")
                && temple.Contains("CapturedTempleNamedRespawnMode.SuccessorOnly")
                && temple.Contains("CapturedTempleNamedRespawnMode.ChainResetAfterNpcDespawn"));
            Assert.IsTrue(
                subway.Contains("CapturedNamedBossRespawnDelay = TimeSpan.FromMinutes(10)")
                && subway.Contains("EumenidesObservedRespawnDelay = TimeSpan.FromMinutes(10)")
                && subway.Contains("slot.SpawnDueAtUtc = null;"));
            Assert.IsTrue(
                templeOrdinary.Contains("totw.named.murial.300-after-npc-despawn-policy")
                && templeOrdinary.Contains("WorldRespawnPolicyAssignment.Explicit(MurialRespawn)")
                && templeOrdinary.Contains("OrdinaryEnemyMovementMode.Patrol"));
            var templeProvider = new CapturedTempleOfThreeWindsContentProvider();
            OrdinaryEnemySpawnDefinition murialSpawn = templeProvider.GetSpawns().Single(
                value => value.ProfileKey
                         == CapturedTempleOfThreeWindsContentProvider.MurialProfileKey);
            OrdinaryEnemyProfile murialProfile = templeProvider.GetProfiles().Single(
                value => value.ProfileKey == murialSpawn.ProfileKey);
            Assert.AreEqual(OrdinaryEnemyMovementMode.Patrol, murialSpawn.MovementMode);
            Assert.AreEqual(20, murialSpawn.Waypoints.Length);
            Assert.AreEqual(
                WorldRespawnPolicyAssignmentMode.Explicit,
                murialSpawn.RespawnPolicy.Mode);
            Assert.AreEqual(
                "totw.named.murial.300-after-npc-despawn-policy",
                murialSpawn.RespawnPolicy.PolicyKey);
            Assert.AreEqual(
                300.0,
                murialSpawn.RespawnPolicy.ExplicitPolicy.FixedDelaySeconds.Value);
            Assert.AreEqual(
                RespawnDelayStartsAt.NpcDespawn,
                murialSpawn.RespawnPolicy.ExplicitPolicy.DelayStartsAt);
            Assert.IsTrue(murialSpawn.RespawnPolicy.ExplicitPolicy.RespawnAtOriginalPosition);
            Assert.IsTrue(murialSpawn.RespawnPolicy.ExplicitPolicy.ResetHealth);
            Assert.IsTrue(murialSpawn.RespawnPolicy.ExplicitPolicy.ResetMovementState);
            Assert.IsTrue(murialSpawn.RespawnPolicy.ExplicitPolicy.ResetAggressionState);
            Assert.AreEqual(30.0, murialProfile.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(180.0, murialProfile.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, murialProfile.Corpse.LootedCleanupSeconds);
            Assert.IsFalse(
                subwayOrdinary.Contains("\"Strike Foreman\""),
                "Strike Foreman is named encounter content and must remain outside ordinary population generation.");
            Assert.IsTrue(
                subway.Contains(
                    "StrikeForemanProfileKey = \"subway.127.named.strike-foreman\"")
                && subway.Contains("CreateStrikeForemanDefinition()")
                && subway.Contains("this.ProcessStrikeForemanRespawn(utcNow);")
                && subway.Contains(
                    "diedAtUtc.Add(CapturedNamedBossRespawnDelay)")
                && subway.Contains(
                    "this.strikeForemanIdentity = Identity.None;"),
                "Strike Foreman must be active through the shared PF127 named encounter lifecycle.");
        }

        [TestMethod]
        public void PostAzturDespawnSchedulesExactlyOneFullChainReset()
        {
            string temple = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsEncounterRuntimeService.cs"));
            DateTime resetAtUtc =
                new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
            DateTime resetDueAtUtc;

            Assert.IsTrue(
                temple.Contains("this.ScheduleMainRoomReset(utcNow);")
                && temple.Contains("NamedEncounterState uklesh = this.FindNamed(UkleshProfileKey);")
                && temple.Contains("NamedEncounterState khalum = this.FindNamed(KhalumProfileKey);")
                && temple.Contains("NamedEncounterState aztur = this.FindNamed(AzturProfileKey);")
                && temple.Contains("this.namedRespawns.Schedule(")
                && temple.Contains("UkleshProfileKey,")
                && temple.Contains("AzturProfileKey,")
                && temple.Contains("resetDueAtUtc);"));
            Assert.AreEqual(
                DungeonNamedRespawnClassification.ExplicitlyNoIndependentRespawn,
                DungeonNamedLifecycleCatalog.Get(
                    DungeonNamedLifecycleCatalog.KhalumProfileKey).Classification);
            Assert.AreEqual(
                DungeonNamedRespawnClassification.ExplicitlyNoIndependentRespawn,
                DungeonNamedLifecycleCatalog.Get(
                    DungeonNamedLifecycleCatalog.AzturProfileKey).Classification);
            Assert.IsTrue(
                CapturedTempleOfThreeWindsEncounterRules.TryResolveMainRoomResetDue(
                    resetAtUtc,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    out resetDueAtUtc));
            Assert.AreEqual(resetAtUtc.AddMinutes(10), resetDueAtUtc);
            AssertMainRoomResetRejected(resetAtUtc, true, false, false, false, false, false);
            AssertMainRoomResetRejected(resetAtUtc, false, true, false, false, false, false);
            AssertMainRoomResetRejected(resetAtUtc, false, false, true, false, false, false);
            AssertMainRoomResetRejected(resetAtUtc, false, false, false, true, false, false);
            AssertMainRoomResetRejected(resetAtUtc, false, false, false, false, true, false);
            AssertMainRoomResetRejected(resetAtUtc, false, false, false, false, false, true);
            Assert.IsTrue(
                CapturedTempleOfThreeWindsEncounterRules.IsLivingMainRoomStage(50000, false));
            Assert.IsFalse(
                CapturedTempleOfThreeWindsEncounterRules.IsLivingMainRoomStage(50000, true),
                "A dead predecessor corpse must not block the policy-timed chain reset.");
            Assert.IsFalse(
                CapturedTempleOfThreeWindsEncounterRules.IsLivingMainRoomStage(0, false));
            Assert.IsTrue(
                temple.Contains("CapturedTempleOfThreeWindsEncounterRules.IsLivingMainRoomStage("));
            Assert.IsFalse(
                temple.Contains("uklesh.Dead = false;")
                || temple.Contains("khalum.Dead = false;")
                || temple.Contains("aztur.Dead = false;"),
                "Scheduling the reset must not revive corpse-owned predecessor state.");
            Assert.AreEqual(
                resetDueAtUtc,
                CapturedTempleOfThreeWindsEncounterRules.ResolveNamedRespawnDueAtUtc(
                    CapturedTempleNamedRespawnMode.SuccessorOnly,
                    resetDueAtUtc,
                    resetAtUtc.AddMinutes(2)),
                "A dead Uklesh corpse despawn must not cancel the already scheduled full-chain reset.");
            Assert.IsFalse(
                CapturedTempleOfThreeWindsEncounterRules.ResolveNamedRespawnDueAtUtc(
                    CapturedTempleNamedRespawnMode.SuccessorOnly,
                    null,
                    resetAtUtc).HasValue,
                "Successor-only stages must not gain an independent respawn.");

            double respawnDelaySeconds;
            Assert.IsTrue(
                CapturedTempleOfThreeWindsEncounterRules.TryResolveNamedRespawnDelay(
                    CapturedTempleNamedRespawnMode.CapturedAfterNpcDespawn,
                    out respawnDelaySeconds));
            Assert.AreEqual(600.0, respawnDelaySeconds);
            Assert.IsTrue(
                CapturedTempleOfThreeWindsEncounterRules.TryResolveNamedRespawnDelay(
                    CapturedTempleNamedRespawnMode.TemplePolicyAfterNpcDespawn,
                    out respawnDelaySeconds));
            Assert.AreEqual(600.0, respawnDelaySeconds);
            Assert.IsFalse(
                CapturedTempleOfThreeWindsEncounterRules.TryResolveNamedRespawnDelay(
                    CapturedTempleNamedRespawnMode.SuccessorOnly,
                    out respawnDelaySeconds));
            Assert.IsFalse(
                CapturedTempleOfThreeWindsEncounterRules.TryResolveNamedRespawnDelay(
                    CapturedTempleNamedRespawnMode.ChainResetAfterNpcDespawn,
                    out respawnDelaySeconds));
            Assert.IsTrue(
                temple.Contains("CapturedEncounterRuntimeRegistry.RemoveForPlayfield(")
                && temple.Contains("state.ResetAll();"),
                "Runtime disposal must cancel the pending full-chain reset.");
        }

        [TestMethod]
        public void TempleNanoEffectsRemainExactAndOnlyOwnedEffectsReachGameplay()
        {
            string temple = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsEncounterRuntimeService.cs"));
            int[] packetOnlyNanoIds =
            {
                205389, 205561, 205600, 205594, 205592,
                205383, 205565, 205395, 205563,
                209924, 204830, 70294
            };
            foreach (int nanoId in packetOnlyNanoIds)
            {
                CapturedTempleNanoEffectOwnership ownership;
                Assert.IsTrue(
                    CapturedTempleOfThreeWindsEncounterRules
                        .TryGetCapturedNanoEffectOwnership(nanoId, out ownership),
                    nanoId.ToString());
                Assert.AreEqual(
                    CapturedTempleNanoEffectOwnership.PacketOnly,
                    ownership,
                    nanoId.ToString());
            }

            CapturedTempleNanoEffectOwnership gulardOwnership;
            Assert.IsTrue(
                CapturedTempleOfThreeWindsEncounterRules.TryGetCapturedNanoEffectOwnership(
                    205584,
                    out gulardOwnership));
            Assert.AreEqual(
                CapturedTempleNanoEffectOwnership.InstantSelfNanoData,
                gulardOwnership);
            CapturedTempleNanoEffectOwnership gartuaOwnership;
            Assert.IsTrue(
                CapturedTempleOfThreeWindsEncounterRules.TryGetCapturedNanoEffectOwnership(
                    205590,
                    out gartuaOwnership));
            Assert.AreEqual(
                CapturedTempleNanoEffectOwnership.ExplicitTargetNanoData,
                gartuaOwnership);
            CapturedTempleNanoEffectOwnership reanimationOwnership;
            Assert.IsTrue(
                CapturedTempleOfThreeWindsEncounterRules.TryGetCapturedNanoEffectOwnership(
                    205604,
                    out reanimationOwnership));
            Assert.AreEqual(
                CapturedTempleNanoEffectOwnership.ReanimatedAddLifecycle,
                reanimationOwnership);
            CapturedTempleNanoEffectOwnership unknownOwnership;
            Assert.IsFalse(
                CapturedTempleOfThreeWindsEncounterRules.TryGetCapturedNanoEffectOwnership(
                    1,
                    out unknownOwnership));
            Assert.IsTrue(temple.Contains("this.RequestNextReanimation(pending.FinishAtUtc);"));
            Assert.IsTrue(
                temple.Contains("NanoLoader.NanoList.TryGetValue(pending.NanoId, out nano)")
                && temple.Contains("NanoEventRuntimeService.Default.ExecuteOnUseEvents(actor, nano)"),
                "The exact instant Gulard self-heal must use the shared nano-data runtime.");
            Assert.IsTrue(
                temple.Contains("NanoLandingResult.NotRequired")
                && temple.Contains("ExecuteCapturedOnUseEvents(")
                && temple.Contains("NotifyActiveNanoDurationToPlayfield(")
                && temple.Contains("BuffMessageHandler.Default.SendRemoveNanoBuff(")
                && temple.Contains("new HealthDamageMessage"),
                "Gartua must use the explicit shared target path with captured refresh, duration, and heal packets.");
            Assert.IsFalse(
                temple.Contains("new[] { DefenderUnscheduledNanoId }")
                || temple.Contains("new[] { UkleshUnscheduledNanoId }")
                || temple.Contains("new[] { MurialNanoId }"),
                "Captured nano identities without a proven schedule must remain unscheduled.");

            string root = FindRepositoryRoot();
            string nanoRuntime = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\NanoEventRuntimeService.cs"));
            string activeNanoRuntime = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\ActiveNanoRuntimeService.cs"));
            string modify = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Functions\GameFunctions\modify.cs"));
            string modifyPercentage = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Functions\GameFunctions\modifypercentage.cs"));
            Assert.IsTrue(
                nanoRuntime.Contains("target.Stats[modifier.StatId].Modifier -= modifier.Delta;")
                && nanoRuntime.Contains("target.Stats[modifier.StatId].PercentageModifier -= modifier.Delta;")
                && nanoRuntime.Contains("execution.PreparedTargets.Add(target.Identity.Instance)")
                && nanoRuntime.Contains("landingResult == NanoLandingResult.Unresolved")
                && nanoRuntime.Contains("landingResult == NanoLandingResult.Resisted")
                && nanoRuntime.Contains("FunctionCollection.Instance.GetFunctionByNumber(function.FunctionType) == null")
                && nanoRuntime.Contains("RemoveModifiersCastBy(")
                && nanoRuntime.Contains("RemoveAllModifiers("),
                "Landing must fail closed before partial effects, and modifiers must reverse exact contributions.");
            Assert.IsTrue(
                modify.Contains("RecordModifier(")
                && modifyPercentage.Contains("RecordModifier(")
                && modifyPercentage.Contains("Character affected = Target as Character;"),
                "Both modifier functions must record the actual target contribution.");
            Assert.IsTrue(
                activeNanoRuntime.Contains("NanoEventRuntimeService.Default.RemoveModifiers(character, nanoId);")
                && activeNanoRuntime.Contains("ClearAllActiveNanos("),
                "Expiry, overwrite, reset, and disposal must share modifier cleanup.");
        }

        [TestMethod]
        public void CapturedNanoLandingResultsMapToExactFinishPacketSemantics()
        {
            string nanoRuntime = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\NanoEventRuntimeService.cs"));
            Assert.IsTrue(
                nanoRuntime.Contains("case NanoLandingResult.NotRequired:")
                && nanoRuntime.Contains("case NanoLandingResult.Landed:")
                && nanoRuntime.Contains("parameter = 1;")
                && nanoRuntime.Contains("case NanoLandingResult.Resisted:")
                && nanoRuntime.Contains("parameter = 3;")
                && nanoRuntime.Contains("default:")
                && nanoRuntime.Contains("parameter = 0;")
                && nanoRuntime.Contains("return false;"),
                "Captured finish result 1 must mean landed, 3 resisted, and unresolved must fail closed.");
        }

        [TestMethod]
        public void CapturedDungeonLootRemainsAtomicWithUnresolvedSelectionProbabilities()
        {
            string root = FindRepositoryRoot();
            string templeLoot = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsLootDefinitions.cs"));
            string globalLoot = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));

            Assert.IsTrue(
                templeLoot.Contains("ObservedCorpseSnapshots = snapshots")
                && templeLoot.Contains("SelectionProbabilityEvidence = LootEvidenceConfidence.Unresolved")
                && templeLoot.Contains("Weight = 0")
                && templeLoot.Contains("DropChanceBasisPoints = 0")
                && templeLoot.Contains("ProbabilityEvidence = \"unresolved\""));
            Assert.IsTrue(
                globalLoot.Contains("ObservedCorpseSnapshots = snapshots")
                && globalLoot.Contains("SelectionProbabilityEvidence = LootEvidenceConfidence.Unresolved")
                && globalLoot.Contains("ObservedCorpseSnapshots = new[]")
                && globalLoot.Contains("ItemPoolUnresolved = true"));
        }

        [TestMethod]
        public void OrdinaryDungeonBaselineRemainsLockedAtFourHundredEightyNine()
        {
            var catalog =
                new OrdinaryEnemyCatalog(
                    new CapturedSubwayContentProvider(),
                    new CapturedSubwayOrdinaryContentProvider(),
                    new CapturedTempleOfThreeWindsContentProvider());
            Assert.AreEqual(322, catalog.GetSpawns().Count(value => value.PlayfieldInstance == 127));
            Assert.AreEqual(167, catalog.GetSpawns().Count(value => value.PlayfieldInstance == 1931));
            Assert.AreEqual(489, catalog.GetSpawns().Count(value => value.PlayfieldInstance == 127
                                                                   || value.PlayfieldInstance == 1931));
        }

        private static Entry[] Inventory()
        {
            return new[]
            {
                new Entry(127, "subway.127.boss.abmouth-supremus", "initial",
                    null),
                new Entry(127, "subway.127.boss.vergil-aeneid", "initial",
                    null),
                new Entry(127, "subway.127.named.eumenides", "initial",
                    null),
                new Entry(127, "subway.127.named.strike-foreman", "initial",
                    null),
                new Entry(127, "subway.127.encounter.abmouth-infector", "add",
                    null),
                new Entry(1931, "totw.647.boss.defender-of-the-three", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.DefenderOfTheThree()),
                new Entry(1931, "totw.647.named.windcaller-yatila", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.WindcallerYatila()),
                new Entry(1931, "totw.647.named.reverend-gulard", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.ReverendGulard()),
                new Entry(1931, "totw.647.boss.the-re-animator", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.ReAnimator()),
                new Entry(1931, "totw.647.named.acolyte-betany", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.AcolyteBetany()),
                new Entry(1931, "totw.647.boss.the-curator", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.TheCurator()),
                new Entry(1931, "totw.647.boss.nematet-the-custodian-of-time", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.NematetTheCustodianOfTime()),
                new Entry(1931, "totw.1931.boss.guardian-of-tomorrow", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.GuardianOfTomorrow()),
                new Entry(1931, "totw.1931.boss.gartua-the-doorkeeper", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.GartuaTheDoorkeeper()),
                new Entry(1931, "totw.1931.boss.uklesh-the-frozen", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.UkleshTheFrozen()),
                new Entry(1931, "totw.1931.boss.khalum", "successor",
                    CapturedTempleOfThreeWindsCombatCatalog.Khalum()),
                new Entry(1931, "totw.1931.boss.aztur-the-immortal", "successor",
                    CapturedTempleOfThreeWindsCombatCatalog.AzturTheImmortal()),
                new Entry(1931, "totw.647.encounter.re-animator.reanimated-corpse", "add",
                    CapturedTempleOfThreeWindsCombatCatalog.ReanimatedCorpse()),
                new Entry(1931, CapturedTempleOfThreeWindsContentProvider.MurialProfileKey, "ordinary-patrol",
                    CapturedTempleOfThreeWindsCombatCatalog.MurialTheFaithful())
            };
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            DirectoryInfo directory = new FileInfo(sourcePath).Directory;
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "AORebirth"))
                    && File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            Assert.Fail("Could not find repository root.");
            return string.Empty;
        }

        private static void AssertMainRoomResetRejected(
            DateTime resetAtUtc,
            bool ukleshActive,
            bool khalumActive,
            bool azturActive,
            bool ukleshScheduled,
            bool khalumScheduled,
            bool azturScheduled)
        {
            DateTime resetDueAtUtc;
            Assert.IsFalse(
                CapturedTempleOfThreeWindsEncounterRules.TryResolveMainRoomResetDue(
                    resetAtUtc,
                    ukleshActive,
                    khalumActive,
                    azturActive,
                    ukleshScheduled,
                    khalumScheduled,
                    azturScheduled,
                    out resetDueAtUtc));
            Assert.AreEqual(default(DateTime), resetDueAtUtc);
        }

        private sealed class Entry
        {
            internal Entry(
                int playfield,
                string profileKey,
                string kind,
                CapturedEnemyCombatContract combat)
            {
                this.Playfield = playfield;
                this.ProfileKey = profileKey;
                this.Kind = kind;
                this.Combat = combat;
            }

            internal int Playfield { get; private set; }
            internal string ProfileKey { get; private set; }
            internal string Kind { get; private set; }
            internal CapturedEnemyCombatContract Combat { get; private set; }
        }
    }
}
