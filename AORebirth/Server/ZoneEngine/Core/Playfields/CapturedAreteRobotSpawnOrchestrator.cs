namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class CapturedAreteRobotSpawnOrchestrator
    {
        private const int PrivateAretePlayfieldInstance = 6553;

        // Mike: soft-respawn 60s. Prior Rox capture (~70s) incomplete in 20260722-keeper-exect-nano.
        private const double RespawnSeconds = 60.0;

        // Mike: after ~2 minutes of life, Malfunctioning Cleaning Robots catch fire,
        // then explode (death Parameter2=503) and soft-respawn after RespawnSeconds.
        private const double LifeUntilBurnSeconds = 120.0;

        private const double BurnBeforeExplodeSeconds = 8.0;

        private const float LivingNearRadiusSquared = 6.25f;

        private readonly CapturedAreteRobotContentProvider capturedRobotContent;

        private readonly NpcPatrolReplayCoordinator patrolReplay;

        private readonly Action<ICharacter> activateNpc;

        private readonly Dictionary<int, DateTime[]> nextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();

        private readonly Dictionary<int, RobotLifeState> robotLifeByInstance =
            new Dictionary<int, RobotLifeState>();

        private readonly HashSet<int> linkedPlayfields = new HashSet<int>();

        private sealed class RobotLifeState
        {
            public DateTime SpawnedUtc;

            public DateTime BurnStartedUtc;

            public DateTime NextFireSpellListUtc;

            public bool Burning;
        }

        internal CapturedAreteRobotSpawnOrchestrator(
            CapturedAreteRobotContentProvider capturedRobotContent,
            NpcPatrolReplayCoordinator patrolReplay,
            Action<ICharacter> activateNpc)
        {
            this.capturedRobotContent = capturedRobotContent;
            this.patrolReplay = patrolReplay;
            this.activateNpc = activateNpc;
        }

        internal void ClearPlayfield(int playfieldInstance)
        {
            this.linkedPlayfields.Remove(playfieldInstance);
            this.nextRespawnUtcByPlayfield.Remove(playfieldInstance);
            this.robotLifeByInstance.Clear();
        }

        internal void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity)
        {
            if (playfieldIdentity.Instance != PrivateAretePlayfieldInstance)
            {
                return;
            }

            CapturedAreteRobotSpawnDefinition[] spawns = this.capturedRobotContent.GetSpawnDefinitions();
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnRowsLoaded,
                PlayfieldLifecycleTrace.MessageCapturedAreteRobotSpawnRowsLoaded,
                playfieldIdentity,
                PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnRowsDetail(
                    spawns.Length,
                    CapturedAreteRobotContentProvider.MonsterData));

            this.linkedPlayfields.Add(playfieldIdentity.Instance);
            DateTime[] timers = new DateTime[spawns.Length];
            this.nextRespawnUtcByPlayfield[playfieldIdentity.Instance] = timers;

            for (int i = 0; i < spawns.Length; i++)
            {
                if (this.SpawnCapturedAreteCleaningRobot(playfield, playfieldIdentity, spawns[i]) != null)
                {
                    timers[i] = DateTime.MaxValue;
                }
            }
        }

        internal void TickRespawn(Playfield playfield, Identity playfieldIdentity)
        {
            if (playfield == null
                || this.activateNpc == null
                || playfieldIdentity.Instance != PrivateAretePlayfieldInstance)
            {
                return;
            }

            CapturedAreteRobotSpawnDefinition[] spawns = this.capturedRobotContent.GetSpawnDefinitions();
            if (spawns == null || spawns.Length == 0)
            {
                return;
            }

            this.linkedPlayfields.Add(playfieldIdentity.Instance);
            DateTime[] timers;
            if (!this.nextRespawnUtcByPlayfield.TryGetValue(playfieldIdentity.Instance, out timers)
                || timers == null
                || timers.Length != spawns.Length)
            {
                timers = new DateTime[spawns.Length];
                this.nextRespawnUtcByPlayfield[playfieldIdentity.Instance] = timers;
            }

            for (int i = 0; i < spawns.Length; i++)
            {
                CapturedAreteRobotSpawnDefinition spawn = spawns[i];
                if (HasLivingRobotNear(playfield, spawn))
                {
                    timers[i] = DateTime.MaxValue;
                }
                else if (timers[i] == DateTime.MaxValue || timers[i] == default(DateTime))
                {
                    timers[i] = DateTime.UtcNow + TimeSpan.FromSeconds(RespawnSeconds);
                }
                else if (!(timers[i] > DateTime.UtcNow))
                {
                    try
                    {
                        if (this.SpawnCapturedAreteCleaningRobot(playfield, playfieldIdentity, spawn) != null)
                        {
                            timers[i] = DateTime.MaxValue;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            this.TickBurnAndExplodeLifecycle(playfield, playfieldIdentity);
        }

        private void TickBurnAndExplodeLifecycle(Playfield playfield, Identity playfieldIdentity)
        {
            if (playfield == null || playfieldIdentity.Instance != PrivateAretePlayfieldInstance)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            var living = new HashSet<int>();

            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(
                        candidate.Name,
                        CapturedAreteRobotContentProvider.RobotName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int instance = candidate.Identity.Instance;
                if (candidate.Stats[StatIds.health].Value <= 0)
                {
                    this.robotLifeByInstance.Remove(instance);
                    continue;
                }

                living.Add(instance);
                RobotLifeState state;
                if (!this.robotLifeByInstance.TryGetValue(instance, out state) || state == null)
                {
                    state = new RobotLifeState
                    {
                        SpawnedUtc = now,
                        BurnStartedUtc = DateTime.MinValue,
                        NextFireSpellListUtc = DateTime.MinValue,
                        Burning = false
                    };
                    this.robotLifeByInstance[instance] = state;
                }

                if (!state.Burning)
                {
                    if (now < state.SpawnedUtc + TimeSpan.FromSeconds(LifeUntilBurnSeconds))
                    {
                        continue;
                    }

                    state.Burning = true;
                    state.BurnStartedUtc = now;
                    state.NextFireSpellListUtc =
                        now + TimeSpan.FromSeconds(CapturedSpellListVisualEffects.BurningFireIntervalSeconds);
                    CapturedSpellListVisualEffects.AnnounceBurningRobotFire(candidate);
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "Captured Arete robot burning identity="
                        + candidate.Identity.ToString(true)
                        + " afterLifeSeconds="
                        + LifeUntilBurnSeconds.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                if (now >= state.BurnStartedUtc + TimeSpan.FromSeconds(BurnBeforeExplodeSeconds))
                {
                    // Health=0 → ProcessDeadNpcDespawn → BeginNpcDeath → explode Parameter2=503.
                    candidate.Stats[StatIds.health].Value = 0;
                    this.robotLifeByInstance.Remove(instance);
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "Captured Arete robot explode identity="
                        + candidate.Identity.ToString(true));
                    continue;
                }

                if (!(state.NextFireSpellListUtc > now))
                {
                    CapturedSpellListVisualEffects.AnnounceBurningRobotFire(candidate);
                    state.NextFireSpellListUtc =
                        now + TimeSpan.FromSeconds(CapturedSpellListVisualEffects.BurningFireIntervalSeconds);
                }
            }

            if (this.robotLifeByInstance.Count == 0)
            {
                return;
            }

            var stale = new List<int>();
            foreach (int tracked in this.robotLifeByInstance.Keys)
            {
                if (!living.Contains(tracked))
                {
                    stale.Add(tracked);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                this.robotLifeByInstance.Remove(stale[i]);
            }
        }

        private Character SpawnCapturedAreteCleaningRobot(
            Playfield playfield,
            Identity playfieldIdentity,
            CapturedAreteRobotSpawnDefinition spawn)
        {
            var npcController = new NPCController();
            Character mobCharacter = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                CombatTestMobArchetype.TemplateHash,
                playfieldIdentity,
                new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z },
                new AORebirth.Core.Vector.Quaternion(0, 0, 0, 1),
                npcController,
                spawn.Level);

            if (mobCharacter == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Captured Arete robot spawn failed source=20260721-Rox-robots sourceIdentity=SimpleChar:{0:X8}",
                        spawn.SourceInstance));
                return null;
            }

            mobCharacter.Name = CapturedAreteRobotContentProvider.RobotName;
            mobCharacter.Playfield = playfield;
            CombatTestMobArchetype.Prepare(mobCharacter, CombatTestMobArchetype.MalfunctioningCleaningRobot);
            SetCapturedMobStat(mobCharacter, StatIds.life, spawn.Health);
            SetCapturedMobStat(mobCharacter, StatIds.health, spawn.Health);
            SetCapturedMobStat(mobCharacter, StatIds.level, spawn.Level);
            SetCapturedMobStat(mobCharacter, StatIds.runspeed, spawn.RunSpeed);
            mobCharacter.Coordinates(new Coordinate { x = spawn.X, y = spawn.Y, z = spawn.Z });
            AssignCapturedPatrolWaypoints(mobCharacter, spawn);
            CapturedEnemyCombatContract contract = AreteRegularMobCombatProfileSelector.Create(
                "arete-malfunctioning-cleaning-robot-20260721-Rox-robots",
                spawn.CombatProfileSelector,
                spawn.CombatEvidenceSourceIdentity,
                0,
                0,
                NpcAiProfile.Passive);
            string combatFailure;
            bool combatReady = CapturedEnemyCombatRuntime.PrepareAndRequireCombatReady(
                mobCharacter,
                npcController,
                contract,
                out combatFailure);
            if (!combatReady)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Captured Arete robot intentionally quarantined sourceIdentity=SimpleChar:"
                    + spawn.SourceInstance.ToString("X8") + " reason=" + combatFailure);
            }

            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                PlayfieldLifecycleTrace.MessageCapturedAreteRobotSpawnCreated,
                mobCharacter.Identity,
                PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnCreatedDetail(
                    spawn.SourceInstance,
                    CapturedAreteRobotContentProvider.MonsterData,
                    spawn.Health,
                    spawn.Level,
                    spawn.RunSpeed,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.PatrolX,
                    spawn.PatrolY,
                    spawn.PatrolZ));

            int replaySegmentCount = 0;
            this.patrolReplay.AssignCapturedAreteRobotReplay(
                spawn.SourceInstance,
                segments =>
                {
                    replaySegmentCount = segments == null ? 0 : segments.Length;
                    npcController.SetCapturedPatrolReplaySegments(segments);
                });
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                PlayfieldLifecycleTrace.MessageCapturedAreteRobotPatrolReplayAssigned,
                mobCharacter.Identity,
                PlayfieldLifecycleTrace.FormatCapturedAreteRobotPatrolReplayAssignedDetail(
                    spawn.SourceInstance,
                    replaySegmentCount));

            mobCharacter.DoNotDoTimers = false;
            this.activateNpc(mobCharacter);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                PlayfieldLifecycleTrace.StageCapturedAreteRobotSimpleCharFullUpdateBroadcast,
                PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                mobCharacter.Identity,
                PlayfieldLifecycleTrace.FormatCapturedAreteRobotSimpleCharFullUpdateDetail(spawn.SourceInstance));
            playfield.AnnounceSpawnedCharacterVisibility(mobCharacter, Identity.None);

            this.robotLifeByInstance[mobCharacter.Identity.Instance] = new RobotLifeState
            {
                SpawnedUtc = DateTime.UtcNow,
                BurnStartedUtc = DateTime.MinValue,
                NextFireSpellListUtc = DateTime.MinValue,
                Burning = false
            };

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Captured Arete robot spawned source=20260721-Rox-robots sourceIdentity=SimpleChar:{0:X8} serverIdentity={1} pos=({2},{3},{4}) health={5} level={6} runSpeed={7}",
                    spawn.SourceInstance,
                    mobCharacter.Identity,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.Health,
                    spawn.Level,
                    spawn.RunSpeed));
            return mobCharacter;
        }

        private static void AssignCapturedPatrolWaypoints(
            ICharacter mobCharacter,
            CapturedAreteRobotSpawnDefinition spawn)
        {
            mobCharacter.Waypoints.Clear();
            mobCharacter.AddWaypoint(
                new AORebirth.Core.Vector.Vector3(spawn.X, spawn.Y, spawn.Z),
                false);
            mobCharacter.AddWaypoint(
                new AORebirth.Core.Vector.Vector3(spawn.PatrolX, spawn.PatrolY, spawn.PatrolZ),
                false);
            mobCharacter.Controller.State = CharacterState.Patrolling;
        }

        private static void SetCapturedMobStat(ICharacter mobCharacter, StatIds stat, int value)
        {
            mobCharacter.Stats[stat].Value = value;
            mobCharacter.Stats[stat].BaseValue = (uint)value;
        }

        private static bool HasLivingRobotNear(Playfield playfield, CapturedAreteRobotSpawnDefinition spawn)
        {
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(
                        candidate.Name,
                        CapturedAreteRobotContentProvider.RobotName,
                        StringComparison.OrdinalIgnoreCase)
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                float dx = candidate.Coordinates().x - spawn.X;
                float dz = candidate.Coordinates().z - spawn.Z;
                if ((dx * dx) + (dz * dz) <= LivingNearRadiusSquared)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
