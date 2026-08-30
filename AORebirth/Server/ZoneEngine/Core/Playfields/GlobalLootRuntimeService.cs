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
    using ZoneEngine.Core.Playfields.Content;
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
        private const string AlexPadLootEvidence =
            "AOSharpLiveCapture 20260722-cap-mob-drop-cred corpse-loot-observations; Docker credits=4; Waste credits=11; Flea credits=5|11; Cleaning Robot credits=5";
        private const string CleaningRobotLootEvidence =
            "AOSharpLiveCapture 20260722-cap-mob-drop-cred; Cleaning Robot credits=5; Robot Junk 42620 / empty / misc";
        // Capture 20260723-221330 Nascence Life corpses.
        private const string NascenceChimeraProfileKey = "captured.nascence.barking-chimera";
        private const string NascenceYuttosProfileKey = "captured.nascence.yuttos";
        private const string NascenceDreamingSilvertailProfileKey = "captured.nascence.dreaming-silvertail";
        private const string NascenceSwiftProfileKey = "captured.nascence.swift-silvertail";
        private const string NascenceCascadingSpiritProfileKey = "captured.nascence.cascading-spirit";
        private const string NascenceSpiritHunterProfileKey = "captured.nascence.spirit-hunter";
        private const string NascenceSoulDredgeProfileKey = "captured.nascence.soul-dredge";
        private const string NascenceDiseaseRiddenRafterProfileKey = "captured.nascence.disease-ridden-rafter";
        private const string NascenceTempterusProfileKey = "captured.nascence.tempterus";
        private const string NascencePredatorStrikerProfileKey = "captured.nascence.predator-striker";
        private const string NascenceStalkingPredatorProfileKey = "captured.nascence.stalking-predator";
        private const string NascenceOutdoorWeaverOfMaliceProfileKey = "captured.nascence.weaver-of-malice";
        private const string NascenceSpinetoothHatchlingProfileKey = "captured.nascence.spinetooth-hatchling";
        private const string NascenceCripplerOfGrowthProfileKey = "captured.nascence.crippler-of-growth";
        private const string NascenceDemonicSubjugatorProfileKey = "captured.nascence.demonic-subjugator";
        private const string NascenceDeadlyPredatorProfileKey = "captured.nascence.deadly-predator";
        private const string NascenceCorruptingImpProfileKey = "captured.nascence.corrupting-imp";
        private const string NascenceSliveringChimeraProfileKey = "captured.nascence.slivering-chimera";
        private const string NascenceHiathlinProfileKey = "captured.nascence.hiathlin";
        private const string NascenceOmathonProfileKey = "captured.nascence.omathon";
        private const string NascenceHesosasProfileKey = "captured.nascence.hesosas";
        private const string NascenceDojaChipProfileKey = "documented.nascence.doja-chip";
        // Capture 20260823-171238 Nascence SL ACG interior corpse-loot-observations.
        private const string NascenceD1CoralRafterProfileKey = "captured.nascence-d1.coral-rafter";
        private const string NascenceD1WailingSpiritProfileKey = "captured.nascence-d1.wailing-spirit";
        private const string NascenceD1SmellyWeaverProfileKey = "captured.nascence-d1.smelly-weaver";
        private const string NascenceD1CripplerDestinyProfileKey = "captured.nascence-d1.crippler-destiny";
        private const string NascenceD1CroakerDesolationProfileKey = "captured.nascence-d1.croaker-desolation";
        private const string NascenceD1CroakerSolitudeProfileKey = "captured.nascence-d1.croaker-solitude";
        private const string NascenceD1HavarisProfileKey = "captured.nascence-d1.havaris";
        private const string NascenceD2BoundDryadProfileKey = "captured.nascence-d2.bound-dryad";
        private const string NascenceD2InfernalVortexoidProfileKey = "captured.nascence-d2.infernal-vortexoid";
        private const string NascenceD2MalahFamaProfileKey = "captured.nascence-d2.malah-fama";
        private const string NascenceD2WeaverOfMaliceProfileKey = "captured.nascence-d2.weaver-of-malice";
        private const string NascenceD2CroakerSolitudeProfileKey = "captured.nascence-d2.croaker-solitude";
        private const string NascenceD2BurningShadowProfileKey = "captured.nascence-d2.burning-shadow";
        private const string NascenceD2IcyShadowProfileKey = "captured.nascence-d2.icy-shadow";
        private const string NascenceD2SmellyWeaverProfileKey = "captured.nascence-d2.smelly-weaver";
        private const string NascenceD2HavarisProfileKey = "captured.nascence-d2.havaris";
        private const string NascenceD1LootEvidence =
            "AOSharpLiveCapture 20260823-171238 Nascence Frontier PF4310/SL ACG corpse-loot-observations; credits=0 on all linked corpses";
        private const string NascenceD1HavarisLootEvidence =
            "AOSharpLiveCapture 20260824-175852 Havaris boss corpse FE9001 InventoryUpdate seq6241 + ContainerAddItem seq6259/6262";
        private const string NascenceD2LootEvidence =
            "AOSharpLiveCapture 20260825-094236 SL ACG(dng) PF 0x002080D9 InventoryUpdate Remains loot; Encapsulated Bullet never on Remains";
        // AO-Universe: outdoor plain mobs; drop rate not great — provisional 2.5% until capture-backed rate.
        private const int NascenceDojaChipDropChanceBasisPoints = 250;
        // Capture 20260826-052537 + Mike: Hiathlin Nascense DOJA 5%.
        private const int NascenceHiathlinDojaChipDropChanceBasisPoints = 500;
        // Mike: Predator Striker Nascense DOJA 5% (capture 20260826-054154 pocket).
        private const int NascencePredatorStrikerDojaChipDropChanceBasisPoints = 500;
        // Mike: Compact Message Datadisc ~30% on quest source mobs (Independent overlay on observed loot).
        private const int NascenceCompactMessageDatadiscDropChanceBasisPoints = 3000;
        // InventoryUpdate disc ids: Silvertail 0x3F76D, Chimera 0x3F76F, Predator 0x3F76E, Weaver 0x3F770.
        private const int NascenceSwiftSilvertailDatadiscItemId = 259949;
        private const int NascenceBarkingChimeraDatadiscItemId = 259951;
        private const int NascencePredatorDatadiscItemId = 259950;
        private const int NascenceWeaverOfMaliceDatadiscItemId = 259952;
        private const string NascenceCompactMessageDatadiscEvidence =
            "AOSharpLiveCapture PF4310 Compact Message Datadisc InventoryUpdate; "
            + "20260822-082554 Silvertail=259949; 20260822-083345 Chimera=259951; "
            + "20260822-083846 Predator=259950 Weaver=259952; Mike 30% Independent drop";
        private const string NascenceLifeLootEvidence =
            "AOSharpLiveCapture 20260723-225021 Barking Chimera 15 corpses; 20260723-221330 Swift Silvertail; "
            + "20260822-221109 Jobe Research PF4001/4310 starter mobs (Chimera/Silvertail/Yuttos Geosurvey Dog loot+empty corpses); "
            + "20260823-103458 Spirit Hunter/Cascading Spirit/Soul Dredge corpse-loot-observations; "
            + "20260823-112044 Disease-Ridden Rafter/Tempterus/Predator Striker/Crippler of Growth corpse-loot-observations";
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
            this.EnsureAlexPadCreditsEvenWhenEmpty(context, result);
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

        private void EnsureAlexPadCreditsEvenWhenEmpty(LootGenerationContext context, LootGenerationResult result)
        {
            if (context == null
                || result == null
                || result.Credits > 0
                || context.SuppressMonsterDataFallbackLoot)
            {
                return;
            }

            int credits;
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
            var context = new LootGenerationContext
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
            CapturedEnemyCombatContract combatContract;
            CapturedEnemyCombatRuntimeRegistry.TryGet(
                target.Identity.Instance,
                out combatContract);
            AreteCombatLootIdentityPolicy.Apply(
                context,
                combatContract,
                playfieldId,
                target.Name);

            return context;
        }

        private void EnsureDefinitions(ICharacter target, LootGenerationContext context)
        {
            if (context.IsOwnedSummon || context.SuppressMonsterDataFallbackLoot) return;
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

                if (context.MonsterData == AlexDockerMonsterData)
                {
                    this.EnsureAlexDocker();
                    context.EnemyProfileKey = AlexDockerProfileKey;
                    return;
                }

                if (context.MonsterData == AlexWasteMonsterData)
                {
                    this.EnsureAlexWasteCollector();
                    context.EnemyProfileKey = AlexWasteProfileKey;
                    return;
                }

                if (context.MonsterData == AlexFleaMonsterData)
                {
                    this.EnsureAlexGarbageFlea();
                    context.EnemyProfileKey = AlexFleaProfileKey;
                    return;
                }

                if (context.MonsterData == CleaningRobotMonsterData)
                {
                    this.EnsureCleaningRobot();
                    context.EnemyProfileKey = CleaningRobotProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Barking Chimera", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceBarkingChimera();
                    this.EnsureNascenceCompactMessageDatadiscOnTable(
                        "captured.nascence.barking-chimera",
                        NascenceBarkingChimeraDatadiscItemId);
                    context.EnemyProfileKey = NascenceChimeraProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Yuttos Nascence Geosurvey Dog", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceGeosurveyDog();
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
                    this.EnsureNascenceCompactMessageDatadiscOnTable(
                        "captured.nascence.swift-silvertail",
                        NascenceSwiftSilvertailDatadiscItemId);
                    context.EnemyProfileKey = NascenceSwiftProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Cascading Spirit", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceCascadingSpirit();
                    context.EnemyProfileKey = NascenceCascadingSpiritProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Nascence Spirit Hunter", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceSpiritHunter();
                    context.EnemyProfileKey = NascenceSpiritHunterProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Soul Dredge", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceSoulDredge();
                    context.EnemyProfileKey = NascenceSoulDredgeProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Disease-Ridden Rafter", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceDiseaseRiddenRafter();
                    context.EnemyProfileKey = NascenceDiseaseRiddenRafterProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Tempterus", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceTempterus();
                    context.EnemyProfileKey = NascenceTempterusProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Predator Striker", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascencePredatorStriker();
                    this.EnsureNascenceDojaChipOnTable(
                        "captured.nascence.predator-striker",
                        NascencePredatorStrikerDojaChipDropChanceBasisPoints);
                    this.EnsureNascenceCompactMessageDatadiscOnTable(
                        "captured.nascence.predator-striker",
                        NascencePredatorDatadiscItemId);
                    context.EnemyProfileKey = NascencePredatorStrikerProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Stalking Predator", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceStalkingPredator();
                    this.EnsureNascenceCompactMessageDatadiscOnTable(
                        "captured.nascence.stalking-predator",
                        NascencePredatorDatadiscItemId);
                    context.EnemyProfileKey = NascenceStalkingPredatorProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase)
                    && !NascenceDungeon2Rules.IsDungeonPlayfield(context.PlayfieldId))
                {
                    this.EnsureNascenceOutdoorWeaverOfMalice();
                    this.EnsureNascenceCompactMessageDatadiscOnTable(
                        "captured.nascence.weaver-of-malice",
                        NascenceWeaverOfMaliceDatadiscItemId);
                    context.EnemyProfileKey = NascenceOutdoorWeaverOfMaliceProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase)
                    && context.PlayfieldId == NascenceLifeContentModule.FrontierPlayfieldId)
                {
                    this.EnsureNascenceSpinetoothHatchling();
                    this.EnsureNascenceDojaChipOnTable("captured.nascence.spinetooth-hatchling");
                    context.EnemyProfileKey = NascenceSpinetoothHatchlingProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Crippler of Growth", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceCripplerOfGrowth();
                    context.EnemyProfileKey = NascenceCripplerOfGrowthProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "The Demonic Subjugator", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(target.Name, "Demonic Subjugator", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceDemonicSubjugator();
                    context.EnemyProfileKey = NascenceDemonicSubjugatorProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Deadly Predator", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceDeadlyPredator();
                    context.EnemyProfileKey = NascenceDeadlyPredatorProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Corrupting Imp", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceCorruptingImp();
                    context.EnemyProfileKey = NascenceCorruptingImpProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Slivering Chimera", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceSliveringChimera();
                    context.EnemyProfileKey = NascenceSliveringChimeraProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Hiathlin", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(target.Name, "Hiathlin Prime", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceHiathlin();
                    this.EnsureNascenceDojaChipOnTable(
                        "captured.nascence.hiathlin",
                        NascenceHiathlinDojaChipDropChanceBasisPoints);
                    context.EnemyProfileKey = NascenceHiathlinProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Hesosas", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceHesosas();
                    context.EnemyProfileKey = NascenceHesosasProfileKey;
                    return;
                }

                if (string.Equals(target.Name, "Omathon", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnsureNascenceOmathon();
                    context.EnemyProfileKey = NascenceOmathonProfileKey;
                    return;
                }

                if (IsNascenceDojaChipDropper(target.Name))
                {
                    this.EnsureNascenceDojaChipLoot();
                    context.EnemyProfileKey = NascenceDojaChipProfileKey;
                    return;
                }

                if (NascenceDungeon2Rules.IsDungeonPlayfield(context.PlayfieldId)
                    && NascenceDungeon2Rules.IsDungeonCorpseName(target.Name))
                {
                    this.EnsureNascenceDungeon2Loot(target.Name);
                    context.EnemyProfileKey = NascenceDungeon2ProfileKeyFor(target.Name);
                    return;
                }

                if (NascenceDungeon1Rules.IsDungeonCorpseName(target.Name))
                {
                    this.EnsureNascenceDungeon1Loot(target.Name);
                    context.EnemyProfileKey = NascenceDungeon1ProfileKeyFor(target.Name);
                    return;
                }

                OrdinaryEnemyRuntimeDefinition ordinary;
                if (OrdinaryEnemyRuntimeRegistry.TryGet(target.Identity.Instance, out ordinary))
                {
                    this.EnsureOrdinary(ordinary.Profile, context.Level);
                    return;
                }

                this.EnsureDatabaseLoaded();
                this.EnsureLegacyTarget(
                    target,
                    context.EnemyProfileKey,
                    context.PlayfieldId);
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
            CapturedSubwayLootDefinitions.ApplyDocumentedMembership(
                table,
                encounter.ProfileKey,
                encounter.DisplayName);
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

            LootTableDefinition table = BuildCapturedVergilLootTable();
            CapturedSubwayLootDefinitions.ApplyDocumentedMembership(
                table,
                CapturedVergilProfileKey,
                "Vergil Aeneid");
            this.registry.RegisterTable(table);
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
            CapturedSubwayLootDefinitions.ApplyDocumentedMembership(
                adapted.Table,
                profile.ProfileKey,
                profile.DisplayName);
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
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.docker.b", 248307, 248307, 1, 1))
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
                        ObservedCorpseSnapshotEntry(AlexPadLootEvidence, "capture.20260722.waste.e", 42620, 42619, 2, 1))
                };

            this.RegisterAlexPadTable(
                tableKey,
                "Waste Collector captured corpse",
                AlexWasteProfileKey,
                snapshots);
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

        private static bool IsNascenceDojaChipDropper(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            // AO-Universe DOJA guide: Nascense outdoor mobs (Hiathlin quest parts are kill-granted).
            return name.IndexOf("Crippler of Growth", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("Malah-Ana", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("Predator Striker", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("Spinetooth Hatchling", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnsureNascenceDojaChipLoot()
        {
            const string tableKey = "documented.nascense.doja-chip";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string evidence = "ao-universe.doja-chips; item capture 20260821-222107 item=284954";
            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = "Nascense DOJA Chip (documented)",
                    TableType = LootTableType.EnemyType,
                    RollGroups =
                        new[]
                        {
                            new LootGroupDefinition
                            {
                                LootGroupKey = "nascense-doja-chip",
                                RollMode = LootRollMode.Independent,
                                RollCount = 1,
                                EmptyWeight = 0,
                                DropChanceBasisPoints = NascenceDojaChipDropChanceBasisPoints,
                                Entries =
                                    new[]
                                    {
                                        FixedEntry(
                                            284954,
                                            1,
                                            "doja-chip-nascense",
                                            1)
                                    }
                            }
                        },
                    ObservedCorpseSnapshots = new ObservedCorpseSnapshotDefinition[0],
                    CreditsPolicy = new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.Unresolved,
                        Evidence = LootEvidenceConfidence.CommunityDocumented
                    },
                    QualityPolicy = "documented-ao-universe-doja",
                    Evidence = evidence,
                    Confidence = LootEvidenceConfidence.CommunityDocumented,
                    ItemPoolUnresolved = false,
                    Enabled = true
                });
            this.registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.EnemyType,
                    TargetKey = NascenceDojaChipProfileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = evidence,
                    Confidence = LootEvidenceConfidence.CommunityDocumented,
                    Enabled = true
                });
        }

        private void EnsureNascenceCompactMessageDatadiscOnTable(string tableKey, int itemId)
        {
            if (!this.registry.ContainsTable(tableKey) || itemId <= 0)
            {
                return;
            }

            // Capture-backed disc IDs (must stay 1:1 with drop mob):
            // Swift Silvertail=259949, Barking Chimera=259951,
            // Stalking Predator/Predator Striker=259950, Weaver of Malice=259952.
            LootTableDefinition table = this.registry.GetTable(tableKey);
            string groupKey = tableKey + ".compact-message-datadisc";
            LootGroupDefinition[] existing = table.RollGroups ?? new LootGroupDefinition[0];
            var groups = new List<LootGroupDefinition>(existing.Length + 1);
            for (int index = 0; index < existing.Length; index++)
            {
                if (string.Equals(existing[index].LootGroupKey, groupKey, StringComparison.Ordinal))
                {
                    continue;
                }

                groups.Add(existing[index]);
            }

            groups.Add(
                new LootGroupDefinition
                {
                    LootGroupKey = groupKey,
                    RollMode = LootRollMode.Independent,
                    RollCount = 1,
                    EmptyWeight = 0,
                    DropChanceBasisPoints = NascenceCompactMessageDatadiscDropChanceBasisPoints,
                    Entries =
                        new[]
                        {
                            FixedEntry(
                                itemId,
                                1,
                                "compact-message-datadisc-" + itemId.ToString(CultureInfo.InvariantCulture),
                                1)
                        },
                    Conditions = new string[0]
                });
            table.RollGroups = groups.ToArray();
        }

        private void EnsureNascenceDojaChipOnTable(string tableKey)
        {
            EnsureNascenceDojaChipOnTable(tableKey, NascenceDojaChipDropChanceBasisPoints);
        }

        private void EnsureNascenceDojaChipOnTable(string tableKey, int dropChanceBasisPoints)
        {
            if (!this.registry.ContainsTable(tableKey))
            {
                return;
            }

            this.EnsureNascenceDojaChipLoot();

            LootTableDefinition table = this.registry.GetTable(tableKey);
            const string groupKey = "nascense-doja-chip";
            LootGroupDefinition[] existing = table.RollGroups ?? new LootGroupDefinition[0];
            for (int index = 0; index < existing.Length; index++)
            {
                if (string.Equals(existing[index].LootGroupKey, groupKey, StringComparison.Ordinal))
                {
                    return;
                }
            }

            LootTableDefinition dojaTable = this.registry.GetTable("documented.nascense.doja-chip");
            LootGroupDefinition dojaGroup = null;
            if (dojaTable?.RollGroups != null)
            {
                for (int index = 0; index < dojaTable.RollGroups.Length; index++)
                {
                    if (string.Equals(dojaTable.RollGroups[index].LootGroupKey, groupKey, StringComparison.Ordinal))
                    {
                        dojaGroup = dojaTable.RollGroups[index];
                        break;
                    }
                }
            }

            if (dojaGroup == null)
            {
                return;
            }

            if (dropChanceBasisPoints != dojaGroup.DropChanceBasisPoints)
            {
                dojaGroup = new LootGroupDefinition
                {
                    LootGroupKey = dojaGroup.LootGroupKey,
                    RollMode = dojaGroup.RollMode,
                    RollCount = dojaGroup.RollCount,
                    EmptyWeight = dojaGroup.EmptyWeight,
                    DropChanceBasisPoints = dropChanceBasisPoints,
                    Entries = dojaGroup.Entries,
                    Conditions = dojaGroup.Conditions
                };
            }

            var groups = new List<LootGroupDefinition>(existing.Length + 1);
            groups.AddRange(existing);
            groups.Add(dojaGroup);
            table.RollGroups = groups.ToArray();
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
                    // Datadisc moved to Independent 30% overlay (Mike); keep snapshot slot as empty.
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798E09BF.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798C1F93.empty", 0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260723-225021.chimera.798E0A33",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798E0A33", 214789, 214789, 1, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798E0A32.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798E0A31.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260723-225021.chimera.798E0A36.empty", 0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260723-225021.chimera.798E0A08",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260723-225021.chimera.798E0A08", 214788, 214788, 1, 1)),
                    // Capture 20260822-221109: 28 Barking Chimera corpse opens on PF 4310 starter bridge.
                    ObservedCorpseSnapshot(e, "capture.20260822-221109.chimera.FCE008.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260822-221109.chimera.FCE00A.empty", 0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260822-221109.chimera.FCE00A",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260822-221109.chimera.FCE00A", 214789, 214789, 1, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260822-221109.chimera.FCE00E.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260822-221109.chimera.FCE004.empty", 0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260822-221109.chimera.FCE007",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260822-221109.chimera.FCE007", 225981, 225982, 8, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260822-221109.chimera.FCE013.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260822-221109.chimera.FCE019.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260822-221109.chimera.FCE003.empty", 0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260822-221109.chimera.FCE023",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260822-221109.chimera.FCE023", 225977, 225978, 5, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260822-221109.chimera.FCE023", 214789, 214789, 1, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260822-221109.chimera.FCE022",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260822-221109.chimera.FCE022", 214788, 214788, 1, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260822-221109.chimera.FCE028",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260822-221109.chimera.FCE028", 214788, 214788, 1, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260822-221109.chimera.FCE029",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260822-221109.chimera.FCE029", 232839, 232840, 5, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260822-221109.chimera.FCE029", 214788, 214788, 1, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260822-221109.chimera.FCE025",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260822-221109.chimera.FCE025", 232834, 232835, 7, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260822-221109.chimera.FCE027.empty", 0),
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
            // Capture 20260822-082554 / 221109: Compact Message Datadisc 259949 → Independent 30% overlay.
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
                                    1)),
                            ObservedCorpseSnapshot(
                                NascenceLifeLootEvidence,
                                "capture.20260822-082554.swift.FCE01C.empty",
                                0),
                            // Capture 20260822-221109: Swift Silvertail starter bridge corpses.
                            ObservedCorpseSnapshot(
                                NascenceLifeLootEvidence,
                                "capture.20260822-221109.swift.FCE021",
                                0,
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260822-221109.swift.FCE021",
                                    227168,
                                    227169,
                                    6,
                                    1)),
                            ObservedCorpseSnapshot(NascenceLifeLootEvidence, "capture.20260822-221109.swift.FCE00B.empty", 0),
                            ObservedCorpseSnapshot(NascenceLifeLootEvidence, "capture.20260822-221109.swift.FCE014.empty", 0),
                            ObservedCorpseSnapshot(
                                NascenceLifeLootEvidence,
                                "capture.20260822-221109.swift.FCE017",
                                0,
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260822-221109.swift.FCE017",
                                    225979,
                                    225980,
                                    7,
                                    1)),
                            ObservedCorpseSnapshot(
                                NascenceLifeLootEvidence,
                                "capture.20260822-221109.swift.FCE01B",
                                0,
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260822-221109.swift.FCE01B",
                                    42640,
                                    42641,
                                    5,
                                    1)),
                            ObservedCorpseSnapshot(
                                NascenceLifeLootEvidence,
                                "capture.20260822-221109.swift.FCE01D",
                                0,
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260822-221109.swift.FCE01D",
                                    232839,
                                    232840,
                                    9,
                                    1),
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260822-221109.swift.FCE01D",
                                    42640,
                                    42641,
                                    10,
                                    1)),
                            ObservedCorpseSnapshot(
                                NascenceLifeLootEvidence,
                                "capture.20260822-221109.swift.FCE023",
                                0,
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260822-221109.swift.FCE023",
                                    227168,
                                    227169,
                                    7,
                                    1),
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260822-221109.swift.FCE023",
                                    42640,
                                    42641,
                                    6,
                                    1)),
                            ObservedCorpseSnapshot(
                                NascenceLifeLootEvidence,
                                "capture.20260822-221109.swift.FCE00D",
                                0,
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260822-221109.swift.FCE00D",
                                    42640,
                                    42641,
                                    6,
                                    1)),
                            ObservedCorpseSnapshot(
                                NascenceLifeLootEvidence,
                                "capture.20260822-221109.swift.FCE007",
                                0,
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260822-221109.swift.FCE007",
                                    223421,
                                    223422,
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

        private void EnsureNascenceGeosurveyDog()
        {
            const string tableKey = "captured.nascence.yuttos";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260822-221109: Yuttos Nascence Geosurvey Dog drops item 223768 ql1.
            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = "Yuttos Nascence Geosurvey Dog captured corpse",
                    TableType = LootTableType.EnemyType,
                    RollGroups = new LootGroupDefinition[0],
                    ObservedCorpseSnapshots =
                        new[]
                        {
                            ObservedCorpseSnapshot(
                                NascenceLifeLootEvidence,
                                "capture.20260822-221109.yuttos.FCE002",
                                0,
                                ObservedCorpseSnapshotEntry(
                                    NascenceLifeLootEvidence,
                                    "capture.20260822-221109.yuttos.FCE002",
                                    223768,
                                    223768,
                                    1,
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
                    TargetKey = NascenceYuttosProfileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = NascenceLifeLootEvidence,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
        }

        private void EnsureNascenceCascadingSpirit()
        {
            const string tableKey = "captured.nascence.cascading-spirit";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260822-083345: Essence of the Haunted 259956.
            // Capture 20260823-103458: corpse-loot-observations for Cascading Spirit opens.
            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260822-083345.cascading.FCE012",
                        0,
                        ObservedCorpseSnapshotEntry(
                            e,
                            "capture.20260822-083345.cascading.FCE012",
                            259956,
                            259956,
                            1,
                            1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-103458.cascading.FCE002",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.cascading.FCE002", 214940, 229944, 11, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-103458.cascading.FCE007",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.cascading.FCE007", 214940, 229944, 11, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260823-103458.cascading.FCE008.empty", 0),
                    ObservedCorpseSnapshot(e, "capture.20260823-103458.cascading.FCE00A.empty", 0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-103458.cascading.FCE00D",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.cascading.FCE00D", 225213, 225213, 1, 1)),
                };

            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = "Cascading Spirit captured corpse",
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
                    TargetKey = NascenceCascadingSpiritProfileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = NascenceLifeLootEvidence,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
        }

        private void EnsureNascenceSpiritHunter()
        {
            const string tableKey = "captured.nascence.spirit-hunter";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260823-103458 Spirit Hunter corpse-loot-observations.
            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-103458.hunter.FCE001",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE001", 236285, 236285, 1, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE001", 214940, 229944, 12, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE001", 232839, 232840, 15, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-103458.hunter.FCE003",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE003", 235989, 235989, 5, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE003", 214940, 229944, 15, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE003", 223440, 223441, 13, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE003", 225979, 225980, 11, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-103458.hunter.FCE004",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE004", 236387, 236387, 10, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE004", 214940, 229944, 14, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE004", 232834, 232835, 12, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-103458.hunter.FCE005",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE005", 236315, 236315, 1, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE005", 214940, 229944, 13, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.hunter.FCE005", 225981, 225982, 11, 1)),
                };

            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = "Nascence Spirit Hunter captured corpse",
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
                    TargetKey = NascenceSpiritHunterProfileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = NascenceLifeLootEvidence,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
        }

        private void EnsureNascenceSoulDredge()
        {
            const string tableKey = "captured.nascence.soul-dredge";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260823-103458 Soul Dredge corpse FCE012.
            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-103458.dredge.FCE012",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.dredge.FCE012", 226590, 226591, 17, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.dredge.FCE012", 226701, 226702, 17, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.dredge.FCE012", 235546, 235546, 5, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.dredge.FCE012", 214940, 229944, 13, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-103458.dredge.FCE012", 223423, 223424, 15, 1)),
                };

            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = "Soul Dredge captured corpse",
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
                    TargetKey = NascenceSoulDredgeProfileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = NascenceLifeLootEvidence,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
        }

        private void EnsureNascenceDiseaseRiddenRafter()
        {
            const string tableKey = "captured.nascence.disease-ridden-rafter";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260823-112044 Disease-Ridden Rafter corpse-loot-observations.
            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-112044.rafter.FCE014",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-112044.rafter.FCE014", 223566, 223567, 10, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260823-112044.rafter.FCE01C.empty", 0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Disease-Ridden Rafter captured corpse",
                NascenceDiseaseRiddenRafterProfileKey,
                snapshots);
        }

        private void EnsureNascenceTempterus()
        {
            const string tableKey = "captured.nascence.tempterus";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-112044.tempterus.item",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-112044.tempterus.item", 223445, 223446, 8, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260823-112044.tempterus.empty.a", 0),
                    ObservedCorpseSnapshot(e, "capture.20260823-112044.tempterus.empty.b", 0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Tempterus captured corpse",
                NascenceTempterusProfileKey,
                snapshots);
        }

        private void EnsureNascencePredatorStriker()
        {
            const string tableKey = "captured.nascence.predator-striker";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260826-054154 pocket ~755-810/1900-1965: empty + junk; DOJA 5% overlay.
            const string e =
                "AOSharpLiveCapture 20260826-054154 Predator Striker corpse-loot-observations; "
                + NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(e, "capture.20260826-054154.predator.empty", 0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-054154.predator.42640",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-054154.predator.42640", 42640, 42641, 17, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-054154.predator.42640.b",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-054154.predator.42640.b", 42640, 42641, 16, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-054154.predator.232816",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-054154.predator.232816", 232816, 232817, 14, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Predator Striker captured corpse",
                NascencePredatorStrikerProfileKey,
                snapshots);
        }

        private void EnsureNascenceStalkingPredator()
        {
            const string tableKey = "captured.nascence.stalking-predator";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260825-202932: 214788; 232834:232835 ql10 + prior empty + disc overlay 259950.
            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-202932.stalking.214788",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-202932.stalking.214788", 214788, 214788, 1, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-202932.stalking.232834",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-202932.stalking.232834", 232834, 232835, 10, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260822-083846.stalking-predator.empty",
                        0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Stalking Predator captured corpse",
                NascenceStalkingPredatorProfileKey,
                snapshots);
        }

        private void EnsureNascenceDemonicSubjugator()
        {
            const string tableKey = "captured.nascence.demonic-subjugator";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260825-202932 boss corpse 7A2ED7C3.
            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-202932.demonic.boss",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-202932.demonic.boss", 235441, 235441, 1, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-202932.demonic.boss", 236438, 236438, 1, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-202932.demonic.boss", 227028, 227029, 17, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-202932.demonic.boss", 223423, 223424, 22, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "The Demonic Subjugator captured corpse",
                NascenceDemonicSubjugatorProfileKey,
                snapshots);
        }

        private void EnsureNascenceDeadlyPredator()
        {
            const string tableKey = "captured.nascence.deadly-predator";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260826-054154 + 20260825-202932: pocket boss Sabretooth Slicer A/B/C/D — limit 1 random.
            const string e =
                "AOSharpLiveCapture 20260826-054154 Deadly Predator corpse-loot-observations; "
                + "itemnames Abhan/Bhotaar/Chi/Dom Sabretooth Slicer; Mike limit 1 random pattern";
            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = "Deadly Predator Sabretooth Slicer pattern",
                    TableType = LootTableType.EnemyType,
                    RollGroups =
                        new[]
                        {
                            new LootGroupDefinition
                            {
                                LootGroupKey = "sabretooth-slicer-pattern",
                                RollMode = LootRollMode.WeightedOne,
                                RollCount = 1,
                                EmptyWeight = 0,
                                DropChanceBasisPoints = 10000,
                                Entries =
                                    new[]
                                    {
                                        FixedEntry(242802, 35, "sabretooth.abhan", 1),
                                        FixedEntry(239545, 35, "sabretooth.bhotaar", 1),
                                        FixedEntry(239546, 35, "sabretooth.chi", 1),
                                        FixedEntry(239547, 35, "sabretooth.dom", 1),
                                    },
                                Conditions = new string[0]
                            }
                        },
                    CreditsPolicy = new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.Fixed,
                        MinimumCredits = 0,
                        MaximumCredits = 0,
                        Evidence = LootEvidenceConfidence.ProvenCapture
                    },
                    QualityPolicy = "capture-backed-pocket-boss",
                    Evidence = e,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
            this.registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.EnemyType,
                    TargetKey = NascenceDeadlyPredatorProfileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = e,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
        }

        private void EnsureNascenceCorruptingImp()
        {
            const string tableKey = "captured.nascence.corrupting-imp";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-202932.imp",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-202932.imp", 223421, 223422, 24, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-202932.imp", 214788, 214788, 1, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Corrupting Imp captured corpse",
                NascenceCorruptingImpProfileKey,
                snapshots);
        }

        private void EnsureNascenceSliveringChimera()
        {
            const string tableKey = "captured.nascence.slivering-chimera";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-202932.slivering.214789",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-202932.slivering.214789", 214789, 214789, 1, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-202932.slivering.232834",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-202932.slivering.232834", 232834, 232835, 12, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Slivering Chimera captured corpse",
                NascenceSliveringChimeraProfileKey,
                snapshots);
        }

        private void EnsureNascenceOutdoorWeaverOfMalice()
        {
            const string tableKey = "captured.nascence.weaver-of-malice";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260822-083846 outdoor Weaver of Malice Compact Message Datadisc 259952 (30% overlay).
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        NascenceLifeLootEvidence,
                        "capture.20260822-083846.outdoor-weaver.empty",
                        0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Weaver of Malice (outdoor) captured corpse",
                NascenceOutdoorWeaverOfMaliceProfileKey,
                snapshots);
        }

        private void EnsureNascenceSpinetoothHatchling()
        {
            const string tableKey = "captured.nascence.spinetooth-hatchling";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260826-051307 + 20260826-212737 outdoor Spinetooth corpse opens.
            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-051307.spinetooth.empty",
                        0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-051307.spinetooth.224437-214789",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-051307.spinetooth.224437", 224437, 224437, 50, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-051307.spinetooth.214789", 214789, 214789, 1, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-212737.spinetooth.214788",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-212737.spinetooth.214788", 214788, 214788, 1, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-212737.spinetooth.223572",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-212737.spinetooth.223572", 223572, 223573, 24, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-212737.spinetooth.223423",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-212737.spinetooth.223423", 223423, 223424, 15, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Spinetooth Hatchling captured corpse",
                NascenceSpinetoothHatchlingProfileKey,
                snapshots);
        }

        private void EnsureNascenceCripplerOfGrowth()
        {
            const string tableKey = "captured.nascence.crippler-of-growth";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceLifeLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-112044.crippler.FCE",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-112044.crippler.FCE", 284954, 284954, 1, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-112044.crippler.FCE", 223568, 223569, 10, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Crippler of Growth captured corpse",
                NascenceCripplerOfGrowthProfileKey,
                snapshots);
        }

        private void EnsureNascenceHiathlin()
        {
            const string tableKey = "captured.nascence.hiathlin";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260826-052537 Hiathlin corpse opens (credits=0).
            const string e = "AOSharpLiveCapture 20260826-052537 Hiathlin corpse-loot-observations";
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-052537.hiathlin.214788",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-052537.hiathlin.214788", 214788, 214788, 1, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-052537.hiathlin.214789",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-052537.hiathlin.214789", 214789, 214789, 1, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-052537.hiathlin.empty",
                        0),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-052537.hiathlin.223442",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-052537.hiathlin.223442", 223442, 223443, 19, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Hiathlin captured corpse",
                NascenceHiathlinProfileKey,
                snapshots);
        }

        private void EnsureNascenceHesosas()
        {
            const string tableKey = "captured.nascence.hesosas";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260826-055143 + Mike: Killer's Armor all parts ql21-25, limit 1 piece.
            const string e =
                "AOSharpLiveCapture 20260826-055143 Hesosas corpse-loot-observations; "
                + "itemrelations Killer's Armor 227030-227049; Mike limit 1 armor piece ql21-25";
            this.registry.RegisterTable(
                new LootTableDefinition
                {
                    LootTableKey = tableKey,
                    DisplayName = "Hesosas Killer's Armor piece",
                    TableType = LootTableType.EnemyType,
                    RollGroups =
                        new[]
                        {
                            new LootGroupDefinition
                            {
                                LootGroupKey = "killers-armor-piece",
                                RollMode = LootRollMode.WeightedOne,
                                RollCount = 1,
                                EmptyWeight = 0,
                                DropChanceBasisPoints = 10000,
                                Entries =
                                    new[]
                                    {
                                        KillerArmorEntry(227030, "killers-armor.227030"),
                                        KillerArmorEntry(227032, "killers-armor.227032"),
                                        KillerArmorEntry(227034, "killers-armor.227034"),
                                        KillerArmorEntry(227036, "killers-armor.227036"),
                                        KillerArmorEntry(227040, "killers-armor.227040"),
                                        KillerArmorEntry(227042, "killers-armor.227042"),
                                        KillerArmorEntry(227044, "killers-armor.227044"),
                                        KillerArmorEntry(227045, "killers-armor.227045"),
                                        KillerArmorEntry(227046, "killers-armor.227046"),
                                        KillerArmorEntry(227047, "killers-armor.227047"),
                                        KillerArmorEntry(227048, "killers-armor.227048"),
                                        KillerArmorEntry(227049, "killers-armor.227049"),
                                    },
                                Conditions = new string[0]
                            }
                        },
                    CreditsPolicy = new CreditsPolicyDefinition
                    {
                        Mode = CreditsPolicyMode.Fixed,
                        MinimumCredits = 0,
                        MaximumCredits = 0,
                        Evidence = LootEvidenceConfidence.ProvenCapture
                    },
                    QualityPolicy = "capture-backed-pocket-boss-armor",
                    Evidence = e,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
            this.registry.RegisterAssignment(
                new LootAssignmentDefinition
                {
                    AssignmentKey = tableKey,
                    TargetType = LootAssignmentTargetType.EnemyType,
                    TargetKey = NascenceHesosasProfileKey,
                    LootTableKey = tableKey,
                    Priority = 0,
                    Conditions = new string[0],
                    Evidence = e,
                    Confidence = LootEvidenceConfidence.ProvenCapture,
                    Enabled = true
                });
        }

        private void EnsureNascenceOmathon()
        {
            const string tableKey = "captured.nascence.omathon";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            // Capture 20260826-052537 Omathon corpse (4 items, credits=0).
            const string e = "AOSharpLiveCapture 20260826-052537 Omathon corpse-loot-observations";
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260826-052537.omathon.loot",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-052537.omathon.226474", 226474, 226475, 12, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-052537.omathon.223445", 223445, 223446, 12, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-052537.omathon.235714", 235714, 235714, 1, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260826-052537.omathon.232822", 232822, 232823, 14, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Omathon captured corpse",
                NascenceOmathonProfileKey,
                snapshots);
        }

        private void RegisterNascenceObservedCorpseTable(
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

        private static string NascenceDungeon1ProfileKeyFor(string name)
        {
            if (name != null && name.EndsWith("Coral Rafter", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD1CoralRafterProfileKey;
            }

            if (string.Equals(name, "Wailing Spirit", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD1WailingSpiritProfileKey;
            }

            if (string.Equals(name, "Smelly Weaver", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD1SmellyWeaverProfileKey;
            }

            if (string.Equals(name, "Crippler of Destiny", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD1CripplerDestinyProfileKey;
            }

            if (string.Equals(name, "Croaker of Desolation", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD1CroakerDesolationProfileKey;
            }

            if (string.Equals(name, "Croaker of Solitude", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD1CroakerSolitudeProfileKey;
            }

            if (string.Equals(name, "Havaris", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD1HavarisProfileKey;
            }

            return NascenceD1CroakerSolitudeProfileKey;
        }

        private static string NascenceDungeon2ProfileKeyFor(string name)
        {
            if (string.Equals(name, "Bound Dryad", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD2BoundDryadProfileKey;
            }

            if (string.Equals(name, "Infernal Vortexoid", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD2InfernalVortexoidProfileKey;
            }

            if (string.Equals(name, "Malah-Fama", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD2MalahFamaProfileKey;
            }

            if (string.Equals(name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD2WeaverOfMaliceProfileKey;
            }

            if (string.Equals(name, "Croaker of Solitude", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD2CroakerSolitudeProfileKey;
            }

            if (string.Equals(name, "Burning Shadow", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD2BurningShadowProfileKey;
            }

            if (string.Equals(name, "Icy Shadow", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD2IcyShadowProfileKey;
            }

            if (string.Equals(name, "Smelly Weaver", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD2SmellyWeaverProfileKey;
            }

            if (string.Equals(name, "Havaris", StringComparison.OrdinalIgnoreCase))
            {
                return NascenceD2HavarisProfileKey;
            }

            return NascenceD2BoundDryadProfileKey;
        }

        private void EnsureNascenceDungeon1Loot(string name)
        {
            if (name != null && name.EndsWith("Coral Rafter", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD1CoralRafter();
                return;
            }

            if (string.Equals(name, "Wailing Spirit", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD1WailingSpirit();
                return;
            }

            if (string.Equals(name, "Smelly Weaver", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD1SmellyWeaver();
                return;
            }

            if (string.Equals(name, "Crippler of Destiny", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD1CripplerDestiny();
                return;
            }

            if (string.Equals(name, "Croaker of Desolation", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD1CroakerDesolation();
                return;
            }

            if (string.Equals(name, "Croaker of Solitude", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD1CroakerSolitude();
                return;
            }

            if (string.Equals(name, "Havaris", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD1Havaris();
            }
        }

        private void EnsureNascenceDungeon2Loot(string name)
        {
            if (string.Equals(name, "Bound Dryad", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD2BoundDryad();
                return;
            }

            if (string.Equals(name, "Infernal Vortexoid", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD2InfernalVortexoid();
                return;
            }

            if (string.Equals(name, "Malah-Fama", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD2MalahFama();
                return;
            }

            if (string.Equals(name, "Weaver of Malice", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD2WeaverOfMalice();
                return;
            }

            if (string.Equals(name, "Croaker of Solitude", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD2CroakerSolitude();
                return;
            }

            if (string.Equals(name, "Burning Shadow", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD2BurningShadow();
                return;
            }

            if (string.Equals(name, "Icy Shadow", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD2IcyShadow();
                return;
            }

            if (string.Equals(name, "Smelly Weaver", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD2SmellyWeaver();
                return;
            }

            if (string.Equals(name, "Havaris", StringComparison.OrdinalIgnoreCase))
            {
                this.EnsureNascenceD2Havaris();
            }
        }

        private void EnsureNascenceD2BoundDryad()
        {
            const string tableKey = "captured.nascence-d2.bound-dryad";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD2LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.dryad.single.a",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.dryad.single.a", 232816, 232817, 17, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.dryad.single.b",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.dryad.single.b", 232822, 232823, 24, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.dryad.multi.a",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.dryad.multi.a", 225981, 225982, 15, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.dryad.multi.a", 232822, 232823, 15, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.dryad.multi.a", 223423, 223424, 25, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.dryad.multi.b",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.dryad.multi.b", 223421, 223422, 19, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.dryad.multi.b", 225977, 225978, 18, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260825-094236.dryad.empty", 0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D2 Bound Dryad captured corpse",
                NascenceD2BoundDryadProfileKey,
                snapshots,
                NascenceD2LootEvidence);
        }

        private void EnsureNascenceD2InfernalVortexoid()
        {
            const string tableKey = "captured.nascence-d2.infernal-vortexoid";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD2LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.vortexoid.a",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.a", 211242, 211243, 17, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.a", 232909, 232910, 16, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.a", 232839, 232840, 17, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.vortexoid.b",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.b", 211236, 211237, 18, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.b", 211227, 211228, 16, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.b", 225983, 225984, 21, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.b", 232834, 232835, 24, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.b", 225977, 225978, 16, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.vortexoid.c",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.c", 211230, 211231, 24, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.c", 229091, 229091, 15, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.c", 232816, 232817, 18, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.vortexoid.d",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.d", 232909, 232910, 18, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.d", 225987, 225988, 21, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.vortexoid.d", 223421, 223422, 22, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D2 Infernal Vortexoid captured corpse",
                NascenceD2InfernalVortexoidProfileKey,
                snapshots,
                NascenceD2LootEvidence);
        }

        private void EnsureNascenceD2MalahFama()
        {
            const string tableKey = "captured.nascence-d2.malah-fama";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD2LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.malah.a",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.malah.a", 223423, 223424, 15, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.malah.a", 225977, 225978, 15, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.malah.b",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.malah.b", 232828, 232829, 17, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.malah.c",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.malah.c", 223423, 223424, 21, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.malah.c", 232816, 232817, 17, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.malah.d",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.malah.d", 225975, 225976, 25, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.malah.d", 232834, 232835, 21, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260825-094236.malah.empty", 0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D2 Malah-Fama captured corpse",
                NascenceD2MalahFamaProfileKey,
                snapshots,
                NascenceD2LootEvidence);
        }

        private void EnsureNascenceD2WeaverOfMalice()
        {
            const string tableKey = "captured.nascence-d2.weaver-of-malice";
            if (!this.registry.ContainsTable(tableKey))
            {
                const string e = NascenceD2LootEvidence;
                ObservedCorpseSnapshotDefinition[] snapshots =
                    {
                        ObservedCorpseSnapshot(
                            e,
                            "capture.20260825-094236.weaver.item",
                            0,
                            ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.weaver.item", 214789, 214789, 1, 1)),
                        ObservedCorpseSnapshot(e, "capture.20260825-094236.weaver.empty", 0),
                    };

                this.RegisterNascenceObservedCorpseTable(
                    tableKey,
                    "Nascence D2 Weaver of Malice captured corpse",
                    NascenceD2WeaverOfMaliceProfileKey,
                    snapshots,
                    NascenceD2LootEvidence);
            }

            this.EnsureNascenceCompactMessageDatadiscOnTable(
                "captured.nascence-d2.weaver-of-malice",
                NascenceWeaverOfMaliceDatadiscItemId);
        }

        private void EnsureNascenceD2CroakerSolitude()
        {
            const string tableKey = "captured.nascence-d2.croaker-solitude";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD2LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.croaker.a",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.croaker.a", 225983, 225984, 15, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.croaker.a", 232839, 232840, 23, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.croaker.b",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.croaker.b", 225975, 225976, 21, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.croaker.c",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.croaker.c", 232822, 232823, 15, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D2 Croaker of Solitude captured corpse",
                NascenceD2CroakerSolitudeProfileKey,
                snapshots,
                NascenceD2LootEvidence);
        }

        private void EnsureNascenceD2BurningShadow()
        {
            const string tableKey = "captured.nascence-d2.burning-shadow";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD2LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.burning",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.burning", 225977, 225978, 18, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D2 Burning Shadow captured corpse",
                NascenceD2BurningShadowProfileKey,
                snapshots,
                NascenceD2LootEvidence);
        }

        private void EnsureNascenceD2IcyShadow()
        {
            const string tableKey = "captured.nascence-d2.icy-shadow";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD2LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.icy.a",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.icy.a", 225977, 225978, 19, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.icy.a", 223423, 223424, 22, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.icy.b",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.icy.b", 223421, 223422, 15, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.icy.c",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.icy.c", 232828, 232829, 21, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D2 Icy Shadow captured corpse",
                NascenceD2IcyShadowProfileKey,
                snapshots,
                NascenceD2LootEvidence);
        }

        private void EnsureNascenceD2SmellyWeaver()
        {
            const string tableKey = "captured.nascence-d2.smelly-weaver";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD2LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(e, "capture.20260825-094236.smelly.empty", 0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D2 Smelly Weaver captured corpse",
                NascenceD2SmellyWeaverProfileKey,
                snapshots,
                NascenceD2LootEvidence);
        }

        private void EnsureNascenceD2Havaris()
        {
            const string tableKey = "captured.nascence-d2.havaris";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD2LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.havaris.inventory",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.havaris.inventory", 230041, 230041, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.havaris.inventory", 230172, 230172, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.havaris.inventory", 223575, 223576, 47, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.havaris.inventory", 225983, 225984, 54, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D2 Havaris boss captured corpse",
                NascenceD2HavarisProfileKey,
                snapshots,
                NascenceD2LootEvidence);
        }

        private void EnsureNascenceD1Havaris()
        {
            const string tableKey = "captured.nascence-d1.havaris";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD1HavarisLootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260825-094236.havaris.inventory",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.havaris.inventory", 230041, 230041, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.havaris.inventory", 230172, 230172, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.havaris.inventory", 223575, 223576, 47, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260825-094236.havaris.inventory", 225983, 225984, 54, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260824-175852.havaris.gems.bruised-burning",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.bruised-burning", 168469, 168470, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.bruised-burning", 168509, 168510, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.bruised-burning", 225981, 225982, 22, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260824-175852.havaris.gems.corroded-eternal",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.corroded-eternal", 168793, 168794, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.corroded-eternal", 165385, 165386, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.corroded-eternal", 223423, 223424, 22, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260824-175852.havaris.gems.frozen-moebius",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.frozen-moebius", 168616, 168617, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.frozen-moebius", 168428, 168429, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.frozen-moebius", 42640, 42641, 22, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260824-175852.havaris.gems.jagged-rainbow",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.jagged-rainbow", 168713, 168714, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.jagged-rainbow", 168753, 168754, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.jagged-rainbow", 225979, 225980, 18, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260824-175852.havaris.gems.searing-silent",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.searing-silent", 168549, 168550, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.searing-silent", 168839, 168840, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.searing-silent", 232839, 232840, 23, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260824-175852.havaris.gems.bruised-corroded",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.bruised-corroded", 168469, 168470, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.bruised-corroded", 168793, 168794, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.bruised-corroded", 225981, 225982, 22, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260824-175852.havaris.gems.eternal-frozen",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.eternal-frozen", 165385, 165386, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.eternal-frozen", 168616, 168617, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.eternal-frozen", 223423, 223424, 22, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260824-175852.havaris.gems.moebius-jagged",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.moebius-jagged", 168428, 168429, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.moebius-jagged", 168713, 168714, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.moebius-jagged", 42640, 42641, 22, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260824-175852.havaris.gems.rainbow-searing",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.rainbow-searing", 168753, 168754, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.rainbow-searing", 168549, 168550, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.rainbow-searing", 232822, 232823, 23, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260824-175852.havaris.gems.silent-burning",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.silent-burning", 168839, 168840, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.silent-burning", 168509, 168510, 80, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260824-175852.havaris.gems.silent-burning", 225979, 225980, 18, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D1 Havaris boss captured corpse",
                NascenceD1HavarisProfileKey,
                snapshots,
                NascenceD1HavarisLootEvidence);
        }

        private void EnsureNascenceD1CoralRafter()
        {
            const string tableKey = "captured.nascence-d1.coral-rafter";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD1LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.rafter.FD9001",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.rafter.FD9001", 232839, 232840, 23, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.rafter.FD9004",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.rafter.FD9004", 225981, 225982, 22, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.rafter.FD9004", 225979, 225980, 18, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.rafter.FD9005",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.rafter.FD9005", 232822, 232823, 23, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260823-171238.rafter.empty", 0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D1 Coral Rafter captured corpse",
                NascenceD1CoralRafterProfileKey,
                snapshots,
                NascenceD1LootEvidence);
        }

        private void EnsureNascenceD1WailingSpirit()
        {
            const string tableKey = "captured.nascence-d1.wailing-spirit";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD1LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.spirit.FD9002",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.spirit.FD9002", 232720, 232721, 19, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.spirit.FD9002", 232714, 232715, 19, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.spirit.FD9004",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.spirit.FD9004", 232702, 232703, 17, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.spirit.FD9006",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.spirit.FD9006", 214940, 229944, 15, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.spirit.FD9006", 232720, 232721, 22, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260823-171238.spirit.empty", 0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D1 Wailing Spirit captured corpse",
                NascenceD1WailingSpiritProfileKey,
                snapshots,
                NascenceD1LootEvidence);
        }

        private void EnsureNascenceD1SmellyWeaver()
        {
            const string tableKey = "captured.nascence-d1.smelly-weaver";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD1LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.weaver.FD9002",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.weaver.FD9002", 42640, 42641, 20, 1)),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D1 Smelly Weaver captured corpse",
                NascenceD1SmellyWeaverProfileKey,
                snapshots,
                NascenceD1LootEvidence);
        }

        private void EnsureNascenceD1CripplerDestiny()
        {
            const string tableKey = "captured.nascence-d1.crippler-destiny";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD1LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.crippler.FD9003",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.crippler.FD9003", 225981, 225982, 18, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.crippler.FD9002",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.crippler.FD9002", 225981, 225982, 22, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.crippler.FD9002", 225977, 225978, 21, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260823-171238.crippler.empty", 0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D1 Crippler of Destiny captured corpse",
                NascenceD1CripplerDestinyProfileKey,
                snapshots,
                NascenceD1LootEvidence);
        }

        private void EnsureNascenceD1CroakerDesolation()
        {
            const string tableKey = "captured.nascence-d1.croaker-desolation";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD1LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.croaker-des.FD9008",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.croaker-des.FD9008", 223423, 223424, 17, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.croaker-des.FD9008", 225975, 225976, 16, 1)),
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.croaker-des.FD9017",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.croaker-des.FD9017", 225983, 225984, 18, 1),
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.croaker-des.FD9017", 225979, 225980, 24, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260823-171238.croaker-des.empty", 0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D1 Croaker of Desolation captured corpse",
                NascenceD1CroakerDesolationProfileKey,
                snapshots,
                NascenceD1LootEvidence);
        }

        private void EnsureNascenceD1CroakerSolitude()
        {
            const string tableKey = "captured.nascence-d1.croaker-solitude";
            if (this.registry.ContainsTable(tableKey))
            {
                return;
            }

            const string e = NascenceD1LootEvidence;
            ObservedCorpseSnapshotDefinition[] snapshots =
                {
                    ObservedCorpseSnapshot(
                        e,
                        "capture.20260823-171238.croaker-sol.FD9007",
                        0,
                        ObservedCorpseSnapshotEntry(e, "capture.20260823-171238.croaker-sol.FD9007", 225981, 225982, 18, 1)),
                    ObservedCorpseSnapshot(e, "capture.20260823-171238.croaker-sol.empty", 0),
                };

            this.RegisterNascenceObservedCorpseTable(
                tableKey,
                "Nascence D1 Croaker of Solitude captured corpse",
                NascenceD1CroakerSolitudeProfileKey,
                snapshots,
                NascenceD1LootEvidence);
        }

        private void RegisterNascenceObservedCorpseTable(
            string tableKey,
            string displayName,
            string profileKey,
            ObservedCorpseSnapshotDefinition[] snapshots,
            string evidence)
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
                    Evidence = evidence,
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
                    Evidence = evidence,
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

        private void EnsureLegacyTarget(
            ICharacter target,
            string profileKey,
            int playfieldId)
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
            bool hasDocumentedInnerSanctumLoot =
                DocumentedInnerSanctumLootDefinitions
                    .DropsForDisplayName(playfieldId, target.Name)
                    .Any(value => value.IsActive);
            bool hasDocumentedForemansLoot =
                DocumentedForemansLootDefinitions
                    .DropsForDisplayName(playfieldId, target.Name)
                    .Any();
            bool hasDocumentedStepsOfMadnessLoot =
                DocumentedStepsOfMadnessLootDefinitions
                    .DropsForDisplayName(playfieldId, target.Name)
                    .Any(value => value.IsActive);
            bool hasDocumentedSmugglersDenLoot =
                DocumentedSmugglersDenLootDefinitions
                    .DropsForDisplayName(playfieldId, target.Name)
                    .Any(value => value.IsActive);
            bool hasDocumentedCyborgBarracksLoot =
                DocumentedCyborgBarracksLootDefinitions
                    .DropsForDisplayName(playfieldId, target.Name)
                    .Any(value => value.IsActive);
            bool hasDocumentedCryptOfHomeLoot =
                DocumentedCryptOfHomeLootDefinitions
                    .DropsForDisplayName(playfieldId, target.Name)
                    .Any(value => value.IsActive);
            if (matches.Length == 0
                && !hasCredits
                && !hasDocumentedInnerSanctumLoot
                && !hasDocumentedForemansLoot
                && !hasDocumentedStepsOfMadnessLoot
                && !hasDocumentedSmugglersDenLoot
                && !hasDocumentedCyborgBarracksLoot
                && !hasDocumentedCryptOfHomeLoot) return;
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
            string repositoryEvidence = debugMatches.Length > 0
                ? "combat-test-catalog"
                : "mobtemplate/mobdroptable";
            string documentedEvidence = hasDocumentedInnerSanctumLoot
                ? DocumentedInnerSanctumLootDefinitions.DocumentedLootSourceUrl
                : (hasDocumentedForemansLoot
                    ? DocumentedForemansLootDefinitions.DocumentedLootSourceUrl
                    : (hasDocumentedStepsOfMadnessLoot
                        ? DocumentedStepsOfMadnessLootDefinitions.DocumentedLootSourceUrl
                        : (hasDocumentedSmugglersDenLoot
                            ? DocumentedSmugglersDenLootDefinitions.DocumentedLootSourceUrl
                            : (hasDocumentedCyborgBarracksLoot
                                ? DocumentedCyborgBarracksLootDefinitions.DocumentedLootSourceUrl
                                : (hasDocumentedCryptOfHomeLoot
                                    ? DocumentedCryptOfHomeLootDefinitions.DocumentedLootSourceUrl
                                    : null)))));
            string evidence = matches.Length > 0
                ? repositoryEvidence
                : (!string.IsNullOrWhiteSpace(documentedEvidence)
                    ? documentedEvidence
                    : "captured-credit-range");
            if (matches.Length > 0 && !string.IsNullOrWhiteSpace(documentedEvidence))
            {
                evidence += "; " + documentedEvidence;
            }

            var table = new LootTableDefinition
            {
                LootTableKey = tableKey,
                DisplayName = target.Name + " legacy DB loot",
                TableType = LootTableType.EnemyType,
                RollGroups = groups.ToArray(),
                CreditsPolicy = hasCredits
                    ? CreditsRange(creditMinimum, creditMaximum, LootEvidenceConfidence.ProvenRepository)
                    : new CreditsPolicyDefinition { Mode = CreditsPolicyMode.Unresolved, Evidence = LootEvidenceConfidence.Unresolved },
                QualityPolicy = hasDocumentedInnerSanctumLoot
                    ? "legacy-range-check; inner-sanctum-wiki-fixed-quality"
                    : (hasDocumentedForemansLoot
                        ? "legacy-range-check; foremans-wiki-fixed-and-range-quality"
                        : (hasDocumentedStepsOfMadnessLoot
                            ? "legacy-range-check; steps-of-madness-wiki-fixed-quality"
                            : (hasDocumentedSmugglersDenLoot
                                ? "legacy-range-check; smugglers-den-wiki-fixed-quality"
                                : (hasDocumentedCyborgBarracksLoot
                                    ? "legacy-range-check; cyborg-barracks-wiki-fixed-and-range-quality"
                                    : (hasDocumentedCryptOfHomeLoot
                                        ? "legacy-range-check; crypt-of-home-wiki-fixed-quality"
                                        : "legacy-range-check"))))),
                Evidence = evidence,
                Confidence = matches.Length > 0
                    ? LootEvidenceConfidence.ProvenRepository
                    : LootEvidenceConfidence.CommunityDocumented,
                Enabled = true
            };
            DocumentedInnerSanctumLootDefinitions.ApplyDocumentedBossLoot(
                table,
                playfieldId,
                target.Name);
            DocumentedForemansLootDefinitions.ApplyDocumentedMembership(
                table,
                playfieldId,
                target.Name);
            DocumentedStepsOfMadnessLootDefinitions.ApplyDocumentedLoot(
                table,
                playfieldId,
                target.Name);
            DocumentedSmugglersDenLootDefinitions.ApplyDocumentedLoot(
                table,
                playfieldId,
                target.Name);
            DocumentedCyborgBarracksLootDefinitions.ApplyDocumentedLoot(
                table,
                playfieldId,
                target.Name);
            DocumentedCryptOfHomeLootDefinitions.ApplyDocumentedLoot(
                table,
                playfieldId,
                target.Name);
            this.registry.RegisterTable(table);
            this.registry.RegisterAssignment(new LootAssignmentDefinition
            {
                AssignmentKey = tableKey,
                TargetType = LootAssignmentTargetType.EnemyType,
                TargetKey = profileKey,
                LootTableKey = tableKey,
                PlayfieldId = hasDocumentedInnerSanctumLoot
                    ? (int?)DocumentedInnerSanctumLootDefinitions.PlayfieldInstance
                    : (hasDocumentedForemansLoot
                        ? (int?)DocumentedForemansLootDefinitions.PlayfieldInstance
                        : (hasDocumentedStepsOfMadnessLoot
                            ? (int?)DocumentedStepsOfMadnessLootDefinitions.PlayfieldInstance
                            : (hasDocumentedSmugglersDenLoot
                                ? (int?)DocumentedSmugglersDenLootDefinitions.PlayfieldInstance
                                : (hasDocumentedCyborgBarracksLoot
                                    ? (int?)DocumentedCyborgBarracksLootDefinitions.PlayfieldInstance
                                    : (hasDocumentedCryptOfHomeLoot
                                        ? (int?)DocumentedCryptOfHomeLootDefinitions.PlayfieldInstance
                                        : null))))),
                Priority = 0,
                Evidence = evidence,
                Confidence = matches.Length > 0
                    ? LootEvidenceConfidence.ProvenRepository
                    : LootEvidenceConfidence.CommunityDocumented,
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

        private static LootEntryDefinition KillerArmorEntry(int itemId, string selectionKey)
        {
            return new LootEntryDefinition
            {
                SelectionKey = selectionKey,
                ItemTemplateId = itemId,
                HighItemTemplateId = itemId,
                FixedQuality = 21,
                MinimumQuality = 21,
                MaximumQuality = 25,
                MinimumQuantity = 1,
                MaximumQuantity = 1,
                Weight = 1,
                DropChanceBasisPoints = 10000,
                Semantics = LootSemantics.WeightedDocumented,
                Evidence = LootEvidenceConfidence.ProvenCapture,
                EvidenceReference = "capture.20260826-055143.hesosas.killers-armor"
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
