namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using AORebirth.Core.Playfields;

    #endregion

    /// <summary>
    /// UNIFIED hydration service: replaces 15+ ContentModule implementations.
    /// Single SQL-driven path for all playfields.
    /// 
    /// Orchestrates loading of:
    /// - Ordinary enemy profiles + spawns
    /// - Patrol routes
    /// - Vendors
    /// - Static dynels (doors, objects)
    /// </summary>
    internal sealed class UnifiedPlayfieldContentHydrationService
    {
        // Toggle NEW dynamic-hydration diagnostics independently from OLD static diagnostics.
        private const bool EnableNewContentConsoleDiagnostics = true;

        private readonly Playfield playfield;
        private readonly Identity playfieldIdentity;
        private readonly PlayfieldDynelRegistry dynelRegistry;
        private readonly Action<ICharacter> activateNpc;

        internal UnifiedPlayfieldContentHydrationService(
            Playfield playfield,
            Identity playfieldIdentity,
            PlayfieldDynelRegistry dynelRegistry,
            Action<ICharacter> activateNpc)
        {
            this.playfield = playfield ?? throw new ArgumentNullException(nameof(playfield));
            this.playfieldIdentity = playfieldIdentity;
            this.dynelRegistry = dynelRegistry ?? throw new ArgumentNullException(nameof(dynelRegistry));
            this.activateNpc = activateNpc ?? throw new ArgumentNullException(nameof(activateNpc));
        }

        /// <summary>
        /// Single entry point: load all content from DB based on playfield_configurations.
        /// </summary>
        internal void HydrateFromDatabase()
        {
            string stage = "configuration DAO";
            ConsoleLog($"[NEW unified hydration][DB] PF {this.playfieldIdentity.Instance} START");
            try
            {
            // 1) Load configuration for this playfield
            ConsoleLog($"[NEW unified hydration][DB] PF {this.playfieldIdentity.Instance} -> PlayfieldConfigurationDao.GetByPlayfieldId");
            var config = PlayfieldConfigurationDao.Instance.GetByPlayfieldId(
                this.playfieldIdentity.Instance);
            ConsoleLog($"[NEW unified hydration][DB] PF {this.playfieldIdentity.Instance} <- configuration {(config == null ? "NULL" : "FOUND")}");

            if (config == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    $"No playfield_configurations entry for PF {this.playfieldIdentity.Instance}, skipping unified hydration.");
                return;
            }

            if (!config.Enabled)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    $"Playfield {this.playfieldIdentity.Instance} disabled in playfield_configurations.");
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                $"Starting unified hydration for PF {this.playfieldIdentity.Instance}");

            // 2) Hydrate ordinary enemies (if applicable)
            stage = "ordinary enemy spawn DAO";
            int npcCount = this.HydrateOrdinaryEnemies(config);

            // 3) Hydrate vendors
            stage = "vendor DAO";
            int shopCount = this.HydrateVendors(config);

            // 4) Hydrate static dynels
            stage = "static dynel DAO";
            int staticDynelCount = this.HydrateStaticDynels(config);

            string hydrationSummary =
                $"[Unified hydration] Playfield {this.playfieldIdentity.Instance}: " +
                $"NPCs/mobs={npcCount}, shops={shopCount}, static dynels={staticDynelCount}.";
            ConsoleLog(hydrationSummary);
            LogUtil.Debug(DebugInfoDetail.Engine, hydrationSummary);
            }
            catch (Exception ex)
            {
                ConsoleLog($"[NEW unified hydration][DB] PF {this.playfieldIdentity.Instance} FAILED stage={stage}: {ex}");
                throw;
            }
        }

        private int HydrateVendors(DBPlayfieldConfiguration config)
        {
            ConsoleLog($"[NEW unified hydration][DB] PF {this.playfieldIdentity.Instance} -> PlayfieldVendorDao.GetByPlayfieldId");
            var vendors = PlayfieldVendorDao.Instance
                .GetByPlayfieldId(this.playfieldIdentity.Instance)
                .ToList();
            int instantiated = 0;
            int failed = 0;

            foreach (var vendorDef in vendors)
            {
                try
                {
                    var vendor = this.InstantiateVendor(vendorDef);
                    vendor.Playfield = this.playfield;
                    this.dynelRegistry.Register(vendor);
                    instantiated++;
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        $"Failed to instantiate shop {vendorDef.VendorId}: {ex.Message}");
                    failed++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                $"PF {this.playfieldIdentity.Instance}: Shops = {instantiated} spawned, {failed} failed.");

            return instantiated;
        }

        private int HydrateOrdinaryEnemies(DBPlayfieldConfiguration config)
        {
            // Load all spawns for this playfield
            ConsoleLog($"[NEW unified hydration][DB] PF {this.playfieldIdentity.Instance} -> OrdinaryEnemySpawnDao.GetByPlayfieldId");
            var spawns = OrdinaryEnemySpawnDao.Instance
                .GetByPlayfieldId(this.playfieldIdentity.Instance)
                .ToList();
            ConsoleLog(
                $"[Unified hydration][DB] PF {this.playfieldIdentity.Instance} ordinary spawn rows={spawns.Count}");

            if (spawns.Count == 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    $"PF {this.playfieldIdentity.Instance}: no ordinary enemy spawns in DB.");
                return 0;
            }

            int instantiated = 0;
            int failed = 0;

            foreach (var spawn in spawns)
            {
                // Load profile
                ConsoleLog($"[NEW unified hydration][DB] PF {this.playfieldIdentity.Instance} -> OrdinaryEnemyProfileDao.GetByProfileKey key={spawn.ProfileKey}");
                var profile = OrdinaryEnemyProfileDao.Instance.GetByProfileKey(spawn.ProfileKey);

                if (profile == null)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        $"Profile '{spawn.ProfileKey}' not found for spawn {spawn.SpawnId}.");
                    failed++;
                    continue;
                }

                // Instantiate NPC
                try
                {
                    var npc = this.InstantiateOrdinaryEnemy(spawn, profile);

                    // Set patrol if applicable
                    if (spawn.PatrolRouteId.HasValue && spawn.PatrolRouteId.Value > 0)
                    {
                        this.AttachPatrolRoute(npc, spawn.PatrolRouteId.Value);
                    }

                    this.dynelRegistry.Register(npc);
                    this.activateNpc(npc);
                    this.DumpHydratedNpc(npc, spawn, profile);
                    ConsoleLog(
                        $"[Unified hydration][NPC] PF {this.playfieldIdentity.Instance} sourceSpawn={spawn.SpawnId} runtime={npc.Identity} activated");
                    instantiated++;
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        $"Failed to instantiate ordinary enemy spawn {spawn.SpawnId}: {ex.Message}");
                    ConsoleLog(
                        $"[Unified hydration][NPC] PF {this.playfieldIdentity.Instance} sourceSpawn={spawn.SpawnId} FAILED: {ex}");
                    failed++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                $"PF {this.playfieldIdentity.Instance}: Ordinary enemies = {instantiated} spawned, {failed} failed.");

            return instantiated;
        }

        private ICharacter InstantiateOrdinaryEnemy(
            DBOrdinaryEnemySpawn spawn,
            DBOrdinaryEnemyProfile profile)
        {
            // Create NPC entity
            int runtimeInstance = Pool.Instance.GetFreeInstance<Character>(1000000, IdentityType.CanbeAffected);
            var identity = new Identity
            {
                Type = IdentityType.CanbeAffected,
                Instance = runtimeInstance
            };
            var controller = new NPCController();
            var npc = new Character(this.playfield.Identity, identity, controller);
/*                Heading = new Quaternion(
                    spawn.OrientationX,
                    spawn.OrientationY,
                    spawn.OrientationZ,
                    spawn.OrientationW),
  */
            controller.Character = npc;
            npc.Read();
            npc.Playfield = this.playfield;
            npc.Name = profile.EnemyName;
            npc.Coordinates(
                new Coordinate
                {
                    x = spawn.PositionX,
                    y = spawn.PositionY,
                    z = spawn.PositionZ
                });
            npc.RawHeading =
                new AORebirth.Core.Vector.Quaternion(
                    spawn.OrientationX,
                    spawn.OrientationY,
                    spawn.OrientationZ,
                    spawn.OrientationW);


            // Assign stats from profile + spawn level
            int level = this.ResolveLevel(spawn);
            this.AssignNpcStats(npc, profile, level);

            return npc;
        }

        private int ResolveLevel(DBOrdinaryEnemySpawn spawn)
        {
            // Parse level_definition_key if present
            if (!string.IsNullOrWhiteSpace(spawn.LevelDefinitionKey))
            {
                // Format: "fixed:10" or "band:5-15"
                if (spawn.LevelDefinitionKey.StartsWith("fixed:"))
                {
                    if (int.TryParse(spawn.LevelDefinitionKey.Substring(6), out int fixedLevel))
                    {
                        return fixedLevel;
                    }
                }
                else if (spawn.LevelDefinitionKey.StartsWith("band:"))
                {
                    // "band:5-15" -> random between min-max
                    var range = spawn.LevelDefinitionKey.Substring(5).Split('-');
                    if (range.Length == 2
                        && int.TryParse(range[0], out int minLvl)
                        && int.TryParse(range[1], out int maxLvl))
                    {
                        return minLvl + (new Random()).Next(maxLvl - minLvl + 1);
                    }
                }
            }

            // Fallback to database fields
            if (spawn.MinLevel.HasValue && spawn.MaxLevel.HasValue)
            {
                return spawn.MinLevel.Value; // Or random in band
            }

            if (spawn.MinLevel.HasValue)
            {
                return spawn.MinLevel.Value;
            }

            return 1; // Default fallback
        }

        private void AssignNpcStats(
            ICharacter npc,
            DBOrdinaryEnemyProfile profile,
            int level)
        {
            // CalculateIP assumes valid one-based breed/profession values. A freshly
            // materialized NPC starts at zero, which underflows breed - 1 and causes
            // an IndexOutOfRangeException while any dependent stat is recalculated.
            npc.Stats[StatIds.breed].SetBaseValue(1);
            npc.Stats[StatIds.profession].SetBaseValue(1);

            // Set MonsterData for loot resolution
            npc.Stats[StatIds.monsterdata].Value = (int)profile.MonsterData;

            // Set level
            npc.Stats[StatIds.level].Value = (int)level;

            // Set family
            if (!string.IsNullOrWhiteSpace(profile.FamilyKey))
            {
                // Family is typically stored as enum, map from key if needed
                npc.Stats[StatIds.npcfamily].Value = 0; // TODO: resolve family key to ID
            }

            // Additional stats loading from combat profiles would happen here
            // For now, basic structure in place
        }

        private void AttachPatrolRoute(ICharacter npc, int routeId)
        {
            var route = NpcPatrolRouteDao.Instance.GetByRouteId(routeId);
            if (route == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    $"Patrol route {routeId} not found for NPC {npc.Identity}.");
                return;
            }

            var segments = NpcPatrolRouteDao.Instance.GetSegmentsForRoute(routeId)
                .OrderBy(s => s.SegmentIndex)
                .ToArray();

            if (segments.Length == 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    $"Patrol route {routeId} has no segments.");
                return;
            }

            // Convert to NpcPatrolReplaySegment[] and attach
            var replaySegments = segments
                .Select(s => new NpcPatrolReplaySegment(
                    s.DurationSeconds,
                    s.StartX,
                    s.StartY,
                    s.StartZ,
                    s.EndX,
                    s.EndY,
                    s.EndZ))
                .ToArray();

            var controller = npc.Controller as NPCController;
            if (controller != null)
            {
                controller.SetCapturedPatrolReplaySegments(
                    replaySegments,
                    route.UseRuntimeStart,
                    route.BatchZeroDelay);

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    $"Attached patrol route {routeId} ({segments.Length} segments) to NPC {npc.Identity}.");
            }
        }

        private Vendor InstantiateVendor(DBPlayfieldVendor vendorDef)
        {
            var identity = new Identity
            {
                Type = IdentityType.CanbeAffected,
                Instance = vendorDef.VendorId
            };

            Vendor vendor;
            if (!string.IsNullOrEmpty(vendorDef.VendorTemplateHash))
            {
                vendor = new Vendor(this.playfieldIdentity, identity, vendorDef.VendorTemplateHash);
            }
            else
            {
                vendor = new Vendor(this.playfieldIdentity, identity, vendorDef.VendorTemplateId);
            }

            vendor.Coordinates(new AORebirth.Core.Vector.Vector3(
                vendorDef.PositionX,
                vendorDef.PositionY,
                vendorDef.PositionZ));

            vendor.RawHeading = new AORebirth.Core.Vector.Quaternion(
                vendorDef.OrientationX,
                vendorDef.OrientationY,
                vendorDef.OrientationZ,
                vendorDef.OrientationW);

            return vendor;
        }

        private int HydrateStaticDynels(DBPlayfieldConfiguration config)
        {
            ConsoleLog($"[NEW unified hydration][DB] PF {this.playfieldIdentity.Instance} -> PlayfieldStaticDynelDao.GetByPlayfieldId");
            var staticDynels = PlayfieldStaticDynelDao.Instance
                .GetByPlayfieldId(this.playfieldIdentity.Instance)
                .ToList();

            if (staticDynels.Count == 0)
            {
                return 0;
            }

            int instantiated = 0;
            int failed = 0;

            foreach (var staticDynel in staticDynels)
            {
                try
                {
                    var entity = this.InstantiateStaticDynel(staticDynel);
                    if (entity != null)
                    {
                        this.dynelRegistry.Register(entity);
                        instantiated++;
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        $"Failed to instantiate static dynel {staticDynel.StaticDynelId}: {ex.Message}");
                    failed++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                $"PF {this.playfieldIdentity.Instance}: Static dynels = {instantiated} spawned, {failed} failed.");

            return instantiated;
        }

        private IEntity InstantiateStaticDynel(DBPlayfieldStaticDynel def)
        {
            // Route by dynel_type: door, object, etc.
            // For now, stub - would need to create appropriate entity type per type
            // This is where Door, StaticObject, etc. entities would be created

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                $"Static dynel type '{def.DynelType}' not yet implemented (ID {def.StaticDynelId}).");

            return null;
        }

        private static void ConsoleLog(string message)
        {
            if (EnableNewContentConsoleDiagnostics)
            {
                Console.WriteLine(message);
            }
        }

        private void DumpHydratedNpc(
            ICharacter npc,
            DBOrdinaryEnemySpawn spawn,
            DBOrdinaryEnemyProfile profile)
        {
            ConsoleLog(
                $"[NEW unified hydration][NPC DUMP] "
                + $"PF={this.playfieldIdentity.Instance} "
                + $"sourceSpawn={spawn.SpawnId} "
                + $"runtime={npc.Identity} "
                + $"name='{npc.Name}' "
                + $"profileKey='{profile.ProfileKey}' "
                + $"monsterData={profile.MonsterData} "
                + $"position={npc.Coordinates()} "
                + $"level={npc.Stats[StatIds.level].Value} "
                + $"breed={npc.Stats[StatIds.breed].Value} "
                + $"profession={npc.Stats[StatIds.profession].Value} "
                + $"registered=true activated=true");
        }
    }
}
