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
            this.RegisterNpcHome(character);
        }

        internal void ClearRuntimeState()
        {
            this.chaseNavigation.ClearAll(NpcChaseInvalidationReason.PlayfieldReset);
            foreach (ICharacter character in this.dynelRegistry.Characters())
            {
                if (character.Controller is NPCController)
                {
                    this.combatTick.ClearTracking(character.Identity);
                }
            }

            this.worldPopulation.ClearPlayfield(this.playfield.Identity.Instance);
            this.ordinaryEnemies.ClearRuntimeState(this.playfield.Identity.Instance);
            this.chaseNavigation.ClearAll(NpcChaseInvalidationReason.EncounterReset);
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

            DateTime diedAtUtc = DateTime.UtcNow;
            Identity corpseIdentity = Identity.None;
            if (this.playfield.CanBuildKnownCorpseVisual(target))
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
            this.rewards.RunNpcDeathRewardHooks(attacker, target, this.playfield.AwardCombatXp);
            this.ScheduleNpcDeathCorpseSpawn(target, corpseIdentity);
            this.worldPopulation.NotifyDeath(target, corpseIdentity, diedAtUtc);

            foreach (ICharacter summon in this.capturedSubwayEncounters.NotifyDeath(target, diedAtUtc))
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
            this.ClearCombatTracking(
                character.Identity,
                NpcChaseInvalidationReason.TargetLost);
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

            DateTime utcNow = DateTime.UtcNow;
            if (this.TryBeginLeashReturn(character))
            {
                this.TryProcessLeashReturn(character, utcNow);
                return;
            }

            if (this.TryProcessLeashReturn(character, utcNow))
            {
                return;
            }

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
