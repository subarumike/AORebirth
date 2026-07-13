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
    using Utility;

    internal sealed class GlobalLootRuntimeService
    {
        private const string CleaningRobotProfileKey = "captured.arete.cleaning-robot";
        private const int CleaningRobotMonsterData = 297023;
        private const int CleaningRobotCredits = 5;
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

        internal LootGenerationResult GenerateDeterministic(LootGenerationContext context, int seed)
        {
            context.Seed = seed;
            return this.generator.Generate(context, new SeededLootRandomSource(seed));
        }

        private LootGenerationContext BuildContext(ICharacter target, int playfieldId)
        {
            OrdinaryEnemyRuntimeDefinition ordinary;
            bool hasOrdinary = OrdinaryEnemyRuntimeRegistry.TryGet(target.Identity.Instance, out ordinary);
            bool owned = PetCombatRules.IsPlayerOwnedPet(target);
            return new LootGenerationContext
            {
                EnemyProfileKey = owned ? "owned-summon" : hasOrdinary ? ordinary.Profile.ProfileKey : LegacyProfileKey(target),
                EnemyIdentityInstance = target.Identity.Instance,
                MonsterData = target.Stats[StatIds.monsterdata].Value,
                FamilyKey = hasOrdinary ? ordinary.Profile.FamilyKey : "legacy." + target.Stats[StatIds.npcfamily].Value,
                Level = target.Stats[StatIds.level].Value,
                PlayfieldId = playfieldId,
                SpawnKey = hasOrdinary ? ordinary.Spawn.SpawnKey : null,
                IsOwnedSummon = owned
            };
        }

        private void EnsureDefinitions(ICharacter target, LootGenerationContext context)
        {
            if (context.IsOwnedSummon) return;
            lock (this.sync)
            {
                if (context.MonsterData == CleaningRobotMonsterData)
                {
                    this.EnsureCleaningRobot();
                    context.EnemyProfileKey = CleaningRobotProfileKey;
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

        private void EnsureOrdinary(OrdinaryEnemyProfile profile, int targetLevel)
        {
            bool levelSpecificCredits = profile.Loot.LevelCreditRules.Length > 0;
            string levelSuffix = levelSpecificCredits ? ".level." + targetLevel.ToString(CultureInfo.InvariantCulture) : string.Empty;
            string tableKey = "ordinary." + profile.ProfileKey + levelSuffix;
            string assignmentKey = "ordinary." + profile.ProfileKey + levelSuffix;
            if (this.registry.ContainsTable(tableKey)) return;
            LootGroupDefinition[] groups = profile.Loot.Entries.Select((entry, index) =>
                new LootGroupDefinition
                {
                    LootGroupKey = "entry." + index.ToString(CultureInfo.InvariantCulture),
                    RollMode = entry.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven
                        ? LootRollMode.Guaranteed : LootRollMode.Independent,
                    RollCount = 1,
                    DropChanceBasisPoints = 10000,
                    Entries = new[] { OrdinaryEntry(entry) },
                    Conditions = new string[0]
                }).ToArray();
            var table = new LootTableDefinition
            {
                LootTableKey = tableKey,
                DisplayName = profile.DisplayName,
                TableType = LootTableType.EnemyType,
                RollGroups = groups,
                CreditsPolicy = OrdinaryCredits(profile.Loot, targetLevel),
                QualityPolicy = "captured-fixed",
                Evidence = profile.Loot.Evidence.ToString(),
                Confidence = profile.Loot.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven
                    ? LootEvidenceConfidence.ProvenCapture
                    : profile.Loot.Evidence == OrdinaryEnemyLootEvidence.ObservedAvailableLoot
                        ? LootEvidenceConfidence.ObservedAvailableLoot
                        : LootEvidenceConfidence.Unresolved,
                Enabled = true
            };
            this.registry.RegisterTable(table);
            this.registry.RegisterAssignment(new LootAssignmentDefinition
            {
                AssignmentKey = assignmentKey,
                TargetType = LootAssignmentTargetType.EnemyType,
                TargetKey = profile.ProfileKey,
                LootTableKey = tableKey,
                MinimumLevel = levelSpecificCredits ? (int?)targetLevel : null,
                MaximumLevel = levelSpecificCredits ? (int?)targetLevel : null,
                Priority = 0,
                Evidence = table.Evidence,
                Confidence = table.Confidence,
                Enabled = true,
                Conditions = new string[0]
            });
        }

        private static LootEntryDefinition OrdinaryEntry(OrdinaryEnemyLootEntry entry)
        {
            bool guaranteed = entry.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven;
            return new LootEntryDefinition
            {
                ItemTemplateId = entry.LowId,
                HighItemTemplateId = entry.HighId,
                FixedQuality = entry.Quality,
                MinimumQuality = entry.Quality,
                MaximumQuality = entry.Quality,
                MinimumQuantity = 1,
                MaximumQuantity = 1,
                Weight = 1,
                DropChanceBasisPoints = guaranteed ? 10000 : entry.BasisPoints,
                UniquePerCorpse = true,
                Semantics = guaranteed ? LootSemantics.GuaranteedProven : LootSemantics.ObservedAvailable,
                Evidence = guaranteed ? LootEvidenceConfidence.ProvenCapture : LootEvidenceConfidence.ObservedAvailableLoot,
                EvidenceReference = entry.Evidence.ToString()
            };
        }

        private static CreditsPolicyDefinition OrdinaryCredits(OrdinaryEnemyLootProfile loot, int targetLevel)
        {
            OrdinaryEnemyLevelCreditRule level = loot.LevelCreditRules.FirstOrDefault(x => x.EnemyLevel == targetLevel);
            if (level != null) return CreditsRange(level.MinimumCredits, level.MaximumCredits, LootEvidenceConfidence.ProvenCapture);
            if (loot.CreditEvidence == OrdinaryEnemyEvidenceState.Observed
                && loot.MinimumCredits.HasValue && loot.MaximumCredits.HasValue)
                return CreditsRange(loot.MinimumCredits.Value, loot.MaximumCredits.Value, LootEvidenceConfidence.ProvenCapture);
            return new CreditsPolicyDefinition { Mode = CreditsPolicyMode.Unresolved, Evidence = LootEvidenceConfidence.Unresolved };
        }

        private void EnsureCleaningRobot()
        {
            const string tableKey = "captured.arete.cleaning-robot";
            if (this.registry.ContainsTable(tableKey)) return;
            int[][] outcomes =
            {
                new[] { 42620 }, new int[0], new[] { 36779, 84142 }, new int[0], new[] { 297289 },
                new int[0], new int[0], new[] { 70558, 155685 }, new[] { 297289, 150306 },
                new int[0], new[] { 155666 }, new int[0], new[] { 70564 }, new[] { 155666 },
                new[] { 155687 }, new[] { 70565 }, new[] { 155684 }, new int[0]
            };
            var entries = new List<LootEntryDefinition>();
            int emptyWeight = 0;
            for (int index = 0; index < outcomes.Length; index++)
            {
                if (outcomes[index].Length == 0) { emptyWeight++; continue; }
                foreach (int itemId in outcomes[index]) entries.Add(FixedEntry(itemId, 1, "outcome." + index, 1));
            }
            this.registry.RegisterTable(new LootTableDefinition
            {
                LootTableKey = tableKey,
                DisplayName = "Malfunctioning Cleaning Robot captured outcomes",
                TableType = LootTableType.EnemyType,
                RollGroups = new[]
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
                CreditsPolicy = CreditsRange(CleaningRobotCredits, CleaningRobotCredits, LootEvidenceConfidence.ProvenCapture),
                QualityPolicy = "captured-fixed",
                Evidence = "live-capture-20260629-142800",
                Confidence = LootEvidenceConfidence.ProvenCapture,
                Enabled = true
            });
            this.registry.RegisterAssignment(new LootAssignmentDefinition
            {
                AssignmentKey = tableKey,
                TargetType = LootAssignmentTargetType.EnemyType,
                TargetKey = CleaningRobotProfileKey,
                LootTableKey = tableKey,
                Priority = 0,
                Evidence = "live-capture-20260629-142800",
                Confidence = LootEvidenceConfidence.ProvenCapture,
                Enabled = true,
                Conditions = new string[0]
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
