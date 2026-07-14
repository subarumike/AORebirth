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
                npcRuntime.Contains("foreach (ICharacter summon in this.capturedSubwayEncounters.NotifyDeath(target))")
                && npcRuntime.Contains("this.playfield.DespawnNpcImmediately(summon);")
                && npcRuntime.Contains("this.capturedSubwayEncounters.NotifyNpcDespawn(target, utcNow);")
                && npcRuntime.Contains("CapturedEncounterRuntimeRegistry.Remove(target.Identity.Instance);"),
                "NPCRuntimeService must immediately despawn both living summons and remove encounter registration.");
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
                && encounter.Contains("155548,\n                300.0,\n                3.0,")
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
                && globalLoot.Contains("LootRollMode.ObservedSnapshot")
                && globalLoot.Contains("ItemPoolUnresolved = true")
                && globalLoot.Contains("ObservedSnapshotGroup(0, 136622, 136623, 30)")
                && globalLoot.Contains("ObservedSnapshotGroup(1, 202717, 202718, 28)")
                && globalLoot.Contains("ObservedSnapshotGroup(2, 107933, 107934, 23)")
                && globalLoot.Contains("ObservedSnapshotGroup(3, 85693, 27389, 30)")
                && globalLoot.Contains("ObservedSnapshotGroup(4, 287146, 287146, 200)")
                && globalLoot.Contains("CapturedAbmouthCredits = 587")
                && globalLoot.Contains("RollCount = 1")
                && globalLoot.Contains("MinimumQuantity = 1")
                && globalLoot.Contains("MaximumQuantity = 1")
                && globalLoot.Contains("DropChanceBasisPoints = 0")
                && globalLoot.Contains("Semantics = LootSemantics.ObservedAvailable"),
                "The one observed boss corpse snapshot must remain exact while the wider item pool stays unresolved.");
        }

        [TestMethod]
        public void EncounterDoesNotInventAnUncapturedBossRespawn()
        {
            string encounter = ReadPlayfieldSource(FindRepositoryRoot(), "CapturedSubwayEncounterRuntimeService.cs");

            Assert.IsTrue(
                encounter.Contains("definition.ProfileKey,\n                AbmouthProfileKey,")
                && encounter.Contains("this.abmouthIdentity = Identity.None;")
                && encounter.Contains("this.abmouthIdentity = boss.Identity;")
                && encounter.Contains("ActivatePlayfield(Identity playfieldIdentity)")
                && CountOccurrences(encounter, "CreateBossDefinition()") == 2,
                "The boss may spawn at playfield activation and must become absent after its captured despawn.");
            Assert.IsFalse(
                encounter.Contains("BossRespawn")
                || encounter.Contains("RespawnBoss")
                || encounter.Contains("bossSpawnDueAtUtc")
                || encounter.Contains("BossRespawnDelay"),
                "No boss respawn trigger or delay may be invented from the incomplete capture.");
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
        public void VergilPreservesExactPf127SpawnAppearanceAndObservedLevelHealthPair()
        {
            string encounter = ReadPlayfieldSource(
                FindRepositoryRoot(),
                "CapturedSubwayEncounterRuntimeService.cs");

            Assert.IsTrue(
                encounter.Contains("internal const int SubwayPlayfieldId = 127;")
                && encounter.Contains("new CapturedEncounterLevelHealthVariant(\n                30,\n                7227,")
                && encounter.Contains("new CapturedEncounterLevelHealthVariant(\n                31,\n                7659,")
                && encounter.Contains("variant = VergilAeneidVariants[this.spawnRandom.Next(VergilAeneidVariants.Length)]")
                && encounter.Contains("variant.Level,\n                variant.Health,"),
                "Vergil must select only the two captured level/health pairs in PF127.");
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
        public void VergilUsesCapturedWeaponTimingCorpseAndTwoLootAlternatives()
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
                encounter.Contains("5921,\n                300.0,\n                3.0,")
                && corpse.Contains("CapturedSubwayVergilPacketLength = 420")
                && corpse.Contains("CapturedSubwayVergilTemplate")
                && corpse.Contains("BuildCapturedSubwayVergil(")
                && corpse.Contains("corpseMonsterData == NpcCombatAttackRules.CapturedSubwayVergilMonsterData"),
                "Vergil must retain the exact 420-byte corpse template and CATMesh 5921.");
            Assert.IsTrue(
                globalLoot.Contains("CapturedVergilCreditOutcomes = { 587, 610 }")
                && globalLoot.Contains("ObservedAlternativeGroup(\n                        0,")
                && globalLoot.Contains("\"capture.20260712-232711\",\n                            301713,\n                            301713,\n                            1,")
                && globalLoot.Contains("\"capture.20260712-234401\",\n                            301714,\n                            301714,\n                            1,")
                && globalLoot.Contains("ObservedAlternativeGroup(\n                        1,")
                && globalLoot.Contains("\"capture.20260712-232711\",\n                            202743,\n                            202744,\n                            32,")
                && globalLoot.Contains("\"capture.20260712-234401\",\n                            123571,\n                            123572,\n                            23,")
                && globalLoot.Contains("ObservedSnapshotGroup(\n                        2,\n                        287146,\n                        287146,\n                        200,")
                && globalLoot.Contains("CreditsPolicy = CreditsObservedSet(CapturedVergilCreditOutcomes)")
                && globalLoot.Contains("ItemPoolUnresolved = true"),
                "Vergil loot must select the two observed slot alternatives, fixed third item, and captured 587/610 credit set only.");
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
                && encounter.Contains("if (level >= 31)")
                && encounter.Contains("VergilDirectHealNanoId,\n                    VergilDirectHealAmount,\n                    VergilDirectHealCastSeconds,")
                && encounter.Contains("utcNow.AddSeconds(VergilDirectHealCooldownSeconds)"),
                "Level-31 Vergil must retain captured nano 43827, 187 healing, 1.480007-second cast, and 30.654-second cooldown.");
            Assert.IsTrue(
                encounter.Contains("VergilSelfHealNanoId = 43880")
                && encounter.Contains("VergilSelfHealAmount = 34")
                && encounter.Contains("VergilSelfHealDurationMilliseconds = 14000")
                && encounter.Contains("VergilSelfHealCastSeconds = 1.763334")
                && encounter.Contains("VergilSelfHealNanoId,\n                VergilSelfHealAmount,\n                VergilSelfHealCastSeconds,\n                VergilSelfHealDurationMilliseconds,")
                && encounter.Contains("this.vergilNextHealAtUtc = DateTime.MaxValue;"),
                "Level-30 Vergil must retain captured nano 43880, 34 healing, 14-second duration, and 1.763334-second cast without invented repetition.");
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
        public void VergilDoesNotInventRespawnAndCaptureProjectionsRemainAlwaysOn()
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
                && CountOccurrences(encounter, "CreateVergilAeneidDefinition()") == 2,
                "Vergil may spawn on PF127 activation and becomes absent after despawn.");
            Assert.IsFalse(
                encounter.Contains("VergilRespawn")
                || encounter.Contains("RespawnVergil")
                || encounter.Contains("vergilSpawnDueAtUtc")
                || encounter.Contains("VergilRespawnDelay"),
                "No Vergil respawn trigger or delay may be invented from these captures.");
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
                && captureTool.Contains("RAW-PACKET-CALLBACK-ERROR")
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
            int openInactive = captureTool.IndexOf(
                "this.OpenFreshCaptureSession(pluginDir, true, false);",
                run,
                StringComparison.Ordinal);
            int subscribeInbound = captureTool.IndexOf(
                "Network.PacketReceived += this.OnPacketReceived;",
                openInactive,
                StringComparison.Ordinal);
            int subscribeOutbound = captureTool.IndexOf(
                "Network.PacketSent += this.OnPacketSent;",
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
                openInactive > run
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
