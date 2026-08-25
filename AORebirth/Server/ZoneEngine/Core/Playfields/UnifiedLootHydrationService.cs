namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Core.Playfields;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using Utility;

    /// <summary>
    /// Service unifié : hydrate LootTableRegistry depuis playfield_configurations + DB.
    /// Remplace hardcodé GlobalLootRuntimeService + captures.
    /// </summary>
    internal sealed class UnifiedLootHydrationService
    {
        private readonly LootTableRegistry registry;
        private readonly int playfieldId;

        internal UnifiedLootHydrationService(LootTableRegistry registry, int playfieldId)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.playfieldId = playfieldId;
        }

        /// <summary>
        /// Hydrate loot tables et assignations pour ce playfield depuis DB.
        /// </summary>
        internal void HydrateFromDatabase()
        {
            // 1) Charger tables globales
            var globalTables = LootTableDefinitionDao.Instance.GetGlobal().ToList();
            foreach (var dbTable in globalTables)
            {
                try
                {
                    var table = this.ConvertDBTableToDefinition(dbTable);
                    this.registry.RegisterTable(table);
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        $"Failed to register global loot table {dbTable.LootTableKey}: {ex.Message}");
                }
            }

            // 2) Charger tables playfield-spécifiques
            var pfTables = LootTableDefinitionDao.Instance.GetByPlayfieldId(this.playfieldId).ToList();
            foreach (var dbTable in pfTables)
            {
                try
                {
                    var table = this.ConvertDBTableToDefinition(dbTable);
                    this.registry.RegisterTable(table);
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        $"Failed to register playfield loot table {dbTable.LootTableKey}: {ex.Message}");
                }
            }

            // 3) Charger assignations globales
            var globalAssignments = LootAssignmentDao.Instance.GetGlobal().ToList();
            foreach (var dbAssign in globalAssignments)
            {
                try
                {
                    var assignment = this.ConvertDBAssignmentToDefinition(dbAssign);
                    this.registry.RegisterAssignment(assignment);
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        $"Failed to register global loot assignment {dbAssign.AssignmentKey}: {ex.Message}");
                }
            }

            // 4) Charger assignations playfield-spécifiques
            var pfAssignments = LootAssignmentDao.Instance.GetByPlayfieldId(this.playfieldId).ToList();
            foreach (var dbAssign in pfAssignments)
            {
                try
                {
                    var assignment = this.ConvertDBAssignmentToDefinition(dbAssign);
                    this.registry.RegisterAssignment(assignment);
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        $"Failed to register playfield loot assignment {dbAssign.AssignmentKey}: {ex.Message}");
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                $"Playfield {this.playfieldId} loot hydration complete.");
        }

        private LootTableDefinition ConvertDBTableToDefinition(DBLootTableDefinition db)
        {
            // Parse JSON fields, convert to C# objects
            return new LootTableDefinition
            {
                LootTableKey = db.LootTableKey,
                DisplayName = db.DisplayName,
                TableType = (LootTableType)Enum.Parse(typeof(LootTableType), db.TableType),
                RollGroups = this.LoadRollGroups(db.LootTableKey),
                ObservedCorpseSnapshots = this.LoadObservedSnapshots(db.LootTableKey),
                CreditsPolicy = new CreditsPolicyDefinition
                {
                    Mode = (CreditsPolicyMode)Enum.Parse(typeof(CreditsPolicyMode), db.CreditsPolicyMode),
                    MinimumCredits = db.CreditsMin,
                    MaximumCredits = db.CreditsMax,
                    ObservedCredits = this.ParseObservedCredits(db.CreditsObservedJson)
                },
                QualityPolicy = db.QualityPolicy,
                Evidence = db.EvidenceJson,
                Confidence = (LootEvidenceConfidence)Enum.Parse(typeof(LootEvidenceConfidence), db.Confidence),
                ItemPoolUnresolved = db.ItemPoolUnresolved,
                Enabled = db.Enabled
            };
        }

        private LootGroupDefinition[] LoadRollGroups(string lootTableKey)
        {
            // SELECT * FROM loot_roll_groups WHERE loot_table_key = ?
            // For each group, load loot_entries
            // Return LootGroupDefinition[]
            return new LootGroupDefinition[0]; // Stub
        }

        private ObservedCorpseSnapshotDefinition[] LoadObservedSnapshots(string lootTableKey)
        {
            // SELECT * FROM loot_observed_corpse_snapshots WHERE loot_table_key = ?
            // For each snapshot, load loot_snapshot_entries
            // Return ObservedCorpseSnapshotDefinition[]
            return new ObservedCorpseSnapshotDefinition[0]; // Stub
        }

        private int[] ParseObservedCredits(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new int[0];
            // Parse JSON array
            return new int[0]; // Stub
        }

        private LootAssignmentDefinition ConvertDBAssignmentToDefinition(DBLootAssignment db)
        {
            return new LootAssignmentDefinition
            {
                AssignmentKey = db.AssignmentKey,
                TargetType = (LootAssignmentTargetType)Enum.Parse(typeof(LootAssignmentTargetType), db.TargetType),
                TargetKey = db.TargetKey,
                LootTableKey = db.LootTableKey,
                PlayfieldId = db.PlayfieldId,
                EncounterKey = db.EncounterKey,
                MinimumLevel = db.MinLevel,
                MaximumLevel = db.MaxLevel,
                Priority = db.Priority,
                Evidence = db.Evidence,
                Confidence = (LootEvidenceConfidence)Enum.Parse(typeof(LootEvidenceConfidence), db.Confidence),
                Enabled = db.Enabled
            };
        }
    }
}
