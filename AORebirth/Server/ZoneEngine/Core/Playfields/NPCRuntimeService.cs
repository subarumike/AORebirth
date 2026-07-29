namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Navigation;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class NPCRuntimeService
    {
        private readonly Playfield playfield;

        private readonly PlayfieldDynelRegistry dynelRegistry;

        private readonly PlayfieldRewardRuntimeService rewards;

        private readonly NpcCorpseLifecycleCoordinator corpseLifecycle;

        private readonly NpcCombatTickCoordinator combatTick;

        private readonly NpcChaseNavigationRuntimeService chaseNavigation;

        private readonly CapturedAreteRobotContentProvider capturedAreteRobotContent;

        private readonly CapturedSubwayContentProvider capturedSubwayContent;

        private readonly CapturedSubwayOrdinaryContentProvider capturedSubwayOrdinaryContent;

        private readonly OrdinaryEnemyCatalog ordinaryEnemyCatalog;

        private readonly NpcPatrolReplayCoordinator patrolReplay;

        private readonly CapturedAreteRobotSpawnOrchestrator capturedAreteRobotSpawns;

        private readonly OrdinaryEnemyRuntimeService ordinaryEnemies;

        private readonly WorldPopulationController worldPopulation;

        private readonly CapturedSubwayEncounterRuntimeService capturedSubwayEncounters;

        private readonly NascenceCoreHecklerSpawnOrchestrator nascenceCoreHecklers;

        private readonly CapturedTempleOfThreeWindsEncounterRuntimeService capturedTempleEncounters;

        private readonly Dictionary<int, NpcHomeState> npcHomeStates = new Dictionary<int, NpcHomeState>();

        private readonly Dictionary<int, DateTime> corpseDespawnTicks = new Dictionary<int, DateTime>();

        internal NPCRuntimeService(
            Playfield playfield,
            PlayfieldDynelRegistry dynelRegistry,
            PlayfieldRewardRuntimeService rewards,
            NpcChaseNavigationRuntimeService chaseNavigation)
        {
            this.playfield = playfield;
            this.dynelRegistry = dynelRegistry;
            this.rewards = rewards ?? new PlayfieldRewardRuntimeService();
            this.chaseNavigation = chaseNavigation
                                   ?? throw new ArgumentNullException("chaseNavigation");
            this.corpseLifecycle = new NpcCorpseLifecycleCoordinator(playfield, this.RemoveNpcHome);
            this.combatTick = new NpcCombatTickCoordinator(playfield);
            this.capturedAreteRobotContent = new CapturedAreteRobotContentProvider(LogCapturedAreteRobotContent);
            this.capturedSubwayContent = new CapturedSubwayContentProvider();
            this.capturedSubwayOrdinaryContent = new CapturedSubwayOrdinaryContentProvider();
            this.ordinaryEnemyCatalog =
                new OrdinaryEnemyCatalog(
                    this.capturedSubwayContent,
                    this.capturedSubwayOrdinaryContent,
                    new CapturedTempleOfThreeWindsContentProvider());
            this.patrolReplay =
                new NpcPatrolReplayCoordinator(this.capturedAreteRobotContent, this.capturedSubwayContent);
            this.capturedAreteRobotSpawns =
                new CapturedAreteRobotSpawnOrchestrator(
                    this.capturedAreteRobotContent,
                    this.patrolReplay,
                    this.ActivateNpc);
            this.ordinaryEnemies =
                new OrdinaryEnemyRuntimeService(
                    this.ordinaryEnemyCatalog,
                    this.patrolReplay,
                    this.dynelRegistry,
                    this.ActivateNpc,
                    new NpcDamageLineOfSightRuntimeService(
                        this.playfield.Identity.Instance));
            this.worldPopulation =
                new WorldPopulationController(this.playfield, this.ordinaryEnemyCatalog, this.ordinaryEnemies);
            this.capturedSubwayEncounters =
                new CapturedSubwayEncounterRuntimeService(
                    this.playfield,
                    this.dynelRegistry,
                    this.ActivateNpc);
            this.nascenceCoreHecklers = new NascenceCoreHecklerSpawnOrchestrator(this.ActivateNpc);
            this.capturedTempleEncounters =
                new CapturedTempleOfThreeWindsEncounterRuntimeService(
                    this.playfield,
                    this.dynelRegistry,
                    this.ActivateNpc);
        }

        internal void ActivateNpc(ICharacter character)
        {
            this.dynelRegistry.Register(character);
            this.RegisterNpcHome(character);
        }

        internal void EnsureAreteCapturePopulation()
        {
            AreteLandingPopulationEnsure.Tick(
                this.playfield,
                this.playfield.Identity,
                this.ActivateNpc);
            this.capturedAreteRobotSpawns.TickRespawn(this.playfield, this.playfield.Identity);
        }

        internal void ClearRuntimeState()
        {
            this.chaseNavigation.ClearAll(NpcChaseInvalidationReason.PlayfieldReset);
            foreach (ICharacter character in this.dynelRegistry.Characters())
            {
                if (character.Controller is NPCController)
                {
                    character.DoNotDoTimers = true;
                    character.SetTarget(Identity.None);
                    character.SetFightingTarget(Identity.None);
                    NPCController controller = (NPCController)character.Controller;
                    controller.State = CharacterState.Idle;
                    controller.StopFollow();
                    this.combatTick.ClearTracking(character.Identity);
                    CapturedEnemyCombatRuntimeRegistry.Remove(character.Identity.Instance);
                }
            }

            this.combatTick.ClearRuntimeState();
            this.corpseLifecycle.ClearRuntimeState();
            this.worldPopulation.ClearPlayfield(this.playfield.Identity.Instance);
            this.ordinaryEnemies.ClearRuntimeState(this.playfield.Identity.Instance);
            this.chaseNavigation.ClearAll(NpcChaseInvalidationReason.EncounterReset);
            this.capturedSubwayEncounters.ClearRuntimeState();
            this.capturedTempleEncounters.ClearRuntimeState();
            this.npcHomeStates.Clear();
            this.corpseDespawnTicks.Clear();
            AndromedaIccHqIdleGestureRuntime.Clear();
            AndromedaIccHqSpawn.ClearPlayfield(this.playfield.Identity.Instance);
            AreteLandingSpawn.ClearPlayfield(this.playfield.Identity.Instance);
            AreteIccPeacekeeperPatrolRuntime.ClearPlayfield(this.playfield.Identity.Instance);
            MarcusPadAmbientCombat.ClearPlayfield(this.playfield.Identity.Instance);
            this.capturedAreteRobotSpawns.ClearPlayfield(this.playfield.Identity.Instance);
            JunkyardCleaningRobotRuntime.ClearPlayfield(this.playfield.Identity.Instance);
            AlexAreaMobRuntime.ClearPlayfield(this.playfield.Identity.Instance);
            LoreleiOasisMobRuntime.ClearPlayfield(this.playfield.Identity.Instance);
            NascenceLifeSpawn.ClearPlayfield(this.playfield.Identity.Instance);
            AreteFinishCaptureMobRuntime.ClearPlayfield(this.playfield.Identity.Instance);
            SurveillanceDroidRuntime.ClearPlayfield(this.playfield.Identity.Instance);
            AreteLandingPopulationEnsure.ClearPlayfield(this.playfield.Identity.Instance);
            HoloDeckSpawn.ClearPlayfield(this.playfield.Identity.Instance);
        }

        internal void RegisterNpcHome(ICharacter character)
        {
            if (character == null || !(character.Controller is NPCController))
            {
                return;
            }

            double maximumNpcDistanceFromHome =
                NpcCombatLeashPolicy.SubwayDefaultMaximumNpcDistanceFromHome;
            CapturedEncounterRuntimeDefinition encounterDefinition;
            if (CapturedEncounterRuntimeRegistry.TryGet(
                    character.Identity.Instance,
                    out encounterDefinition)
                && encounterDefinition.MaximumNpcLeashDistanceFromHome.HasValue)
            {
                maximumNpcDistanceFromHome =
                    encounterDefinition.MaximumNpcLeashDistanceFromHome.Value;
            }
            else if (AreteRoboticGuardDogRuntime.IsRegisteredDog(character))
            {
                maximumNpcDistanceFromHome =
                    AreteRoboticGuardDogRuntime.MaximumNpcDistanceFromHomeMeters;
            }

            this.npcHomeStates[character.Identity.Instance] =
                new NpcHomeState
                {
                    Coordinates = new Coordinate(character.Coordinates()),
                    MaximumNpcDistanceFromHome = maximumNpcDistanceFromHome
                };
        }

        internal void RemoveNpcHome(Identity identity)
        {
            this.npcHomeStates.Remove(identity.Instance);
        }

        internal void DespawnNpcImmediately(
            ICharacter target,
            Action<Identity> stopFightingDeadTarget,
            Action<Identity> cancelPendingCorpseSpawn)
        {
            if (target == null || target.Identity.Type != IdentityType.CanbeAffected)
            {
                return;
            }

            stopFightingDeadTarget(target.Identity);
            cancelPendingCorpseSpawn(target.Identity);
            this.FinalizeNpcDespawn(target);
        }

        internal void SpawnCapturedNpcContent(Identity playfieldIdentity)
        {
            this.capturedAreteRobotSpawns.SpawnForPlayfield(this.playfield, playfieldIdentity);
            PerkResetServiceProviderSpawn.SpawnForPlayfield(
                this.playfield,
                playfieldIdentity,
                this.ActivateNpc);
            this.nascenceCoreHecklers.SpawnForPlayfield(this.playfield, playfieldIdentity);
            NascenceLifeSpawn.SpawnForPlayfield(this.playfield, playfieldIdentity, this.ActivateNpc);
            ThrakOmniGardenSpawn.SpawnForPlayfield(this.playfield, playfieldIdentity, this.ActivateNpc);
            RomeBlueCitySpawn.SpawnForPlayfield(this.playfield, playfieldIdentity, this.ActivateNpc);
            AndromedaIccHqSpawn.SpawnForPlayfield(this.playfield, playfieldIdentity, this.ActivateNpc);
            try
            {
                AreteLandingSpawn.SpawnForPlayfield(this.playfield, playfieldIdentity, this.ActivateNpc);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteLandingSpawn batch failed: " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                MarcusPadAmbientCombat.StartForPlayfield(this.playfield, playfieldIdentity, this.ActivateNpc);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "MarcusPadAmbientCombat start failed: " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                // Capture 20260720-212302: Arete Cleaning Robot population (mesh/attack/loot).
                JunkyardCleaningRobotRuntime.StartForPlayfield(
                    this.playfield,
                    playfieldIdentity,
                    this.ActivateNpc);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "JunkyardCleaningRobotRuntime start failed: " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                // Capture 20260720-212302: Alex-pad Docker / Waste / Flea / Cleanmeister.
                AlexAreaMobRuntime.StartForPlayfield(this.playfield, playfieldIdentity, this.ActivateNpc);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AlexAreaMobRuntime start failed: " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                // Capture 20260721-loralei: Desert Reets + Lolly at oasis cage.
                LoreleiOasisMobRuntime.StartForPlayfield(this.playfield, playfieldIdentity, this.ActivateNpc);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "LoreleiOasisMobRuntime start failed: " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                // Capture 20260721-finish: Engineer Automaton I near Vernon (A004 monster body).
                AreteFinishCaptureMobRuntime.StartForPlayfield(
                    this.playfield,
                    playfieldIdentity,
                    this.ActivateNpc);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteFinishCaptureMobRuntime start failed: " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                SurveillanceDroidRuntime.StartForPlayfield(
                    this.playfield,
                    playfieldIdentity,
                    this.ActivateNpc);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "SurveillanceDroidRuntime start failed: " + ex.GetType().Name + ": " + ex.Message);
            }

            HoloDeckSpawn.SpawnForPlayfield(this.playfield, playfieldIdentity, this.ActivateNpc);
            try
            {
                if (!ZoneEngine.Core.Missions.MissionAcgOperationalRuntime.TrySpawnForPlayfield(
                    this.playfield,
                    playfieldIdentity,
                    this.ActivateNpc))
                {
                    MissionInstanceSpawn.SpawnForPlayfield(
                        this.playfield,
                        playfieldIdentity,
                        this.ActivateNpc);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Mission ACG/instance spawn failed: " + ex.GetType().Name + ": " + ex.Message);
                try
                {
                    MissionInstanceSpawn.SpawnForPlayfield(
                        this.playfield,
                        playfieldIdentity,
                        this.ActivateNpc);
                }
                catch (Exception fallbackEx)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "MissionInstanceSpawn fallback failed: "
                        + fallbackEx.GetType().Name + ": " + fallbackEx.Message);
                }
            }
            this.worldPopulation.ActivatePlayfield(playfieldIdentity);
            this.capturedSubwayEncounters.ActivatePlayfield(playfieldIdentity);
            this.capturedTempleEncounters.ActivatePlayfield(playfieldIdentity);
        }

        internal bool HasPendingDeadNpcDespawn(Identity identity)
        {
            return this.corpseLifecycle.HasPendingDeadNpcDespawn(identity);
        }

        internal void ScheduleNpcCorpseDespawn(Identity corpseIdentity, DateTime expiresAtUtc)
        {
            this.corpseDespawnTicks[corpseIdentity.Instance] = expiresAtUtc;
        }

        internal void ProcessDueNpcCorpseDespawns(DateTime utcNow, Action<int> despawnCorpse)
        {
            foreach (int corpseInstance in this.corpseDespawnTicks
                .Where(x => x.Value <= utcNow)
                .Select(x => x.Key)
                .ToArray())
            {
                despawnCorpse(corpseInstance);
            }
        }

        internal void ProcessDueCapturedSubwayRespawns(Identity playfieldIdentity, DateTime utcNow)
        {
            this.ordinaryEnemies.ProcessExpiredSupportNanoEffects(utcNow);
            this.worldPopulation.ProcessDue(utcNow);
            this.capturedSubwayEncounters.ProcessDue(utcNow, this.AcquireAggro);
            this.nascenceCoreHecklers.ProcessDue(utcNow);
            NascenceLifeSpawn.TickBarkingChimeraRespawn(
                this.playfield,
                playfieldIdentity,
                this.ActivateNpc);
            AndromedaIccHqIdleGestureRuntime.ProcessDue(utcNow);
            this.capturedTempleEncounters.ProcessDue(utcNow, this.AcquireAggro);
        }

        internal void ClearNpcCorpseDespawn(int corpseInstance)
        {
            this.corpseDespawnTicks.Remove(corpseInstance);
        }

        internal void NotifyCorpseRemoved(Identity corpseIdentity)
        {
            this.worldPopulation.NotifyCorpseRemoved(corpseIdentity, DateTime.UtcNow);
        }

        internal void BeginNpcDeath(ICharacter attacker, ICharacter target)
        {
            if (!(target.Controller is NPCController)
                || this.corpseLifecycle.HasPendingDeadNpcDespawn(target.Identity))
            {
                return;
            }

            DateTime diedAtUtc = DateTime.UtcNow;
            this.ordinaryEnemies.NotifyCharacterDied(target);
            Identity corpseIdentity = Identity.None;
            bool isOperationalNpc;
            bool operationalDeathPersisted =
                ZoneEngine.Core.Missions.MissionAcgOperationalRuntime.TryPrepareNpcDeath(
                    target,
                    this.playfield.CanBuildKnownCorpseVisual(target),
                    out corpseIdentity,
                    out isOperationalNpc);
            if (!isOperationalNpc && this.playfield.CanBuildKnownCorpseVisual(target))
            {
                corpseIdentity = this.playfield.AllocateCorpseIdentity();
            }

            this.chaseNavigation.Clear(
                target.Identity.Instance,
                NpcChaseInvalidationReason.CorpseTransition);
            this.playfield.MarkNpcDead(target);
            this.playfield.StopFightingDeadTarget(target.Identity);
            this.playfield.StopDyingNpcCombatState(target);
            this.playfield.SendNpcDeathAnimation(target);
            if (!isOperationalNpc || operationalDeathPersisted)
            {
                this.rewards.RunNpcDeathRewardHooks(attacker, target, this.playfield.AwardCombatXp);
                this.ScheduleNpcDeathCorpseSpawn(target, corpseIdentity);
            }
            else
            {
                ZoneEngine.Core.Missions.MissionDiagnostics.Log(
                    "ACG-OPERATIONAL-DEATH-BLOCK runtime={0} livePf2={1} reason=durable-death-persist-failed",
                    target.Identity.Instance,
                    this.playfield.Identity.Instance);
            }
            this.worldPopulation.NotifyDeath(target, corpseIdentity, diedAtUtc);
            this.nascenceCoreHecklers.NotifyDeath(target, diedAtUtc);

            foreach (ICharacter summon in this.capturedSubwayEncounters.NotifyDeath(target, diedAtUtc))
            {
                this.playfield.DespawnNpcImmediately(summon);
            }
            foreach (ICharacter summon in this.capturedTempleEncounters.NotifyDeath(
                target,
                diedAtUtc))
            {
                this.playfield.DespawnNpcImmediately(summon);
            }

            this.ScheduleDeadNpcDespawn(target);

            LogUtil.Debug(DebugInfoDetail.Network, string.Format("NPC died target={0}", target.Identity));
        }

        internal bool ProcessDeadNpcDespawn(ICharacter character)
        {
            if (!(character.Controller is NPCController)
                || character.Stats[StatIds.health].Value > 0)
            {
                return false;
            }

            DateTime despawnTick;
            if (!this.corpseLifecycle.TryGetDeadNpcDespawn(character.Identity, out despawnTick))
            {
                this.BeginNpcDeath(null, character);
                return true;
            }

            if (despawnTick > DateTime.UtcNow)
            {
                return true;
            }

            this.FinalizeNpcDespawn(character);
            return true;
        }

        internal void FinalizeNpcDespawn(ICharacter target)
        {
            this.chaseNavigation.Clear(
                target.Identity.Instance,
                NpcChaseInvalidationReason.Despawn);
            DateTime utcNow = DateTime.UtcNow;
            this.worldPopulation.NotifyNpcDespawn(target, utcNow);
            this.capturedSubwayEncounters.NotifyNpcDespawn(target, utcNow);
            this.nascenceCoreHecklers.NotifyNpcDespawn(target);
            this.capturedTempleEncounters.NotifyNpcDespawn(target, utcNow);
            this.corpseLifecycle.FinalizeNpcDespawn(target);
            this.dynelRegistry.Unregister(target.Identity);
            OrdinaryEnemyRuntimeRegistry.Remove(target.Identity.Instance);
            SubwayVisibilityDiagnosticSelection.RemoveRuntimeIdentity(target.Identity.Instance);
            CapturedEnemyCombatRuntimeRegistry.Remove(target.Identity.Instance);
            CapturedEncounterRuntimeRegistry.Remove(target.Identity.Instance);
        }

        internal void ResetCombatTick(ICharacter attacker)
        {
            this.combatTick.ResetCombatTick(attacker);
        }

        internal void ProcessCombatTick(ICharacter attacker)
        {
            if (this.TryBeginLeashReturn(attacker))
            {
                return;
            }

            if (this.ordinaryEnemies.TryProcessSupportNano(attacker, DateTime.UtcNow))
            {
                return;
            }

            if (this.capturedSubwayEncounters.IsCapturedNanoCastInProgress(attacker)
                || this.capturedTempleEncounters.IsCapturedNanoCastInProgress(attacker))
            {
                return;
            }

            this.combatTick.ProcessCombatTick(attacker);
        }

        internal void ClearInvalidCombatTarget(ICharacter attacker)
        {
            this.ClearFightingTarget(attacker);
        }

        internal void ClearFightingTarget(ICharacter character)
        {
            character.SetFightingTarget(Identity.None);
            this.ClearCombatTracking(
                character.Identity,
                NpcChaseInvalidationReason.TargetLost);

            // Player death / combat end left Follow set → ProcessPatrolTick kept DoFollow
            // and never resumed captured patrol replay. Clear follow and restore patrol.
            NPCController npcController = character.Controller as NPCController;
            if (npcController != null)
            {
                npcController.StopFollow();
                if (character.Waypoints != null && character.Waypoints.Count > 0)
                {
                    npcController.State = CharacterState.Patrolling;
                }
            }
        }

        internal void StopDyingNpcCombatState(ICharacter target)
        {
            target.SetTarget(Identity.None);
            target.SetFightingTarget(Identity.None);
            this.ClearCombatTracking(
                target.Identity,
                NpcChaseInvalidationReason.Death);

            NPCController npcController = target.Controller as NPCController;
            if (npcController != null)
            {
                npcController.SnapshotCurrentMotionPosition();
                npcController.StopFollow();
            }
        }

        internal void AcquireAggro(ICharacter attacker, ICharacter target)
        {
            this.AcquireAggro(attacker, target, true);
        }

        private void AcquireAggro(
            ICharacter attacker,
            ICharacter target,
            bool allowSocialAggro)
        {
            string missionSpatialFailure;
            if (!ZoneEngine.Core.Missions.MissionAcgSpatialRuntime.TryValidateCombatPair(
                attacker,
                target,
                out missionSpatialFailure))
            {
                return;
            }

            NPCController npcController = target.Controller as NPCController;
            if (npcController == null)
            {
                return;
            }

            NpcHomeState home;
            if (this.npcHomeStates.TryGetValue(target.Identity.Instance, out home))
            {
                if (home.ReturningHome)
                {
                    return;
                }

                bool playerControlledPet = this.IsPlayerControlledPet(target);
                ChaseNavigationPoint homePoint = ToNavigationPoint(home.Coordinates.coordinate);
                ChaseNavigationPoint npcPoint = ToNavigationPoint(target.Coordinates().coordinate);
                ChaseNavigationPoint attackerPoint = ToNavigationPoint(attacker.Coordinates().coordinate);
                if (NpcCombatLeashPolicy.ShouldResetCombat(
                    this.playfield.Identity.Instance,
                    playerControlledPet,
                    homePoint,
                    npcPoint,
                    attackerPoint,
                    home.MaximumNpcDistanceFromHome))
                {
                    if (homePoint.Distance2D(npcPoint)
                        > home.MaximumNpcDistanceFromHome)
                    {
                        this.BeginLeashReturn(target, home);
                    }

                    return;
                }
            }

            CapturedEnemyCombatContract capturedContract;
            if (CapturedEnemyCombatRuntimeRegistry.TryGet(target.Identity.Instance, out capturedContract)
                && !capturedContract.IsCombatReady)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Captured enemy combat refused npc={0} attacker={1} reason=contract-incomplete evidence={2}",
                        target.Identity,
                        attacker.Identity,
                        capturedContract.Evidence));
                return;
            }

            OrdinaryEnemyRuntimeDefinition ordinaryDefinition;
            if (OrdinaryEnemyRuntimeRegistry.TryGet(target.Identity.Instance, out ordinaryDefinition)
                && ordinaryDefinition.Profile.Aggression.Mode == OrdinaryEnemyAggressionMode.Passive)
            {
                return;
            }

            if (npcController.KnuBot != null
                || !NpcAiProfiles.CanRetaliate(npcController.AiProfile)
                || target.Stats[StatIds.health].Value <= 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "NPC combat refused npc={0} attacker={1} knubot={2} profile={3} health={4} fightingTarget={5}",
                        target.Identity,
                        attacker.Identity,
                        npcController.KnuBot != null,
                        npcController.AiProfile,
                        target.Stats[StatIds.health].Value,
                        target.FightingTarget));
                return;
            }

            if (target.FightingTarget.Instance != 0)
            {
                if (target.FightingTarget.Instance == attacker.Identity.Instance)
                {
                    return;
                }

                // Player/pet can pull NPC off ambient NPC-vs-NPC fights (Marcus pad robot).
                if (!PlayerVersusPlayerCombatRules.IsPlayerControlledCombatant(attacker))
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Network,
                        string.Format(
                            "NPC combat refused npc={0} attacker={1} reason=already-fighting fightingTarget={2}",
                            target.Identity,
                            attacker.Identity,
                            target.FightingTarget));
                    return;
                }
            }

            this.StartCombatWithAcquiredTarget(attacker, target, capturedContract);
            if (!allowSocialAggro)
            {
                return;
            }

            foreach (ICharacter ally in this.ordinaryEnemies.FindSocialAggroAllies(
                target,
                attacker))
            {
                this.AcquireAggro(attacker, ally, false);
            }

            foreach (ICharacter ally in LoreleiOasisMobRuntime.FindSocialAggroAllies(
                target,
                attacker))
            {
                this.AcquireAggro(attacker, ally, false);
            }

            // Capture 20260722-235510: ICC Peacekeeper attacks the hostile that aggroed the player.
            if (PlayerVersusPlayerCombatRules.IsPlayerControlledCombatant(attacker))
            {
                foreach (ICharacter peacekeeper in AreteIccPeacekeeperPatrolRuntime.FindPlayerDefenseAllies(
                    attacker,
                    target))
                {
                    this.AcquireAggro(target, peacekeeper, false);
                }
            }
        }

        /// <summary>
        /// Force retarget for TauntNpc nanos (Mongo Slam). Steals aggro from other players.
        /// Social / quest NPCs (Rex, Marcus, etc.) must never be pulled — Mongo is combat-mob only.
        /// </summary>
        internal void ForceTauntAggro(ICharacter taunter, ICharacter target)
        {
            if (taunter == null || target == null)
            {
                return;
            }

            string missionSpatialFailure;
            if (!ZoneEngine.Core.Missions.MissionAcgSpatialRuntime.TryValidateCombatPair(
                taunter,
                target,
                out missionSpatialFailure))
            {
                return;
            }

            NPCController npcController = target.Controller as NPCController;
            if (npcController == null
                || npcController.KnuBot != null
                || !NpcAiProfiles.CanRetaliate(npcController.AiProfile)
                || target.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            CapturedEnemyCombatContract capturedContract;
            if (CapturedEnemyCombatRuntimeRegistry.TryGet(
                    target.Identity.Instance,
                    out capturedContract)
                && !capturedContract.IsCombatReady)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "Captured enemy taunt refused npc={0} taunter={1} reason=contract-incomplete evidence={2}",
                        target.Identity,
                        taunter.Identity,
                        capturedContract.Evidence));
                return;
            }

            this.StartCombatWithAcquiredTarget(taunter, target, capturedContract);
            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "ForceTauntAggro taunter={0} npc={1} previousTargetStolen=true",
                    taunter.Identity,
                    target.Identity));
        }

        internal void ProcessPatrolTick(ICharacter character)
        {
            if (character == null || !(character.Controller is NPCController))
            {
                return;
            }

            if (this.playfield != null
                && this.playfield.Identity.Instance == 6553
                && string.Equals(character.Name, "Marcus Stone", StringComparison.OrdinalIgnoreCase))
            {
                MarcusPadAmbientCombat.TickRespawn(
                    this.playfield,
                    this.playfield.Identity,
                    this.ActivateNpc);
                MarcusWoundedWorkersQuestRuntime.TickHealRecoveries(this.playfield);
            }

            if (this.playfield != null && this.playfield.Identity.Instance == 6553)
            {
                SurveillanceDroidRuntime.TickEnsurePresent(
                    this.playfield,
                    this.playfield.Identity,
                    this.ActivateNpc);
                JunkyardCleaningRobotRuntime.TickRespawn(
                    this.playfield,
                    this.playfield.Identity,
                    this.ActivateNpc);
                AlexAreaMobRuntime.TickRespawn(
                    this.playfield,
                    this.playfield.Identity,
                    this.ActivateNpc);
                LoreleiOasisMobRuntime.TickRespawn(
                    this.playfield,
                    this.playfield.Identity,
                    this.ActivateNpc);
                // Burn→explode lifecycle for Malfunctioning Cleaning Robots (also via EnsureArete).
                this.capturedAreteRobotSpawns.TickRespawn(this.playfield, this.playfield.Identity);
            }

            PetCommandService.ProcessPetHealTick(character);

            DateTime utcNow = DateTime.UtcNow;
            string missionStationaryReason;
            bool missionStationary =
                ZoneEngine.Core.Missions.MissionAcgSpatialRuntime.RequiresStationaryNpc(
                    character,
                    null,
                    out missionStationaryReason);
            if (missionStationary)
            {
                NPCController missionController = character.Controller as NPCController;
                if (missionController != null)
                {
                    missionController.SnapshotCurrentMotionPosition();
                    missionController.StopFollow();
                }
            }

            if (this.TryBeginLeashReturn(character))
            {
                this.TryProcessLeashReturn(character, utcNow);
                return;
            }

            if (this.TryProcessLeashReturn(character, utcNow))
            {
                return;
            }

            if (this.ordinaryEnemies.TryProcessSupportNano(character, utcNow))
            {
                return;
            }

            // Capture 20260722-235510: PK assists when a hostile fighting a player enters range
            // (not only at the instant of first aggro).
            ICharacter defenseHostile = AreteIccPeacekeeperPatrolRuntime.FindDefenseHostile(character);
            if (defenseHostile != null)
            {
                this.AcquireAggro(defenseHostile, character, false);
            }

            ICharacter automaticTarget = this.capturedSubwayEncounters.FindAutomaticAggroTarget(character)
                                         ?? this.capturedTempleEncounters.FindAutomaticAggroTarget(character)
                                         ?? this.ordinaryEnemies.FindAutomaticAggroTarget(character)
                                         ?? AlexAreaMobRuntime.FindAutomaticAggroTarget(character)
                                         ?? LoreleiOasisMobRuntime.FindAutomaticAggroTarget(character)
                                         ?? AreteRoboticGuardDogRuntime.FindAutomaticAggroTarget(character)
                                         ?? ZoneEngine.Core.Missions.MissionInstanceMobCombat.FindAutomaticAggroTarget(
                                             character);
            if (automaticTarget != null)
            {
                this.AcquireAggro(automaticTarget, character);
            }

            if (character.FightingTarget.Instance != 0)
            {
                if (!missionStationary && character.Controller.IsFollowing())
                {
                    character.Controller.DoFollow();
                }

                return;
            }

            this.chaseNavigation.Clear(
                character.Identity.Instance,
                NpcChaseInvalidationReason.LeashReset);

            this.ordinaryEnemies.TryReturnToSpawn(character);

            if (character.Controller.IsFollowing())
            {
                character.Controller.DoFollow();
                return;
            }

            if (character.Controller.State == CharacterState.Patrolling)
            {
                character.Controller.StartPatrolling();
            }
        }

        internal void ClearCombatTracking(Identity identity)
        {
            this.ClearCombatTracking(identity, NpcChaseInvalidationReason.CombatCancelled);
        }

        private void ClearCombatTracking(
            Identity identity,
            NpcChaseInvalidationReason navigationReason)
        {
            this.combatTick.ClearTracking(identity);
            this.chaseNavigation.Clear(identity.Instance, navigationReason);
        }

        private void StartCombatWithAcquiredTarget(
            ICharacter attacker,
            ICharacter target,
            CapturedEnemyCombatContract capturedContract)
        {
            this.chaseNavigation.Clear(
                target.Identity.Instance,
                NpcChaseInvalidationReason.TargetReplaced);
            target.SetTarget(attacker.Identity);
            target.SetFightingTarget(attacker.Identity);

            NPCController npcController = target.Controller as NPCController;
            string stationaryReason;
            if (npcController != null
                && ZoneEngine.Core.Missions.MissionAcgSpatialRuntime
                    .RequiresStationaryNpc(target, attacker, out stationaryReason))
            {
                npcController.SnapshotCurrentMotionPosition();
                npcController.StopFollow();
                this.ResetCombatTick(target);
            }
            else if (npcController != null
                && capturedContract != null
                && capturedContract.HasCapturedCombatStopSequence)
            {
                this.ResetCombatTick(target);
            }
            else
            {
                if (npcController != null)
                {
                    npcController.StopFollowForCombatRange(attacker.Coordinates().coordinate);
                }

                this.ResetCombatTick(target);
            }

            this.capturedSubwayEncounters.NotifyCombatStarted(target, attacker, DateTime.UtcNow);
            this.capturedTempleEncounters.NotifyCombatStarted(target, attacker, DateTime.UtcNow);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "NPC combat engaged attacker={0} npc={1}",
                    attacker.Identity.ToString(true),
                    target.Identity.ToString(true)));
        }

        private bool TryBeginLeashReturn(ICharacter npc)
        {
            if (npc == null
                || npc.FightingTarget.Instance == 0
                || this.IsPlayerControlledPet(npc))
            {
                return false;
            }

            NpcHomeState home;
            if (!this.npcHomeStates.TryGetValue(npc.Identity.Instance, out home))
            {
                return false;
            }

            ICharacter target = this.dynelRegistry.FindByIdentity<ICharacter>(npc.FightingTarget);
            if (target == null
                || !NpcCombatLeashPolicy.ShouldResetCombat(
                    this.playfield.Identity.Instance,
                    false,
                    ToNavigationPoint(home.Coordinates.coordinate),
                    ToNavigationPoint(npc.Coordinates().coordinate),
                    ToNavigationPoint(target.Coordinates().coordinate),
                    home.MaximumNpcDistanceFromHome))
            {
                return false;
            }

            this.BeginLeashReturn(npc, home);
            return true;
        }

        private void BeginLeashReturn(ICharacter npc, NpcHomeState home)
        {
            bool wasFighting = npc.FightingTarget.Instance != 0;
            home.ReturningHome = true;
            npc.SetTarget(Identity.None);
            npc.SetFightingTarget(Identity.None);
            this.ClearCombatTracking(
                npc.Identity,
                NpcChaseInvalidationReason.LeashReset);

            NPCController controller = npc.Controller as NPCController;
            if (controller != null)
            {
                home.ControllerStateBeforeReturn = controller.State;
                controller.State = CharacterState.Idle;
                controller.SnapshotCurrentMotionPosition();
                controller.StopFollow();
            }

            if (wasFighting)
            {
                this.playfield.Announce(
                    new StopFightMessage
                    {
                        Identity = npc.Identity,
                        Unknown1 = 1
                    });
            }

            foreach (ICharacter summon in this.capturedSubwayEncounters.NotifyCombatReset(npc))
            {
                this.playfield.DespawnNpcImmediately(summon);
            }
            foreach (ICharacter summon in this.capturedTempleEncounters.NotifyCombatReset(npc))
            {
                this.playfield.DespawnNpcImmediately(summon);
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "NPC leash reset npc={0} home={1} position={2} maxDistance={3}",
                    npc.Identity,
                    home.Coordinates.coordinate,
                    npc.Coordinates().coordinate,
                    home.MaximumNpcDistanceFromHome));
        }

        private bool TryProcessLeashReturn(ICharacter npc, DateTime utcNow)
        {
            NpcHomeState home;
            if (npc == null
                || !this.npcHomeStates.TryGetValue(npc.Identity.Instance, out home)
                || !home.ReturningHome)
            {
                return false;
            }

            NPCController controller = npc.Controller as NPCController;
            if (controller == null)
            {
                return true;
            }

            if (controller.IsFollowing())
            {
                controller.DoFollow();
            }

            ChaseNavigationPoint homePoint = ToNavigationPoint(home.Coordinates.coordinate);
            ChaseNavigationPoint currentPoint = ToNavigationPoint(npc.Coordinates().coordinate);
            if (NpcCombatLeashPolicy.HasReturnedHome(homePoint, currentPoint))
            {
                home.ReturningHome = false;
                this.chaseNavigation.Clear(
                    npc.Identity.Instance,
                    NpcChaseInvalidationReason.LeashReset);
                controller.SnapshotCurrentMotionPosition();
                controller.StopFollow();
                controller.State = home.ControllerStateBeforeReturn;
                return false;
            }

            NpcChaseUpdateResult result = this.chaseNavigation.UpdateReturnToHome(
                npc.Identity.Instance,
                currentPoint,
                homePoint,
                NpcCombatLeashPolicy.ReturnNavigationStopDistance,
                utcNow);
            if (result.HasDestination
                && (result.ShouldIssueMovement || !controller.IsFollowing()))
            {
                controller.MoveTo(
                    new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                    {
                        X = (float)result.Destination.X,
                        Y = (float)result.Destination.Y,
                        Z = (float)result.Destination.Z
                    });
            }
            else if (result.Kind == NpcChaseMovementKind.Unavailable
                     || (result.Kind == NpcChaseMovementKind.Hold
                         && !this.chaseNavigation.HasActivePursuit(npc.Identity.Instance)))
            {
                controller.SnapshotCurrentMotionPosition();
                controller.StopFollow();
            }

            return true;
        }

        private bool IsPlayerControlledPet(ICharacter npc)
        {
            if (!PetCombatRules.IsPlayerOwnedPet(npc))
            {
                return false;
            }

            ICharacter owner = PetCombatRules.ResolvePetOwner(npc);
            return owner == null || owner.Controller is PlayerController;
        }

        private static ChaseNavigationPoint ToNavigationPoint(
            AORebirth.Core.Vector.Vector3 point)
        {
            return new ChaseNavigationPoint(point.x, point.y, point.z);
        }

        private void ScheduleNpcDeathCorpseSpawn(ICharacter target, Identity corpseIdentity)
        {
            if (corpseIdentity != Identity.None)
            {
                this.playfield.ScheduleCorpseSpawn(target, corpseIdentity);
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format("Skipping corpse visual spawn for {0}; no known MonsterData-to-CATMesh mapping.", target.Identity));
        }

        private void ScheduleDeadNpcDespawn(ICharacter target)
        {
            this.corpseLifecycle.ScheduleDeadNpcDespawn(target);
        }

        private static void LogCapturedAreteRobotContent(bool isError, string message)
        {
            LogUtil.Debug(isError ? DebugInfoDetail.Error : DebugInfoDetail.Engine, message);
        }

        private class NpcHomeState
        {
            public Coordinate Coordinates { get; set; }

            public double MaximumNpcDistanceFromHome { get; set; }

            public bool ReturningHome { get; set; }

            public CharacterState ControllerStateBeforeReturn { get; set; }
        }
    }
}
