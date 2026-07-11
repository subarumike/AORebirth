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

        private readonly NpcPatrolReplayCoordinator patrolReplay;

        private readonly CapturedAreteRobotSpawnOrchestrator capturedAreteRobotSpawns;

        private readonly CapturedSubwaySpawnOrchestrator capturedSubwaySpawns;

        private readonly CapturedSubwayOrdinarySpawnOrchestrator capturedSubwayOrdinarySpawns;

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
            this.patrolReplay =
                new NpcPatrolReplayCoordinator(this.capturedAreteRobotContent, this.capturedSubwayContent);
            this.capturedAreteRobotSpawns =
                new CapturedAreteRobotSpawnOrchestrator(
                    this.capturedAreteRobotContent,
                    this.patrolReplay,
                    this.ActivateNpc);
            this.capturedSubwaySpawns =
                new CapturedSubwaySpawnOrchestrator(
                    this.capturedSubwayContent,
                    this.patrolReplay,
                    this.ActivateNpc);
            this.capturedSubwayOrdinarySpawns =
                new CapturedSubwayOrdinarySpawnOrchestrator(
                    this.capturedSubwayOrdinaryContent,
                    this.ActivateNpc);
        }

        internal void ActivateNpc(ICharacter character)
        {
            this.dynelRegistry.Register(character);
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
            this.capturedSubwaySpawns.SpawnForPlayfield(this.playfield, playfieldIdentity);
            this.capturedSubwayOrdinarySpawns.SpawnForPlayfield(this.playfield, playfieldIdentity);
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

        internal void ClearNpcCorpseDespawn(int corpseInstance)
        {
            this.corpseDespawnTicks.Remove(corpseInstance);
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
            this.corpseLifecycle.FinalizeNpcDespawn(target);
            this.dynelRegistry.Unregister(target.Identity);
            CapturedSubwayOrdinaryRuntimeRegistry.Remove(target.Identity.Instance);
        }

        internal void ResetCombatTick(ICharacter attacker)
        {
            this.combatTick.ResetCombatTick(attacker);
        }

        internal void ProcessCombatTick(ICharacter attacker)
        {
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
            if (npcController == null
                || npcController.KnuBot != null
                || !NpcAiProfiles.CanRetaliate(npcController.AiProfile)
                || target.Stats[StatIds.health].Value <= 0
                || target.FightingTarget.Instance != 0)
            {
                return;
            }

            this.StartCombatWithAcquiredTarget(attacker, target);
        }

        internal void ProcessPatrolTick(ICharacter character)
        {
            if (character == null || !(character.Controller is NPCController))
            {
                return;
            }

            PetCommandService.ProcessPetHealTick(character);

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

        private void StartCombatWithAcquiredTarget(ICharacter attacker, ICharacter target)
        {
            target.SetTarget(attacker.Identity);
            target.SetFightingTarget(attacker.Identity);
            this.ResetCombatTick(target);

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
