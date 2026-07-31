namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields;
    using Utility;

    internal sealed class GlobalLootRuntimeService
    {
        private const string CleaningRobotProfileKey = "captured.arete.cleaning-robot";
        private const int CleaningRobotMonsterData = 297023;
        private const int CleaningRobotCredits = 5;

        // Capture 20260720-204431 / 20260720-212302 Alex-pad hostiles.
        private const string AlexDockerProfileKey = "captured.arete.alex-32v-docker";
        private const int AlexDockerMonsterData = 17649;
        private const int AlexDockerCredits = 4;
        private const string AlexWasteProfileKey = "captured.arete.alex-waste-collector";
        private const int AlexWasteMonsterData = 17714;
        private const int AlexWasteCredits = 11;
        private const string AlexFleaProfileKey = "captured.arete.alex-garbage-flea";
        private const int AlexFleaMonsterData = 17657;
        private const int AlexFleaCredits = 11;
        private const string AreteCleanmeisterProfileKey =
            "captured.arete.cleanmeister-intelligence-robot";
        private const string AreteSupremeCollectorProfileKey =
            "captured.arete.supreme-collector-of-waste";
        private const string AreteMalfunctioningRobotProfileKey =
            "captured.arete.malfunctioning-cleaning-robot";
        private const string AreteRollerratProfileKey = "captured.arete.rollerrat";
        private const string AreteDesertReetProfileKey = "captured.arete.desert-reet";
        private const string AreteAngryMinibullProfileKey = "captured.arete.angry-minibull";
        private const string AreteGnarlProfileKey = "captured.arete.gnarl-the-roller";
        private const string AreteKneebreakerProfileKey =
            "captured.arete.kneebreaker-alfonzo-rizzolo";
        private const int AretePlayfieldId = 1044525;
        private const int AreteRollerratMonsterData = 17687;
        private const int AreteDesertReetMonsterData = 30365;
        private const int AreteAngryMinibullMonsterData = 30360;
        private const string AlexPadLootEvidence =
            "AOSharpLiveCapture 20260722-cap-mob-drop-cred corpse-loot-observations; Docker credits=4; Waste credits=11; Flea credits=5|11; Cleaning Robot credits=5";
        private const string AretePartOneLootEvidence =
            "AOSharpLiveCapture 20260722-104809 corpse-loot-observations.csv; "
            + "identity-linked atomic snapshots for Cleanmeister Intelligence Robot, "
            + "Supreme Collector of Waste, and Malfunctioning Cleaning Robot";
        private const string AretePartTwoLootEvidence =
            "AOSharpLiveCapture 20260722-152454 corpse-loot-observations.csv; "
            + "identity-linked atomic snapshots; snapshot probabilities and wider pools unresolved";
        private const string CleaningRobotLootEvidence =
            "AOSharpLiveCapture 20260722-cap-mob-drop-cred; Cleaning Robot credits=5; Robot Junk 42620 / empty / misc";
        // Capture 20260723-221330 Nascence Life corpses.
        private const string NascenceChimeraProfileKey = "captured.nascence.barking-chimera";
        private const string NascenceYuttosProfileKey = "captured.nascence.yuttos";
        private const string NascenceDreamingSilvertailProfileKey = "captured.nascence.dreaming-silvertail";
        private const string NascenceSwiftProfileKey = "captured.nascence.swift-silvertail";
        private const string NascenceLifeLootEvidence =
            "AOSharpLiveCapture 20260723-225021 Barking Chimera 15 corpses credits=0 (8 empty + 7 with items); 20260723-221330 Swift Silvertail 798C1F89 items 232839:232840 ql6 + 42640:42641 ql7; Dreaming/Yuttos empty openable corpse";
        private const int CapturedAbmouthCredits = 587;
        private const int CapturedInfectorCredits = 150;
        private const int CapturedEumenidesCredits = 186;
        private const int CapturedStrikeForemanCredits = 176;
        private const string CapturedVergilProfileKey = "subway.127.boss.vergil-aeneid";
        private const int CapturedVergilMonsterData = 203748;
        private const string CapturedAbmouthLootEvidence =
            "official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved";
        private const string CapturedVergilLootEvidence =
            "official-live-captures 20260712-232711/234401/20260716-034433; three exact observed corpse snapshots with linked credits 610/587/563; 20260716-034433 inventory linked by normalized corpse identity F69001; snapshot probabilities and wider pool unresolved";
        private const string CapturedEumenidesLootEvidence =
            "official-live-captures 20260717-214751/20260717-215250; two exact identity-linked Eumenides corpse snapshots, each with 186 credits and three item rows; 20260717-220340 adds exact local-name/identity-linked item membership for two already-existing Remains of Eumenides corpses but no CorpseFullUpdate, credits, dead-NPC link, or playfield context, so those rows are not promoted as atomic runtime snapshots; snapshot probabilities and wider pool unresolved";
        private const string CapturedStrikeForemanLootEvidence =
            "official-live-captures 20260720-032106/033513; two exact "
            + "identity-linked Strike Foreman corpse snapshots, each with 176 "
            + "credits; item membership is atomic, snapshot probabilities and "
            + "the wider pool remain unresolved, and enemy level owns item QL "
            + "within each captured template pair";
        private readonly object sync = new object();
        private readonly object productionRandomSync = new object();
        private readonly Random productionRandom = new Random();
        private readonly LootTableRegistry registry;
        private readonly LootGenerationService generator;
        private bool databaseLoaded;
        private CombatLootTableEntry[] databaseEntries = new CombatLootTableEntry[0];
        private CombatLootTableEntry[] debugEntries = new CombatLootTableEntry[0];

        internal GlobalLootRuntimeService()
        {
            this.registry = new LootTableRegistry(value => ItemLoader.ItemList.ContainsKey(value));
            this.generator = new LootGenerationService(this.registry, new LootAssignmentResolver());
        }

        internal LootTableRegistry Registry { get { return this.registry; } }

        internal LootGenerationResult Generate(ICharacter target, int playfieldId)
        {
            if (target == null) throw new ArgumentNullException("target");
            LootGenerationContext context = this.BuildContext(target, playfieldId);
            try
            {
                this.EnsureDefinitions(target, context);
            }
            catch (LootDefinitionValidationException error)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "Global loot definition rejected: " + error.Message);
            }
            int seed;
            lock (this.productionRandomSync) seed = this.productionRandom.Next();
            context.Seed = seed;
            LootGenerationResult result = this.generator.Generate(context, new SeededLootRandomSource(seed));
            this.EnsureAlexPadCreditsEvenWhenEmpty(context, result, target.Name);
            if (DiagnosticsEnabled())
            {
                LogUtil.Debug(DebugInfoDetail.Engine, string.Format(
                    CultureInfo.InvariantCulture,
                    "GlobalLoot target={0} profile={1} tables={2} assignments={3} items={4} credits={5} unresolved={6}/{7}",
                    target.Identity, context.EnemyProfileKey,
                    string.Join(",", result.AppliedTableKeys), string.Join(",", result.AppliedAssignmentKeys),
                    result.Items.Count, result.Credits, result.LootUnresolved, result.CreditsUnresolved));
            }
            return result;
        }

        private void EnsureAlexPadCreditsEvenWhenEmpty(
            LootGenerationContext context,
            LootGenerationResult result,
            string enemyName)
        {
            if (context == null || result == null || result.Credits > 0)
            {
                return;
            }

            int credits;
            if (CapturedAreteLandingLootDefinitions.TryGetTypicalCredits(enemyName, out credits)
                && credits > 0)
            {
                result.Credits = credits;
                result.CreditsUnresolved = true;
                result.LootUnresolved = true;
                return;
            }

            if (!TryGetAlexPadEmptyCredits(context.MonsterData, out credits))
            {
                return;
            }

            // Capture empty corpses still carried credits and stayed openable.
            result.Credits = credits;
            result.CreditsUnresolved = true;
            result.LootUnresolved = true;
        }

        private static bool TryGetAlexPadEmptyCredits(int monsterData, out int credits)
        {
            if (monsterData == AlexDockerMonsterData)
            {
                credits = AlexDockerCredits;
                return true;
            }

            if (monsterData == AlexWasteMonsterData)
            {
                credits = AlexWasteCredits;
                return true;
            }

            if (monsterData == AlexFleaMonsterData)
            {
                credits = AlexFleaCredits;
                return true;
            }

            if (monsterData == CleaningRobotMonsterData)
            {
                credits = CleaningRobotCredits;
                return true;
            }

            credits = 0;
            return false;
        }

        internal LootGenerationResult GenerateDeterministic(LootGenerationContext context, int seed)
        {
            context.Seed = seed;
            return this.generator.Generate(context, new SeededLootRandomSource(seed));
        }

        private LootGenerationContext BuildContext(ICharacter target, int playfieldId)
        {
            CapturedEncounterRuntimeDefinition encounter;
            bool hasEncounter = CapturedEncounterRuntimeRegistry.TryGet(
                target.Identity.Instance,
                out encounter);
            OrdinaryEnemyRuntimeDefinition ordinary;
            bool hasOrdinary = OrdinaryEnemyRuntimeRegistry.TryGet(target.Identity.Instance, out ordinary);
            bool owned = PetCombatRules.IsPlayerOwnedPet(target);
            int monsterData = target.Stats[StatIds.monsterdata].Value;
            bool isCapturedVergil = !owned && monsterData == CapturedVergilMonsterData;
            return new LootGenerationContext
            {
                EnemyProfileKey = isCapturedVergil
                    ? CapturedVergilProfileKey
                    : hasEncounter
                    ? encounter.ProfileKey
                    : owned ? "owned-summon" : hasOrdinary ? ordinary.Profile.ProfileKey : LegacyProfileKey(target),
                EnemyIdentityInstance = target.Identity.Instance,
                MonsterData = monsterData,
                FamilyKey = isCapturedVergil
                    ? "subway.127.named-boss"
                    : hasEncounter
                    ? "encounter." + encounter.EncounterKey
                    : hasOrdinary ? ordinary.Profile.FamilyKey : "legacy." + target.Stats[StatIds.npcfamily].Value,
                Level = target.Stats[StatIds.level].Value,
                PlayfieldId = playfieldId,
                SpawnKey = hasEncounter ? encounter.SpawnKey : hasOrdinary ? ordinary.Spawn.SpawnKey : null,
                EncounterKey = hasEncounter ? encounter.EncounterKey : null,
                IsBoss = isCapturedVergil || (hasEncounter && encounter.IsBoss),
                IsOwnedSummon = owned && !hasEncounter
            };
        }

        private void EnsureDefinitions(ICharacter target, LootGenerationContext context)
        {
            if (context.IsOwnedSummon) return;
            lock (this.sync)
            {
                if (string.Equals(
                    context.EnemyProfileKey,
                    CapturedVergilProfileKey,
                    StringComparison.Ordinal))
                {
                    this.EnsureCapturedVergil();
                    return;
                }

                CapturedEncounterRuntimeDefinition encounter;
                if (CapturedEncounterRuntimeRegistry.TryGet(
                    target.Identity.Instance,
                    out encounter))
                {
                    this.EnsureCapturedEncounter(encounter);
                    return;
                }

                if (string.Equals(
                    target.Name,
                    "Cleanmeister Intelligence Robot",
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureAreteCleanmeister();
                    context.EnemyProfileKey = AreteCleanmeisterProfileKey;
                    return;
                }

                if (string.Equals(
                    target.Name,
                    "Supreme Collector of Waste",
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureAreteSupremeCollector();
                    context.EnemyProfileKey = AreteSupremeCollectorProfileKey;
                    return;
                }

                if (string.Equals(
                    target.Name,
                    "Malfunctioning Cleaning Robot",
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureAreteMalfunctioningRobot();
                    context.EnemyProfileKey = AreteMalfunctioningRobotProfileKey;
                    return;
                }

                if (context.PlayfieldId == AretePlayfieldId
                    && string.Equals(
                        target.Name,
                        "Gnarl the Roller",
                        StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureAreteGnarl();
                    context.EnemyProfileKey = AreteGnarlProfileKey;
                    return;
                }

                if (context.PlayfieldId == AretePlayfieldId
                    && string.Equals(
                        target.Name,
                        "Kneebreaker Alfonzo Rizzolo",
                        StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureAreteKneebreaker();
                    context.EnemyProfileKey = AreteKneebreakerProfileKey;
                    return;
                }

                if (context.PlayfieldId == AretePlayfieldId
                    && context.MonsterData == AreteRollerratMonsterData
                    && string.Equals(target.Name, "Rollerrat", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureAreteRollerrat();
                    context.EnemyProfileKey = AreteRollerratProfileKey;
                    return;
                }

                if (context.PlayfieldId == AretePlayfieldId
                    && context.MonsterData == AreteDesertReetMonsterData
                    && string.Equals(target.Name, "Desert Reet", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureAreteDesertReet();
                    context.EnemyProfileKey = AreteDesertReetProfileKey;
                    return;
                }

                if (context.PlayfieldId == AretePlayfieldId
                    && context.MonsterData == AreteAngryMinibullMonsterData
                    && string.Equals(target.Name, "Angry Minibull", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureAreteAngryMinibull();
                    context.EnemyProfileKey = AreteAngryMinibullProfileKey;
                    return;
                }

                // Arete Landing: Shiny Sword 297289 QL1 @ 6.2% on all PF 6553 kills
                // (Independent Global; stacks with name/MonsterData corpse tables).
                if (context.PlayfieldId == CapturedAreteLandingLootDefinitions.AreteLandingPlayfieldId)
                {
                    this.EnsureAreteShinySwordIndependent();
                }

                // Arete Landing part 1/2: exact-name observed corpse snapshots (items + credits).
                if (context.PlayfieldId == CapturedAreteLandingLootDefinitions.AreteLandingPlayfieldId)
                {
                    string areteProfileKey;
                    if (CapturedAreteLandingLootDefinitions.TryRegister(
                        this.registry,
                        target.Name,
                        out areteProfileKey))
                    {
                        context.EnemyProfileKey = areteProfileKey;
                        return;
                    }
                }

                // Legacy Alex-pad MonsterData tables — Arete PF only (shared MD must not clobber Subway).
                if (context.PlayfieldId == CapturedAreteLandingLootDefinitions.AreteLandingPlayfieldId
                    && context.MonsterData == AlexDockerMonsterData)
                {
                    this.EnsureAlexDocker();
                    context.EnemyProfileKey = AlexDockerProfileKey;
                    return;
                }

                if (context.PlayfieldId == CapturedAreteLandingLootDefinitions.AreteLandingPlayfieldId
                    && context.MonsterData == AlexWasteMonsterData)
                {
                    this.EnsureAlexWasteCollector();
                    context.EnemyProfileKey = AlexWasteProfileKey;
                    return;
                }

                if (context.PlayfieldId == CapturedAreteLandingLootDefinitions.AreteLandingPlayfieldId
                    && context.MonsterData == AlexFleaMonsterData)
                {
                    this.EnsureAlexGarbageFlea();
                    context.EnemyProfileKey = AlexFleaProfileKey;
                    return;
                }

                if (context.PlayfieldId == CapturedAreteLandingLootDefinitions.AreteLandingPlayfieldId
                    && context.MonsterData == CleaningRobotMonsterData)
                {
                    this.EnsureCleaningRobot();
                    context.EnemyProfileKey = CleaningRobotProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Barking Chimera", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceBarkingChimera();
                    context.EnemyProfileKey = NascenceChimeraProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceEmptyAnimalLoot(
                        "captured.nascence.yuttos",
                        NascenceYuttosProfileKey,
                        "Yuttos captured empty corpse",
                        "capture.20260723-221330.yuttos.empty");
                    context.EnemyProfileKey = NascenceYuttosProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Dreaming Silvertail", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceEmptyAnimalLoot(
                        "captured.nascence.dreaming-silvertail",
                        NascenceDreamingSilvertailProfileKey,
                        "Dreaming Silvertail captured empty corpse",
                        "capture.20260723-221330.dreaming.empty");
                    context.EnemyProfileKey = NascenceDreamingSilvertailProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Swift Silvertail", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceSwiftSilvertail();
                    context.EnemyProfileKey = NascenceSwiftProfileKey;
                    return;
                }

                OrdinaryEnemyRuntimeDefinition ordinary;
                if (OrdinaryEnemyRuntimeRegistry.TryGet(target.Identity.Instance, out ordinary))
                {
                    this.EnsureOrdinary(ordinary.Profile, context.Level);
                    return;
                }

                this.EnsureDatabaseLoaded();
                this.EnsureLegacyTarget(target, context.EnemyProfileKey);
            }
        }

        private void EnsureCapturedEncounter(CapturedEncounterRuntimeDefinition encounter)
        {
            if (CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                    this.registry,
                    encounter.ProfileKey,
                    encounter.EncounterKey))
            {
                return;
            }

            string tableKey = "captured." + encounter.ProfileKey;
            if (this.registry.ContainsTable(tableKey)) return;

            bool isAbmouth = encounter.IsBoss
                && string.Equals(
                    encounter.ProfileKey,
                    AbmouthEncounterRuntimeService.AbmouthProfileKey,
                    StringComparison.Ordinal);
            bool isInfector = string.Equals(
                encounter.ProfileKey,
                AbmouthEncounterRuntimeService.InfectorProfileKey,
                StringComparison.Ordinal);
            bool isEumenides = string.Equals(
                encounter.ProfileKey,
                CapturedSubwayEncounterRuntimeService.EumenidesProfileKey,
                StringComparison.Ordinal);
            bool isStrikeForeman = string.Equals(
                encounter.ProfileKey,
                CapturedSubwayEncounterRuntimeService.StrikeForemanProfileKey,
                StringComparison.Ordinal);
            if (!isAbmouth && !isInfector && !isEumenides && !isStrikeForeman) return;

            ObservedCorpseSnapshotDefinition[] snapshots = isAbmouth
                ? new[]
                {
                    ObservedCorpseSnapshot(
                        CapturedAbmouthLootEvidence,
                        "capture.20260712-232137",
                        CapturedAbmouthCredits,
                        ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, "capture.20260712-232137", 136622, 136623, 30, 1),
                        ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, "capture.20260712-232137", 202717, 202718, 28, 1),
                        ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, "capture.20260712-232137", 107933, 107934, 23, 1),
                        ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, "capture.20260712-232137", 85693, 27389, 30, 1),
                        ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, "capture.20260712-232137", 287146, 287146, 200, 1)),
                    ObservedCorpseSnapshot(
                        CapturedAbmouthLootEvidence,
                        "capture.20260716-220400",
                        CapturedAbmouthCredits,
                        ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, "capture.20260716-220400", 202741, 202742, 32, 1),
                        ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, "capture.20260716-220400", 202734, 202735, 32, 1),
                        ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, "capture.20260716-220400", 202717, 202718, 32, 1),
                        ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, "capture.20260716-220400", 85723, 85722, 32, 1),
                        ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, "capture.20260716-220400", 123968, 123970, 25, 1),
                        ObservedCorpseSnapshotEntry(CapturedAbmouthLootEvidence, "capture.20260716-220400", 287146, 287146, 200, 1))
                }
                : isEumenides
                ? new[]
                {
                    ObservedCorpseSnapshot(
                        CapturedEumenidesLootEvidence,
                        "capture.20260717-214751",
                        CapturedEumenidesCredits,
                        ObservedCorpseSnapshotEntry(CapturedEumenidesLootEvidence, "capture.20260717-214751", 163430, 163431, 22, 1),
                        ObservedCorpseSnapshotEntry(CapturedEumenidesLootEvidence, "capture.20260717-214751", 301714, 301714, 1, 1),
                        ObservedCorpseSnapshotEntry(CapturedEumenidesLootEvidence, "capture.20260717-214751", 287146, 287146, 200, 1)),
                    ObservedCorpseSnapshot(
                        CapturedEumenidesLootEvidence,
                        "capture.20260717-215250",
                        CapturedEumenidesCredits,
                        ObservedCorpseSnapshotEntry(CapturedEumenidesLootEvidence, "capture.20260717-215250", 301715, 301715, 1, 1),
                        ObservedCorpseSnapshotEntry(CapturedEumenidesLootEvidence, "capture.20260717-215250", 160051, 160050, 16, 1),
                        ObservedCorpseSnapshotEntry(CapturedEumenidesLootEvidence, "capture.20260717-215250", 287146, 287146, 200, 1))
                }
                : isStrikeForeman
                ? new[]
                {
                    ObservedCorpseSnapshot(
                        CapturedStrikeForemanLootEvidence,
                        "capture.20260720-032106",
                        CapturedStrikeForemanCredits,
                        LevelBoundedObservedCorpseSnapshotEntry(
                            CapturedStrikeForemanLootEvidence,
                            "capture.20260720-032106",
                            27199,
                            27199,
                            10,
                            1),
                        LevelBoundedObservedCorpseSnapshotEntry(
                            CapturedStrikeForemanLootEvidence,
                            "capture.20260720-032106",
                            123744,
                            123745,
                            20,
                            1),
                        LevelBoundedObservedCorpseSnapshotEntry(
                            CapturedStrikeForemanLootEvidence,
                            "capture.20260720-032106",
                            301713,
                            301713,
                            1,
                            1)),
                    ObservedCorpseSnapshot(
                        CapturedStrikeForemanLootEvidence,
                        "capture.20260720-033513",
                        CapturedStrikeForemanCredits,
                        LevelBoundedObservedCorpseSnapshotEntry(
                            CapturedStrikeForemanLootEvidence,
                            "capture.20260720-033513",
                            85676,
                            22072,
                            15,
                            1),
                        LevelBoundedObservedCorpseSnapshotEntry(
                            CapturedStrikeForemanLootEvidence,
                            "capture.20260720-033513",
                            301707,
                            301707,
                            1,
                            1))
                }
                : new ObservedCorpseSnapshotDefinition[0];
            var table = new LootTableDefinition
            {
                LootTableKey = tableKey,
                DisplayName = encounter.DisplayName + " captured corpse",
                TableType = isAbmouth ? LootTableType.Boss : LootTableType.EnemyType,
                RollGroups = new LootGroupDefinition[0],
                ObservedCorpseSnapshots = snapshots,
                CreditsPolicy = isAbmouth || isEumenides || isStrikeForeman
                    ? new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.Unresolved,
                        Evidence = LootEvidenceConfidence.Unresolved
                    }
                    : CreditsRange(
                        CapturedInfectorCredits,
                        CapturedInfectorCredits,
                        LootEvidenceConfidence.ProvenCapture),
                QualityPolicy = isAbmouth || isEumenides || isStrikeForeman
                    ? isStrikeForeman
                      ? "captured-atomic-membership-enemy-level-bounded-item-ql"
                      : "captured-observed-corpse-snapshots"
                    : "unresolved",
                Evidence = isAbmouth
                    ? CapturedAbmouthLootEvidence
                    : isEumenides
                    ? CapturedEumenidesLootEvidence
                    : isStrikeForeman
                    ? CapturedStrikeForemanLootEvidence
                    : encounter.Evidence + "; item pool unresolved",
                Confidence = isAbmouth || isEumenides || isStrikeForeman
                    ? LootEvidenceConfidence.ObservedAvailableLoot
                    : LootEvidenceConfidence.Unresolved,
                ItemPoolUnresolved = true,
                Enabled = true
            };
            this.registry.RegisterTable(table);
            this.registry.RegisterAssignment(new LootAssignmentDefinition
            {
                AssignmentKey = tableKey,
                TargetType = isAbmouth
                    ? LootAssignmentTargetType.Boss
                    : LootAssignmentTargetType.EnemyType,
                TargetKey = encounter.ProfileKey,
                LootTableKey = tableKey,
                PlayfieldId = AbmouthEncounterRuntimeService.SubwayPlayfieldId,
                EncounterKey = encounter.EncounterKey,
                Priority = 0,
                Conditions = new string[0],
                Evidence = table.Evidence,
                Confidence = table.Confidence,
                Enabled = true
            });
        }

        private void EnsureCapturedVergil()
        {
            string tableKey = "captured." + CapturedVergilProfileKey;
            if (this.registry.ContainsTable(tableKey)) return;

            this.registry.RegisterTable(BuildCapturedVergilLootTable());
            this.registry.RegisterAssignment(new LootAssignmentDefinition
            {
                AssignmentKey = tableKey,
                TargetType = LootAssignmentTargetType.Boss,
                TargetKey = CapturedVergilProfileKey,
                LootTableKey = tableKey,
                PlayfieldId = NpcCombatAttackRules.CapturedSubwayPlayfield,
                Priority = 0,
                Conditions = new string[0],
                Evidence = CapturedVergilLootEvidence,
                Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                Enabled = true
            });
        }

        internal static LootTableDefinition BuildCapturedVergilLootTable()
        {
            string tableKey = "captured." + CapturedVergilProfileKey;
            return new LootTableDefinition
            {
                LootTableKey = tableKey,
                DisplayName = "Vergil Aeneid captured corpse snapshots",
                TableType = LootTableType.Boss,
                RollGroups = new LootGroupDefinition[0],
                ObservedCorpseSnapshots = new[]
                {
                    ObservedCorpseSnapshot(
                        "capture.20260712-232711",
                        610,
                        ObservedCorpseSnapshotEntry("capture.20260712-232711", 301713, 301713, 1, 1),
                        ObservedCorpseSnapshotEntry("capture.20260712-232711", 202743, 202744, 32, 1),
                        ObservedCorpseSnapshotEntry("capture.20260712-232711", 287146, 287146, 200, 1)),
                    ObservedCorpseSnapshot(
                        "capture.20260712-234401",
                        587,
                        ObservedCorpseSnapshotEntry("capture.20260712-234401", 301714, 301714, 1, 1),
                        ObservedCorpseSnapshotEntry("capture.20260712-234401", 123571, 123572, 23, 1),
                        ObservedCorpseSnapshotEntry("capture.20260712-234401", 287146, 287146, 200, 1)),
                    ObservedCorpseSnapshot(
                        "capture.20260716-034433",
                        563,
                        ObservedCorpseSnapshotEntry("capture.20260716-034433", 202734, 202735, 33, 1),
                        ObservedCorpseSnapshotEntry("capture.20260716-034433", 301715, 301715, 1, 1),
                        ObservedCorpseSnapshotEntry("capture.20260716-034433", 160051, 160050, 24, 1),
                        ObservedCorpseSnapshotEntry("capture.20260716-034433", 21605, 21605, 1, 100),
                        ObservedCorpseSnapshotEntry("capture.20260716-034433", 287146, 287146, 200, 1))
                },
                CreditsPolicy = new CreditsPolicyDefinition
                {
                    Mode = CreditsPolicyMode.Unresolved,
                    Evidence = LootEvidenceConfidence.Unresolved
                },
                QualityPolicy = "captured-observed-corpse-snapshots",
                Evidence = CapturedVergilLootEvidence,
                Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                ItemPoolUnresolved = true,
                Enabled = true
            };
        }

        private static ObservedCorpseSnapshotDefinition ObservedCorpseSnapshot(
            string snapshotKey,
            int credits,
            params LootEntryDefinition[] entries)
        {
            return ObservedCorpseSnapshot(
                CapturedVergilLootEvidence,
                snapshotKey,
                credits,
                entries);
        }

        private static ObservedCorpseSnapshotDefinition ObservedCorpseSnapshot(
            string evidence,
            string snapshotKey,
            int credits,
            params LootEntryDefinition[] entries)
        {
            return new ObservedCorpseSnapshotDefinition
            {
                SnapshotKey = snapshotKey,
                Credits = credits,
                Entries = entries ?? new LootEntryDefinition[0],
                Evidence = LootEvidenceConfidence.ProvenCapture,
                SelectionProbabilityEvidence = LootEvidenceConfidence.Unresolved,
                EvidenceReference = evidence + "; " + snapshotKey
            };
        }

        private static LootEntryDefinition ObservedCorpseSnapshotEntry(
            string snapshotKey,
            int itemTemplateId,
            int highItemTemplateId,
            int quality,
            int quantity)
        {
            return ObservedCorpseSnapshotEntry(
                CapturedVergilLootEvidence,
                snapshotKey,
                itemTemplateId,
                highItemTemplateId,
                quality,
                quantity);
        }

        private static LootEntryDefinition ObservedCorpseSnapshotEntry(
            string evidence,
            string snapshotKey,
            int itemTemplateId,
            int highItemTemplateId,
            int quality,
            int quantity)
        {
            return new LootEntryDefinition
            {
                SelectionKey = snapshotKey,
                ItemTemplateId = itemTemplateId,
                HighItemTemplateId = highItemTemplateId,
                FixedQuality = quality,
                MinimumQuality = quality,
                MaximumQuality = quality,
                MinimumQuantity = quantity,
                MaximumQuantity = quantity,
                Weight = 0,
                DropChanceBasisPoints = 0,
                UniquePerCorpse = true,
                Semantics = LootSemantics.ObservedAvailable,
                Evidence = LootEvidenceConfidence.ObservedAvailableLoot,
                EvidenceReference = evidence + "; " + snapshotKey,
                ProbabilityEvidence = "unresolved"
            };
        }

        private static LootEntryDefinition LevelBoundedObservedCorpseSnapshotEntry(
            string evidence,
            string snapshotKey,
            int itemTemplateId,
            int highItemTemplateId,
            int observedQuality,
            int quantity)
        {
            if (!ItemLoader.ItemList.ContainsKey(itemTemplateId)
                || !ItemLoader.ItemList.ContainsKey(highItemTemplateId))
            {
                throw new LootDefinitionValidationException(
                    "Level-bounded loot template is unavailable: "
                    + itemTemplateId
                    + "/"
                    + highItemTemplateId);
            }

            int lowTemplateQuality = ItemLoader.ItemList[itemTemplateId].Quality;
            int highTemplateQuality = ItemLoader.ItemList[highItemTemplateId].Quality;
            return new LootEntryDefinition
            {
                SelectionKey = snapshotKey,
                ItemTemplateId = itemTemplateId,
                HighItemTemplateId = highItemTemplateId,
                UsesEnemyLevelQuality = true,
                MinimumQuality = Math.Min(lowTemplateQuality, highTemplateQuality),
                MaximumQuality = Math.Max(lowTemplateQuality, highTemplateQuality),
                MinimumQuantity = quantity,
                MaximumQuantity = quantity,
                Weight = 0,
                DropChanceBasisPoints = 0,
                UniquePerCorpse = true,
                Semantics = LootSemantics.ObservedAvailable,
                Evidence = LootEvidenceConfidence.ObservedAvailableLoot,
                EvidenceReference = evidence
                                    + "; "
                                    + snapshotKey
                                    + "; observed QL"
                                    + observedQuality.ToString(CultureInfo.InvariantCulture),
                ProbabilityEvidence = "unresolved"
            };
        }

        private void EnsureOrdinary(OrdinaryEnemyProfile profile, int targetLevel)
        {
            bool levelSpecificCredits = profile.Loot.LevelCreditRules.Length > 0;
            string levelSuffix = levelSpecificCredits ? ".level." + targetLevel.ToString(CultureInfo.InvariantCulture) : string.Empty;
            string tableKey = "ordinary." + profile.ProfileKey + levelSuffix;
            string assignmentKey = "ordinary." + profile.ProfileKey + levelSuffix;
            if (this.registry.ContainsTable(tableKey)) return;
            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                profile,
                targetLevel,
                tableKey,
                assignmentKey);
            this.registry.RegisterTableAndAssignment(adapted.Table, adapted.Assignment);
        }

        private void EnsureAlexDocker()
        {
            const string tableKey = "captured.arete.alex-32v-docker";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260722-cap-mob-drop-cred: credits always 4; empty or 248307.
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(AlexPadLootEvidence, "capture.20260722.docker.empty-a", AlexDockerCredits),
                    ObservedCorpseSnapshot(AlexPadLootEvidence, "capture.20260722.docker.empty-b", AlexDockerCredits),
                    ObservedCorpseSnapshot(AlexPadLootEvidence, "capture.20260722.docker.empty-c", AlexDockerCredits),
                    ObservedCorpseSnapshot(AlexPadLootEvidence, "capture.20260722.docker.empty-d", AlexDockerCredits),
                    ObservedCorpseSnapshot(
                        AlexPadLootEvidence,
                        "capture.20260722.docker.a",
                        AlexDockerCredits,
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.docker.a", 248307, 248307, 1, 1)),
                    ObservedCorpseSnapshot(
                        AlexPadLootEvidence,
                        "capture.20260722.docker.b",
                        AlexDockerCredits,
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.docker.b", 248307, 248307, 1, 1)),
                    ObservedCorpseSnapshot(
                        AretePartTwoLootEvidence,
                        "capture.20260722-152454.docker.798914DC",
                        AlexDockerCredits,
                        ObservedCorpseSnapshotEntry(
                            AretePartTwoLootEvidence,
                            "capture.20260722-152454.docker.798914DC",
                            248318,
                            248318,
                            1,
                            1))
                };

            this.RegisterAlexPadTable(
                tableKey,
                "32-V Docker captured corpse",
                AlexDockerProfileKey,
                snapshots);
        }

        private void EnsureAlexWasteCollector()
        {
            const string tableKey = "captured.arete.alex-waste-collector";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260722-cap-mob-drop-cred: credits=11; items 248315/248319/248334/42620/70564…
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(AlexPadLootEvidence, "capture.20260722.waste.empty", AlexWasteCredits),
                    ObservedCorpseSnapshot(
                        AlexPadLootEvidence,
                        "capture.20260722.waste.a",
                        AlexWasteCredits,
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.waste.a", 248334, 248334, 1, 1)),
                    ObservedCorpseSnapshot(
                        AlexPadLootEvidence,
                        "capture.20260722.waste.b",
                        AlexWasteCredits,
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.waste.b", 248315, 248315, 1, 1),
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.waste.b", 42620, 42619, 2, 1)),
                    ObservedCorpseSnapshot(
                        AlexPadLootEvidence,
                        "capture.20260722.waste.c",
                        AlexWasteCredits,
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.waste.c", 248319, 248319, 1, 1)),
                    ObservedCorpseSnapshot(
                        AlexPadLootEvidence,
                        "capture.20260722.waste.d",
                        AlexWasteCredits,
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.waste.d", 42620, 42619, 2, 1)),
                    ObservedCorpseSnapshot(
                        AlexPadLootEvidence,
                        "capture.20260722.waste.e",
                        AlexWasteCredits,
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.waste.e", 248315, 248315, 1, 1),
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.waste.e", 70564, 85515, 2, 1),
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.waste.e", 42620, 42619, 2, 1)),
                    ObservedCorpseSnapshot(
                        AretePartTwoLootEvidence,
                        "capture.20260722-152454.waste.798913CD",
                        AlexWasteCredits,
                        ObservedCorpseSnapshotEntry(
                            AretePartTwoLootEvidence,
                            "capture.20260722-152454.waste.798913CD",
                            248315,
                            248315,
                            1,
                            1),
                        ObservedCorpseSnapshotEntry(
                            AretePartTwoLootEvidence,
                            "capture.20260722-152454.waste.798913CD",
                            70558,
                            85640,
                            2,
                            1)),
                    ObservedCorpseSnapshot(
                        AretePartTwoLootEvidence,
                        "capture.20260722-152454.waste.79891536",
                        AlexWasteCredits,
                        ObservedCorpseSnapshotEntry(
                            AretePartTwoLootEvidence,
                            "capture.20260722-152454.waste.79891536",
                            248334,
                            248334,
                            1,
                            1),
                        ObservedCorpseSnapshotEntry(
                            AretePartTwoLootEvidence,
                            "capture.20260722-152454.waste.79891536",
                            248319,
                            248319,
                            1,
                            1),
                        ObservedCorpseSnapshotEntry(
                            AretePartTwoLootEvidence,
                            "capture.20260722-152454.waste.79891536",
                            42620,
                            42619,
                            2,
                            1))
                };

            this.RegisterAlexPadTable(
                tableKey,
                "Waste Collector captured corpse",
                AlexWasteProfileKey,
                snapshots);
        }

        private void EnsureAreteShinySwordIndependent()
        {
            const string tableKey = "arete.landing.shiny-sword.independent";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Mike loot table / capture-backed rate: Shiny Sword 297289 QL1 @ 6.2% (620 bp).
            const string evidence =
                "Mike Arete loot table; Shiny Sword 297289 QL1 6.2%; also observed Waste Collector corpse in arete-part-1";
            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = "Arete Landing Shiny Sword independent",
                    TableType = LootTableType.GlobalDefault,
                    RollGroups =
                        new[]
                        {
                            new LootGroupDefinition
                            {
                                LootGroupKey = "shiny-sword",
                                RollMode = LootRollMode.Independent,
                                RollCount = 1,
                                EmptyWeight = 0,
                                DropChanceBasisPoints = 10000,
                                Entries =
                                    new[]
                                    {
                                        new LootEntryDefinition
                                        {
                                            SelectionKey = "shiny-sword-297289",
                                            ItemTemplateId = 297289,
                                            HighItemTemplateId = 297289,
                                            FixedQuality = 1,
                                            MinimumQuality = 1,
                                            MaximumQuality = 1,
                                            MinimumQuantity = 1,
                                            MaximumQuantity = 1,
                                            Weight = 1,
                                            DropChanceBasisPoints = 620,
                                            Semantics = LootSemantics.WeightedDocumented,
                                            Evidence = LootEvidenceConfidence.ObservedAvailableLoot,
                                            EvidenceReference = evidence
                                        }
                                    },
                                Conditions = new string[0]
                            }
                        },
                    ObservedCorpseSnapshots = new ObservedCorpseSnapshotDefinition[0],
                    CreditsPolicy = new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.None,
                        Evidence = LootEvidenceConfidence.ProvenCapture
                    },
                    QualityPolicy = "fixed-ql1",
                    Evidence = evidence,
                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                    ItemPoolUnresolved = false,
                    Enabled = true
                });
            this.registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.Global,
                    TargetKey = null,
                    LootTableKey = tableKey,
                    PlayfieldId = CapturedAreteLandingLootDefinitions.AreteLandingPlayfieldId,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = evidence,
                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                    Enabled = true
                });
        }

        private void EnsureAlexGarbageFlea()
        {
            const string tableKey = "captured.arete.alex-garbage-flea";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(AlexPadLootEvidence, "capture.20260722.flea.empty-5a", 5),
                    ObservedCorpseSnapshot(AlexPadLootEvidence, "capture.20260722.flea.empty-5b", 5),
                    ObservedCorpseSnapshot(AlexPadLootEvidence, "capture.20260722.flea.empty-5c", 5),
                    ObservedCorpseSnapshot(AlexPadLootEvidence, "capture.20260722.flea.empty-11", 11),
                    ObservedCorpseSnapshot(
                        AlexPadLootEvidence,
                        "capture.20260722.flea.a",
                        5,
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.flea.a", 248322, 248322, 1, 1)),
                    ObservedCorpseSnapshot(
                        AlexPadLootEvidence,
                        "capture.20260722.flea.b",
                        5,
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.flea.b", 70560, 70560, 1, 1),
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.flea.b", 248322, 248322, 1, 1))
                };

            this.RegisterAlexPadTable(
                tableKey,
                "Garbage Flea captured corpse",
                AlexFleaProfileKey,
                snapshots);
        }

        private void EnsureAreteCleanmeister()
        {
            if (this.registry.ContainsTable(AreteCleanmeisterProfileKey))
            {
                return;
            }

            const string first = "capture.20260722-104809.cleanmeister.7988C930";
            const string second = "capture.20260722-104809.cleanmeister.7988CAD3";
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        AretePartOneLootEvidence,
                        first,
                        17,
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 42620, 42619, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 85517, 27360, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 85740, 85739, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 123514, 123515, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 154069, 150213, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 161609, 161610, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 162736, 162736, 7, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 206656, 206657, 2, 1)),
                    ObservedCorpseSnapshot(
                        AretePartOneLootEvidence,
                        second,
                        17,
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 85693, 27389, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 135719, 135719, 1, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 123789, 123790, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 129064, 129065, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 160338, 160339, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 162736, 162736, 7, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 201045, 201046, 2, 1)),
                    ObservedCorpseSnapshot(
                        AretePartTwoLootEvidence,
                        "capture.20260722-152454.cleanmeister.798915E0",
                        17,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.cleanmeister.798915E0", 85691, 22004, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.cleanmeister.798915E0", 70562, 85597, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.cleanmeister.798915E0", 122981, 122982, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.cleanmeister.798915E0", 130042, 130043, 2, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.cleanmeister.798915E0", 160604, 160604, 20, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.cleanmeister.798915E0", 162736, 162736, 7, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.cleanmeister.798915E0", 201072, 201073, 2, 1))
                };

            this.RegisterObservedAreteVariantTable(
                AreteCleanmeisterProfileKey,
                "Cleanmeister Intelligence Robot captured corpses",
                snapshots);
        }

        private void EnsureAreteSupremeCollector()
        {
            if (this.registry.ContainsTable(AreteSupremeCollectorProfileKey))
            {
                return;
            }

            const string first = "capture.20260722-104809.supreme-collector.79882C8F";
            const string second = "capture.20260722-104809.supreme-collector.7988CB09";
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        AretePartOneLootEvidence,
                        first,
                        35,
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 42620, 42619, 4, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 123038, 123039, 5, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 160216, 160217, 5, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 160603, 160603, 20, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, first, 160704, 160704, 24, 1)),
                    ObservedCorpseSnapshot(
                        AretePartOneLootEvidence,
                        second,
                        35,
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 85761, 85760, 3, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 85533, 85532, 3, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 124383, 124384, 3, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 121629, 121630, 3, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 160603, 160603, 20, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 162736, 162736, 7, 1),
                        ObservedCorpseSnapshotEntry(AretePartOneLootEvidence, second, 201068, 201069, 3, 1)),
                    ObservedCorpseSnapshot(
                        AretePartTwoLootEvidence,
                        "capture.20260722-152454.supreme-collector.7989146B",
                        35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.supreme-collector.7989146B", 70565, 85514, 3, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.supreme-collector.7989146B", 135719, 135719, 1, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.supreme-collector.7989146B", 124391, 124392, 3, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.supreme-collector.7989146B", 153083, 153084, 3, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.supreme-collector.7989146B", 160736, 160737, 3, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.supreme-collector.7989146B", 162736, 162736, 7, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.supreme-collector.7989146B", 201076, 201077, 3, 1))
                };

            this.RegisterObservedAreteVariantTable(
                AreteSupremeCollectorProfileKey,
                "Supreme Collector of Waste captured corpses",
                snapshots);
        }

        private void EnsureAreteMalfunctioningRobot()
        {
            if (this.registry.ContainsTable(AreteMalfunctioningRobotProfileKey))
            {
                return;
            }

            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        AretePartOneLootEvidence,
                        "capture.20260722-104809.malfunctioning-robot.7988C9B9",
                        5),
                    ObservedCorpseSnapshot(
                        AretePartOneLootEvidence,
                        "capture.20260722-104809.malfunctioning-robot.7988C9BF",
                        5)
                };

            this.RegisterObservedAreteVariantTable(
                AreteMalfunctioningRobotProfileKey,
                "Malfunctioning Cleaning Robot captured corpses",
                snapshots);
        }

        private void EnsureAreteRollerrat()
        {
            if (this.registry.ContainsTable(AreteRollerratProfileKey))
            {
                return;
            }

            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.798915D0", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.798915D0", 248333, 248333, 1, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891513", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891513", 70560, 85688, 7, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7988CFD2", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7988CFD2", 70561, 85744, 7, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7988CFD0", 29,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7988CFD0", 84150, 84149, 4, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7988CFCF", 29),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79882AEC", 29),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7989160C", 29),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891606", 35),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.798915FF", 35),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7988CFD3", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7988CFD3", 248333, 248333, 1, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891150", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891150", 70559, 85689, 7, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.798912B7", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.798912B7", 84158, 84157, 6, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.798912B7", 248333, 248333, 1, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.798912B5", 29),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.798912B8", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.798912B8", 70559, 85689, 5, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.798912B8", 248333, 248333, 1, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7988CFE0", 35),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7988CD22", 35),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7989164E", 35),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891665", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891665", 70558, 85640, 6, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891665", 248333, 248333, 1, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891663", 29,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891663", 248333, 248333, 1, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891636", 29,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891636", 248333, 248333, 1, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7989163B", 29),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891714", 29),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7989162F", 35),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.79891611", 35),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7988CA3F", 35),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7989165A", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.rollerrat.7989165A", 70559, 85689, 7, 1))
                };

            this.RegisterObservedAretePartTwoTable(
                AreteRollerratProfileKey,
                "Arete Rollerrat captured corpses",
                snapshots);
        }

        private void EnsureAreteDesertReet()
        {
            if (this.registry.ContainsTable(AreteDesertReetProfileKey))
            {
                return;
            }

            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.798828FB", 29,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.798828FB", 42640, 42641, 5, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.798828EE", 29),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.798828E7", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.798828E7", 70561, 85744, 6, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.798828E7", 42640, 42641, 7, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.798828F0", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.798828F0", 42640, 42641, 5, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.798828E4", 29,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.798828E4", 42640, 42641, 6, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.798767DE", 29),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.7989160F", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.7989160F", 42640, 42641, 6, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.7989161C", 35,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.7989161C", 248328, 248328, 1, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.7989161C", 70558, 85640, 5, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.7989161C", 42640, 42641, 5, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.7989162B", 29,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.desert-reet.7989162B", 248328, 248328, 1, 1))
                };

            this.RegisterObservedAretePartTwoTable(
                AreteDesertReetProfileKey,
                "Arete Desert Reet captured corpses",
                snapshots);
        }

        private void EnsureAreteAngryMinibull()
        {
            if (this.registry.ContainsTable(AreteAngryMinibullProfileKey))
            {
                return;
            }

            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.79891740", 79),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.79891749", 59,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.79891749", 248310, 248310, 1, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.7989174A", 72,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.7989174A", 248325, 248325, 1, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.7989175A", 53,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.7989175A", 84156, 84155, 7, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.79891770", 47),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.79891779", 47,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.79891779", 248330, 248330, 1, 2)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.798915D5", 47,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.798915D5", 248310, 248310, 1, 1)),
                    ObservedCorpseSnapshot(AretePartTwoLootEvidence, "capture.20260722-152454.angry-minibull.79891763", 79)
                };

            this.RegisterObservedAretePartTwoTable(
                AreteAngryMinibullProfileKey,
                "Arete Angry Minibull captured corpses",
                snapshots);
        }

        private void EnsureAreteGnarl()
        {
            if (this.registry.ContainsTable(AreteGnarlProfileKey))
            {
                return;
            }

            const string key = "capture.20260722-152454.gnarl.79891671";
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        AretePartTwoLootEvidence,
                        key,
                        62,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, key, 85750, 85749, 6, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, key, 85512, 85511, 6, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, key, 124106, 124107, 6, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, key, 122064, 122065, 6, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, key, 160103, 160104, 6, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, key, 162736, 162736, 7, 1),
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, key, 201087, 201088, 6, 1))
                };

            this.RegisterObservedAretePartTwoTable(
                AreteGnarlProfileKey,
                "Gnarl the Roller captured corpse",
                snapshots);
        }

        private void EnsureAreteKneebreaker()
        {
            if (this.registry.ContainsTable(AreteKneebreakerProfileKey))
            {
                return;
            }

            const string key = "capture.20260722-152454.kneebreaker.7989147B";
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        AretePartTwoLootEvidence,
                        key,
                        23,
                        ObservedCorpseSnapshotEntry(AretePartTwoLootEvidence, key, 70561, 85744, 4, 1))
                };

            this.RegisterObservedAretePartTwoTable(
                AreteKneebreakerProfileKey,
                "Kneebreaker Alfonzo Rizzolo captured corpse",
                snapshots);
        }

        private void RegisterObservedAretePartTwoTable(
            string tableKey,
            string displayName,
            ObservedCorpseSnapshotDefinition[] snapshots)
        {
            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = displayName,
                    TableType = LootTableType.EnemyType,
                    RollGroups = new LootGroupDefinition[0],
                    ObservedCorpseSnapshots = snapshots,
                    CreditsPolicy = new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.Unresolved,
                        Evidence = LootEvidenceConfidence.Unresolved
                    },
                    QualityPolicy = "captured-observed-corpse-snapshots",
                    Evidence = AretePartTwoLootEvidence,
                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                    ItemPoolUnresolved = true,
                    Enabled = true
                });
            this.registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.EnemyType,
                    TargetKey = tableKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = AretePartTwoLootEvidence,
                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                    Enabled = true
                });
        }

        private void RegisterObservedAreteVariantTable(
            string tableKey,
            string displayName,
            ObservedCorpseSnapshotDefinition[] snapshots)
        {
            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = displayName,
                    TableType = LootTableType.EnemyType,
                    RollGroups = new LootGroupDefinition[0],
                    ObservedCorpseSnapshots = snapshots,
                    CreditsPolicy = new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.Unresolved,
                        Evidence = LootEvidenceConfidence.Unresolved
                    },
                    QualityPolicy = "captured-observed-corpse-snapshots",
                    Evidence = AretePartOneLootEvidence,
                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                    ItemPoolUnresolved = true,
                    Enabled = true
                });
            this.registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.EnemyType,
                    TargetKey = tableKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = AretePartOneLootEvidence,
                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                    Enabled = true
                });
        }

        private void RegisterAlexPadTable(
            string tableKey,
            string displayName,
            string profileKey,
            ObservedCorpseSnapshotDefinition[] snapshots)
        {
            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = displayName,
                    TableType = LootTableType.EnemyType,
                    RollGroups = new LootGroupDefinition[0],
                    ObservedCorpseSnapshots = snapshots,
                    CreditsPolicy = new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.Unresolved,
                        Evidence = LootEvidenceConfidence.Unresolved
                    },
                    QualityPolicy = "captured-observed-corpse-snapshots",
                    Evidence = AlexPadLootEvidence,
                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                    ItemPoolUnresolved = true,
                    Enabled = true
                });
            this.registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.EnemyType,
                    TargetKey = profileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = AlexPadLootEvidence,
                    Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                    Enabled = true
                });
        }

        private void EnsureCleaningRobot()
        {
            const string tableKey = "captured.arete.cleaning-robot";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260722-cap-mob-drop-cred: credits=5; empty / 42620 / misc.
            int[][] outcomes =
                {
                    new[] { 42620 }, new int[0], new[] { 42620 }, new int[0],
                    new[] { 155666, 70560, 42620 }, new[] { 84148 }, new[] { 36783 },
                    new int[0], new[] { 42620 }, new int[0]
                };
            var entries = new List<LootEntryDefinition>();
            int emptyWeight = 0;
            for (int index = 0; index < outcomes.Length; index++)
            {
                if (outcomes[index].Length == 0)
                {
                    emptyWeight++;
                    continue;
                }

                foreach (int itemId in outcomes[index])
                {
                    entries.Add(FixedEntry(itemId, 1, "outcome." + index, 1));
                }
            }

            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = "Cleaning Robot captured outcomes",
                    TableType = LootTableType.EnemyType,
                    RollGroups =
                        new[]
                        {
                            new LootGroupDefinition
                            {
                                LootGroupKey = "captured-outcome",
                                RollMode = LootRollMode.WeightedOne,
                                RollCount = 1,
                                EmptyWeight = emptyWeight,
                                DropChanceBasisPoints = 10000,
                                Entries = entries.ToArray(),
                                Conditions = new string[0]
                            }
                        },
                    CreditsPolicy = CreditsRange(
                        CleaningRobotCredits,
                        CleaningRobotCredits,
                        LootEvidenceConfidence.ProvenCapture),
                    QualityPolicy = "captured-fixed",
                    Evidence = "live-capture-20260629-142800; 20260720-212302 empty/junk",
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
            this.registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.EnemyType,
                    TargetKey = CleaningRobotProfileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Evidence = "live-capture-20260629-142800; 20260720-212302 empty/junk",
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true,
                    Conditions = new string[0]
                });
        }

        private void EnsureNascenceBarkingChimera()
        {
            const string tableKey = "captured.nascence.barking-chimera";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260723-225021: 15 Barking Chimera corpse opens, credits=0.
            // 8 empty; 7 with items (214789 x4, 259951 x2, 225975:225976 ql9, 232726:232727 ql6,
            // 232834:232835 ql6, 214788). Snapshot roulette matches observed opens.
            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260723-225021.chimera.798C1F4F",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798C1F4F", 225975, 225976, 9, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260723-225021.chimera.798C1F96",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798C1F96", 214789, 214789, 1, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798E0A0A.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798E0A09.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798C1F94.empty", 0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260723-225021.chimera.798E0A06",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798E0A06", 214789, 214789, 1, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260723-225021.chimera.798E09BD",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798E09BD", 232726, 232727, 6, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798E09BD", 232834, 232835, 6, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798E09BD", 214789, 214789, 1, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798E09BE.empty", 0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260723-225021.chimera.798E09BF",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798E09BF", 259951, 259951, 1, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798C1F93.empty", 0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260723-225021.chimera.798E0A33",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798E0A33", 259951, 259951, 1, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798E0A33", 214789, 214789, 1, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798E0A32.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798E0A31.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798E0A36.empty", 0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260723-225021.chimera.798E0A08",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798E0A08", 214788, 214788, 1, 1)),
                };

            // ObservedCorpseSnapshots require ItemPoolUnresolved + Unresolved credits policy
            // (same contract as Alex-pad / Vergil tables); Fixed ProvenCapture credits rejects registration.
            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = "Barking Chimera captured corpse",
                    TableType = LootTableType.EnemyType,
                    RollGroups = new LootGroupDefinition[0],
                    ObservedCorpseSnapshots = snapshots,
                    CreditsPolicy = new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.Unresolved,
                        Evidence = LootEvidenceConfidence.Unresolved
                    },
                    QualityPolicy = "captured-observed-corpse-snapshots",
                    Evidence = NascenceLifeLootEvidence,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    ItemPoolUnresolved = true,
                    Enabled = true
                });
            this.registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.EnemyType,
                    TargetKey = NascenceChimeraProfileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = NascenceLifeLootEvidence,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
        }

        private void EnsureNascenceEmptyAnimalLoot(
            string tableKey,
            string profileKey,
            string displayName,
            string snapshotKey)
        {
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260723-221330: openable empty corpse (credits=0) for Nascence animals without observed loot.
            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = displayName,
                    TableType = LootTableType.EnemyType,
                    RollGroups = new LootGroupDefinition[0],
                    ObservedCorpseSnapshots =
                        new[]
                        {
                            ObservedCorpseSnapshot(
                                NascenceLifeLootEvidence,
                                snapshotKey,
                                0)
                        },
                    CreditsPolicy = new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.Unresolved,
                        Evidence = LootEvidenceConfidence.Unresolved
                    },
                    QualityPolicy = "captured-observed-corpse-snapshots",
                    Evidence = NascenceLifeLootEvidence,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    ItemPoolUnresolved = true,
                    Enabled = true
                });
            this.registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.EnemyType,
                    TargetKey = profileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = NascenceLifeLootEvidence,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
        }

        private void EnsureNascenceSwiftSilvertail()
        {
            const string tableKey = "captured.nascence.swift-silvertail";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260723-221330: Swift Silvertail corpse items 232839:232840 ql6 + 42640:42641 ql7, credits=0.
            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = "Swift Silvertail captured corpse",
                    TableType = LootTableType.EnemyType,
                    RollGroups = new LootGroupDefinition[0],
                    ObservedCorpseSnapshots =
                        new[]
                        {
                            ObservedCorpseSnapshot(
                                NascenceLifeLootEvidence,
                                "capture.20260723-221330.swift.798C1F89",
                                0,
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260723-221330.swift.798C1F89",
                                    232839,
                                    232840,
                                    6,
                                    1),
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260723-221330.swift.798C1F89",
                                    42640,
                                    42641,
                                    7,
                                    1))
                        },
                    CreditsPolicy = new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.Unresolved,
                        Evidence = LootEvidenceConfidence.Unresolved
                    },
                    QualityPolicy = "captured-observed-corpse-snapshots",
                    Evidence = NascenceLifeLootEvidence,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    ItemPoolUnresolved = true,
                    Enabled = true
                });
            this.registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.EnemyType,
                    TargetKey = NascenceSwiftProfileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = NascenceLifeLootEvidence,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
        }

        private void EnsureDatabaseLoaded()
        {
            if (this.databaseLoaded) return;
            this.debugEntries = CombatTestLootCatalog.BuildEntries();
            try
            {
                this.databaseEntries = CombatMobLootCatalog.BuildEntries(
                    MobTemplateDao.Instance.GetAll().ToArray(),
                    MobDroptableDao.Instance.GetAll().ToArray());
            }
            catch (Exception error)
            {
                this.databaseEntries = new CombatLootTableEntry[0];
                LogUtil.Debug(DebugInfoDetail.Error, "Global loot DB adapter load failed: " + error.Message);
            }
            this.databaseLoaded = true;
        }

        private void EnsureLegacyTarget(ICharacter target, string profileKey)
        {
            string tableKey = profileKey;
            if (this.registry.ContainsTable(tableKey)) return;
            CombatLootTableEntry[] debugMatches = this.debugEntries
                .Where(x => x.Matches(target.Name, target.Stats[StatIds.monsterdata].Value, target.Stats[StatIds.npcfamily].Value))
                .ToArray();
            CombatLootTableEntry[] databaseMatches = this.databaseEntries
                .Where(x => x.Matches(target.Name, target.Stats[StatIds.monsterdata].Value, target.Stats[StatIds.npcfamily].Value))
                .ToArray();
            CombatLootTableEntry[] matches = debugMatches.Length > 0 ? debugMatches : databaseMatches;
            int creditMinimum;
            int creditMaximum;
            bool hasCredits = CombatCorpseRules.TryGetObservedCreditRange(
                target.Name, target.Stats[StatIds.monsterdata].Value, out creditMinimum, out creditMaximum);
            if (matches.Length == 0 && !hasCredits) return;
            var groups = new List<LootGroupDefinition>();
            for (int index = 0; index < matches.Length; index++)
            {
                CombatLootTableEntry match = matches[index];
                LootEntryDefinition[] entries = LegacyEntries(match);
                groups.Add(new LootGroupDefinition
                {
                    LootGroupKey = "db.slot." + match.Slot + "." + index,
                    RollMode = LootRollMode.WeightedOne,
                    RollCount = 1,
                    DropChanceBasisPoints = match.EffectiveDropChanceBasisPoints,
                    Entries = entries,
                    Conditions = new string[0]
                });
            }
            this.registry.RegisterTable(new LootTableDefinition
            {
                LootTableKey = tableKey,
                DisplayName = target.Name + " legacy DB loot",
                TableType = LootTableType.EnemyType,
                RollGroups = groups.ToArray(),
                CreditsPolicy = hasCredits
                    ? CreditsRange(creditMinimum, creditMaximum, LootEvidenceConfidence.ProvenRepository)
                    : new CreditsPolicyDefinition { Mode = CreditsPolicyMode.Unresolved, Evidence = LootEvidenceConfidence.Unresolved },
                QualityPolicy = "legacy-range-check",
                Evidence = debugMatches.Length > 0 ? "combat-test-catalog" : "mobtemplate/mobdroptable",
                Confidence = LootEvidenceConfidence.ProvenRepository,
                Enabled = true
            });
            this.registry.RegisterAssignment(new LootAssignmentDefinition
            {
                AssignmentKey = tableKey,
                TargetType = LootAssignmentTargetType.EnemyType,
                TargetKey = profileKey,
                LootTableKey = tableKey,
                Priority = 0,
                Evidence = debugMatches.Length > 0 ? "combat-test-catalog" : "mobtemplate/mobdroptable",
                Confidence = LootEvidenceConfidence.ProvenRepository,
                Enabled = true,
                Conditions = new string[0]
            });
        }

        private static LootEntryDefinition[] LegacyEntries(CombatLootTableEntry match)
        {
            if (match.ItemTemplates != null && match.ItemTemplates.Length > 0)
            {
                return match.ItemTemplates.Select(x => new LootEntryDefinition
                {
                    ItemTemplateId = x.LowId,
                    HighItemTemplateId = x.HighId,
                    MinimumQuality = Math.Max(1, x.MinQuality),
                    MaximumQuality = Math.Max(Math.Max(1, x.MinQuality), x.MaxQuality),
                    MinimumQuantity = 1,
                    MaximumQuantity = 1,
                    Weight = 1,
                    DropChanceBasisPoints = 10000,
                    Semantics = LootSemantics.WeightedDocumented,
                    Evidence = LootEvidenceConfidence.ProvenRepository,
                    EvidenceReference = x.DropGroupHash
                }).ToArray();
            }
            return (match.ItemTemplateIds ?? new int[0]).Select(x => FixedEntry(x, Math.Max(1, match.Quality), null, 1)).ToArray();
        }

        private static LootEntryDefinition FixedEntry(int itemId, int quality, string selectionKey, int weight)
        {
            return new LootEntryDefinition
            {
                SelectionKey = selectionKey,
                ItemTemplateId = itemId,
                HighItemTemplateId = itemId,
                FixedQuality = quality,
                MinimumQuality = quality,
                MaximumQuality = quality,
                MinimumQuantity = 1,
                MaximumQuantity = 1,
                Weight = weight,
                DropChanceBasisPoints = 10000,
                Semantics = LootSemantics.WeightedDocumented,
                Evidence = LootEvidenceConfidence.ProvenCapture,
                EvidenceReference = "live-capture-20260629-142800"
            };
        }

        private static CreditsPolicyDefinition CreditsRange(int minimum, int maximum, LootEvidenceConfidence evidence)
        {
            return new CreditsPolicyDefinition
            {
                Mode = minimum == maximum ? CreditsPolicyMode.Fixed : CreditsPolicyMode.Range,
                MinimumCredits = minimum,
                MaximumCredits = maximum,
                Evidence = evidence
            };
        }

        private static CreditsPolicyDefinition CreditsObservedSet(params int[] outcomes)
        {
            int[] observed = (outcomes ?? new int[0])
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            if (observed.Length == 0)
            {
                return new CreditsPolicyDefinition
                {
                    Mode = CreditsPolicyMode.Unresolved,
                    Evidence = LootEvidenceConfidence.Unresolved
                };
            }

            return new CreditsPolicyDefinition
            {
                Mode = CreditsPolicyMode.ObservedSet,
                MinimumCredits = observed[0],
                MaximumCredits = observed[observed.Length - 1],
                ObservedCredits = observed,
                Evidence = LootEvidenceConfidence.ObservedAvailableLoot
            };
        }

        private static string LegacyProfileKey(ICharacter target)
        {
            return string.Format(CultureInfo.InvariantCulture, "legacy.{0}.{1}.{2}",
                target.Stats[StatIds.monsterdata].Value,
                target.Stats[StatIds.npcfamily].Value,
                (target.Name ?? "unnamed").Replace(' ', '-').ToLowerInvariant());
        }

        private static bool DiagnosticsEnabled()
        {
            return string.Equals(Environment.GetEnvironmentVariable("AO_REBIRTH_LOOT_DIAGNOSTICS"), "1", StringComparison.Ordinal);
        }
    }
}
