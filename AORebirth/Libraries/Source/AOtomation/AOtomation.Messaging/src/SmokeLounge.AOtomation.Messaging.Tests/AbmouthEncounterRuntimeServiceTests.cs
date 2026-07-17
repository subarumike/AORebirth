namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    using MessagingStreamReader = SmokeLounge.AOtomation.Messaging.Serialization.StreamReader;
    using MessagingStreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    [TestClass]
    public class AbmouthEncounterRuntimeServiceTests
    {
        private const string CapturedReplacementInfectorScfuHex =
            "271B3A6B0000C35079607AD0003A022A4A430015300843A7038C42933C6442C617A4000000003F37"
            + "4729000000003F32BB6F000004C809496E666563746F7200100812010000000096000A00001803C8"
            + "0000007CA50046001F000000001C0000000000000000800000000301000100010001000100000002"
            + "000069000003F1000017A60000000000000000000000000000000100000000000000000000000200"
            + "00000000000000000000030000000000000000000000040000000000000000000003F1000000020000";

        [TestMethod]
        public void DedicatedEncounterOwnsAbmouthAndOrdinaryPopulationRejectsBossesAndSummons()
        {
            string root = FindRepositoryRoot();
            string encounter = ReadPlayfieldSource(root, "CapturedSubwayEncounterRuntimeService.cs");
            string npcRuntime = ReadPlayfieldSource(root, "NPCRuntimeService.cs");
            string ordinaryProvider = ReadPlayfieldSource(root, "CapturedSubwayOrdinaryContentProvider.cs");
            string populationDefinitions = ReadPlayfieldSource(root, "WorldPopulationDefinitions.cs");

            Assert.IsTrue(
                encounter.Contains("internal sealed class CapturedSubwayEncounterRuntimeService")
                && encounter.Contains("internal const int SubwayPlayfieldId = 127;")
                && encounter.Contains("internal const string EncounterKey = \"subway.127.encounter.abmouth\";")
                && encounter.Contains("if (playfieldIdentity.Instance != SubwayPlayfieldId"),
                "Abmouth must remain owned by one PF127-only dedicated encounter runtime.");
            Assert.IsTrue(
                npcRuntime.Contains("private readonly CapturedSubwayEncounterRuntimeService capturedSubwayEncounters;")
                && npcRuntime.Contains("new CapturedSubwayEncounterRuntimeService(")
                && npcRuntime.Contains("this.worldPopulation.ActivatePlayfield(playfieldIdentity);\n            this.capturedSubwayEncounters.ActivatePlayfield(playfieldIdentity);")
                && npcRuntime.Contains("this.capturedSubwayEncounters.ProcessDue(utcNow, this.AcquireAggro);"),
                "NPCRuntimeService must retain the dedicated encounter owner after ordinary population activation.");
            Assert.IsFalse(
                ordinaryProvider.Contains("Abmouth Supremus") || ordinaryProvider.Contains("155962"),
                "Abmouth must not be reintroduced through ordinary Subway population rows.");
            Assert.IsTrue(
                populationDefinitions.Contains("spawn.OwnedSummon || spawn.BossOrScripted"),
                "The normalized ordinary-world validator must continue rejecting owned summons and scripted bosses.");
            Assert.IsTrue(
                encounter.Contains("IsEncounterSummon")
                && encounter.Contains("InfectorProfileKey = \"subway.127.encounter.abmouth-infector\""),
                "Abmouth-owned Infectors must remain encounter summons, separate from ordinary Infector rows.");
        }

        [TestMethod]
        public void CapturedBossAndSummonDefinitionsPreserveExactScfuSpawnFacts()
        {
            string encounter = ReadPlayfieldSource(FindRepositoryRoot(), "CapturedSubwayEncounterRuntimeService.cs");

            Assert.IsTrue(
                encounter.Contains("internal const int AbmouthMonsterData = 155962;")
                && encounter.Contains("\"Abmouth Supremus\",")
                && encounter.Contains("30,\n                10324,\n                162,\n                115,\n                114,\n                0,\n                3,")
                && encounter.Contains("357.088409f,\n                76.107948f,\n                99.123543f,")
                && encounter.Contains("-0.713226199f")
                && encounter.Contains("0.700933933f"),
                "The boss definition must preserve the captured template, stats, position, and heading.");
            Assert.IsTrue(
                encounter.Contains("internal const int InfectorMonsterData = 31909;")
                && encounter.Contains("24,\n                968,\n                70,\n                162,\n                105,\n                10,\n                0,")
                && encounter.Contains("355.542145f,\n                68.955902f,\n                99.459953f,")
                && encounter.Contains("350.425507f,\n                71.647079f,\n                99.786812f,")
                && encounter.Contains("-0.673485816f")
                && encounter.Contains("0.739200115f")
                && encounter.Contains("-0.715518296f")
                && encounter.Contains("0.698594034f"),
                "Both initial Infector slots must preserve their distinct captured SCFU positions and headings.");
            Assert.IsTrue(
                encounter.Contains("0x04CB,")
                && encounter.Contains("0x04C8,")
                && CountOccurrences(encounter, "unchecked((int)0x022A4A43)") == 2
                && encounter.Contains("HexToBytes(\"80000000000000008000000003010001000100010001000000020000\")")
                && encounter.Contains("FirstInfectorUnknown1")
                && encounter.Contains("SecondInfectorUnknown1")
                && encounter.Contains("ReplacementInfectorUnknown1"),
                "Captured appearance values, SCFU flags, and unknown blocks must not drift.");
        }

        [TestMethod]
        public void EncounterCapsTwoSlotsRefillsOnlyDuringCombatAndCleansOnBossDeath()
        {
            string root = FindRepositoryRoot();
            string encounter = ReadPlayfieldSource(root, "CapturedSubwayEncounterRuntimeService.cs");
            string npcRuntime = ReadPlayfieldSource(root, "NPCRuntimeService.cs");

            Assert.IsTrue(
                encounter.Contains("new InfectorSlotState(0)")
                && encounter.Contains("new InfectorSlotState(1)")
                && !encounter.Contains("new InfectorSlotState(2)"),
                "The encounter must remain capped at two owned Infector slots.");
            Assert.IsTrue(
                encounter.Contains("FirstInfectorDelaySeconds = 1.212281")
                && encounter.Contains("SecondInfectorDelaySeconds = 2.326367")
                && encounter.Contains("CapturedRefillDelays = { 0.830, 0.380, 3.322, 3.490 }")
                && encounter.Contains("CapturedReplacementInfectorOffsetX = 3.0f")
                && encounter.Contains("boss.RawCoordinates.X + CapturedReplacementInfectorOffsetX")
                && encounter.Contains("slot.ActiveIdentity.Instance != 0")
                && encounter.Contains("if (!this.abmouthDead && this.combatActive && this.abmouthIdentity.Instance != 0)"),
                "Initial summon timing and captured live-fight refill gating must remain explicit.");
            Assert.IsTrue(
                encounter.Contains("this.abmouthDead = true;")
                && encounter.Contains("this.combatActive = false;")
                && encounter.Contains("slot.SpawnDueAtUtc = null;")
                && encounter.Contains("summon.Stats[StatIds.petmaster].Value = 0;")
                && encounter.Contains("SetStat(character, StatIds.flags, unchecked((int)0x18081201));")
                && encounter.Contains("return livingSummons.ToArray();"),
                "Boss death must cancel refills, clear summon ownership, and return every living summon for despawn.");
            Assert.IsTrue(
                npcRuntime.Contains("foreach (ICharacter summon in this.capturedSubwayEncounters.NotifyDeath(target, diedAtUtc))")
                && npcRuntime.Contains("this.playfield.DespawnNpcImmediately(summon);")
                && npcRuntime.Contains("this.capturedSubwayEncounters.NotifyNpcDespawn(target, utcNow);")
                && npcRuntime.Contains("CapturedEncounterRuntimeRegistry.Remove(target.Identity.Instance);"),
                "NPCRuntimeService must immediately despawn both living summons and remove encounter registration.");
        }

        [TestMethod]
        public void LeashResetCancelsBossEncounterStateAndLivingSummons()
        {
            string root = FindRepositoryRoot();
            string encounter = ReadPlayfieldSource(root, "CapturedSubwayEncounterRuntimeService.cs");
            string npcRuntime = ReadPlayfieldSource(root, "NPCRuntimeService.cs");

            Assert.IsTrue(
                encounter.Contains("internal ICharacter[] NotifyCombatReset(ICharacter npc)")
                && encounter.Contains("this.ClearVergilCombatState();")
                && encounter.Contains("this.combatActive = false;")
                && encounter.Contains("this.refillDelayIndex = 0;")
                && encounter.Contains("slot.SpawnDueAtUtc = null;")
                && encounter.Contains("slot.ActiveIdentity = Identity.None;")
                && encounter.Contains("slot.Generation = 0;")
                && encounter.Contains("return activeSummons.ToArray();"),
                "Leashing a captured boss must cancel pending combat-only encounter state.");
            Assert.IsTrue(
                npcRuntime.Contains("this.capturedSubwayEncounters.NotifyCombatReset(npc)")
                && npcRuntime.Contains("this.playfield.DespawnNpcImmediately(summon);"),
                "The shared NPC leash must immediately remove Abmouth's living encounter summons.");
        }

        [TestMethod]
        public void AbmouthUsesIndependentXopzAndDenwStreamsWhileSummonsUseDmxf()
        {
            string root = FindRepositoryRoot();
            string rules = ReadPlayfieldSource(root, "NpcCombatAttackRules.cs");
            string contracts = ReadPlayfieldSource(root, "CapturedEnemyCombatContract.cs");
            string coordinator = ReadPlayfieldSource(root, "NpcCombatTickCoordinator.cs");

            Assert.IsTrue(
                rules.Contains("CapturedSubwayAbmouthXopzMinimumDamage = 74")
                && rules.Contains("CapturedSubwayAbmouthXopzMaximumDamage = 96")
                && rules.Contains("CapturedSubwayAbmouthXopzTag = 0x584F505A")
                && rules.Contains("CapturedSubwayAbmouthDenwMinimumDamage = 115")
                && rules.Contains("CapturedSubwayAbmouthDenwMaximumDamage = 126")
                && rules.Contains("CapturedSubwayAbmouthDenwTag = 0x44454E57")
                && rules.Contains("CapturedSubwayAbmouthAttackCycleSeconds = 6.3"),
                "Captured XOPZ and DENW damage, tags, and independent cadence must remain exact.");
            Assert.IsTrue(
                contracts.Contains("case 155962:")
                && contracts.Contains("CapturedEnemyCombatContract.CapturedParallelAttackSequence")
                && contracts.Contains("CapturedSubwayAbmouthXopzFirstInitialSeconds")
                && contracts.Contains("CapturedSubwayAbmouthDenwInitialSeconds")
                && contracts.Contains("CapturedSubwayAbmouthXopzSecondInitialSeconds")
                && CountOccurrences(contracts, "abmouthXopzAttack)") == 2
                && CountOccurrences(contracts, "abmouthDenwAttack)") == 1,
                "The boss contract must preserve two XOPZ clocks and one DENW clock instead of flattening them.");
            Assert.IsTrue(
                coordinator.Contains("nextCapturedParallelAttackTicks")
                && coordinator.Contains("nextTicks[index] <= now && nextTicks[index] < dueAt")
                && coordinator.Contains("nextTicks[dueIndex] = now + TimeSpan.FromSeconds(attack.RechargeSeconds);"),
                "Parallel captured streams must schedule and recharge independently.");
            Assert.IsTrue(
                rules.Contains("CapturedSubwayAbmouthInfectorMinimumDamage = 21")
                && rules.Contains("CapturedSubwayAbmouthInfectorMaximumDamage = 26")
                && rules.Contains("CapturedSubwayAbmouthInfectorRechargeSeconds = 3.7")
                && rules.Contains("CapturedSubwayAbmouthInfectorTag = 0x444D5846")
                && contracts.Contains("case 31909:")
                && contracts.Contains("Abmouth-owned Infector DMXF attacks"),
                "Owned Infector combat must retain its distinct DMXF stream and captured damage range.");
        }

        [TestMethod]
        public void CapturedAppearanceCorpseAndLootContextRemainDedicatedToTheBoss()
        {
            string root = FindRepositoryRoot();
            string encounter = ReadPlayfieldSource(root, "CapturedSubwayEncounterRuntimeService.cs");
            string npcRuntime = ReadPlayfieldSource(root, "NPCRuntimeService.cs");
            string scfu = ReadPacketSource(root, "SimpleCharFullUpdate.cs");
            string corpse = ReadPacketSource(root, "CorpseFullUpdate.cs");
            string playfield = ReadPlayfieldSource(root, "Playfield.cs");
            string lootDefinitions = ReadPlayfieldSource(root, "LootDefinitions.cs");
            string globalLoot = ReadPlayfieldSource(root, "GlobalLootRuntimeService.cs");
            string lootRules = ReadPlayfieldSource(root, "SubwayLootPoolRules.cs");
            string lootGeneration = ReadPlayfieldSource(root, "LootGenerationService.cs");

            Assert.IsTrue(
                encounter.Contains("CapturedScfuFlags")
                && encounter.Contains("CapturedScfuFlags2")
                && encounter.Contains("CapturedScfuUnknown1")
                && encounter.Contains("CapturedScfuUnknown2")
                && encounter.Contains("155548,\n                1800.0,\n                3.0,")
                && encounter.Contains("31868,\n                300.0,\n                3.0,"),
                "Runtime SCFU and corpse definitions must retain captured boss/summon constants and corpse lifetimes.");
            Assert.IsTrue(
                scfu.Contains("CapturedEncounterRuntimeRegistry.TryGet")
                && scfu.Contains("scfu.Version = 58;")
                && scfu.Contains("scfu.Appearance.Value = encounterRuntime.AppearanceValue;")
                && scfu.Contains("encounterRuntime.CapturedScfuRunSpeedBase")
                && scfu.Contains("scfu.Flags2 = (byte)encounterRuntime.CapturedScfuFlags2;")
                && scfu.Contains("scfu.Unknown1 = encounterRuntime.CapturedScfuUnknown1.ToArray();")
                && scfu.Contains("capturedNpcInfo.UnknownData = (byte)encounterRuntime.CapturedScfuNpcUnknownData;")
                && scfu.Contains("encounterRuntime.Textures.Select(")
                && scfu.Contains("encounterRuntime.Meshes.Select(")
                && scfu.Contains("encounterRuntime.Waypoints.Select("),
                "SCFU serialization must consume the dedicated encounter definition without generic appearance fallback.");
            Assert.IsTrue(
                npcRuntime.Contains("this.capturedSubwayEncounters.FindAutomaticAggroTarget(character)")
                && npcRuntime.Contains("?? this.ordinaryEnemies.FindAutomaticAggroTarget(character)")
                && npcRuntime.Contains("this.capturedSubwayEncounters.NotifyCombatStarted(target, attacker, DateTime.UtcNow);"),
                "Captured proactive aggro and summon timing must start through the encounter before ordinary fallback.");
            Assert.IsTrue(
                corpse.Contains("CapturedSubwayAbmouthPacketLength = 415")
                && corpse.Contains("CapturedSubwayAbmouthMonsterDataOffset = 331")
                && corpse.Contains("CapturedSubwayAbmouthTailDeadNpcInstanceOffset = 343")
                && corpse.Contains("BuildCapturedSubwayAbmouth(")
                && corpse.Contains("WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);")
                && corpse.Contains("CapturedSubwayAbmouthMonsterDataOffset, corpseMonsterData")
                && playfield.Contains("CapturedEncounterRuntimeRegistry.TryGet")
                && playfield.Contains("encounterDefinition.CorpseCatMesh")
                && playfield.Contains("encounterDefinition.UnlootedCorpseLifetimeSeconds")
                && playfield.Contains("encounterDefinition.LootedCleanupSeconds"),
                "Corpse serialization/state must preserve the 415-byte Abmouth template and encounter-owned visual/lifetimes.");
            Assert.IsTrue(
                encounter.Contains("AbmouthProfileKey = \"subway.127.boss.abmouth-supremus\"")
                && encounter.Contains("EncounterKey = \"subway.127.encounter.abmouth\"")
                && lootRules.Contains("if (context.IsBoss)")
                && lootRules.Contains("SubwayLootPoolKind.Boss")
                && lootGeneration.Contains("case LootAssignmentTargetType.Boss:")
                && lootGeneration.Contains("context.IsBoss && Same(assignment.TargetKey, context.EnemyProfileKey)"),
                "Abmouth loot must resolve through its dedicated boss profile rather than ordinary dungeon/enemy fallback.");
            Assert.IsTrue(
                lootDefinitions.Contains("ObservedSnapshot")
                && lootDefinitions.Contains("ItemPoolUnresolved")
                && globalLoot.Contains("AbmouthEncounterRuntimeService.AbmouthProfileKey")
                && globalLoot.Contains("ItemPoolUnresolved = true")
                && globalLoot.Contains("ObservedCorpseSnapshots = snapshots")
                && globalLoot.Contains("\"capture.20260712-232137\",")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, \"capture.20260712-232137\", 136622, 136623, 30, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, \"capture.20260712-232137\", 202717, 202718, 28, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, \"capture.20260712-232137\", 107933, 107934, 23, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, \"capture.20260712-232137\", 85693, 27389, 30, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, \"capture.20260712-232137\", 287146, 287146, 200, 1)")
                && globalLoot.Contains("\"capture.20260716-220400\",")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, \"capture.20260716-220400\", 202741, 202742, 32, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, \"capture.20260716-220400\", 202734, 202735, 32, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, \"capture.20260716-220400\", 202717, 202718, 32, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, \"capture.20260716-220400\", 85723, 85722, 32, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, \"capture.20260716-220400\", 123968, 123970, 25, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, \"capture.20260716-220400\", 287146, 287146, 200, 1)")
                && globalLoot.Contains("CapturedAbmouthCredits = 587")
                && globalLoot.Contains("Mode = CreditsPolicyMode.Unresolved")
                && globalLoot.Contains("SelectionProbabilityEvidence = LootEvidenceConfidence.Unresolved"),
                "Both exact Abmouth item-plus-credit snapshots must remain atomic while the wider pool and selection probabilities stay unresolved.");
        }

        [TestMethod]
        public void NamedBossesRespawnTenMinutesAfterDeathIndependentlyOfCorpses()
        {
            string root = FindRepositoryRoot();
            string encounter = ReadPlayfieldSource(root, "CapturedSubwayEncounterRuntimeService.cs");
            string npcRuntime = ReadPlayfieldSource(root, "NPCRuntimeService.cs");

            Assert.IsTrue(
                encounter.Contains("CapturedNamedBossRespawnDelay = TimeSpan.FromMinutes(10)")
                && encounter.Contains("private DateTime? abmouthRespawnDueAtUtc;")
                && encounter.Contains("private DateTime? vergilRespawnDueAtUtc;")
                && encounter.Contains("this.abmouthRespawnDueAtUtc = diedAtUtc.Add(CapturedNamedBossRespawnDelay);")
                && encounter.Contains("this.vergilRespawnDueAtUtc = diedAtUtc.Add(CapturedNamedBossRespawnDelay);")
                && encounter.Contains("this.ProcessNamedBossRespawns(utcNow);")
                && CountOccurrences(encounter, "CreateBossDefinition()") == 3
                && CountOccurrences(encounter, "CreateVergilAeneidDefinition()") == 3,
                "Abmouth and Vergil must use the confirmed ten-minute post-death named-boss respawn path.");
            Assert.IsTrue(
                encounter.Contains("this.abmouthIdentity.Instance == 0 && !this.abmouthRespawnDueAtUtc.HasValue")
                && encounter.Contains("this.vergilAeneidIdentity.Instance == 0 && !this.vergilRespawnDueAtUtc.HasValue")
                && encounter.Contains("this.abmouthRespawnDueAtUtc.Value <= utcNow")
                && encounter.Contains("this.vergilRespawnDueAtUtc.Value <= utcNow"),
                "Playfield activation must not bypass a pending boss timer, and due retries must wait for the dead NPC identity to clear.");
            Assert.IsTrue(
                npcRuntime.Contains("DateTime diedAtUtc = DateTime.UtcNow;")
                && npcRuntime.Contains("this.worldPopulation.NotifyDeath(target, corpseIdentity, diedAtUtc);")
                && npcRuntime.Contains("this.capturedSubwayEncounters.NotifyDeath(target, diedAtUtc)"),
                "Ordinary population and named encounters must receive the same death-time boundary.");
            Assert.IsTrue(
                encounter.IndexOf("this.ProcessNamedBossRespawns(utcNow);", StringComparison.Ordinal)
                < encounter.IndexOf(
                    "if (!this.combatActive || this.abmouthDead || this.abmouthIdentity.Instance == 0)",
                    StringComparison.Ordinal),
                "Named-boss respawn processing must run before the Abmouth combat-only early return.");
        }

        [TestMethod]
        public void VergilProfileCannotActivateAbmouthAggroSummonsOrDeathCleanup()
        {
            string encounter = ReadPlayfieldSource(
                FindRepositoryRoot(),
                "CapturedSubwayEncounterRuntimeService.cs");
            int automaticAggro = encounter.IndexOf(
                "internal ICharacter FindAutomaticAggroTarget",
                StringComparison.Ordinal);
            int notifyCombat = encounter.IndexOf(
                "internal void NotifyCombatStarted",
                automaticAggro,
                StringComparison.Ordinal);
            int processDue = encounter.IndexOf(
                "internal void ProcessDue",
                notifyCombat,
                StringComparison.Ordinal);
            int notifyDeath = encounter.IndexOf(
                "internal ICharacter[] NotifyDeath",
                processDue,
                StringComparison.Ordinal);

            Assert.IsTrue(
                encounter.Contains("VergilAeneidProfileKey = \"subway.127.boss.vergil-aeneid\"")
                && encounter.Contains("VergilAeneidEncounterKey = \"subway.127.encounter.vergil-aeneid\"")
                && automaticAggro >= 0
                && notifyCombat > automaticAggro
                && processDue > notifyCombat
                && notifyDeath > processDue
                && encounter.IndexOf("AbmouthProfileKey", automaticAggro, StringComparison.Ordinal) < notifyCombat
                && encounter.IndexOf("AbmouthProfileKey", notifyCombat, StringComparison.Ordinal) < processDue
                && encounter.IndexOf("AbmouthProfileKey", notifyDeath, StringComparison.Ordinal) > notifyDeath
                && encounter.Contains("if (!this.combatActive || this.abmouthDead || this.abmouthIdentity.Instance == 0)"),
                "Only the Abmouth profile may activate proactive aggro, Infector timers, or summon cleanup.");
            Assert.IsTrue(
                encounter.Contains("VergilAeneidProfileKey,\n                \"subway.127.boss.vergil-aeneid.spawn\",")
                && encounter.Contains("VergilAeneidMonsterData,\n                true,\n                false,")
                && !encounter.Contains("this.abmouthIdentity = vergil.Identity")
                && !encounter.Contains("this.combatActive = true;\n                    this.vergilAeneidIdentity"),
                "Vergil must remain a separate boss profile and never become an Abmouth-owned summon source.");
        }

        [TestMethod]
        public void VergilPreservesExactPf127SpawnAppearanceAndObservedLevelHealthVariants()
        {
            string encounter = ReadPlayfieldSource(
                FindRepositoryRoot(),
                "CapturedSubwayEncounterRuntimeService.cs");

            Assert.IsTrue(
                encounter.Contains("internal const int SubwayPlayfieldId = 127;")
                && encounter.Contains("new CapturedEncounterLevelHealthVariant(\n                29,\n                6796,\n                131,\n                131,")
                && encounter.Contains("new CapturedEncounterLevelHealthVariant(\n                30,\n                7227,\n                132,\n                135,")
                && encounter.Contains("new CapturedEncounterLevelHealthVariant(\n                31,\n                7659,\n                132,\n                140,")
                && encounter.Contains("variant = VergilAeneidVariants[this.spawnRandom.Next(VergilAeneidVariants.Length)]")
                && encounter.Contains("variant.Level,\n                variant.Health,\n                variant.MonsterScale,\n                variant.RunSpeed,"),
                "Vergil must select only the three captured level, health, scale, and RunSpeed variants in PF127.");
            Assert.IsTrue(
                encounter.Contains("278.045074f,\n                73.01795f,\n                98.80104f,")
                && encounter.Contains("-0.7096085f")
                && encounter.Contains("0.704596162f")
                && encounter.Contains("1643u,\n                unchecked((int)0x020B4ACB)")
                && encounter.Contains("HexToBytes(\"00000000000000000000000002010001000100010001000000020000\")")
                && encounter.Contains("npcFamily: 138")
                && encounter.Contains("breed: 3")
                && encounter.Contains("sex: 2")
                && encounter.Contains("race: 1")
                && encounter.Contains("headMesh: 40171")
                && encounter.Contains("new CapturedSubwayTextureDefinition(0, 117653, 0)")
                && encounter.Contains("new CapturedSubwayTextureDefinition(4, 9622, 0)")
                && encounter.Contains("new CapturedSubwayMeshDefinition(0, 40171u, 0, 4)")
                && encounter.Contains("new CapturedSubwayMeshDefinition(1, 21126u, 0, 2)"),
                "Vergil's captured spawn coordinates, heading, SCFU appearance, textures, and meshes must remain exact.");
        }

        [TestMethod]
        public void EumenidesPreservesAtomicScfuAndDedicatedNamedEnemyLifecyclePolicy()
        {
            string root = FindRepositoryRoot();
            string encounter = ReadPlayfieldSource(root, "CapturedSubwayEncounterRuntimeService.cs");
            string ordinary = ReadPlayfieldSource(root, "CapturedSubwayOrdinaryContentProvider.cs");

            Assert.IsTrue(
                encounter.Contains("EumenidesMonsterData = 203726")
                && encounter.Contains("EumenidesProfileKey = \"subway.127.named.eumenides\"")
                && encounter.Contains("EumenidesEncounterKey = \"subway.127.encounter.eumenides\"")
                && encounter.Contains("CapturedEumenidesAggroRadius = 15.609f")
                && encounter.Contains("EumenidesPrivateRespawnDelay = TimeSpan.FromMinutes(10)")
                && encounter.Contains("this.eumenidesRespawnDueAtUtc = diedAtUtc.Add(EumenidesPrivateRespawnDelay)")
                && encounter.Contains("this.ProcessEumenidesRespawn(utcNow);")
                && encounter.Contains("maximumNpcLeashDistanceFromHome: 100.0"),
                "Eumenides must use its own named profile, bounded observed acquisition radius, and explicit private lifecycle policy.");
            Assert.IsTrue(
                encounter.Contains("EumenidesProfileKey,\n                \"subway.127.named.eumenides.spawn\",\n                EumenidesEncounterKey,\n                \"Eumenides\",\n                EumenidesMonsterData,\n                false,\n                false,")
                && encounter.Contains("20,\n                2792,\n                130,\n                76,\n                76,")
                && encounter.Contains("241.105133f,\n                73.0453949f,\n                44.0469055f,")
                && encounter.Contains("0.250876963f")
                && encounter.Contains("-0.96801883f")
                && encounter.Contains("1643u,\n                unchecked((int)0x020A4ACB)")
                && encounter.Contains("HexToBytes(\"80000000000000000000000002010001000100010001000000020000\")")
                && encounter.Contains("17905,\n                1800.0,\n                3.0,")
                && encounter.Contains("npcFamily: 148")
                && encounter.Contains("breed: 3")
                && encounter.Contains("sex: 2")
                && encounter.Contains("headMesh: 29708")
                && encounter.Contains("new CapturedSubwayTextureDefinition(0, 9620, 0)")
                && encounter.Contains("new CapturedSubwayTextureDefinition(4, 9625, 0)")
                && encounter.Contains("new CapturedSubwayMeshDefinition(0, 29708u, 0, 4)")
                && encounter.Contains("new CapturedSubwayMeshDefinition(1, 35564u, 0, 2)"),
                "Eumenides must preserve one atomic 20260716-034559 SCFU plus the captured corpse CATMesh and private corpse timings.");
            Assert.IsFalse(
                ordinary.Contains("Eumenides") || ordinary.Contains("203726"),
                "Eumenides must remain outside ordinary population generation.");
            Assert.IsTrue(
                encounter.Contains("active nano refresh unresolved and omitted"),
                "The two observed active nanos must remain explicitly omitted until refresh semantics are known.");
        }

        [TestMethod]
        public void EumenidesUsesCapturedWeaponContextButLeavesDamageAndRechargeItemOwned()
        {
            string root = FindRepositoryRoot();
            string rules = ReadPlayfieldSource(root, "NpcCombatAttackRules.cs");
            string contracts = ReadPlayfieldSource(root, "CapturedEnemyCombatContract.cs");
            string generated = File.ReadAllText(
                    Path.Combine(root, @"docs\generated\subway_enemy_combat_contracts.json"))
                .Replace("\r\n", "\n");
            int eumenidesStart = generated.IndexOf("\"Eumenides\": {", StringComparison.Ordinal);
            int nextContract = generated.IndexOf("\n  \"", eumenidesStart + 16, StringComparison.Ordinal);
            string eumenides = nextContract < 0
                ? generated.Substring(eumenidesStart)
                : generated.Substring(eumenidesStart, nextContract - eumenidesStart);

            Assert.IsTrue(eumenidesStart >= 0, "The generated Eumenides evidence contract must exist.");
            Assert.IsTrue(
                rules.Contains("CapturedSubwayEumenidesWeaponLowTemplate = 123267")
                && rules.Contains("CapturedSubwayEumenidesWeaponHighTemplate = 123268")
                && rules.Contains("CapturedSubwayEumenidesWeaponQuality = 20")
                && rules.Contains("CapturedSubwayEumenidesWeaponDamageMinimumOverride = 0")
                && rules.Contains("CapturedSubwayEumenidesWeaponDamageMaximumOverride = 0")
                && rules.Contains("CapturedSubwayEumenidesRechargeOverrideSeconds = 0.0")
                && rules.Contains("CapturedSubwayEumenidesAttackStartDelaySeconds = 0.001000")
                && rules.Contains("CapturedSubwayEumenidesMovementTransitionDelaySeconds = 0.233124")
                && rules.Contains("CapturedSubwayEumenidesFirstHitDelaySeconds = 5.199992")
                && rules.Contains("CapturedSubwayEumenidesSpecialAttackWeaponUnknown1 = 143")
                && rules.Contains("CapturedSubwayEumenidesSpecialAttackWeaponUnknown2 = 171"),
                "Eumenides must retain the captured QL20 weapon, SIW shape, and opening timing without hard-coded runtime rolls.");
            Assert.IsTrue(
                contracts.Contains("case 203726:")
                && contracts.Contains("NpcCombatAttackRules.CapturedSubwayEumenidesWeaponLowTemplate")
                && contracts.Contains("NpcCombatAttackRules.CapturedSubwayEumenidesWeaponHighTemplate")
                && contracts.Contains("NpcCombatAttackRules.CapturedSubwayEumenidesRechargeOverrideSeconds")
                && contracts.Contains("requiresDamageLineOfSight: true")
                && contracts.Contains("two observed normal player hits 39/45")
                && contracts.Contains("one observed 9.749082-second interval"),
                "The runtime contract must equip the weapon, preserve observed evidence, and require PF127 damage LOS.");
            Assert.IsTrue(
                eumenides.Contains("\"normalAttackInfoRows\": 2")
                && eumenides.Contains("\"normalMinDamage\": 39")
                && eumenides.Contains("\"normalMaxDamage\": 45")
                && eumenides.Contains("\"medianRechargeSeconds\": 9.749082")
                && eumenides.Contains("\"equippedWeaponTemplateId\": 123267")
                && eumenides.Contains("\"equippedWeaponQuality\": 20"),
                "Observed Eumenides hits and cadence must remain evidence only and separate from the zero runtime overrides.");
        }

        [TestMethod]
        public void EumenidesCorpseEvidenceReplaysExactCapturedShapeWithoutInventingItemLoot()
        {
            string root = FindRepositoryRoot();
            string encounter = ReadPlayfieldSource(root, "CapturedSubwayEncounterRuntimeService.cs");
            string corpse = ReadPacketSource(root, "CorpseFullUpdate.cs");
            string loot = ReadPlayfieldSource(root, "GlobalLootRuntimeService.cs");
            string captured = File.ReadAllText(
                Path.Combine(
                    root,
                    @"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260716-222007\corpse-full-updates.csv"));

            Assert.IsTrue(
                captured.Contains("Remains of Eumenides")
                && captured.Contains(",130,2,3,1,")
                && captured.Contains(",17905,186,203726,")
                && captured.Contains(",416,\""),
                "The preserved official-live corpse row must retain packet length, scale, sex, breed, race, CATMesh, credits, and MonsterData.");
            Assert.IsTrue(
                encounter.Contains("17905,\n                1800.0,\n                3.0,")
                && corpse.Contains("CapturedSubwayEumenidesPacketLength = 416")
                && corpse.Contains("CapturedSubwayEumenidesMonsterDataOffset = 332")
                && corpse.Contains("CapturedSubwayEumenidesTailDeadNpcInstanceOffset = 344")
                && corpse.Contains("CapturedSubwayEumenidesTemplate")
                && corpse.Contains("BuildCapturedSubwayEumenides(")
                && corpse.Contains("WriteInt32(buffer, MonsterScaleOffset, deadNpc.Stats[StatIds.monsterscale].Value);")
                && corpse.Contains("WriteInt32(buffer, CorpseCatMeshOffset, corpseCatMesh);")
                && corpse.Contains("WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));")
                && corpse.Contains("WriteInt32(buffer, CapturedSubwayEumenidesMonsterDataOffset, corpseMonsterData);")
                && corpse.Contains("CapturedSubwayEumenidesTailDeadNpcInstanceOffset"),
                "Eumenides must replay the captured 416-byte corpse visual while patching only runtime state fields.");
            Assert.IsTrue(
                encounter.Contains("private named-enemy policy: 10-minute respawn, 30-minute loot-bearing corpse")
                && encounter.Contains("active nano refresh unresolved and omitted"),
                "Private timing substitutions and unresolved capture fields must remain explicit rather than presented as official-live facts.");
            Assert.IsTrue(
                loot.Contains("CapturedEumenidesCredits = 186")
                && loot.Contains("CapturedSubwayEncounterRuntimeService.EumenidesProfileKey")
                && loot.Contains("isEumenides ? CapturedEumenidesCredits : CapturedInfectorCredits")
                && loot.Contains("20260716-222007 fixed 186 corpse credits; item pool unresolved"),
                "Eumenides must award the fixed captured 186 credits while leaving its item pool unresolved.");
        }

        [TestMethod]
        public void VergilUsesCapturedWeaponTimingCorpseAndThreeAtomicLootSnapshots()
        {
            string root = FindRepositoryRoot();
            string encounter = ReadPlayfieldSource(root, "CapturedSubwayEncounterRuntimeService.cs");
            string rules = ReadPlayfieldSource(root, "NpcCombatAttackRules.cs");
            string contracts = ReadPlayfieldSource(root, "CapturedEnemyCombatContract.cs");
            string corpse = ReadPacketSource(root, "CorpseFullUpdate.cs");
            string globalLoot = ReadPlayfieldSource(root, "GlobalLootRuntimeService.cs");

            Assert.IsTrue(
                rules.Contains("CapturedSubwayVergilWeaponTemplate = 122123")
                && rules.Contains("CapturedSubwayVergilWeaponQuality = 23")
                && rules.Contains("CapturedSubwayVergilWeaponDamageMinimumOverride = 0")
                && rules.Contains("CapturedSubwayVergilWeaponDamageMaximumOverride = 0")
                && rules.Contains("CapturedSubwayVergilRechargeOverrideSeconds = 0.0")
                && rules.Contains("CapturedSubwayVergilAttackStartDelaySeconds = 0.646433")
                && rules.Contains("CapturedSubwayVergilMovementTransitionDelaySeconds = 0.001000")
                && rules.Contains("CapturedSubwayVergilFirstHitDelaySeconds = 2.787410")
                && contracts.Contains("case 203748:")
                && contracts.Contains("EquippedWeaponWithEmptySpecialAttackContext(")
                && contracts.Contains("NpcCombatAttackRules.CapturedSubwayVergilWeaponDamageMinimumOverride")
                && contracts.Contains("NpcCombatAttackRules.CapturedSubwayVergilWeaponDamageMaximumOverride")
                && contracts.Contains("NpcCombatAttackRules.CapturedSubwayVergilRechargeOverrideSeconds"),
                "Vergil must equip captured weapon 122123 QL23 while damage and recharge remain weapon-owned.");
            Assert.IsTrue(
                encounter.Contains("5921,\n                1800.0,\n                3.0,")
                && corpse.Contains("CapturedSubwayVergilPacketLength = 420")
                && corpse.Contains("CapturedSubwayVergilTemplate")
                && corpse.Contains("BuildCapturedSubwayVergil(")
                && corpse.Contains("corpseMonsterData == NpcCombatAttackRules.CapturedSubwayVergilMonsterData")
                && corpse.Contains("WriteInt32(buffer, MonsterScaleOffset, deadNpc.Stats[StatIds.monsterscale].Value);"),
                "Vergil must retain the exact 420-byte corpse template and CATMesh 5921.");
            Assert.IsTrue(
                globalLoot.Contains("ObservedCorpseSnapshots = new[]")
                && globalLoot.Contains("\"capture.20260712-232711\",\n                        610,")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260712-232711\", 301713, 301713, 1, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260712-232711\", 202743, 202744, 32, 1)")
                && globalLoot.Contains("\"capture.20260712-234401\",\n                        587,")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260712-234401\", 301714, 301714, 1, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260712-234401\", 123571, 123572, 23, 1)")
                && globalLoot.Contains("\"capture.20260716-034433\",\n                        563,")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260716-034433\", 202734, 202735, 33, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260716-034433\", 301715, 301715, 1, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260716-034433\", 160051, 160050, 24, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260716-034433\", 21605, 21605, 1, 100)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260716-034433\", 287146, 287146, 200, 1)")
                && globalLoot.Contains("Mode = CreditsPolicyMode.Unresolved")
                && globalLoot.Contains("ItemPoolUnresolved = true"),
                "Vergil loot must replay only the three exact item-plus-credit corpse snapshots, including QL1 bullets quantity 100.");
        }

        [TestMethod]
        public void VergilFollowupCombatEvidenceSeparatesLocalPlayerAndKillerPet()
        {
            string root = FindRepositoryRoot();
            string analyzer = File.ReadAllText(
                    Path.Combine(root, @"tools-temp\AOSharpCaptureAnalyzer\analyze_subway_enemy_combat_contracts.py"))
                .Replace("\r\n", "\n");
            string generated = File.ReadAllText(
                    Path.Combine(root, @"docs\generated\subway_enemy_combat_contracts.json"))
                .Replace("\r\n", "\n");
            int vergilStart = generated.IndexOf("\"Vergil Aeneid\": {", StringComparison.Ordinal);
            Assert.IsTrue(vergilStart >= 0, "The generated Vergil combat contract must exist.");
            int nextContract = generated.IndexOf("\n  \"", vergilStart + 20, StringComparison.Ordinal);
            string vergil = nextContract < 0
                ? generated.Substring(vergilStart)
                : generated.Substring(vergilStart, nextContract - vergilStart);
            int petStart = vergil.IndexOf("\"playerOwnedPet\": {", StringComparison.Ordinal);

            Assert.IsTrue(
                analyzer.Contains("\"20260716-034433\": frozenset({\"Vergil Aeneid\"})")
                && analyzer.Contains("\"20260716-034433\": frozenset({\"(SimpleChar:796D400B)\"})")
                && analyzer.Contains("CADENCE_UNRESOLVED_ENEMIES = frozenset({\"Vergil Aeneid\"})"),
                "Capture 034433 must stay Vergil-only, explicitly classify Killer as a player-owned pet, and leave cadence unresolved.");
            Assert.IsTrue(
                vergil.Contains("\"20260716-034433\"")
                && vergil.Contains("\"retaliationRows\": 4")
                && vergil.Contains("\"attackInfoRows\": 5")
                && vergil.Contains("\"minDamage\": 22")
                && vergil.Contains("\"maxDamage\": 23")
                && vergil.Contains("\"intervalRows\": 0")
                && vergil.Contains("\"cadenceStatus\": \"unresolved-mixed-target-fight\"")
                && vergil.Contains("\"equippedWeaponTemplateId\": 122123")
                && vergil.Contains("\"equippedWeaponQuality\": 23"),
                "Top-level Vergil combat evidence must remain local-player-facing and retain the existing weapon proof.");
            Assert.IsTrue(
                petStart >= 0
                && vergil.Substring(petStart).Contains("\"(SimpleChar:796D400B)\"")
                && vergil.Substring(petStart).Contains("\"retaliationRows\": 3")
                && vergil.Substring(petStart).Contains("\"attackInfoRows\": 3")
                && vergil.Substring(petStart).Contains("\"minDamage\": 23")
                && vergil.Substring(petStart).Contains("\"maxDamage\": 28"),
                "Killer's three captured hits must remain in the separate player-owned-pet sidecar.");
        }

        [TestMethod]
        public void AbmouthFollowupCombatEvidenceSeparatesLocalPlayerAndOwnedPets()
        {
            string root = FindRepositoryRoot();
            string analyzer = File.ReadAllText(
                    Path.Combine(root, @"tools-temp\AOSharpCaptureAnalyzer\analyze_subway_enemy_combat_contracts.py"))
                .Replace("\r\n", "\n");
            string generated = File.ReadAllText(
                    Path.Combine(root, @"docs\generated\subway_enemy_combat_contracts.json"))
                .Replace("\r\n", "\n");
            int abmouthStart = generated.IndexOf("\"Abmouth Supremus\": {", StringComparison.Ordinal);
            Assert.IsTrue(abmouthStart >= 0, "The generated Abmouth combat contract must exist.");
            int nextContract = generated.IndexOf("\n  \"", abmouthStart + 24, StringComparison.Ordinal);
            string abmouth = nextContract < 0
                ? generated.Substring(abmouthStart)
                : generated.Substring(abmouthStart, nextContract - abmouthStart);
            int petStart = abmouth.IndexOf("\"playerOwnedPet\": {", StringComparison.Ordinal);

            Assert.IsTrue(
                analyzer.Contains("\"20260716-220400\": frozenset({\"Abmouth Supremus\"})")
                && analyzer.Contains("{\"Abmouth Supremus\", \"Melded Patterns\", \"Vergil Aeneid\"}")
                && analyzer.Contains("\"(SimpleChar:7970253A)\", \"(SimpleChar:7970253C)\""),
                "Capture 220400 must stay Abmouth-only and classify Healer plus Wrath Incarnation as player-owned pets.");
            Assert.IsTrue(
                abmouth.Contains("\"20260716-220400\"")
                && abmouth.Contains("\"attackInfoRows\": 4")
                && abmouth.Contains("\"minDamage\": 74")
                && abmouth.Contains("\"maxDamage\": 125")
                && abmouth.Contains("\"weaponInstance\": 1145392727")
                && abmouth.Contains("\"weaponInstance\": 1481592922"),
                "Top-level Abmouth evidence must retain only the four local-player hits and both independent attack shapes.");
            Assert.IsTrue(
                petStart >= 0
                && abmouth.Substring(petStart).Contains("\"(SimpleChar:7970253A)\"")
                && abmouth.Substring(petStart).Contains("\"(SimpleChar:7970253C)\"")
                && abmouth.Substring(petStart).Contains("\"attackInfoRows\": 10")
                && abmouth.Substring(petStart).Contains("\"minDamage\": 77")
                && abmouth.Substring(petStart).Contains("\"maxDamage\": 138"),
                "Abmouth's ten pet-facing hits must remain separate from player-facing runtime damage.");
        }

        [TestMethod]
        public void VergilHealingUsesCapturedNanoValuesAndPausesWeaponCombatTicks()
        {
            string root = FindRepositoryRoot();
            string encounter = ReadPlayfieldSource(root, "CapturedSubwayEncounterRuntimeService.cs");
            string npcRuntime = ReadPlayfieldSource(root, "NPCRuntimeService.cs");

            Assert.IsTrue(
                encounter.Contains("VergilDirectHealNanoId = 43827")
                && encounter.Contains("VergilDirectHealAmount = 187")
                && encounter.Contains("VergilDirectHealCastSeconds = 1.480007")
                && encounter.Contains("VergilDirectHealCooldownSeconds = 30.654")
                && encounter.Contains("if (level == 31)")
                && encounter.Contains("VergilDirectHealNanoId,\n                    VergilDirectHealAmount,\n                    VergilDirectHealCastSeconds,")
                && encounter.Contains("utcNow.AddSeconds(VergilDirectHealCooldownSeconds)"),
                "Level-31 Vergil must retain captured nano 43827, 187 healing, 1.480007-second cast, and 30.654-second cooldown.");
            Assert.IsTrue(
                encounter.Contains("VergilSelfHealNanoId = 43880")
                && encounter.Contains("VergilSelfHealAmount = 34")
                && encounter.Contains("VergilSelfHealDurationMilliseconds = 14000")
                && encounter.Contains("VergilSelfHealCastSeconds = 1.763334")
                && encounter.Contains("if (level != 30)\n            {\n                return;\n            }")
                && encounter.Contains("VergilSelfHealNanoId,\n                VergilSelfHealAmount,\n                VergilSelfHealCastSeconds,\n                VergilSelfHealDurationMilliseconds,")
                && encounter.Contains("this.vergilNextHealAtUtc = DateTime.MaxValue;"),
                "Level-30 Vergil must retain captured nano 43880 without repetition, while level 29 fails closed instead of inheriting an unobserved heal.");
            Assert.IsTrue(
                encounter.Contains("internal bool IsCapturedNanoCastInProgress(ICharacter character)")
                && encounter.Contains("this.vergilPendingHeal != null")
                && encounter.Contains("CastNanoSpellMessageHandler.Default.Send(vergil, nanoId, target.Identity);")
                && encounter.Contains("CharacterActionMessageHandler.Default.FinishNanoCasting(")
                && encounter.Contains("pending.DurationMilliseconds")
                && encounter.Contains("Unknown2 = appliedHeal")
                && npcRuntime.Contains("if (this.capturedSubwayEncounters.IsCapturedNanoCastInProgress(attacker))\n            {\n                return;\n            }")
                && npcRuntime.Contains("this.combatTick.ProcessCombatTick(attacker);"),
                "Vergil weapon ticks must pause during captured nano casting and resume through the normal combat coordinator afterward.");
        }

        [TestMethod]
        public void VergilUsesConfirmedRespawnAndCaptureProjectionsRemainAlwaysOn()
        {
            string root = FindRepositoryRoot();
            string encounter = ReadPlayfieldSource(root, "CapturedSubwayEncounterRuntimeService.cs");
            string captureTool = File.ReadAllText(
                    Path.Combine(root, @"tools-temp\AOSharpLiveCapture\Main.cs"))
                .Replace("\r\n", "\n");
            int annotationStage = captureTool.IndexOf("\"enemy-fight-annotation\"", StringComparison.Ordinal);
            int evidenceStage = captureTool.IndexOf("\"enemy-evidence-export\"", annotationStage, StringComparison.Ordinal);
            int stateStage = captureTool.IndexOf("\"enemy-state-track\"", evidenceStage, StringComparison.Ordinal);
            int combatProjection = captureTool.IndexOf(
                "this.ExportEnemyN3Evidence(direction, sequence, message)",
                evidenceStage,
                StringComparison.Ordinal);

            Assert.IsTrue(
                encounter.Contains("this.vergilAeneidIdentity = Identity.None;")
                && encounter.Contains("this.vergilRespawnDueAtUtc = diedAtUtc.Add(CapturedNamedBossRespawnDelay);")
                && encounter.Contains("this.vergilRespawnDueAtUtc.Value <= utcNow")
                && CountOccurrences(encounter, "CreateVergilAeneidDefinition()") == 3,
                "Vergil must become absent after despawn and return on the confirmed ten-minute death-based schedule.");
            Assert.IsTrue(
                annotationStage >= 0
                && evidenceStage > annotationStage
                && stateStage > evidenceStage
                && combatProjection > evidenceStage
                && combatProjection < stateStage
                && captureTool.Contains("if (IsEnemyCombatEvidenceMessage(message))")
                && captureTool.Contains("\"AttackInfo\"")
                && captureTool.Contains("\"SpecialAttackWeapon\"")
                && captureTool.Contains("\"CastNanoSpell\"")
                && captureTool.Contains("\"CharacterAction\"")
                && captureTool.Contains("\"HealthDamage\""),
                "Focused annotations, always-on combat projection, and state tracking must remain independently guarded.");
            Assert.IsTrue(
                captureTool.Contains("message.N3MessageType.ToString(),\n                        \"InventoryUpdate\"")
                && captureTool.Contains("this.ExportInventoryUpdate(direction, sequence, message);")
                && captureTool.Contains("private void ExportInventoryUpdate(string direction, int sequence, N3Message message)")
                && captureTool.Contains("object itemsValue = GetMemberValue(message, \"Items\");")
                && captureTool.Contains("foreach (object item in enumerableItems)")
                && !captureTool.Contains("InventoryUpdateMessage inventoryUpdate = message as InventoryUpdateMessage"),
                "Inventory projection must enumerate every slot without a concrete runtime cast or capture marker gate.");
        }

        [TestMethod]
        public void LiveAndOfflineCaptureToolsShareTheRawScfuContract()
        {
            string root = FindRepositoryRoot();
            string liveProject = File.ReadAllText(
                Path.Combine(root, @"tools-temp\AOSharpLiveCapture\AOSharpLiveCapture.csproj"));
            string analyzerProject = File.ReadAllText(
                Path.Combine(root, @"tools-temp\AOSharpCaptureAnalyzer\AOSharpCaptureAnalyzer.csproj"));
            string protocol = File.ReadAllText(
                    Path.Combine(root, @"tools-temp\AOSharpCaptureProtocol\RawSimpleCharFullUpdateDecoder.cs"))
                .Replace("\r\n", "\n");
            string captureTool = File.ReadAllText(
                    Path.Combine(root, @"tools-temp\AOSharpLiveCapture\Main.cs"))
                .Replace("\r\n", "\n");
            string analyzer = File.ReadAllText(
                    Path.Combine(root, @"tools-temp\AOSharpCaptureAnalyzer\Program.cs"))
                .Replace("\r\n", "\n");

            Assert.IsTrue(
                liveProject.Contains(@"..\AOSharpCaptureProtocol\RawSimpleCharFullUpdateDecoder.cs")
                && analyzerProject.Contains(@"..\AOSharpCaptureProtocol\RawSimpleCharFullUpdateDecoder.cs")
                && liveProject.Contains("<Link>RawSimpleCharFullUpdateDecoder.cs</Link>")
                && analyzerProject.Contains("<Link>RawSimpleCharFullUpdateDecoder.cs</Link>"),
                "The live plugin and offline analyzer must compile the same tracked raw SCFU decoder source.");
            Assert.IsTrue(
                protocol.Contains("internal const int N3BodyOffset = 16;")
                && protocol.Contains("internal const int SimpleCharFullUpdateType = 0x271B3A6B;")
                && protocol.Contains("internal static class RawScfuAppearanceCsv")
                && protocol.Contains("NpcUnknownDataWidth")
                && protocol.Contains("NpcUnknownData3")
                && protocol.Contains("TextureOverrides")
                && protocol.Contains("SpecialAttacks")
                && protocol.Contains("UnknownFlag3Data")
                && protocol.Contains("DecodeFullyConsumed")
                && protocol.Contains("UndecodedTailHex")
                && protocol.Contains("RawPacketHex")
                && protocol.Contains("RawBodyHex"),
                "The shared protocol must retain the full NPC definition, undecoded tail, and raw packet/body evidence.");
            Assert.IsTrue(
                captureTool.Contains("RawSimpleCharFullUpdateDecoder.TryDecodePacket(")
                && captureTool.Contains("RawScfuAppearanceCsv.Header")
                && captureTool.Contains("RawScfuAppearanceCsv.FormatRow(")
                && analyzer.Contains("RawSimpleCharFullUpdateDecoder.TryDecodePacket(")
                && analyzer.Contains("RawScfuAppearanceCsv.Header")
                && analyzer.Contains("RawScfuAppearanceCsv.FormatRow(")
                && !analyzer.Contains("SerializerResolver")
                && analyzer.Contains("ReadInt32BigEndian(packet, RawSimpleCharFullUpdateDecoder.N3BodyOffset)")
                && analyzer.Contains("== RawSimpleCharFullUpdateDecoder.SimpleCharFullUpdateType")
                && analyzer.Contains("RawSimpleCharFullUpdateDecoder.TryDecodePacket(truncated")
                && analyzer.Contains("!extraTail.DecodeFullyConsumed"),
                "Both consumers must use the shared raw contract; the analyzer must detect numeric type 0x271B3A6B at offset 16 without the messaging resolver.");
        }

        [TestMethod]
        public void CaptureToolPreservesEveryRawPacketAcrossIndependentNoThrowSinks()
        {
            string captureTool = File.ReadAllText(
                    Path.Combine(FindRepositoryRoot(), @"tools-temp\AOSharpLiveCapture\Main.cs"))
                .Replace("\r\n", "\n");
            int packetLogWrite = captureTool.IndexOf("this.packetsLog.WriteLine(", StringComparison.Ordinal);
            int packetLogCount = captureTool.IndexOf(
                "this.rawPacketLogRowCount++;",
                packetLogWrite,
                StringComparison.Ordinal);
            int packetIndexWrite = captureTool.IndexOf("this.rawPacketsCsvLog.WriteLine(", StringComparison.Ordinal);
            int packetIndexCount = captureTool.IndexOf(
                "this.rawPacketIndexRowCount++;",
                packetIndexWrite,
                StringComparison.Ordinal);

            Assert.IsTrue(
                captureTool.Contains("CreateWriter(Path.Combine(this.sessionDirectory, \"packets.hex.log\"), true)")
                && captureTool.Contains("CreateWriter(Path.Combine(this.sessionDirectory, \"raw-packets.csv\"), true)")
                && captureTool.Contains("AutoFlush = autoFlush")
                && packetLogWrite >= 0
                && packetLogCount > packetLogWrite
                && packetIndexWrite >= 0
                && packetIndexCount > packetIndexWrite,
                "Both authoritative raw sinks must auto-flush and increment their parity counters only after a successful write.");
            Assert.IsTrue(
                captureTool.Contains("private void CaptureNetworkPacketNoThrow(")
                && captureTool.Contains("private void OnPacketReceivedBoundary(")
                && captureTool.Contains("private void OnPacketSentBoundary(")
                && captureTool.Contains("\"Network.PacketReceived\"")
                && captureTool.Contains("\"Network.PacketSent\"")
                && captureTool.Contains("capture-callback-errors.log")
                && captureTool.Contains("CaptureCallbackBoundarySnapshot callbackHealth")
                && captureTool.Contains("private void RunRawPacketProjectionStage(")
                && captureTool.Contains("RAW-PACKET-PROJECTION-ERROR")
                && captureTool.Contains("if (packetLogWritten || packetIndexWritten)")
                && captureTool.Contains("this.rawPacketPreservedCount++"),
                "Packet callbacks, each projection, and each raw sink must be isolated so a secondary failure cannot discard preserved bytes.");
            Assert.IsTrue(
                captureTool.Contains("return this.rawPacketLogRowCount != observedRawPackets")
                && captureTool.Contains("&& this.rawPacketIndexRowCount != observedRawPackets")
                && captureTool.Contains("&& this.rawPacketPreservedCount != observedRawPackets")
                && captureTool.Contains("Neither authoritative raw sink is complete")
                && captureTool.Contains("bool offlineDecodeRequired = !recaptureRequired && this.HasOfflineDecodeWork(observedRawPackets);"),
                "Either complete authoritative raw sink must prevent a recapture while projection or secondary-sink repair remains offline work.");
        }

        [TestMethod]
        public void CaptureToolAccountsEveryScfuStateAndFinalizesWithoutDroppingTheRawTail()
        {
            string captureTool = File.ReadAllText(
                    Path.Combine(FindRepositoryRoot(), @"tools-temp\AOSharpLiveCapture\Main.cs"))
                .Replace("\r\n", "\n");
            int completion = captureTool.IndexOf(
                "private void CompleteCaptureStop(",
                StringComparison.Ordinal);
            int quietGateTentative = captureTool.IndexOf(
                "RawCaptureGateTentative",
                completion,
                StringComparison.Ordinal);
            int quietGateReopened = captureTool.IndexOf(
                "RawCaptureGateOpen",
                quietGateTentative,
                StringComparison.Ordinal);
            int quietGateClosed = captureTool.IndexOf(
                "RawCaptureGateClosed",
                quietGateReopened,
                StringComparison.Ordinal);
            int finalInFlightCheck = captureTool.IndexOf(
                "Volatile.Read(ref this.rawPacketCallbacksInFlight) != 0",
                quietGateClosed,
                StringComparison.Ordinal);
            int finalized = captureTool.IndexOf(
                "this.captureFinalized = true;",
                finalInFlightCheck,
                StringComparison.Ordinal);
            int closeRaw = captureTool.IndexOf(
                "this.FlushAndCloseRawWritersNoThrow();",
                finalized,
                StringComparison.Ordinal);
            int validate = captureTool.IndexOf(
                "validation = this.ValidateCapture();",
                closeRaw,
                StringComparison.Ordinal);
            int rawCallback = captureTool.IndexOf(
                "private void CaptureNetworkPacketNoThrow(",
                StringComparison.Ordinal);
            int callbackClosedGate = captureTool.IndexOf(
                "gateState == RawCaptureGateClosed",
                rawCallback,
                StringComparison.Ordinal);
            int callbackTentativeGate = captureTool.IndexOf(
                "gateState == RawCaptureGateTentative",
                callbackClosedGate,
                StringComparison.Ordinal);
            int callbackRegistered = captureTool.IndexOf(
                "Interlocked.Increment(ref this.rawPacketCallbacksInFlight);",
                callbackTentativeGate,
                StringComparison.Ordinal);
            int callbackGateVerified = captureTool.IndexOf(
                "Volatile.Read(ref this.rawCaptureGateState) == RawCaptureGateOpen",
                callbackRegistered,
                StringComparison.Ordinal);
            int run = captureTool.IndexOf("public override void Run(string pluginDir)", StringComparison.Ordinal);
            int initialize = captureTool.IndexOf(
                "private void Initialize(string pluginDir)",
                run,
                StringComparison.Ordinal);
            int openInactive = captureTool.IndexOf(
                "this.OpenFreshCaptureSession(pluginDir, true, false);",
                initialize,
                StringComparison.Ordinal);
            int subscribeInbound = captureTool.IndexOf(
                "Network.PacketReceived += this.OnPacketReceivedBoundary;",
                openInactive,
                StringComparison.Ordinal);
            int subscribeOutbound = captureTool.IndexOf(
                "Network.PacketSent += this.OnPacketSentBoundary;",
                subscribeInbound,
                StringComparison.Ordinal);
            int activateInitial = captureTool.IndexOf(
                "this.ActivateCaptureSession();",
                subscribeOutbound,
                StringComparison.Ordinal);
            int openFresh = captureTool.IndexOf(
                "private void OpenFreshCaptureSession(string pluginDir, bool resetState, bool activate)",
                StringComparison.Ordinal);
            int openFreshLock = captureTool.IndexOf("lock (this.syncRoot)", openFresh, StringComparison.Ordinal);
            int restartFinalize = captureTool.IndexOf(
                "this.CompleteCaptureStop(DateTime.UtcNow, false, false);",
                openFreshLock,
                StringComparison.Ordinal);
            int restartActivate = captureTool.IndexOf(
                "this.ActivateCaptureSession();",
                restartFinalize,
                StringComparison.Ordinal);
            int teardownFinalize = captureTool.IndexOf(
                "private void FinalizeCapture()",
                StringComparison.Ordinal);
            int teardownBoundary = captureTool.IndexOf(
                "this.CloseRawCaptureBoundaryAndWait(TimeSpan.FromSeconds(5))",
                teardownFinalize,
                StringComparison.Ordinal);
            int teardownComplete = captureTool.IndexOf(
                "this.CompleteCaptureStop(finalizedUtc, false, false);",
                teardownBoundary,
                StringComparison.Ordinal);

            Assert.IsTrue(
                captureTool.Contains("this.rawSimpleCharFullUpdateDecodeCount++")
                && captureTool.Contains("this.rawSimpleCharFullUpdateIncompleteDecodeCount++")
                && captureTool.Contains("this.rawSimpleCharFullUpdateDecodeErrorCount++")
                && captureTool.Contains("this.scfuAppearanceRowCount++")
                && captureTool.Contains("!= this.rawSimpleCharFullUpdateDecodeCount + this.rawSimpleCharFullUpdateDecodeErrorCount")
                && captureTool.Contains("this.rawSimpleCharFullUpdateIncompleteDecodeCount > 0")
                && captureTool.Contains("this.rawSimpleCharFullUpdateDecodeErrorCount > 0")
                && captureTool.Contains("bool offlineDecodeRequired = !recaptureRequired && this.HasOfflineDecodeWork(observedRawPackets);"),
                "Live capture must account for completely decoded, incompletely decoded, and failed SCFUs while preserving one appearance row per raw SCFU.");
            Assert.IsTrue(
                captureTool.Contains("capture stop requested; draining raw packets until quiet")
                && captureTool.Contains("this.captureStopQuietDeadlineUtc = quietDeadline < this.captureStopMaximumDeadlineUtc.Value")
                && captureTool.Contains("this.CompleteCaptureStop(now, quietPeriodPassed, true);")
                && captureTool.Contains("if (this.enabled && !this.captureFinalized)")
                && captureTool.Contains("this.CompleteCaptureStop(DateTime.UtcNow, false, false);")
                && captureTool.Contains("if (maximumDrainReached)")
                && captureTool.Contains("Thread.Yield();")
                && callbackClosedGate > rawCallback
                && callbackTentativeGate > callbackClosedGate
                && callbackRegistered > callbackTentativeGate
                && callbackGateVerified > callbackRegistered
                && quietGateTentative > completion
                && quietGateReopened > quietGateTentative
                && quietGateClosed > quietGateReopened
                && finalInFlightCheck > quietGateClosed
                && finalized > finalInFlightCheck
                && completion >= 0
                && closeRaw > completion
                && validate > closeRaw,
                "Quiet stop must tentatively block new callbacks, reopen when a registered callback extends quiet, close at the maximum deadline, and wait for every accepted callback before raw sinks close and validation starts.");
            Assert.IsTrue(
                initialize > run
                && openInactive > initialize
                && subscribeInbound > openInactive
                && subscribeOutbound > subscribeInbound
                && activateInitial > subscribeOutbound
                && openFreshLock > openFresh
                && restartFinalize > openFreshLock
                && restartActivate > restartFinalize
                && captureTool.Contains("Interlocked.Exchange(\n                ref this.rawCaptureGateState,\n                RawCaptureGateOpen);")
                && teardownBoundary > teardownFinalize
                && teardownComplete > teardownBoundary
                && captureTool.Contains("while (Volatile.Read(ref this.rawPacketCallbacksInFlight) != 0)")
                && captureTool.Contains("Monitor.Wait(this.syncRoot, remaining)")
                && captureTool.Contains("this.rawPacketCallbackDrainTimeoutCount++")
                && captureTool.Contains("|| this.rawPacketCallbackDrainTimeoutCount > 0;"),
                "Initial start must attach raw hooks before activation, active restart must remain serialized, and teardown must close the gate, drain accepted callbacks, and require recapture after a timeout.");
        }

        [TestMethod]
        public void CaptureToolExportsPf127GeometryAndLineOfSightAlwaysOn()
        {
            string root = FindRepositoryRoot();
            string captureTool = File.ReadAllText(
                    Path.Combine(root, @"tools-temp\AOSharpLiveCapture\Main.cs"))
                .Replace("\r\n", "\n");
            string geometryCapture = File.ReadAllText(
                    Path.Combine(root, @"tools-temp\AOSharpLiveCapture\Pf127GeometryCapture.cs"))
                .Replace("\r\n", "\n");
            string minimalCapture = File.ReadAllText(
                    Path.Combine(root, @"tools-temp\AOSharpLiveCapture\MinimalPf127Capture.cs"))
                .Replace("\r\n", "\n");
            string captureProject = File.ReadAllText(
                Path.Combine(root, @"tools-temp\AOSharpLiveCapture\AOSharpLiveCapture.csproj"));
            string activateCapture = ExtractMethodBlock(
                captureTool,
                "private void ActivateCaptureSession()");
            string playfieldInit = ExtractMethodBlock(
                captureTool,
                "private void OnPlayfieldInit(object sender, uint playfieldId)");
            string teleportStarted = ExtractMethodBlock(
                captureTool,
                "private void OnTeleportStarted(object sender, EventArgs e)");
            string teleportEnded = ExtractMethodBlock(
                captureTool,
                "private void OnTeleportEnded(object sender, EventArgs e)");
            string mainUpdate = ExtractMethodBlock(
                captureTool,
                "private void OnUpdate(object sender, float deltaTime)");
            string rawPacketCapture = ExtractMethodBlock(
                captureTool,
                "private void LogPacket(string direction, int sequence, byte[] packet)");
            string pf127Detection = ExtractMethodBlock(
                captureTool,
                "private bool IsDetectedResourcePlayfield127()");
            string resourcePlayfieldDetection = ExtractMethodBlock(
                captureTool,
                "private string GetDetectedResourcePlayfieldId()");
            string minimalUpdate = ExtractMethodBlock(
                minimalCapture,
                "private void UpdateCore(DateTime capturedUtc)");
            string geometryUpdate = ExtractMethodBlock(
                geometryCapture,
                "public void Update(DateTime nowUtc, bool isPf127, string runtimePlayfieldId)");
            string playfieldChange = ExtractMethodBlock(
                geometryCapture,
                "public void NotifyPlayfieldChanged(bool isPf127)");
            string combatRequest = ExtractMethodBlock(
                geometryCapture,
                "public void RequestCombatSample()");
            string geometryWriter = ExtractMethodBlock(
                geometryCapture,
                "private void TryWriteCanonicalGeometry()");
            string geometryAttempt = ExtractMethodBlock(
                geometryCapture,
                "private static GeometryWriteResult WriteCanonicalGeometryAttempt(string path)");
            string staticDoorCapture = ExtractMethodBlock(
                geometryCapture,
                "private static List<DoorSnapshot> CaptureStaticDoorSnapshots()");
            string doorStateBatch = ExtractMethodBlock(
                geometryCapture,
                "private DoorStateBatchResult CaptureDoorStateBatch(");
            string lineOfSightSampler = ExtractMethodBlock(
                geometryCapture,
                "private void SampleLineOfSight(");
            string lineOfSightRow = ExtractMethodBlock(
                geometryCapture,
                "private LineOfSightTargetBatchResult WriteLineOfSightRows(");
            string lineOfSightVariantRow = ExtractMethodBlock(
                geometryCapture,
                "private bool WriteLineOfSightVariantRow(");
            string roomGeometryProjection = ExtractMethodBlock(
                geometryCapture,
                "private static RoomGeometrySourceSnapshot CaptureRoomGeometrySourceSnapshot(Room room)");
            string meshGeometryProjection = ExtractMethodBlock(
                geometryCapture,
                "private static MeshGeometrySourceSnapshot CaptureMeshGeometrySourceSnapshot(");
            string staticDoorProjection = ExtractMethodBlock(
                geometryCapture,
                "private static DoorSnapshot CaptureStaticDoorSnapshot(Door door)");

            Assert.IsTrue(
                captureProject.Contains("<Compile Include=\"Pf127GeometryCapture.cs\" />")
                && captureProject.Contains("<Compile Include=\"MinimalPf127Capture.cs\" />")
                && captureTool.Contains("new Pf127GeometryCapture(this.sessionDirectory, this.LogEvent)")
                && geometryCapture.Contains("Path.Combine(sessionDirectory, \"pf127-geometry.json\")")
                && geometryCapture.Contains("Path.Combine(sessionDirectory, \"pf127-line-of-sight.csv\")")
                && geometryCapture.Contains("Path.Combine(sessionDirectory, \"pf127-door-state.csv\")"),
                "The live capture project must always ship the dedicated PF127 geometry, line-of-sight, and dynamic door-state evidence files.");
            int modelIdentityDetection = resourcePlayfieldDetection.IndexOf(
                "Playfield.ModelIdentity.Instance == 127",
                StringComparison.Ordinal);
            int captureObjectFallback = resourcePlayfieldDetection.IndexOf(
                "\"122002\"",
                StringComparison.Ordinal);
            Assert.IsTrue(
                pf127Detection.Contains("this.GetDetectedResourcePlayfieldId()")
                && pf127Detection.Contains("\"127\"")
                && modelIdentityDetection >= 0
                && captureObjectFallback > modelIdentityDetection
                && !activateCapture.Contains("Playfield.")
                && !activateCapture.Contains("IsDetectedResourcePlayfield127()")
                && !activateCapture.Contains("NotifyPlayfieldChanged(")
                && !activateCapture.Contains("RequestImmediateUpdate()")
                && playfieldInit.Contains("this.lastPlayfieldId = playfieldId.ToString")
                && playfieldInit.Contains("ref this.playfieldInitGeneration")
                && playfieldInit.Contains("NotifyPlayfieldChanged(false)")
                && !playfieldInit.Contains("Playfield.")
                && !playfieldInit.Contains("IsDetectedResourcePlayfield127()")
                && !playfieldInit.Contains("RequestImmediateUpdate()")
                && teleportStarted.Contains("Interlocked.Increment(ref this.teleportGeneration)")
                && teleportStarted.Contains("Interlocked.Exchange(ref this.teleportInProgress, 1)")
                && teleportStarted.Contains("Interlocked.Exchange(ref this.pf127CollectionArmed, 0)")
                && teleportStarted.Contains("NotifyPlayfieldChanged(false)")
                && teleportEnded.Contains("matchingPlayfieldInit")
                && teleportEnded.Contains("string.Equals(this.lastPlayfieldId, \"127\"")
                && teleportEnded.Contains("NotifyPlayfieldChanged(isPf127)")
                && teleportEnded.Contains("RequestImmediateUpdate()")
                && mainUpdate.Contains("geometryCapture.ExecuteUpdateBoundary(")
                && mainUpdate.Contains("Volatile.Read(ref this.pf127CaptureRuntimeReady) != 0")
                && mainUpdate.Contains("Volatile.Read(ref this.teleportInProgress) == 0")
                && mainUpdate.Contains("Volatile.Read(ref this.pf127CollectionArmed) != 0")
                && mainUpdate.Contains("this.IsDetectedResourcePlayfield127()")
                && mainUpdate.Contains("this.GetDetectedPlayfieldId()")
                && minimalCapture.Contains("RequiredStableDuration = TimeSpan.FromSeconds(5)")
                && minimalCapture.Contains("RequiredStableTicks = 20")
                && minimalUpdate.Contains("if (Game.IsZoning)")
                && minimalUpdate.Contains("TryCaptureStableSignal")
                && minimalUpdate.Contains("this.geometryCapture.ExecuteUpdateBoundary("),
                "Comprehensive capture must not touch PF native wrappers during activation or PlayfieldInit; matching TeleportEnded arms collection, while explicit geometry-only mode safely handles attach-inside after a stable gate.");
            Assert.IsTrue(
                playfieldChange.Contains("Interlocked.Exchange(ref this.pf127Observed, 1)")
                && geometryUpdate.Contains("!this.GeometryWritten")
                && geometryUpdate.Contains("nowUtc >= this.nextGeometryAttemptUtc")
                && geometryUpdate.Contains("this.TryWriteCanonicalGeometry()")
                && geometryUpdate.Contains("this.combatRequestGate.TryBegin(")
                && geometryUpdate.Contains("\"combat\"")
                && geometryUpdate.Contains("\"periodic\"")
                && geometryCapture.Contains("GeometryRetryInterval = TimeSpan.FromSeconds(1)")
                && geometryCapture.Contains("PeriodicLineOfSightInterval = TimeSpan.FromSeconds(1)"),
                "PF127 geometry must retry until promoted, while LOS evidence is sampled both periodically and immediately after combat evidence.");
            Assert.IsTrue(
                rawPacketCapture.Contains("IsRawCombatEvidencePacket(packet)")
                && rawPacketCapture.Contains("this.rawCombatPacketCount++")
                && rawPacketCapture.Contains("this.pf127GeometryCapture?.RequestCombatSample()")
                && combatRequest.Contains("this.isPf127Active")
                && combatRequest.Contains("this.pf127CombatObserved")
                && combatRequest.Contains("this.combatTriggerCount")
                && combatRequest.Contains("this.combatRequestGate.Request()"),
                "Every raw PF127 combat packet must request LOS evidence independently of decoded combat classification.");

            int updateArmedGate = mainUpdate.IndexOf(
                "Volatile.Read(ref this.pf127CollectionArmed) != 0",
                StringComparison.Ordinal);
            int updateCollector = mainUpdate.IndexOf(
                "geometryCapture.ExecuteUpdateBoundary(",
                StringComparison.Ordinal);
            Assert.IsTrue(
                updateArmedGate >= 0 && updateCollector > updateArmedGate,
                "The PF127 armed/stability gate must be evaluated before any native geometry collection call.");
            string alwaysOnPath = teleportStarted
                                  + playfieldInit
                                  + teleportEnded
                                  + mainUpdate
                                  + rawPacketCapture
                                  + geometryUpdate
                                  + lineOfSightSampler;
            Assert.IsFalse(
                alwaysOnPath.Contains("enemyFightCapture")
                || alwaysOnPath.Contains("focusedEnemyIdentities")
                || alwaysOnPath.Contains("lootCaptureRequested")
                || alwaysOnPath.Contains("respawnCaptureRequested")
                || alwaysOnPath.Contains("MARK"),
                "PF127 geometry and LOS evidence must never depend on a focus, fight, loot, respawn, or marker mode.");

            Assert.IsTrue(
                geometryCapture.Contains("private static bool IsCanonicalGeometryReady()")
                && geometryWriter.Contains("DevExtras.LoadAllSurfaces()")
                && geometryWriter.Contains("GeometryStageWaitingForReadiness")
                && geometryWriter.Contains("GeometryStageReadinessObserved")
                && geometryWriter.Contains("GeometryStageSurfacesLoaded")
                && geometryWriter.Contains("GeometryStageCircuitBroken")
                && geometryWriter.Contains("this.loadAllSurfacesCallCount")
                && geometryWriter.Contains("return;")
                && geometryWriter.Contains("WriteCanonicalGeometryAttempt(attemptPath)")
                && geometryWriter.Contains("this.stableGeometryCandidateSha256")
                && geometryWriter.Contains("ComputeFileSha256(attemptPath)")
                && geometryWriter.Contains("ComputeFileSha256(candidatePath)")
                && geometryWriter.Contains("PromoteAttemptFile(attemptPath, candidatePath)")
                && geometryWriter.Contains("PromoteAttemptFile(attemptPath, this.geometryPath)")
                && geometryAttempt.Contains("modelIdentity.Instance != ResourcePlayfieldId")
                && geometryAttempt.Contains("Playfield.Zones")
                && geometryAttempt.Contains("Playfield.Rooms")
                && geometryAttempt.Contains("SnapshotReferenceCollection(liveZones")
                && geometryAttempt.Contains("SnapshotReferenceCollection(liveRooms")
                && geometryAttempt.Contains("zoneInstances.SequenceEqual(roomInstances)")
                && geometryAttempt.Contains("foreach (RoomGeometrySourceSnapshot room in rooms)")
                && geometryAttempt.Contains("mesh.Vertices")
                && geometryAttempt.Contains("mesh.TriangleIndices")
                && geometryAttempt.Contains("mesh.LocalToWorld")
                && geometryAttempt.Contains("MultiplyPoint3x4")
                && geometryAttempt.Contains("roomSnapshot.VertexCount == 0")
                && geometryAttempt.Contains("roomSnapshot.SourceTriangleIndexCount == 0")
                && geometryAttempt.Contains("roomSnapshot.TriangleCount == 0")
                && geometryAttempt.Contains("roomSnapshots.Sum(room => room.MeshCount) != result.MeshCount")
                && staticDoorCapture.Contains("Playfield.Doors")
                && staticDoorCapture.Contains("CaptureStaticDoorSnapshot(door)")
                && staticDoorProjection.Contains("Link1Resolution = DoorLinkUnavailableForClientSafety")
                && staticDoorProjection.Contains("Link2Resolution = DoorLinkUnavailableForClientSafety")
                && !geometryCapture.Contains("door.RoomLink1")
                && !geometryCapture.Contains("door.RoomLink2")
                && !geometryCapture.Contains("room.Doors")
                && !geometryCapture.Contains("PropertyInfo")
                && !geometryCapture.Contains("BindingFlags")
                && !geometryCapture.Contains("GetValue(door")
                && !geometryCapture.Contains("room.NumDoors")
                && roomGeometryProjection.Contains("N3Zone_t.GetSurface(room.Pointer)")
                && roomGeometryProjection.IndexOf("N3Zone_t.GetSurface(room.Pointer)", StringComparison.Ordinal)
                   < roomGeometryProjection.IndexOf("SurfaceResource surface = room.SurfaceResource", StringComparison.Ordinal)
                && roomGeometryProjection.Contains("SurfaceResource surface = room.SurfaceResource")
                && roomGeometryProjection.Contains("SnapshotReferenceCollection(")
                && meshGeometryProjection.Contains("IEnumerable<Vector3> liveVertices = mesh.Vertices")
                && meshGeometryProjection.Contains("IEnumerable<int> liveTriangles = mesh.Triangles")
                && meshGeometryProjection.Contains("Matrix4x4 localToWorld = mesh.LocalToWorldMatrix")
                && geometryCapture.Contains("\\\"rooms\\\"")
                && geometryCapture.Contains("\\\"doors\\\"")
                && geometryCapture.Contains("\\\"triangles\\\"")
                && geometryCapture.Contains("\\\"doorLinkSchemaVersion\\\"")
                && geometryCapture.Contains("\\\"doorLinkCapturePolicy\\\"")
                && geometryCapture.Contains("unavailable_not_read_for_client_safety")
                && geometryCapture.Contains("\\\"rawLink1Index\\\"")
                && geometryCapture.Contains("\\\"link1Resolution\\\"")
                && geometryCapture.Contains("\\\"rawLink2Index\\\"")
                && geometryCapture.Contains("\\\"link2Resolution\\\"")
                && geometryCapture.Contains("\\\"roomInstances\\\"")
                && geometryCapture.Contains("\\\"meshes\\\""),
                "The streamed canonical PF127 artifact must validate complete world-space room collision geometry while explicitly omitting unsafe in-process door-link reads.");
            Assert.IsFalse(
                staticDoorCapture.Contains("door.IsOpen")
                || staticDoorCapture.Contains("door.IsLocked")
                || geometryAttempt.Contains("\\\"isOpen\\\"")
                || geometryAttempt.Contains("\\\"isLocked\\\"")
                || geometryCapture.Contains("GeometrySnapshot")
                || geometryCapture.Contains("TriangleSnapshot")
                || geometryCapture.Contains("worldVertices")
                || geometryCapture.Contains("GetBytes(content)")
                || geometryCapture.Contains("File.WriteAllText"),
                "Canonical geometry must exclude transient client door open/locked state so identical geometry snapshots remain promotable.");
            Assert.IsTrue(
                lineOfSightSampler.Contains("DynelManager.LocalPlayer")
                && lineOfSightSampler.Contains("DynelManager.Characters")
                && lineOfSightSampler.Contains("foreach (LineOfSightTargetSnapshot target in characterSnapshots)")
                && lineOfSightSampler.Contains("this.WriteLineOfSightRows(")
                && lineOfSightSampler.Contains("targetResult.HasUsableVariantPair")
                && lineOfSightSampler.Contains("doorState.Usable")
                && lineOfSightSampler.Contains("VergilAeneidMonsterData")
                && lineOfSightSampler.Contains("() => character.IsInLineOfSight")
                && lineOfSightRow.Contains("target.SimpleCharLineOfSight")
                && !geometryCapture.Contains("target.Character")
                && lineOfSightRow.Contains("\"raw\"")
                && lineOfSightRow.Contains("\"plus-one-y\"")
                && lineOfSightRow.Contains("rawUsable && plusOneUsable")
                && lineOfSightRow.Contains("localPosition + new Vector3(0f, 1f, 0f)")
                && lineOfSightRow.Contains("targetPosition + new Vector3(0f, 1f, 0f)")
                && lineOfSightVariantRow.Contains("Playfield.LineOfSight(")
                && lineOfSightVariantRow.Contains("Playfield.Raycast(")
                && lineOfSightVariantRow.Contains("localIdentity.ToString()")
                && lineOfSightVariantRow.Contains("targetIdentity.ToString()")
                && lineOfSightVariantRow.Contains("targetIdentity.Instance")
                && lineOfSightVariantRow.Contains("FloatCsv(origin.X)")
                && lineOfSightVariantRow.Contains("FloatCsv(origin.Y)")
                && lineOfSightVariantRow.Contains("FloatCsv(origin.Z)")
                && geometryCapture.Contains("ProbeVariant,ProbeHeight")
                && geometryCapture.Contains("DoorStateRevision,EvidenceBatchId")
                && geometryCapture.Contains("OriginX,OriginY,OriginZ")
                && geometryCapture.Contains("TargetIdentity,TargetIdentityType,TargetIdentityInstance,TargetMonsterData,TargetName")
                && geometryCapture.Contains("TargetX,TargetY,TargetZ")
                && geometryCapture.Contains("SimpleCharIsInLineOfSight,PlayfieldLineOfSight,RaycastHit")
                && geometryCapture.Contains("RaycastHitX,RaycastHitY,RaycastHitZ")
                && geometryCapture.Contains("RaycastNormalX,RaycastNormalY,RaycastNormalZ,Usable,Error")
                && geometryCapture.Contains("!IsFinite(position) || !IsFinite(rotation)")
                && doorStateBatch.Contains("evidenceBatchId")
                && doorStateBatch.Contains("this.usableDoorStateBatchCount")
                && geometryCapture.Contains("CapturedUtc,Trigger,Revision,EvidenceBatchId")
                && geometryCapture.Contains("DoorLinkSchemaVersion,RawLink1Index,Link1Resolution,Room1Instance,RawLink2Index,Link2Resolution,Room2Instance")
                && geometryCapture.Contains("PositionX,PositionY,PositionZ,RotationX,RotationY,RotationZ,RotationW"),
                "Each LOS batch must pair both variants for the same identified target with a finite dynamic door-state revision and all client obstruction probe details.");
        }

        [TestMethod]
        public void CaptureToolFailsClosedWhenPf127GeometryOrLosEvidenceIsIncomplete()
        {
            string root = FindRepositoryRoot();
            string captureTool = File.ReadAllText(
                    Path.Combine(root, @"tools-temp\AOSharpLiveCapture\Main.cs"))
                .Replace("\r\n", "\n");
            string geometryCapture = File.ReadAllText(
                    Path.Combine(root, @"tools-temp\AOSharpLiveCapture\Pf127GeometryCapture.cs"))
                .Replace("\r\n", "\n");
            string recaptureContract = ExtractMethodBlock(
                geometryCapture,
                "public bool RecaptureRequired");
            string geometryValidation = ExtractMethodBlock(
                geometryCapture,
                "public void AppendValidation(List<string> issues, List<string> notes)");
            string captureValidation = ExtractMethodBlock(
                captureTool,
                "private CaptureValidation ValidateCapture()");
            string recaptureAggregation = ExtractMethodBlock(
                captureTool,
                "private bool IsCaptureRecaptureRequired()");
            string geometryWriter = ExtractMethodBlock(
                geometryCapture,
                "private void TryWriteCanonicalGeometry()");
            string geometryAttempt = ExtractMethodBlock(
                geometryCapture,
                "private static GeometryWriteResult WriteCanonicalGeometryAttempt(string path)");
            string doorStateBatch = ExtractMethodBlock(
                geometryCapture,
                "private DoorStateBatchResult CaptureDoorStateBatch(");
            string lineOfSightRow = ExtractMethodBlock(
                geometryCapture,
                "private LineOfSightTargetBatchResult WriteLineOfSightRows(");
            string lineOfSightVariantRow = ExtractMethodBlock(
                geometryCapture,
                "private bool WriteLineOfSightVariantRow(");

            Assert.IsTrue(
                recaptureContract.Contains("this.Pf127Observed")
                && recaptureContract.Contains("!this.GeometryWritten")
                && recaptureContract.Contains("this.Pf127CombatObserved")
                && recaptureContract.Contains("this.combatUsableRawVariantRowCount")
                && recaptureContract.Contains("this.combatUsablePlusOneVariantRowCount")
                && recaptureContract.Contains("this.combatMatchedDoorAndLosBatchCount")
                && recaptureContract.Contains("this.vergilCombatObserved")
                && recaptureContract.Contains("this.vergilCombatMatchedDoorAndLosBatchCount")
                && recaptureContract.Contains("this.usableDoorStateBatchCount")
                && recaptureContract.Contains("this.lineOfSightWriteErrorCount")
                && recaptureContract.Contains("this.doorStateWriteErrorCount")
                && recaptureContract.Contains("this.RuntimeBoundaryCircuitBroken"),
                "PF127 runtime-only geometry loss, unrecovered same-target raw/plus-one combat LOS and door-state coverage, or evidence writer failure must require recapture.");
            Assert.IsFalse(
                recaptureContract.Contains("this.lineOfSightProbeErrorCount"),
                "A transient LOS probe failure must remain recoverable when later raw and plus-one-Y coverage succeeds.");
            int probeValidationStart = geometryValidation.IndexOf(
                "int probeErrors = Volatile.Read(ref this.lineOfSightProbeErrorCount)",
                StringComparison.Ordinal);
            int writerValidationStart = geometryValidation.IndexOf(
                "int writeErrors = Volatile.Read(ref this.lineOfSightWriteErrorCount)",
                StringComparison.Ordinal);
            string probeValidation = probeValidationStart >= 0 && writerValidationStart > probeValidationStart
                                         ? geometryValidation.Substring(
                                             probeValidationStart,
                                             writerValidationStart - probeValidationStart)
                                         : string.Empty;
            string writerValidation = writerValidationStart >= 0
                                          ? geometryValidation.Substring(writerValidationStart)
                                          : string.Empty;
            Assert.IsTrue(
                geometryValidation.Contains("if (!this.Pf127Observed)")
                && geometryValidation.Contains("if (!this.GeometryWritten)")
                && geometryValidation.Contains("issues.Add(")
                && geometryValidation.Contains("deterministic pf127-geometry.json was not written")
                && geometryValidation.Contains("this.Pf127CombatObserved")
                && geometryValidation.Contains("this.combatUsableRawVariantRowCount")
                && geometryValidation.Contains("this.combatUsablePlusOneVariantRowCount")
                && geometryValidation.Contains("this.combatMatchedDoorAndLosBatchCount")
                && geometryValidation.Contains("this.vergilCombatMatchedDoorAndLosBatchCount")
                && geometryValidation.Contains("this.usableDoorStateBatchCount")
                && geometryValidation.Contains("same-batch combat-target match")
                && geometryValidation.Contains("this.lineOfSightProbeErrorCount")
                && geometryValidation.Contains("this.lineOfSightWriteErrorCount")
                && geometryValidation.Contains("this.doorStateWriteErrorCount")
                && probeValidation.Contains("notes.Add(")
                && !probeValidation.Contains("issues.Add(")
                && writerValidation.Contains("issues.Add("),
                "PF127 validation must fail closed for missing required evidence and writer loss while reporting recovered probe errors without permanently poisoning the capture.");
            Assert.IsTrue(
                recaptureAggregation.Contains("this.IsRawRecaptureRequired()")
                && recaptureAggregation.Contains("this.pf127GeometryCapture.RecaptureRequired")
                && captureValidation.Contains("bool recaptureRequired = this.IsCaptureRecaptureRequired()")
                && captureValidation.Contains("this.pf127GeometryCapture?.AppendValidation(issues, notes)")
                && captureValidation.Contains("bool offlineDecodeRequired = !recaptureRequired")
                && captureValidation.Contains("bool processingAllowed = issues.Count == 0")
                && captureValidation.IndexOf(
                    "this.pf127GeometryCapture?.AppendValidation(issues, notes)",
                    StringComparison.Ordinal)
                   < captureValidation.IndexOf(
                       "bool offlineDecodeRequired = !recaptureRequired",
                       StringComparison.Ordinal),
                "PF127 runtime evidence must participate in the authoritative recapture decision before offline-repair and complete-status decisions are made.");
            Assert.IsTrue(
                geometryCapture.Contains("\"pf127Observed\"")
                && geometryCapture.Contains("\"pf127CombatObserved\"")
                && geometryCapture.Contains("\"complete\"")
                && geometryCapture.Contains("\"recaptureRequired\"")
                && geometryCapture.Contains("\"attempts\"")
                && geometryCapture.Contains("\"failures\"")
                && geometryCapture.Contains("\"stage\"")
                && geometryCapture.Contains("\"circuitBroken\"")
                && geometryCapture.Contains("\"loadAllSurfacesCalls\"")
                && geometryCapture.Contains("\"combatTriggers\"")
                && geometryCapture.Contains("\"periodicBatches\"")
                && geometryCapture.Contains("\"combatBatches\"")
                && geometryCapture.Contains("\"combatUsableRows\"")
                && geometryCapture.Contains("\"combatUsableRawVariantRows\"")
                && geometryCapture.Contains("\"combatUsablePlusOneVariantRows\"")
                && geometryCapture.Contains("\"vergilCombatMatchedDoorAndLosBatches\"")
                && geometryCapture.Contains("\"doorStatePath\"")
                && geometryCapture.Contains("\"usableBatches\"")
                && geometryCapture.Contains("\"combatMatchedLosBatches\"")
                && geometryCapture.Contains("\"probeErrors\"")
                && geometryCapture.Contains("\"writeErrors\"")
                && captureTool.Contains("this.pf127GeometryCapture.AppendHealthJson(json, \"  \")"),
                "capture-health and capture-info must expose the PF127 completeness, retry, sampling, usability, and error counters used by validation.");

            int streamedGeometryWrite = geometryWriter.IndexOf(
                "WriteCanonicalGeometryAttempt(attemptPath)",
                StringComparison.Ordinal);
            int hashGeometry = geometryWriter.IndexOf(
                "ComputeFileSha256(attemptPath)",
                StringComparison.Ordinal);
            int promoteGeometry = geometryWriter.IndexOf(
                "PromoteAttemptFile(attemptPath, this.geometryPath)",
                StringComparison.Ordinal);
            int geometryComplete = geometryWriter.IndexOf(
                "Interlocked.Exchange(ref this.geometryWritten, 1)",
                StringComparison.Ordinal);
            int losWrite = lineOfSightVariantRow.IndexOf(
                "this.lineOfSightWriter.WriteLine(row)",
                StringComparison.Ordinal);
            int losRowCount = lineOfSightVariantRow.IndexOf(
                "Interlocked.Increment(ref this.lineOfSightRowCount)",
                StringComparison.Ordinal);
            Assert.IsTrue(
                streamedGeometryWrite >= 0
                && hashGeometry > streamedGeometryWrite
                && promoteGeometry > hashGeometry
                && geometryComplete > promoteGeometry
                && geometryAttempt.Contains("zoneInstances.SequenceEqual(roomInstances)")
                && geometryAttempt.Contains("roomSnapshot.VertexCount == 0")
                && geometryAttempt.Contains("roomSnapshot.SourceTriangleIndexCount == 0")
                && geometryAttempt.Contains("roomSnapshot.TriangleCount == 0")
                && geometryAttempt.Contains("CaptureStaticDoorSnapshots()")
                && geometryAttempt.Contains("doors.Count == 0")
                && geometryAttempt.Contains("unavailable_not_read_for_client_safety")
                && geometryWriter.Contains("Interlocked.Increment(ref this.geometryFailureCount)")
                && geometryWriter.Contains("GeometryStageCircuitBroken")
                && geometryWriter.Contains("DeleteFileNoThrow(attemptPath)")
                && losWrite >= 0
                && losRowCount > losWrite
                && lineOfSightRow.Contains("Interlocked.Increment(ref this.lineOfSightProbeErrorCount)")
                && lineOfSightVariantRow.Contains("Interlocked.Increment(ref this.lineOfSightProbeErrorCount)")
                && lineOfSightRow.Contains("rawUsable && plusOneUsable")
                && geometryCapture.Contains("!IsFinite(position) || !IsFinite(rotation)")
                && doorStateBatch.IndexOf(
                    "this.doorStateWriter.WriteLine(row)",
                    StringComparison.Ordinal)
                   < doorStateBatch.IndexOf(
                       "Interlocked.Increment(ref this.usableDoorStateBatchCount)",
                       StringComparison.Ordinal),
                "Geometry may become complete only after validated streamed hashing and atomic promotion, while LOS and finite door-state counters advance only after preserved same-batch evidence.");
        }

        [TestMethod]
        public void ReplacementInfectorScfuMatchesCapturedPacket1896Exactly()
        {
            var capturedFlags = (SimpleCharFullUpdateFlags)0x022A4A43;
            var message = new SimpleCharFullUpdateMessage
            {
                Identity = new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = unchecked((int)0x79607AD0)
                },
                Version = 58,
                PlayfieldId = 0x00153008,
                Coordinates = new Vector3
                {
                    X = 334.027709961f,
                    Y = 73.617950440f,
                    Z = 99.046173096f
                },
                Heading = new Quaternion
                {
                    X = 0.0f,
                    Y = 0.715929568f,
                    Z = 0.0f,
                    W = 0.698172510f
                },
                Appearance = new Appearance { Value = 0x04C8 },
                Name = "Infector",
                CharacterFlags = (CharacterFlags)unchecked((int)0x10081201),
                CharacterInfo = new SimpleNpcInfo
                {
                    Family = 150,
                    LosHeight = 0,
                    UnknownData = 0x0A
                },
                Level = 24,
                Health = 968,
                HealthDamage = 0,
                MonsterData = 31909,
                MonsterScale = 70,
                VisualFlags = 31,
                Unknown1 = HexToBytes(
                    "00000000000000008000000003010001000100010001000000020000"),
                RunSpeedBase = 105,
                ActiveNanos = new ActiveNano[0],
                Waypoints = new Vector3[0],
                Textures = new[]
                {
                    new Texture { Place = 0, Id = 0, Unknown = 0 },
                    new Texture { Place = 1, Id = 0, Unknown = 0 },
                    new Texture { Place = 2, Id = 0, Unknown = 0 },
                    new Texture { Place = 3, Id = 0, Unknown = 0 },
                    new Texture { Place = 4, Id = 0, Unknown = 0 }
                },
                Meshes = new Mesh[0],
                AdditionalFlags = capturedFlags,
                SuppressedFlags = ~capturedFlags,
                Flags2 = 2,
                Unknown2 = 0
            };

            Assert.AreEqual(
                CapturedReplacementInfectorScfuHex,
                ToHex(Serialize(message)));
        }

        [TestMethod]
        public void CapturedReplacementInfectorScfuDecodesCompletelyAndRoundTripsExactly()
        {
            SimpleCharFullUpdateMessage message = Deserialize<SimpleCharFullUpdateMessage>(
                HexToBytes(CapturedReplacementInfectorScfuHex));
            var npc = message.CharacterInfo as SimpleNpcInfo;

            Assert.IsNotNull(npc);
            Assert.AreEqual("Infector", message.Name);
            Assert.AreEqual(0x00153008, message.PlayfieldId);
            Assert.AreEqual(334.027709961f, message.Coordinates.X);
            Assert.AreEqual(0.715929568f, message.Heading.Y);
            Assert.AreEqual((short)150, npc.Family);
            Assert.AreEqual((short)0x0A, npc.UnknownData);
            Assert.AreEqual((short)24, message.Level);
            Assert.AreEqual(968, message.Health);
            Assert.AreEqual((uint)31909, message.MonsterData);
            Assert.AreEqual(28, message.Unknown1.Length);
            Assert.AreEqual(0, message.ActiveNanos.Length);
            Assert.AreEqual(0, message.Waypoints.Length);
            Assert.AreEqual(5, message.Textures.Length);
            Assert.AreEqual(0, message.Meshes.Length);
            Assert.AreEqual(2, message.Flags2);
            Assert.AreEqual((byte)0, message.Unknown4);
            Assert.IsTrue(message.TailFullyDecoded);
            Assert.AreEqual(CapturedReplacementInfectorScfuHex.Length / 2, message.RawBody.Length);
            Assert.AreEqual(CapturedReplacementInfectorScfuHex, ToHex(Serialize(message)));
        }

        [TestMethod]
        public void ScfuTailReadsUnknown4OnlyWhenFlags2RequiresIt()
        {
            SimpleCharFullUpdateMessage message = Deserialize<SimpleCharFullUpdateMessage>(
                HexToBytes(CapturedReplacementInfectorScfuHex));
            message.Flags2 = 0;
            message.Unknown2 = 0x5A;
            message.Unknown4 = 0xA5;

            byte[] withoutUnknown4 = Serialize(message);
            SimpleCharFullUpdateMessage decoded = Deserialize<SimpleCharFullUpdateMessage>(withoutUnknown4);

            Assert.AreEqual((CapturedReplacementInfectorScfuHex.Length / 2) - 1, withoutUnknown4.Length);
            Assert.IsTrue(ToHex(withoutUnknown4).EndsWith("000000005A", StringComparison.Ordinal));
            Assert.AreEqual(0, decoded.Flags2);
            Assert.AreEqual((byte)0x5A, decoded.Unknown2);
            Assert.AreEqual((byte)0, decoded.Unknown4);
            Assert.IsTrue(decoded.TailFullyDecoded);
            Assert.AreEqual(ToHex(withoutUnknown4), ToHex(Serialize(decoded)));
        }

        private static string ReadPlayfieldSource(string root, string file)
        {
            return File.ReadAllText(
                    Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Playfields", file))
                .Replace("\r\n", "\n");
        }

        private static string ReadPacketSource(string root, string file)
        {
            return File.ReadAllText(
                    Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Packets", file))
                .Replace("\r\n", "\n");
        }

        private static int CountOccurrences(string value, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += pattern.Length;
            }

            return count;
        }

        private static string ExtractMethodBlock(string text, string methodMarker)
        {
            int signatureIndex = text.IndexOf(methodMarker, StringComparison.Ordinal);
            Assert.IsTrue(signatureIndex >= 0, "Missing method or member " + methodMarker + ".");

            int startIndex = text.IndexOf("{", signatureIndex, StringComparison.Ordinal);
            Assert.IsTrue(startIndex >= 0, "Missing body for " + methodMarker + ".");

            int depth = 0;
            bool insideString = false;
            bool insideCharacter = false;
            bool escaped = false;
            for (int index = startIndex; index < text.Length; index++)
            {
                char current = text[index];
                if (insideString || insideCharacter)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (current == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if ((insideString && current == '"') || (insideCharacter && current == '\''))
                    {
                        insideString = false;
                        insideCharacter = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    insideString = true;
                    continue;
                }

                if (current == '\'')
                {
                    insideCharacter = true;
                    continue;
                }

                if (current == '{')
                {
                    depth++;
                }
                else if (current == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(startIndex, index - startIndex + 1);
                    }
                }
            }

            Assert.Fail("Unterminated body for " + methodMarker + ".");
            return string.Empty;
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] result = new byte[hex.Length / 2];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
            }

            return result;
        }

        private static byte[] Serialize(MessageBody body)
        {
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            var serializer = resolver.GetSerializer(body.GetType());
            using (var stream = new MemoryStream())
            using (var writer = new MessagingStreamWriter(stream))
            {
                // The production packet writer places the N3 body after a 16-byte transport header.
                // The SCFU serializer patches its flags at the resulting absolute offset 30.
                writer.Position = 16;
                serializer.Serialize(writer, new SerializationContext(resolver), body);
                byte[] packet = stream.ToArray();
                byte[] serializedBody = new byte[packet.Length - 16];
                Buffer.BlockCopy(packet, 16, serializedBody, 0, serializedBody.Length);
                return serializedBody;
            }
        }

        private static T Deserialize<T>(byte[] bytes)
            where T : MessageBody
        {
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            var serializer = resolver.GetSerializer(typeof(T));
            using (var stream = new MemoryStream(bytes))
            using (var reader = new MessagingStreamReader(stream))
            {
                return (T)serializer.Deserialize(reader, new SerializationContext(resolver));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        private static string FindRepositoryRoot()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, ".git")))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            throw new InvalidOperationException("Repository root not found.");
        }
    }
}
