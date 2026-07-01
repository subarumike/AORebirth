namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    #endregion

    internal sealed class NpcCorpseLifecycleCoordinator
    {
        private readonly Dictionary<int, DateTime> deadNpcDespawnTicks = new Dictionary<int, DateTime>();

        private readonly Playfield playfield;

        internal NpcCorpseLifecycleCoordinator(Playfield playfield)
        {
            this.playfield = playfield;
        }

        internal bool HasPendingDeadNpcDespawn(Identity identity)
        {
            return this.deadNpcDespawnTicks.ContainsKey(identity.Instance);
        }

        internal void BeginNpcDeath(ICharacter attacker, ICharacter target)
        {
            if (!(target.Controller is NPCController)
                || this.deadNpcDespawnTicks.ContainsKey(target.Identity.Instance))
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
            if (attacker != null)
            {
                RexB18CObjectiveProgressTracker.TryObserveNpcDeath(attacker, target);
                this.playfield.AwardCombatXp(attacker, target);
            }

            if (corpseIdentity != Identity.None)
            {
                this.playfield.ScheduleCorpseSpawn(target, corpseIdentity);
            }
            else
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format("Skipping corpse visual spawn for {0}; no known MonsterData-to-CATMesh mapping.", target.Identity));
            }

            this.deadNpcDespawnTicks[target.Identity.Instance] =
                DateTime.UtcNow + NpcCorpseLifecycleRules.DeadNpcDespawnDelay;
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                PlayfieldLifecycleTrace.StageDeadNpcDespawnScheduled,
                "DeadNpcDespawnScheduled",
                target.Identity,
                "delayMs=" + ((int)NpcCorpseLifecycleRules.DeadNpcDespawnDelay.TotalMilliseconds));

            LogUtil.Debug(DebugInfoDetail.Network, string.Format("NPC died target={0}", target.Identity));
        }

        internal bool ProcessDeadNpc(ICharacter character)
        {
            if (!(character.Controller is NPCController)
                || character.Stats[StatIds.health].Value > 0)
            {
                return false;
            }

            DateTime despawnTick;
            if (!this.deadNpcDespawnTicks.TryGetValue(character.Identity.Instance, out despawnTick))
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
            target.DoNotDoTimers = true;
            this.playfield.ClearCombatTracking(target.Identity);
            this.deadNpcDespawnTicks.Remove(target.Identity.Instance);
            this.playfield.RemoveNpcHome(target.Identity);
            this.playfield.Despawn(target.Identity);
            Pool.Instance.RemoveObject((Character)target);

            LogUtil.Debug(DebugInfoDetail.Network, string.Format("NPC despawned target={0}", target.Identity));
        }
    }
}
