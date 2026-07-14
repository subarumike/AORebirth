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

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class NPCRuntimeService
    {
        private readonly Playfield playfield;

        private readonly PlayfieldDynelRegistry dynelRegistry;

        private readonly PlayfieldRewardRuntimeService rewards;

        private readonly NpcCorpseLifecycleCoordinator corpseLifecycle;

        private readonly NpcCombatTickCoordinator combatTick;

        private readonly CapturedAreteRobotContentProvider capturedAreteRobotContent;

        private readonly CapturedSubwayContentProvider capturedSubwayContent;

        private readonly CapturedSubwayOrdinaryContentProvider capturedSubwayOrdinaryContent;

        private readonly OrdinaryEnemyCatalog ordinaryEnemyCatalog;

        private readonly NpcPatrolReplayCoordinator patrolReplay;

        private readonly CapturedAreteRobotSpawnOrchestrator capturedAreteRobotSpawns;

        private readonly OrdinaryEnemyRuntimeService ordinaryEnemies;

        private readonly WorldPopulationController worldPopulation;

        private readonly CapturedSubwayEncounterRuntimeService capturedSubwayEncounters;

        private readonly Dictionary<int, NpcHomeState> npcHomeStates = new Dictionary<int, NpcHomeState>();

        private readonly Dictionary<int, DateTime> corpseDespawnTicks = new Dictionary<int, DateTime>();

        internal NPCRuntimeService(
            Playfield playfield,
            PlayfieldDynelRegistry dynelRegistry,
            PlayfieldRewardRuntimeService rewards)
        {
            this.playfield = playfield;
            this.dynelRegistry = dynelRegistry;
            this.rewards = rewards ?? new PlayfieldRewardRuntimeService();
            this.corpseLifecycle = new NpcCorpseLifecycleCoordinator(playfield, this.RemoveNpcHome);
            this.combatTick = new NpcCombatTickCoordinator(playfield);
            this.capturedAreteRobotContent = new CapturedAreteRobotContentProvider(LogCapturedAreteRobotContent);
            this.capturedSubwayContent = new CapturedSubwayContentProvider();
            this.capturedSubwayOrdinaryContent = new CapturedSubwayOrdinaryContentProvider();
            this.ordinaryEnemyCatalog =
                new OrdinaryEnemyCatalog(
                    this.capturedSubwayContent,
                    this.capturedSubwayOrdinaryContent);
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
                    this.ActivateNpc);
            this.worldPopulation =
                new WorldPopulationController(this.playfield, this.ordinaryEnemyCatalog, this.ordinaryEnemies);
            this.capturedSubwayEncounters =
                new CapturedSubwayEncounterRuntimeService(
                    this.playfield,
                    this.dynelRegistry,
                    this.ActivateNpc);
        }

        internal void ActivateNpc(ICharacter character)
        {
            this.dynelRegistry.Register(character);
        }

        internal void ClearRuntimeState()
        {
            foreach (ICharacter character in this.dynelRegistry.Characters())
            {
                if (character.Controller is NPCController)
                {
                    this.combatTick.ClearTracking(character.Identity);
                }
            }

            this.worldPopulation.ClearPlayfield(this.playfield.Identity.Instance);
            this.ordinaryEnemies.ClearRuntimeState(this.playfield.Identity.Instance);
            this.capturedSubwayEncounters.ClearRuntimeState();
            this.npcHomeStates.Clear();
            this.corpseDespawnTicks.Clear();
        }

        internal void RegisterNpcHome(ICharacter character)
        {
            if (character == null || !(character.Controller is NPCController))
            {
                return;
            }

            this.npcHomeStates[character.Identity.Instance] =
                new NpcHomeState
                {
                    Coordinates = new Coordinate(character.Coordinates())
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
            this.worldPopulation.ActivatePlayfield(playfieldIdentity);
            this.capturedSubwayEncounters.ActivatePlayfield(playfieldIdentity);
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
            this.worldPopulation.ProcessDue(utcNow);
            this.capturedSubwayEncounters.ProcessDue(utcNow, this.AcquireAggro);
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

            Identity corpseIdentity = Identity.None;
            if (this.playfield.CanBuildKnownCorpseVisual(target))
            {
                corpseIdentity = this.playfield.AllocateCorpseIdentity();
            }

            this.playfield.MarkNpcDead(target);
            this.playfield.StopFightingDeadTarget(target.Identity);
            this.playfield.StopDyingNpcCombatState(target);
            this.playfield.SendNpcDeathAnimation(target);
            this.rewards.RunNpcDeathRewardHooks(attacker, target, this.playfield.AwardCombatXp);
            this.ScheduleNpcDeathCorpseSpawn(target, corpseIdentity);
            this.worldPopulation.NotifyDeath(target, corpseIdentity, DateTime.UtcNow);

            foreach (ICharacter summon in this.capturedSubwayEncounters.NotifyDeath(target))
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
            DateTime utcNow = DateTime.UtcNow;
            this.worldPopulation.NotifyNpcDespawn(target, utcNow);
            this.capturedSubwayEncounters.NotifyNpcDespawn(target, utcNow);
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
            if (this.capturedSubwayEncounters.IsCapturedNanoCastInProgress(attacker))
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
            this.ClearCombatTracking(character.Identity);
        }

        internal void StopDyingNpcCombatState(ICharacter target)
        {
            target.SetTarget(Identity.None);
            target.SetFightingTarget(Identity.None);
            this.ClearCombatTracking(target.Identity);

            NPCController npcController = target.Controller as NPCController;
            if (npcController != null)
            {
                npcController.SnapshotCurrentMotionPosition();
                npcController.StopFollow();
            }
        }

        internal void AcquireAggro(ICharacter attacker, ICharacter target)
        {
            NPCController npcController = target.Controller as NPCController;
            if (npcController == null)
            {
                return;
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
                || target.Stats[StatIds.health].Value <= 0
                || target.FightingTarget.Instance != 0)
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

            this.StartCombatWithAcquiredTarget(attacker, target, capturedContract);
        }

        internal void ProcessPatrolTick(ICharacter character)
        {
            if (character == null || !(character.Controller is NPCController))
            {
                return;
            }

            PetCommandService.ProcessPetHealTick(character);

            ICharacter automaticTarget = this.capturedSubwayEncounters.FindAutomaticAggroTarget(character)
                                         ?? this.ordinaryEnemies.FindAutomaticAggroTarget(character);
            if (automaticTarget != null)
            {
                this.AcquireAggro(automaticTarget, character);
            }

            if (character.FightingTarget.Instance != 0)
            {
                if (character.Controller.IsFollowing())
                {
                    character.Controller.DoFollow();
                }

                return;
            }

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
            this.combatTick.ClearTracking(identity);
        }

        private void StartCombatWithAcquiredTarget(
            ICharacter attacker,
            ICharacter target,
            CapturedEnemyCombatContract capturedContract)
        {
            target.SetTarget(attacker.Identity);
            target.SetFightingTarget(attacker.Identity);

            NPCController npcController = target.Controller as NPCController;
            if (npcController != null
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

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "NPC combat engaged attacker={0} npc={1}",
                    attacker.Identity.ToString(true),
                    target.Identity.ToString(true)));
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
        }
    }
}
